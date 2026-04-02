namespace ShopApp.Web.Services.Interfaces;

// ---------------------------------------------------------------------------
// ML INTEGRATION POINT
// ---------------------------------------------------------------------------
// The ML team should provide a concrete implementation of this interface.
// Register it in Program.cs to replace the default RuleBasedScoringService.
//
// Recommended pattern:
//   builder.Services.AddScoped<IScoringService, YourMlScoringService>();
//
// The implementation receives IServiceProvider via DI so it can query the DB,
// call an external HTTP endpoint, or load an ONNX model — whatever fits the
// team's ML pipeline.
// ---------------------------------------------------------------------------

public interface IScoringService
{
    /// <summary>
    /// Human-readable name of the model / strategy in use (shown in the UI).
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// UTC timestamp of the most recent scoring run. Null if never run.
    /// </summary>
    DateTime? LastRunAt { get; }

    /// <summary>
    /// Triggers a fresh inference pass over all orders and caches the results.
    /// Called when the user clicks "Run Scoring".
    /// </summary>
    Task RunScoringAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the top <paramref name="top"/> orders ranked by predicted
    /// late-delivery probability (descending).  If scoring has never been
    /// run, triggers it automatically.
    /// </summary>
    Task<IEnumerable<ScoredOrder>> GetTopAtRiskAsync(int top = 50, CancellationToken ct = default);
}

/// <summary>
/// Lightweight result record produced by IScoringService.
/// </summary>
public record ScoredOrder(
    int OrderId,
    float LateDeliveryProbability,
    // Extra features forwarded from the DB for display — not used by scoring
    int CustomerId = 0,
    string CustomerName = "",
    string OrderDate = "",
    decimal OrderTotal = 0,
    string? Carrier = null,
    string? ShippingMethod = null,
    string? DistanceBand = null,
    int? PromisedDays = null
);
