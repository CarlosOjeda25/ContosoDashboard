namespace ContosoDashboard.Models;

/// <summary>
/// Represents an uploaded document.
/// FKs to <see cref="User"/> and <see cref="Project"/> use <c>int</c> to match
/// the existing integer primary keys of those entities.
/// No soft-delete: hard-delete only (constitution I1 / FR-018).
/// </summary>
public sealed class Document
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DocumentCategory Category { get; set; }

    /// <summary>
    /// Server-side path (outside web root). NEVER exposed in any DTO.
    /// Uses GUID-based naming — original filename is stored in <see cref="OriginalFileName"/>.
    /// </summary>
    public string StoredPath { get; set; } = string.Empty;

    /// <summary>
    /// User-supplied original filename, stored only in the database.
    /// NEVER used as the on-disk name.
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    public string MimeType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTimeOffset UploadedAtUtc { get; set; }

    // ── FK: int to match User.UserId ──────────────────────────────────────
    public int UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = default!;

    // ── FK: int? to match Project.ProjectId ───────────────────────────────
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────
    public ICollection<DocumentTag> Tags { get; set; } = new List<DocumentTag>();
    public ICollection<DocumentShare> Shares { get; set; } = new List<DocumentShare>();
    public ICollection<DocumentAuditLog> AuditLogs { get; set; } = new List<DocumentAuditLog>();
}
