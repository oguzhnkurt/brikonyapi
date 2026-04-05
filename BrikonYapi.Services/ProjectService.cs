using BrikonYapi.Data;
using BrikonYapi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Services
{
    public class ProjectService
    {
        private readonly AppDbContext _context;

        public ProjectService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Project>> GetFeaturedProjectsAsync(ProjectStatus? status = null)
        {
            var query = _context.Projects
                .Include(p => p.Images)
                .Where(p => p.IsActive);

            if (status.HasValue)
                query = query.Where(p => p.Status == status.Value);

            return await query.OrderBy(p => p.OrderIndex).ThenByDescending(p => p.CreatedAt).ToListAsync();
        }

        public async Task<List<Project>> GetAllActiveAsync(ProjectStatus? status = null)
        {
            var query = _context.Projects
                .Include(p => p.Images)
                .Where(p => p.IsActive);

            if (status.HasValue)
                query = query.Where(p => p.Status == status.Value);

            return await query.OrderBy(p => p.OrderIndex).ThenByDescending(p => p.CreatedAt).ToListAsync();
        }

        public async Task<Project?> GetBySlugAsync(string slug)
        {
            return await _context.Projects
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive);
        }

        public async Task<Project?> GetByIdAsync(int id)
        {
            return await _context.Projects
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<List<HeroSlide>> GetActiveHeroSlidesAsync()
        {
            return await _context.HeroSlides
                .Include(h => h.Project)
                .Where(h => h.IsActive)
                .OrderBy(h => h.OrderIndex)
                .ToListAsync();
        }

        public async Task CreateProjectAsync(Project project)
        {
            _context.Projects.Add(project);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateProjectAsync(Project project)
        {
            project.UpdatedAt = DateTime.Now;
            _context.Projects.Update(project);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteProjectAsync(int id)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project != null)
            {
                _context.Projects.Remove(project);
                await _context.SaveChangesAsync();
            }
        }

        public async Task AddImageAsync(ProjectImage image)
        {
            _context.ProjectImages.Add(image);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteImageAsync(int imageId)
        {
            var image = await _context.ProjectImages.FindAsync(imageId);
            if (image != null)
            {
                _context.ProjectImages.Remove(image);
                await _context.SaveChangesAsync();
            }
        }

        public string GenerateSlug(string name)
        {
            var slug = name.ToLower()
                .Replace(" ", "-")
                .Replace("ğ", "g").Replace("ü", "u").Replace("ş", "s")
                .Replace("ı", "i").Replace("ö", "o").Replace("ç", "c")
                .Replace("Ğ", "g").Replace("Ü", "u").Replace("Ş", "s")
                .Replace("İ", "i").Replace("Ö", "o").Replace("Ç", "c");

            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-").Trim('-');
            return slug;
        }
    }
}
