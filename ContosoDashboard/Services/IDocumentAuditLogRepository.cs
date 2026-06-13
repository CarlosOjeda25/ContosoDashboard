using ContosoDashboard.Models;
using ContosoDashboard.Services;

namespace ContosoDashboard.Services;

/// <summary>
/// Write-only contract for <see cref="DocumentAuditLog"/> persistence.
/// Audit entries are IMMUTABLE and NEVER deleted (FR-030, constitution §III).
/// Implementations MUST NOT call <c>SaveChangesAsync</c> — the handler owns
/// the transaction boundary.
/// </summary>
public interface IDocumentAuditLogRepository
{
    /// <summary>
    /// Stages a new audit log entry. Does NOT call SaveChangesAsync.
    /// Both <see cref="AddAsync"/> and the corresponding document operation
    /// MUST be committed in the same handler transaction.
    /// </summary>
    Task AddAsync(DocumentAuditLog entry, CancellationToken ct);

    /// <summary>
    /// Returns a paged list of audit entries for a given document,
    /// ordered by <see cref="DocumentAuditLog.OccurredAtUtc"/> descending.
    /// Uses <c>AsNoTracking()</c>.
    /// </summary>
    Task<PagedResult<DocumentAuditLog>> GetByDocumentIdAsync(
        Guid documentId, int page, int pageSize, CancellationToken ct);
}
