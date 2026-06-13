using ContosoDashboard.Services;
using Microsoft.Extensions.Configuration;

namespace ContosoDashboard.Data;

/// <summary>
/// Local file-system implementation of <see cref="IFileStorageService"/>.
/// Reads base path from <c>IConfiguration["Storage:BasePath"]</c>
/// (fallback: <c>AppData/uploads</c> relative to the application's base directory).
///
/// Files are stored OUTSIDE the web root — never under <c>wwwroot/</c> (constitution §IV).
/// Streams are NEVER loaded into <c>byte[]</c> in memory.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(
        IConfiguration configuration,
        ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;

        var configured = configuration["Storage:BasePath"];
        _basePath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "AppData", "uploads")
            : Path.GetFullPath(configured);

        // Ensure directory exists at service instantiation time
        Directory.CreateDirectory(_basePath);
    }

    /// <inheritdoc/>
    public async Task UploadAsync(Stream stream, string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(stream);

        var fullPath = ResolveFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        // Streaming write — 80 KB buffer, never materialises the entire stream in RAM
        await using var fileStream = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81_920,
            useAsync: true);

        await stream.CopyToAsync(fileStream, 81_920, ct);

        _logger.LogInformation("Stored file at relative path {StoragePath}", path);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = ResolveFullPath(path);
        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("DeleteAsync: file not found at {StoragePath} — skipping", path);
            return Task.CompletedTask;
        }

        File.Delete(fullPath);
        _logger.LogInformation("Deleted file at relative path {StoragePath}", path);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<Stream> DownloadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = ResolveFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Storage file not found.", path);

        // Return a read-only FileStream — caller must NOT buffer the entire file in memory
        Stream fileStream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81_920,
            useAsync: true);

        return Task.FromResult(fileStream);
    }

    /// <inheritdoc/>
    public string GeneratePath(int userId, int? projectId, string extension)
    {
        var segment = projectId.HasValue
            ? projectId.Value.ToString()
            : "personal";

        // Sanitise extension: ensure leading dot, lowercase
        var ext = extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
        var fileName = $"{Guid.NewGuid()}{ext}";

        // Relative path — never expose _basePath (physical location) outside this service
        return Path.Combine(userId.ToString(), segment, fileName);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a relative storage path to an absolute path inside <see cref="_basePath"/>.
    /// Prevents path-traversal attacks by verifying the resolved path starts with the base.
    /// </summary>
    private string ResolveFullPath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_basePath, relativePath));

        // Security: reject any path that escapes the base directory (path traversal)
        if (!fullPath.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Resolved path '{fullPath}' is outside the configured storage base path.");

        return fullPath;
    }
}
