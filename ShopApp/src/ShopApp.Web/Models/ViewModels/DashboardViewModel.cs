using ShopApp.Web.Models.Entities;

namespace ShopApp.Web.Models.ViewModels;

public class DashboardViewModel
{
    public Customer Customer { get; init; } = null!;

    // Order stats
    public int TotalOrders { get; init; }
    public decimal TotalSpend { get; init; }
    public decimal AverageOrderValue { get; init; }
    public int LateDeliveries { get; init; }

    // Recent orders (last 5)
    public IEnumerable<OrderSummary> RecentOrders { get; init; } = Enumerable.Empty<OrderSummary>();
}

public class OrderSummary
{
    public int OrderId { get; init; }
    public string OrderDate { get; init; } = string.Empty;
    public decimal OrderTotal { get; init; }
    public int ItemCount { get; init; }
    public string? Carrier { get; init; }
    public string? ShippingMethod { get; init; }
    public bool? IsLate { get; init; }
    public string DeliveryStatus { get; init; } = "Unknown";
}
