using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopApp.Web.Models.Entities;

[Table("shipments")]
public class Shipment
{
    [Key]
    [Column("shipment_id")]
    public int ShipmentId { get; set; }

    [Column("order_id")]
    public int OrderId { get; set; }

    /// <summary>Stored as TIMESTAMP in PostgreSQL. Null for unshipped orders.</summary>
    [Column("ship_datetime")]
    public DateTime? ShipDatetime { get; set; }

    [Column("carrier")]
    public string? Carrier { get; set; }

    [Column("shipping_method")]
    public string? ShippingMethod { get; set; }

    [Column("distance_band")]
    public string? DistanceBand { get; set; }

    [Column("promised_days")]
    public int? PromisedDays { get; set; }

    [Column("actual_days")]
    public int? ActualDays { get; set; }

    /// <summary>
    /// Ground-truth late flag (1 = late). NULL for shipments not yet delivered.
    /// </summary>
    [Column("late_delivery")]
    public int? LateDelivery { get; set; }

    // Navigation
    public Order? Order { get; set; }
}
