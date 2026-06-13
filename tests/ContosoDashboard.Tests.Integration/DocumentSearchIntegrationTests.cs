using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Services.Documents.Queries;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Tests.Integration;

/// <summary>
/// T-061: Document search integration tests.
/// Uses InMemory EF to verify SearchDocumentsQueryHandler end-to-end.
/// </summary>
public sealed class DocumentSearchIntegrationTests
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

    private static Document MakeDoc(int ownerId, string title, int? projectId = null,
        string? description = null) => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            OriginalFileName = "d.pdf",
            StoredPath = $"/uploads/{Guid.NewGuid()}.pdf",
            MimeType = "application/pdf",
            FileSizeBytes = 512,
            UploadedAtUtc = DateTimeOffset.UtcNow,
            UploadedByUserId = ownerId,
            ProjectId = projectId,
            Category = DocumentCategory.Other
        };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_ByTitle_ReturnsMatchingDocuments()
    {
        var db = MakeDb();
        db.Users.Add(MakeUser(1));
        db.Documents.AddRange(
            MakeDoc(1, "Annual Budget 2025"),
            MakeDoc(1, "Meeting Notes"));
        db.SaveChanges();

        var handler = new SearchDocumentsQueryHandler(db);
        var result = await handler.Handle(
            new SearchDocumentsQuery("budget", ActorUserId: 1, Page: 1, PageSize: 10), default);

        Assert.Equal(1, result.TotalCount);
        Assert.Contains("Budget", result.Items[0].Title);
    }

    [Fact]
    public async Task Search_RbacFiltersPrivateDocuments()
    {
        var db = MakeDb();
        db.Users.AddRange(MakeUser(1), MakeUser(2));
        // User 2's private document — user 1 should NOT see it
        db.Documents.Add(MakeDoc(2, "Private Memo"));
        db.SaveChanges();

        var handler = new SearchDocumentsQueryHandler(db);
        var result = await handler.Handle(
            new SearchDocumentsQuery("private", ActorUserId: 1, Page: 1, PageSize: 10), default);

        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Search_Admin_SeesAllDocuments()
    {
        var db = MakeDb();
        db.Users.AddRange(MakeUser(1), MakeUser(999, UserRole.Administrator));
        db.Documents.AddRange(
            MakeDoc(1, "Private Doc A"),
            MakeDoc(1, "Private Doc B"));
        db.SaveChanges();

        var handler = new SearchDocumentsQueryHandler(db);
        var result = await handler.Handle(
            new SearchDocumentsQuery("private", ActorUserId: 999, Page: 1, PageSize: 10), default);

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Search_Pagination_ReturnsCorrectPage()
    {
        var db = MakeDb();
        db.Users.Add(MakeUser(1));
        for (int i = 1; i <= 15; i++)
            db.Documents.Add(MakeDoc(1, $"Document {i:D2}"));
        db.SaveChanges();

        var handler = new SearchDocumentsQueryHandler(db);
        var page2 = await handler.Handle(
            new SearchDocumentsQuery("document", ActorUserId: 1, Page: 2, PageSize: 10), default);

        Assert.Equal(15, page2.TotalCount);
        Assert.Equal(5, page2.Items.Count);
    }
}
