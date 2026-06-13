using ContosoDashboard.Models;
using ContosoDashboard.Services.Documents;
using MediatR;

namespace ContosoDashboard.Services.Documents.Commands;

/// <summary>
/// Command to upload a new document through the 8-step fail-closed pipeline.
/// <see cref="FileStream"/> is a raw stream — NEVER materialised into a <c>byte[]</c>.
/// </summary>
public sealed record UploadDocumentCommand(
    Stream FileStream,
    long FileSizeBytes,
    string OriginalFileName,
    string Title,
    string? Description,
    DocumentCategory Category,
    int? ProjectId,
    int? TaskId,
    IReadOnlyList<string> Tags,
    int ActorUserId
) : IRequest<UploadDocumentResult>;
