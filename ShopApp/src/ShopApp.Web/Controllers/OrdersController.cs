using Microsoft.AspNetCore.Mvc;
using ShopApp.Web.Models.DTOs;
using ShopApp.Web.Models.ViewModels;
using ShopApp.Web.Services.Interfaces;

namespace ShopApp.Web.Controllers;

/// <summary>
/// Order history + new order placement for a selected customer.
/// </summary>
public class OrdersController : Controller
{
    private readonly IOrderService _orders;
    private readonly IProductService _products;
    private readonly ICustomerService _customers;

    public OrdersController(
        IOrderService orders,
        IProductService products,
        ICustomerService customers)
    {
        _orders = orders;
        _products = products;
        _customers = customers;
    }

    // ── History ───────────────────────────────────────────────────────────────

    [HttpGet("/orders/{customerId:int}")]
    public async Task<IActionResult> History(
        int customerId,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        try
        {
            var vm = await _orders.GetOrderHistoryAsync(customerId, page, ct: ct);
            return View(vm);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Customer {customerId} not found.");
        }
    }

    // ── New Order — GET form ──────────────────────────────────────────────────

    [HttpGet("/orders/{customerId:int}/new")]
    public async Task<IActionResult> New(int customerId, CancellationToken ct = default)
    {
        var customer = await _customers.GetByIdAsync(customerId, ct);
        if (customer is null) return NotFound($"Customer {customerId} not found.");

        var productList = await _products.GetActiveProductsAsync(ct);
        var categories = productList.Select(p => p.Category ?? "Other").Distinct().OrderBy(c => c);

        return View(new NewOrderViewModel
        {
            Customer = customer,
            Products = productList,
            Categories = categories
        });
    }

    // ── New Order — POST submit ───────────────────────────────────────────────

    [HttpPost("/orders/{customerId:int}/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(
        int customerId,
        [FromForm] NewOrderFormData form,
        CancellationToken ct = default)
    {
        // Rebuild the request from the flat form post
        var request = new CreateOrderRequest
        {
            CustomerId = customerId,
            PaymentMethod = form.PaymentMethod,
            PromoCode = form.PromoCode,
            ShippingMethod = form.ShippingMethod,
            Carrier = form.Carrier,
            Lines = form.ProductIds
                .Zip(form.Quantities, (pid, qty) => new OrderLineRequest
                {
                    ProductId = pid,
                    Quantity = qty
                })
                .Where(l => l.Quantity > 0)
                .ToList()
        };

        if (!request.Lines.Any())
        {
            ModelState.AddModelError("", "Please add at least one product to your order.");
            goto ReturnForm;
        }

        try
        {
            var orderId = await _orders.PlaceOrderAsync(request, ct);
            TempData["SuccessMessage"] = $"Order #{orderId} placed successfully!";
            return RedirectToAction("History", new { customerId });
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError("", ex.Message);
        }

        ReturnForm:
        var customer = await _customers.GetByIdAsync(customerId, ct);
        if (customer is null) return NotFound();
        var productList = await _products.GetActiveProductsAsync(ct);
        return View(new NewOrderViewModel
        {
            Customer = customer,
            Products = productList,
            Categories = productList.Select(p => p.Category ?? "Other").Distinct().OrderBy(c => c)
        });
    }
}

/// <summary>Flat form binding model for the new-order form POST.</summary>
public class NewOrderFormData
{
    public string PaymentMethod { get; set; } = "card";
    public string? PromoCode { get; set; }
    public string ShippingMethod { get; set; } = "standard";
    public string Carrier { get; set; } = "UPS";
    public List<int> ProductIds { get; set; } = new();
    public List<int> Quantities { get; set; } = new();
}
