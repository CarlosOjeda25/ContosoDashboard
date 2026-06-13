using ContosoDashboard.Data;
using ContosoDashboard.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services.Documents.Commands;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Share a document with individual users and/or project teams.
/// A1: RecipientTeamIds contains ProjectIds — "team" = active ProjectMember records.
/// </summary>
public sealed record ShareDocumentCommand(
    Guid DocumentId,
    int ActorUserId,
    IReadOnlyList<int> RecipientUserIds,
    IReadOnlyList<int> RecipientTeamIds
) : IRequest;

// ── Handler ───────────────────────────────────────────────────────────────────

/// <summary>
/// Creates one DocumentShare per recipient (user or team member) plus one
/// DocumentAuditLog per share — all in a single SaveChangesAsync call.
/// Notifications are best-effort (FR-024): failure → Warning, no rollback.
/// </summary>
public sealed class ShareDocumentCommandHandler : IRequestHandler<ShareDocumentCommand>
{
    private readonly IDocumentRepository _documents;
    private readonly IDocumentAuditLogRepository _auditLogs;
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly ILogger<ShareDocumentCommandHandler> _logger;

    public ShareDocumentCommandHandler(
        IDocumentRepository documents,
        IDocumentAuditLogRepository auditLogs,
        ApplicationDbContext db,
        INotificationService notifications,
        ILogger<ShareDocumentCommandHandler> logger)
    {
        _documents = documents;
        _auditLogs = auditLogs;
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task Handle(ShareDocumentCommand cmd, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(cmd.DocumentId, ct)
            ?? throw new DocumentNotFoundException(cmd.DocumentId);

        // RBAC: only the owner can share
        if (document.UploadedByUserId != cmd.ActorUserId)
        {
            var role = await _db.Users
                .AsNoTracking()
                .Where(u => u.UserId == cmd.ActorUserId)
                .Select(u => u.Role)
                .FirstOrDefaultAsync(ct);

            if (role is not UserRole.Administrator)
                throw new ForbiddenException(
                    $"User {cmd.ActorUserId} is not authorised to share document {cmd.DocumentId}.");
        }

        var now = DateTimeOffset.UtcNow;
        var allRecipientUsers = new List<int>(cmd.RecipientUserIds);

        // Resolve team shares: team = active ProjectMembers of that ProjectId (A1)
        foreach (var teamId in cmd.RecipientTeamIds)
        {
            var memberIds = await _db.ProjectMembers
                .AsNoTracking()
                .Where(pm => pm.ProjectId == teamId)
                .Select(pm => pm.UserId)
                .ToListAsync(ct);

            foreach (var memberId in memberIds)
            {
                // Create a team-scoped share entry
                await _auditLogs.AddAsync(new DocumentAuditLog
                {
                    Id = Guid.NewGuid(),
                    DocumentId = cmd.DocumentId,
                    EventType = DocumentAuditEventType.ShareGranted,
                    ActorUserId = cmd.ActorUserId,
                    OccurredAtUtc = now
                }, ct);

                _db.DocumentShares.Add(new DocumentShare
                {
                    Id = Guid.NewGuid(),
                    DocumentId = cmd.DocumentId,
                    RecipientUserId = memberId,
                    RecipientTeamId = teamId,
                    GrantedByUserId = cmd.ActorUserId,
                    GrantedAtUtc = now
                });

                if (!allRecipientUsers.Contains(memberId))
                    allRecipientUsers.Add(memberId);
            }
        }

        // Direct user shares
        foreach (var userId in cmd.RecipientUserIds)
        {
            _db.DocumentShares.Add(new DocumentShare
            {
                Id = Guid.NewGuid(),
                DocumentId = cmd.DocumentId,
                RecipientUserId = userId,
                GrantedByUserId = cmd.ActorUserId,
                GrantedAtUtc = now
            });

            await _auditLogs.AddAsync(new DocumentAuditLog
            {
                Id = Guid.NewGuid(),
                DocumentId = cmd.DocumentId,
                EventType = DocumentAuditEventType.ShareGranted,
                ActorUserId = cmd.ActorUserId,
                OccurredAtUtc = now
            }, ct);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Document {DocumentId} shared by user {ActorUserId} with {RecipientCount} users",
            cmd.DocumentId, cmd.ActorUserId, allRecipientUsers.Count);

        // Best-effort notifications (FR-024)
        try
        {
            await _notifications.NotifyShareAsync(cmd.DocumentId, allRecipientUsers, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Best-effort share notification failed for document {DocumentId} — no rollback", cmd.DocumentId);
        }
    }
}
