using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;

namespace TheGame.Core.OS;

/// <summary>
/// A specialized notification that shows installation/update progress.
/// This is now a data-driven class that configures the base Notification.
/// </summary>
public class InstallNotification : Notification {
    public InstallNotification(string title, string initialMessage) {
        Title = title;
        Text = initialMessage;
        ShowProgress = true;
        AutoDismiss = false; // Stay until complete
        Progress = 0;
    }
    
    public void UpdateProgress(float value, string statusText) {
        Update(statusText, value);
        
        if (value >= 1.0f) {
            // Auto-dismiss after 3 seconds on completion
            AutoDismiss = true;
            DismissTime = 3.0f;
        }
    }
}
