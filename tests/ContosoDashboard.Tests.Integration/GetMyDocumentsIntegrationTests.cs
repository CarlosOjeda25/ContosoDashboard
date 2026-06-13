using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Services.Documents;
using ContosoDashboard.Services.Documents.Queries;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Tests.Integration;

/// <summary>
/// T-059: GetMyDocuments integration tests — EF query roundtrip.
/// Directly exercises the QueryHandler against InMemory EF to verify paging, filters, RBAC.
/// </summary>
public sealed class GetMyDocumentsIntegrationTests
{
    private static ApplicationDbContext MakeDb()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(opts);
    }

    private static User MakeUser(int id) => new()
    {
        UserId = id,
        Role = UserRole.Employee,
        DisplayName = $"U{id}",
        Email = $"u{id}@t.com"
    };

    private static Document MakeDoc(int ownerId, string title,
        DocumentCategory cat = DocumentCategory.Other, int? projectId = null) => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            OriginalFileName = "d.pdf",
            StoredPath = $"/uploads/{Guid.NewGuid()}.pdf",
            MimeType = "application/pdf",
            FileSizeBytes = 1024,
            UploadedAtUtc = DateTimeOffset.UtcNow,
            UploadedByUserId = ownerId,
            ProjectId = projectId,
            Category = cat
        };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyDocuments_ReturnsPaginatedResults_OwnerOnly()
    {
        var db = MakeDb();
        db.Users.AddRange(MakeUser(1), MakeUser(2));
        db.Documents.AddRange(
            MakeDoc(1, "Doc A"),
            MakeDoc(1, "Doc B"),
            MakeDoc(2, "Other User Doc"));  // should NOT appear
        db.SaveChanges();

        var repo = new DocumentRepository(db);
        var handler = new GetMyDocumentsQueryHandler(repo);
        var filter = new DocumentFilter(UserId: null, null, null, null, null, "uploadDate", true, 1, 10);

        var result = await handler.Handle(new GetMyDocumentsQuery(1, filter), default);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, d => Assert.Equal(1, d.UploadedByUserId));
    }

    [Fact]
    public async Task GetMyDocuments_FilterByCategory_ReturnsMatching()
    {
        var db = MakeDb();
        db.Users.Add(MakeUser(1));
        db.Documents.AddRange(
            MakeDoc(1, "Report", DocumentCategory.Reports),
            MakeDoc(1, "Personal", DocumentCategory.PersonalFiles));
        db.SaveChanges();

        var repo = new DocumentRepository(db);
        var handler = new GetMyDocumentsQueryHandler(repo);
        var filter = new DocumentFilter(null, null, DocumentCategory.Reports, null, null, "uploadDate", true, 1, 10);

        var result = await handler.Handle(new GetMyDocumentsQuery(1, filter), default);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Report", result.Items[0].Title);
    }
}
