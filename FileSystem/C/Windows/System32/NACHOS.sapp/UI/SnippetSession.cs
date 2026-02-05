using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TheGame.Core.Input;
using TheGame.Graphics;

namespace NACHOS;

public class SnippetSession {
    public class Marker {
        public int Index;
        public int Line;
        public int StartCol;
        public int EndCol;
        public string Value;
        public bool IsActive => EndCol >= StartCol;
        public int Length => EndCol - StartCol;
    }

    private CodeEditor _editor;
    private List<Marker> _markers = new();
    private int _activeIndex = 1;
    private bool _isSynchronizing = false;

    internal List<Marker> MarkersInternal => _markers;

    public SnippetSession(CodeEditor editor, List<Marker> markers) {
        _editor = editor;
        _markers = markers;
        
        // Find first valid index (usually 1)
        var indices = _markers.Select(m => m.Index).Where(i => i > 0).Distinct().OrderBy(i => i).ToList();
        if (indices.Count > 0) _activeIndex = indices[0];
        else if (_markers.Any(m => m.Index == 0)) _activeIndex = 0;
        
        JumpToActive();
    }

    public void JumpNext() {
        var indices = _markers.Select(m => m.Index).Where(i => i > 0).Distinct().OrderBy(i => i).ToList();
        int currentPos = indices.IndexOf(_activeIndex);
        
        if (currentPos != -1 && currentPos < indices.Count - 1) {
            _activeIndex = indices[currentPos + 1];
            JumpToActive();
        } else if (_markers.Any(m => m.Index == 0)) {
            _activeIndex = 0;
            JumpToActive();
            EndSession();
        } else {
            EndSession();
        }
    }

    public void JumpBack() {
        var indices = _markers.Select(m => m.Index).Where(i => i > 0).Distinct().OrderBy(i => i).ToList();
        int currentPos = indices.IndexOf(_activeIndex);
        
        if (currentPos > 0) {
            _activeIndex = indices[currentPos - 1];
            JumpToActive();
        }
    }

    private void JumpToActive() {
        // Always pick the first marker of this index for the cursor/selection
        var marker = _markers.OrderBy(m => m.Line).ThenBy(m => m.StartCol).FirstOrDefault(m => m.Index == _activeIndex);
        if (marker != null) {
            _editor.SetCursor(marker.Line, marker.StartCol);
            _editor.SetSelection(marker.Line, marker.StartCol, marker.Line, marker.EndCol);
        }
    }

    public bool IsEnded { get; private set; }

    public void EndSession() {
        IsEnded = true;
        _editor.ActiveSnippetSession = null;
    }

    public void HandleInput() {
        if (InputManager.IsKeyJustPressed(Keys.Tab)) {
            bool shift = InputManager.IsKeyDown(Keys.LeftShift) || InputManager.IsKeyDown(Keys.RightShift);
            if (shift) JumpBack();
            else JumpNext();
            InputManager.IsKeyboardConsumed = true;
        } else if (InputManager.IsKeyJustPressed(Keys.Escape)) {
            EndSession();
            InputManager.IsKeyboardConsumed = true;
        } else if (InputManager.IsKeyJustPressed(Keys.Enter)) {
            InputManager.IsKeyboardConsumed = true;
            
            // If we are at the last field or if there is no $0, Enter can end session
            var indices = _markers.Select(m => m.Index).Where(i => i > 0).Distinct().OrderBy(i => i).ToList();
            if (_activeIndex == 0 || (indices.Count > 0 && _activeIndex == indices.Last())) {
                 _activeIndex = 0;
                 JumpToActive();
                 EndSession();
            } else {
                 JumpNext();
            }
        }
    }

    public void SyncMarkers(int index) {
        if (_isSynchronizing) return;
        
        var group = _markers.Where(m => m.Index == index).ToList();
        if (group.Count <= 1) return;

        // Use the active marker as the source for the current value
        var primary = _markers.FirstOrDefault(m => m.Index == index && m.Index == _activeIndex);
        if (primary == null) primary = group[0];

        if (primary.Line < 0 || primary.Line >= _editor.Lines.Count) return;
        string currentLine = _editor.Lines[primary.Line];
        if (primary.StartCol < 0 || primary.EndCol > currentLine.Length || primary.StartCol > primary.EndCol) return;
        
        string val = currentLine.Substring(primary.StartCol, primary.EndCol - primary.StartCol);

        _isSynchronizing = true;
        try {
            foreach (var m in group) {
                if (m == primary) continue;

                if (m.Line < 0 || m.Line >= _editor.Lines.Count) continue;
                string mLine = _editor.Lines[m.Line];
                if (m.StartCol < 0 || m.EndCol > mLine.Length || m.StartCol > m.EndCol) continue;
                
                string oldVal = mLine.Substring(m.StartCol, m.EndCol - m.StartCol);
                
                if (oldVal != val) {
                    _editor.ModifyLine(m.Line, mLine.Remove(m.StartCol, oldVal.Length).Insert(m.StartCol, val));
                    int diff = val.Length - oldVal.Length;
                    m.EndCol += diff;
                    
                    // Shift all subsequent markers on THE SAME LINE
                    foreach (var other in _markers.Where(o => o.Line == m.Line && o.StartCol > m.StartCol && o != m)) {
                        other.StartCol += diff;
                        other.EndCol += diff;
                    }
                }
            }
        } finally {
            _isSynchronizing = false;
        }
    }

    public void UpdateMarkers(int line, int col, int deltaSnippet, int deltaLines = 0) {
        if (_isSynchronizing) return;

        // changePos is the logical position where the edit occurred.
        int changePos = col - Math.Max(0, deltaSnippet);

        foreach (var m in _markers) {
            if (m.Line > line) {
                // Markers strictly below the edit line just shift down/up
                m.Line += deltaLines;
            }
            else if (m.Line == line) {
                // If we inserted lines at this line, anything after THE CURSOR moves to a new line
                if (deltaLines != 0 && m.StartCol >= changePos) {
                    m.Line += deltaLines;
                    // For multi-line, the text after the cursor is usually moved to the end of the LAST line.
                    // This is complex to track perfectly without knowing the EXACT insertion body,
                    // but we can estimate or improve based on common snippet behavior.
                    // In CodeEditor.InsertSnippet, suffix is appended to indent + lastLine.
                    continue; 
                }

                // Determine if we should Shift, Grow, or Move this marker
                if (m.StartCol > changePos) {
                    // Shift: edit happened BEFORE this marker on the same line
                    m.StartCol += deltaSnippet;
                    m.EndCol += deltaSnippet;
                }
                else if (changePos > m.StartCol && changePos < m.EndCol) {
                    // Grow: edit happened INSIDE the marker
                    m.EndCol += deltaSnippet;
                }
                else if (changePos == m.EndCol) {
                    // Typing at the very END of the marker: grow it
                    m.EndCol += deltaSnippet;
                }
                else if (changePos == m.StartCol) {
                    if (deltaSnippet > 0) {
                        // Grow/Shift at start (decide based on active)
                        if (m.Index == _activeIndex) m.EndCol += deltaSnippet;
                        else { m.StartCol += deltaSnippet; m.EndCol += deltaSnippet; }
                    } else if (deltaSnippet < 0) {
                        // Shrink: deletion at start
                        m.EndCol += deltaSnippet;
                    }
                }
            }
        }

        // Now sync if we changed the active field
        // We check if the cursor is anywhere within the active marker's NEW range
        // For replacements (deltaSnippet == 0), this is essential on the first char.
        var am = _markers.FirstOrDefault(m => m.Index == _activeIndex && m.Line == line && col >= m.StartCol && col <= m.EndCol);
        if (am != null) {
            SyncMarkers(am.Index);
        }
    }

    public void Draw(SpriteBatch sb, ShapeBatch batch, Func<int, int, Vector2> posFunc) {
        foreach (var m in _markers) {
            if (m.Index == 0) continue;
            
            Vector2 start = posFunc(m.Line, m.StartCol);
            Vector2 end = posFunc(m.Line, m.EndCol);
            
            if (start == Vector2.Zero || end == Vector2.Zero) continue;

            // Background highlight - use slightly more vibrant colors
            Color color = m.Index == _activeIndex ? new Color(0, 150, 255) : Color.LightGray * 0.5f;
            float alpha = m.Index == _activeIndex ? 0.6f : 0.3f;
            
            Vector2 rectStart = start - new Vector2(2, 0);
            Vector2 rectSize = new Vector2(Math.Max(4, end.X - start.X + 4), 18);
            
            // Fill background
            batch.FillRectangle(rectStart, rectSize, color * alpha, rounded: 2f);
            
            // Draw border or underline
            if (m.Index == _activeIndex) {
                batch.BorderRectangle(rectStart, rectSize, color, 1.5f, rounded: 2f);
            } else {
                batch.FillLine(new Vector2(start.X, start.Y + 18), new Vector2(end.X, end.Y + 18), 1f, color * 0.5f);
            }
        }
    }
}
