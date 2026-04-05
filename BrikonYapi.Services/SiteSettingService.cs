using BrikonYapi.Data;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Services
{
    public class SiteSettingService
    {
        private readonly AppDbContext _context;

        public SiteSettingService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<string?> GetValueAsync(string key)
        {
            var setting = await _context.SiteSettings.FirstOrDefaultAsync(s => s.Key == key);
            return setting?.Value;
        }

        public async Task<Dictionary<string, string?>> GetAllAsync()
        {
            var settings = await _context.SiteSettings.ToListAsync();
            return settings.ToDictionary(s => s.Key, s => s.Value);
        }

        public async Task SetValueAsync(string key, string value)
        {
            var setting = await _context.SiteSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (setting != null)
            {
                setting.Value = value;
                await _context.SaveChangesAsync();
            }
        }

        public async Task SaveAllAsync(Dictionary<string, string> values)
        {
            foreach (var kv in values)
            {
                await SetValueAsync(kv.Key, kv.Value);
            }
        }
    }
}
