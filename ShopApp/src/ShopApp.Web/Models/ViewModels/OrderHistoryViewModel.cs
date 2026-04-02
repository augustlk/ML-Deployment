using ShopApp.Web.Models.Entities;

namespace ShopApp.Web.Models.ViewModels;

public class OrderHistoryViewModel
{
    public Customer Customer { get; init; } = null!;
    public IEnumerable<OrderDetailRow> Orders { get; init; } = Enumerable.Empty<OrderDetailRow>();
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class OrderDetailRow
{
    public int OrderId { get; init; }
    public string OrderDate { get; init; } = string.Empty;
    public decimal OrderTotal { get; init; }
    public string? PaymentMethod { get; init; }
    public string? PromoCode { get; init; }
    public int ItemCount { get; init; }
    public string? Carrier { get; init; }
    public string? ShippingMethod { get; init; }
    public int? PromisedDays { get; init; }
    public int? ActualDays { get; init; }
    public bool? IsLate { get; init; }
    public List<OrderItemDetail> Items { get; init; } = new();
}

public class OrderItemDetail
{
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }
}
