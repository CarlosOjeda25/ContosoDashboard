namespace ContosoDashboard.Models;

/// <summary>
/// Base class for all domain exceptions — constitution §III.
/// Infrastructure exceptions must be caught at the Infrastructure boundary,
/// logged, and re-thrown as domain exceptions when appropriate.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when a document is not found or the caller has no visibility into it.
/// Maps to HTTP 404.
/// </summary>
public sealed class DocumentNotFoundException : DomainException
{
    public Guid DocumentId { get; }

    public DocumentNotFoundException(Guid documentId)
        : base($"Document '{documentId}' was not found.")
    {
        DocumentId = documentId;
    }
}

/// <summary>
/// Thrown when the caller lacks the required permission for the requested operation.
/// Maps to HTTP 403.
/// </summary>
public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string reason)
        : base(reason) { }
}

/// <summary>
/// Thrown during the document upload pipeline when a validation or infrastructure step fails.
/// <see cref="ErrorCode"/> controls the HTTP status code (400, 413, 503).
/// </summary>
public sealed class DocumentUploadException : DomainException
{
    /// <summary>Known error codes used by the global exception handler.</summary>
    public static class Codes
    {
        public const string FileTooLarge = nameof(FileTooLarge);
        public const string InvalidMimeType = nameof(InvalidMimeType);
        public const string InfectedFile = nameof(InfectedFile);
        public const string ScannerUnavailable = nameof(ScannerUnavailable);
    }

    public string ErrorCode { get; }

    public DocumentUploadException(string errorCode, string? detail = null)
        : base(detail ?? errorCode)
    {
        ErrorCode = errorCode;
    }
}
