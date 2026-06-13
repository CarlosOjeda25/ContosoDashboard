using Bunit;
using ContosoDashboard.Models;
using ContosoDashboard.Services.Documents;
using ContosoDashboard.Shared;

namespace ContosoDashboard.Tests.UI;

/// <summary>T-065: RecentDocumentsWidget bUnit tests (bUnit 2.7+ API).</summary>
public sealed class RecentDocumentsWidgetTests : BunitContext
{
    private static DocumentSummary MakeDoc(string title) => new(
        Id: Guid.NewGuid(),
        Title: title,
        Description: null,
        Category: DocumentCategory.Reports,
        UploadedAtUtc: DateTimeOffset.UtcNow,
        UploadedByUserId: 1,
        UploaderName: "Test User",
        ProjectId: null,
        ProjectName: null,
        FileSizeBytes: 512,
        MimeType: "application/pdf",
        Tags: Array.Empty<string>());

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Widget_NullDocuments_RendersLoadingMessage()
    {
        var cut = Render<RecentDocumentsWidget>(
            p => p.Add(c => c.Documents, (IReadOnlyList<DocumentSummary>?)null));

        cut.MarkupMatches("<p><em>Loading\u2026</em></p>");
    }

    [Fact]
    public void Widget_EmptyList_RendersEmptyState()
    {
        var cut = Render<RecentDocumentsWidget>(
            p => p.Add(c => c.Documents, Array.Empty<DocumentSummary>()));

        Assert.Contains("no has subido documentos", cut.Markup);
    }

    [Fact]
    public void Widget_WithDocuments_RendersTitlesAndViewAllLink()
    {
        var docs = new[] { MakeDoc("Budget 2025"), MakeDoc("Q1 Report") };

        var cut = Render<RecentDocumentsWidget>(
            p => p.Add(c => c.Documents, docs));

        Assert.Contains("Budget 2025", cut.Markup);
        Assert.Contains("Q1 Report", cut.Markup);
        Assert.Contains("View all documents", cut.Markup);
    }

    [Fact]
    public void Widget_WithDocuments_DownloadLinkPointsToApiRoute()
    {
        var doc = MakeDoc("My File");
        var cut = Render<RecentDocumentsWidget>(
            p => p.Add(c => c.Documents, new[] { doc }));

        var link = cut.Find("a[href*='/api/documents/']");
        Assert.Contains(doc.Id.ToString(), link.GetAttribute("href"));
    }
}
