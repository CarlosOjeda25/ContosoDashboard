using Bunit;
using ContosoDashboard.Models;
using ContosoDashboard.Pages;
using ContosoDashboard.Services.Documents;
using ContosoDashboard.Services.Documents.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ContosoDashboard.Tests.UI;

/// <summary>T-063: DocumentUpload page bUnit tests (bUnit 2.7+ API).</summary>
public sealed class DocumentUploadTests : BunitContext
{
    private void SetupAuth(Mock<IMediator>? mediator = null)
    {
        mediator ??= new Mock<IMediator>();
        Services.AddScoped(_ => mediator.Object);
        var auth = AddAuthorization();
        auth.SetAuthorized("testuser");
        auth.SetRoles("Employee");
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_UploadForm_ContainsTitleAndCategoryInputs()
    {
        SetupAuth();
        var cut = Render<DocumentUpload>();

        Assert.Contains("Title", cut.Markup);
        Assert.Contains("Category", cut.Markup);
        Assert.Contains("Upload", cut.Markup);
    }

    [Fact]
    public void Render_WithQueryProjectId_ProjectInputHidden()
    {
        SetupAuth();
        Services.GetRequiredService<Bunit.TestDoubles.BunitNavigationManager>().NavigateTo("/documents/upload?ProjectId=42");
        var cut = Render<DocumentUpload>();

        Assert.Contains("type=\"hidden\"", cut.Markup);
        Assert.DoesNotContain("Project (optional)", cut.Markup);
    }

    [Fact]
    public void Render_MaxFileSizeHint_IsDisplayed()
    {
        SetupAuth();
        var cut = Render<DocumentUpload>();

        Assert.Contains("25 MB", cut.Markup);
    }

    [Fact]
    public void Render_SubmitButton_DisabledWithNoFileSelected()
    {
        SetupAuth();
        var cut = Render<DocumentUpload>();

        var btn = cut.Find("button[type='submit']");
        Assert.NotNull(btn.GetAttribute("disabled"));
    }
}
