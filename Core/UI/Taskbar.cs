using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using TheGame.Core.Animation;
using TheGame.Core.Input;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;

namespace TheGame.Core.UI;

public class Taskbar : Panel {
    public static Taskbar Instance { get; private set; }
    private const float StartButtonWidth = 100f;
    private const float WindowButtonWidth = 160f;
    private const float Padding = 2f;
    private const float WindowListOffset = StartButtonWidth + 10f; // Gap after start button

    private Button _startButton;
    private Panel _windowListPanel;
    private UIElement _windowLayer;
    private UIElement _startMenu; // Reference to toggle
    private SystemTray _systemTray;
    public SystemTray SystemTray => _systemTray;

    // Track processes independently
    private List<OS.Process> _trackedProcesses = new();
    private Dictionary<OS.Process, float> _cachedButtonWidths = new();
    private float _lastMaxAllowed = 0f;
    private TaskbarPreviewPopup _previewPopup;
    private OS.Process _hoveredProcess;
    private float _hoverTimer = 0f;
    private const float HoverThreshold = 0.4f; // 400ms delay
    
    private float _hideTimer = 0f;
    private const float HideThreshold = 0.3f; // 300ms grace period

    public Taskbar(Vector2 position, Vector2 size, UIElement windowLayer, UIElement startMenu, VolumeMixerPanel volumeMixer) : base(position, size) {
        Instance = this;
        _windowLayer = windowLayer;
        _startMenu = startMenu;

        BackgroundColor = new Color(20, 20, 20);
        BorderColor = new Color(60, 60, 60);
        BorderThickness = 1f;

        // Start Button
        _startButton = new Button(new Vector2(Padding, Padding), new Vector2(StartButtonWidth, size.Y - (Padding * 2)), "Start") {
            BackgroundColor = new Color(0, 120, 215),
            HoverColor = new Color(0, 150, 255),
            OnClickAction = () => {
                if (_startMenu is StartMenu sm) sm.Toggle();
                else _startMenu.IsVisible = !_startMenu.IsVisible; // Fallback
            },
            Tooltip = "Open Start Menu"
        };
        AddChild(_startButton);

        // Window List Container
        _windowListPanel = new Panel(new Vector2(WindowListOffset, 0), new Vector2(size.X - WindowListOffset - 120f, size.Y)) {
            BackgroundColor = Color.Transparent,
            BorderColor = Color.Transparent,
            ConsumesInput = false
        };
        AddChild(_windowListPanel);

        // System Tray
        _systemTray = new SystemTray(new Vector2(size.X - 120f, 0), new Vector2(120f, size.Y), volumeMixer);
        AddChild(_systemTray);
        OS.Shell.SystemTray.Initialize(_systemTray);

        // Window Preview Popup
        _previewPopup = new TaskbarPreviewPopup();
        OS.Shell.AddOverlayElement(_previewPopup);
    }

    public override void Update(GameTime gameTime) {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        UpdateWindowButtons(deltaTime);

        bool isDragging = (_draggingButton != null);

        // Handle Button Dragging Logic
        if (_draggingButton != null) {
            CustomCursor.Instance.SetCursor(CursorType.Move);
            InputManager.IsMouseConsumed = true; // Consuming input so buttons don't react
            if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                // Dragging: Clamp to prevent dragging over Start button (left) or System Tray (right)
                float dragX = InputManager.MousePosition.X - _windowListPanel.AbsolutePosition.X + _dragOffsetX;
                float minX = Padding;
                float maxX = _windowListPanel.Size.X - _draggingButton.Size.X - Padding;
                _draggingButton.Position = new Vector2(Math.Clamp(dragX, minX, maxX), _draggingButton.Position.Y);

                // Live Reorder Logic: Early swap detection using leading edges
                float dragLeft = _draggingButton.Position.X;
                float dragRight = _draggingButton.Position.X + _draggingButton.Size.X;

                int targetIndex = _dragSourceIndex;
                float maxAllowed = GetMaxAllowedWidth();
                
                // Calculate center offset the same way as in UpdateWindowButtons
                float totalWidth = 0;
                foreach (var child in _windowListPanel.Children) {
                    if (child is ProgressButton btn && btn.Tag is OS.Process proc) {
                        totalWidth += CalculateButtonWidth(proc, maxAllowed) + Padding;
                    } else if (child is Button btnOther) {
                        totalWidth += btnOther.Size.X + Padding;
                    }
                }
                float centerOffset = Math.Max(Padding, (_windowListPanel.Size.X - totalWidth) / 2f);
                
                float cumulativeX = centerOffset;
                for (int i = 0; i < _trackedProcesses.Count; i++) {
                    var proc = _trackedProcesses[i];
                    if (!_cachedButtonWidths.TryGetValue(proc, out float btnWidth) || _lastMaxAllowed != maxAllowed) {
                        btnWidth = CalculateButtonWidth(proc, maxAllowed);
                        _cachedButtonWidths[proc] = btnWidth;
                    }
                    float mid = cumulativeX + (btnWidth / 2f);

                    if (i < _dragSourceIndex) {
                        // Dragging Left: Trigger if your Leading Edge (Left) crosses the neighbor's midpoint
                        if (dragLeft < mid) {
                            targetIndex = i;
                            break;
                        }
                    } else if (i > _dragSourceIndex) {
                        // Dragging Right: Trigger if your Leading Edge (Right) crosses the neighbor's midpoint
                        if (dragRight > mid) {
                            targetIndex = i;
                            // continue to check further icons
                        }
                    }

                    cumulativeX += btnWidth + Padding;
                }
                _lastMaxAllowed = maxAllowed;

                // Validity checks to prevent crash
                if (targetIndex != _dragSourceIndex &&
                    _dragSourceIndex >= 0 && _dragSourceIndex < _trackedProcesses.Count &&
                    targetIndex >= 0 && targetIndex < _trackedProcesses.Count) {
                    // Swap Data in _trackedProcesses (logical order)
                    var procOffset = _trackedProcesses[_dragSourceIndex];
                    _trackedProcesses.RemoveAt(_dragSourceIndex);
                    _trackedProcesses.Insert(targetIndex, procOffset);

                    // Swap Buttons in Children (keep in sync)
                    var btnOffset = _windowListPanel.Children[_dragSourceIndex];
                    _windowListPanel.RemoveChildAt(_dragSourceIndex);
                    _windowListPanel.InsertChild(targetIndex, btnOffset);

                    // Update logical index
                    _dragSourceIndex = targetIndex;
                }

            } else {
                // Drop (Just release, order is already correct from live swap)
                _draggingButton = null;
                InputManager.IsMouseConsumed = true; // Still consume in release frame
            }
        }

        // Check for Potential Drag Start (Only if not already dragging and input not consumed)
        if (!isDragging && _potentialDragButton == null && !InputManager.IsMouseConsumed && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
            for (int i = 0; i < _windowListPanel.Children.Count; i++) {
                var btn = _windowListPanel.Children[i] as Button;
                if (btn != null && btn.Bounds.Contains(InputManager.MousePosition)) {
                    // Record potential drag, don't start actual drag yet
                    _potentialDragButton = btn;
                    _potentialDragStart = InputManager.MousePosition.ToVector2();
                    _dragSourceIndex = i;
                    _dragOffsetX = btn.Position.X - (InputManager.MousePosition.X - _windowListPanel.AbsolutePosition.X);
                    break;
                }
            }
        }

        // Handle Potential Drag -> Real Drag
        if (_potentialDragButton != null) {
            if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                if (Vector2.Distance(InputManager.MousePosition.ToVector2(), _potentialDragStart) > 5f) { // 5px threshold
                    _draggingButton = _potentialDragButton;
                    _potentialDragButton = null;
                    // Keep _dragSourceIndex as set when press started
                }
            } else {
                // Released before dragging
                _potentialDragButton = null;
            }
        }

        UpdatePreviewPopup(gameTime);
        base.Update(gameTime); // Update Children (Buttons)
    }

    private void UpdatePreviewPopup(GameTime gameTime) {
        // If we are dragging, don't show previews
        if (_draggingButton != null) {
            _previewPopup.Hide();
            _hoveredProcess = null;
            return;
        }

        OS.Process currentlyHovered = null;
        Button hoveredBtn = null;

        foreach (var child in _windowListPanel.Children) {
            if (child is Button btn && btn.IsMouseOver && btn.Tag is OS.Process proc) {
                currentlyHovered = proc;
                hoveredBtn = btn;
                break;
            }
        }

        if (currentlyHovered != null) {
            _hideTimer = 0f;
            if (_hoveredProcess != currentlyHovered) {
                // We moved to a NEW process button or just started hovering
                // If a preview is already showing for a DIFFERENT process, hide it immediately
                if (_previewPopup.IsVisible && _hoveredProcess != null) {
                    _previewPopup.Hide(force: true);
                    // If switching between buttons, make it feel faster
                    _hoverTimer = HoverThreshold; 
                } else {
                    _hoverTimer = 0f;
                }
                
                _hoveredProcess = currentlyHovered;
            }

            if (!_previewPopup.IsVisible || (_previewPopup.Opacity < 1.0f && !_previewPopup.IsPinned)) {
                _hoverTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_hoverTimer >= HoverThreshold) {
                    _previewPopup.Show(currentlyHovered, hoveredBtn.AbsolutePosition, hoveredBtn.Size.X, pin: false);
                }
            }
        } else {
            // No button is currently hovered
            _hoverTimer = 0f;
            
            // Check if mouse is over the preview popup itself, if so, keep it open
            if (_previewPopup.IsMouseOver) {
                _hideTimer = 0f;
            } else {
                _hideTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                
                // If clicked somewhere else, force hide even pinned
                bool justClicked = InputManager.IsMouseButtonJustPressed(MouseButton.Left) || InputManager.IsMouseButtonJustPressed(MouseButton.Right);
                if (justClicked || _hideTimer >= HideThreshold) {
                    _previewPopup.Hide(force: justClicked);
                }
            }
            
            // Clear process if it actually went away
            if (!_previewPopup.IsVisible) {
                _hoveredProcess = null;
            }
        }

    }

    private Button _potentialDragButton;
    private Vector2 _potentialDragStart;
    private Button _draggingButton;
    private int _dragSourceIndex;
    private float _dragOffsetX;


    private float GetMaxAllowedWidth() {
        if (_trackedProcesses.Count == 0) return 250f;
        float availableWidth = _windowListPanel.Size.X - Padding;
        return Math.Min(250f, (availableWidth / _trackedProcesses.Count) - Padding);
    }

    private void UpdateWindowButtons(float deltaTime) {
        float trayWidth = _systemTray.DesiredWidth;

        // Dynamic Layout Adjustment
        _windowListPanel.Position = new Vector2(WindowListOffset, 0);
        _windowListPanel.Size = new Vector2(Size.X - WindowListOffset - trayWidth, Size.Y);

        // Enforce Tray Position
        _systemTray.Position = new Vector2(Size.X - trayWidth, 0);
        _systemTray.Size = new Vector2(trayWidth, Size.Y);

        var currentWindows = _windowLayer.Children.OfType<Window>().ToList();
        var taskbarWindows = currentWindows.Where(w => w.ShowInTaskbar).ToList();
        var processesWithTaskbarWindows = taskbarWindows.Select(w => w.OwnerProcess).Where(p => p != null).Distinct().ToList();

        // 1. Handle New Processes (Incremental Add)
        foreach (var proc in processesWithTaskbarWindows) {
            if (!_trackedProcesses.Contains(proc)) {
                _trackedProcesses.Add(proc);
                
                float maxAllowed = GetMaxAllowedWidth();
                float width = CalculateButtonWidth(proc, maxAllowed);
                
                // Spawn position: Start at the end of the current list to slide in
                float lastX = Padding;
                if (_windowListPanel.Children.Count > 0) {
                    var last = _windowListPanel.Children.Last();
                    lastX = last.Position.X + last.Size.X + Padding;
                }

                var btn = CreateTaskbarButton(proc, new Vector2(lastX + 200f, Padding), width);
                btn.Opacity = 0f;
                Tweener.To(btn, o => btn.Opacity = o, 0f, 1f, 0.4f, Easing.Linear);
                _windowListPanel.AddChild(btn);
            }
        }

        // 2. Handle Processes with no taskbar windows left (Incremental Remove)
        for (int i = _trackedProcesses.Count - 1; i >= 0; i--) {
            var trackedProc = _trackedProcesses[i];
            bool hasAnyTaskbarWindows = taskbarWindows.Any(w => w.OwnerProcess == trackedProc);
            
            if (!hasAnyTaskbarWindows) {
                _trackedProcesses.RemoveAt(i);
                _cachedButtonWidths.Remove(trackedProc);
                
                // Find the specific button for this process
                var btn = _windowListPanel.Children.FirstOrDefault(c => c.Tag == trackedProc) as Button;
                if (btn != null) {
                    btn.Tag = null; // Detach from process
                    btn.ConsumesInput = false; // Prevent interaction during death
                    
                    // Animate Death
                    Tweener.To(btn, o => btn.Opacity = o, btn.Opacity, 0f, 0.3f, Easing.Linear);
                    Tweener.To(btn, w => btn.Size = new Vector2(w, btn.Size.Y), btn.Size.X, 0f, 0.3f, Easing.EaseInQuad)
                        .OnCompleteAction(() => _windowListPanel.RemoveChild(btn));
                }
            }
        }

        // 3. Update Visuals & Lerp Positions
        var buttons = _windowListPanel.Children;
        float maxAllowedCur = GetMaxAllowedWidth();
        
        // Calculate total width for centering
        float totalWidth = 0;
        foreach (var child in buttons) {
            if (child is Button btn) {
                if (btn.Tag is OS.Process proc) totalWidth += CalculateButtonWidth(proc, maxAllowedCur) + Padding;
                else totalWidth += btn.Size.X + Padding;
            }
        }
        
        float currentX = Math.Max(Padding, (_windowListPanel.Size.X - totalWidth) / 2f);

        foreach (var child in buttons) {
            if (child is Button btn) {
                // If button is still attached to a process, update its properties
                if (btn.Tag is OS.Process proc) {
                    if (!_cachedButtonWidths.TryGetValue(proc, out float width) || _lastMaxAllowed != maxAllowedCur) {
                        width = CalculateButtonWidth(proc, maxAllowedCur);
                        _cachedButtonWidths[proc] = width;
                    }
                    
                    var procWindows = taskbarWindows.Where(w => w.OwnerProcess == proc).ToList();
                    bool isActive = procWindows.Any(w => w == Window.ActiveWindow);
                    bool isAnyVisible = procWindows.Any(w => w.IsVisible && w.Opacity > 0.5f);

                    // Use MainWindow title and icon if available
                    string baseTitle = proc.MainWindow?.Title ?? proc.AppId;
                    string title = baseTitle;
                    if (procWindows.Count > 1) title += $" ({procWindows.Count})";
                    
                    btn.Text = title;
                    btn.Icon = proc.MainWindow?.Icon ?? procWindows.FirstOrDefault()?.Icon;
                    btn.Size = new Vector2(width, Size.Y - (Padding * 2));

                    if (btn is ProgressButton pbtn) {
                        pbtn.Progress = proc.Progress;
                        pbtn.ProgressColor = proc.ProgressColor;
                    }

                    // Visuals
                    if (isActive) btn.BackgroundColor = new Color(80, 80, 80);
                    else if (!isAnyVisible) btn.BackgroundColor = new Color(40, 40, 40, 150);
                    else btn.BackgroundColor = new Color(50, 50, 50);
                }

                // Dragging Visual Override
                if (btn == _draggingButton) {
                    btn.BackgroundColor = Color.CornflowerBlue;
                } else {
                    float baseX = currentX;

                    // Compact Gap Shift (for reordering feel)
                    if (_draggingButton != null) {
                        // We need the index in _trackedProcesses to compare with _dragSourceIndex
                        int trackIdx = _trackedProcesses.IndexOf(btn.Tag as OS.Process);
                        if (trackIdx != -1 && trackIdx > _dragSourceIndex) {
                             baseX += (20f);
                        }
                    }

                    Vector2 targetPos = new Vector2(baseX, Padding);

                    float lerpSpeed = 4f; 
                    float t = 1f - MathF.Pow(0.001f, deltaTime * lerpSpeed);
                    btn.Position = Vector2.Lerp(btn.Position, targetPos, t);
                }

                // Update currentX based on CURRENT (possibly shrinking) width
                currentX += btn.Size.X + Padding;
            }
        }
        _lastMaxAllowed = maxAllowedCur;
    }

    private Button CreateTaskbarButton(OS.Process proc, Vector2 pos, float width) {
        var btn = new ProgressButton(pos, new Vector2(width, Size.Y - (Padding * 2)), proc.AppId) {
            BackgroundColor = new Color(50, 50, 50),
            HoverColor = new Color(70, 70, 70),
            Tag = proc // Store process reference
        };

        btn.OnClickAction = () => {
            // Get all taskbar windows for this process
            var currentWindows = _windowLayer.Children.OfType<TheGame.Core.UI.Window>().ToList();
            var procWindows = currentWindows.Where(w => w.ShowInTaskbar && w.OwnerProcess == proc).ToList();
            
            if (procWindows.Count == 0) return;

            if (procWindows.Count == 1) {
                var win = procWindows[0];
                Vector2 center = btn.AbsolutePosition + (btn.Size / 2f);
                if (!win.IsVisible || win.Opacity < 0.5f) {
                    win.Restore(center);
                } else {
                    if (TheGame.Core.UI.Window.ActiveWindow == win) {
                        win.Minimize(center);
                    } else {
                        Window.ActiveWindow = win;
                        win.Parent?.BringToFront(win);
                    }
                }
            } else {
                // Multiple windows - show previews pinned
                if (_previewPopup.IsVisible && _previewPopup.IsPinned && _hoveredProcess == proc) {
                    _previewPopup.Hide(force: true);
                } else {
                    _previewPopup.Show(proc, btn.AbsolutePosition, btn.Size.X, pin: true);
                    _hoveredProcess = proc;
                }
            }
        };

        return btn;
    }

    public Vector2 GetButtonCenter(OS.Process process) {
        int index = _trackedProcesses.IndexOf(process);
        if (index == -1 || index >= _windowListPanel.Children.Count) return Vector2.Zero;

        var btn = _windowListPanel.Children[index];
        return btn.AbsolutePosition + (btn.Size / 2f);
    }

    private float CalculateButtonWidth(OS.Process proc, float maxAllowed = 250f) {
        float minWidth = 30f;
        float safeMax = Math.Max(minWidth, maxAllowed);
        string title = proc.MainWindow?.Title ?? proc.AppId;

        if (GameContent.FontSystem == null) return Math.Min(WindowButtonWidth, safeMax);
        var font = GameContent.FontSystem.GetFont(20);
        if (font == null) return Math.Min(WindowButtonWidth, safeMax);

        float textWidth = font.MeasureString(title).X;
        float iconSpace = 30f; // Padding + Icon + Padding
        float finalWidth = textWidth + iconSpace + 20f; // Extra breathing room

        return Math.Clamp(finalWidth, minWidth, safeMax);
    }

    private void FullRebuild() {
        float trayWidth = _systemTray?.DesiredWidth ?? 120f;
        // Force layout sync before rebuilding children
        _windowListPanel.Position = new Vector2(WindowListOffset, 0);
        _windowListPanel.Size = new Vector2(Size.X - WindowListOffset - trayWidth, Size.Y);

        if (_systemTray != null) _systemTray.Position = new Vector2(Size.X - trayWidth, 0);

        // Clear existing buttons
        foreach (var child in _windowListPanel.Children.ToArray()) {
            _windowListPanel.RemoveChild(child);
        }

        // NOTE: We don't reset _draggingButton here anymore to preserve drag across rebuilds
        // though rebuilds should now be rare (only on window close/open)

        float maxAllowed = GetMaxAllowedWidth();

        float xOffset = Padding;
        for (int i = 0; i < _trackedProcesses.Count; i++) {
            var proc = _trackedProcesses[i];
            float width = CalculateButtonWidth(proc, maxAllowed);

            var btn = CreateTaskbarButton(proc, new Vector2(xOffset, Padding), width);
            _windowListPanel.AddChild(btn);
            xOffset += width + Padding;
        }
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible) return;
        DrawSelf(spriteBatch, batch);

        // Draw children normally, but handle window list specially to support top-layer dragging
        foreach (var child in Children) {
            if (child == _windowListPanel) {
                // Draw all buttons EXCEPT the one being dragged
                foreach (var btn in _windowListPanel.Children) {
                    if (btn == _draggingButton) continue;
                    btn.Draw(spriteBatch, batch);
                }
            } else if (child != _previewPopup) {
                child.Draw(spriteBatch, batch);
            }
        }

        // Final draw for the dragged button to ensure it's on the absolute top
        if (_draggingButton != null) {
            _draggingButton.Draw(spriteBatch, batch);
        }

    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;
        batch.FillRectangle(absPos, Size, BackgroundColor);
        batch.DrawLine(absPos, new Vector2(absPos.X + Size.X, absPos.Y), 1f, BorderColor, BorderColor);
    }
}
