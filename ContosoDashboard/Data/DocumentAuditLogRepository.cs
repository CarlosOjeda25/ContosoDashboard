using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Data;

/// <summary>
/// EF Core implementation of <see cref="IDocumentAuditLogRepository"/>.
/// Audit entries are IMMUTABLE and NEVER deleted (FR-030, constitution §III).
/// NEVER calls <c>SaveChangesAsync</c> — the handler owns the transaction boundary (C2).
/// </summary>
public sealed class DocumentAuditLogRepository : IDocumentAuditLogRepository
{
    private readonly ApplicationDbContext _db;

    public DocumentAuditLogRepository(ApplicationDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task AddAsync(DocumentAuditLog entry, CancellationToken ct) =>
        await _db.DocumentAuditLogs.AddAsync(entry, ct);

    /// <inheritdoc/>
    public async Task<PagedResult<DocumentAuditLog>> GetByDocumentIdAsync(
        Guid documentId, int page, int pageSize, CancellationToken ct)
    {
        var pageS = Math.Clamp(pageSize, 1, 100);
        var pageN = Math.Max(page, 1);
        var skip = (pageN - 1) * pageS;

        var query = _db.DocumentAuditLogs
            .AsNoTracking()
            .Where(l => l.DocumentId == documentId)
            .OrderByDescending(l => l.OccurredAtUtc);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip(skip)
            .Take(pageS)
            .ToListAsync(ct);

        return new PagedResult<DocumentAuditLog>(items, totalCount, pageN, pageS);
    }
}
