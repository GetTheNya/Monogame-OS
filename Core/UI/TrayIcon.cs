using System;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.OS;

namespace TheGame.Core.UI;

/// <summary>
/// Represents an icon in the system tray with callbacks for mouse events.
/// Automatically associated with the creating window/process for auto-cleanup.
/// </summary>
public class TrayIcon {
    /// <summary>Unique identifier for this tray icon (auto-generated).</summary>
    public string Id { get; }
    
    /// <summary>The window that owns this tray icon (if created from a window).</summary>
    public Window OwnerWindow { get; internal set; }
    
    /// <summary>The process that owns this tray icon.</summary>
    public Process OwnerProcess { get; internal set; }
    
    /// <summary>
    /// If true, the icon persists after the owner window closes and is only removed when the process terminates.
    /// Used for "minimize to tray" functionality. Default is false.
    /// </summary>
    public bool PersistAfterWindowClose { get; set; }
    
    /// <summary>The icon texture displayed in the tray.</summary>
    public Texture2D Icon { get; private set; }
    
    /// <summary>Tooltip text shown on hover.</summary>
    public string Tooltip { get; set; }
    
    /// <summary>Called on left mouse button click.</summary>
    public Action OnClick { get; set; }
    
    /// <summary>Called on left mouse button double-click.</summary>
    public Action OnDoubleClick { get; set; }
    
    /// <summary>Called on right mouse button click.</summary>
    public Action OnRightClick { get; set; }
    
    /// <summary>Called on right mouse button double-click.</summary>
    public Action OnRightDoubleClick { get; set; }
    
    /// <summary>Called on mouse wheel scroll. Parameter is the scroll delta (+1 or -1).</summary>
    public Action<int> OnMouseWheel { get; set; }

    /// <summary>
    /// Creates a new tray icon. ID is auto-generated.
    /// </summary>
    public TrayIcon(Texture2D icon = null, string tooltip = null) {
        Id = Guid.NewGuid().ToString();
        Icon = icon;
        Tooltip = tooltip;
        PersistAfterWindowClose = false;
    }

    /// <summary>
    /// Updates the icon texture.
    /// </summary>
    public void SetIcon(Texture2D newIcon) {
        Icon = newIcon;
    }

    /// <summary>
    /// Updates the tooltip text.
    /// </summary>
    public void SetTooltip(string newTooltip) {
        Tooltip = newTooltip;
    }
}
