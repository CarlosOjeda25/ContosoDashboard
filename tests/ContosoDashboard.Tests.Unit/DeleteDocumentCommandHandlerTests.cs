using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Services.Documents.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ContosoDashboard.Tests.Unit;

/// <summary>T-051: DeleteDocumentCommandHandler unit tests.</summary>
public sealed class DeleteDocumentCommandHandlerTests
{
    private static ApplicationDbContext MakeDbContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Document MakeDoc(int ownerId, int? projectId = null) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Test",
        OriginalFileName = "test.pdf",
        StoredPath = "/uploads/test.pdf",
        MimeType = "application/pdf",
        FileSizeBytes = 1024,
        UploadedAtUtc = DateTimeOffset.UtcNow,
        UploadedByUserId = ownerId,
        ProjectId = projectId,
        Category = DocumentCategory.PersonalFiles
    };

    private static (DeleteDocumentCommandHandler handler,
                    Mock<IDocumentRepository> repoMock,
                    Mock<IFileStorageService> storageMock,
                    ApplicationDbContext db)
        Build(Document doc, User? actor = null)
    {
        var repo = new Mock<IDocumentRepository>();
        var audit = new Mock<IDocumentAuditLogRepository>();
        var storage = new Mock<IFileStorageService>();
        var db = MakeDbContext();

        repo.Setup(r => r.GetByIdAsync(doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        repo.Setup(r => r.RemoveAsync(doc, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        audit.Setup(a => a.AddAsync(It.IsAny<DocumentAuditLog>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);
        storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

        if (actor != null)
            db.Users.Add(actor);
        db.SaveChanges();

        var handler = new DeleteDocumentCommandHandler(
            repo.Object, audit.Object, storage.Object, db,
            NullLogger<DeleteDocumentCommandHandler>.Instance);

        return (handler, repo, storage, db);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Owner_HardDeletesFileAndRow()
    {
        var doc = MakeDoc(ownerId: 1, projectId: null);
        var (handler, repo, storage, _) = Build(doc);

        await handler.Handle(new DeleteDocumentCommand(doc.Id, ActorUserId: 1), default);

        // Order: storage.Delete → repo.Remove → audit.Add → SaveChanges
        storage.Verify(s => s.DeleteAsync("/uploads/test.pdf", default), Times.Once);
        repo.Verify(r => r.RemoveAsync(doc, default), Times.Once);
    }

    [Fact]
    public async Task Handle_ProjectManager_Succeeds()
    {
        var doc = MakeDoc(ownerId: 99, projectId: 5);
        var pm = new User { UserId = 2, Role = UserRole.ProjectManager, DisplayName = "PM", Email = "pm@test.com" };
        var (handler, _, _, _) = Build(doc, pm);

        // Should NOT throw
        await handler.Handle(new DeleteDocumentCommand(doc.Id, ActorUserId: 2), default);
    }

    [Fact]
    public async Task Handle_TeamLeadInProject_Succeeds()
    {
        // G2a remediation: TeamLead should also be allowed to delete
        var doc = MakeDoc(ownerId: 99, projectId: 5);
        var tl = new User { UserId = 3, Role = UserRole.TeamLead, DisplayName = "TL", Email = "tl@test.com" };
        var (handler, _, _, _) = Build(doc, tl);

        await handler.Handle(new DeleteDocumentCommand(doc.Id, ActorUserId: 3), default);
    }

    [Fact]
    public async Task Handle_EmployeeOtherOwner_ThrowsForbiddenException()
    {
        var doc = MakeDoc(ownerId: 99, projectId: 5);
        var emp = new User { UserId = 7, Role = UserRole.Employee, DisplayName = "Emp", Email = "e@test.com" };
        var (handler, _, _, _) = Build(doc, emp);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(new DeleteDocumentCommand(doc.Id, ActorUserId: 7), default));
    }

    [Fact]
    public async Task Handle_DocumentNotFound_ThrowsDocumentNotFoundException()
    {
        var repo = new Mock<IDocumentRepository>();
        var audit = new Mock<IDocumentAuditLogRepository>();
        var storage = new Mock<IFileStorageService>();
        var db = MakeDbContext();

        var missingId = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        var handler = new DeleteDocumentCommandHandler(
            repo.Object, audit.Object, storage.Object, db,
            NullLogger<DeleteDocumentCommandHandler>.Instance);

        await Assert.ThrowsAsync<DocumentNotFoundException>(
            () => handler.Handle(new DeleteDocumentCommand(missingId, 1), default));
    }
}
