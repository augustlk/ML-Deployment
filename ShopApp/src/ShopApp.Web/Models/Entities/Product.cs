using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopApp.Web.Models.Entities;

[Table("products")]
public class Product
{
    [Key]
    [Column("product_id")]
    public int ProductId { get; set; }

    [Column("sku")]
    public string? Sku { get; set; }

    [Column("product_name")]
    [Required]
    public string ProductName { get; set; } = string.Empty;

    [Column("category")]
    public string? Category { get; set; }

    [Column("price")]
    public decimal Price { get; set; }

    [Column("cost")]
    public decimal Cost { get; set; }

    [Column("is_active")]
    public int IsActive { get; set; }

    // Navigation
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<ProductReview> Reviews { get; set; } = new List<ProductReview>();
}
