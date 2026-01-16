using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using TheGame.Core.Input;
using TheGame.Graphics;

namespace TheGame.Core.UI.Controls;

public class ScrollPanel : Panel {
    public float ScrollY { get; set; } = 0f;
    private float _contentHeight = 0f;
    private float _targetScrollY = 0f;
    
    // Protected property for derived classes
    protected float TargetScrollY {
        get => _targetScrollY;
        set => _targetScrollY = value;
    }
    
    // Scrollbar
    private const float ScrollbarWidth = 8f;
    private const float ScrollbarMargin = 2f;
    private bool _isDraggingScrollbar = false;
    private float _scrollbarDragOffset = 0f;
    private float _scrollbarAlpha = 0f;
    private float _scrollbarHoverAlpha = 0f;
    
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

    protected void ClampScroll() {
        float maxScroll = MathF.Max(0, _contentHeight - Size.Y);
        if (_targetScrollY < -maxScroll) _targetScrollY = -maxScroll;
        if (_targetScrollY > 0) _targetScrollY = 0;
    }

    public override Vector2 GetChildOffset(UIElement child) {
        return new Vector2(0, (float)Math.Round(ScrollY));
    }

    protected bool HasScrollableContent() {
        return _contentHeight > Size.Y;
    }

    private Rectangle GetScrollbarTrackRect() {
        var absPos = AbsolutePosition;
        return new Rectangle(
            (int)(absPos.X + Size.X - ScrollbarWidth - ScrollbarMargin),
            (int)(absPos.Y + ScrollbarMargin),
            (int)ScrollbarWidth,
            (int)(Size.Y - ScrollbarMargin * 2)
        );
    }

    private Rectangle GetScrollbarThumbRect() {
        if (!HasScrollableContent()) return Rectangle.Empty;

        var track = GetScrollbarTrackRect();
        float viewportRatio = Size.Y / _contentHeight;
        float thumbHeight = Math.Max(30f, track.Height * viewportRatio);
        
        float maxScroll = _contentHeight - Size.Y;
        float scrollPercentage = maxScroll > 0 ? (-ScrollY / maxScroll) : 0f;
        
        float maxThumbY = track.Height - thumbHeight;
        float thumbY = track.Y + (maxThumbY * scrollPercentage);

        return new Rectangle(track.X, (int)thumbY, track.Width, (int)thumbHeight);
    }

    public override void Update(GameTime gameTime) {
        var absPos = AbsolutePosition;
        var myBounds = new Rectangle((int)absPos.X, (int)absPos.Y, (int)Size.X, (int)Size.Y);
        bool isInBounds = myBounds.Contains(InputManager.MousePosition) && !InputManager.IsMouseConsumed;
        
        // Animate scrollbar visibility
        if (HasScrollableContent()) {
            if (isInBounds || _isDraggingScrollbar) {
                _scrollbarAlpha = MathHelper.Lerp(_scrollbarAlpha, 1f, 0.15f);
            } else {
                _scrollbarAlpha = MathHelper.Lerp(_scrollbarAlpha, 0.3f, 0.1f);
            }
        } else {
            _scrollbarAlpha = MathHelper.Lerp(_scrollbarAlpha, 0f, 0.2f);
        }

        // Handle scrollbar dragging
        if (HasScrollableContent() && isInBounds) {
            var thumbRect = GetScrollbarThumbRect();
            var trackRect = GetScrollbarTrackRect();
            bool isHoveringThumb = thumbRect.Contains(InputManager.MousePosition);
            
            // Hover animation
            if (isHoveringThumb || _isDraggingScrollbar) {
                _scrollbarHoverAlpha = MathHelper.Lerp(_scrollbarHoverAlpha, 1f, 0.2f);
            } else {
                _scrollbarHoverAlpha = MathHelper.Lerp(_scrollbarHoverAlpha, 0f, 0.2f);
            }

            // Start dragging
            if (isHoveringThumb && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
                _isDraggingScrollbar = true;
                _scrollbarDragOffset = InputManager.MousePosition.Y - thumbRect.Y;
                InputManager.IsMouseConsumed = true;
            }

            // Handle track click (jump to position)
            if (!_isDraggingScrollbar && trackRect.Contains(InputManager.MousePosition) && 
                !thumbRect.Contains(InputManager.MousePosition) && 
                InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
                
                float clickY = InputManager.MousePosition.Y - trackRect.Y;
                float thumbHeight = thumbRect.Height;
                float maxThumbY = trackRect.Height - thumbHeight;
                float percentage = (clickY - thumbHeight / 2) / maxThumbY;
                percentage = MathHelper.Clamp(percentage, 0f, 1f);
                
                float maxScroll = _contentHeight - Size.Y;
                _targetScrollY = -maxScroll * percentage;
                ClampScroll();
                InputManager.IsMouseConsumed = true;
            }
        }

        // Update dragging
        if (_isDraggingScrollbar) {
            if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                var trackRect = GetScrollbarTrackRect();
                var thumbRect = GetScrollbarThumbRect();
                
                float newThumbY = InputManager.MousePosition.Y - _scrollbarDragOffset;
                float maxThumbY = trackRect.Height - thumbRect.Height;
                float clampedThumbY = MathHelper.Clamp(newThumbY - trackRect.Y, 0, maxThumbY);
                
                float percentage = maxThumbY > 0 ? (clampedThumbY / maxThumbY) : 0f;
                float maxScroll = _contentHeight - Size.Y;
                _targetScrollY = -maxScroll * percentage;
                ScrollY = _targetScrollY; // Immediate for dragging
                ClampScroll();
                InputManager.IsMouseConsumed = true;
            } else {
                _isDraggingScrollbar = false;
            }
        }

        // Mouse wheel scrolling - only if in bounds and not consumed
        if (isInBounds) {
            float scrollDelta = InputManager.ScrollDelta;
            if (scrollDelta != 0 && HasScrollableContent()) {
                _targetScrollY += (scrollDelta / 120f) * 60f;
                ClampScroll();
                InputManager.IsMouseConsumed = true;
            }
        }

        // Smoothly interpolate ScrollY to targetScrollY (only when not dragging)
        if (!_isDraggingScrollbar) {
            ScrollY = MathHelper.Lerp(ScrollY, _targetScrollY, 0.12f);
            if (Math.Abs(ScrollY - _targetScrollY) < 0.1f) ScrollY = _targetScrollY;
        }

        base.Update(gameTime);
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
        if (!IsVisible) return;

        // Flush previous draws
        shapeBatch.End();
        spriteBatch.End();

        // Enable scissor rect
        var oldRect = spriteBatch.GraphicsDevice.ScissorRectangle;
        var absPos = AbsolutePosition;
        
        // Calculate scissor rect in screen coordinates
        Rectangle scissor = new Rectangle((int)absPos.X, (int)absPos.Y, (int)Size.X, (int)Size.Y);
        
        // Intersect with parent scissor if necessary
        Rectangle finalScissor = Rectangle.Intersect(oldRect, scissor);
        
        // Ensure scissor rect is valid
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


        // Restore
        spriteBatch.GraphicsDevice.ScissorRectangle = oldRect;
        
        // Restart with the PARENT's scissor state if it was active
        bool parentHadScissor = oldRect != spriteBatch.GraphicsDevice.Viewport.Bounds;
        spriteBatch.GraphicsDevice.RasterizerState = parentHadScissor ? _scissorRasterizer : _noScissorRasterizer;
        
        shapeBatch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, parentHadScissor ? _scissorRasterizer : _noScissorRasterizer);

        // Draw scrollbar WITH scissor test to prevent drawing outside bounds
        if (_scrollbarAlpha > 0.01f && HasScrollableContent()) {
            var thumbRect = GetScrollbarThumbRect();
            if (thumbRect != Rectangle.Empty) {
                // Apply scissor test for scrollbar
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
                    
                    // Scrollbar track (dark background)
                    var trackRect = GetScrollbarTrackRect();
                    float trackAlpha = _scrollbarAlpha * 0.5f;
                    shapeBatch.FillRectangle(
                        trackRect.Location.ToVector2(),
                        trackRect.Size.ToVector2(),
                        new Color(20, 20, 20, (int)(trackAlpha * 120)),
                        rounded: ScrollbarWidth / 2
                    );

                    // Scrollbar thumb (light gray, gets lighter on hover)
                    float thumbAlpha = _scrollbarAlpha * (0.7f + _scrollbarHoverAlpha * 0.3f);
                    Color thumbColor = new Color(140, 140, 140, (int)(thumbAlpha * 200));
                    shapeBatch.FillRectangle(
                        thumbRect.Location.ToVector2(),
                        thumbRect.Size.ToVector2(),
                        thumbColor,
                        rounded: ScrollbarWidth / 2
                    );
                    
                    shapeBatch.End();
                    spriteBatch.End();
                    
                    // Restore parent scissor
                    spriteBatch.GraphicsDevice.ScissorRectangle = oldRect;
                    spriteBatch.GraphicsDevice.RasterizerState = parentHadScissor ? _scissorRasterizer : _noScissorRasterizer;
                    shapeBatch.Begin();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, parentHadScissor ? _scissorRasterizer : _noScissorRasterizer);
                }
            }
        }
    }
}

