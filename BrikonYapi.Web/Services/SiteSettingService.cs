using BrikonYapi.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Services
{
    public class SiteSettingService
    {
        private readonly AppDbContext _db;
        public SiteSettingService(AppDbContext db) => _db = db;

        public async Task<Dictionary<string, string?>> GetAllAsync() =>
            (await _db.SiteSettings.ToListAsync()).ToDictionary(s => s.Key, s => s.Value);

        public async Task<string?> GetAsync(string key) =>
            (await _db.SiteSettings.FirstOrDefaultAsync(s => s.Key == key))?.Value;

        public async Task SaveAllAsync(Dictionary<string, string> values)
        {
            foreach (var kv in values)
            {
                var s = await _db.SiteSettings.FirstOrDefaultAsync(x => x.Key == kv.Key);
                if (s != null) { s.Value = kv.Value; }
            }
            await _db.SaveChangesAsync();
        }
    }
}
