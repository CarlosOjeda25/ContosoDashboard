namespace ContosoDashboard.Models;

/// <summary>
/// Records that a document was shared with a specific user or with an entire
/// project team (A1: <see cref="RecipientTeamId"/> stores <see cref="Project.ProjectId"/>
/// until a dedicated Team entity is introduced in v2).
/// </summary>
public sealed class DocumentShare
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = default!;

    /// <summary>FK to <see cref="User.UserId"/> (int). Null if this is a team share.</summary>
    public int? RecipientUserId { get; set; }
    public User? RecipientUser { get; set; }

    /// <summary>
    /// A1 decision: stores <see cref="Project.ProjectId"/> in v1.
    /// "Sharing with a team" = sharing with all active ProjectMembers of this project.
    /// Null if this is a direct user share.
    /// </summary>
    public int? RecipientTeamId { get; set; }

    /// <summary>FK to <see cref="User.UserId"/> (int). The user who granted the share.</summary>
    public int GrantedByUserId { get; set; }
    public User GrantedByUser { get; set; } = default!;

    public DateTimeOffset GrantedAtUtc { get; set; }
}
