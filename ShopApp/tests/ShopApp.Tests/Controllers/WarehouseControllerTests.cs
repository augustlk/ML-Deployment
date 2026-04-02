using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShopApp.Web.Controllers;
using ShopApp.Web.Services.Interfaces;

namespace ShopApp.Tests.Controllers;

public class WarehouseControllerTests
{
    private readonly Mock<IScoringService> _mockScoring;
    private readonly WarehouseController _sut;

    public WarehouseControllerTests()
    {
        _mockScoring = new Mock<IScoringService>();
        _mockScoring.Setup(s => s.ModelName).Returns("Test Model");
        _mockScoring.Setup(s => s.LastRunAt).Returns((DateTime?)null);
        _mockScoring.Setup(s => s.GetTopAtRiskAsync(50, default))
            .ReturnsAsync(Enumerable.Empty<ScoredOrder>());

        _sut = new WarehouseController(_mockScoring.Object, NullLogger<WarehouseController>.Instance);
    }

    [Fact]
    public async Task Index_ReturnsViewResult()
    {
        var result = await _sut.Index();
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Index_CallsGetTopAtRisk()
    {
        await _sut.Index();
        _mockScoring.Verify(s => s.GetTopAtRiskAsync(50, default), Times.Once);
    }

    [Fact]
    public async Task RunScoring_BrowserForm_RedirectsToIndex()
    {
        _mockScoring.Setup(s => s.RunScoringAsync(default)).Returns(Task.CompletedTask);

        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        _sut.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await _sut.RunScoring();

        _mockScoring.Verify(s => s.RunScoringAsync(default), Times.Once);
        Assert.IsType<RedirectToActionResult>(result);
    }

    [Fact]
    public async Task RunScoringApi_ReturnsOkWithScoringMetadata()
    {
        _mockScoring.Setup(s => s.RunScoringAsync(default)).Returns(Task.CompletedTask);
        _mockScoring.Setup(s => s.GetTopAtRiskAsync(50, default))
            .ReturnsAsync(new[] { new ScoredOrder(1, 0.8f) });
        _mockScoring.Setup(s => s.LastRunAt).Returns(DateTime.UtcNow);

        var result = await _sut.RunScoringApi();

        _mockScoring.Verify(s => s.RunScoringAsync(default), Times.Once);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetPriorityQueueJson_ReturnsOk()
    {
        _mockScoring.Setup(s => s.GetTopAtRiskAsync(50, default))
            .ReturnsAsync(new[]
            {
                new ScoredOrder(1, 0.8f, 1, "Alice", "2025-01-01", 100m, "UPS", "standard", "local", 6)
            });

        var result = await _sut.GetPriorityQueueJson(50);
        Assert.IsType<OkObjectResult>(result);
    }
}
