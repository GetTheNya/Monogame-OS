using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using TheGame.Core.Animation;
using TheGame.Core.OS;
using TheGame.Graphics;

namespace TheGame.Core.UI;

/// <summary>
/// A popup picker that appears above the taskbar when a process has multiple windows.
/// </summary>
public class TaskbarWindowPicker : Panel {
    private Process _process;
    private Vector2 _buttonPosition;
    private const float ItemHeight = 32f;
    private const float Width = 200f;
    
    public TaskbarWindowPicker() : base(Vector2.Zero, Vector2.Zero) {
        BackgroundColor = new Color(30, 30, 30, 240);
        BorderColor = new Color(80, 80, 80);
        BorderThickness = 1f;
        IsVisible = false;
    }
    
    /// <summary>
    /// Shows the picker for the given process at the specified button position.
    /// </summary>
    public void Show(Process process, Vector2 buttonPosition) {
        if (process == null || process.Windows.Count == 0) return;
        
        _process = process;
        _buttonPosition = buttonPosition;
        
        Rebuild();
        
        // Position above the taskbar button
        float height = process.Windows.Count * ItemHeight + 10;
        Position = new Vector2(buttonPosition.X - Width / 2f, buttonPosition.Y - height - 5);
        Size = new Vector2(Width, height);
        
        // Animate in
        Opacity = 0f;
        IsVisible = true;
        Tweener.To(this, o => Opacity = o, 0f, 1f, 0.15f, Easing.EaseOutQuad);
    }
    
    /// <summary>
    /// Hides the picker with animation.
    /// </summary>
    public void Hide() {
        Tweener.To(this, o => Opacity = o, Opacity, 0f, 0.1f, Easing.EaseInQuad)
            .OnCompleteAction(() => {
                IsVisible = false;
                ClearChildren();
            });
    }
    
    private void Rebuild() {
        ClearChildren();
        
        if (_process == null) return;
        
        float y = 5;
        foreach (var window in _process.Windows) {
            if (!window.ShowInTaskbar) continue;
            
            var btn = new Controls.Button(new Vector2(5, y), new Vector2(Width - 10, ItemHeight - 4), window.Title) {
                BackgroundColor = Color.Transparent,
                HoverColor = new Color(60, 60, 60),
                TextAlign = Controls.TextAlign.Left,
                Icon = window.Icon
            };
            
            var capturedWindow = window;
            btn.OnClickAction = () => {
                if (!capturedWindow.IsVisible || capturedWindow.Opacity < 0.5f) {
                    if (capturedWindow is Window w) w.Restore();
                    else capturedWindow.IsVisible = true; // Fallback for naked WindowBase
                } else {
                    WindowBase.ActiveWindow = capturedWindow;
                    capturedWindow.Parent?.BringToFront(capturedWindow);
                }
                Hide();
            };
            
            AddChild(btn);
            y += ItemHeight;
        }
    }
    
    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        
        // Hide if clicked outside
        if (IsVisible && Input.InputManager.IsMouseButtonJustPressed(Input.MouseButton.Left)) {
            if (!Bounds.Contains(Input.InputManager.MousePosition)) {
                Hide();
            }
        }
    }
    
    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible || Opacity <= 0) return;
        
        var absPos = AbsolutePosition;
        var color = BackgroundColor * Opacity;
        var border = BorderColor * Opacity;
        
        batch.FillRectangle(absPos, Size, color, rounded: 5f);
        batch.BorderRectangle(absPos, Size, border, BorderThickness, rounded: 5f);
    }
}
