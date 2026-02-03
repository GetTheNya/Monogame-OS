using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Core.OS;

public class NotificationsAPI : BaseAPI {
    public NotificationsAPI(Process process) : base(process) {
    }

    public string Show(string title, string text, Texture2D icon = null, Action onClick = null, List<NotificationAction> actions = null) {
        return Shell.Notifications.Show(title, text, icon, onClick, actions);
    }

    public void Dismiss(string notificationId) {
        Shell.Notifications.Dismiss(notificationId);
    }
}
