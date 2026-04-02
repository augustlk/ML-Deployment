namespace ShopApp.Web.Models.ViewModels;

public class WarehouseViewModel
{
    public IEnumerable<ScoredOrderRow> PriorityQueue { get; init; } = Enumerable.Empty<ScoredOrderRow>();
    public DateTime? LastScoredAt { get; init; }
    public string ScoringModelName { get; init; } = "Rule-based stub";
    public bool IsScoring { get; init; }
}

public class ScoredOrderRow
{
    public int Rank { get; init; }
    public int OrderId { get; init; }
    public int CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string OrderDate { get; init; } = string.Empty;
    public decimal OrderTotal { get; init; }
    public string? Carrier { get; init; }
    public string? ShippingMethod { get; init; }
    public string? DistanceBand { get; init; }
    public int? PromisedDays { get; init; }

    /// <summary>
    /// Predicted late-delivery probability [0, 1].
    /// Produced by IScoringService — swap in the ML model when ready.
    /// </summary>
    public float LateDeliveryProbability { get; init; }

    public string ProbabilityBadgeClass => LateDeliveryProbability switch
    {
        >= 0.7f => "danger",
        >= 0.4f => "warning",
        _ => "success"
    };
}
