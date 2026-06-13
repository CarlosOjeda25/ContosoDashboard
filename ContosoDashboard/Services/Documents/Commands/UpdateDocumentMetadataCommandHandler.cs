using ContosoDashboard.Data;
using ContosoDashboard.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services.Documents.Commands;

// ── Command ───────────────────────────────────────────────────────────────────

public sealed record UpdateDocumentMetadataCommand(
    Guid DocumentId,
    string Title,
    string? Description,
    DocumentCategory Category,
    IReadOnlyList<string> Tags,
    int ActorUserId
) : IRequest;

// ── Handler ───────────────────────────────────────────────────────────────────

/// <summary>
/// Updates title, description, category, and replaces the entire tag collection in one transaction.
/// No AuditLog entry for metadata-only changes (spec §4.3).
/// </summary>
public sealed class UpdateDocumentMetadataCommandHandler
    : IRequestHandler<UpdateDocumentMetadataCommand>
{
    private readonly IDocumentRepository _documents;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<UpdateDocumentMetadataCommandHandler> _logger;

    public UpdateDocumentMetadataCommandHandler(
        IDocumentRepository documents,
        ApplicationDbContext db,
        ILogger<UpdateDocumentMetadataCommandHandler> logger)
    {
        _documents = documents;
        _db = db;
        _logger = logger;
    }

    public async Task Handle(UpdateDocumentMetadataCommand cmd, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(cmd.DocumentId, ct)
            ?? throw new DocumentNotFoundException(cmd.DocumentId);

        await AuthorizeUpdateAsync(document, cmd.ActorUserId, ct);

        document.Title = cmd.Title;
        document.Description = cmd.Description;
        document.Category = cmd.Category;

        // Replace tag collection in full
        document.Tags.Clear();
        foreach (var tag in cmd.Tags)
        {
            document.Tags.Add(new DocumentTag
            {
                Id = Guid.NewGuid(),
                DocumentId = cmd.DocumentId,
                Value = tag
            });
        }

        await _documents.UpdateAsync(document, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Metadata updated for document {DocumentId} by user {ActorUserId}",
            cmd.DocumentId, cmd.ActorUserId);
    }

    private async Task AuthorizeUpdateAsync(Document doc, int actorUserId, CancellationToken ct)
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
            $"User {actorUserId} is not authorised to update document {doc.Id}.");
    }
}
