using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Services.Documents;
using ContosoDashboard.Services.Documents.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ContosoDashboard.Tests.Unit;

/// <summary>T-056: ReplaceDocumentCommandHandler unit tests.</summary>
public sealed class ReplaceDocumentCommandHandlerTests
{
    private static Stream MakePdfStream() =>
        new MemoryStream(new byte[]
        {
            0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, // %PDF-1.4
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        });

    private static ApplicationDbContext MakeDbContext(bool failSave = false)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return failSave ? new FailingSaveDbContext(opts) : new ApplicationDbContext(opts);
    }

    private sealed class FailingSaveDbContext : ApplicationDbContext
    {
        public FailingSaveDbContext(DbContextOptions<ApplicationDbContext> opts) : base(opts) { }
        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
            => throw new InvalidOperationException("Forced DB failure");
    }

    private static Document MakeDoc(int ownerId, string oldPath = "/uploads/old.pdf") => new()
    {
        Id = Guid.NewGuid(),
        Title = "Original",
        OriginalFileName = "original.pdf",
        StoredPath = oldPath,
        MimeType = "application/pdf",
        FileSizeBytes = 512,
        UploadedAtUtc = DateTimeOffset.UtcNow,
        UploadedByUserId = ownerId,
        Category = DocumentCategory.Other
    };

    private static (ReplaceDocumentCommandHandler handler, Mock<IFileStorageService> storage)
        Build(Document doc, bool saveFails = false)
    {
        var repo = new Mock<IDocumentRepository>();
        var audit = new Mock<IDocumentAuditLogRepository>();
        var storage = new Mock<IFileStorageService>();
        var av = new Mock<IAntivirusScanner>();
        var db = MakeDbContext(failSave: saveFails);

        repo.Setup(r => r.GetByIdAsync(doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        repo.Setup(r => r.UpdateAsync(doc, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        audit.Setup(a => a.AddAsync(It.IsAny<DocumentAuditLog>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);
        av.Setup(a => a.ScanAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new ScanResult(true, null));

        const string newPath = "/uploads/new.pdf";
        storage.Setup(s => s.GeneratePath(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string>()))
               .Returns(newPath);
        storage.Setup(s => s.UploadAsync(It.IsAny<Stream>(), newPath, It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

        var handler = new ReplaceDocumentCommandHandler(
            repo.Object, audit.Object, storage.Object, av.Object, db,
            NullLogger<ReplaceDocumentCommandHandler>.Instance);

        return (handler, storage);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Owner_ReplacesFileAndUpdatesMetadata()
    {
        var doc = MakeDoc(ownerId: 1);
        var (handler, storage) = Build(doc);

        var result = await handler.Handle(
            new ReplaceDocumentCommand(doc.Id, MakePdfStream(), "new.pdf", ActorUserId: 1), default);

        Assert.NotEqual(Guid.Empty, result.DocumentId);
        // New file was uploaded
        storage.Verify(s => s.UploadAsync(It.IsAny<Stream>(), "/uploads/new.pdf", default), Times.Once);
    }

    [Fact]
    public async Task Handle_DbSaveFailsAfterNewFileSaved_DeletesNewFileAndLeavesOldIntact()
    {
        var doc = MakeDoc(ownerId: 1, oldPath: "/uploads/old.pdf");
        var (handler, storage) = Build(doc, saveFails: true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(
                new ReplaceDocumentCommand(doc.Id, MakePdfStream(), "new.pdf", ActorUserId: 1), default));

        // Compensation: new file deleted, old file NOT deleted
        storage.Verify(s => s.DeleteAsync("/uploads/new.pdf", It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(s => s.DeleteAsync("/uploads/old.pdf", It.IsAny<CancellationToken>()), Times.Never);
    }
}
