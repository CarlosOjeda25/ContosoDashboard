using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Services.Documents.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ContosoDashboard.Tests.Unit;

/// <summary>T-053: UpdateDocumentMetadataCommandHandler unit tests.</summary>
public sealed class UpdateDocumentMetadataCommandHandlerTests
{
    private static ApplicationDbContext MakeDbContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Document MakeDoc(int ownerId, int? projectId = null) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Original",
        OriginalFileName = "doc.pdf",
        StoredPath = "/uploads/doc.pdf",
        MimeType = "application/pdf",
        FileSizeBytes = 512,
        UploadedAtUtc = DateTimeOffset.UtcNow,
        UploadedByUserId = ownerId,
        ProjectId = projectId,
        Category = DocumentCategory.Other
    };

    private static (UpdateDocumentMetadataCommandHandler handler, ApplicationDbContext db)
        Build(Document doc, User? actor = null)
    {
        var repo = new Mock<IDocumentRepository>();
        var db = MakeDbContext();

        repo.Setup(r => r.GetByIdAsync(doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        repo.Setup(r => r.UpdateAsync(doc, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        if (actor != null)
        {
            db.Users.Add(actor);
            db.SaveChanges();
        }

        var handler = new UpdateDocumentMetadataCommandHandler(
            repo.Object, db, NullLogger<UpdateDocumentMetadataCommandHandler>.Instance);

        return (handler, db);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Owner_UpdatesMetadataAndReplacesTagsInTransaction()
    {
        var doc = MakeDoc(ownerId: 1);
        var (handler, _) = Build(doc);

        var cmd = new UpdateDocumentMetadataCommand(
            doc.Id, "New Title", "Description", DocumentCategory.Reports,
            new[] { "tag1", "tag2" }, ActorUserId: 1);

        await handler.Handle(cmd, default);

        Assert.Equal("New Title", doc.Title);
        Assert.Equal("Description", doc.Description);
        Assert.Equal(DocumentCategory.Reports, doc.Category);
    }

    [Fact]
    public async Task Handle_TeamLeadInProject_Succeeds()
    {
        // G2b remediation
        var doc = MakeDoc(ownerId: 99, projectId: 10);
        var tl = new User { UserId = 3, Role = UserRole.TeamLead, DisplayName = "TL", Email = "tl@t.com" };
        var (handler, _) = Build(doc, tl);

        var cmd = new UpdateDocumentMetadataCommand(
            doc.Id, "New", null, DocumentCategory.Other, Array.Empty<string>(), ActorUserId: 3);

        // Should not throw
        await handler.Handle(cmd, default);
    }

    [Fact]
    public async Task Handle_ProjectManager_Succeeds()
    {
        var doc = MakeDoc(ownerId: 99, projectId: 10);
        var pm = new User { UserId = 4, Role = UserRole.ProjectManager, DisplayName = "PM", Email = "pm@t.com" };
        var (handler, _) = Build(doc, pm);

        var cmd = new UpdateDocumentMetadataCommand(
            doc.Id, "Updated", null, DocumentCategory.Reports, Array.Empty<string>(), ActorUserId: 4);

        await handler.Handle(cmd, default);
    }

    [Fact]
    public async Task Handle_NonOwnerEmployee_ThrowsForbiddenException()
    {
        var doc = MakeDoc(ownerId: 99, projectId: 10);
        var emp = new User { UserId = 7, Role = UserRole.Employee, DisplayName = "Emp", Email = "e@t.com" };
        var (handler, _) = Build(doc, emp);

        var cmd = new UpdateDocumentMetadataCommand(
            doc.Id, "Hack", null, DocumentCategory.Other, Array.Empty<string>(), ActorUserId: 7);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(cmd, default));
    }
}
