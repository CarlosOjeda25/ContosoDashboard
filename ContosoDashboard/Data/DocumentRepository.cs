using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Services.Documents;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Data;

/// <summary>
/// EF Core implementation of <see cref="IDocumentRepository"/>.
/// NEVER calls <c>SaveChangesAsync</c> — the handler owns the transaction boundary (constitution §II / C2).
/// <c>StoredPath</c> is NEVER included in <see cref="DocumentSummary"/> projections (constitution §IV).
/// </summary>
public sealed class DocumentRepository : IDocumentRepository
{
    private readonly ApplicationDbContext _db;

    public DocumentRepository(ApplicationDbContext db) => _db = db;

    /// <inheritdoc/>
    /// Eagerly loads <c>Tags</c> and <c>Shares</c>. <c>AuditLogs</c> excluded (not needed in browse/edit flows).
    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await _db.Documents
            .Include(d => d.Tags)
            .Include(d => d.Shares)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    /// <inheritdoc/>
    public async Task<PagedResult<DocumentSummary>> GetPagedAsync(
        DocumentFilter filter, CancellationToken ct)
    {
        var query = _db.Documents
            .AsNoTracking()
            .Include(d => d.Tags)
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
            .AsQueryable();

        // ── Filters ──────────────────────────────────────────────────────────
        if (filter.UserId.HasValue)
            query = query.Where(d => d.UploadedByUserId == filter.UserId.Value);

        if (filter.ProjectId.HasValue)
            query = query.Where(d => d.ProjectId == filter.ProjectId.Value);

        if (filter.Category.HasValue)
            query = query.Where(d => d.Category == filter.Category.Value);

        if (filter.FromUtc.HasValue)
            query = query.Where(d => d.UploadedAtUtc >= filter.FromUtc.Value);

        if (filter.ToUtc.HasValue)
            query = query.Where(d => d.UploadedAtUtc <= filter.ToUtc.Value);

        // ── Sorting (FR-010) ──────────────────────────────────────────────────
        query = filter.SortBy?.ToLowerInvariant() switch
        {
            "title" => filter.SortDescending ? query.OrderByDescending(d => d.Title) : query.OrderBy(d => d.Title),
            "category" => filter.SortDescending ? query.OrderByDescending(d => d.Category) : query.OrderBy(d => d.Category),
            "filesize" => filter.SortDescending ? query.OrderByDescending(d => d.FileSizeBytes) : query.OrderBy(d => d.FileSizeBytes),
            _ => filter.SortDescending ? query.OrderByDescending(d => d.UploadedAtUtc) : query.OrderBy(d => d.UploadedAtUtc)  // default: uploadDate
        };

        // ── Count + Pagination ────────────────────────────────────────────────
        var totalCount = await query.CountAsync(ct);

        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var page = Math.Max(filter.Page, 1);
        var skip = (page - 1) * pageSize;

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .Select(d => new DocumentSummary(
                d.Id,
                d.Title,
                d.Description,
                d.Category,
                d.UploadedAtUtc,
                d.UploadedByUserId,
                d.UploadedByUser != null ? d.UploadedByUser.DisplayName : string.Empty,
                d.ProjectId,
                d.Project != null ? d.Project.Name : null,
                d.FileSizeBytes,
                d.MimeType,
                d.Tags.Select(t => t.Value).ToList()))
            .ToListAsync(ct);

        return new PagedResult<DocumentSummary>(items, totalCount, page, pageSize);
    }

    /// <inheritdoc/>
    public async Task AddAsync(Document doc, CancellationToken ct) =>
        await _db.Documents.AddAsync(doc, ct);

    /// <inheritdoc/>
    public Task UpdateAsync(Document doc, CancellationToken ct)
    {
        _db.Documents.Update(doc);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveAsync(Document doc, CancellationToken ct)
    {
        _db.Documents.Remove(doc);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> ExistsAsync(Guid id, CancellationToken ct) =>
        _db.Documents.AnyAsync(d => d.Id == id, ct);
}
