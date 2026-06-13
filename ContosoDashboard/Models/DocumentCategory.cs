namespace ContosoDashboard.Models;

/// <summary>Classification of a document in the library.</summary>
public enum DocumentCategory
{
    ProjectDocuments = 1,
    TeamResources = 2,
    PersonalFiles = 3,
    Reports = 4,
    Presentations = 5,
    Other = 6
}

/// <summary>
/// Audit trail event types.
/// Only write-side operations generate audit entries (FR-030).
/// </summary>
public enum DocumentAuditEventType
{
    Uploaded = 1,
    Deleted = 2,
    Replaced = 3,
    ShareGranted = 4
}
