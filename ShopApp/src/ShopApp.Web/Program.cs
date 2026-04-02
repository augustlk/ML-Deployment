using Microsoft.EntityFrameworkCore;
using Serilog;
using ShopApp.Web.Data;
using ShopApp.Web.Services.Implementations;
using ShopApp.Web.Services.Interfaces;

// ── Bootstrap Serilog ────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Logging ───────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .WriteTo.Console());

    // ── MVC ───────────────────────────────────────────────────────────────────
    builder.Services.AddControllersWithViews();

    // ── Database (Supabase / PostgreSQL) ──────────────────────────────────────
    // Connection string priority:
    //   1. DATABASE_URL environment variable  (Vercel / Railway / Fly.io standard)
    //   2. ConnectionStrings:ShopDb in appsettings.json
    //
    // Supabase connection string format (direct):
    //   Host=db.<ref>.supabase.co;Port=5432;Database=postgres;
    //   Username=postgres;Password=<password>;SSL Mode=Require
    //
    // For Vercel / serverless, prefer the Supabase *transaction pooler* URL:
    //   Host=aws-0-<region>.pooler.supabase.com;Port=6543;Database=postgres;
    //   Username=postgres.<ref>;Password=<password>;SSL Mode=Require
    var connectionString =
        Environment.GetEnvironmentVariable("DATABASE_URL")
        ?? builder.Configuration.GetConnectionString("ShopDb")
        ?? throw new InvalidOperationException(
            "No database connection string found. " +
            "Set DATABASE_URL env var or ConnectionStrings:ShopDb in appsettings.");

    // Npgsql 6+ requires UTC timestamps by default; the legacy switch lets us
    // pass plain DateTime values without an explicit Kind=UTC annotation.
    AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    builder.Services.AddDbContext<ShopDbContext>(options =>
        options.UseNpgsql(connectionString));

    // ── In-memory cache (used by scoring service) ─────────────────────────────
    builder.Services.AddMemoryCache();

    // ── Application services ──────────────────────────────────────────────────
    builder.Services.AddScoped<ICustomerService, CustomerService>();
    builder.Services.AddScoped<IOrderService, OrderService>();
    builder.Services.AddScoped<IProductService, ProductService>();

    // ML INTEGRATION POINT ────────────────────────────────────────────────────
    // The scoring service is registered as a singleton so its in-memory cache
    // (LastRunAt + top-50 list) survives across HTTP requests.
    //
    // When the ML team delivers their model, swap this line:
    //   builder.Services.AddSingleton<IScoringService, YourMlScoringService>();
    //
    // YourMlScoringService can take IServiceProvider / IServiceScopeFactory
    // to open a scoped DB context as needed.
    builder.Services.AddSingleton<IScoringService>(sp =>
    {
        // Create a long-lived scope for the singleton's DB access
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var scope = scopeFactory.CreateScope();
        return new RuleBasedScoringService(
            scope.ServiceProvider.GetRequiredService<ShopDbContext>(),
            sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
            sp.GetRequiredService<ILogger<RuleBasedScoringService>>());
    });

    var app = builder.Build();

    // ── Middleware pipeline ───────────────────────────────────────────────────
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthorization();

    // ── Routes ────────────────────────────────────────────────────────────────
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program accessible for integration tests
public partial class Program { }
