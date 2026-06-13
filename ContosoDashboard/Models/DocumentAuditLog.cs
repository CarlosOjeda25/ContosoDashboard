namespace ContosoDashboard.Models;

/// <summary>
/// Immutable audit trail entry for write-side document operations.
/// No public setters post-construction; entries are never deleted (FR-030).
/// </summary>
public sealed class DocumentAuditLog
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = default!;

    public DocumentAuditEventType EventType { get; set; }

    /// <summary>FK to <see cref="User.UserId"/> (int).</summary>
    public int ActorUserId { get; set; }
    public User ActorUser { get; set; } = default!;

    public DateTimeOffset OccurredAtUtc { get; set; }
}
