using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics; // Added for RasterizerState
using TheGame.Core.UI;
using TheGame.Core.Input;
using TheGame.Core.OS;
using TheGame.Graphics;

namespace TheGame.Core.UI.Widgets;

/// <summary>
/// Base class for all DeskToys widgets.
/// Widgets are lightweight UI components that live on the desktop layer.
/// </summary>
public abstract class Widget : UIElement {
    protected virtual void OnLeftClick(Vector2 localPos) { }
    protected virtual void OnRightClick(Vector2 localPos) { }
    private static readonly RasterizerState _scissorRasterizer = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
    private static readonly RasterizerState _noScissorRasterizer = new RasterizerState { ScissorTestEnable = false, CullMode = CullMode.None };

    public string WidgetId { get; set; }
    public string WidgetType { get; protected set; }
    public int ZIndex { get; set; } = 0;
    public bool IsLocked { get; set; } = true;
    public bool IsResizable { get; set; } = false;
    
    // Refresh policy for performance optimization
    public string RefreshPolicy { get; set; } = "Interval"; // "Interval" or "OnEvent"
    public int UpdateIntervalMs { get; set; } = 0; // 0 means every frame in Interval mode
    private double _timeSinceLastUpdate = 0;

    // Serialized settings
    public Dictionary<string, object> Settings { get; set; } = new();

    protected Widget(Vector2 position, Vector2 size, string widgetId) : base(position, size) {
        WidgetId = widgetId;
        ConsumesInput = true;
        // Default appearance for widgets (often transparent or semi-transparent)
    }

    public virtual void OnSettingsChanged() { }
    
    public virtual Dictionary<string, object> GetDefaultSettings() => new();

    public abstract void SaveToRegistry();
    public abstract void LoadFromRegistry();

    private bool _isDragging = false;
    private Vector2 _dragGrabOffset;
    private bool _dragThresholdExceeded = false;
    private Vector2 _mousePressPos;
    private const float DRAG_THRESHOLD = 5f;

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
        if (!IsVisible) return;

        // Save current scissor and rasterizer state
        Rectangle oldScissor = G.GraphicsDevice.ScissorRectangle;
        
        // Calculate clipping rect (intersect with current scissor for safety)
        Rectangle widgetBounds = Bounds;
        Rectangle newScissor = Rectangle.Intersect(oldScissor, widgetBounds);

        // We MUST flush the current batches before changing the hardware scissor
        shapeBatch.End();
        spriteBatch.End();

        // Apply new scissor
        G.GraphicsDevice.ScissorRectangle = newScissor;

        // Restart batches with scissor testing enabled
        G.GraphicsDevice.RasterizerState = _scissorRasterizer;
        shapeBatch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, _scissorRasterizer);

        try {
            base.Draw(spriteBatch, shapeBatch);
        } finally {
            // Restore state: Flush widget drawing, restore scissor, restart batches
            shapeBatch.End();
            spriteBatch.End();

            G.GraphicsDevice.ScissorRectangle = oldScissor;

            // Resume with global rasterizer (usually no scissor or managed by UIManager)
            G.GraphicsDevice.RasterizerState = _noScissorRasterizer;
            shapeBatch.Begin();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, _noScissorRasterizer);
        }
    }

    public override void Update(GameTime gameTime) {
        if (!Shell.Widgets.IsEditingLayout) {
            if (_isDragging && _dragThresholdExceeded) {
                SaveToRegistry();
            }
            IsLocked = true;
            _isDragging = false;
        } else {
            IsLocked = false;
        }

        if (IsMouseOver && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
            DebugLogger.Log($"[Widget] {WidgetId} ({WidgetType}) detected click! (Base)");
        }

        base.Update(gameTime);

        // Respect refresh policy
        if (RefreshPolicy == "Interval") {
            if (UpdateIntervalMs <= 0) {
                OnWidgetUpdate(gameTime);
            } else {
                _timeSinceLastUpdate += gameTime.ElapsedGameTime.TotalMilliseconds;
                if (_timeSinceLastUpdate >= UpdateIntervalMs) {
                    OnWidgetUpdate(gameTime);
                    _timeSinceLastUpdate = 0;
                }
            }
        }
    }
    protected override void UpdateInput() {
        if (Shell.Widgets.IsEditingLayout) {
            HandleEditModeDrag();
            return;
        }

        base.UpdateInput();

        if (IsMouseOver) {
            Vector2 localPos = InputManager.MousePosition.ToVector2() - AbsolutePosition;

            if (InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
                OnLeftClick(localPos);
                InputManager.IsMouseConsumed = true;
            } else if (InputManager.IsMouseButtonJustPressed(MouseButton.Right)) {
                OnRightClick(localPos);
                InputManager.IsMouseConsumed = true;
            }
        }
    }

    private void HandleEditModeDrag() {
        bool alreadyConsumed = InputManager.IsMouseConsumed;
        bool hovering = InputManager.IsMouseHovering(Bounds, ignoreConsumed: true);

        if (!_isDragging) {
            if (hovering && !alreadyConsumed && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
                _mousePressPos = InputManager.MousePosition.ToVector2();
                _isDragging = true;
                _dragThresholdExceeded = false;
                _dragGrabOffset = _mousePressPos - AbsolutePosition;
                InputManager.IsMouseConsumed = true;
            }
        } else {
            if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                InputManager.IsMouseConsumed = true;
                
                if (!_dragThresholdExceeded) {
                    float dist = Vector2.Distance(_mousePressPos, InputManager.MousePosition.ToVector2());
                    if (dist > DRAG_THRESHOLD) {
                        _dragThresholdExceeded = true;
                    }
                }

                if (_dragThresholdExceeded) {
                    var targetPos = InputManager.MousePosition.ToVector2() - _dragGrabOffset;
                    Position = new Vector2((float)Math.Round(targetPos.X), (float)Math.Round(targetPos.Y));
                }
            } else {
                if (_isDragging && _dragThresholdExceeded) {
                    SaveToRegistry();
                    Shell.Widgets.RefreshWidgets?.Invoke();
                }
                _isDragging = false;
                _dragThresholdExceeded = false;
            }
        }
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
        OnDrawWidget(spriteBatch, shapeBatch);

        if (Shell.Widgets.IsEditingLayout) {
            shapeBatch.BorderRectangle(AbsolutePosition, Size, Color.Yellow * 0.5f, thickness: 2f, rounded: 8f);
        }
    }

    /// <summary>
    /// Derived widgets should override this instead of DrawSelf.
    /// </summary>
    protected virtual void OnDrawWidget(SpriteBatch spriteBatch, ShapeBatch shapeBatch) { }

    /// <summary>
    /// Called based on RefreshPolicy. Use for animations or time-based logic.
    /// </summary>
    protected virtual void OnWidgetUpdate(GameTime gameTime) { }

    protected void NotifySettingsChanged() {
        OnSettingsChanged();
        SaveToRegistry();
    }
}
