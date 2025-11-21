using hOps.web.Data;
using hOps.web.Models;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Services
{
    public interface IPropertyAccessService
    {
        Task<List<Property>> GetPropertiesForUserAsync(string userId);
        Task<HashSet<int>> GetPropertyIdsForUserAsync(string userId);
    }

    internal sealed class PropertyAccessService : IPropertyAccessService
    {
        private readonly ApplicationDbContext _context;

        public PropertyAccessService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Property>> GetPropertiesForUserAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<Property>();
            }

            var properties = await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == userId)
                .Include(upa => upa.Property)
                .Select(upa => upa.Property!)
                .Where(p => p != null)
                .Distinct()
                .OrderBy(p => p.Name)
                .ToListAsync();

            return properties;
        }

        public async Task<HashSet<int>> GetPropertyIdsForUserAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new HashSet<int>();
            }

            var ids = await _context.UserPropertyAccesses
                .Where(upa => upa.ApplicationUserId == userId)
                .Select(upa => upa.PropertyId)
                .ToListAsync();

            return ids.ToHashSet();
        }
    }
}
