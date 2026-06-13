using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ContosoDashboard.Tests.Unit;

/// <summary>T-057: DocumentAccessService unit tests (5-level access matrix).</summary>
public sealed class DocumentAccessServiceTests
{
    private static ApplicationDbContext MakeDbContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Document MakeDoc(Guid id, int ownerId, int? projectId = null) => new()
    {
        Id = id,
        Title = "Doc",
        OriginalFileName = "doc.pdf",
        StoredPath = "/uploads/doc.pdf",
        MimeType = "application/pdf",
        FileSizeBytes = 512,
        UploadedAtUtc = DateTimeOffset.UtcNow,
        UploadedByUserId = ownerId,
        ProjectId = projectId,
        Category = DocumentCategory.Other
    };

    private static (DocumentAccessService svc, Mock<IDocumentRepository> repo)
        Build(Document? doc, ApplicationDbContext db)
    {
        var repo = new Mock<IDocumentRepository>();
        if (doc != null)
            repo.Setup(r => r.GetByIdAsync(doc.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(doc);
        else
        {
            repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Document?)null);
        }

        var svc = new DocumentAccessService(
            repo.Object, db, NullLogger<DocumentAccessService>.Instance);

        return (svc, repo);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AuthorizeAccessAsync_Owner_Granted()
    {
        var db = MakeDbContext();
        db.Users.Add(new User { UserId = 1, Role = UserRole.Employee, DisplayName = "U1", Email = "u1@t.com" });
        db.SaveChanges();

        var docId = Guid.NewGuid();
        var doc = MakeDoc(docId, ownerId: 1);
        var (svc, _) = Build(doc, db);

        var result = await svc.AuthorizeAccessAsync(docId, actorUserId: 1, default);
        Assert.Equal(docId, result.Id);
    }

    [Fact]
    public async Task AuthorizeAccessAsync_ProjectMember_Granted()
    {
        var db = MakeDbContext();
        db.Users.Add(new User { UserId = 2, Role = UserRole.Employee, DisplayName = "U2", Email = "u2@t.com" });
        db.ProjectMembers.Add(new ProjectMember { UserId = 2, ProjectId = 5 });
        db.SaveChanges();

        var docId = Guid.NewGuid();
        var doc = MakeDoc(docId, ownerId: 99, projectId: 5);
        var (svc, _) = Build(doc, db);

        var result = await svc.AuthorizeAccessAsync(docId, actorUserId: 2, default);
        Assert.Equal(docId, result.Id);
    }

    [Fact]
    public async Task AuthorizeAccessAsync_SharedRecipient_Granted()
    {
        var db = MakeDbContext();
        db.Users.Add(new User { UserId = 3, Role = UserRole.Employee, DisplayName = "U3", Email = "u3@t.com" });
        db.SaveChanges();

        var docId = Guid.NewGuid();
        var doc = MakeDoc(docId, ownerId: 99);
        // Service checks doc.Shares navigation property — populate it directly
        // because the mock returns this instance without loading from DB
        doc.Shares.Add(new DocumentShare
        {
            Id = Guid.NewGuid(),
            DocumentId = docId,
            RecipientUserId = 3,
            GrantedByUserId = 99,
            GrantedAtUtc = DateTimeOffset.UtcNow
        });

        var (svc, _) = Build(doc, db);

        var result = await svc.AuthorizeAccessAsync(docId, actorUserId: 3, default);
        Assert.Equal(docId, result.Id);
    }

    [Fact]
    public async Task AuthorizeAccessAsync_Administrator_Granted()
    {
        var db = MakeDbContext();
        db.Users.Add(new User { UserId = 4, Role = UserRole.Administrator, DisplayName = "Admin", Email = "a@t.com" });
        db.SaveChanges();

        var docId = Guid.NewGuid();
        var doc = MakeDoc(docId, ownerId: 99);
        var (svc, _) = Build(doc, db);

        var result = await svc.AuthorizeAccessAsync(docId, actorUserId: 4, default);
        Assert.Equal(docId, result.Id);
    }

    [Fact]
    public async Task AuthorizeAccessAsync_NoAccess_ThrowsForbiddenException()
    {
        var db = MakeDbContext();
        db.Users.Add(new User { UserId = 5, Role = UserRole.Employee, DisplayName = "U5", Email = "u5@t.com" });
        db.SaveChanges();

        var docId = Guid.NewGuid();
        var doc = MakeDoc(docId, ownerId: 99);
        var (svc, _) = Build(doc, db);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => svc.AuthorizeAccessAsync(docId, actorUserId: 5, default));
    }

    [Fact]
    public async Task AuthorizeAccessAsync_DocumentNotFound_ThrowsDocumentNotFoundException()
    {
        var db = MakeDbContext();
        var (svc, _) = Build(doc: null, db);
        var missingId = Guid.NewGuid();

        await Assert.ThrowsAsync<DocumentNotFoundException>(
            () => svc.AuthorizeAccessAsync(missingId, actorUserId: 1, default));
    }
}
