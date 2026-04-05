using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Services
{
    public class ContactService
    {
        private readonly AppDbContext _db;
        public ContactService(AppDbContext db) => _db = db;

        public async Task SendAsync(ContactMessage msg) { _db.ContactMessages.Add(msg); await _db.SaveChangesAsync(); }
        public async Task<List<ContactMessage>> GetAllAsync() => await _db.ContactMessages.OrderByDescending(m => m.CreatedAt).ToListAsync();
        public async Task<int> GetUnreadCountAsync() => await _db.ContactMessages.CountAsync(m => !m.IsRead);
        public async Task MarkReadAsync(int id) { var m = await _db.ContactMessages.FindAsync(id); if (m != null) { m.IsRead = true; await _db.SaveChangesAsync(); } }
        public async Task DeleteAsync(int id) { var m = await _db.ContactMessages.FindAsync(id); if (m != null) { _db.ContactMessages.Remove(m); await _db.SaveChangesAsync(); } }
    }
}
