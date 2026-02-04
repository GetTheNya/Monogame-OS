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
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (InputManager.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Up)) {
            if (_filteredItems.Count > 0) {
                _selectedIndex = (_selectedIndex - 1 + _filteredItems.Count) % _filteredItems.Count;
                UpdateList();
            }
            InputManager.IsKeyboardConsumed = true;
        } else if (InputManager.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Down)) {
            if (_filteredItems.Count > 0) {
                _selectedIndex = (_selectedIndex + 1) % _filteredItems.Count;
                UpdateList();
            }
            InputManager.IsKeyboardConsumed = true;
        } else if (InputManager.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Enter) || InputManager.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Tab)) {
            if (_filteredItems.Count > 0) {
                _onSelect?.Invoke(_filteredItems[_selectedIndex]);
            }
            InputManager.IsKeyboardConsumed = true;
        } else if (InputManager.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Escape)) {
            OnClosed?.Invoke();
            Parent?.RemoveChild(this);
            InputManager.IsKeyboardConsumed = true;
        }

        // Close on click outside
        if (InputManager.IsAnyMouseButtonJustPressed(MouseButton.Left) && !Bounds.Contains(InputManager.MousePosition)) {
            OnClosed?.Invoke();
            Parent?.RemoveChild(this);
        }
    }

    private void FilterItems() {
        if (!string.IsNullOrEmpty(_searchQuery)) {
            _filteredItems = _allFilesItems
                .Where(i => i.Label.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(i => i.Score)
                .ThenByDescending(i => i.IsPreferred)
                .ThenByDescending(i => i.Label.Equals(_searchQuery, StringComparison.Ordinal))
                .ThenByDescending(i => i.Label.StartsWith(_searchQuery, StringComparison.Ordinal))
                .ThenByDescending(i => i.Label.Equals(_searchQuery, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(i => i.Label.StartsWith(_searchQuery, StringComparison.OrdinalIgnoreCase))
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
