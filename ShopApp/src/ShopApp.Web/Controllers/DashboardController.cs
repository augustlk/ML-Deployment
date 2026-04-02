using Microsoft.AspNetCore.Mvc;
using ShopApp.Web.Services.Interfaces;

namespace ShopApp.Web.Controllers;

/// <summary>
/// Customer-specific dashboard: order stats, recent activity.
/// </summary>
public class DashboardController : Controller
{
    private readonly IOrderService _orders;
    private readonly ICustomerService _customers;

    public DashboardController(IOrderService orders, ICustomerService customers)
    {
        _orders = orders;
        _customers = customers;
    }

    [HttpGet("/dashboard/{customerId:int}")]
    public async Task<IActionResult> Index(int customerId, CancellationToken ct = default)
    {
        try
        {
            var vm = await _orders.GetDashboardAsync(customerId, ct);
            return View(vm);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Customer {customerId} not found.");
        }
    }
}
