# ShopApp — ASP.NET Core 8 Web Application

Operational web app backed by **shop.db** (SQLite). No login required.

## Features

| Page | Route | Description |
|------|-------|-------------|
| Customer Selection | `/` | Browse / search / filter 250 customers |
| Customer Dashboard | `/dashboard/{id}` | Order stats, recent activity |
| Order History | `/orders/{id}` | Paginated full history with accordion detail |
| New Order | `/orders/{id}/new` | Product picker → place order → persists to DB |
| Warehouse Queue | `/warehouse` | Top-50 late-delivery risk orders |
| Run Scoring | `POST /warehouse/score` | Triggers ML inference, refreshes queue |
| Priority Queue API | `GET /api/warehouse/priority-queue` | JSON endpoint for ML pipeline / BI |

---

## Quick Start

### Local development

```bash
cd src/ShopApp.Web
dotnet run
# Open http://localhost:5000
```

### Docker (recommended)

```bash
# Build + run (tests execute automatically during Docker build)
docker compose up --build

# App available at http://localhost:8080
```

---

## Deploy to Vercel

Vercel supports Docker deployments via **Fluid Compute** (Pro/Enterprise plan):

1. Push the repo to GitHub.
2. Import the project in [vercel.com/new](https://vercel.com/new).
3. Vercel detects the `Dockerfile` and `vercel.json` and builds automatically.
4. Add the env var `ConnectionStrings__ShopDb` pointing to your persistent volume path.

### Alternative (free tier) — Railway / Fly.io / Render

```bash
# Railway
railway up

# Fly.io
flyctl launch --dockerfile Dockerfile
flyctl deploy
```

All three read the `Dockerfile` directly. Set `ConnectionStrings__ShopDb` to a persistent volume mount.

---

## ML Pipeline Integration

The **Late Delivery Priority Queue** uses a scoring service to rank orders by predicted late-delivery probability.

### How it works today

`RuleBasedScoringService` (the default stub) calculates a score from:
- `risk_score` (40 %)
- `distance_band` (30 %)
- `promised_days` (20 %)
- `carrier` (10 %)

### How the ML team plugs in their model

1. Implement the interface in `Services/Interfaces/IScoringService.cs`:

```csharp
public class OnnxScoringService : IScoringService
{
    public string ModelName => "XGBoost v2.1";
    public DateTime? LastRunAt { get; private set; }

    public async Task RunScoringAsync(CancellationToken ct = default)
    {
        // Load your ONNX / pickle / HTTP model, score all orders, cache results
        LastRunAt = DateTime.UtcNow;
    }

    public async Task<IEnumerable<ScoredOrder>> GetTopAtRiskAsync(int top = 50, CancellationToken ct = default)
    {
        // Return cached results
    }
}
```

2. Swap the registration in `Program.cs`:

```csharp
// Before:
builder.Services.AddSingleton<IScoringService>(sp => new RuleBasedScoringService(...));

// After:
builder.Services.AddSingleton<IScoringService, OnnxScoringService>();
```

3. **That's it.** The UI, the "Run Scoring" button, and the JSON API endpoint
   (`GET /api/warehouse/priority-queue`) all keep working without any other changes.

### Calling the scoring endpoint externally (ML pipeline webhook)

After a retraining job completes, the ML pipeline can push fresh scores by calling:

```http
POST https://your-app.vercel.app/warehouse/score
Content-Type: application/json
Accept: application/json
```

Response:
```json
{ "scoredAt": "2025-11-29T10:00:00Z", "model": "XGBoost v2.1", "count": 5000 }
```

---

## Project Structure

```
ShopApp/
├── src/ShopApp.Web/
│   ├── Controllers/          # HomeController, DashboardController, OrdersController, WarehouseController
│   ├── Data/
│   │   ├── ShopDbContext.cs  # EF Core context
│   │   └── shop.db           # SQLite database
│   ├── Models/
│   │   ├── Entities/         # EF Core entity classes (map 1-to-1 to DB tables)
│   │   ├── ViewModels/       # View-specific models
│   │   └── DTOs/             # API / form request objects
│   ├── Services/
│   │   ├── Interfaces/       # ICustomerService, IOrderService, IProductService, IScoringService ← ML seam
│   │   └── Implementations/  # Concrete implementations (swap IScoringService for ML model)
│   ├── Views/                # Razor Pages (Bootstrap 5)
│   └── wwwroot/              # Static assets
├── tests/ShopApp.Tests/      # xUnit tests (services + controllers)
├── Dockerfile
├── docker-compose.yml
├── vercel.json
└── .github/workflows/ci.yml  # CI: build → test → docker build
```

---

## Running Tests

```bash
dotnet test ShopApp.sln
```

Test categories:
- `Services/CustomerServiceTests` — 8 tests covering search, filter, pagination
- `Services/OrderServiceTests`    — 9 tests covering order creation, pricing, validation
- `Services/ScoringServiceTests`  — 7 tests covering scoring accuracy, ranking, auto-trigger
- `Controllers/WarehouseControllerTests` — 4 tests with Moq
