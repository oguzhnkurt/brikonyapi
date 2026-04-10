using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Services
{
    public class ProjectService
    {
        private readonly AppDbContext _db;
        public ProjectService(AppDbContext db) => _db = db;

        public async Task<List<Project>> GetAllActiveAsync(ProjectStatus? status = null)
        {
            var q = _db.Projects.Include(p => p.Images).Where(p => p.IsActive);
            if (status.HasValue) q = q.Where(p => p.Status == status.Value);
            return await q
                .OrderBy(p => p.OrderIndex)
                .ToListAsync();
        }

        public async Task<List<Project>> GetAllAsync()
        {
            return await _db.Projects.Include(p => p.Images)
                .OrderBy(p => p.OrderIndex)
                .ToListAsync();
        }

        public async Task<Project?> GetBySlugAsync(string slug) =>
            await _db.Projects.Include(p => p.Images).FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive);

        public async Task<Project?> GetByIdAsync(int id) =>
            await _db.Projects.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);

        public async Task<List<HeroSlide>> GetActiveHeroSlidesAsync() =>
            await _db.HeroSlides.Include(h => h.Project).Where(h => h.IsActive).OrderBy(h => h.OrderIndex).ToListAsync();

        public async Task<List<Project>> GetFeaturedAsync() =>
            await _db.Projects.Include(p => p.Images)
                .Where(p => p.IsActive && p.IsFeatured)
                .OrderBy(p => p.OrderIndex)
                .ThenByDescending(p => p.CreatedAt)
                .ToListAsync();

        public async Task CreateAsync(Project p) { _db.Projects.Add(p); await _db.SaveChangesAsync(); }

        public async Task UpdateAsync(Project p) { p.UpdatedAt = DateTime.Now; _db.Projects.Update(p); await _db.SaveChangesAsync(); }

        public async Task DeleteAsync(int id)
        {
            var p = await _db.Projects.FindAsync(id);
            if (p != null) { _db.Projects.Remove(p); await _db.SaveChangesAsync(); }
        }

        public async Task AddImageAsync(ProjectImage img) { _db.ProjectImages.Add(img); await _db.SaveChangesAsync(); }

        public async Task DeleteImageAsync(int imgId)
        {
            var img = await _db.ProjectImages.FindAsync(imgId);
            if (img != null) { _db.ProjectImages.Remove(img); await _db.SaveChangesAsync(); }
        }

        public string GenerateSlug(string name) =>
            System.Text.RegularExpressions.Regex.Replace(
                name.ToLower()
                    .Replace("ğ","g").Replace("ü","u").Replace("ş","s")
                    .Replace("ı","i").Replace("ö","o").Replace("ç","c")
                    .Replace(" ", "-"),
                @"[^a-z0-9\-]", "").Trim('-');
    }
}
