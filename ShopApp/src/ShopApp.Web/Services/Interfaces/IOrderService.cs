using ShopApp.Web.Models.DTOs;
using ShopApp.Web.Models.ViewModels;

namespace ShopApp.Web.Services.Interfaces;

public interface IOrderService
{
    Task<DashboardViewModel> GetDashboardAsync(int customerId, CancellationToken ct = default);

    Task<OrderHistoryViewModel> GetOrderHistoryAsync(int customerId, int page = 1, int pageSize = 20, CancellationToken ct = default);

    /// <summary>
    /// Persists a new order + its order_items + an initial shipment record.
    /// Returns the new order id.
    /// </summary>
    Task<int> PlaceOrderAsync(CreateOrderRequest request, CancellationToken ct = default);
}
