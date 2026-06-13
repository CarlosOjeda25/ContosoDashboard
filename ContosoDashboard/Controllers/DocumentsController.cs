using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Services.Documents;
using ContosoDashboard.Services.Documents.Commands;
using ContosoDashboard.Services.Documents.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ContosoDashboard.Controllers;

/// <summary>
/// REST API for document management.
/// All endpoints require authentication.
/// Business rules and RBAC are enforced in the CQRS handlers.
/// </summary>
[ApiController]
[Route("api/documents")]
[Authorize]
public sealed class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFileStorageService _storage;
    private readonly DocumentAccessService _access;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IMediator mediator,
        IFileStorageService storage,
        DocumentAccessService access,
        ILogger<DocumentsController> logger)
    {
        _mediator = mediator;
        _storage = storage;
        _access = access;
        _logger = logger;
    }

    // ── POST /api/documents/upload ─────────────────────────────────────────

    [HttpPost("upload")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 31_457_280)] // 30 MB — handler enforces 25 MB business limit
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] string title,
        [FromForm] DocumentCategory category,
        [FromForm] string? description = null,
        [FromForm] int? projectId = null,
        [FromForm] int? taskId = null,
        [FromForm] string? tags = null,
        CancellationToken ct = default)
    {
        // Early size gate — returns 413 before dispatching to MediatR.
        // The handler performs the same check for defense in depth.
        if (file.Length > 25L * 1024 * 1024)
            return StatusCode(StatusCodes.Status413RequestEntityTooLarge, new
            {
                status = 413,
                title = "The uploaded file exceeds the maximum allowed size.",
                instance = HttpContext.Request.Path.Value
            });

        var tagList = tags is null
            ? Array.Empty<string>()
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var command = new UploadDocumentCommand(
            FileStream: file.OpenReadStream(),
            FileSizeBytes: file.Length,
            OriginalFileName: file.FileName,
            Title: title,
            Description: description,
            Category: category,
            ProjectId: projectId,
            TaskId: taskId,
            Tags: tagList,
            ActorUserId: GetCurrentUserId());

        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Download), new { id = result.DocumentId }, result);
    }

    // ── GET /api/documents ─────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Browse(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DocumentCategory? category = null,
        [FromQuery] int? projectId = null,
        [FromQuery] DateTimeOffset? fromDateUtc = null,
        [FromQuery] DateTimeOffset? toDateUtc = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDesc = false,
        CancellationToken ct = default)
    {
        var filter = new DocumentFilter(
            UserId: GetCurrentUserId(),
            ProjectId: projectId,
            Category: category,
            FromUtc: fromDateUtc,
            ToUtc: toDateUtc,
            SortBy: sortBy,
            SortDescending: sortDesc,
            Page: page,
            PageSize: pageSize);

        var query = new GetMyDocumentsQuery(GetCurrentUserId(), filter);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    // ── GET /api/documents/search ──────────────────────────────────────────

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string searchTerm,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new SearchDocumentsQuery(searchTerm, GetCurrentUserId(), page, pageSize);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    // ── GET /api/documents/shared-with-me ─────────────────────────────────

    [HttpGet("shared-with-me")]
    public async Task<IActionResult> SharedWithMe(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetSharedWithMeQuery(GetCurrentUserId(), page, pageSize);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    // ── GET /api/documents/{id}/download ──────────────────────────────────

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var document = await _access.AuthorizeAccessAsync(id, GetCurrentUserId(), ct);

        var stream = await _storage.DownloadAsync(document.StoredPath, ct);
        return File(stream, document.MimeType,
            fileDownloadName: document.OriginalFileName,
            enableRangeProcessing: true);
    }

    // ── GET /api/documents/{id}/preview ───────────────────────────────────

    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken ct)
    {
        var document = await _access.AuthorizeAccessAsync(id, GetCurrentUserId(), ct);

        // Only inline-safe types (FR-021)
        if (!IsPreviewable(document.MimeType))
            return BadRequest(new { message = "This file type cannot be previewed inline." });

        var stream = await _storage.DownloadAsync(document.StoredPath, ct);
        return File(stream, document.MimeType, enableRangeProcessing: true); // no fileDownloadName → inline
    }

    // ── PATCH /api/documents/{id}/metadata ────────────────────────────────

    [HttpPatch("{id:guid}/metadata")]
    public async Task<IActionResult> UpdateMetadata(
        Guid id,
        [FromBody] UpdateMetadataRequest body,
        CancellationToken ct = default)
    {
        var command = new UpdateDocumentMetadataCommand(
            DocumentId: id,
            Title: body.Title,
            Description: body.Description,
            Category: body.Category,
            Tags: body.Tags,
            ActorUserId: GetCurrentUserId());

        await _mediator.Send(command, ct);
        return NoContent();
    }

    // ── POST /api/documents/{id}/replace ──────────────────────────────────

    [HttpPost("{id:guid}/replace")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 31_457_280)]
    public async Task<IActionResult> Replace(
        Guid id,
        IFormFile file,
        CancellationToken ct = default)
    {
        var command = new ReplaceDocumentCommand(
            DocumentId: id,
            FileStream: file.OpenReadStream(),
            OriginalFileName: file.FileName,
            ActorUserId: GetCurrentUserId());

        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    // ── DELETE /api/documents/{id} ────────────────────────────────────────

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteDocumentCommand(id, GetCurrentUserId()), ct);
        return NoContent();
    }

    // ── POST /api/documents/{id}/share ────────────────────────────────────

    [HttpPost("{id:guid}/share")]
    public async Task<IActionResult> Share(
        Guid id,
        [FromBody] ShareRequest body,
        CancellationToken ct = default)
    {
        var command = new ShareDocumentCommand(
            DocumentId: id,
            ActorUserId: GetCurrentUserId(),
            RecipientUserIds: body.RecipientUserIds,
            RecipientTeamIds: body.RecipientTeamIds);

        await _mediator.Send(command, ct);
        return Ok();
    }

    // ── GET /api/documents/{id}/audit ─────────────────────────────────────

    [HttpGet("{id:guid}/audit")]
    public async Task<IActionResult> AuditLog(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetDocumentAuditLogQuery(id, GetCurrentUserId(), page, pageSize);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private int GetCurrentUserId()
    {
        // [Authorize] guarantees the claim exists; ParseException here is a bug, not a user error.
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("NameIdentifier claim missing despite [Authorize]. Check authentication configuration.");
        return int.Parse(claim, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool IsPreviewable(string mimeType) =>
        mimeType is "application/pdf" or "image/jpeg" or "image/png";
}

// ── Request body DTOs ─────────────────────────────────────────────────────────

public sealed record UpdateMetadataRequest(
    string Title,
    string? Description,
    DocumentCategory Category,
    IReadOnlyList<string> Tags);

public sealed record ShareRequest(
    IReadOnlyList<int> RecipientUserIds,
    IReadOnlyList<int> RecipientTeamIds);
