using ShopApp.Web.Models.Entities;

namespace ShopApp.Web.Models.ViewModels;

public class CustomerSelectViewModel
{
    public IEnumerable<Customer> Customers { get; init; } = Enumerable.Empty<Customer>();
    public string? SearchQuery { get; init; }
    public string? SegmentFilter { get; init; }
    public IEnumerable<string> Segments { get; init; } = Enumerable.Empty<string>();
}
