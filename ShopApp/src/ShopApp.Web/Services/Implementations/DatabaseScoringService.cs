using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ShopApp.Web.Data;
using ShopApp.Web.Services.Interfaces;

namespace ShopApp.Web.Services.Implementations;

/// <summary>
/// Reads fraud probability scores produced by the ML pipeline from the
/// ml_scores table in Supabase and exposes them via IScoringService.
///
/// The ML pipeline writes to ml_scores via score_orders.py after each
/// training/inference run. This service reads those results so the
/// warehouse page always shows the latest ML-generated priority queue.
/// </summary>
public sealed class DatabaseScoringService : IScoringService
{
    private readonly ShopDbContext         _db;
    private readonly IMemoryCache          _cache;
    private readonly ILogger<DatabaseScoringService> _logger;

    private const string CacheKey   = "db_scored_orders";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public string    ModelName  { get; private set; } = "ML Pipeline (loading…)";
    public DateTime? LastRunAt  { get; private set; }

    public DatabaseScoringService(
        ShopDbContext db,
        IMemoryCache cache,
        ILogger<DatabaseScoringService> logger)
    {
        _db     = db;
        _cache  = cache;
        _logger = logger;
    }

    /// <summary>
    /// Refreshes the in-memory cache from ml_scores.
    /// Called by the "Run Scoring" button or the /api/warehouse/score webhook.
    /// </summary>
    public async Task RunScoringAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("DatabaseScoringService: refreshing from ml_scores");

        var results = await FetchFromDatabaseAsync(ct);

        _cache.Set(CacheKey, results, CacheTtl);

        // Update metadata from the most-recent row
        var latest = results.MaxBy(r => r.ScoredAt);
        if (latest is not null)
        {
            LastRunAt  = latest.ScoredAt;
            ModelName  = latest.ModelVersion is not null
                ? $"Fraud Model v{latest.ModelVersion}"
                : "Fraud Detection Model";
        }

        _logger.LogInformation("DatabaseScoringService: loaded {Count} scores, latest={ScoredAt}",
            results.Count, LastRunAt);
    }

    /// <summary>
    /// Returns the top-N orders ranked by fraud probability (highest first).
    /// Auto-loads from the database on first call.
    /// </summary>
    public async Task<IEnumerable<ScoredOrder>> GetTopAtRiskAsync(
        int top = 50, CancellationToken ct = default)
    {
        if (!_cache.TryGetValue(CacheKey, out List<MlScoreRow>? cached) || cached is null)
        {
            await RunScoringAsync(ct);
            _cache.TryGetValue(CacheKey, out cached);
        }

        if (cached is null || cached.Count == 0)
            return Enumerable.Empty<ScoredOrder>();

        return cached
            .OrderByDescending(r => r.FraudProbability)
            .Take(top)
            .Select(r => new ScoredOrder(
                OrderId:                r.OrderId,
                LateDeliveryProbability: r.FraudProbability,
                CustomerId:             r.CustomerId,
                CustomerName:           r.CustomerName,
                OrderDate:              r.OrderDatetime.ToString("yyyy-MM-dd HH:mm:ss"),
                OrderTotal:             r.OrderTotal,
                Carrier:                r.Carrier,
                ShippingMethod:         r.ShippingMethod,
                DistanceBand:           r.DistanceBand,
                PromisedDays:           r.PromisedDays
            ));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<List<MlScoreRow>> FetchFromDatabaseAsync(CancellationToken ct)
    {
        // Raw SQL join: ml_scores → orders → customers → shipments
        var sql = """
            SELECT
                ms.order_id,
                ms.fraud_probability,
                ms.model_version,
                ms.scored_at,
                o.customer_id,
                COALESCE(c.full_name, 'Unknown') AS customer_name,
                o.order_datetime,
                o.order_total,
                s.carrier,
                s.shipping_method,
                s.distance_band,
                s.promised_days
            FROM ml_scores ms
            JOIN orders    o  ON ms.order_id    = o.order_id
            LEFT JOIN customers c  ON o.customer_id = c.customer_id
            LEFT JOIN shipments  s  ON o.order_id    = s.order_id
            ORDER BY ms.fraud_probability DESC
            """;

        var rows = new List<MlScoreRow>();

        var conn = _db.Database.GetDbConnection();
        await conn.OpenAsync(ct);

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                rows.Add(new MlScoreRow
                {
                    OrderId          = reader.GetInt32(0),
                    FraudProbability = (float)reader.GetDouble(1),
                    ModelVersion     = reader.IsDBNull(2) ? null : reader.GetString(2),
                    ScoredAt         = reader.GetDateTime(3),
                    CustomerId       = reader.GetInt32(4),
                    CustomerName     = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    OrderDatetime    = reader.GetDateTime(6),
                    OrderTotal       = reader.GetDecimal(7),
                    Carrier          = reader.IsDBNull(8)  ? null : reader.GetString(8),
                    ShippingMethod   = reader.IsDBNull(9)  ? null : reader.GetString(9),
                    DistanceBand     = reader.IsDBNull(10) ? null : reader.GetString(10),
                    PromisedDays     = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                });
            }
        }
        finally
        {
            await conn.CloseAsync();
        }

        return rows;
    }

    // ── Internal DTO ──────────────────────────────────────────────────────────

    private sealed class MlScoreRow
    {
        public int      OrderId          { get; init; }
        public float    FraudProbability { get; init; }
        public string?  ModelVersion     { get; init; }
        public DateTime ScoredAt         { get; init; }
        public int      CustomerId       { get; init; }
        public string   CustomerName     { get; init; } = "";
        public DateTime OrderDatetime    { get; init; }
        public decimal  OrderTotal       { get; init; }
        public string?  Carrier          { get; init; }
        public string?  ShippingMethod   { get; init; }
        public string?  DistanceBand     { get; init; }
        public int?     PromisedDays     { get; init; }
    }
}
