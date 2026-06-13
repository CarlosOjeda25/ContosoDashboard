using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services.Documents.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetProjectDocumentsQuery(
    int ProjectId,
    int ActorUserId,
    DocumentFilter Filter
) : IRequest<PagedResult<DocumentSummary>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetProjectDocumentsQueryHandler
    : IRequestHandler<GetProjectDocumentsQuery, PagedResult<DocumentSummary>>
{
    private readonly IDocumentRepository _documents;
    private readonly ApplicationDbContext _db;

    public GetProjectDocumentsQueryHandler(
        IDocumentRepository documents,
        ApplicationDbContext db)
    {
        _documents = documents;
        _db = db;
    }

    public async Task<PagedResult<DocumentSummary>> Handle(
        GetProjectDocumentsQuery query, CancellationToken ct)
    {
        // RBAC: actor must be a member of the project
        var isMember = await _db.ProjectMembers
            .AsNoTracking()
            .AnyAsync(pm => pm.ProjectId == query.ProjectId && pm.UserId == query.ActorUserId, ct);

        if (!isMember)
        {
            // Check if administrator
            var role = await _db.Users
                .AsNoTracking()
                .Where(u => u.UserId == query.ActorUserId)
                .Select(u => u.Role)
                .FirstOrDefaultAsync(ct);

            if (role is not (Models.UserRole.Administrator or Models.UserRole.ProjectManager))
                throw new ForbiddenException(
                    $"User {query.ActorUserId} is not a member of project {query.ProjectId}.");
        }

        var filter = query.Filter with { ProjectId = query.ProjectId };
        return await _documents.GetPagedAsync(filter, ct);
    }
}
