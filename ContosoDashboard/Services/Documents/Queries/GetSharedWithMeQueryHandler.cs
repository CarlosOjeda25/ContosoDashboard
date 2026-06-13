using ContosoDashboard.Data;
using ContosoDashboard.Services.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services.Documents.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetSharedWithMeQuery(
    int ActorUserId,
    int Page,
    int PageSize
) : IRequest<PagedResult<DocumentSummary>>;

// ── Handler ───────────────────────────────────────────────────────────────────

/// <summary>
/// Returns documents shared directly with the actor, or where the actor is a
/// member of a team (A1: project) that received a share.
/// StoredPath NEVER included.
/// </summary>
public sealed class GetSharedWithMeQueryHandler
    : IRequestHandler<GetSharedWithMeQuery, PagedResult<DocumentSummary>>
{
    private readonly ApplicationDbContext _db;

    public GetSharedWithMeQueryHandler(ApplicationDbContext db) => _db = db;

    public async Task<PagedResult<DocumentSummary>> Handle(
        GetSharedWithMeQuery query, CancellationToken ct)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var page = Math.Max(query.Page, 1);
        var actorId = query.ActorUserId;

        // Project IDs the actor belongs to (for team-scoped shares)
        var actorProjectIds = await _db.ProjectMembers
            .AsNoTracking()
            .Where(pm => pm.UserId == actorId)
            .Select(pm => pm.ProjectId)
            .ToListAsync(ct);

        var baseQuery = _db.Documents
            .AsNoTracking()
            .Include(d => d.Tags)
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
            .Include(d => d.Shares)
            .Where(d =>
                d.Shares.Any(s =>
                    s.RecipientUserId == actorId ||
                    (s.RecipientTeamId != null && actorProjectIds.Contains(s.RecipientTeamId.Value))))
            .Where(d => d.UploadedByUserId != actorId); // exclude own documents

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
