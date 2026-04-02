using Microsoft.EntityFrameworkCore;
using ShopApp.Web.Data;
using ShopApp.Web.Models.Entities;
using ShopApp.Web.Services.Interfaces;

namespace ShopApp.Web.Services.Implementations;

public class ProductService : IProductService
{
    private readonly ShopDbContext _db;
    public ProductService(ShopDbContext db) => _db = db;

    public async Task<IEnumerable<Product>> GetActiveProductsAsync(CancellationToken ct = default) =>
        await _db.Products
            .Where(p => p.IsActive == 1)
            .OrderBy(p => p.Category)
            .ThenBy(p => p.ProductName)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<Product?> GetByIdAsync(int productId, CancellationToken ct = default) =>
        await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == productId, ct);
}
