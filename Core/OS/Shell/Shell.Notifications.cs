using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Core.OS;

public static partial class Shell {
    public static class Notifications {
        public static string Show(string title, string text, Texture2D icon = null, Action onClick = null, List<NotificationAction> actions = null) {
            return NotificationManager.Instance.ShowNotification(title, text, icon, onClick, actions);
        }

        public static void Dismiss(string notificationId) {
            NotificationManager.Instance.Dismiss(notificationId);
        }
    }
}
