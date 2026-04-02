using Microsoft.EntityFrameworkCore;
using ShopApp.Web.Data;
using ShopApp.Web.Models.Entities;
using ShopApp.Web.Services.Implementations;
using Xunit;

namespace ShopApp.Tests.Services;

public class CustomerServiceTests : IDisposable
{
    private readonly ShopDbContext _db;
    private readonly CustomerService _sut;

    public CustomerServiceTests()
    {
        var opts = new DbContextOptionsBuilder<ShopDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ShopDbContext(opts);
        _sut = new CustomerService(_db);

        // Seed
        _db.Customers.AddRange(
            new Customer { CustomerId = 1, FullName = "Alice Smith",  Email = "alice@test.com",  IsActive = 1, CustomerSegment = "premium" },
            new Customer { CustomerId = 2, FullName = "Bob Jones",    Email = "bob@test.com",    IsActive = 1, CustomerSegment = "budget" },
            new Customer { CustomerId = 3, FullName = "Carol White",  Email = "carol@test.com",  IsActive = 0, CustomerSegment = "premium" }
        );
        _db.SaveChanges();
    }

    [Fact]
    public async Task GetAllActive_ReturnsOnlyActiveCustomers()
    {
        var result = await _sut.GetAllActiveAsync();
        Assert.Equal(2, result.Count());
        Assert.DoesNotContain(result, c => c.CustomerId == 3);
    }

    [Fact]
    public async Task GetAllActive_SearchByName_FiltersCorrectly()
    {
        var result = await _sut.GetAllActiveAsync(search: "alice");
        Assert.Single(result);
        Assert.Equal("Alice Smith", result.First().FullName);
    }

    [Fact]
    public async Task GetAllActive_SearchByEmail_FiltersCorrectly()
    {
        var result = await _sut.GetAllActiveAsync(search: "bob@test");
        Assert.Single(result);
        Assert.Equal(2, result.First().CustomerId);
    }

    [Fact]
    public async Task GetAllActive_SegmentFilter_FiltersCorrectly()
    {
        var result = await _sut.GetAllActiveAsync(segment: "premium");
        Assert.Single(result);   // Carol is inactive, only Alice
        Assert.Equal("Alice Smith", result.First().FullName);
    }

    [Fact]
    public async Task GetAllActive_NoFilter_ReturnsSortedByName()
    {
        var result = (await _sut.GetAllActiveAsync()).ToList();
        Assert.Equal("Alice Smith", result[0].FullName);
        Assert.Equal("Bob Jones",   result[1].FullName);
    }

    [Fact]
    public async Task GetSegments_ReturnsDistinctNonNullSegments()
    {
        var segs = (await _sut.GetSegmentsAsync()).ToList();
        Assert.Contains("premium", segs);
        Assert.Contains("budget", segs);
        Assert.Equal(segs.Distinct().Count(), segs.Count); // no duplicates
    }

    [Fact]
    public async Task GetById_ReturnsCustomer_WhenExists()
    {
        var c = await _sut.GetByIdAsync(1);
        Assert.NotNull(c);
        Assert.Equal("Alice Smith", c!.FullName);
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenNotExists()
    {
        var c = await _sut.GetByIdAsync(999);
        Assert.Null(c);
    }

    public void Dispose() => _db.Dispose();
}
