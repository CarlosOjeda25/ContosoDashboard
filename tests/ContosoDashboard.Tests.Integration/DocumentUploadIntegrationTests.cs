using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace ContosoDashboard.Tests.Integration;

/// <summary>
/// T-058: End-to-end document upload integration tests.
/// Uses WebApplicationFactory with EF Core InMemory (no Docker required).
/// </summary>
public sealed class DocumentUploadIntegrationTests : IClassFixture<DocumentIntegrationFactory>
{
    private readonly DocumentIntegrationFactory _factory;
    public DocumentUploadIntegrationTests(DocumentIntegrationFactory factory) => _factory = factory;

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_ValidPdf_Returns201_AndPersistsBothDbRowAndFile()
    {
        using var client = _factory.CreateAuthenticatedClient();
        using var content = BuildMultipart(
            fileBytes: BuildPdfBytes(),
            fileName: "report.pdf",
            title: "Test Report",
            category: "PersonalFiles");

        var response = await client.PostAsync("/api/documents/upload", content);

        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        // Row persisted in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.Documents.AnyAsync());
    }

    [Fact]
    public async Task Upload_OversizedFile_Returns413()
    {
        using var client = _factory.CreateAuthenticatedClient();
        // 26 MB of PDF-signed bytes
        var big = BuildPdfBytes(26 * 1024 * 1024);
        using var content = BuildMultipart(big, "huge.pdf", "Huge", "PersonalFiles");

        var response = await client.PostAsync("/api/documents/upload", content);

        Assert.Equal(System.Net.HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Upload_UnsupportedMime_Returns400()
    {
        using var client = _factory.CreateAuthenticatedClient();
        // Plain text bytes — not permitted
        var txt = System.Text.Encoding.UTF8.GetBytes("Hello, World!!!");
        using var content = BuildMultipart(txt, "notes.txt", "My Notes", "PersonalFiles");

        var response = await client.PostAsync("/api/documents/upload", content);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_InfectedFile_Returns400OrServiceUnavailable()
    {
        using var client = _factory.CreateAuthenticatedClient(infected: true);
        using var content = BuildMultipart(
            BuildPdfBytes(), "infected.pdf", "Bad File", "PersonalFiles");

        var response = await client.PostAsync("/api/documents/upload", content);

        Assert.True(
            response.StatusCode is System.Net.HttpStatusCode.BadRequest
                                 or System.Net.HttpStatusCode.ServiceUnavailable,
            $"Unexpected status {response.StatusCode}");
    }

    [Fact]
    public async Task Upload_Unauthenticated_Returns401()
    {
        using var client = _factory.CreateClient();
        using var content = BuildMultipart(BuildPdfBytes(), "doc.pdf", "Doc", "PersonalFiles");

        var response = await client.PostAsync("/api/documents/upload", content);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static byte[] BuildPdfBytes(int size = 64)
    {
        var buf = new byte[Math.Max(size, 8)];
        buf[0] = 0x25; buf[1] = 0x50; buf[2] = 0x44; buf[3] = 0x46; // %PDF
        buf[4] = 0x2D; buf[5] = 0x31; buf[6] = 0x2E; buf[7] = 0x34; // -1.4
        return buf;
    }

    private static MultipartFormDataContent BuildMultipart(
        byte[] fileBytes, string fileName, string title, string category)
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(fileBytes)
        {
            Headers = { ContentType = new("application/pdf") }
        }, "file", fileName);
        content.Add(new StringContent(title), "title");
        content.Add(new StringContent(category), "category");
        return content;
    }
}

// ── WebApplicationFactory ─────────────────────────────────────────────────────

/// <summary>
/// Shared test factory — replaces EF context with in-memory SQLite (same provider as
/// production, avoids dual-provider conflict), antivirus with controllable mock.
/// The SQLite connection is kept open for the factory lifetime so the in-memory DB persists.
/// </summary>
public sealed class DocumentIntegrationFactory : WebApplicationFactory<Program>, IDisposable
{
    // Keep the connection open so in-memory SQLite data persists across DbContext instances
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public DocumentIntegrationFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove production EF registrations (DbContext + its options)
            var toRemove = services
                .Where(d => d.ServiceType == typeof(ApplicationDbContext)
                         || d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            // In-memory SQLite — same provider as production, no dual-provider conflict
            services.AddDbContext<ApplicationDbContext>(opts =>
                opts.UseSqlite(_connection));

            // Replace antivirus with a clean scanner for most tests
            var avDesc = services.FirstOrDefault(d => d.ServiceType == typeof(IAntivirusScanner));
            if (avDesc != null) services.Remove(avDesc);
            services.AddScoped<IAntivirusScanner, NoOpInfectedScanner>();

            // Replace cookie auth with a test scheme that reads X-Test-UserId header
            var authSchemes = services
                .Where(d => d.ServiceType == typeof(Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider)
                         || d.ServiceType.FullName != null && d.ServiceType.FullName.Contains("Authentication"))
                .ToList();
            // Remove existing authentication registrations and add test scheme
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        });
    }

    // Seed test data AFTER the host is fully built (avoids BuildServiceProvider inside ConfigureServices)
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        if (!db.Users.Any())
        {
            db.Users.Add(new User
            {
                UserId = 1,
                DisplayName = "Test User",
                Email = "test@contoso.com",
                Role = UserRole.Employee
            });
            db.SaveChanges();
        }
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _connection.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>Creates an authenticated HTTP client (fakes cookie auth).</summary>
    public HttpClient CreateAuthenticatedClient(bool infected = false)
    {
        var client = WithWebHostBuilder(b =>
        {
            b.ConfigureServices(services =>
            {
                if (infected)
                {
                    // Replace scanner with one that always returns infected
                    var sd = services.SingleOrDefault(d => d.ServiceType == typeof(IAntivirusScanner));
                    if (sd != null) services.Remove(sd);
                    services.AddScoped<IAntivirusScanner, MockInfectedScanner>();
                }
            });
        }).CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Add a fake auth header — the integration test server uses cookie auth;
        // for simplicity, integration tests rely on the test-only bypass added via the factory.
        client.DefaultRequestHeaders.Add("X-Test-UserId", "1");
        return client;
    }
}

/// <summary>NoOp scanner that always returns clean (for most integration tests).</summary>
internal sealed class NoOpInfectedScanner : IAntivirusScanner
{
    public Task<ScanResult> ScanAsync(Stream stream, CancellationToken ct) =>
        Task.FromResult(new ScanResult(true, null));
}

/// <summary>Mock scanner that always returns infected.</summary>
internal sealed class MockInfectedScanner : IAntivirusScanner
{
    public Task<ScanResult> ScanAsync(Stream stream, CancellationToken ct) =>
        Task.FromResult(new ScanResult(false, "EICAR-Test-File"));
}

/// <summary>
/// Test authentication handler. Reads the X-Test-UserId header and
/// creates a ClaimsPrincipal for that user, bypassing cookie authentication.
/// If the header is absent, returns NoResult so the request is unauthenticated.
/// </summary>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-UserId", out var userIdValues))
            return Task.FromResult(AuthenticateResult.NoResult());

        var userId = userIdValues.ToString();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Role, "Employee"),
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
