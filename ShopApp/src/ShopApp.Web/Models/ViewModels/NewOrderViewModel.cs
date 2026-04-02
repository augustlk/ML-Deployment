using ShopApp.Web.Models.Entities;

namespace ShopApp.Web.Models.ViewModels;

public class NewOrderViewModel
{
    public Customer Customer { get; init; } = null!;
    public IEnumerable<Product> Products { get; init; } = Enumerable.Empty<Product>();
    public IEnumerable<string> Categories { get; init; } = Enumerable.Empty<string>();

    public static readonly string[] PaymentMethods = ["card", "bank", "paypal", "crypto"];
    public static readonly string[] ShippingMethods = ["standard", "expedited", "overnight"];
    public static readonly string[] Carriers = ["UPS", "FedEx", "USPS"];
}
