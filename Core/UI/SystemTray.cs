using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Graphics;
using TheGame.Core.OS;
using TheGame.Core.Input;
using TheGame.Core.UI.Controls;
using FontStashSharp;

namespace TheGame.Core.UI;

public class SystemTray : Panel, ITooltipSubElementProvider {
    private string _currentTime = "";
    private float _updateTimer = 0f;
    public float DesiredWidth { get; private set; } = 150f;

    // Store gameTime for use in HandleTrayIconInput
    private GameTime _gameTime;

    private Button _notificationButton;
    private VolumeMixerPanel _volumeMixerPanel;
    private TrayIcon _volumeIcon;
    private TrayIcon _powerIcon;
    
    // Tray Icons
    private List<TrayIcon> _appIcons = new();
    private List<TrayIcon> _systemIcons = new();
    private const float IconSize = 20f;
    private const float IconSpacing = 4f;
    
    // Delayed click tracking to prevent OnClick during double-click
    private string _pendingClickIconId = null;
    private float _pendingClickTimer = 0f;
    private string _pendingRightClickIconId = null;
    private float _pendingRightClickTimer = 0f;
    private const float ClickDelayThreshold = 0.3f;

    public SystemTray(Vector2 position, Vector2 size, VolumeMixerPanel volumeMixer) : base(position, size) {
        BackgroundColor = Color.Transparent;
        BorderColor = Color.Transparent;
        _volumeMixerPanel = volumeMixer;
        
        // Create notification button
        _notificationButton = new Button(new Vector2(size.X - 35, 4), new Vector2(30, size.Y - 8)) {
            BackgroundColor = Color.Transparent,
            HoverColor = new Color(60, 60, 60),
            Icon = GameContent.NotificationIcon
        };
        AddChild(_notificationButton);
        
        // Setup System Icons
        InitializeSystemIcons();
        
        UpdateTime();
    }

    private void InitializeSystemIcons() {
        // 1. Volume Icon
        _volumeIcon = new TrayIcon(GameContent.VolumeIcons[4], "Volume") {
            OnClick = () => ToggleVolumeMixer(),
            OnMouseWheel = (delta) => {
                float current = Shell.Media.GetMasterVolume();
                Shell.Media.SetMasterVolume(MathHelper.Clamp(current + delta * 0.05f, 0f, 1f));
                UpdateVolumeIcon();
            }
        };
        _systemIcons.Add(_volumeIcon);

        // 2. Power Icon
        _powerIcon = new TrayIcon(GameContent.PowerIcon, "Power") {
            OnClick = () => {
                Shell.ContextMenu.Show(InputManager.MousePosition.ToVector2(), new List<MenuItem> {
                    new MenuItem { Text = "Restart", Action = () => {/*TODO*/} },
                    new MenuItem { Text = "Shut down", Action = () => {/*TODO*/} },
                    new MenuItem { Text = "Sign out", Action = () => {/*TODO*/} }
                });
            }
        };
        _systemIcons.Add(_powerIcon);
    }

    private void ToggleVolumeMixer() {
        if (_volumeMixerPanel == null || _volumeIcon == null) return;

        if (_volumeMixerPanel.IsVisible) {
            _volumeMixerPanel.Close();
        } else {
            // Calculate position specifically for the volume icon
            float iconXRel = GetIconXRel(_volumeIcon);
            
            float absX = AbsolutePosition.X + iconXRel;
            float absY = AbsolutePosition.Y;
            
            _volumeMixerPanel.Position = new Vector2(
                absX + IconSize / 2f - _volumeMixerPanel.Size.X / 2f,
                absY - _volumeMixerPanel.Size.Y - 5
            );
            
            // Keep on screen (absolute check)
            var viewport = G.GraphicsDevice.Viewport;
            if (_volumeMixerPanel.Position.X + _volumeMixerPanel.Size.X > viewport.Width)
                _volumeMixerPanel.Position = new Vector2(viewport.Width - _volumeMixerPanel.Size.X - 10, _volumeMixerPanel.Position.Y);
            if (_volumeMixerPanel.Position.X < 0)
                _volumeMixerPanel.Position = new Vector2(10, _volumeMixerPanel.Position.Y);

            _volumeMixerPanel.Open();
        }
    }

    private void UpdateVolumeIcon() {
        if (_volumeIcon == null || GameContent.VolumeIcons == null) return;
        
        float vol = Shell.Media.GetMasterVolume();
        Texture2D icon;
        if (vol <= 0) icon = GameContent.VolumeIcons[0]; // mute
        else if (vol < 0.33f) icon = GameContent.VolumeIcons[2]; // volume0
        else if (vol < 0.66f) icon = GameContent.VolumeIcons[3]; // volume1
        else icon = GameContent.VolumeIcons[4]; // volume3
        
        _volumeIcon.SetIcon(icon);
        _volumeIcon.Tooltip = $"Volume: {(int)(vol * 100)}%";
    }

    public Action OnNotificationClick {
        get => _notificationButton.OnClickAction;
        set => _notificationButton.OnClickAction = value;
    }

    /// <summary>
    /// Adds a tray icon to the system tray. Use this for application icons.
    /// </summary>
    public void AddIcon(TrayIcon icon) {
        if (icon == null || _appIcons.Exists(i => i.Id == icon.Id) || _systemIcons.Exists(i => i.Id == icon.Id)) return;
        _appIcons.Add(icon);
        DebugLogger.Log($"[SystemTray] Added app icon '{icon.Tooltip}' (ID: {icon.Id})");
        RecalculateWidth();
    }

    /// <summary>
    /// Removes a tray icon by ID.
    /// </summary>
    public bool RemoveIcon(string id) {
        bool removed = _appIcons.RemoveAll(i => i.Id == id) > 0;
        removed |= _systemIcons.RemoveAll(i => i.Id == id) > 0;
        if (removed) {
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
        DebugLogger.Log($"[SystemTray] Current app tray icons: {_appIcons.Count}");
        
        foreach (var icon in _appIcons) {
            DebugLogger.Log($"  - Icon '{icon.Tooltip}' Owner: {icon.OwnerProcess?.AppId ?? "NULL"}, HasWindow: {icon.OwnerWindow != null} (Match: {icon.OwnerProcess == process})");
        }
        
        // Only remove icons that are process-level (no OwnerWindow)
        // Window-owned icons are handled by RemoveIconsForWindow
        int removed = _appIcons.RemoveAll(i => i.OwnerProcess == process && i.OwnerWindow == null);
        if (removed > 0) {
            DebugLogger.Log($"[SystemTray] Removed {removed} process-level tray icon(s) for process {process.AppId}");
            RecalculateWidth();
        }
    }
    
    public void RemoveIconsForWindow(Window window) {
        if (window == null) return;
        
        int removed = _appIcons.RemoveAll(i => i.OwnerWindow == window && !i.PersistAfterWindowClose);
        if (removed > 0) {
            DebugLogger.Log($"[SystemTray] Removed {removed} tray icon(s) for window '{window.Title}' (non-persistent)");
            RecalculateWidth();
        }
    }
    


    /// <summary>
    /// Gets a tray icon by ID for dynamic updates.
    /// </summary>
    public TrayIcon GetIcon(string id) {
        return _appIcons.Find(i => i.Id == id) ?? _systemIcons.Find(i => i.Id == id);
    }

    /// <summary>
    /// Gets all registered tray icons (apps followed by system).
    /// </summary>
    public IEnumerable<TrayIcon> AllIcons => _appIcons.Concat(_systemIcons);

    public override void Update(GameTime gameTime) {
        _gameTime = gameTime; // Store for HandleTrayIconInput
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        _updateTimer += deltaTime;
        if (_updateTimer >= 1f) {
            UpdateTime();
            _updateTimer = 0f;
        }

        UpdateVolumeIcon();
        
        // Update button position based on current size
        _notificationButton.Position = new Vector2(Size.X - 35, 4);
        _notificationButton.Size = new Vector2(30, Size.Y - 8);
        
        // Handle pending delayed click
        if (_pendingClickIconId != null) {
            _pendingClickTimer += deltaTime;
            if (_pendingClickTimer >= ClickDelayThreshold) {
                // Delay exceeded, fire the click
                var icon = GetIcon(_pendingClickIconId);
                icon?.OnClick?.Invoke();
                _pendingClickIconId = null;
            }
        }
        
        // Handle pending delayed right-click
        if (_pendingRightClickIconId != null) {
            _pendingRightClickTimer += deltaTime;
            if (_pendingRightClickTimer >= ClickDelayThreshold) {
                // Delay exceeded, fire the right-click
                var icon = GetIcon(_pendingRightClickIconId);
                icon?.OnRightClick?.Invoke();
                _pendingRightClickIconId = null;
            }
        }
        
        // Note: HandleTrayIconInput was renamed/refactored.
        // Input handling for icons is now partially handled by the logic that was here,
        // but we need to ensure clicks and scrolls still work.
        // Let's restore the input handling logic but keep FindTooltipSubElement for tooltips.
        ProcessTrayIconInput();
        
        base.Update(gameTime);
    }

    private void ProcessTrayIconInput() {
        if (InputManager.IsMouseConsumed || _gameTime == null) return;
        
        var mousePos = InputManager.MousePosition;
        float iconStartX = AbsolutePosition.X + 8f; 
        
        int totalIndex = 0;
        foreach (var icon in AllIcons) {
            float x = iconStartX + (totalIndex * (IconSize + IconSpacing));
            Rectangle iconBounds = new Rectangle((int)x, (int)(AbsolutePosition.Y + (Size.Y - IconSize) / 2f), (int)IconSize, (int)IconSize);
            totalIndex++;

            if (!iconBounds.Contains(mousePos)) continue;
            
            // Mouse wheel
            int scrollDelta = InputManager.ScrollDelta;
            if (scrollDelta != 0) {
                icon.OnMouseWheel?.Invoke(scrollDelta > 0 ? 1 : -1);
                InputManager.IsMouseConsumed = true;
            }
            
            // Left click / double-click
            if (InputManager.IsDoubleClick(MouseButton.Left)) {
                _pendingClickIconId = null;
                icon.OnDoubleClick?.Invoke();
                InputManager.IsMouseConsumed = true;
            } else if (InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
                if (icon.OnDoubleClick == null) {
                    icon.OnClick?.Invoke();
                } else {
                    _pendingClickIconId = icon.Id;
                    _pendingClickTimer = 0f;
                }
                InputManager.IsMouseConsumed = true;
            }
            
            // Right click / double-click
            if (InputManager.IsDoubleClick(MouseButton.Right)) {
                _pendingRightClickIconId = null;
                icon.OnRightDoubleClick?.Invoke();
                InputManager.IsMouseConsumed = true;
            } else if (InputManager.IsMouseButtonJustPressed(MouseButton.Right)) {
                if (icon.OnRightDoubleClick == null) {
                    icon.OnRightClick?.Invoke();
                } else {
                    _pendingRightClickIconId = icon.Id;
                    _pendingRightClickTimer = 0f;
                }
                InputManager.IsMouseConsumed = true;
            }
        }
    }

    public ITooltipTarget FindTooltipSubElement(Vector2 mousePos) {
        float iconStartX = AbsolutePosition.X + 8f; 
        
        int totalIndex = 0;
        foreach (var icon in AllIcons) {
            float x = iconStartX + (totalIndex * (IconSize + IconSpacing));
            Rectangle iconBounds = new Rectangle((int)x, (int)(AbsolutePosition.Y + (Size.Y - IconSize) / 2f), (int)IconSize, (int)IconSize);
            totalIndex++;

            if (iconBounds.Contains(mousePos.ToPoint())) {
                return icon;
            }
        }
        return null;
    }

    private float GetIconXRel(TrayIcon target) {
        int index = 0;
        foreach (var icon in AllIcons) {
            if (icon == target) return 8f + (index * (IconSize + IconSpacing));
            index++;
        }
        return 8f;
    }


    private void UpdateTime() {
        _currentTime = DateTime.Now.ToString("HH:mm");
        RecalculateWidth();
    }

    private void RecalculateWidth() {
        if (GameContent.FontSystem != null) {
            var font = GameContent.FontSystem.GetFont(16);
            float timeWidth = font.MeasureString(_currentTime).X;
            float trayIconsWidth = AllIcons.Count() * (IconSize + IconSpacing);
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
        foreach (var icon in AllIcons) {
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
