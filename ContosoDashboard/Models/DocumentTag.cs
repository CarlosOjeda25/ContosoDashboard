namespace ContosoDashboard.Models;

/// <summary>A searchable tag attached to a <see cref="Document"/>.</summary>
public sealed class DocumentTag
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = default!;

    public string Value { get; set; } = string.Empty;
}
