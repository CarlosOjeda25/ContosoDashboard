using ContosoDashboard.Services;

namespace ContosoDashboard.Tests.Unit;

/// <summary>T-049: MagicBytesValidator unit tests (8 bytes header inspection).</summary>
public sealed class MagicBytesValidatorTests
{
    // ── Valid signatures ──────────────────────────────────────────────────────

    [Fact]
    public void IsPermitted_ValidPdf_ReturnsTrue()
    {
        var header = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // %PDF-1.4
        var result = MagicBytesValidator.IsPermitted(header, out var mime);
        Assert.True(result);
        Assert.Equal("application/pdf", mime);
    }

    [Fact]
    public void IsPermitted_ValidDocx_ReturnsTrue()
    {
        // DOCX = OpenXml/ZIP: PK\x03\x04 header
        var header = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00 };
        var result = MagicBytesValidator.IsPermitted(header, out var mime);
        Assert.True(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument", mime);
    }

    [Fact]
    public void IsPermitted_ValidXlsx_ReturnsTrue()
    {
        var header = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00 };
        // xlsx resolved from extension
        var result = MagicBytesValidator.IsPermitted(header, out _);
        Assert.True(result);
    }

    [Fact]
    public void IsPermitted_ValidPptx_ReturnsTrue()
    {
        var header = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00 };
        var result = MagicBytesValidator.IsPermitted(header, out _);
        Assert.True(result);
    }

    [Fact]
    public void IsPermitted_ValidJpeg_ReturnsTrue()
    {
        var header = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
        var result = MagicBytesValidator.IsPermitted(header, out var mime);
        Assert.True(result);
        Assert.Equal("image/jpeg", mime);
    }

    [Fact]
    public void IsPermitted_ValidPng_ReturnsTrue()
    {
        var header = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var result = MagicBytesValidator.IsPermitted(header, out var mime);
        Assert.True(result);
        Assert.Equal("image/png", mime);
    }

    // ── Invalid / rejected signatures ─────────────────────────────────────────

    [Fact]
    public void IsPermitted_ExecutableBytes_ReturnsFalse()
    {
        // PE/MZ header: 0x4D 0x5A = MZ
        var header = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };
        var result = MagicBytesValidator.IsPermitted(header, out var mime);
        Assert.False(result);
        Assert.Equal(string.Empty, mime);
    }

    [Fact]
    public void IsPermitted_EmptyBytes_ReturnsFalse()
    {
        var result = MagicBytesValidator.IsPermitted(ReadOnlySpan<byte>.Empty, out var mime);
        Assert.False(result);
        Assert.Equal(string.Empty, mime);
    }

    [Fact]
    public void IsPermitted_TruncatedHeader_ReturnsFalse()
    {
        // Only 4 bytes — less than the required 8
        var header = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var result = MagicBytesValidator.IsPermitted(header, out var mime);
        Assert.False(result);
        Assert.Equal(string.Empty, mime);
    }
}
