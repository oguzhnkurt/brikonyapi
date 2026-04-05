using BrikonYapi.Web.Data;
using BrikonYapi.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BrikonYapi.Web.Services
{
    public class ReferenceService
    {
        private readonly AppDbContext _db;
        public ReferenceService(AppDbContext db) => _db = db;

        public async Task<List<Reference>> GetAllAsync() =>
            await _db.References.OrderBy(r => r.OrderIndex).ThenBy(r => r.CreatedAt).ToListAsync();

        public async Task<List<Reference>> GetActiveAsync() =>
            await _db.References.Where(r => r.IsActive).OrderBy(r => r.OrderIndex).ThenBy(r => r.CreatedAt).ToListAsync();

        public async Task<Reference?> GetByIdAsync(int id) =>
            await _db.References.FindAsync(id);

        public async Task CreateAsync(Reference reference)
        {
            _db.References.Add(reference);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Reference reference)
        {
            _db.References.Update(reference);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var r = await _db.References.FindAsync(id);
            if (r != null) { _db.References.Remove(r); await _db.SaveChangesAsync(); }
        }
    }
}
