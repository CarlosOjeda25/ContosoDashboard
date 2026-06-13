using ContosoDashboard.Data;
using ContosoDashboard.Services.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services.Documents.Queries;

// ── Queries ───────────────────────────────────────────────────────────────────

/// <summary>Returns the last 5 documents uploaded by the actor (FR-022).</summary>
public sealed record GetRecentDocumentsQuery(int ActorUserId)
    : IRequest<IReadOnlyList<DocumentSummary>>;

/// <summary>Total documents accessible by the actor — own + project + shared (FR-023).</summary>
public sealed record GetDocumentCountQuery(int ActorUserId) : IRequest<int>;

// ── Handlers ──────────────────────────────────────────────────────────────────

public sealed class GetRecentDocumentsQueryHandler
    : IRequestHandler<GetRecentDocumentsQuery, IReadOnlyList<DocumentSummary>>
{
    private readonly ApplicationDbContext _db;

    public GetRecentDocumentsQueryHandler(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<DocumentSummary>> Handle(
        GetRecentDocumentsQuery query, CancellationToken ct) =>
        await _db.Documents
            .AsNoTracking()
            .Include(d => d.Tags)
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
            .Where(d => d.UploadedByUserId == query.ActorUserId)
            .OrderByDescending(d => d.UploadedAtUtc)
            .Take(5)
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
}

public sealed class GetDocumentCountQueryHandler : IRequestHandler<GetDocumentCountQuery, int>
{
    private readonly ApplicationDbContext _db;

    public GetDocumentCountQueryHandler(ApplicationDbContext db) => _db = db;

    public async Task<int> Handle(GetDocumentCountQuery query, CancellationToken ct)
    {
        var actorId = query.ActorUserId;

        var actorProjectIds = await _db.ProjectMembers
            .AsNoTracking()
            .Where(pm => pm.UserId == actorId)
            .Select(pm => pm.ProjectId)
            .ToListAsync(ct);

        return await _db.Documents
            .AsNoTracking()
            .Include(d => d.Shares)
            .CountAsync(d =>
                d.UploadedByUserId == actorId ||
                (d.ProjectId != null && actorProjectIds.Contains(d.ProjectId.Value)) ||
                d.Shares.Any(s => s.RecipientUserId == actorId),
            ct);
    }
}
