using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services.Documents.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record SearchDocumentsQuery(
    string SearchTerm,
    int ActorUserId,
    int Page,
    int PageSize
) : IRequest<PagedResult<DocumentSummary>>;

// ── Handler ───────────────────────────────────────────────────────────────────

/// <summary>
/// Full-text search across Title, Description, Tags, UploaderName, ProjectName.
/// Results are filtered by RBAC: own documents | project member | explicit share (FR-013).
/// </summary>
public sealed class SearchDocumentsQueryHandler
    : IRequestHandler<SearchDocumentsQuery, PagedResult<DocumentSummary>>
{
    private readonly ApplicationDbContext _db;

    public SearchDocumentsQueryHandler(ApplicationDbContext db) => _db = db;

    public async Task<PagedResult<DocumentSummary>> Handle(
        SearchDocumentsQuery query, CancellationToken ct)
    {
        var term = query.SearchTerm.ToLower();
        var actorId = query.ActorUserId;
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var page = Math.Max(query.Page, 1);

        // Resolve project ids the actor belongs to (for RBAC)
        var actorProjectIds = await _db.ProjectMembers
            .AsNoTracking()
            .Where(pm => pm.UserId == actorId)
            .Select(pm => pm.ProjectId)
            .ToListAsync(ct);

        var isAdmin = await _db.Users
            .AsNoTracking()
            .Where(u => u.UserId == actorId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct) is UserRole.Administrator;

        var baseQuery = _db.Documents
            .AsNoTracking()
            .Include(d => d.Tags)
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
            .Include(d => d.Shares)
            .Where(d =>
                // Search predicate
                d.Title.ToLower().Contains(term) ||
                (d.Description != null && d.Description.ToLower().Contains(term)) ||
                d.Tags.Any(t => t.Value.ToLower().Contains(term)) ||
                (d.UploadedByUser != null && d.UploadedByUser.DisplayName.ToLower().Contains(term)) ||
                (d.Project != null && d.Project.Name.ToLower().Contains(term)))
            .Where(d =>
                // RBAC predicate
                isAdmin ||
                d.UploadedByUserId == actorId ||
                (d.ProjectId != null && actorProjectIds.Contains(d.ProjectId.Value)) ||
                d.Shares.Any(s => s.RecipientUserId == actorId));

        var totalCount = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderByDescending(d => d.UploadedAtUtc)
            .Skip((page - 1) * pageSize)
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
}
