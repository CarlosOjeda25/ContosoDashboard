using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Services.Documents;
using ContosoDashboard.Services.Documents.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ContosoDashboard.Tests.Unit;

/// <summary>T-050: UploadDocumentCommandHandler unit tests.</summary>
public sealed class UploadDocumentCommandHandlerTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static Stream MakePdfStream(int sizeBytes = 64)
    {
        // PDF magic bytes + padding
        var buf = new byte[Math.Max(sizeBytes, 8)];
        buf[0] = 0x25; buf[1] = 0x50; buf[2] = 0x44; buf[3] = 0x46; // %PDF
        buf[4] = 0x2D; buf[5] = 0x31; buf[6] = 0x2E; buf[7] = 0x34; // -1.4
        return new MemoryStream(buf);
    }

    private static ApplicationDbContext MakeDbContext(bool failSave = false)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return failSave ? new FailingSaveDbContext(opts) : new ApplicationDbContext(opts);
    }

    /// <summary>Subclass that throws on SaveChangesAsync to simulate a DB failure.</summary>
    private sealed class FailingSaveDbContext : ApplicationDbContext
    {
        public FailingSaveDbContext(DbContextOptions<ApplicationDbContext> opts) : base(opts) { }
        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
            => throw new InvalidOperationException("Forced DB failure");
    }

    private static (UploadDocumentCommandHandler handler,
                    Mock<IFileStorageService> storageMock,
                    Mock<IAntivirusScanner> avMock,
                    Mock<IDocumentRepository> repoMock,
                    Mock<IDocumentAuditLogRepository> auditMock,
                    ApplicationDbContext db)
        Build(bool avClean = true, bool avThrows = false, bool saveFails = false)
    {
        var storage = new Mock<IFileStorageService>();
        var av = new Mock<IAntivirusScanner>();
        var repo = new Mock<IDocumentRepository>();
        var audit = new Mock<IDocumentAuditLogRepository>();
        var notif = new Mock<INotificationService>();
        var db = MakeDbContext(failSave: saveFails);

        storage.Setup(s => s.GeneratePath(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string>()))
               .Returns("/uploads/test.pdf");
        storage.Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

        if (avThrows)
            av.Setup(a => a.ScanAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("Scanner offline"));
        else
            av.Setup(a => a.ScanAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new ScanResult(avClean, avClean ? null : "EICAR"));

        repo.Setup(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        audit.Setup(a => a.AddAsync(It.IsAny<DocumentAuditLog>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);
        notif.Setup(n => n.NotifyProjectDocumentAddedAsync(
            It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new UploadDocumentCommandHandler(
            storage.Object, av.Object, repo.Object, audit.Object, db,
            notif.Object, NullLogger<UploadDocumentCommandHandler>.Instance);

        return (handler, storage, av, repo, audit, db);
    }

    private static UploadDocumentCommand ValidCommand(Stream? stream = null, long fileSizeBytes = 4096) =>
        new(FileStream: stream ?? MakePdfStream(),
            FileSizeBytes: fileSizeBytes,
            OriginalFileName: "test.pdf",
            Title: "Test Title",
            Description: null,
            Category: DocumentCategory.PersonalFiles,
            ProjectId: null,
            TaskId: null,
            Tags: [],
            ActorUserId: 1);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidPdf_PersistsDocumentAndAuditLog()
    {
        var (handler, storage, _, repo, audit, _) = Build();
        var cmd = ValidCommand();

        var result = await handler.Handle(cmd, default);

        Assert.NotEqual(Guid.Empty, result.DocumentId);
        Assert.Equal("Test Title", result.Title);
        storage.Verify(s => s.UploadAsync(It.IsAny<Stream>(), "/uploads/test.pdf", default), Times.Once);
        repo.Verify(r => r.AddAsync(It.IsAny<Document>(), default), Times.Once);
        audit.Verify(a => a.AddAsync(It.IsAny<DocumentAuditLog>(), default), Times.Once);
    }

    [Fact]
    public async Task Handle_FileTooLarge_ThrowsDocumentUploadException()
    {
        var (handler, _, _, _, _, _) = Build();

        // 26 MB stream — exceeds 25 MB limit
        var bigStream = new MemoryStream(new byte[26 * 1024 * 1024]);
        bigStream.Position = 0;
        var cmd = ValidCommand(bigStream, fileSizeBytes: 26L * 1024 * 1024);

        var ex = await Assert.ThrowsAsync<DocumentUploadException>(
            () => handler.Handle(cmd, default));
        Assert.Equal("FileTooLarge", ex.ErrorCode);
    }

    [Fact]
    public async Task Handle_InvalidMagicBytes_ThrowsDocumentUploadException_NoFileStored()
    {
        var (handler, storage, _, _, _, _) = Build();

        // TXT/plain bytes — not permitted
        var txtStream = new MemoryStream(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F });
        var cmd = ValidCommand(txtStream);

        var ex = await Assert.ThrowsAsync<DocumentUploadException>(
            () => handler.Handle(cmd, default));
        Assert.Equal("InvalidMimeType", ex.ErrorCode);
        storage.Verify(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ScannerFlagsInfected_ThrowsDocumentUploadException()
    {
        var (handler, storage, _, _, _, _) = Build(avClean: false);
        var cmd = ValidCommand();

        var ex = await Assert.ThrowsAsync<DocumentUploadException>(
            () => handler.Handle(cmd, default));
        Assert.Equal("InfectedFile", ex.ErrorCode);
        storage.Verify(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ScannerUnavailable_ThrowsDocumentUploadException()
    {
        var (handler, storage, _, _, _, _) = Build(avThrows: true);
        var cmd = ValidCommand();

        var ex = await Assert.ThrowsAsync<DocumentUploadException>(
            () => handler.Handle(cmd, default));
        Assert.Equal("ScannerUnavailable", ex.ErrorCode);
        storage.Verify(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DbSaveFailsAfterFileStored_DeletesFileAndThrows()
    {
        var (handler, storage, _, repo, _, _) = Build(saveFails: true);

        repo.Setup(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd = ValidCommand();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(cmd, default));

        // Compensation: orphaned file must be deleted
        storage.Verify(s => s.DeleteAsync("/uploads/test.pdf", It.IsAny<CancellationToken>()), Times.Once);
    }
}
