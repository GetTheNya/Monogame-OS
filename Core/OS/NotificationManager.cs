using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Core.OS;

/// <summary>
/// Represents an action button within a notification.
/// </summary>
public class NotificationAction {
    public string Label { get; set; }
    public Action OnClick { get; set; }
}

/// <summary>
/// Represents a single notification with icon, title, text, and optional actions.
/// </summary>
public class Notification {
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; }
    public string Text { get; set; }
    public Texture2D Icon { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public Action OnClick { get; set; }
    public List<NotificationAction> Actions { get; set; } = new();
    public bool IsRead { get; set; } = false;
}

/// <summary>
/// Singleton manager for system-wide notifications.
/// </summary>
public class NotificationManager {
    private static NotificationManager _instance;
    public static NotificationManager Instance => _instance ??= new NotificationManager();

    private List<Notification> _history = new();
    public IReadOnlyList<Notification> History => _history;
    
    // Cached unread count to avoid LINQ every access
    private int _unreadCount = 0;
    
    public event Action<Notification> OnNotificationAdded;
    public event Action<string> OnNotificationDismissed;
    public event Action OnHistoryCleared;

    public int UnreadCount => _unreadCount;

    private NotificationManager() { }
    
    private void UpdateUnreadCount() {
        int count = 0;
        for (int i = 0; i < _history.Count; i++) {
            if (!_history[i].IsRead) count++;
        }
        _unreadCount = count;
    }

    /// <summary>
    /// Shows a new notification toast and adds it to history. Returns the notification ID.
    /// </summary>
    public string ShowNotification(string title, string text, Texture2D icon = null, 
                                  Action onClick = null, List<NotificationAction> actions = null) {
        var notification = new Notification {
            Title = title,
            Text = text,
            Icon = icon,
            OnClick = onClick,
            Actions = actions ?? new List<NotificationAction>()
        };

        _history.Insert(0, notification); // Newest first
        _unreadCount++;
        OnNotificationAdded?.Invoke(notification);
        DebugLogger.Log($"Notification: {title}");
        return notification.Id;
    }

    /// <summary>
    /// Marks a notification as read (does NOT dismiss the toast).
    /// </summary>
    public void MarkAsRead(string notificationId) {
        var notif = _history.Find(n => n.Id == notificationId);
        if (notif != null && !notif.IsRead) {
            notif.IsRead = true;
            _unreadCount = Math.Max(0, _unreadCount - 1);
        }
    }

    /// <summary>
    /// Marks all notifications as read.
    /// </summary>
    public void MarkAllAsRead() {
        foreach (var n in _history) n.IsRead = true;
        _unreadCount = 0;
    }

    /// <summary>
    /// Removes a specific notification from history and dismisses its toast.
    /// </summary>
    public void Dismiss(string notificationId) {
        // Check if we're removing an unread notification
        for (int i = 0; i < _history.Count; i++) {
            if (_history[i].Id == notificationId) {
                if (!_history[i].IsRead) _unreadCount = Math.Max(0, _unreadCount - 1);
                _history.RemoveAt(i);
                break;
            }
        }
        OnNotificationDismissed?.Invoke(notificationId);
    }

    /// <summary>
    /// Clears all notification history.
    /// </summary>
    public void ClearHistory() {
        _history.Clear();
        _unreadCount = 0;
        OnHistoryCleared?.Invoke();
    }
}
