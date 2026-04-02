using System.ComponentModel.DataAnnotations;

namespace ShopApp.Web.Models.DTOs;

/// <summary>
/// Data Transfer Object for placing a new order via the UI form.
/// </summary>
public class CreateOrderRequest
{
    [Required]
    public int CustomerId { get; set; }

    [Required]
    public string PaymentMethod { get; set; } = "card";

    public string? PromoCode { get; set; }

    [Required]
    public string ShippingMethod { get; set; } = "standard";

    [Required]
    public string Carrier { get; set; } = "UPS";

    /// <summary>
    /// List of (product_id, quantity) pairs. At least one item is required.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one item is required.")]
    public List<OrderLineRequest> Lines { get; set; } = new();
}

public class OrderLineRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Required]
    [Range(1, 100)]
    public int Quantity { get; set; }
}
