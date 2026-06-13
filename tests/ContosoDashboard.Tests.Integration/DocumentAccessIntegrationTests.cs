using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ContosoDashboard.Tests.Integration;

/// <summary>
/// T-062: DocumentAccessService integration tests — full access matrix with EF InMemory.
/// </summary>
public sealed class DocumentAccessIntegrationTests
{
    private static ApplicationDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static User MakeUser(int id, UserRole role = UserRole.Employee) => new()
    {
        UserId = id,
        Role = role,
        DisplayName = $"U{id}",
        Email = $"u{id}@t.com"
    };

    private static Document MakeDoc(Guid id, int ownerId, int? projectId = null) => new()
    {
        Id = id,
        Title = "Integrated Doc",
        OriginalFileName = "d.pdf",
        StoredPath = "/uploads/d.pdf",
        MimeType = "application/pdf",
        FileSizeBytes = 512,
        UploadedAtUtc = DateTimeOffset.UtcNow,
        UploadedByUserId = ownerId,
        ProjectId = projectId,
        Category = DocumentCategory.Other
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Access_ProjectMemberCanViewProjectDocument()
    {
        var db = MakeDb();
        var docId = Guid.NewGuid();
        db.Users.AddRange(MakeUser(1), MakeUser(2));
        db.ProjectMembers.Add(new ProjectMember { UserId = 2, ProjectId = 7 });
        var doc = MakeDoc(docId, ownerId: 1, projectId: 7);
        db.Documents.Add(doc);
        db.SaveChanges();

        var repo = new Mock<IDocumentRepository>();
        repo.Setup(r => r.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var svc = new DocumentAccessService(repo.Object, db, NullLogger<DocumentAccessService>.Instance);
        var result = await svc.AuthorizeAccessAsync(docId, actorUserId: 2, default);

        Assert.Equal(docId, result.Id);
    }

    [Fact]
    public async Task Access_ExplicitShare_AllowsRecipient()
    {
        var db = MakeDb();
        var docId = Guid.NewGuid();
        db.Users.AddRange(MakeUser(1), MakeUser(3));
        var doc = MakeDoc(docId, ownerId: 1);
        db.Documents.Add(doc);
        db.DocumentShares.Add(new DocumentShare
        {
            Id = Guid.NewGuid(),
            DocumentId = docId,
            RecipientUserId = 3,
            GrantedByUserId = 1,
            GrantedAtUtc = DateTimeOffset.UtcNow
        });
        db.SaveChanges();

        var repo = new Mock<IDocumentRepository>();
        repo.Setup(r => r.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var svc = new DocumentAccessService(repo.Object, db, NullLogger<DocumentAccessService>.Instance);
        var result = await svc.AuthorizeAccessAsync(docId, actorUserId: 3, default);

        Assert.Equal(docId, result.Id);
    }

    [Fact]
    public async Task Access_NoRelation_ThrowsForbiddenException()
    {
        var db = MakeDb();
        var docId = Guid.NewGuid();
        db.Users.AddRange(MakeUser(1), MakeUser(5));
        var doc = MakeDoc(docId, ownerId: 1);
        db.Documents.Add(doc);
        db.SaveChanges();

        var repo = new Mock<IDocumentRepository>();
        repo.Setup(r => r.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var svc = new DocumentAccessService(repo.Object, db, NullLogger<DocumentAccessService>.Instance);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => svc.AuthorizeAccessAsync(docId, actorUserId: 5, default));
    }
}
