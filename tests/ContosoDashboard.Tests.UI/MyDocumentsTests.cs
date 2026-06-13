using Bunit;
using ContosoDashboard.Models;
using ContosoDashboard.Pages;
using ContosoDashboard.Services;
using ContosoDashboard.Services.Documents;
using ContosoDashboard.Services.Documents.Queries;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ContosoDashboard.Tests.UI;

/// <summary>T-064: MyDocuments page bUnit tests (bUnit 2.7+ API).</summary>
public sealed class MyDocumentsTests : BunitContext
{
    private Mock<IMediator> SetupAuth(PagedResult<DocumentSummary>? result = null)
    {
        var mediator = new Mock<IMediator>();
        var pagedResult = result ?? new PagedResult<DocumentSummary>(
            Array.Empty<DocumentSummary>(), 0, 1, 20);

        mediator.Setup(m => m.Send(
                    It.IsAny<GetMyDocumentsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(pagedResult);

        Services.AddScoped(_ => mediator.Object);
        var auth = AddAuthorization();
        auth.SetAuthorized("testuser");
        auth.SetRoles("Employee");

        return mediator;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_ShowsUploadButton()
    {
        SetupAuth();
        var cut = Render<MyDocuments>();
        Assert.Contains("/documents/upload", cut.Markup);
    }

    [Fact]
    public void Render_EmptyState_ShowsHelpMessage()
    {
        SetupAuth();
        var cut = Render<MyDocuments>();

        Assert.Contains("No documents found", cut.Markup);
    }

    [Fact]
    public void Render_WithDocuments_ShowsTitleAndDownloadLink()
    {
        var docId = Guid.NewGuid();
        var doc = new DocumentSummary(
            Id: docId, Title: "My Report", Description: null,
            Category: DocumentCategory.Reports,
            UploadedAtUtc: DateTimeOffset.UtcNow, UploadedByUserId: 1,
            UploaderName: "Test User", ProjectId: null, ProjectName: null,
            FileSizeBytes: 2048, MimeType: "application/pdf",
            Tags: Array.Empty<string>());
        var page = new PagedResult<DocumentSummary>(new[] { doc }, 1, 1, 20);

        SetupAuth(page);
        var cut = Render<MyDocuments>();

        Assert.Contains("My Report", cut.Markup);
        Assert.Contains($"/api/documents/{docId}/download", cut.Markup);
    }

    [Fact]
    public void Render_FilterControls_Present()
    {
        SetupAuth();
        var cut = Render<MyDocuments>();

        Assert.Contains("Category", cut.Markup);
        Assert.Contains("From", cut.Markup);
        Assert.Contains("Filter", cut.Markup);
    }
}
