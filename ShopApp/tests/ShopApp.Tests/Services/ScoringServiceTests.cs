using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using ShopApp.Web.Data;
using ShopApp.Web.Models.Entities;
using ShopApp.Web.Services.Implementations;

namespace ShopApp.Tests.Services;

public class ScoringServiceTests : IDisposable
{
    private readonly ShopDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly RuleBasedScoringService _sut;

    public ScoringServiceTests()
    {
        var opts = new DbContextOptionsBuilder<ShopDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ShopDbContext(opts);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _sut = new RuleBasedScoringService(_db, _cache, NullLogger<RuleBasedScoringService>.Instance);

        SeedDatabase();
    }

    private void SeedDatabase()
    {
        _db.Customers.AddRange(
            new Customer { CustomerId = 1, FullName = "Alice Smith",  IsActive = 1 },
            new Customer { CustomerId = 2, FullName = "Bob Jones",    IsActive = 1 }
        );
        _db.Products.Add(new Product { ProductId = 1, ProductName = "Widget", Price = 10m, Cost = 5m, IsActive = 1, Sku = "W1" });

        _db.Orders.AddRange(
            new Order { OrderId = 1, CustomerId = 1, OrderDatetime = new DateTime(2025, 1, 1), OrderTotal = 100m, RiskScore = 90 },
            new Order { OrderId = 2, CustomerId = 2, OrderDatetime = new DateTime(2025, 1, 2), OrderTotal = 50m,  RiskScore = 10 }
        );
        _db.Shipments.AddRange(
            // High-risk shipment: national, overnight, high risk score
            new Shipment { ShipmentId = 1, OrderId = 1, Carrier = "USPS", ShippingMethod = "overnight", DistanceBand = "national", PromisedDays = 1 },
            // Low-risk shipment: local, standard, low risk score
            new Shipment { ShipmentId = 2, OrderId = 2, Carrier = "UPS",  ShippingMethod = "standard",  DistanceBand = "local",    PromisedDays = 6 }
        );
        _db.SaveChanges();
    }

    [Fact]
    public void ModelName_ReturnsNonEmptyString()
    {
        Assert.False(string.IsNullOrWhiteSpace(_sut.ModelName));
    }

    [Fact]
    public void LastRunAt_IsNullBeforeFirstRun()
    {
        Assert.Null(_sut.LastRunAt);
    }

    [Fact]
    public async Task RunScoring_SetsLastRunAt()
    {
        await _sut.RunScoringAsync();
        Assert.NotNull(_sut.LastRunAt);
        Assert.True(_sut.LastRunAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task RunScoring_ProducesScoresForAllOrders()
    {
        await _sut.RunScoringAsync();
        var top = (await _sut.GetTopAtRiskAsync(100)).ToList();
        Assert.Equal(2, top.Count);
    }

    [Fact]
    public async Task GetTopAtRisk_RanksHighRiskFirstOverLowRisk()
    {
        await _sut.RunScoringAsync();
        var top = (await _sut.GetTopAtRiskAsync(2)).ToList();

        // Order 1 has risk_score=90, national, overnight, USPS → should rank first
        Assert.Equal(1, top[0].OrderId);
        Assert.True(top[0].LateDeliveryProbability > top[1].LateDeliveryProbability);
    }

    [Fact]
    public async Task GetTopAtRisk_LimitsResultsToTopN()
    {
        // Seed 10 extra orders
        for (int i = 3; i <= 12; i++)
        {
            _db.Orders.Add(new Order   { OrderId = i, CustomerId = 1, OrderDatetime = new DateTime(2025, 1, 1), OrderTotal = 10m, RiskScore = 50 });
            _db.Shipments.Add(new Shipment { ShipmentId = i, OrderId = i, Carrier = "UPS", ShippingMethod = "standard", DistanceBand = "local", PromisedDays = 5 });
        }
        await _db.SaveChangesAsync();
        await _sut.RunScoringAsync();

        var top3 = (await _sut.GetTopAtRiskAsync(3)).ToList();
        Assert.Equal(3, top3.Count);
    }

    [Fact]
    public async Task AllScores_AreInValidRange()
    {
        await _sut.RunScoringAsync();
        var scores = await _sut.GetTopAtRiskAsync(100);
        Assert.All(scores, s => Assert.InRange(s.LateDeliveryProbability, 0f, 1f));
    }

    [Fact]
    public async Task GetTopAtRisk_TriggersAutoScoringIfNeverRun()
    {
        // Do NOT call RunScoring — GetTopAtRisk should auto-trigger it
        var top = (await _sut.GetTopAtRiskAsync(50)).ToList();
        Assert.NotEmpty(top);
        Assert.NotNull(_sut.LastRunAt);
    }

    public void Dispose()
    {
        _db.Dispose();
        _cache.Dispose();
    }
}
