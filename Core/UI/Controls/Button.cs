using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using TheGame.Graphics;

namespace TheGame.Core.UI.Controls;

public enum TextAlign { Center, Left, Right }

public class Button : UIControl {
    public string Text { get; set; } = "";
    public Texture2D Icon { get; set; }
    public Action OnClickAction { get; set; }
    public Color TextColor { get; set; } = Color.White;
    public TextAlign TextAlign { get; set; } = TextAlign.Center;
    public int FontSize { get; set; } = 20;
    public Vector2 Padding { get; set; } = new Vector2(5, 5);
    
    // Scrolling logic
    private enum ScrollState { WaitingAtStart, ScrollingForward, WaitingAtEnd, Returning }
    private ScrollState _scrollState = ScrollState.WaitingAtStart;
    private float _scrollOffset = 0f;
    private float _scrollTimer = 0f;
    private bool _isTextOverflowing = false;
    private float _fullTextWidth = 0f;
    private string _lastMeasuredText = "";
    private int _lastMeasuredFontSize = -1;

    public Button(Vector2 position, Vector2 size, string text = "") : base(position, size) {
        Text = text;
        ConsumesInput = true;
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (GameContent.FontSystem != null && !string.IsNullOrEmpty(Text)) {
            var font = GameContent.FontSystem.GetFont((int)(FontSize * Scale));
            if (font != null) {
                // Check if text or font size changed to re-measure
                if (Text != _lastMeasuredText || (int)(FontSize * Scale) != _lastMeasuredFontSize) {
                    _fullTextWidth = font.MeasureString(Text).X;
                    _lastMeasuredText = Text;
                    _lastMeasuredFontSize = (int)(FontSize * Scale);
                }

                float pX = Padding.X * Scale;
                float iconAreaWidth = Icon != null ? (Size.Y * Scale - (Padding.Y * Scale * 2)) + pX : 0;
                float remainingWidth = (Size.X * Scale) - pX - iconAreaWidth - pX;

                _isTextOverflowing = _fullTextWidth > remainingWidth + 2f; // 2px tolerance like in TextHelper

                if (_isTextOverflowing) {
                    float maxScroll = _fullTextWidth - remainingWidth;
                    if (IsMouseOver) {
                        switch (_scrollState) {
                            case ScrollState.WaitingAtStart:
                                _scrollTimer += dt;
                                if (_scrollTimer > 1.0f) {
                                    _scrollState = ScrollState.ScrollingForward;
                                    _scrollTimer = 0;
                                }
                                break;
                            case ScrollState.ScrollingForward:
                                _scrollOffset += dt * 40f; // Normal scroll speed
                                if (_scrollOffset >= maxScroll) {
                                    _scrollOffset = maxScroll;
                                    _scrollState = ScrollState.WaitingAtEnd;
                                    _scrollTimer = 0;
                                }
                                break;
                            case ScrollState.WaitingAtEnd:
                                _scrollTimer += dt;
                                if (_scrollTimer > 1.0f) {
                                    _scrollState = ScrollState.Returning;
                                    _scrollTimer = 0;
                                }
                                break;
                            case ScrollState.Returning:
                                // Fast return to start (approx 0.5s or less)
                                float returnSpeed = Math.Max(200f, maxScroll / 0.4f);
                                _scrollOffset -= dt * returnSpeed;
                                if (_scrollOffset <= 0) {
                                    _scrollOffset = 0;
                                    _scrollState = ScrollState.WaitingAtStart;
                                    _scrollTimer = 0;
                                }
                                break;
                        }
                    } else {
                        // Smoothly return to start if mouse leaves
                        if (_scrollOffset > 0) {
                            _scrollOffset = Math.Max(0, _scrollOffset - dt * 400f);
                        }
                        _scrollTimer = 0;
                        _scrollState = ScrollState.WaitingAtStart;
                    }
                } else {
                    _scrollOffset = 0;
                    _scrollTimer = 0;
                    _scrollState = ScrollState.WaitingAtStart;
                }
            }
        }
    }

    protected override void OnClick() {
        TheGame.Core.OS.Shell.Audio.PlaySound("C:\\Windows\\Media\\click.wav", 0.5f);
        try {
            OnClickAction?.Invoke();
        } catch (Exception ex) {
            var process = GetOwnerProcess();
            if (process != null && TheGame.Core.OS.CrashHandler.IsAppException(ex, process)) {
                TheGame.Core.OS.CrashHandler.HandleAppException(process, ex);
            } else {
                throw;
            }
        }
        base.OnClick();
    }

    protected override void OnHover() {
        CustomCursor.Instance.SetCursor(CursorType.Link);
        base.OnHover();
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;

        // Scale logic
        Vector2 size = Size * Scale;
        Vector2 offset = (Size - size) / 2f;
        Vector2 drawPos = absPos + offset;

        // Background
        batch.FillRectangle(drawPos, size, CurrentBackgroundColor * AbsoluteOpacity, rounded: 3f);

        // Border
        batch.BorderRectangle(drawPos, size, BorderColor * AbsoluteOpacity, thickness: 1f, rounded: 3f);

        float pX = Padding.X * Scale;
        float pY = Padding.Y * Scale;
        float iconSize = 0f;

        // Draw Icon if present
        if (Icon != null) {
            iconSize = size.Y - (pY * 2);
            var iconPos = new Vector2(drawPos.X + pX, drawPos.Y + pY);
            float scale = iconSize / Icon.Width;
            batch.DrawTexture(Icon, iconPos, Color.White * AbsoluteOpacity, scale);
        }

        // Text (Centering logic with Scrolling/Truncation)
        if (!string.IsNullOrEmpty(Text) && GameContent.FontSystem != null) {
            var font = GameContent.FontSystem.GetFont((int)(FontSize * Scale));
            if (font != null) {
                float contentStartX = drawPos.X + pX + iconSize + (iconSize > 0 ? pX : 0);
                float remainingWidth = size.X - (contentStartX - drawPos.X) - pX;

                if (remainingWidth > 5) {
                    // We only use the fancy scissored scrolling if hovered AND it's actually overflowing
                    if (_isTextOverflowing && IsMouseOver) {
                        // DRAW SCROLLING TEXT WITH SCISSOR
                        batch.End();
                        spriteBatch.End();

                        // Capture current state to restore later
                        var oldScissor = G.GraphicsDevice.ScissorRectangle;
                        var oldRasterizer = G.GraphicsDevice.RasterizerState;

                        var viewport = G.GraphicsDevice.Viewport;
                        var scissorRect = new Rectangle((int)contentStartX, (int)drawPos.Y, (int)remainingWidth, (int)size.Y);
                        
                        // Intersect with screen and parent scissor (if parent has one active)
                        scissorRect = Rectangle.Intersect(scissorRect, viewport.Bounds);
                        if (oldRasterizer.ScissorTestEnable) {
                            scissorRect = Rectangle.Intersect(scissorRect, oldScissor);
                        }

                        if (scissorRect.Width > 0 && scissorRect.Height > 0) {
                            G.GraphicsDevice.ScissorRectangle = scissorRect;
                            
                            // 1. Create a state that enables scissoring
                            var scissorState = new RasterizerState { 
                                ScissorTestEnable = true, 
                                CullMode = CullMode.None,
                                FillMode = oldRasterizer.FillMode,
                                DepthBias = oldRasterizer.DepthBias,
                                MultiSampleAntiAlias = oldRasterizer.MultiSampleAntiAlias,
                                SlopeScaleDepthBias = oldRasterizer.SlopeScaleDepthBias
                            };

                            // 2. Draw scissored text (MUST pass state to Begin)
                            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, scissorState);
                            
                            Vector2 textPos = new Vector2(contentStartX - _scrollOffset, drawPos.Y + (size.Y - font.MeasureString(Text).Y) / 2f);
                            font.DrawText(spriteBatch, Text, textPos, TextColor * AbsoluteOpacity);

                            spriteBatch.End();
                        }

                        // 3. Restore parent state
                        G.GraphicsDevice.ScissorRectangle = oldScissor;
                        // G.GraphicsDevice.RasterizerState is automatically restored by the next batch.Begin calls if we pass it correctly

                        // 4. Restart original batches with parent's RasterizerState
                        batch.Begin();
                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, oldRasterizer);
                    } else {
                        // DRAW NORMAL (Truncated)
                        string textToDraw = TextHelper.TruncateWithEllipsis(font, Text, remainingWidth);
                        var textSize = font.MeasureString(textToDraw);

                        Vector2 textPos;
                        if (TextAlign == TextAlign.Left) {
                             textPos = new Vector2(contentStartX, drawPos.Y + (size.Y - textSize.Y) / 2f);
                        } else if (TextAlign == TextAlign.Right) {
                             textPos = new Vector2(contentStartX + remainingWidth - textSize.X, drawPos.Y + (size.Y - textSize.Y) / 2f);
                        } else {
                             textPos = new Vector2(
                                contentStartX + (remainingWidth - textSize.X) / 2f,
                                drawPos.Y + (size.Y - textSize.Y) / 2f
                            );
                        }

                        font.DrawText(batch, textToDraw, textPos, TextColor * AbsoluteOpacity);
                    }
                }
            }
        }
    }
}
