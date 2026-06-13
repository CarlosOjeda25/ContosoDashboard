using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services.Documents.Commands;

/// <summary>
/// Handles <see cref="UploadDocumentCommand"/> via an 8-step fail-closed upload pipeline.
///
/// Pipeline steps:
///   1. File size ≤ 25 MB
///   2. Magic-bytes validation (MagicBytesValidator — not extension/MIME header)
///   3. Antivirus scan (fail-closed: unavailable scanner → reject)
///   4. Generate GUID storage path
///   5. Store file on the file system
///   6. Stage Document entity
///   7. Stage DocumentAuditLog entry
///   8. SaveChangesAsync (single transaction for steps 6-7)
///
/// Compensation: if SaveChangesAsync fails after step 5, the orphaned file is deleted.
/// Notifications are best-effort (FR-024): failure → Warning log, no rollback.
/// </summary>
public sealed class UploadDocumentCommandHandler
    : IRequestHandler<UploadDocumentCommand, UploadDocumentResult>
{
    private const long MaxFileSizeBytes = 25L * 1024 * 1024; // 25 MB
    private const int MagicBytesHeaderSize = 8;

    private readonly IFileStorageService _storage;
    private readonly IAntivirusScanner _antivirus;
    private readonly IDocumentRepository _documents;
    private readonly IDocumentAuditLogRepository _auditLogs;
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly ILogger<UploadDocumentCommandHandler> _logger;

    public UploadDocumentCommandHandler(
        IFileStorageService storage,
        IAntivirusScanner antivirus,
        IDocumentRepository documents,
        IDocumentAuditLogRepository auditLogs,
        ApplicationDbContext db,
        INotificationService notifications,
        ILogger<UploadDocumentCommandHandler> logger)
    {
        _storage = storage;
        _antivirus = antivirus;
        _documents = documents;
        _auditLogs = auditLogs;
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<UploadDocumentResult> Handle(
        UploadDocumentCommand cmd, CancellationToken ct)
    {
        // ── Step 1: Size gate ─────────────────────────────────────────────
        if (cmd.FileSizeBytes > MaxFileSizeBytes)
            throw new DocumentUploadException("FileTooLarge");

        // ── Step 2: Magic-bytes validation (fail-closed) ───────────────────
        var header = new byte[MagicBytesHeaderSize];
        var read = await cmd.FileStream.ReadAsync(header.AsMemory(0, MagicBytesHeaderSize), ct);
        cmd.FileStream.Position = 0; // rewind for subsequent steps

        if (!MagicBytesValidator.IsPermitted(header.AsSpan(0, read), out var baseMime))
            throw new DocumentUploadException("InvalidMimeType");

        // Resolve precise MIME for Open XML family
        var mimeType = baseMime.StartsWith("application/vnd.openxmlformats-officedocument", StringComparison.Ordinal)
            ? (MagicBytesValidator.ResolveOpenXmlMime(cmd.OriginalFileName) ?? baseMime)
            : baseMime;

        // ── Step 3: Antivirus scan (fail-closed) ──────────────────────────
        ScanResult scanResult;
        try
        {
            scanResult = await _antivirus.ScanAsync(cmd.FileStream, ct);
            cmd.FileStream.Position = 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Antivirus scanner unavailable for upload by user {ActorUserId}", cmd.ActorUserId);
            throw new DocumentUploadException("ScannerUnavailable");
        }

        if (!scanResult.IsClean)
        {
            _logger.LogCritical(
                "Infected file rejected: threat={ThreatName}, user={ActorUserId}, file={FileName}",
                scanResult.ThreatName, cmd.ActorUserId, cmd.OriginalFileName);
            throw new DocumentUploadException("InfectedFile");
        }

        // ── Step 4: Resolve ProjectId (from TaskId if not provided) ───────
        var projectId = cmd.ProjectId;
        if (cmd.TaskId.HasValue && projectId is null)
        {
            var task = await _db.Tasks
                .AsNoTracking()
                .Where(t => t.TaskId == cmd.TaskId.Value)
                .Select(t => (int?)t.ProjectId)
                .FirstOrDefaultAsync(ct);
            projectId = task;
        }

        // ── Step 5: Generate path & store file ────────────────────────────
        var ext = Path.GetExtension(cmd.OriginalFileName);
        var storagePath = _storage.GeneratePath(cmd.ActorUserId, projectId, ext);

        await _storage.UploadAsync(cmd.FileStream, storagePath, ct);

        // ── Steps 6-8: Persist document + audit log in one transaction ─────
        var documentId = Guid.NewGuid();
        var uploadedAt = DateTimeOffset.UtcNow;

        var document = new Document
        {
            Id = documentId,
            Title = cmd.Title,
            Description = cmd.Description,
            Category = cmd.Category,
            StoredPath = storagePath,
            OriginalFileName = cmd.OriginalFileName,
            MimeType = mimeType,
            FileSizeBytes = cmd.FileStream.Length,
            UploadedAtUtc = uploadedAt,
            UploadedByUserId = cmd.ActorUserId,
            ProjectId = projectId,
            Tags = cmd.Tags
                .Select(v => new DocumentTag { Id = Guid.NewGuid(), DocumentId = documentId, Value = v })
                .ToList()
        };

        var auditEntry = new DocumentAuditLog
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            EventType = DocumentAuditEventType.Uploaded,
            ActorUserId = cmd.ActorUserId,
            OccurredAtUtc = uploadedAt
        };

        await _documents.AddAsync(document, ct);
        await _auditLogs.AddAsync(auditEntry, ct);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception dbEx)
        {
            // Compensate: delete the stored file so we don't leave orphans on FS
            _logger.LogError(dbEx,
                "DB save failed after file stored — compensating by deleting {StoragePath}", storagePath);
            try { await _storage.DeleteAsync(storagePath, ct); }
            catch (Exception fsEx)
            {
                _logger.LogError(fsEx,
                    "Compensation delete also failed for {StoragePath} — manual cleanup required", storagePath);
            }
            throw;
        }

        _logger.LogInformation(
            "Document {DocumentId} uploaded by user {ActorUserId}, size={FileSizeBytes}",
            documentId, cmd.ActorUserId, document.FileSizeBytes);

        // ── Best-effort notification (FR-024) ─────────────────────────────
        if (projectId.HasValue)
        {
            try
            {
                await _notifications.NotifyProjectDocumentAddedAsync(
                    projectId.Value, documentId, cmd.ActorUserId, ct);
            }
            catch (Exception notifEx)
            {
                _logger.LogWarning(notifEx,
                    "Best-effort notification failed for document {DocumentId} — no rollback", documentId);
            }
        }

        return new UploadDocumentResult(documentId, cmd.Title, uploadedAt);
    }
}
