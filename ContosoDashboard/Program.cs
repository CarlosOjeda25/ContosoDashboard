using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Services;
using ContosoDashboard.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Serilog;
using Serilog.Events;
// ─────────────────────────────────────────────────────────────────────────────
// T-066 — Serilog: configure early so startup errors are captured (constitution §III)
// Console + rolling-File in all envs; AppInsights in Production only.
// PII (file content, physical paths, personal data) MUST NOT appear in logs —
// only identifiers like {DocumentId}, {UserId}, {EventType}.
// ─────────────────────────────────────────────────────────────────────────────
var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/contoso-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30);

// Azure Application Insights sink — Production only
if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
{
    var aiConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
    if (!string.IsNullOrEmpty(aiConnectionString))
    {
        loggerConfig.WriteTo.ApplicationInsights(
            aiConnectionString,
            TelemetryConverter.Traces);
    }
}

Log.Logger = loggerConfig.CreateLogger();

try
{
    Log.Information("ContosoDashboard starting up");

    var builder = WebApplication.CreateBuilder(args);

    // T-066: Use Serilog as the concrete logging provider
    builder.Host.UseSerilog();

    // T-006: Hard server-side gate — max request body 25 MB (26 214 400 bytes)
    // UX-level check happens in the Blazor upload component (T-040).
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 26_214_400; // 25 MB
    });

    // ─── Services ────────────────────────────────────────────────────────────

    builder.Services.AddRazorPages();
    builder.Services.AddServerSideBlazor();

    builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/login";
            options.LogoutPath = "/logout";
            options.AccessDeniedPath = "/login";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("Employee", policy => policy.RequireRole("Employee", "TeamLead", "ProjectManager", "Administrator"));
        options.AddPolicy("TeamLead", policy => policy.RequireRole("TeamLead", "ProjectManager", "Administrator"));
        options.AddPolicy("ProjectManager", policy => policy.RequireRole("ProjectManager", "Administrator"));
        options.AddPolicy("Administrator", policy => policy.RequireRole("Administrator"));
    });

    // T-005: MediatR — all CQRS commands/queries dispatched exclusively via IMediator.Send()
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

    // T-007: Centralized domain-exception → HTTP status translation (constitution §III)
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // Existing application services
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<ITaskService, TaskService>();
    builder.Services.AddScoped<IProjectService, ProjectService>();
    builder.Services.AddScoped<INotificationService, NotificationService>();
    builder.Services.AddScoped<IDashboardService, DashboardService>();

    // ── T-024: Document Management services (Fase 3) ─────────────────────────
    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
    builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
    builder.Services.AddScoped<IDocumentAuditLogRepository, DocumentAuditLogRepository>();
    builder.Services.AddScoped<DocumentAccessService>();

    // AV scanner: NoOp in Development, must be replaced with a real scanner in Production
    if (builder.Environment.IsDevelopment())
        builder.Services.AddScoped<IAntivirusScanner, NoOpAntivirusScanner>();

    builder.Services.AddHttpContextAccessor();

    var app = builder.Build();

    // T-006: Ensure AppData/uploads directory exists on startup (outside wwwroot)
    var uploadsPath = Path.Combine(AppContext.BaseDirectory, "AppData", "uploads");
    Directory.CreateDirectory(uploadsPath);
    Log.Information("Upload storage directory ensured at {UploadsPath}", uploadsPath);

    // Initialize database
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            context.Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred creating the database");
            throw;
        }
    }

    // ─── Middleware pipeline ──────────────────────────────────────────────────

    // T-007: Global exception handler must be first in the pipeline
    app.UseExceptionHandler();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }
    else
    {
        app.UseHsts();
    }

    // Security headers
    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net; " +
            "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
            "font-src 'self' https://cdn.jsdelivr.net; " +
            "img-src 'self' data: https:; " +
            "connect-src 'self' wss: ws:;";
        await next();
    });

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapBlazorHub();
    app.MapFallbackToPage("/_Host");

    // Health check endpoint for Docker/k8s/orchestrator readiness probes
    app.MapGet("/health", () => Results.Ok(new
    {
        status = "Healthy",
        timestamp = DateTime.UtcNow
    }))
        .AllowAnonymous()
        .WithTags("Infrastructure");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "ContosoDashboard terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

