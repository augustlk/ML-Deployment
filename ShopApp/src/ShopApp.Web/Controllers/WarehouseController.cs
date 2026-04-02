using Microsoft.AspNetCore.Mvc;
using ShopApp.Web.Models.ViewModels;
using ShopApp.Web.Services.Interfaces;

namespace ShopApp.Web.Controllers;

/// <summary>
/// Warehouse / operations page: Late Delivery Priority Queue + ML scoring trigger.
/// </summary>
public class WarehouseController : Controller
{
    private readonly IScoringService _scoring;
    private readonly ILogger<WarehouseController> _logger;

    public WarehouseController(IScoringService scoring, ILogger<WarehouseController> logger)
    {
        _scoring = scoring;
        _logger = logger;
    }

    // ── Priority Queue ─────────────────────────────────────────────────────────

    [HttpGet("/warehouse")]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var topOrders = await _scoring.GetTopAtRiskAsync(50, ct);
        var vm = BuildViewModel(topOrders);
        return View(vm);
    }

    // ── Run Scoring (browser form POST — CSRF protected) ──────────────────────

    /// <summary>
    /// Browser "Run Scoring" button — validates anti-forgery token and redirects
    /// back to the priority queue page when done.
    /// </summary>
    [HttpPost("/warehouse/score")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunScoring(CancellationToken ct = default)
    {
        _logger.LogInformation("Scoring triggered via browser UI");
        await _scoring.RunScoringAsync(ct);
        TempData["ScoringDone"] = $"Scored at {_scoring.LastRunAt:HH:mm:ss UTC}";
        return RedirectToAction(nameof(Index));
    }

    // ── Run Scoring (API POST — for ML pipeline / external callers) ───────────

    /// <summary>
    /// Machine-to-machine scoring trigger used by the ML pipeline after a
    /// retraining job completes.  No anti-forgery token required (not a browser form).
    ///
    /// POST /api/warehouse/score
    /// Returns: { scoredAt, model, count }
    /// </summary>
    [HttpPost("/api/warehouse/score")]
    public async Task<IActionResult> RunScoringApi(CancellationToken ct = default)
    {
        _logger.LogInformation("Scoring triggered via API");
        await _scoring.RunScoringAsync(ct);
        var topOrders = await _scoring.GetTopAtRiskAsync(50, ct);
        return Ok(new
        {
            scoredAt = _scoring.LastRunAt,
            model    = _scoring.ModelName,
            count    = topOrders.Count()
        });
    }

    // ── Priority queue read (JSON) ─────────────────────────────────────────────

    /// <summary>
    /// Returns the raw top-N list as JSON — useful for the ML team's dashboards
    /// or downstream systems without needing the full HTML page.
    /// </summary>
    [HttpGet("/api/warehouse/priority-queue")]
    public async Task<IActionResult> GetPriorityQueueJson(
        [FromQuery] int top = 50,
        CancellationToken ct = default)
    {
        var topOrders = await _scoring.GetTopAtRiskAsync(Math.Min(top, 200), ct);
        return Ok(topOrders);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private WarehouseViewModel BuildViewModel(IEnumerable<ScoredOrder> scored)
    {
        var rows = scored.Select((s, idx) => new ScoredOrderRow
        {
            Rank = idx + 1,
            OrderId = s.OrderId,
            CustomerId = s.CustomerId,
            CustomerName = s.CustomerName,
            OrderDate = s.OrderDate,
            OrderTotal = s.OrderTotal,
            Carrier = s.Carrier,
            ShippingMethod = s.ShippingMethod,
            DistanceBand = s.DistanceBand,
            PromisedDays = s.PromisedDays,
            LateDeliveryProbability = s.LateDeliveryProbability
        });

        return new WarehouseViewModel
        {
            PriorityQueue = rows,
            LastScoredAt = _scoring.LastRunAt,
            ScoringModelName = _scoring.ModelName
        };
    }
}
