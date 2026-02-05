using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.UI;
using TheGame.Core.Input;
using TheGame.Graphics;
using TheGame.Core.OS;
using TheGame.Core.UI.Controls;
using System.Linq;

namespace NACHOS;

public record struct CompletionItem(string Label, string Detail, string Kind, int Score = 0, bool IsPreferred = false);

public class CompletionPopup : UIElement {
    private List<CompletionItem> _allFilesItems = new();
    private List<CompletionItem> _filteredItems = new();
    private int _selectedIndex = 0;
    public Action OnClosed;
    private float _itemHeight = 22f;
    private Action<CompletionItem> _onSelect;
    private ScrollPanel _scrollPanel;
    private Panel _listPanel;
    public int VisibleItemsCount => _filteredItems.Count;
    private string _searchQuery = "";
    public string SearchQuery {
        get => _searchQuery;
        set {
            if (_searchQuery != value) {
                _searchQuery = value;
                FilterItems();
            }
        }
    }

    public CompletionPopup(Vector2 position, List<CompletionItem> items, Action<CompletionItem> onSelect, string initialSearch = "") {
        Position = position;
        _allFilesItems = items;
        _searchQuery = initialSearch ?? "";
        
        _onSelect = onSelect;
        ConsumesInput = true;
        
        FilterItems();

        float width = 450;
        float height = Math.Min(300, 10 * _itemHeight + 4);
        Size = new Vector2(width, height);

        _scrollPanel = new ScrollPanel(Vector2.Zero, Size) {
            BackgroundColor = new Color(30, 30, 30),
            BorderColor = Color.Gray * 0.5f,
            ConsumesInput = true
        };
        AddChild(_scrollPanel);

        _listPanel = new Panel(Vector2.Zero, new Vector2(width - 15, _filteredItems.Count * _itemHeight));
        _listPanel.BackgroundColor = Color.Transparent;
        _scrollPanel.AddChild(_listPanel);
        
        UpdateList();
    }

    private void UpdateList() {
        _listPanel.ClearChildren();
        
        float listHeight = _filteredItems.Count * _itemHeight;
        _listPanel.Size = new Vector2(_listPanel.Size.X, listHeight);

        // Update overall popup size based on item count
        float newHeight = listHeight > 0 ? Math.Min(300, listHeight + 4) : 0;
        Size = new Vector2(Size.X, newHeight);
        _scrollPanel.Size = Size;

        for (int i = 0; i < _filteredItems.Count; i++) {
            var item = _filteredItems[i];
            int idx = i;
            
            var btn = new Button(new Vector2(0, i * _itemHeight), new Vector2(_listPanel.Size.X, _itemHeight), "") {
                BackgroundColor = idx == _selectedIndex ? new Color(0, 120, 215) * 0.8f : Color.Transparent,
                BorderColor = Color.Transparent,
                OnClickAction = () => _onSelect?.Invoke(item),
                OnDrawOver = (sb, shape, pos, size) => {
                    var iconColor = GetKindColor(item.Kind);
                    var font = TheGame.GameContent.FontSystem.GetFont(14);
                    
                    // Draw Icon [X]
                    string iconPrefix = $"[{item.Kind}]";
                    font.DrawText(sb, iconPrefix, pos + new Vector2(5, 3), iconColor);
                    
                    // Draw Label
                    font.DrawText(sb, item.Label, pos + new Vector2(40, 3), Color.White);

                    // Draw Detail (Type/Signature)
                    float labelWidth = font.MeasureString(item.Label).X;
                    font.DrawText(sb, item.Detail, pos + new Vector2(45 + labelWidth, 3), Color.Gray * 0.7f);
                }
            };

            _listPanel.AddChild(btn);
        }
    }

    private Color GetKindColor(string kind) {
        return kind switch {
            "M" => new Color(180, 100, 255), // Method - Purple
            "P" => new Color(100, 180, 255), // Property - Blue
            "F" => new Color(100, 255, 255), // Field - Cyan
            "C" => new Color(255, 180, 100), // Class - Orange
            "S" => new Color(255, 255, 100), // Struct - Yellow
            "I" => new Color(100, 255, 150), // Interface - Green
            "E" => new Color(255, 220, 100), // Enum - Dark Yellow
            "T" => new Color(0, 200, 200),   // Primitive - Teal
            "N" => new Color(200, 200, 200), // Namespace - Gray
            _ => Color.Gray
        };
    }

    public override void Update(GameTime gameTime) {
        if (InputManager.IsKeyboardConsumed) return;
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        bool up = InputManager.IsKeyRepeated(Microsoft.Xna.Framework.Input.Keys.Up);
        bool down = InputManager.IsKeyRepeated(Microsoft.Xna.Framework.Input.Keys.Down);
        bool enter = InputManager.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Enter);
        bool tab = InputManager.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Tab);
        bool escape = InputManager.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Escape);

        if (up) {
            if (_filteredItems.Count > 0) {
                _selectedIndex = (_selectedIndex - 1 + _filteredItems.Count) % _filteredItems.Count;
                UpdateList();
                EnsureVisible(_selectedIndex);
            }
        } else if (down) {
            if (_filteredItems.Count > 0) {
                _selectedIndex = (_selectedIndex + 1) % _filteredItems.Count;
                UpdateList();
                EnsureVisible(_selectedIndex);
            }
        } else if (enter || tab) {
            // Snippet priority: if there is an active snippet session, it should handle Enter/Tab
            if (UIManager.FocusedElement is CodeEditor ed && ed.ActiveSnippetSession != null) {
                // Return and let MainWindow/SnippetSession handle it
                return;
            }

            if (_filteredItems.Count > 0) {
                var selectedItem = _filteredItems[_selectedIndex];
                UsageTracker.RecordSelection(selectedItem.Label);
                _onSelect?.Invoke(selectedItem);
            }
        } else if (escape) {
            OnClosed?.Invoke();
            if (Parent != null) Parent.RemoveChild(this);
            Shell.RemoveOverlayElement(this);
        }

        // Always consume if these keys are held, so they don't leak to the editor
        if (InputManager.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Up) || 
            InputManager.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Down) || 
            InputManager.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Enter) || 
            InputManager.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Tab) || 
            InputManager.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape)) {
            InputManager.IsKeyboardConsumed = true;
        }

        // Close on click outside
        if (InputManager.IsAnyMouseButtonJustPressed(MouseButton.Left) && !Bounds.Contains(InputManager.MousePosition)) {
            OnClosed?.Invoke();
            Parent?.RemoveChild(this);
        }
    }

    private void EnsureVisible(int index) {
        if (_scrollPanel == null) return;
        float itemTop = index * _itemHeight;
        float itemBottom = itemTop + _itemHeight;
        
        float viewportTop = -_scrollPanel.TargetScrollY;
        float viewportBottom = viewportTop + _scrollPanel.Size.Y;
        
        if (itemBottom > viewportBottom) {
            _scrollPanel.TargetScrollY = -(itemBottom - _scrollPanel.Size.Y);
        } else if (itemTop < viewportTop) {
            _scrollPanel.TargetScrollY = -itemTop;
        }
    }

    private void FilterItems() {
        if (!string.IsNullOrEmpty(_searchQuery)) {
            _filteredItems = _allFilesItems
                .Where(i => {
                    // Basic contains check
                    if (i.Label.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)) return true;
                    
                    // Fuzzy matching: uppercase acronym (MNGM → MyNewGoodMethod)
                    if (_searchQuery.Length >= 2 && _searchQuery.All(char.IsUpper)) {
                        string initials = string.Concat(i.Label.Where(char.IsUpper));
                        if (string.Equals(_searchQuery, initials, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                    
                    // Fuzzy matching: mixed acronym (sb → SpriteBatch)
                    if (_searchQuery.Length >= 2) {
                        string initials = string.Concat(i.Label.Where(char.IsUpper));
                        if (string.Equals(_searchQuery, initials, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                    
                    return false;
                })
                .OrderByDescending(i => {
                    // Calculate match score for sorting
                    int matchScore = 0;
                    
                    // Uppercase acronym: +200
                    if (_searchQuery.Length >= 2 && _searchQuery.All(char.IsUpper)) {
                        string initials = string.Concat(i.Label.Where(char.IsUpper));
                        if (string.Equals(_searchQuery, initials, StringComparison.OrdinalIgnoreCase)) {
                            matchScore += 200;
                        }
                    }
                    
                    // Mixed acronym: +50
                    if (_searchQuery.Length >= 2 && matchScore == 0) {
                        string initials = string.Concat(i.Label.Where(char.IsUpper));
                        if (string.Equals(_searchQuery, initials, StringComparison.OrdinalIgnoreCase)) {
                            matchScore += 50;
                        }
                    }
                    
                    // Prefix match (case-sensitive): +1000
                    if (i.Label.StartsWith(_searchQuery, StringComparison.Ordinal)) matchScore += 1000;
                    
                    // Prefix match (case-insensitive): +500
                    else if (i.Label.StartsWith(_searchQuery, StringComparison.OrdinalIgnoreCase)) matchScore += 500;
                    
                    // Exact match: +2000
                    if (i.Label.Equals(_searchQuery, StringComparison.Ordinal)) matchScore += 2000;
                    else if (i.Label.Equals(_searchQuery, StringComparison.OrdinalIgnoreCase)) matchScore += 1000;
                    
                    // Contains (case-sensitive): +100
                    if (i.Label.Contains(_searchQuery, StringComparison.Ordinal)) matchScore += 100;
                    
                    return i.Score + matchScore;
                })
                .ThenByDescending(i => i.IsPreferred)
                .ThenBy(i => i.Label)
                .ToList();
        } else {
            _filteredItems = _allFilesItems.OrderByDescending(i => i.Score).ThenByDescending(i => i.IsPreferred).ThenBy(i => i.Label).ToList();
        }

        _selectedIndex = 0;
        
        if (_filteredItems.Count == 0) {
            IsVisible = false;
            OnClosed?.Invoke();
            if (Parent != null) Parent.RemoveChild(this);
            Shell.RemoveOverlayElement(this);
        } else {
            IsVisible = true;
            // Truncate to avoid massive UI lag if no search query is active
            if (string.IsNullOrEmpty(_searchQuery) && _filteredItems.Count > 300) {
                _filteredItems = _filteredItems.Take(300).ToList();
            }
            if (_listPanel != null) {
                UpdateList();
            }
        }
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) { }
}
