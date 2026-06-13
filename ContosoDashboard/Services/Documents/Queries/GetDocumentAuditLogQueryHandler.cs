using ContosoDashboard.Data;
using ContosoDashboard.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services.Documents.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetDocumentAuditLogQuery(
    Guid DocumentId,
    int ActorUserId,
    int Page,
    int PageSize
) : IRequest<PagedResult<DocumentAuditLog>>;

// ── Handler ───────────────────────────────────────────────────────────────────

/// <summary>
/// Returns a paged audit log for a specific document.
/// RBAC: Administrator only (FR-029). Any other role → ForbiddenException.
/// </summary>
public sealed class GetDocumentAuditLogQueryHandler
    : IRequestHandler<GetDocumentAuditLogQuery, PagedResult<DocumentAuditLog>>
{
    private readonly IDocumentAuditLogRepository _auditLogs;
    private readonly ApplicationDbContext _db;

    public GetDocumentAuditLogQueryHandler(
        IDocumentAuditLogRepository auditLogs,
        ApplicationDbContext db)
    {
        _auditLogs = auditLogs;
        _db = db;
    }

    public async Task<PagedResult<DocumentAuditLog>> Handle(
        GetDocumentAuditLogQuery query, CancellationToken ct)
    {
        var role = await _db.Users
            .AsNoTracking()
            .Where(u => u.UserId == query.ActorUserId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct);

        if (role is not UserRole.Administrator)
            throw new ForbiddenException(
                $"Audit log access requires Administrator role. User {query.ActorUserId} has role {role}.");

        return await _auditLogs.GetByDocumentIdAsync(
            query.DocumentId, query.Page, query.PageSize, ct);
    }
}

