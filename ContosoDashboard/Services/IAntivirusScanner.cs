namespace ContosoDashboard.Services;

/// <summary>
/// Result of a malware scan. Immutable record.
/// If <see cref="IsClean"/> is <c>false</c>, <see cref="ThreatName"/> is never null.
/// </summary>
public sealed record ScanResult(bool IsClean, string? ThreatName);

/// <summary>
/// Abstraction over an antivirus/malware scanning provider.
/// The pipeline is FAIL-CLOSED: if the scanner is unavailable, the upload MUST be rejected —
/// never bypassed (constitution §IV).
/// </summary>
public interface IAntivirusScanner
{
    /// <summary>
    /// Scans <paramref name="stream"/> for malware.
    /// If the scanner cannot be reached, this method MUST throw (not return IsClean=true).
    /// </summary>
    Task<ScanResult> ScanAsync(Stream stream, CancellationToken ct);
}
