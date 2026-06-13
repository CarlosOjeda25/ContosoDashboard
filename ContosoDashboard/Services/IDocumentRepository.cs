using ContosoDashboard.Models;
using ContosoDashboard.Services.Documents;

namespace ContosoDashboard.Services;

// ── Shared DTOs used by repository and query handlers ──────────────────────

/// <summary>
/// Filter criteria for document list queries.
/// All fields are optional; null means "no constraint".
/// Note: UserId and ProjectId are <c>int</c> to match the existing entity PKs.
/// </summary>
public sealed record DocumentFilter(
    int? UserId,
    int? ProjectId,
    DocumentCategory? Category,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? SortBy,
    bool SortDescending,
    int Page,
    int PageSize);

/// <summary>Server-side paginated result wrapper.</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);

// ── Repository interface (Application layer) ───────────────────────────────

/// <summary>
/// Read/write contract for <see cref="Document"/> persistence.
/// Implementations MUST NOT call <c>SaveChangesAsync</c> —
/// the handler owns the transaction boundary (constitution §II / C2).
/// </summary>
public interface IDocumentRepository
{
    /// <summary>
    /// Returns the document by <paramref name="id"/> with <c>Tags</c> and <c>Shares</c>
    /// eagerly loaded. <c>AuditLogs</c> are excluded from this projection.
    /// Returns <c>null</c> if not found.
    /// </summary>
    Task<Document?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Returns a paged list of <see cref="DocumentSummary"/> projections.
    /// Uses <c>AsNoTracking()</c>. <c>StoredPath</c> is NEVER included in projections.
    /// Supports sort by: <c>title</c>, <c>uploadDate</c>, <c>category</c>, <c>fileSize</c> (FR-010).
    /// </summary>
    Task<PagedResult<DocumentSummary>> GetPagedAsync(DocumentFilter filter, CancellationToken ct);

    /// <summary>Stages a new document for insertion. Does NOT call SaveChangesAsync.</summary>
    Task AddAsync(Document doc, CancellationToken ct);

    /// <summary>Marks an existing document as modified. Does NOT call SaveChangesAsync.</summary>
    Task UpdateAsync(Document doc, CancellationToken ct);

    /// <summary>Stages a document for hard-deletion. Does NOT call SaveChangesAsync.</summary>
    Task RemoveAsync(Document doc, CancellationToken ct);

    /// <summary>Returns <c>true</c> if a document with <paramref name="id"/> exists.</summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);
}
