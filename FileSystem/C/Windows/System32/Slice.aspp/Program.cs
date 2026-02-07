using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TheGame;
using TheGame.Core.OS;
using TheGame.Core;
using TheGame.Core.Input;
using TheGame.Core.UI;
using TheGame.Graphics;

namespace ScreenCapture;

public class Program : Application {
    public static Application Main(string[] args) => new Program();

    private CaptureState _state = CaptureState.Idle;
    private Texture2D _screenshot;
    private Vector2? _startPos;
    private Rectangle? _selection;
    private TaskCompletionSource<bool> _exitTcs = new();
    private TrayIcon _trayIcon;
    private readonly List<CapturePreview> _previews = new();
    private CaptureHistory _history;

    private const string ScreenshotDir = "C:\\Users\\Admin\\Documents\\Screenshots";

    public override bool IsAsync => true;

    protected override async Task OnLoadAsync(string[] args) {
        ExitOnMainWindowClose = false;
        Shell.Core.SetStartup(Process, true);
        _history = Shell.AppSettings.Load<CaptureHistory>(Process);
        
        // Prune non-existent files on load
        _history.Items.RemoveAll(item => !VirtualFileSystem.Instance.Exists(item.Path));
        Shell.AppSettings.Save(Process, _history);

        try {
            // Setup Tray Icon
            Texture2D iconTex = Shell.Images.LoadAppImage(Process, "tray_icon.png");
            _trayIcon = new TrayIcon(iconTex, "Screen Capture") {
                PersistAfterWindowClose = true,
                OnClick = () => StartCapture(),
                OnDoubleClick = () => ShowHistory(),
                OnRightClick = () => {
                    Shell.ContextMenu.Show(InputManager.MousePosition.ToVector2(), new List<MenuItem> {
                        new MenuItem { Text = "Open screenshot folder", Action = () => Shell.Execute(ScreenshotDir) },
                        new MenuItem { Text = "Make screenshot", Action = () => StartCapture() },
                        new MenuItem { Text = "Open history", Action = () => ShowHistory() },
                        new MenuItem { Type = MenuItemType.Separator },
                        new MenuItem { Text = "Exit", Action = () => Exit() }
                    });
                }
            };
            Shell.SystemTray.AddIcon(Process, _trayIcon);

            // Set initial priority to Low (background)
            Priority = ProcessPriority.Low;
            
            // Wait until the app is done (process terminated)
            await _exitTcs.Task;
        } catch (Exception ex) {
            DebugLogger.Log($"ScreenCapture Load Error: {ex.Message}");
            Exit();
        }
    }

    private async Task StartCapture() {
        if (_state != CaptureState.Idle) return;

        try {
            _state = CaptureState.Capturing;
            Priority = ProcessPriority.High;

            // Trigger the full screen capture
            _screenshot?.Dispose();
            _screenshot = await Shell.Core.TakeScreenshotAsync();
            
            _state = CaptureState.Selecting;
            _startPos = null;
            _selection = null;
        } catch (Exception ex) {
            DebugLogger.Log($"StartCapture Error: {ex.Message}");
            _state = CaptureState.Idle;
            Priority = ProcessPriority.Low;
        }
    }

    protected override void OnUpdate(GameTime gameTime) {
        if (_state != CaptureState.Selecting) return;

        // Prevent mouse clicks from passing through to windows/desktop below
        InputManager.IsMouseConsumed = true;

        // Start selection
        if (InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
            _startPos = InputManager.MousePosition.ToVector2();
            _selection = null;
        }

        // Update selection rect
        if (InputManager.IsMouseButtonDown(MouseButton.Left) && _startPos.HasValue) {
            Vector2 current = InputManager.MousePosition.ToVector2();
            
            float x = Math.Min(_startPos.Value.X, current.X);
            float y = Math.Min(_startPos.Value.Y, current.Y);
            float w = Math.Abs(_startPos.Value.X - current.X);
            float h = Math.Abs(_startPos.Value.Y - current.Y);
            
            _selection = new Rectangle((int)x, (int)y, (int)w, (int)h);
        }

        // Finish selection
        if (InputManager.IsMouseButtonJustReleased(MouseButton.Left) && _selection.HasValue) {
            var finalRect = _selection.Value;
            if (finalRect.Width > 5 && finalRect.Height > 5) {
                _state = CaptureState.Saving;
                SaveRegion(finalRect);
            } else {
                _startPos = null;
                _selection = null;
            }
        }
        
        // Cancel on Escape or Right Click (Go back to Idle)
        if (InputManager.IsKeyJustPressed(Keys.Escape) || InputManager.IsMouseButtonJustPressed(MouseButton.Right)) {
            _state = CaptureState.Idle;
            Priority = ProcessPriority.Low;
            _screenshot?.Dispose();
            _screenshot = null;
            _startPos = null;
            _selection = null;
        }
    }

    private async void SaveRegion(Rectangle rect) {
        try {
            // Crop the texture
            rect.X = MathHelper.Clamp(rect.X, 0, _screenshot.Width);
            rect.Y = MathHelper.Clamp(rect.Y, 0, _screenshot.Height);
            rect.Width = MathHelper.Clamp(rect.Width, 0, _screenshot.Width - rect.X);
            rect.Height = MathHelper.Clamp(rect.Height, 0, _screenshot.Height - rect.Y);

            if (rect.Width <= 0 || rect.Height <= 0) {
                _state = CaptureState.Idle;
                Priority = ProcessPriority.Low;
                return;
            }

            Color[] data = new Color[rect.Width * rect.Height];
            _screenshot.GetData(0, rect, data, 0, data.Length);
            
            Texture2D cropped = new Texture2D(_screenshot.GraphicsDevice, rect.Width, rect.Height);
            cropped.SetData(data);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string fileName = $"Capture_{timestamp}.png";
            string virtualPath = Path.Combine(ScreenshotDir, fileName);

            // Add to history and save
            _history.Items.Add(new HistoryItem {
                Path = virtualPath,
                Timestamp = DateTime.Now
            });
            Shell.AppSettings.Save(Process, _history);

            // Create preview before saving to disk
            ShowPreview(cropped, virtualPath);

            await Task.Run(() => {
                using (var stream = VirtualFileSystem.Instance.OpenWrite(virtualPath)) {
                    if (stream != null) {
                        cropped.SaveAsPng(stream, cropped.Width, cropped.Height);
                    }
                }
            });

            Shell.RefreshExplorers(ScreenshotDir);
        } catch (Exception ex) {
            DebugLogger.Log($"ScreenCapture Save Error: {ex.Message}");
            Shell.Notifications.Show("Screen Capture Error", "Failed to save the region.");
        } finally {
            _state = CaptureState.Idle;
            Priority = ProcessPriority.Low;
            _screenshot?.Dispose();
            _screenshot = null;
            _startPos = null;
            _selection = null;
        }
    }

    private void ShowHistory() {
        // Find existing HistoryWindow or create new
        var existing = Shell.WindowLayer?.Children.FirstOrDefault(w => w is HistoryWindow && w.GetOwnerProcess() == Process);
        if (existing != null) {
            OpenWindow((Window)existing); // Bring to front
            return;
        }

        var historyWin = new HistoryWindow(_history);
        if (Process.MainWindow == null) Process.MainWindow = historyWin;
        OpenWindow(historyWin);
    }

    private void ShowPreview(Texture2D texture, string virtualPath) {
        var preview = new CapturePreview(texture, virtualPath);
        preview.OnClosed += () => {
            lock (_previews) {
                _previews.Remove(preview);
                preview.Dispose();
            }
        };

        lock (_previews) {
            // Position it
            var viewport = G.GraphicsDevice.Viewport;
            float margin = 20f;
            float bottomOffset = 40f; // Above taskbar
            
            // Simple stacking: find the top of the last preview
            float yOffset = bottomOffset;
            foreach (var p in _previews) {
                yOffset += p.Size.Y + margin;
            }

            preview.Position = new Vector2(
                viewport.Width - preview.Size.X - margin,
                viewport.Height - preview.Size.Y - yOffset
            );

            _previews.Add(preview);
        }
    }

    protected override void OnDraw(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
        if (_screenshot == null || _state != CaptureState.Selecting) return;

        int sw = _screenshot.Width;
        int sh = _screenshot.Height;
        Color dimColor = new Color(0, 0, 0, 150);

        // 1. Draw the captured screen (frozen) and dimming using SpriteBatch
        spriteBatch.Begin();
        spriteBatch.Draw(_screenshot, Vector2.Zero, Color.White);

        if (_selection.HasValue) {
            Rectangle rect = _selection.Value;
            
            // Top
            if (rect.Y > 0)
                spriteBatch.Draw(GameContent.Pixel, new Rectangle(0, 0, sw, rect.Y), dimColor);
            // Bottom
            if (rect.Bottom < sh)
                spriteBatch.Draw(GameContent.Pixel, new Rectangle(0, rect.Bottom, sw, sh - rect.Bottom), dimColor);
            // Left (middle part)
            if (rect.X > 0)
                spriteBatch.Draw(GameContent.Pixel, new Rectangle(0, rect.Y, rect.X, rect.Height), dimColor);
            // Right (middle part)
            if (rect.Right < sw)
                spriteBatch.Draw(GameContent.Pixel, new Rectangle(rect.Right, rect.Y, sw - rect.Right, rect.Height), dimColor);
        } else {
            // No selection, dim everything
            spriteBatch.Draw(GameContent.Pixel, new Rectangle(0, 0, sw, sh), dimColor);
        }
        spriteBatch.End();

        // 2. Draw border using ShapeBatch
        if (_selection.HasValue) {
            shapeBatch.Begin();
            Rectangle rect = _selection.Value;
            shapeBatch.BorderRectangle(new Vector2(rect.X, rect.Y), new Vector2(rect.Width, rect.Height), Color.White, 1f);
            shapeBatch.End();
        }
    }

    protected override void OnClose() {
        if (_trayIcon != null) Shell.SystemTray.RemoveIcon(_trayIcon.Id);
        
        _screenshot?.Dispose();
        lock (_previews) {
            foreach (var p in _previews.ToArray()) {
                p.Close();
            }
            _previews.Clear();
        }
        _exitTcs.TrySetResult(true);
    }
}
