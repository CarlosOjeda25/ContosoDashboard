using ContosoDashboard.Models;

namespace ContosoDashboard.Services.Documents;

// ── Shared read-side projections ───────────────────────────────────────────
// StoredPath is NEVER included — files are served exclusively through
// authenticated controller endpoints (constitution §IV).

/// <summary>
/// Safe read-only projection used in document lists and search results.
/// Does NOT expose <see cref="Document.StoredPath"/>.
/// </summary>
public sealed record DocumentSummary(
    Guid Id,
    string Title,
    string? Description,
    DocumentCategory Category,
    DateTimeOffset UploadedAtUtc,
    int UploadedByUserId,
    string UploaderName,
    int? ProjectId,
    string? ProjectName,
    long FileSizeBytes,
    string MimeType,
    IReadOnlyList<string> Tags);

/// <summary>Returned by UploadDocumentCommandHandler on success.</summary>
public sealed record UploadDocumentResult(
    Guid DocumentId,
    string Title,
    DateTimeOffset UploadedAtUtc);
