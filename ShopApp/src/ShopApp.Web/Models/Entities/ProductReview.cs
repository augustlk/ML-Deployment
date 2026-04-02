using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopApp.Web.Models.Entities;

[Table("product_reviews")]
public class ProductReview
{
    [Key]
    [Column("review_id")]
    public int ReviewId { get; set; }

    [Column("customer_id")]
    public int CustomerId { get; set; }

    [Column("product_id")]
    public int ProductId { get; set; }

    [Column("rating")]
    public int Rating { get; set; }

    /// <summary>Stored as TIMESTAMP in PostgreSQL.</summary>
    [Column("review_datetime")]
    public DateTime? ReviewDatetime { get; set; }

    [Column("review_text")]
    public string? ReviewText { get; set; }

    // Navigation
    public Customer? Customer { get; set; }
    public Product? Product { get; set; }
}
