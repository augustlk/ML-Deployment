using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopApp.Web.Models.Entities;

[Table("customers")]
public class Customer
{
    [Key]
    [Column("customer_id")]
    public int CustomerId { get; set; }

    [Column("full_name")]
    [Required]
    public string FullName { get; set; } = string.Empty;

    [Column("email")]
    public string? Email { get; set; }

    [Column("gender")]
    public string? Gender { get; set; }

    /// <summary>Stored as DATE in PostgreSQL (no time component).</summary>
    [Column("birthdate")]
    public DateOnly? Birthdate { get; set; }

    /// <summary>Stored as TIMESTAMP in PostgreSQL.</summary>
    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("city")]
    public string? City { get; set; }

    [Column("state")]
    public string? State { get; set; }

    [Column("zip_code")]
    public string? ZipCode { get; set; }

    [Column("customer_segment")]
    public string? CustomerSegment { get; set; }

    [Column("loyalty_tier")]
    public string? LoyaltyTier { get; set; }

    [Column("is_active")]
    public int IsActive { get; set; }

    // Navigation
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<ProductReview> Reviews { get; set; } = new List<ProductReview>();
}
