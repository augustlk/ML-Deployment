using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ShopApp.Web.Data;
using ShopApp.Web.Services.Interfaces;

namespace ShopApp.Web.Services.Implementations;

// ---------------------------------------------------------------------------
// STUB SCORING SERVICE — replace with ML implementation when ready
// ---------------------------------------------------------------------------
// This class satisfies IScoringService using simple business rules so the
// warehouse page works out-of-the-box.
//
// HOW TO HAND OFF TO THE ML TEAM
// --------------------------------
// 1. Create a class that implements IScoringService (e.g. OnnxScoringService,
//    HttpScoringService, etc.).
// 2. In Program.cs change the registration:
//       builder.Services.AddSingleton<IScoringService, YourMlScoringService>();
// 3. Delete or keep this file as a fallback — your call.
//
// FEATURE COLUMNS AVAILABLE IN THE DB (per order + shipment join)
//   orders  : customer_id, payment_method, device_type, ip_country,
//             promo_used, order_subtotal, shipping_fee, tax_amount,
//             order_total, risk_score
//   shipments: carrier, shipping_method, distance_band, promised_days
// ---------------------------------------------------------------------------

public class RuleBasedScoringService : IScoringService
{
    private const string CacheKey = "scoring:top50";
    private readonly ShopDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RuleBasedScoringService> _logger;

    public string ModelName => "Rule-based heuristic (stub)";
    public DateTime? LastRunAt { get; private set; }

    public RuleBasedScoringService(
        ShopDbContext db,
        IMemoryCache cache,
        ILogger<RuleBasedScoringService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task RunScoringAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Scoring run started ({Model})", ModelName);

        // Pull the features we need for all orders
        var rows = await _db.Orders
            .Join(_db.Shipments, o => o.OrderId, s => s.OrderId,
                (o, s) => new
                {
                    o.OrderId,
                    o.CustomerId,
                    CustomerName = o.Customer != null ? o.Customer.FullName : "",
                    o.OrderDatetime,
                    o.OrderTotal,
                    o.RiskScore,
                    s.Carrier,
                    s.ShippingMethod,
                    s.DistanceBand,
                    s.PromisedDays,
                    s.ActualDays,
                    s.LateDelivery
                })
            .AsNoTracking()
            .ToListAsync(ct);

        // Enrich with customer names (the projection above might miss them if not loaded)
        var customerNames = await _db.Customers
            .AsNoTracking()
            .ToDictionaryAsync(c => c.CustomerId, c => c.FullName, ct);

        var scored = rows.Select(r =>
        {
            var prob = ScoreOrder(
                riskScore: (float)r.RiskScore,
                shippingMethod: r.ShippingMethod,
                distanceBand: r.DistanceBand,
                carrier: r.Carrier,
                promisedDays: r.PromisedDays);

            return new ScoredOrder(
                OrderId: r.OrderId,
                LateDeliveryProbability: prob,
                CustomerId: r.CustomerId,
                CustomerName: customerNames.GetValueOrDefault(r.CustomerId, "Unknown"),
                OrderDate: r.OrderDatetime.ToString("yyyy-MM-dd HH:mm:ss"),
                OrderTotal: r.OrderTotal,
                Carrier: r.Carrier,
                ShippingMethod: r.ShippingMethod,
                DistanceBand: r.DistanceBand,
                PromisedDays: r.PromisedDays
            );
        })
        .OrderByDescending(s => s.LateDeliveryProbability)
        .ToList();

        _cache.Set(CacheKey, scored, TimeSpan.FromHours(1));
        LastRunAt = DateTime.UtcNow;

        _logger.LogInformation("Scoring complete. {Count} orders scored.", scored.Count);
    }

    public async Task<IEnumerable<ScoredOrder>> GetTopAtRiskAsync(int top = 50, CancellationToken ct = default)
    {
        if (!_cache.TryGetValue(CacheKey, out List<ScoredOrder>? cached) || cached is null)
            await RunScoringAsync(ct);

        return (_cache.Get<List<ScoredOrder>>(CacheKey) ?? []).Take(top);
    }

    // ── Heuristic formula ────────────────────────────────────────────────────
    // ML TEAM: replace this logic with your model's predict() call.
    // The return value must be in [0, 1].
    private static float ScoreOrder(
        float riskScore,
        string? shippingMethod,
        string? distanceBand,
        string? carrier,
        int? promisedDays)
    {
        // Normalise risk_score (0–100) → (0–1), weight 40 %
        var riskPart = Math.Clamp(riskScore / 100f, 0f, 1f) * 0.40f;

        // Distance contributes 30 %
        var distancePart = distanceBand switch
        {
            "national" => 0.30f,
            "regional" => 0.15f,
            _ => 0.05f   // local
        };

        // Tight promised window = higher risk, 20 %
        var daysPart = promisedDays switch
        {
            1 => 0.20f,
            <= 3 => 0.12f,
            _ => 0.04f
        };

        // Carrier reliability, 10 %
        var carrierPart = carrier switch
        {
            "USPS" => 0.10f,
            "FedEx" => 0.05f,
            _ => 0.03f   // UPS
        };

        return Math.Clamp(riskPart + distancePart + daysPart + carrierPart, 0f, 1f);
    }
}
