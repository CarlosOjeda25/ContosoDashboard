using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services.Documents.Commands;

// ── Command ───────────────────────────────────────────────────────────────────

public sealed record ReplaceDocumentCommand(
    Guid DocumentId,
    Stream FileStream,
    string OriginalFileName,
    int ActorUserId
) : IRequest<UploadDocumentResult>;

// ── Handler ───────────────────────────────────────────────────────────────────

/// <summary>
/// Replaces the stored file while keeping the document's identity and metadata.
///
/// Compensation logic:
///   - New file stored → SaveChangesAsync fails → delete new file; old file untouched.
///   - SaveChangesAsync succeeds → delete old file.
///
/// Audit log: EventType = Replaced.
/// RBAC: identical to Delete (Owner | Team Lead | Project Manager | Administrator).
/// </summary>
public sealed class ReplaceDocumentCommandHandler
    : IRequestHandler<ReplaceDocumentCommand, UploadDocumentResult>
{
    private const long MaxFileSizeBytes = 25L * 1024 * 1024;
    private const int MagicBytesHeaderLen = 8;

    private readonly IDocumentRepository _documents;
    private readonly IDocumentAuditLogRepository _auditLogs;
    private readonly IFileStorageService _storage;
    private readonly IAntivirusScanner _antivirus;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ReplaceDocumentCommandHandler> _logger;

    public ReplaceDocumentCommandHandler(
        IDocumentRepository documents,
        IDocumentAuditLogRepository auditLogs,
        IFileStorageService storage,
        IAntivirusScanner antivirus,
        ApplicationDbContext db,
        ILogger<ReplaceDocumentCommandHandler> logger)
    {
        _documents = documents;
        _auditLogs = auditLogs;
        _storage = storage;
        _antivirus = antivirus;
        _db = db;
        _logger = logger;
    }

    public async Task<UploadDocumentResult> Handle(
        ReplaceDocumentCommand cmd, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(cmd.DocumentId, ct)
            ?? throw new DocumentNotFoundException(cmd.DocumentId);

        await AuthorizeAsync(document, cmd.ActorUserId, ct);

        // ── Validation pipeline (mirrors Upload steps 1-3) ────────────────
        if (cmd.FileStream.Length > MaxFileSizeBytes)
            throw new DocumentUploadException("FileTooLarge");

        var header = new byte[MagicBytesHeaderLen];
        var read = await cmd.FileStream.ReadAsync(header.AsMemory(0, MagicBytesHeaderLen), ct);
        cmd.FileStream.Position = 0;

        if (!MagicBytesValidator.IsPermitted(header.AsSpan(0, read), out var baseMime))
            throw new DocumentUploadException("InvalidMimeType");

        var mimeType = baseMime.StartsWith("application/vnd.openxmlformats-officedocument", StringComparison.Ordinal)
            ? (MagicBytesValidator.ResolveOpenXmlMime(cmd.OriginalFileName) ?? baseMime)
            : baseMime;

        ScanResult scan;
        try
        {
            scan = await _antivirus.ScanAsync(cmd.FileStream, ct);
            cmd.FileStream.Position = 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "AV scanner unavailable during replace for document {DocumentId}", cmd.DocumentId);
            throw new DocumentUploadException("ScannerUnavailable");
        }

        if (!scan.IsClean)
        {
            _logger.LogCritical(
                "Infected replacement file rejected: threat={ThreatName}, document={DocumentId}",
                scan.ThreatName, cmd.DocumentId);
            throw new DocumentUploadException("InfectedFile");
        }

        // ── Store new file ────────────────────────────────────────────────
        var ext = Path.GetExtension(cmd.OriginalFileName);
        var newPath = _storage.GeneratePath(cmd.ActorUserId, document.ProjectId, ext);
        var oldPath = document.StoredPath;
        var replacedAt = DateTimeOffset.UtcNow;

        await _storage.UploadAsync(cmd.FileStream, newPath, ct);

        // ── Update document entity ────────────────────────────────────────
        document.StoredPath = newPath;
        document.OriginalFileName = cmd.OriginalFileName;
        document.MimeType = mimeType;
        document.FileSizeBytes = cmd.FileStream.Length;

        await _documents.UpdateAsync(document, ct);
        await _auditLogs.AddAsync(new DocumentAuditLog
        {
            Id = Guid.NewGuid(),
            DocumentId = cmd.DocumentId,
            EventType = DocumentAuditEventType.Replaced,
            ActorUserId = cmd.ActorUserId,
            OccurredAtUtc = replacedAt
        }, ct);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception dbEx)
        {
            // Compensation: delete the new file, keep the old one intact
            _logger.LogError(dbEx,
                "DB save failed during replace for document {DocumentId} — compensating by deleting new path {NewPath}",
                cmd.DocumentId, newPath);
            try { await _storage.DeleteAsync(newPath, ct); }
            catch (Exception fsEx)
            {
                _logger.LogError(fsEx,
                    "Compensation delete failed for {NewPath} — manual cleanup required", newPath);
            }
            throw;
        }

        // ── Delete old file (SaveChangesAsync already succeeded) ──────────
        try { await _storage.DeleteAsync(oldPath, ct); }
        catch (Exception fsEx)
        {
            _logger.LogWarning(fsEx,
                "Old file at {OldPath} could not be deleted after successful replace — orphaned file", oldPath);
        }

        _logger.LogInformation(
            "Document {DocumentId} replaced by user {ActorUserId}", cmd.DocumentId, cmd.ActorUserId);

        return new UploadDocumentResult(document.Id, document.Title, replacedAt);
    }

    private async Task AuthorizeAsync(Document doc, int actorUserId, CancellationToken ct)
    {
        if (doc.UploadedByUserId == actorUserId) return;

        if (doc.ProjectId.HasValue)
        {
            var role = await _db.Users
                .AsNoTracking()
                .Where(u => u.UserId == actorUserId)
                .Select(u => u.Role)
                .FirstOrDefaultAsync(ct);

            if (role is UserRole.ProjectManager or UserRole.TeamLead or UserRole.Administrator)
                return;
        }

        throw new ForbiddenException(
            $"User {actorUserId} is not authorised to replace document {doc.Id}.");
    }
}
