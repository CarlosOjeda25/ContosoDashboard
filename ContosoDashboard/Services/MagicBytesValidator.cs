namespace ContosoDashboard.Services;

/// <summary>
/// Validates uploaded files by inspecting their magic bytes (file signature),
/// NOT the Content-Type header or file extension — constitution §IV.
/// Permitted types: PDF, DOCX, XLSX, PPTX, JPEG, PNG (TXT excluded per constitution §IV).
/// </summary>
public static class MagicBytesValidator
{
    // Minimum header bytes needed for any of our checks.
    private const int MinHeaderLength = 8;

    // ── Magic-byte signatures ──────────────────────────────────────────────────
    // PDF:  %PDF
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46];

    // DOCX / XLSX / PPTX: all Open XML (ZIP) containers start with PK\x03\x04
    private static readonly byte[] OpenXml = [0x50, 0x4B, 0x03, 0x04];

    // JPEG: FF D8 FF
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];

    // PNG: \x89PNG\r\n\x1A\n
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Inspects the first bytes of <paramref name="header"/> and determines whether
    /// the file is of a permitted type.
    /// </summary>
    /// <param name="header">
    ///   At least the first <c>8</c> bytes of the file. Fewer bytes always returns <c>false</c>.
    /// </param>
    /// <param name="mimeType">
    ///   When the method returns <c>true</c>, contains the inferred MIME type string.
    ///   When the method returns <c>false</c>, this is <see cref="string.Empty"/>.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the file is a permitted type; <c>false</c> for any other input
    ///   including empty, truncated, or unknown signatures. Never throws.
    /// </returns>
    public static bool IsPermitted(ReadOnlySpan<byte> header, out string mimeType)
    {
        mimeType = string.Empty;

        if (header.Length < MinHeaderLength)
            return false;

        // PDF
        if (StartsWith(header, Pdf))
        {
            mimeType = "application/pdf";
            return true;
        }

        // JPEG
        if (StartsWith(header, Jpeg))
        {
            mimeType = "image/jpeg";
            return true;
        }

        // PNG
        if (StartsWith(header, Png))
        {
            mimeType = "image/png";
            return true;
        }

        // Open XML family (DOCX / XLSX / PPTX) — all share the ZIP PK header.
        // Distinguishing between them requires inspecting the ZIP content directory,
        // which is done here by extension convention after the magic-byte gate passes.
        // The security gate is the PK signature; the MIME label is informational.
        if (StartsWith(header, OpenXml))
        {
            // Caller sets the precise MIME from the extension label after validation;
            // we return a generic Open XML MIME as the validated base type.
            mimeType = "application/vnd.openxmlformats-officedocument";
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the precise MIME type for Open XML files based on the original
    /// file extension (used after <see cref="IsPermitted"/> confirms the PK magic bytes).
    /// Returns <c>null</c> if the extension is not in the permitted Open XML set.
    /// </summary>
    public static string? ResolveOpenXmlMime(string originalFileName)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        return ext switch
        {
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            _ => null
        };
    }

    private static bool StartsWith(ReadOnlySpan<byte> source, byte[] pattern)
    {
        if (source.Length < pattern.Length) return false;
        for (var i = 0; i < pattern.Length; i++)
            if (source[i] != pattern[i]) return false;
        return true;
    }
}
