using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShopApp.Web.Data;
using ShopApp.Web.Models.DTOs;
using ShopApp.Web.Models.Entities;
using ShopApp.Web.Services.Implementations;
using Xunit;

namespace ShopApp.Tests.Services;

public class OrderServiceTests : IDisposable
{
    private readonly ShopDbContext _db;
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        var opts = new DbContextOptionsBuilder<ShopDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ShopDbContext(opts);
        _sut = new OrderService(_db, NullLogger<OrderService>.Instance);

        // Seed
        _db.Customers.Add(new Customer
        {
            CustomerId = 1, FullName = "Alice Smith", Email = "alice@test.com",
            IsActive = 1, ZipCode = "12345", State = "CA", CustomerSegment = "premium"
        });
        _db.Products.AddRange(
            new Product { ProductId = 10, ProductName = "Widget A", Price = 25.00m, Cost = 10m, IsActive = 1, Sku = "SKU-010" },
            new Product { ProductId = 11, ProductName = "Gadget B", Price = 50.00m, Cost = 20m, IsActive = 1, Sku = "SKU-011" },
            new Product { ProductId = 99, ProductName = "Discontinued", Price = 1.00m,  Cost = 0.5m, IsActive = 0, Sku = "SKU-099" }
        );
        _db.SaveChanges();
    }

    // ── PlaceOrderAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrder_CreatesOrderAndShipment()
    {
        var req = new CreateOrderRequest
        {
            CustomerId = 1,
            PaymentMethod = "card",
            ShippingMethod = "standard",
            Carrier = "UPS",
            Lines = [new OrderLineRequest { ProductId = 10, Quantity = 2 }]
        };

        var orderId = await _sut.PlaceOrderAsync(req);

        Assert.True(orderId > 0);
        var order = _db.Orders.Include(o => o.OrderItems).First(o => o.OrderId == orderId);
        Assert.Equal(1, order.CustomerId);
        Assert.Single(order.OrderItems);
        Assert.Equal(2, order.OrderItems.First().Quantity);

        var shipment = _db.Shipments.First(s => s.OrderId == orderId);
        Assert.Null(shipment.ActualDays);    // pending
        Assert.Null(shipment.LateDelivery);  // unknown yet
        Assert.Equal("UPS", shipment.Carrier);
        Assert.Equal("standard", shipment.ShippingMethod);
    }

    [Fact]
    public async Task PlaceOrder_CalculatesTotalsFromServerSidePrices()
    {
        var req = new CreateOrderRequest
        {
            CustomerId = 1,
            PaymentMethod = "card",
            ShippingMethod = "expedited",
            Carrier = "FedEx",
            Lines = [new OrderLineRequest { ProductId = 10, Quantity = 1 },
                     new OrderLineRequest { ProductId = 11, Quantity = 2 }]
        };

        var orderId = await _sut.PlaceOrderAsync(req);
        var order = _db.Orders.First(o => o.OrderId == orderId);

        // Subtotal = 25 + 100 = 125
        Assert.Equal(125.00m, order.OrderSubtotal);
        Assert.Equal(14.99m, order.ShippingFee);                        // expedited
        Assert.Equal(Math.Round(125m * 0.08m, 2), order.TaxAmount);     // 10.00
        Assert.Equal(125m + 14.99m + Math.Round(125m * 0.08m, 2), order.OrderTotal);
    }

    [Fact]
    public async Task PlaceOrder_ThrowsWhenProductInactive()
    {
        var req = new CreateOrderRequest
        {
            CustomerId = 1,
            ShippingMethod = "standard",
            Carrier = "UPS",
            Lines = [new OrderLineRequest { ProductId = 99, Quantity = 1 }]
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.PlaceOrderAsync(req));
    }

    [Fact]
    public async Task PlaceOrder_ThrowsWhenProductNotFound()
    {
        var req = new CreateOrderRequest
        {
            CustomerId = 1,
            ShippingMethod = "standard",
            Carrier = "UPS",
            Lines = [new OrderLineRequest { ProductId = 9999, Quantity = 1 }]
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.PlaceOrderAsync(req));
    }

    [Fact]
    public async Task PlaceOrder_ThrowsWhenCustomerNotFound()
    {
        var req = new CreateOrderRequest
        {
            CustomerId = 9999,
            ShippingMethod = "standard",
            Carrier = "UPS",
            Lines = [new OrderLineRequest { ProductId = 10, Quantity = 1 }]
        };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.PlaceOrderAsync(req));
    }

    [Theory]
    [InlineData("standard", 5.99)]
    [InlineData("expedited", 14.99)]
    [InlineData("overnight", 24.99)]
    public async Task PlaceOrder_AppliesCorrectShippingFee(string method, decimal expectedFee)
    {
        var req = new CreateOrderRequest
        {
            CustomerId = 1,
            ShippingMethod = method,
            Carrier = "UPS",
            Lines = [new OrderLineRequest { ProductId = 10, Quantity = 1 }]
        };

        var orderId = await _sut.PlaceOrderAsync(req);
        var order = _db.Orders.First(o => o.OrderId == orderId);
        Assert.Equal(expectedFee, order.ShippingFee);
    }

    // ── GetDashboardAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboard_ThrowsWhenCustomerNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.GetDashboardAsync(999));
    }

    [Fact]
    public async Task GetDashboard_ReturnsZeroStatsForNewCustomer()
    {
        var vm = await _sut.GetDashboardAsync(1);
        Assert.Equal(0, vm.TotalOrders);
        Assert.Equal(0, vm.TotalSpend);
        Assert.Empty(vm.RecentOrders);
    }

    // ── GetOrderHistoryAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetOrderHistory_ThrowsWhenCustomerNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.GetOrderHistoryAsync(999));
    }

    [Fact]
    public async Task GetOrderHistory_PaginatesCorrectly()
    {
        // Place 5 orders
        for (int i = 0; i < 5; i++)
        {
            var req = new CreateOrderRequest
            {
                CustomerId = 1, ShippingMethod = "standard", Carrier = "UPS",
                Lines = [new OrderLineRequest { ProductId = 10, Quantity = 1 }]
            };
            await _sut.PlaceOrderAsync(req);
        }

        var page1 = await _sut.GetOrderHistoryAsync(1, page: 1, pageSize: 3);
        var page2 = await _sut.GetOrderHistoryAsync(1, page: 2, pageSize: 3);

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.Orders.Count());
        Assert.Equal(2, page2.Orders.Count());
        Assert.Equal(2, page1.TotalPages);
    }

    public void Dispose() => _db.Dispose();
}
