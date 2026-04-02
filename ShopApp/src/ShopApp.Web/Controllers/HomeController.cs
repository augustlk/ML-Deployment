using Microsoft.AspNetCore.Mvc;
using ShopApp.Web.Models.ViewModels;
using ShopApp.Web.Services.Interfaces;

namespace ShopApp.Web.Controllers;

/// <summary>
/// Customer selection screen — the app entry point (no login required).
/// </summary>
public class HomeController : Controller
{
    private readonly ICustomerService _customers;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ICustomerService customers, ILogger<HomeController> logger)
    {
        _customers = customers;
        _logger = logger;
    }

    [HttpGet("/")]
    [HttpGet("/Home")]
    [HttpGet("/Home/Index")]
    public async Task<IActionResult> Index(
        [FromQuery] string? search,
        [FromQuery] string? segment,
        CancellationToken ct = default)
    {
        var customers = await _customers.GetAllActiveAsync(search, segment, ct);
        var segments = await _customers.GetSegmentsAsync(ct);

        return View(new CustomerSelectViewModel
        {
            Customers = customers,
            SearchQuery = search,
            SegmentFilter = segment,
            Segments = segments
        });
    }

    [HttpGet("/Home/Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        _logger.LogError("Unhandled error page reached");
        return View();
    }
}
