using Microsoft.EntityFrameworkCore;
using ShopApp.Web.Data;
using ShopApp.Web.Models.Entities;
using ShopApp.Web.Services.Interfaces;

namespace ShopApp.Web.Services.Implementations;

public class CustomerService : ICustomerService
{
    private readonly ShopDbContext _db;

    public CustomerService(ShopDbContext db) => _db = db;

    public async Task<IEnumerable<Customer>> GetAllActiveAsync(
        string? search = null,
        string? segment = null,
        CancellationToken ct = default)
    {
        var query = _db.Customers.Where(c => c.IsActive == 1);

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim().ToLower();
            query = query.Where(c =>
                c.FullName.ToLower().Contains(search) ||
                (c.Email != null && c.Email.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(segment))
            query = query.Where(c => c.CustomerSegment == segment);

        return await query
            .OrderBy(c => c.FullName)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<string>> GetSegmentsAsync(CancellationToken ct = default) =>
        await _db.Customers
            .Where(c => c.CustomerSegment != null)
            .Select(c => c.CustomerSegment!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);

    public async Task<Customer?> GetByIdAsync(int customerId, CancellationToken ct = default) =>
        await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
}
