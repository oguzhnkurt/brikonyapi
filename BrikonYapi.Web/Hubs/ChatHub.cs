using System.Collections.Concurrent;
using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Hubs
{
    /// <summary>
    /// Proje bazlı malikler arası canlı sohbet.
    /// GÜVENLİK: İstemci hiçbir zaman doğrudan bir gruba katılamaz; her katılım ve her mesaj
    /// sunucu tarafında "bu kullanıcı bu projede bağımsız bölüm sahibi mi?" kontrolünden geçer.
    /// </summary>
    [Authorize(Roles = "KatMaliki,Admin")]
    public class ChatHub : Hub
    {
        private const int MaxMessageLength = 2000;
        private const int MaxPollOptions = 8;
        private const int MaxPollQuestionLength = 300;
        private const int MaxPollOptionLength = 200;

        // Basit hız sınırı: kullanıcı başına 10 saniyede en fazla 10 mesaj.
        private const int RateLimitWindowSeconds = 10;
        private const int RateLimitMaxMessages = 10;
        private static readonly ConcurrentDictionary<string, (DateTime WindowStart, int Count)> RateLimits = new();

        private readonly AppDbContext _db;
        private readonly UserManager<IdentityUser> _users;

        public ChatHub(AppDbContext db, UserManager<IdentityUser> users)
        {
            _db = db;
            _users = users;
        }

        private static string GroupName(int projectId) => $"project-{projectId}";

        private bool IsAdmin => Context.User?.IsInRole("Admin") == true;

        /// <summary>
        /// Kullanıcının bu proje sohbetine erişimi var mı? Malik ise admin'in kendisine açıkça
        /// verdiği sohbet erişimi (OwnerProjectAccess.CanChat) aranır — bölüm sahipliğinden
        /// bağımsızdır. Admin her projeye erişir.
        /// </summary>
        private async Task<(bool Allowed, Owner? Owner)> ResolveAccessAsync(int projectId)
        {
            var userId = _users.GetUserId(Context.User!);
            if (string.IsNullOrEmpty(userId)) return (false, null);

            if (IsAdmin) return (true, null);

            var owner = await _db.Owners.FirstOrDefaultAsync(o => o.UserId == userId);
            if (owner == null || !owner.IsActive) return (false, null);

            var allowed = await _db.OwnerProjectAccesses
                .AnyAsync(a => a.OwnerId == owner.Id && a.ProjectId == projectId && a.CanChat);
            return (allowed, allowed ? owner : null);
        }

        /// <summary>İstemci sohbeti açtığında çağrılır. Yetki yoksa gruba eklenmez.</summary>
        public async Task JoinProject(int projectId)
        {
            var (allowed, _) = await ResolveAccessAsync(projectId);
            if (!allowed)
                throw new HubException("Bu sohbete erişim yetkiniz yok.");

            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(projectId));
        }

        public async Task LeaveProject(int projectId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(projectId));
        }

        public async Task SendMessage(int projectId, string body)
        {
            var (allowed, owner) = await ResolveAccessAsync(projectId);
            if (!allowed)
                throw new HubException("Bu sohbete mesaj gönderme yetkiniz yok.");

            body = (body ?? string.Empty).Trim();
            if (body.Length == 0)
                throw new HubException("Boş mesaj gönderilemez.");

            if (body.Length > MaxMessageLength)
                throw new HubException($"Mesaj en fazla {MaxMessageLength} karakter olabilir.");

            var userId = _users.GetUserId(Context.User!)!;
            if (!PassesRateLimit(userId))
                throw new HubException("Çok hızlı mesaj gönderiyorsunuz, lütfen biraz bekleyin.");

            var senderName = IsAdmin ? "Yönetim" : (owner?.FullName ?? "Kat Maliki");

            var message = new ChatMessage
            {
                ProjectId = projectId,
                OwnerId = owner?.Id,
                SenderUserId = userId,
                SenderName = senderName,
                IsFromManagement = IsAdmin,
                Body = body,
                CreatedAt = DateTime.Now
            };

            _db.ChatMessages.Add(message);
            await _db.SaveChangesAsync();

            // Not: İstemci tarafında mesaj textContent ile basılır, innerHTML kullanılmaz (XSS önlemi).
            await Clients.Group(GroupName(projectId)).SendAsync("ReceiveMessage", new
            {
                id = message.Id,
                senderName = message.SenderName,
                senderUserId = message.SenderUserId,
                isFromManagement = message.IsFromManagement,
                body = message.Body,
                createdAt = message.CreatedAt.ToString("o")
            });
        }

        /// <summary>
        /// Yönetim, WhatsApp tarzı hızlı bir sohbet anketi açar. Anket, sohbet akışında normal
        /// bir mesaj gibi (IsPoll=true) görünür ve tüm bağlı istemcilere yayınlanır.
        /// </summary>
        public async Task CreatePoll(int projectId, string question, List<string> options)
        {
            var (allowed, _) = await ResolveAccessAsync(projectId);
            if (!allowed)
                throw new HubException("Bu sohbete erişim yetkiniz yok.");
            if (!IsAdmin)
                throw new HubException("Anket yalnızca yönetim tarafından oluşturulabilir.");

            question = (question ?? string.Empty).Trim();
            if (question.Length == 0)
                throw new HubException("Anket sorusu boş olamaz.");
            if (question.Length > MaxPollQuestionLength)
                throw new HubException($"Soru en fazla {MaxPollQuestionLength} karakter olabilir.");

            var cleanOptions = (options ?? new List<string>())
                .Select(o => (o ?? string.Empty).Trim())
                .Where(o => o.Length > 0)
                .Take(MaxPollOptions)
                .ToList();

            if (cleanOptions.Count < 2)
                throw new HubException("En az 2 seçenek girmelisiniz.");
            if (cleanOptions.Any(o => o.Length > MaxPollOptionLength))
                throw new HubException($"Seçenekler en fazla {MaxPollOptionLength} karakter olabilir.");

            var userId = _users.GetUserId(Context.User!)!;
            if (!PassesRateLimit(userId))
                throw new HubException("Çok hızlı işlem yapıyorsunuz, lütfen biraz bekleyin.");

            var poll = new ChatPoll { ProjectId = projectId, Question = question, CreatedAt = DateTime.Now };
            for (var i = 0; i < cleanOptions.Count; i++)
                poll.Options.Add(new ChatPollOption { Text = cleanOptions[i], OrderIndex = i });

            _db.ChatPolls.Add(poll);
            await _db.SaveChangesAsync();

            var message = new ChatMessage
            {
                ProjectId = projectId,
                OwnerId = null,
                SenderUserId = userId,
                SenderName = "Yönetim",
                IsFromManagement = true,
                IsPoll = true,
                ChatPollId = poll.Id,
                Body = question,
                CreatedAt = DateTime.Now
            };
            _db.ChatMessages.Add(message);
            await _db.SaveChangesAsync();

            await Clients.Group(GroupName(projectId)).SendAsync("ReceiveMessage", new
            {
                id = message.Id,
                senderName = message.SenderName,
                senderUserId = message.SenderUserId,
                isFromManagement = message.IsFromManagement,
                body = message.Body,
                createdAt = message.CreatedAt.ToString("o"),
                isPoll = true,
                poll = new
                {
                    id = poll.Id,
                    question = poll.Question,
                    totalVotes = 0,
                    options = poll.Options.OrderBy(o => o.OrderIndex)
                        .Select(o => new { id = o.Id, text = o.Text, count = 0, pct = 0 })
                }
            });
        }

        /// <summary>
        /// Bir malik sohbet anketinde seçeneğe dokunarak oy verir; daha sonra dokunup oyunu
        /// değiştirebilir. Güncel sonuçlar tüm bağlı istemcilere (yönetim dahil) yayınlanır.
        /// </summary>
        public async Task VotePoll(int pollId, int optionId)
        {
            var poll = await _db.ChatPolls.FirstOrDefaultAsync(p => p.Id == pollId);
            if (poll == null)
                throw new HubException("Anket bulunamadı.");

            var (allowed, owner) = await ResolveAccessAsync(poll.ProjectId);
            if (!allowed)
                throw new HubException("Bu sohbete erişim yetkiniz yok.");
            if (owner == null)
                throw new HubException("Yönetim oy kullanamaz.");

            var option = await _db.ChatPollOptions.FirstOrDefaultAsync(o => o.Id == optionId && o.ChatPollId == pollId);
            if (option == null)
                throw new HubException("Geçersiz seçenek.");

            var vote = await _db.ChatPollVotes.FirstOrDefaultAsync(v => v.ChatPollId == pollId && v.OwnerId == owner.Id);
            if (vote == null)
            {
                vote = new ChatPollVote { ChatPollId = pollId, OwnerId = owner.Id, ChatPollOptionId = optionId, CreatedAt = DateTime.Now };
                _db.ChatPollVotes.Add(vote);
            }
            else
            {
                vote.ChatPollOptionId = optionId;
                vote.UpdatedAt = DateTime.Now;
            }
            await _db.SaveChangesAsync();

            var options = await _db.ChatPollOptions.Where(o => o.ChatPollId == pollId).OrderBy(o => o.OrderIndex).ToListAsync();
            var counts = await _db.ChatPollVotes.Where(v => v.ChatPollId == pollId)
                .GroupBy(v => v.ChatPollOptionId)
                .Select(g => new { OptionId = g.Key, Count = g.Count() })
                .ToListAsync();
            var totalVotes = counts.Sum(c => c.Count);

            await Clients.Group(GroupName(poll.ProjectId)).SendAsync("ReceivePollUpdate", new
            {
                pollId = poll.Id,
                totalVotes,
                options = options.Select(o =>
                {
                    var c = counts.FirstOrDefault(x => x.OptionId == o.Id)?.Count ?? 0;
                    var pct = totalVotes > 0 ? (int)Math.Round(c * 100.0 / totalVotes) : 0;
                    return new { id = o.Id, count = c, pct };
                })
            });
        }

        private static bool PassesRateLimit(string userId)
        {
            var now = DateTime.UtcNow;
            var entry = RateLimits.AddOrUpdate(
                userId,
                _ => (now, 1),
                (_, prev) => (now - prev.WindowStart).TotalSeconds > RateLimitWindowSeconds
                    ? (now, 1)
                    : (prev.WindowStart, prev.Count + 1));

            return entry.Count <= RateLimitMaxMessages;
        }
    }
}
