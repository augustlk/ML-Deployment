using ShopApp.Web.Models.Entities;

namespace ShopApp.Web.Services.Interfaces;

public interface ICustomerService
{
    Task<IEnumerable<Customer>> GetAllActiveAsync(string? search = null, string? segment = null, CancellationToken ct = default);
    Task<IEnumerable<string>> GetSegmentsAsync(CancellationToken ct = default);
    Task<Customer?> GetByIdAsync(int customerId, CancellationToken ct = default);
}
