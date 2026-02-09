using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;
using TheGame;

namespace NACHOS;

public class Gutter : UIControl {
    private CodeEditor _editor;
    public float Width { get; private set; } = 40;

    public Gutter(CodeEditor editor) : base(Vector2.Zero, Vector2.Zero) {
        _editor = editor;
        BackgroundColor = new Color(40, 40, 40);
        ConsumesInput = false; // Let input pass through or handle specifically
    }

    public void UpdateWidth(int maxLine) {
        if (GameContent.FontSystem == null) return;
        var font = GameContent.FontSystem.GetFont(_editor.FontSize);
        string maxStr = maxLine.ToString();
        Width = font.MeasureString(maxStr).X + 20;
        Size = new Vector2(Width, _editor.Size.Y);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;
        batch.FillRectangle(absPos, Size, BackgroundColor * AbsoluteOpacity);
        
        // Draw vertical separator
        var color = new Color(60, 60, 60) * AbsoluteOpacity;
        batch.DrawLine(new Vector2(absPos.X + Size.X - 1, absPos.Y), new Vector2(absPos.X + Size.X - 1, absPos.Y + Size.Y), 0.5f, color, color);

        if (GameContent.FontSystem == null) return;
        var font = GameContent.FontSystem.GetFont(_editor.FontSize);
        float lineHeight = font.LineHeight;

        // Draw line numbers
        // We need to access _visualLines and scroll offset from the editor
        // Since we are in the same namespace or have access to protected members if we were inheriting, 
        // but here we are a separate control. We'll use public properties if available.
        
        float textY = absPos.Y + 10 - _editor.ScrollOffset;
        
        var visualLines = _editor.VisualLines;
        int lastLogicalLine = -1;

        for (int i = 0; i < visualLines.Count; i++) {
            var vl = visualLines[i];
            float y = textY + i * lineHeight;
            
            // Skip if not visible
            if (y + lineHeight < absPos.Y || y > absPos.Y + Size.Y) continue;

            // Only draw number for the first visual line of a logical line
            if (vl.LogicalLineIndex != lastLogicalLine) {
                string numStr = (vl.LogicalLineIndex + 1).ToString();
                float numWidth = font.MeasureString(numStr).X;
                
                Color lineNumColor = (vl.LogicalLineIndex == _editor.CursorLine) ? Color.White : Color.Gray;
                
                font.DrawText(batch, numStr, new Vector2(absPos.X + Size.X - 10 - numWidth, y), lineNumColor * AbsoluteOpacity);
                lastLogicalLine = vl.LogicalLineIndex;
            }
        }
    }
}
