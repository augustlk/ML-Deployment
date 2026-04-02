using Microsoft.EntityFrameworkCore;
using ShopApp.Web.Data;
using ShopApp.Web.Models.DTOs;
using ShopApp.Web.Models.Entities;
using ShopApp.Web.Models.ViewModels;
using ShopApp.Web.Services.Interfaces;

namespace ShopApp.Web.Services.Implementations;

public class OrderService : IOrderService
{
    private readonly ShopDbContext _db;
    private readonly ILogger<OrderService> _logger;

    public OrderService(ShopDbContext db, ILogger<OrderService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── Dashboard ────────────────────────────────────────────────────────────

    public async Task<DashboardViewModel> GetDashboardAsync(int customerId, CancellationToken ct = default)
    {
        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct)
            ?? throw new KeyNotFoundException($"Customer {customerId} not found.");

        // Aggregate stats in one query
        var stats = await _db.Orders
            .Where(o => o.CustomerId == customerId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Spend = g.Sum(o => o.OrderTotal),
                Avg = g.Average(o => o.OrderTotal)
            })
            .FirstOrDefaultAsync(ct);

        var lateCount = await _db.Shipments
            .Where(s => s.Order!.CustomerId == customerId && s.LateDelivery == 1)
            .CountAsync(ct);

        // Recent 5 orders with shipment info
        var recent = await _db.Orders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.OrderDatetime)
            .Take(5)
            .Select(o => new
            {
                o.OrderId,
                o.OrderDatetime,
                o.OrderTotal,
                ItemCount = o.OrderItems.Count(),
                Carrier = o.Shipment != null ? o.Shipment.Carrier : null,
                ShippingMethod = o.Shipment != null ? o.Shipment.ShippingMethod : null,
                LateDelivery = o.Shipment != null ? o.Shipment.LateDelivery : (int?)null,
                ActualDays = o.Shipment != null ? o.Shipment.ActualDays : (int?)null
            })
            .AsNoTracking()
            .ToListAsync(ct);

        return new DashboardViewModel
        {
            Customer = customer,
            TotalOrders = stats?.Total ?? 0,
            TotalSpend = stats?.Spend ?? 0,
            AverageOrderValue = stats?.Avg ?? 0,
            LateDeliveries = lateCount,
            RecentOrders = recent.Select(r => new OrderSummary
            {
                OrderId = r.OrderId,
                OrderDate = r.OrderDatetime.ToString("yyyy-MM-dd HH:mm:ss"),
                OrderTotal = r.OrderTotal,
                ItemCount = r.ItemCount,
                Carrier = r.Carrier,
                ShippingMethod = r.ShippingMethod,
                IsLate = r.LateDelivery.HasValue ? r.LateDelivery == 1 : null,
                DeliveryStatus = r.ActualDays.HasValue
                    ? (r.LateDelivery == 1 ? "Late" : "On Time")
                    : "In Transit"
            })
        };
    }

    // ── History ───────────────────────────────────────────────────────────────

    public async Task<OrderHistoryViewModel> GetOrderHistoryAsync(
        int customerId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct)
            ?? throw new KeyNotFoundException($"Customer {customerId} not found.");

        var baseQuery = _db.Orders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.OrderDatetime);

        var totalCount = await baseQuery.CountAsync(ct);

        var rows = await baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.OrderId,
                o.OrderDatetime,
                o.OrderTotal,
                o.PaymentMethod,
                o.PromoCode,
                ItemCount = o.OrderItems.Count(),
                Carrier = o.Shipment != null ? o.Shipment.Carrier : null,
                ShippingMethod = o.Shipment != null ? o.Shipment.ShippingMethod : null,
                PromisedDays = o.Shipment != null ? o.Shipment.PromisedDays : (int?)null,
                ActualDays = o.Shipment != null ? o.Shipment.ActualDays : (int?)null,
                LateDelivery = o.Shipment != null ? o.Shipment.LateDelivery : (int?)null,
                Items = o.OrderItems.Select(i => new
                {
                    ProductName = i.Product != null ? i.Product.ProductName : $"Product #{i.ProductId}",
                    i.Quantity,
                    i.UnitPrice,
                    i.LineTotal
                }).ToList()
            })
            .AsNoTracking()
            .ToListAsync(ct);

        return new OrderHistoryViewModel
        {
            Customer = customer,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Orders = rows.Select(r => new OrderDetailRow
            {
                OrderId = r.OrderId,
                OrderDate = r.OrderDatetime.ToString("yyyy-MM-dd HH:mm:ss"),
                OrderTotal = r.OrderTotal,
                PaymentMethod = r.PaymentMethod,
                PromoCode = r.PromoCode,
                ItemCount = r.ItemCount,
                Carrier = r.Carrier,
                ShippingMethod = r.ShippingMethod,
                PromisedDays = r.PromisedDays,
                ActualDays = r.ActualDays,
                IsLate = r.LateDelivery.HasValue ? r.LateDelivery == 1 : null,
                Items = r.Items.Select(i => new OrderItemDetail
                {
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    LineTotal = i.LineTotal
                }).ToList()
            })
        };
    }

    // ── Place Order ───────────────────────────────────────────────────────────

    public async Task<int> PlaceOrderAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        // Resolve prices from DB (never trust client-side prices)
        var productIds = request.Lines.Select(l => l.ProductId).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.ProductId) && p.IsActive == 1)
            .ToDictionaryAsync(p => p.ProductId, ct);

        if (products.Count != productIds.Distinct().Count())
            throw new ArgumentException("One or more products not found or inactive.");

        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == request.CustomerId && c.IsActive == 1, ct)
            ?? throw new KeyNotFoundException($"Customer {request.CustomerId} not found.");

        // Build order items
        var items = request.Lines.Select(l => new OrderItem
        {
            ProductId = l.ProductId,
            Quantity = l.Quantity,
            UnitPrice = products[l.ProductId].Price,
            LineTotal = products[l.ProductId].Price * l.Quantity
        }).ToList();

        var subtotal = items.Sum(i => i.LineTotal);
        var shippingFee = request.ShippingMethod switch
        {
            "overnight" => 24.99m,
            "expedited" => 14.99m,
            _ => 5.99m  // standard
        };
        var tax = Math.Round(subtotal * 0.08m, 2);

        var order = new Order
        {
            CustomerId = request.CustomerId,
            OrderDatetime = DateTime.UtcNow,
            BillingZip = customer.ZipCode,
            ShippingZip = customer.ZipCode,
            ShippingState = customer.State,
            PaymentMethod = request.PaymentMethod,
            DeviceType = "desktop",
            IpCountry = "US",
            PromoUsed = string.IsNullOrWhiteSpace(request.PromoCode) ? 0 : 1,
            PromoCode = string.IsNullOrWhiteSpace(request.PromoCode) ? null : request.PromoCode,
            OrderSubtotal = subtotal,
            ShippingFee = shippingFee,
            TaxAmount = tax,
            OrderTotal = subtotal + shippingFee + tax,
            RiskScore = 0,   // ML team can fill this in via IScoringService
            IsFraud = 0,
            OrderItems = items
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        // Create an initial shipment record (pending delivery)
        var distanceBand = request.Carrier == "UPS" ? "regional" : "local";
        var promisedDays = request.ShippingMethod switch
        {
            "overnight" => 1,
            "expedited" => 3,
            _ => 6
        };

        var shipment = new Shipment
        {
            OrderId = order.OrderId,
            ShipDatetime = DateTime.UtcNow.AddHours(8),
            Carrier = request.Carrier,
            ShippingMethod = request.ShippingMethod,
            DistanceBand = distanceBand,
            PromisedDays = promisedDays,
            ActualDays = null,     // not yet delivered
            LateDelivery = null    // not yet known
        };

        _db.Shipments.Add(shipment);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Order {OrderId} placed for customer {CustomerId}", order.OrderId, request.CustomerId);
        return order.OrderId;
    }
}
