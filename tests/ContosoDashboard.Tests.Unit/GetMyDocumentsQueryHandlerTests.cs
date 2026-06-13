using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Services.Documents;
using ContosoDashboard.Services.Documents.Queries;
using Moq;

namespace ContosoDashboard.Tests.Unit;

/// <summary>T-052: GetMyDocumentsQueryHandler unit tests (delegates to IDocumentRepository).</summary>
public sealed class GetMyDocumentsQueryHandlerTests
{
    private static GetMyDocumentsQueryHandler MakeHandler(Mock<IDocumentRepository> repo) =>
        new(repo.Object);

    private static DocumentFilter BaseFilter(string sortBy = "uploadDate", bool desc = true) =>
        new(UserId: null, ProjectId: null, Category: null,
            FromUtc: null, ToUtc: null,
            SortBy: sortBy, SortDescending: desc, Page: 1, PageSize: 20);

    private static PagedResult<DocumentSummary> EmptyPage() =>
        new(Array.Empty<DocumentSummary>(), 0, 1, 20);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsOnlyUploadersDocuments_Paginated()
    {
        var repo = new Mock<IDocumentRepository>();
        repo.Setup(r => r.GetPagedAsync(
                It.Is<DocumentFilter>(f => f.UserId == 5),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyPage());

        var handler = MakeHandler(repo);
        await handler.Handle(new GetMyDocumentsQuery(5, BaseFilter()), default);

        // Filter MUST be scoped to userId=5, never null
        repo.Verify(r => r.GetPagedAsync(
            It.Is<DocumentFilter>(f => f.UserId == 5), default), Times.Once);
    }

    [Fact]
    public async Task Handle_FilterByCategory_ReturnsMatchingOnly()
    {
        var repo = new Mock<IDocumentRepository>();
        repo.Setup(r => r.GetPagedAsync(It.IsAny<DocumentFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyPage());

        var filter = BaseFilter() with { Category = DocumentCategory.Reports };
        var handler = MakeHandler(repo);
        await handler.Handle(new GetMyDocumentsQuery(1, filter), default);

        repo.Verify(r => r.GetPagedAsync(
            It.Is<DocumentFilter>(f => f.Category == DocumentCategory.Reports), default), Times.Once);
    }

    [Fact]
    public async Task Handle_FilterByDateRange_ReturnsMatchingOnly()
    {
        var repo = new Mock<IDocumentRepository>();
        repo.Setup(r => r.GetPagedAsync(It.IsAny<DocumentFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyPage());

        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to = DateTimeOffset.UtcNow;
        var filter = BaseFilter() with { FromUtc = from, ToUtc = to };
        var handler = MakeHandler(repo);
        await handler.Handle(new GetMyDocumentsQuery(1, filter), default);

        repo.Verify(r => r.GetPagedAsync(
            It.Is<DocumentFilter>(f => f.FromUtc == from && f.ToUtc == to), default), Times.Once);
    }

    [Fact]
    public async Task Handle_SortByUploadDateDesc_OrderCorrect()
    {
        var repo = new Mock<IDocumentRepository>();
        repo.Setup(r => r.GetPagedAsync(It.IsAny<DocumentFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyPage());

        var filter = BaseFilter("uploadDate", desc: true);
        await MakeHandler(repo).Handle(new GetMyDocumentsQuery(1, filter), default);

        repo.Verify(r => r.GetPagedAsync(
            It.Is<DocumentFilter>(f => f.SortBy == "uploadDate" && f.SortDescending), default), Times.Once);
    }

    [Fact]
    public async Task Handle_SortByTitle_OrderCorrect()
    {
        // G1a remediation
        var repo = new Mock<IDocumentRepository>();
        repo.Setup(r => r.GetPagedAsync(It.IsAny<DocumentFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyPage());

        var filter = BaseFilter("title", desc: false);
        await MakeHandler(repo).Handle(new GetMyDocumentsQuery(1, filter), default);

        repo.Verify(r => r.GetPagedAsync(
            It.Is<DocumentFilter>(f => f.SortBy == "title" && !f.SortDescending), default), Times.Once);
    }

    [Fact]
    public async Task Handle_SortByCategory_OrderCorrect()
    {
        // G1b remediation
        var repo = new Mock<IDocumentRepository>();
        repo.Setup(r => r.GetPagedAsync(It.IsAny<DocumentFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyPage());

        var filter = BaseFilter("category", desc: false);
        await MakeHandler(repo).Handle(new GetMyDocumentsQuery(1, filter), default);

        repo.Verify(r => r.GetPagedAsync(
            It.Is<DocumentFilter>(f => f.SortBy == "category"), default), Times.Once);
    }

    [Fact]
    public async Task Handle_SortByFileSize_OrderCorrect()
    {
        // G1c remediation
        var repo = new Mock<IDocumentRepository>();
        repo.Setup(r => r.GetPagedAsync(It.IsAny<DocumentFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyPage());

        var filter = BaseFilter("fileSize", desc: true);
        await MakeHandler(repo).Handle(new GetMyDocumentsQuery(1, filter), default);

        repo.Verify(r => r.GetPagedAsync(
            It.Is<DocumentFilter>(f => f.SortBy == "fileSize" && f.SortDescending), default), Times.Once);
    }
}
