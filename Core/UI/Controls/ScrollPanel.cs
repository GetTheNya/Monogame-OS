using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using TheGame.Core.Input;
using TheGame.Graphics;

namespace TheGame.Core.UI.Controls;

public class ScrollPanel : Panel {
    public float ScrollY { get; set; } = 0f;
    public float ScrollX { get; set; } = 0f;
    private float _contentHeight = 0f;
    private float _contentWidth = 0f;
    private float _targetScrollY = 0f;
    private float _targetScrollX = 0f;
    
    // Protected properties for derived classes
    protected float TargetScrollY {
        get => _targetScrollY;
        set => _targetScrollY = value;
    }
    protected float TargetScrollX {
        get => _targetScrollX;
        set => _targetScrollX = value;
    }
    
    // Scrollbars
    private const float ScrollbarWidth = 8f;
    private const float ScrollbarMargin = 2f;
    private bool _isDraggingScrollbarV = false;
    private bool _isDraggingScrollbarH = false;
    private float _scrollbarDragOffsetV = 0f;
    private float _scrollbarDragOffsetH = 0f;
    private float _scrollbarAlphaV = 0f;
    private float _scrollbarAlphaH = 0f;
    private float _scrollbarHoverAlphaV = 0f;
    private float _scrollbarHoverAlphaH = 0f;
    private bool _scrollbarPriorityConsumed = false;
    
    // Cached RasterizerStates to avoid per-frame allocations
    private static readonly RasterizerState _scissorRasterizer = new RasterizerState { ScissorTestEnable = true };
    private static readonly RasterizerState _noScissorRasterizer = new RasterizerState { ScissorTestEnable = false };

    public ScrollPanel(Vector2 position, Vector2 size) : base(position, size) {
        BackgroundColor = Color.Transparent;
        BorderThickness = 0;
        ConsumesInput = true;
    }

    public void UpdateContentHeight(float height) {
        _contentHeight = height;
        ClampScroll();
    }
    
    public void UpdateContentWidth(float width) {
        _contentWidth = width;
        ClampScroll();
    }
    
    public void UpdateContentSize(float width, float height) {
        _contentWidth = width;
        _contentHeight = height;
        ClampScroll();
    }

    protected void ClampScroll() {
        float maxScrollY = MathF.Max(0, _contentHeight - Size.Y);
        if (_targetScrollY < -maxScrollY) _targetScrollY = -maxScrollY;
        if (_targetScrollY > 0) _targetScrollY = 0;
        
        float maxScrollX = MathF.Max(0, _contentWidth - Size.X);
        if (_targetScrollX < -maxScrollX) _targetScrollX = -maxScrollX;
        if (_targetScrollX > 0) _targetScrollX = 0;
    }

    public override Vector2 GetChildOffset(UIElement child) {
        return new Vector2((float)Math.Round(ScrollX), (float)Math.Round(ScrollY));
    }

    protected bool HasScrollableContentV() {
        return _contentHeight > Size.Y;
    }
    
    protected bool HasScrollableContentH() {
        return _contentWidth > Size.X;
    }

    private Rectangle GetScrollbarTrackRectV() {
        var absPos = AbsolutePosition;
        float hGap = HasScrollableContentH() ? (ScrollbarWidth + ScrollbarMargin) : 0;
        return new Rectangle(
            (int)(absPos.X + Size.X - ScrollbarWidth - ScrollbarMargin),
            (int)(absPos.Y + ScrollbarMargin),
            (int)ScrollbarWidth,
            (int)(Size.Y - ScrollbarMargin * 2 - hGap)
        );
    }

    private Rectangle GetScrollbarThumbRectV() {
        if (!HasScrollableContentV()) return Rectangle.Empty;

        var track = GetScrollbarTrackRectV();
        float viewportRatio = Size.Y / Math.Max(1f, _contentHeight);
        float thumbHeight = Math.Max(30f, track.Height * viewportRatio);
        
        float maxScroll = _contentHeight - Size.Y;
        float scrollPercentage = maxScroll > 0 ? (-ScrollY / maxScroll) : 0f;
        
        float maxThumbY = Math.Max(0.1f, track.Height - thumbHeight);
        float thumbY = track.Y + (maxThumbY * scrollPercentage);

        return new Rectangle(track.X, (int)thumbY, track.Width, (int)thumbHeight);
    }

    private Rectangle GetScrollbarTrackRectH() {
        var absPos = AbsolutePosition;
        float vGap = HasScrollableContentV() ? (ScrollbarWidth + ScrollbarMargin) : 0;
        return new Rectangle(
            (int)(absPos.X + ScrollbarMargin),
            (int)(absPos.Y + Size.Y - ScrollbarWidth - ScrollbarMargin),
            (int)(Size.X - ScrollbarMargin * 2 - vGap),
            (int)ScrollbarWidth
        );
    }

    private Rectangle GetScrollbarThumbRectH() {
        if (!HasScrollableContentH()) return Rectangle.Empty;

        var track = GetScrollbarTrackRectH();
        float viewportRatio = Size.X / Math.Max(1f, _contentWidth);
        float thumbWidth = Math.Max(30f, track.Width * viewportRatio);
        
        float maxScroll = _contentWidth - Size.X;
        float scrollPercentage = maxScroll > 0 ? (-ScrollX / maxScroll) : 0f;
        
        float maxThumbX = Math.Max(0.1f, track.Width - thumbWidth);
        float thumbX = track.X + (maxThumbX * scrollPercentage);

        return new Rectangle((int)thumbX, track.Y, (int)thumbWidth, track.Height);
    }

    public override void Update(GameTime gameTime) {
        if (!IsVisible) return;

        // Custom prioritization:
        // 1. If we click on a scrollbar, we consume it BEFORE children see it.
        // BUT only if input wasn't already consumed by a parent (e.g. Window resize edge)
        _scrollbarPriorityConsumed = false;
        if (!InputManager.IsMouseConsumed) {
            var trackV = GetScrollbarTrackRectV();
            var trackH = GetScrollbarTrackRectH();
            bool overV = HasScrollableContentV() && trackV.Contains(InputManager.MousePosition);
            bool overH = HasScrollableContentH() && trackH.Contains(InputManager.MousePosition);
            
            if ((overV || overH) && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
                 InputManager.IsMouseConsumed = true;
                 _scrollbarPriorityConsumed = true; // Track that WE consumed it
            }
        }

        base.Update(gameTime); // This updates children and their input

        // 3. Automatically calculate content size from children's FRESH sizes
        if (Children.Count > 0) {
            float maxR = 0;
            float maxB = 0;
            foreach (var child in Children) {
                if (!child.IsVisible) continue;
                maxR = Math.Max(maxR, child.Position.X + child.Size.X);
                maxB = Math.Max(maxB, child.Position.Y + child.Size.Y);
            }
            _contentWidth = Math.Max(Size.X, maxR);
            _contentHeight = Math.Max(Size.Y, maxB);
            ClampScroll();
        }

        // 4. Smooth visual scroll
        if (!_isDraggingScrollbarV) {
            ScrollY = MathHelper.Lerp(ScrollY, _targetScrollY, 0.12f);
            if (Math.Abs(ScrollY - _targetScrollY) < 0.1f) ScrollY = _targetScrollY;
        }
        if (!_isDraggingScrollbarH) {
            ScrollX = MathHelper.Lerp(ScrollX, _targetScrollX, 0.12f);
            if (Math.Abs(ScrollX - _targetScrollX) < 0.1f) ScrollX = _targetScrollX;
        }
    }

    protected override void UpdateInput() {
        // We do NOT call base.UpdateInput() because we already updated children 
        // and we want to handle IsMouseOver ourselves without being blocked by children.
        // But we still want to set IsMouseOver for hover animations.
        
        var absPos = AbsolutePosition;
        var myBounds = new Rectangle((int)absPos.X, (int)absPos.Y, (int)Size.X, (int)Size.Y);
        bool isMouseInViewport = myBounds.Contains(InputManager.MousePosition);
        
        // Handle Mouse Wheel Scroll (respect IsScrollConsumed for nested scrolling)
        if (isMouseInViewport && InputManager.ScrollDelta != 0 && !InputManager.IsScrollConsumed) {
            if (HasScrollableContentV()) {
                _targetScrollY += (InputManager.ScrollDelta / 120f) * 60f;
                ClampScroll();
                InputManager.IsScrollConsumed = true;
                InputManager.IsMouseConsumed = true;
            } else if (HasScrollableContentH()) {
                _targetScrollX += (InputManager.ScrollDelta / 120f) * 60f;
                ClampScroll();
                InputManager.IsScrollConsumed = true;
                InputManager.IsMouseConsumed = true;
            }
        }

        // Scrollbar Interaction Logic
        var trackV = GetScrollbarTrackRectV();
        var trackH = GetScrollbarTrackRectH();
        bool overTrackV = HasScrollableContentV() && trackV.Contains(InputManager.MousePosition);
        bool overTrackH = HasScrollableContentH() && trackH.Contains(InputManager.MousePosition);

        // Hover status for alpha animations
        // canHover is true if mouse is in viewport and not consumed by someone ABOVE us.
        // (If children consumed it, we still want scrollbars to be visible if we are hovered generally)
        bool canTrackHover = isMouseInViewport && (Parent == null || !InputManager.IsMouseConsumed || _isDraggingScrollbarV || _isDraggingScrollbarH);

        if (HasScrollableContentV()) {
            _scrollbarAlphaV = MathHelper.Lerp(_scrollbarAlphaV, (canTrackHover || _isDraggingScrollbarV) ? 1f : 0.3f, 0.12f);
        } else {
            _scrollbarAlphaV = MathHelper.Lerp(_scrollbarAlphaV, 0f, 0.2f);
        }

        if (HasScrollableContentH()) {
            _scrollbarAlphaH = MathHelper.Lerp(_scrollbarAlphaH, (canTrackHover || _isDraggingScrollbarH) ? 1f : 0.3f, 0.12f);
        } else {
            _scrollbarAlphaH = MathHelper.Lerp(_scrollbarAlphaH, 0f, 0.2f);
        }

        // Vertical Scrollbar Start/Track Logic
        if (HasScrollableContentV()) {
            var thumbRect = GetScrollbarThumbRectV();
            bool isOverThumb = thumbRect.Contains(InputManager.MousePosition);
            _scrollbarHoverAlphaV = MathHelper.Lerp(_scrollbarHoverAlphaV, (isOverThumb || _isDraggingScrollbarV) ? 1f : 0f, 0.2f);

            if (!_isDraggingScrollbarV && overTrackV && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
                if (isOverThumb) {
                    _isDraggingScrollbarV = true;
                    _scrollbarDragOffsetV = InputManager.MousePosition.Y - thumbRect.Y;
                } else {
                    float clickY = InputManager.MousePosition.Y - trackV.Y;
                    float percentage = (clickY - thumbRect.Height / 2) / Math.Max(1f, trackV.Height - thumbRect.Height);
                    _targetScrollY = -(_contentHeight - Size.Y) * MathHelper.Clamp(percentage, 0f, 1f);
                    ClampScroll();
                }
                InputManager.IsMouseConsumed = true;
            }
        }

        // Horizontal Scrollbar Start/Track Logic
        if (HasScrollableContentH()) {
            var thumbRect = GetScrollbarThumbRectH();
            bool isOverThumb = thumbRect.Contains(InputManager.MousePosition);
            _scrollbarHoverAlphaH = MathHelper.Lerp(_scrollbarHoverAlphaH, (isOverThumb || _isDraggingScrollbarH) ? 1f : 0f, 0.2f);

            if (!_isDraggingScrollbarH && overTrackH && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
                if (isOverThumb) {
                    _isDraggingScrollbarH = true;
                    _scrollbarDragOffsetH = InputManager.MousePosition.X - thumbRect.X;
                } else {
                    float clickX = InputManager.MousePosition.X - trackH.X;
                    float percentage = (clickX - thumbRect.Width / 2) / Math.Max(1f, trackH.Width - thumbRect.Width);
                    _targetScrollX = -(_contentWidth - Size.X) * MathHelper.Clamp(percentage, 0f, 1f);
                    ClampScroll();
                }
                InputManager.IsMouseConsumed = true;
            }
        }

        // Drag Updates
        if (_isDraggingScrollbarV) {
            if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                var trackRect = GetScrollbarTrackRectV();
                var thumbRect = GetScrollbarThumbRectV();
                float maxThumbY = trackRect.Height - thumbRect.Height;
                float percentage = maxThumbY > 0 ? ((InputManager.MousePosition.Y - _scrollbarDragOffsetV - trackRect.Y) / maxThumbY) : 0f;
                _targetScrollY = -(_contentHeight - Size.Y) * MathHelper.Clamp(percentage, 0f, 1f);
                ScrollY = _targetScrollY;
                ClampScroll();
                InputManager.IsMouseConsumed = true;
            } else _isDraggingScrollbarV = false;
        }

        if (_isDraggingScrollbarH) {
            if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                var trackRect = GetScrollbarTrackRectH();
                var thumbRect = GetScrollbarThumbRectH();
                float maxThumbX = trackRect.Width - thumbRect.Width;
                float percentage = maxThumbX > 0 ? ((InputManager.MousePosition.X - _scrollbarDragOffsetH - trackRect.X) / maxThumbX) : 0f;
                _targetScrollX = -(_contentWidth - Size.X) * MathHelper.Clamp(percentage, 0f, 1f);
                ScrollX = _targetScrollX;
                ClampScroll();
                InputManager.IsMouseConsumed = true;
            } else _isDraggingScrollbarH = false;
        }

        // Update basic hover state for Panel but don't let it consume if it was already?
        // Actually, we already handled priority.
        IsMouseOver = InputManager.IsMouseHovering(myBounds);
        if (IsMouseOver) {
            OnHover();
            if (ConsumesInput) InputManager.IsMouseConsumed = true;
        }
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
        if (!IsVisible) return;

        // Flush previous draws
        shapeBatch.End();
        spriteBatch.End();

        // Enable scissor rect
        var oldRect = spriteBatch.GraphicsDevice.ScissorRectangle;
        var absPos = AbsolutePosition;
        Rectangle scissor = new Rectangle((int)absPos.X, (int)absPos.Y, (int)Size.X, (int)Size.Y);
        Rectangle finalScissor = Rectangle.Intersect(oldRect, scissor);
        
        if (finalScissor.Width <= 0 || finalScissor.Height <= 0) {
            finalScissor = new Rectangle(0, 0, 0, 0);
        }
        
        spriteBatch.GraphicsDevice.ScissorRectangle = finalScissor;
        spriteBatch.GraphicsDevice.RasterizerState = _scissorRasterizer;
        
        shapeBatch.Begin(); 
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, _scissorRasterizer);

        DrawSelf(spriteBatch, shapeBatch);

        if (finalScissor.Width > 0 && finalScissor.Height > 0) {
            foreach (var child in Children) {
                child.Draw(spriteBatch, shapeBatch);
            }
        }

        shapeBatch.End();
        spriteBatch.End();

        // Restore parent scissor
        spriteBatch.GraphicsDevice.ScissorRectangle = oldRect;
        bool parentHadScissor = oldRect != spriteBatch.GraphicsDevice.Viewport.Bounds;
        spriteBatch.GraphicsDevice.RasterizerState = parentHadScissor ? _scissorRasterizer : _noScissorRasterizer;
        
        shapeBatch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, parentHadScissor ? _scissorRasterizer : _noScissorRasterizer);

        // Draw vertical scrollbar
        if (_scrollbarAlphaV > 0.01f && HasScrollableContentV()) {
            var thumbRect = GetScrollbarThumbRectV();
            if (thumbRect != Rectangle.Empty) {
                var scrollbarAbsPos = AbsolutePosition;
                Rectangle scrollbarScissor = new Rectangle((int)scrollbarAbsPos.X, (int)scrollbarAbsPos.Y, (int)Size.X, (int)Size.Y);
                Rectangle finalScrollbarScissor = parentHadScissor ? Rectangle.Intersect(oldRect, scrollbarScissor) : scrollbarScissor;
                
                if (finalScrollbarScissor.Width > 0 && finalScrollbarScissor.Height > 0) {
                    shapeBatch.End();
                    spriteBatch.End();
                    
                    spriteBatch.GraphicsDevice.ScissorRectangle = finalScrollbarScissor;
                    spriteBatch.GraphicsDevice.RasterizerState = _scissorRasterizer;
                    shapeBatch.Begin();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, _scissorRasterizer);
                    
                    var trackRect = GetScrollbarTrackRectV();
                    float trackAlpha = _scrollbarAlphaV * 0.5f * AbsoluteOpacity;
                    shapeBatch.FillRectangle(trackRect.Location.ToVector2(), trackRect.Size.ToVector2(), new Color(20, 20, 20, (int)(trackAlpha * 120)), rounded: ScrollbarWidth / 2);

                    float thumbAlpha = _scrollbarAlphaV * (0.7f + _scrollbarHoverAlphaV * 0.3f) * AbsoluteOpacity;
                    shapeBatch.FillRectangle(thumbRect.Location.ToVector2(), thumbRect.Size.ToVector2(), new Color(140, 140, 140, (int)(thumbAlpha * 200)), rounded: ScrollbarWidth / 2);
                    
                    shapeBatch.End();
                    spriteBatch.End();
                    
                    spriteBatch.GraphicsDevice.ScissorRectangle = oldRect;
                    spriteBatch.GraphicsDevice.RasterizerState = parentHadScissor ? _scissorRasterizer : _noScissorRasterizer;
                    shapeBatch.Begin();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, parentHadScissor ? _scissorRasterizer : _noScissorRasterizer);
                }
            }
        }

        // Draw horizontal scrollbar
        if (_scrollbarAlphaH > 0.01f && HasScrollableContentH()) {
            var thumbRect = GetScrollbarThumbRectH();
            if (thumbRect != Rectangle.Empty) {
                var scrollbarAbsPos = AbsolutePosition;
                Rectangle scrollbarScissor = new Rectangle((int)scrollbarAbsPos.X, (int)scrollbarAbsPos.Y, (int)Size.X, (int)Size.Y);
                Rectangle finalScrollbarScissor = parentHadScissor ? Rectangle.Intersect(oldRect, scrollbarScissor) : scrollbarScissor;
                
                if (finalScrollbarScissor.Width > 0 && finalScrollbarScissor.Height > 0) {
                    shapeBatch.End();
                    spriteBatch.End();
                    
                    spriteBatch.GraphicsDevice.ScissorRectangle = finalScrollbarScissor;
                    spriteBatch.GraphicsDevice.RasterizerState = _scissorRasterizer;
                    shapeBatch.Begin();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, _scissorRasterizer);
                    
                    var trackRect = GetScrollbarTrackRectH();
                    float trackAlpha = _scrollbarAlphaH * 0.5f * AbsoluteOpacity;
                    shapeBatch.FillRectangle(trackRect.Location.ToVector2(), trackRect.Size.ToVector2(), new Color(20, 20, 20, (int)(trackAlpha * 120)), rounded: ScrollbarWidth / 2);

                    float thumbAlpha = _scrollbarAlphaH * (0.7f + _scrollbarHoverAlphaH * 0.3f) * AbsoluteOpacity;
                    shapeBatch.FillRectangle(thumbRect.Location.ToVector2(), thumbRect.Size.ToVector2(), new Color(140, 140, 140, (int)(thumbAlpha * 200)), rounded: ScrollbarWidth / 2);
                    
                    shapeBatch.End();
                    spriteBatch.End();
                    
                    spriteBatch.GraphicsDevice.ScissorRectangle = oldRect;
                    spriteBatch.GraphicsDevice.RasterizerState = parentHadScissor ? _scissorRasterizer : _noScissorRasterizer;
                    shapeBatch.Begin();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, parentHadScissor ? _scissorRasterizer : _noScissorRasterizer);
                }
            }
        }
    }
}
