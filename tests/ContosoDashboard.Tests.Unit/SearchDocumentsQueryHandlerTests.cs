using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services.Documents;
using ContosoDashboard.Services.Documents.Queries;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Tests.Unit;

/// <summary>T-055: SearchDocumentsQueryHandler unit tests (in-memory EF).</summary>
public sealed class SearchDocumentsQueryHandlerTests
{
    private static ApplicationDbContext MakeDbContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static User MakeUser(int id, UserRole role = UserRole.Employee) => new()
    {
        UserId = id,
        Role = role,
        DisplayName = $"User{id}",
        Email = $"u{id}@t.com"
    };

    private static Document MakeDoc(Guid id, int ownerId, string title,
        int? projectId = null, string? description = null) => new()
        {
            Id = id,
            Title = title,
            Description = description,
            OriginalFileName = "d.pdf",
            StoredPath = $"/uploads/{id}.pdf",
            MimeType = "application/pdf",
            FileSizeBytes = 512,
            UploadedAtUtc = DateTimeOffset.UtcNow,
            UploadedByUserId = ownerId,
            ProjectId = projectId,
            Category = DocumentCategory.Other
        };

    private static SearchDocumentsQueryHandler MakeHandler(ApplicationDbContext db) =>
        new(db);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_SearchByTitle_ReturnsMatchingDocuments()
    {
        var db = MakeDbContext();
        var user = MakeUser(1);
        db.Users.Add(user);
        var doc = MakeDoc(Guid.NewGuid(), ownerId: 1, title: "Budget Report 2025");
        db.Documents.Add(doc);
        db.SaveChanges();

        var handler = MakeHandler(db);
        var result = await handler.Handle(
            new SearchDocumentsQuery("budget", ActorUserId: 1, Page: 1, PageSize: 10), default);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Budget Report 2025", result.Items[0].Title);
    }

    [Fact]
    public async Task Handle_SearchByTag_ReturnsMatchingDocuments()
    {
        var db = MakeDbContext();
        var user = MakeUser(1);
        db.Users.Add(user);
        var docId = Guid.NewGuid();
        var doc = MakeDoc(docId, ownerId: 1, title: "My File");
        doc.Tags.Add(new DocumentTag { Id = Guid.NewGuid(), DocumentId = docId, Value = "finance" });
        db.Documents.Add(doc);
        db.SaveChanges();

        var handler = MakeHandler(db);
        var result = await handler.Handle(
            new SearchDocumentsQuery("finance", ActorUserId: 1, Page: 1, PageSize: 10), default);

        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Handle_ExcludesDocumentsOutsideUserRbacScope()
    {
        var db = MakeDbContext();
        db.Users.AddRange(MakeUser(1), MakeUser(2));
        // Doc owned by user 2, no share
        var doc = MakeDoc(Guid.NewGuid(), ownerId: 2, title: "Private Doc");
        db.Documents.Add(doc);
        db.SaveChanges();

        var handler = MakeHandler(db);
        // User 1 searches — should get 0 results
        var result = await handler.Handle(
            new SearchDocumentsQuery("private", ActorUserId: 1, Page: 1, PageSize: 10), default);

        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Handle_SearchByProject_ReturnsMatchingDocuments()
    {
        var db = MakeDbContext();
        var owner = MakeUser(1);
        db.Users.Add(owner);
        var project = new Project
        {
            ProjectId = 10,
            Name = "Phoenix Project",
            Description = "test",
            Status = ProjectStatus.Active,
            StartDate = DateTime.Today,
            ProjectManagerId = 1
        };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember { UserId = 1, ProjectId = 10 });
        var doc = MakeDoc(Guid.NewGuid(), ownerId: 1, title: "Spec Doc", projectId: 10);
        db.Documents.Add(doc);
        db.SaveChanges();

        var handler = MakeHandler(db);
        var result = await handler.Handle(
            new SearchDocumentsQuery("phoenix", ActorUserId: 1, Page: 1, PageSize: 10), default);

        Assert.Equal(1, result.TotalCount);
    }
}
