using Microsoft.EntityFrameworkCore;
using ShopApp.Web.Models.Entities;

namespace ShopApp.Web.Data;

public class ShopDbContext : DbContext
{
    public ShopDbContext(DbContextOptions<ShopDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Order → Customer  (many-to-one)
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId);

        // Order → Shipment  (one-to-one)
        modelBuilder.Entity<Shipment>()
            .HasOne(s => s.Order)
            .WithOne(o => o.Shipment)
            .HasForeignKey<Shipment>(s => s.OrderId);

        // OrderItem → Order  (many-to-one)
        modelBuilder.Entity<OrderItem>()
            .HasOne(i => i.Order)
            .WithMany(o => o.OrderItems)
            .HasForeignKey(i => i.OrderId);

        // OrderItem → Product  (many-to-one)
        modelBuilder.Entity<OrderItem>()
            .HasOne(i => i.Product)
            .WithMany(p => p.OrderItems)
            .HasForeignKey(i => i.ProductId);

        // ProductReview → Customer / Product
        modelBuilder.Entity<ProductReview>()
            .HasOne(r => r.Customer)
            .WithMany(c => c.Reviews)
            .HasForeignKey(r => r.CustomerId);

        modelBuilder.Entity<ProductReview>()
            .HasOne(r => r.Product)
            .WithMany(p => p.Reviews)
            .HasForeignKey(r => r.ProductId);
    }
}
