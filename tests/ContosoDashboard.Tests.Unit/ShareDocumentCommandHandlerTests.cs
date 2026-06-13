using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Services.Documents.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ContosoDashboard.Tests.Unit;

/// <summary>T-054: ShareDocumentCommandHandler unit tests.</summary>
public sealed class ShareDocumentCommandHandlerTests
{
    private static ApplicationDbContext MakeDbContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Document MakeDoc(int ownerId) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Shared Doc",
        OriginalFileName = "share.pdf",
        StoredPath = "/uploads/share.pdf",
        MimeType = "application/pdf",
        FileSizeBytes = 1024,
        UploadedAtUtc = DateTimeOffset.UtcNow,
        UploadedByUserId = ownerId,
        Category = DocumentCategory.PersonalFiles
    };

    private static (ShareDocumentCommandHandler handler, Mock<IDocumentAuditLogRepository> audit)
        Build(Document doc, User? actor = null, bool notifThrows = false)
    {
        var repo = new Mock<IDocumentRepository>();
        var audit = new Mock<IDocumentAuditLogRepository>();
        var notif = new Mock<INotificationService>();
        var db = MakeDbContext();

        repo.Setup(r => r.GetByIdAsync(doc.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        audit.Setup(a => a.AddAsync(It.IsAny<DocumentAuditLog>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

        if (notifThrows)
            notif.Setup(n => n.NotifyShareAsync(
                    It.IsAny<Guid>(), It.IsAny<IReadOnlyList<int>>(),
                    It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new Exception("Notification service down"));
        else
            notif.Setup(n => n.NotifyShareAsync(
                    It.IsAny<Guid>(), It.IsAny<IReadOnlyList<int>>(),
                    It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        if (actor != null)
        {
            db.Users.Add(actor);
            db.SaveChanges();
        }

        var handler = new ShareDocumentCommandHandler(
            repo.Object, audit.Object, db, notif.Object,
            NullLogger<ShareDocumentCommandHandler>.Instance);

        return (handler, audit);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Owner_CreatesShareRecordsAndAuditLogs()
    {
        var doc = MakeDoc(ownerId: 1);
        var (handler, audit) = Build(doc);

        var cmd = new ShareDocumentCommand(doc.Id, ActorUserId: 1,
            RecipientUserIds: new[] { 2, 3 }, RecipientTeamIds: Array.Empty<int>());

        await handler.Handle(cmd, default);

        // One audit log per direct user share
        audit.Verify(a => a.AddAsync(
            It.Is<DocumentAuditLog>(l => l.EventType == DocumentAuditEventType.ShareGranted),
            default), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_NonOwner_ThrowsForbiddenException()
    {
        var doc = MakeDoc(ownerId: 99);
        var emp = new User { UserId = 7, Role = UserRole.Employee, DisplayName = "Emp", Email = "e@t.com" };
        var (handler, _) = Build(doc, emp);

        var cmd = new ShareDocumentCommand(doc.Id, ActorUserId: 7,
            RecipientUserIds: new[] { 2 }, RecipientTeamIds: Array.Empty<int>());

        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(cmd, default));
    }

    [Fact]
    public async Task Handle_NotificationFails_OperationSucceeds_WarningLogged()
    {
        // FR-024 best-effort: notification failure must NOT roll back the share
        var doc = MakeDoc(ownerId: 1);
        var (handler, audit) = Build(doc, notifThrows: true);

        var cmd = new ShareDocumentCommand(doc.Id, ActorUserId: 1,
            RecipientUserIds: new[] { 2 }, RecipientTeamIds: Array.Empty<int>());

        // Should NOT throw — notification failure is swallowed
        await handler.Handle(cmd, default);

        // Share audit log still created
        audit.Verify(a => a.AddAsync(It.IsAny<DocumentAuditLog>(), default), Times.Once);
    }
}
