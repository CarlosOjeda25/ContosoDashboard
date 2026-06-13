using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services;

/// <summary>
/// Determines whether an actor may access (read/download/preview) a specific document.
///
/// Access matrix (FR-020/021/022, T-038):
///   - Owner (UploadedByUserId == actorUserId)         → allowed
///   - Project member (document has projectId and actor is a project member) → allowed
///   - Explicit DocumentShare recipient                → allowed
///   - Administrator                                   → always allowed
///   - Anyone else                                     → ForbiddenException
///   - Document not found                              → DocumentNotFoundException
/// </summary>
public sealed class DocumentAccessService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DocumentAccessService> _logger;

    public DocumentAccessService(
        IDocumentRepository documentRepository,
        ApplicationDbContext db,
        ILogger<DocumentAccessService> logger)
    {
        _documentRepository = documentRepository;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Throws <see cref="DocumentNotFoundException"/> or <see cref="ForbiddenException"/>
    /// if access is not permitted. Returns the <see cref="Document"/> if allowed.
    /// </summary>
    public async Task<Document> AuthorizeAccessAsync(
        Guid documentId, int actorUserId, CancellationToken ct)
    {
        var document = await _documentRepository.GetByIdAsync(documentId, ct)
            ?? throw new DocumentNotFoundException(documentId);

        if (await IsAllowedAsync(document, actorUserId, ct))
            return document;

        _logger.LogWarning(
            "Access denied: actor {ActorUserId} for document {DocumentId}",
            actorUserId, documentId);

        throw new ForbiddenException(
            $"User {actorUserId} does not have access to document {documentId}.");
    }

    /// <summary>Non-throwing check — used for UI visibility decisions.</summary>
    public async Task<bool> CanAccessAsync(
        Guid documentId, int actorUserId, CancellationToken ct)
    {
        var document = await _documentRepository.GetByIdAsync(documentId, ct);
        if (document is null) return false;
        return await IsAllowedAsync(document, actorUserId, ct);
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private async Task<bool> IsAllowedAsync(Document doc, int actorUserId, CancellationToken ct)
    {
        // 1. Owner
        if (doc.UploadedByUserId == actorUserId) return true;

        // 2. Administrator
        var role = await _db.Users
            .AsNoTracking()
            .Where(u => u.UserId == actorUserId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct);

        if (role is UserRole.Administrator) return true;

        // 3. Project member (if document belongs to a project)
        if (doc.ProjectId.HasValue)
        {
            var isMember = await _db.ProjectMembers
                .AsNoTracking()
                .AnyAsync(pm => pm.ProjectId == doc.ProjectId.Value && pm.UserId == actorUserId, ct);
            if (isMember) return true;
        }

        // 4. Explicit share recipient
        if (doc.Shares.Any(s => s.RecipientUserId == actorUserId)) return true;

        // 5. Team share (actor is a member of a project that received a share)
        var actorProjectIds = await _db.ProjectMembers
            .AsNoTracking()
            .Where(pm => pm.UserId == actorUserId)
            .Select(pm => pm.ProjectId)
            .ToListAsync(ct);

        if (doc.Shares.Any(s =>
            s.RecipientTeamId.HasValue &&
            actorProjectIds.Contains(s.RecipientTeamId.Value)))
            return true;

        return false;
    }
}

