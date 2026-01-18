using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Graphics;
using TheGame.Core.OS;
using TheGame.Core.Input;
using TheGame.Core.UI.Controls;
using FontStashSharp;

namespace TheGame.Core.UI;

public class SystemTray : Panel {
    private string _currentTime = "";
    private float _updateTimer = 0f;
    public float DesiredWidth { get; private set; } = 150f;

    // Store gameTime for use in HandleTrayIconInput
    private GameTime _gameTime;

    private Button _notificationButton;
    
    // Tray Icons
    private List<TrayIcon> _trayIcons = new();
    private const float IconSize = 20f;
    private const float IconSpacing = 4f;
    
    // Delayed click tracking to prevent OnClick during double-click
    private string _pendingClickIconId = null;
    private float _pendingClickTimer = 0f;
    private string _pendingRightClickIconId = null;
    private float _pendingRightClickTimer = 0f;
    private const float ClickDelayThreshold = 0.3f;

    public SystemTray(Vector2 position, Vector2 size) : base(position, size) {
        BackgroundColor = Color.Transparent;
        BorderColor = Color.Transparent;
        
        // Create notification button
        _notificationButton = new Button(new Vector2(size.X - 35, 4), new Vector2(30, size.Y - 8)) {
            BackgroundColor = Color.Transparent,
            HoverColor = new Color(60, 60, 60),
            Icon = GameContent.NotificationIcon
        };
        AddChild(_notificationButton);
        
        UpdateTime();
    }

    public Action OnNotificationClick {
        get => _notificationButton.OnClickAction;
        set => _notificationButton.OnClickAction = value;
    }

    /// <summary>
    /// Adds a tray icon to the system tray.
    /// </summary>
    public void AddIcon(TrayIcon icon) {
        if (icon == null || _trayIcons.Exists(i => i.Id == icon.Id)) return;
        _trayIcons.Add(icon);
        DebugLogger.Log($"[SystemTray] Added icon '{icon.Tooltip}' (ID: {icon.Id}, Window: {icon.OwnerWindow?.Title ?? "NULL"}, Process: {icon.OwnerProcess?.AppId ?? "NULL"}, Persist: {icon.PersistAfterWindowClose})");
        RecalculateWidth();
    }

    /// <summary>
    /// Removes a tray icon by ID.
    /// </summary>
    public bool RemoveIcon(string id) {
        int removed = _trayIcons.RemoveAll(i => i.Id == id);
        if (removed > 0) {
            RecalculateWidth();
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Removes all tray icons owned by the specified process.
    /// Called automatically when a process terminates.
    /// Only removes process-level icons; window-owned icons are handled by RemoveIconsForWindow.
    /// </summary>
    public void RemoveIconsForProcess(OS.Process process) {
        if (process == null) {
            DebugLogger.Log("[SystemTray] RemoveIconsForProcess called with null process");
            return;
        }
        
        DebugLogger.Log($"[SystemTray] RemoveIconsForProcess called for process {process.AppId} (ID: {process.ProcessId})");
        DebugLogger.Log($"[SystemTray] Current tray icons: {_trayIcons.Count}");
        
        foreach (var icon in _trayIcons) {
            DebugLogger.Log($"  - Icon '{icon.Tooltip}' Owner: {icon.OwnerProcess?.AppId ?? "NULL"}, HasWindow: {icon.OwnerWindow != null} (Match: {icon.OwnerProcess == process})");
        }
        
        // Only remove icons that are process-level (no OwnerWindow)
        // Window-owned icons are handled by RemoveIconsForWindow
        int removed = _trayIcons.RemoveAll(i => i.OwnerProcess == process && i.OwnerWindow == null);
        if (removed > 0) {
            DebugLogger.Log($"[SystemTray] Removed {removed} process-level tray icon(s) for process {process.AppId}");
            RecalculateWidth();
        } else {
            DebugLogger.Log($"[SystemTray] No process-level tray icons found for process {process.AppId}");
        }
    }
    
    /// <summary>
    /// Removes all tray icons owned by the specified window (unless PersistAfterWindowClose is true).
    /// Called automatically when a window closes.
    /// </summary>
    public void RemoveIconsForWindow(Window window) {
        if (window == null) return;
        
        int removed = _trayIcons.RemoveAll(i => i.OwnerWindow == window && !i.PersistAfterWindowClose);
        if (removed > 0) {
            DebugLogger.Log($"[SystemTray] Removed {removed} tray icon(s) for window '{window.Title}' (non-persistent)");
            RecalculateWidth();
        }
    }
    


    /// <summary>
    /// Gets a tray icon by ID for dynamic updates.
    /// </summary>
    public TrayIcon GetIcon(string id) {
        return _trayIcons.Find(i => i.Id == id);
    }

    /// <summary>
    /// Gets all registered tray icons.
    /// </summary>
    public IReadOnlyList<TrayIcon> Icons => _trayIcons;

    public override void Update(GameTime gameTime) {
        _gameTime = gameTime; // Store for HandleTrayIconInput
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        _updateTimer += deltaTime;
        if (_updateTimer >= 1f) {
            UpdateTime();
            _updateTimer = 0f;
        }
        
        // Update button position based on current size
        _notificationButton.Position = new Vector2(Size.X - 35, 4);
        _notificationButton.Size = new Vector2(30, Size.Y - 8);
        
        // Handle pending delayed click
        if (_pendingClickIconId != null) {
            _pendingClickTimer += deltaTime;
            if (_pendingClickTimer >= ClickDelayThreshold) {
                // Delay exceeded, fire the click
                var icon = _trayIcons.Find(i => i.Id == _pendingClickIconId);
                icon?.OnClick?.Invoke();
                _pendingClickIconId = null;
            }
        }
        
        // Handle pending delayed right-click
        if (_pendingRightClickIconId != null) {
            _pendingRightClickTimer += deltaTime;
            if (_pendingRightClickTimer >= ClickDelayThreshold) {
                // Delay exceeded, fire the right-click
                var icon = _trayIcons.Find(i => i.Id == _pendingRightClickIconId);
                icon?.OnRightClick?.Invoke();
                _pendingRightClickIconId = null;
            }
        }
        
        // Handle tray icon mouse events
        HandleTrayIconInput();
        
        base.Update(gameTime);
    }

    private void HandleTrayIconInput() {
        if (InputManager.IsMouseConsumed || _gameTime == null) return;
        
        var mousePos = InputManager.MousePosition;
        float iconStartX = AbsolutePosition.X + 8f; // Start of tray icons
        
        for (int i = 0; i < _trayIcons.Count; i++) {
            var icon = _trayIcons[i];
            float x = iconStartX + (i * (IconSize + IconSpacing));
            Rectangle iconBounds = new Rectangle((int)x, (int)(AbsolutePosition.Y + (Size.Y - IconSize) / 2f), (int)IconSize, (int)IconSize);
            
            if (!iconBounds.Contains(mousePos)) continue;
            
            // Mouse wheel
            int scrollDelta = InputManager.ScrollDelta;
            if (scrollDelta != 0) {
                icon.OnMouseWheel?.Invoke(scrollDelta > 0 ? 1 : -1);
                InputManager.IsMouseConsumed = true;
            }
            
            // Left click / double-click
            if (InputManager.IsDoubleClick(MouseButton.Left)) {
                // Cancel pending click and fire double-click instead
                _pendingClickIconId = null;
                icon.OnDoubleClick?.Invoke();
                InputManager.IsMouseConsumed = true;
            } else if (InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
                // Delay click to see if double-click follows
                _pendingClickIconId = icon.Id;
                _pendingClickTimer = 0f;
                InputManager.IsMouseConsumed = true;
            }
            
            // Right click / double-click (same delayed pattern as left-click)
            if (InputManager.IsDoubleClick(MouseButton.Right)) {
                // Cancel pending right-click and fire double-click instead
                _pendingRightClickIconId = null;
                icon.OnRightDoubleClick?.Invoke();
                InputManager.IsMouseConsumed = true;
            } else if (InputManager.IsMouseButtonJustPressed(MouseButton.Right)) {
                // Delay right-click to see if double-click follows
                _pendingRightClickIconId = icon.Id;
                _pendingRightClickTimer = 0f;
                InputManager.IsMouseConsumed = true;
            }
        }
    }


    private void UpdateTime() {
        _currentTime = DateTime.Now.ToString("HH:mm");
        RecalculateWidth();
    }

    private void RecalculateWidth() {
        if (GameContent.FontSystem != null) {
            var font = GameContent.FontSystem.GetFont(16);
            float timeWidth = font.MeasureString(_currentTime).X;
            float trayIconsWidth = _trayIcons.Count * (IconSize + IconSpacing);
            float notifBtnWidth = 40f;
            DesiredWidth = trayIconsWidth + timeWidth + notifBtnWidth + 30f;
        }
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        base.DrawSelf(spriteBatch, batch);
        
        var absPos = AbsolutePosition;
        float x = absPos.X + 8f;
        
        // Draw Tray Icons
        float trayIconY = absPos.Y + (Size.Y - IconSize) / 2f;
        foreach (var icon in _trayIcons) {
            if (icon.Icon != null) {
                spriteBatch.Draw(icon.Icon, new Rectangle((int)x, (int)trayIconY, (int)IconSize, (int)IconSize), Color.White);
            }
            x += IconSize + IconSpacing;
        }
        
        x += 5f;

        // Draw Clock
        if (GameContent.FontSystem != null) {
            var font = GameContent.FontSystem.GetFont(16);
            var timeSize = font.MeasureString(_currentTime);
            Vector2 timePos = new Vector2(x, absPos.Y + (Size.Y - timeSize.Y) / 2f);
            font.DrawText(batch, _currentTime, timePos, Color.White);
        }

        // Draw unread badge on notification button
        int unread = NotificationManager.Instance.UnreadCount;
        if (unread > 0 && GameContent.FontSystem != null) {
            var btnPos = _notificationButton.AbsolutePosition;
            var badgeFont = GameContent.FontSystem.GetFont(9);
            string badgeText = unread > 9 ? "9+" : unread.ToString();
            Vector2 badgePos = btnPos + new Vector2(_notificationButton.Size.X - 8, 0);
            batch.FillCircle(badgePos + new Vector2(5, 5), 8f, Color.Red);
            badgeFont.DrawText(batch, badgeText, badgePos + new Vector2(unread > 9 ? 1 : 3, 1), Color.White);
        }
    }
}
