namespace ContosoDashboard.Services;

/// <summary>
/// Abstraction over the physical file storage backend.
/// The concrete implementation in Infrastructure uses the local file system;
/// a future implementation may target Azure Blob Storage.
/// Files MUST be stored outside the web root (constitution §IV).
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Persists <paramref name="stream"/> to <paramref name="path"/> using streaming —
    /// the stream is NEVER loaded into a <c>byte[]</c> in memory.
    /// </summary>
    Task UploadAsync(Stream stream, string path, CancellationToken ct);

    /// <summary>
    /// Deletes the file at <paramref name="path"/>.
    /// If the file does not exist, logs a Warning and returns without throwing.
    /// </summary>
    Task DeleteAsync(string path, CancellationToken ct);

    /// <summary>
    /// Returns a readable stream for the file at <paramref name="path"/>.
    /// Callers MUST NOT load the entire stream into memory.
    /// </summary>
    Task<Stream> DownloadAsync(string path, CancellationToken ct);

    /// <summary>
    /// Generates a GUID-based storage path for a new upload.
    /// Format: <c>{basePath}/{userId}/{projectId-or-personal}/{newGuid}.{ext}</c>
    /// </summary>
    string GeneratePath(int userId, int? projectId, string extension);
}
