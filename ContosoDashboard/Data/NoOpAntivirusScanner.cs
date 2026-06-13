using ContosoDashboard.Services;

namespace ContosoDashboard.Data;

/// <summary>
/// Development-only stub of <see cref="IAntivirusScanner"/> that always
/// reports files as clean. MUST NOT be registered in Production (constitution §IV).
///
/// Registered conditionally in Program.cs:
///   <c>if (app.Environment.IsDevelopment()) services.AddScoped&lt;IAntivirusScanner, NoOpAntivirusScanner&gt;();</c>
/// </summary>
public sealed class NoOpAntivirusScanner : IAntivirusScanner
{
    private readonly ILogger<NoOpAntivirusScanner> _logger;

    public NoOpAntivirusScanner(ILogger<NoOpAntivirusScanner> logger)
    {
        _logger = logger;
        // Warn every time this scanner is instantiated so the dev can't miss it
        _logger.LogWarning(
            "NoOpAntivirusScanner activo — no usar en producción. " +
            "Reemplazar con una implementación real antes del despliegue (constitution §IV).");
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Always returns <c>IsClean = true</c>. The pipeline still enforces magic-byte
    /// validation and size limits — only the AV step is bypassed in development.
    /// </remarks>
    public Task<ScanResult> ScanAsync(Stream stream, CancellationToken ct)
    {
        _logger.LogDebug("NoOpAntivirusScanner.ScanAsync — skipping scan, returning IsClean=true");
        return Task.FromResult(new ScanResult(IsClean: true, ThreatName: null));
    }
}
