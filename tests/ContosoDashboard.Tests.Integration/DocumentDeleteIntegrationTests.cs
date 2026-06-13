using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Services.Documents;
using ContosoDashboard.Services.Documents.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ContosoDashboard.Tests.Integration;

/// <summary>
/// T-060: Document delete RBAC integration tests.
/// Uses InMemory EF to verify hard-delete semantics across all role combinations.
/// </summary>
public sealed class DocumentDeleteIntegrationTests
{
    private static ApplicationDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static User MakeUser(int id, UserRole role) => new()
    {
        UserId = id,
        Role = role,
        DisplayName = $"U{id}",
        Email = $"u{id}@t.com"
    };

    private static Document MakeDoc(int ownerId, int? projectId = null) => new()
    {
        Id = Guid.NewGuid(),
        Title = "To Delete",
        OriginalFileName = "del.pdf",
        StoredPath = "/uploads/del.pdf",
        MimeType = "application/pdf",
        FileSizeBytes = 512,
        UploadedAtUtc = DateTimeOffset.UtcNow,
        UploadedByUserId = ownerId,
        ProjectId = projectId,
        Category = DocumentCategory.Other
    };

    private static (DeleteDocumentCommandHandler handler, Mock<IFileStorageService> storage)
        BuildHandler(ApplicationDbContext db)
    {
        var repo = new Mock<IDocumentRepository>();
        var audit = new Mock<IDocumentAuditLogRepository>();
        var storage = new Mock<IFileStorageService>();

        // Delegate repo calls to db directly
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                db.Documents.FirstOrDefault(d => d.Id == id));
        repo.Setup(r => r.RemoveAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .Returns((Document doc, CancellationToken _) =>
            {
                db.Documents.Remove(doc);
                return Task.CompletedTask;
            });
        audit.Setup(a => a.AddAsync(It.IsAny<DocumentAuditLog>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);
        storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

        var handler = new DeleteDocumentCommandHandler(
            repo.Object, audit.Object, storage.Object, db,
            NullLogger<DeleteDocumentCommandHandler>.Instance);

        return (handler, storage);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_Owner_RemovesDocumentAndFile()
    {
        var db = MakeDb();
        var doc = MakeDoc(ownerId: 1);
        db.Users.Add(MakeUser(1, UserRole.Employee));
        db.Documents.Add(doc);
        db.SaveChanges();

        var (handler, storage) = BuildHandler(db);
        await handler.Handle(new DeleteDocumentCommand(doc.Id, ActorUserId: 1), default);

        Assert.False(db.Documents.Any(d => d.Id == doc.Id));
        storage.Verify(s => s.DeleteAsync("/uploads/del.pdf", default), Times.Once);
    }

    [Fact]
    public async Task Delete_EmployeeNotOwner_ThrowsForbiddenException()
    {
        var db = MakeDb();
        var doc = MakeDoc(ownerId: 99, projectId: null);
        db.Users.AddRange(
            MakeUser(99, UserRole.Employee),
            MakeUser(7, UserRole.Employee));
        db.Documents.Add(doc);
        db.SaveChanges();

        var (handler, _) = BuildHandler(db);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(new DeleteDocumentCommand(doc.Id, ActorUserId: 7), default));

        // Document still exists
        Assert.True(db.Documents.Any(d => d.Id == doc.Id));
    }

    [Fact]
    public async Task Delete_Administrator_AlwaysSucceeds()
    {
        var db = MakeDb();
        var doc = MakeDoc(ownerId: 99, projectId: null);
        db.Users.AddRange(
            MakeUser(99, UserRole.Employee),
            MakeUser(1, UserRole.Administrator));
        db.Documents.Add(doc);
        db.SaveChanges();

        var (handler, _) = BuildHandler(db);
        await handler.Handle(new DeleteDocumentCommand(doc.Id, ActorUserId: 1), default);

        Assert.False(db.Documents.Any(d => d.Id == doc.Id));
    }
}
