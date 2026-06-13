using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

public interface INotificationService
{
    Task<List<Notification>> GetUserNotificationsAsync(int userId, bool unreadOnly = false);
    Task<Notification> CreateNotificationAsync(Notification notification);
    Task<bool> MarkAsReadAsync(int notificationId, int requestingUserId);
    Task<int> GetUnreadCountAsync(int userId);

    // ── Document notifications (G3, FR-024) ─────────────────────────────
    /// <summary>Best-effort: notifies all active project members that a document was added.</summary>
    Task NotifyProjectDocumentAddedAsync(int projectId, Guid documentId, int actorUserId, CancellationToken ct);

    /// <summary>Best-effort: creates one Notification per recipient user id.</summary>
    Task NotifyShareAsync(Guid documentId, IReadOnlyList<int> recipientUserIds, CancellationToken ct);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;

    public NotificationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Notification>> GetUserNotificationsAsync(int userId, bool unreadOnly = false)
    {
        var query = _context.Notifications
            .Where(n => n.UserId == userId);

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.Priority)
            .ThenByDescending(n => n.CreatedDate)
            .Take(50)
            .ToListAsync();
    }

    public async Task<Notification> CreateNotificationAsync(Notification notification)
    {
        notification.CreatedDate = DateTime.UtcNow;
        notification.IsRead = false;

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        return notification;
    }

    public async Task<bool> MarkAsReadAsync(int notificationId, int requestingUserId)
    {
        var notification = await _context.Notifications.FindAsync(notificationId);
        if (notification == null) return false;

        // Authorization: Users can only mark their own notifications as read
        if (notification.UserId != requestingUserId)
        {
            return false; // User not authorized to mark this notification as read
        }

        notification.IsRead = true;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    // ── Document notification helpers ────────────────────────────────────

    public async Task NotifyProjectDocumentAddedAsync(
        int projectId, Guid documentId, int actorUserId, CancellationToken ct)
    {
        var memberUserIds = await _context.ProjectMembers
            .Where(pm => pm.ProjectId == projectId)
            .Select(pm => pm.UserId)
            .ToListAsync(ct);

        foreach (var userId in memberUserIds.Where(uid => uid != actorUserId))
        {
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = "New document added",
                Message = $"A new document was added to your project.",
                Type = NotificationType.ProjectUpdate,
                Priority = NotificationPriority.Informational,
                CreatedDate = DateTime.UtcNow,
                IsRead = false
            });
        }

        if (memberUserIds.Any())
            await _context.SaveChangesAsync(ct);
    }

    public async Task NotifyShareAsync(
        Guid documentId, IReadOnlyList<int> recipientUserIds, CancellationToken ct)
    {
        foreach (var userId in recipientUserIds)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = "Document shared with you",
                Message = "A document has been shared with you.",
                Type = NotificationType.SystemAnnouncement,
                Priority = NotificationPriority.Informational,
                CreatedDate = DateTime.UtcNow,
                IsRead = false
            });
        }

        if (recipientUserIds.Count > 0)
            await _context.SaveChangesAsync(ct);
    }
}
