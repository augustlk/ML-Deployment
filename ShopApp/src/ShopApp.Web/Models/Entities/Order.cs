using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopApp.Web.Models.Entities;

[Table("orders")]
public class Order
{
    [Key]
    [Column("order_id")]
    public int OrderId { get; set; }

    [Column("customer_id")]
    public int CustomerId { get; set; }

    /// <summary>Stored as TIMESTAMP in PostgreSQL.</summary>
    [Column("order_datetime")]
    public DateTime OrderDatetime { get; set; }

    [Column("billing_zip")]
    public string? BillingZip { get; set; }

    [Column("shipping_zip")]
    public string? ShippingZip { get; set; }

    [Column("shipping_state")]
    public string? ShippingState { get; set; }

    [Column("payment_method")]
    public string? PaymentMethod { get; set; }

    [Column("device_type")]
    public string? DeviceType { get; set; }

    [Column("ip_country")]
    public string? IpCountry { get; set; }

    [Column("promo_used")]
    public int PromoUsed { get; set; }

    [Column("promo_code")]
    public string? PromoCode { get; set; }

    [Column("order_subtotal")]
    public decimal OrderSubtotal { get; set; }

    [Column("shipping_fee")]
    public decimal ShippingFee { get; set; }

    [Column("tax_amount")]
    public decimal TaxAmount { get; set; }

    [Column("order_total")]
    public decimal OrderTotal { get; set; }

    [Column("risk_score")]
    public decimal RiskScore { get; set; }

    [Column("is_fraud")]
    public int IsFraud { get; set; }

    // Navigation
    public Customer? Customer { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public Shipment? Shipment { get; set; }
}
