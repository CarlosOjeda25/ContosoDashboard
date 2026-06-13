using ContosoDashboard.Data;
using ContosoDashboard.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services.Documents.Commands;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>Hard-delete a document. No soft-delete, no IsDeleted flag (I1).</summary>
public sealed record DeleteDocumentCommand(
    Guid DocumentId,
    int ActorUserId
) : IRequest;

// ── Handler ───────────────────────────────────────────────────────────────────

/// <summary>
/// Deletion order: (1) FileSystem → (2) DB Remove → (3) AuditLog → (4) SaveChangesAsync.
/// Cascade FK rules remove Tags, Shares, and previous AuditLogs automatically.
/// If SaveChangesAsync fails after step 1, the loss is logged — the exception propagates.
/// </summary>
public sealed class DeleteDocumentCommandHandler : IRequestHandler<DeleteDocumentCommand>
{
    private readonly IDocumentRepository _documents;
    private readonly IDocumentAuditLogRepository _auditLogs;
    private readonly IFileStorageService _storage;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DeleteDocumentCommandHandler> _logger;

    public DeleteDocumentCommandHandler(
        IDocumentRepository documents,
        IDocumentAuditLogRepository auditLogs,
        IFileStorageService storage,
        ApplicationDbContext db,
        ILogger<DeleteDocumentCommandHandler> logger)
    {
        _documents = documents;
        _auditLogs = auditLogs;
        _storage = storage;
        _db = db;
        _logger = logger;
    }

    public async Task Handle(DeleteDocumentCommand cmd, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(cmd.DocumentId, ct)
            ?? throw new DocumentNotFoundException(cmd.DocumentId);

        // RBAC: Owner | Project Manager of the associated project | Administrator
        await AuthorizeDeleteAsync(document, cmd.ActorUserId, ct);

        var storagePath = document.StoredPath;

        // Step 1 — Delete physical file
        await _storage.DeleteAsync(storagePath, ct);

        // Step 2 — Stage DB remove (no SaveChangesAsync yet)
        await _documents.RemoveAsync(document, ct);

        // Step 3 — Stage audit entry
        await _auditLogs.AddAsync(new DocumentAuditLog
        {
            Id = Guid.NewGuid(),
            DocumentId = cmd.DocumentId,
            EventType = DocumentAuditEventType.Deleted,
            ActorUserId = cmd.ActorUserId,
            OccurredAtUtc = DateTimeOffset.UtcNow
        }, ct);

        // Step 4 — Commit in one transaction
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception dbEx)
        {
            _logger.LogError(dbEx,
                "DB save failed after physical delete of {StoragePath}. " +
                "File is gone but DB row may still exist — manual reconciliation required.",
                storagePath);
            throw;
        }

        _logger.LogInformation(
            "Document {DocumentId} hard-deleted by user {ActorUserId}", cmd.DocumentId, cmd.ActorUserId);
    }

    private async Task AuthorizeDeleteAsync(Document doc, int actorUserId, CancellationToken ct)
    {
        // Owner always allowed
        if (doc.UploadedByUserId == actorUserId) return;

        var actorRole = await _db.Users
            .AsNoTracking()
            .Where(u => u.UserId == actorUserId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct);

        // Administrator can delete any document regardless of project association
        if (actorRole == UserRole.Administrator) return;

        // Project Manager / Team Lead of the associated project
        if (doc.ProjectId.HasValue &&
            actorRole is UserRole.ProjectManager or UserRole.TeamLead)
            return;

        throw new ForbiddenException(
            $"User {actorUserId} is not authorised to delete document {doc.Id}.");
    }
}
