using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheGame.Core.OS;
using TheGame.Core.UI.Controls;
using TheGame.Core.Input;
using TheGame;
using TheGame.Graphics;

namespace NACHOS;

public class CodeEditor : TextArea {
    private Gutter _gutter;
    private bool _isDirty = false;
    private string _filePath;

    public bool IsDirty => _isDirty;
    public string FilePath => _filePath;
    public string FileName => string.IsNullOrEmpty(_filePath) ? "Untitled" : Path.GetFileName(_filePath);

    // Expose necessary protected members from TextArea via public properties if needed, 
    // or just use them directly if inheriting in the same assembly. 
    // Since this is a .sapp app compiled at runtime, it might not have access to protected members 
    // if it's considered a different assembly. 
    // WAIT, sapp apps ARE compiled into separate assemblies. 
    // I need to check if TextArea's fields are internal or protected.
    // I saw they are protected. Protected members are accessible to derived classes even in different assemblies.

    public IReadOnlyList<VisualLine> VisualLines => _visualLines;
    public float ScrollOffset => _scrollOffset;
    public int CursorLine => _cursorLine;

    public Action OnDirtyChanged;

    public CodeEditor(Vector2 position, Vector2 size, string filePath = null) : base(position, size) {
        _filePath = filePath;
        _gutter = new Gutter(this);
        AddChild(_gutter);
        
        BackgroundColor = new Color(30, 30, 30);
        TextColor = new Color(220, 220, 220);
        FontSize = 14;
        
        UpdateLayout();
        
        if (!string.IsNullOrEmpty(_filePath) && VirtualFileSystem.Instance.Exists(_filePath)) {
            Value = VirtualFileSystem.Instance.ReadAllText(_filePath);
            _isDirty = false;
        }

        OnValueChanged += (val) => {
            if (!_isDirty) {
                _isDirty = true;
                OnDirtyChanged?.Invoke();
            }
        };
    }

    private void UpdateLayout() {
        _gutter.UpdateWidth(_lines.Count);
        _gutter.Position = Vector2.Zero;
        _gutter.Size = new Vector2(_gutter.Width, Size.Y);
        
        // TextArea doesn't have a "Padding" or "Margin" for text area easily.
        // I might need to override DrawSelf to offset the text.
    }

    protected override void NotifyUserChanged() {
        base.NotifyUserChanged();
        _gutter.UpdateWidth(_lines.Count);
    }

    public void Save() {
        if (string.IsNullOrEmpty(_filePath)) return;
        VirtualFileSystem.Instance.WriteAllText(_filePath, Value);
        _isDirty = false;
        OnDirtyChanged?.Invoke();
    }

    public void SaveAs(string path) {
        _filePath = path;
        Save();
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        // We override DrawSelf to shift the text to the right of the gutter
        var absPos = AbsolutePosition;

        // Background
        if (DrawBackground) {
            batch.FillRectangle(absPos, Size, BackgroundColor * AbsoluteOpacity, rounded: 3f);
            batch.BorderRectangle(absPos, Size, (IsFocused ? FocusedBorderColor : BorderColor) * AbsoluteOpacity, thickness: 1f, rounded: 3f);
        }

        if (GameContent.FontSystem == null) return;
        var font = GameContent.FontSystem.GetFont(FontSize);
        float lineHeight = font.LineHeight;

        // Offset for gutter
        float gutterWidth = _gutter.Width;
        
        // Clip region for text
        var oldScissor = G.GraphicsDevice.ScissorRectangle;
        int scX = (int)Math.Floor(absPos.X + gutterWidth + 2);
        int scY = (int)Math.Floor(absPos.Y + 2);
        int scW = (int)Math.Ceiling(Size.X - gutterWidth - 4);
        int scH = (int)Math.Ceiling(Size.Y - 4);
        var scissor = new Rectangle(scX, scY, scW, scH);
        scissor = Rectangle.Intersect(oldScissor, scissor);
        
        if (scissor.Width <= 0 || scissor.Height <= 0) {
            // Still draw children (the gutter)
            foreach (var child in Children) {
                child.Draw(spriteBatch, batch);
            }
            return;
        }

        // Draw children first (including gutter)
        foreach (var child in Children) {
            child.Draw(spriteBatch, batch);
        }

        batch.End();
        spriteBatch.End();
        
        var oldState = G.GraphicsDevice.ScissorRectangle;
        G.GraphicsDevice.ScissorRectangle = scissor;
        var rasterizerState = _scissorRasterizer;
        G.GraphicsDevice.RasterizerState = rasterizerState;
        
        batch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, rasterizerState);

        float textX = absPos.X + gutterWidth + 5 - _scrollOffsetX;
        float textY = absPos.Y + 10 - _scrollOffset;

        // Draw selection
        if (HasSelection()) {
            GetSelectionRange(out int sl, out int sc, out int el, out int ec);
            for (int i = 0; i < _visualLines.Count; i++) {
                var vl = _visualLines[i];
                float y = textY + i * lineHeight;
                if (y + lineHeight < absPos.Y || y > absPos.Y + Size.Y) continue;
                int visualLogicalLine = vl.LogicalLineIndex;
                if (visualLogicalLine < sl || visualLogicalLine > el) continue;
                int startC = (visualLogicalLine == sl) ? Math.Max(0, sc - vl.StartIndex) : 0;
                int endC = (visualLogicalLine == el) ? Math.Min(vl.Length, ec - vl.StartIndex) : vl.Length;
                if (startC >= endC && visualLogicalLine == sl && visualLogicalLine == el) continue;
                if (startC >= vl.Length || endC <= 0) continue;
                string visualText = _lines[vl.LogicalLineIndex].Substring(vl.StartIndex, vl.Length);
                float x1 = startC <= 0 ? 0 : font.MeasureString(visualText.Substring(0, startC)).X;
                float x2 = endC >= vl.Length ? font.MeasureString(visualText).X : font.MeasureString(visualText.Substring(0, endC)).X;
                batch.FillRectangle(new Vector2(textX + x1, y), new Vector2(x2 - x1, lineHeight), FocusedBorderColor * 0.3f * AbsoluteOpacity);
            }
        }

        // Draw lines
        int firstVis = (int)Math.Floor(_scrollOffset / lineHeight);
        int lastVis = (int)Math.Ceiling((_scrollOffset + Size.Y) / lineHeight);
        int maxVis = Math.Max(0, _visualLines.Count - 1);
        firstVis = Math.Clamp(firstVis, 0, maxVis);
        lastVis = Math.Clamp(lastVis, 0, maxVis);

        for (int i = firstVis; i <= lastVis; i++) {
            var vl = _visualLines[i];
            float y = textY + i * lineHeight;
            string lineText = _lines[vl.LogicalLineIndex];
            string visualPart = lineText.Substring(vl.StartIndex, vl.Length);
            if (!string.IsNullOrEmpty(visualPart)) {
                font.DrawText(batch, visualPart, new Vector2(textX, y), TextColor * AbsoluteOpacity);
            }
        }

        // Cursor
        if (_showCursor && IsFocused) {
            int visualIdx = GetVisualLineIndex(_cursorLine, _cursorCol);
            if (visualIdx >= 0 && visualIdx < _visualLines.Count) {
                var vl = _visualLines[visualIdx];
                string lineText = _lines[vl.LogicalLineIndex];
                int start = Math.Clamp(vl.StartIndex, 0, lineText.Length);
                int col = Math.Clamp(_cursorCol, start, start + vl.Length);
                string visualPart = lineText.Substring(start, col - start);
                float cursorX = font.MeasureString(visualPart).X;
                float cursorY = textY + visualIdx * lineHeight;
                batch.FillRectangle(new Vector2(textX + cursorX, cursorY), new Vector2(2, lineHeight), FocusedBorderColor * AbsoluteOpacity);
            }
        }
        
        batch.End();
        spriteBatch.End();

        G.GraphicsDevice.ScissorRectangle = oldState;
        batch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
    }

    // Override SetCursorFromMouse to account for gutter
    protected override void SetCursorFromMouse() {
        if (GameContent.FontSystem == null || _visualLines.Count == 0) return;
        var font = GameContent.FontSystem.GetFont(FontSize);
        float lineHeight = font.LineHeight;

        Vector2 local = InputManager.MousePosition.ToVector2() - AbsolutePosition - new Vector2(_gutter.Width + 5, 5);
        local.Y += _scrollOffset;
        local.X += _scrollOffsetX;

        int visualIdx = (int)(local.Y / lineHeight);
        visualIdx = Math.Clamp(visualIdx, 0, _visualLines.Count - 1);

        var vl = _visualLines[visualIdx];
        string lineText = _lines[vl.LogicalLineIndex];
        int start = Math.Clamp(vl.StartIndex, 0, lineText.Length);
        int length = Math.Clamp(vl.Length, 0, lineText.Length - start);
        string visualPart = lineText.Substring(start, length);

        int colInVisual = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i <= visualPart.Length; i++) {
            float w = i == 0 ? 0 : font.MeasureString(visualPart.Substring(0, i)).X;
            float dist = Math.Abs(local.X - w);
            if (dist < bestDist) { bestDist = dist; colInVisual = i; }
        }

        _cursorLine = vl.LogicalLineIndex;
        _cursorCol = vl.StartIndex + colInVisual;
    }

    public override Vector2? GetCaretPosition() {
         var basePos = base.GetCaretPosition();
         if (basePos == null) return null;
         return basePos.Value + new Vector2(_gutter.Width, 0);
    }
}
