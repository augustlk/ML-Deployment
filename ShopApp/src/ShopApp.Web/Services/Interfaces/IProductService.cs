using ShopApp.Web.Models.Entities;

namespace ShopApp.Web.Services.Interfaces;

public interface IProductService
{
    Task<IEnumerable<Product>> GetActiveProductsAsync(CancellationToken ct = default);
    Task<Product?> GetByIdAsync(int productId, CancellationToken ct = default);
}
