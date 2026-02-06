using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;
using TheGame.Core.UI;
using TheGame.Core.Designer;
using TheGame.Core.OS;
using TheGame.Core.Input;

namespace NACHOS.Designer;

public class PropertyGrid : ScrollPanel {
    private UIElement _target;
    private readonly List<UIElement> _editorRows = new();
    
    public PropertyGrid(Vector2 position, Vector2 size) : base(position, size) {
        BackgroundColor = new Color(45, 45, 45);
        Padding = new Vector4(5, 5, 5, 5);
    }
    
    public void Inspect(UIElement element) {
        if (_target == element) {
            RefreshValues();
            return;
        }
        
        _target = element;
        ClearChildren();
        _editorRows.Clear();
        
        if (element == null) return;
        
        // Use reflection to get properties
        var props = element.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .Where(p => !Attribute.IsDefined(p, typeof(DesignerIgnoreProperty)))
            .OrderBy(p => p.Name);
            
        float y = 0;
        foreach (var prop in props) {
            if (ShouldSkipProperty(prop)) continue;
            
            var tooltipAttr = prop.GetCustomAttribute<DesignerTooltip>();

            var row = CreatePropertyRow(prop, element, tooltipAttr?.Description);
            row.Position = new Vector2(0, y);
            AddChild(row);
            _editorRows.Add(row);
            y += row.Size.Y + 2;
        }
    }
    
    private bool ShouldSkipProperty(PropertyInfo prop) {
        string[] skip = { "Parent", "Children", "Tag", "ActiveWindow", "OwnerProcess" };
        if (skip.Contains(prop.Name)) return true;
        if (prop.PropertyType == typeof(System.Action)) return true;
        return false;
    }
    
    private UIElement CreatePropertyRow(PropertyInfo prop, UIElement target, string tooltip) {
        var container = new PropertyRow(prop.Name, Size.X);
        
        UIElement editor = null;
        if (prop.PropertyType == typeof(string)) {
            editor = CreateStringEditor(prop, target, container);
        } else if (prop.PropertyType == typeof(float)) {
            editor = CreateFloatEditor(prop, target, container);
        } else if (prop.PropertyType == typeof(int)) {
            editor = CreateIntEditor(prop, target, container);
        } else if (prop.PropertyType == typeof(bool)) {
            editor = CreateBoolEditor(prop, target, container);
        } else if (prop.PropertyType == typeof(Vector2)) {
            editor = CreateVector2Editor(prop, target, container);
        } else if (prop.PropertyType == typeof(Color)) {
            editor = CreateColorEditor(prop, target, container);
        } else if (prop.PropertyType.IsEnum) {
            editor = CreateEnumEditor(prop, target, container);
        }
        
        if (editor != null) {
            editor.Position = new Vector2(100, 0); // Label takes first 100px
            editor.Size = new Vector2(container.Size.X - 105, 20);
            container.AddChild(editor);
            container.Editor = editor;
            container.Tooltip = tooltip;
        }
        
        return container;
    }
    
    private void RefreshValues() {
        foreach (var row in _editorRows.OfType<PropertyRow>()) {
            row.Refresh();
        }
    }

    #region Specialized Editors
    
    private UIElement CreateStringEditor(PropertyInfo prop, UIElement target, PropertyRow row) {
        var editor = new TextInput(Vector2.Zero, Vector2.Zero) { Value = prop.GetValue(target)?.ToString() ?? "" };
        editor.OnSubmit += (val) => prop.SetValue(target, val);
        editor.OnLostFocus += () => prop.SetValue(target, editor.Value);
        
        row.OnRefresh = () => {
            if (!editor.IsFocused)
                editor.Value = prop.GetValue(target)?.ToString() ?? "";
        };
        
        return editor;
    }
    
    private UIElement CreateFloatEditor(PropertyInfo prop, UIElement target, PropertyRow row) {
        var editor = new TextInput(Vector2.Zero, Vector2.Zero) { Value = prop.GetValue(target)?.ToString() ?? "0" };
        string lastValid = editor.Value;
        
        Action apply = () => {
            if (float.TryParse(editor.Value, out float val)) {
                prop.SetValue(target, val);
                lastValid = editor.Value;
                editor.BorderColor = Color.Transparent;
            } else {
                editor.Value = lastValid;
                editor.BorderColor = Color.Red;
            }
        };
        
        editor.OnSubmit += (val) => apply();
        editor.OnLostFocus += apply;

        row.OnRefresh = () => {
            if (!editor.IsFocused) {
                var current = prop.GetValue(target)?.ToString() ?? "0";
                if (editor.Value != current) {
                    editor.Value = current;
                    lastValid = current;
                }
            }
        };

        return editor;
    }

    private UIElement CreateIntEditor(PropertyInfo prop, UIElement target, PropertyRow row) {
        var editor = new TextInput(Vector2.Zero, Vector2.Zero) { Value = prop.GetValue(target)?.ToString() ?? "0" };
        string lastValid = editor.Value;
        
        Action apply = () => {
            if (int.TryParse(editor.Value, out int val)) {
                prop.SetValue(target, val);
                lastValid = editor.Value;
                editor.BorderColor = Color.Transparent;
            } else {
                editor.Value = lastValid;
                editor.BorderColor = Color.Red;
            }
        };
        
        editor.OnSubmit += (val) => apply();
        editor.OnLostFocus += apply;

        row.OnRefresh = () => {
            if (!editor.IsFocused) {
                var current = prop.GetValue(target)?.ToString() ?? "0";
                if (editor.Value != current) {
                    editor.Value = current;
                    lastValid = current;
                }
            }
        };

        return editor;
    }

    private UIElement CreateBoolEditor(PropertyInfo prop, UIElement target, PropertyRow row) {
        var editor = new Checkbox(Vector2.Zero) { Value = (bool)prop.GetValue(target) };
        editor.OnValueChanged += (val) => prop.SetValue(target, val);
        
        row.OnRefresh = () => {
            editor.Value = (bool)prop.GetValue(target);
        };
        
        return editor;
    }

    private UIElement CreateVector2Editor(PropertyInfo prop, UIElement target, PropertyRow row) {
        // Simple string-based Vector2 editor for now: "X, Y"
        var val = (Vector2)prop.GetValue(target);
        var editor = new TextInput(Vector2.Zero, Vector2.Zero) { Value = $"{val.X}, {val.Y}" };
        string lastValid = editor.Value;
        
        Action apply = () => {
            var parts = editor.Value.Split(',');
            if (parts.Length == 2 && float.TryParse(parts[0], out float x) && float.TryParse(parts[1], out float y)) {
                prop.SetValue(target, new Vector2(x, y));
                lastValid = editor.Value;
                editor.BorderColor = Color.Transparent;
            } else {
                editor.Value = lastValid;
                editor.BorderColor = Color.Red;
            }
        };
        
        editor.OnSubmit += (v) => apply();
        editor.OnLostFocus += apply;

        row.OnRefresh = () => {
            if (!editor.IsFocused) {
                var v = (Vector2)prop.GetValue(target);
                var current = $"{v.X}, {v.Y}";
                if (editor.Value != current) {
                    editor.Value = current;
                    lastValid = current;
                }
            }
        };

        return editor;
    }

    private UIElement CreateColorEditor(PropertyInfo prop, UIElement target, PropertyRow row) {
        var c = (Color)prop.GetValue(target);
        var editor = new Button(Vector2.Zero, Vector2.Zero, "") { 
            BackgroundColor = c,
            HoverColor = c * 1.1f,
            PressedColor = c * 0.9f,
            BorderColor = Color.White * 0.3f
        };

        ColorPickerPopup popup = null;

        editor.OnClickAction = () => {
            if (popup != null) return;
            
            var absPos = editor.AbsolutePosition;
            // Position popup to the left of the property grid if possible, or just floating
            Vector2 popupPos = new Vector2(absPos.X - 170, absPos.Y);
            
            popup = new ColorPickerPopup(popupPos, (Color)prop.GetValue(target), 
                (newColor) => {
                    prop.SetValue(target, newColor);
                    editor.BackgroundColor = newColor;
                    editor.HoverColor = newColor * 1.1f;
                    editor.PressedColor = newColor * 0.9f;
                },
                () => {
                    popup?.MarkForRemoval();
                    popup = null;
                }
            );
            Shell.AddOverlayElement(popup);
        };

        row.OnRefresh = () => {
            var col = (Color)prop.GetValue(target);
            if (editor.BackgroundColor != col) {
                editor.BackgroundColor = col;
                editor.HoverColor = col * 1.1f;
                editor.PressedColor = col * 0.9f;
            }
        };

        return editor;
    }

    private UIElement CreateEnumEditor(PropertyInfo prop, UIElement target, PropertyRow row) {
        var editor = new ComboBox(Vector2.Zero, new Vector2(100, 25));
        var values = Enum.GetNames(prop.PropertyType);
        foreach (var v in values) editor.Items.Add(v);
        
        var currentValue = prop.GetValue(target).ToString();
        editor.Value = Array.IndexOf(values, currentValue);
        
        editor.OnValueChanged += (idx) => {
            if (idx >= 0 && idx < values.Length) {
                prop.SetValue(target, Enum.Parse(prop.PropertyType, values[idx]));
            }
        };

        row.OnRefresh = () => {
            var val = prop.GetValue(target).ToString();
            var idx = Array.IndexOf(values, val);
            if (editor.Value != idx) {
                editor.SetValue(idx, false);
            }
        };

        return editor;
    }
    
    #endregion
}

public class PropertyRow : UIElement {
    private readonly Label _nameLabel;
    public UIElement Editor { get; set; }
    
    public PropertyRow(string name, float width) : base(Vector2.Zero, new Vector2(width, 25)) {
        _nameLabel = new Label(new Vector2(5, 5), name) {
            Color = Color.LightGray,
            FontSize = 14
        };
        AddChild(_nameLabel);
    }
    
    public Action OnRefresh { get; set; }
    
    public void Refresh() {
        OnRefresh?.Invoke();
    }
    
    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        batch.FillRectangle(AbsolutePosition, Size, new Color(55, 55, 55));
        batch.BorderRectangle(AbsolutePosition, Size, new Color(40, 40, 40), 1f);
    }
}

internal class ColorPickerPopup : UIElement {
    private ColorPicker _picker;
    private Action<Color> _onChanged;
    private Action _onClose;
    private float _openAnim = 0f;
    private bool _markedForRemoval = false;

    public ColorPickerPopup(Vector2 position, Color initialColor, Action<Color> onChanged, Action onClose) 
        : base(position, new Vector2(160, 220)) {
        _onChanged = onChanged;
        _onClose = onClose;
        ConsumesInput = true;

        _picker = new ColorPicker(Vector2.Zero, 60);
        _picker.Value = initialColor;
        _picker.OnValueChanged = (c) => _onChanged?.Invoke(c);
        AddChild(_picker);
        
        Size = _picker.Size;
    }

    public void MarkForRemoval() => _markedForRemoval = true;

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        float targetAnim = _markedForRemoval ? 0f : 1f;
        _openAnim = MathHelper.Lerp(_openAnim, targetAnim, MathHelper.Clamp(dt * 15f, 0, 1));

        if (_markedForRemoval && _openAnim < 0.01f) {
            Parent?.RemoveChild(this);
            return;
        }

        if (InputManager.IsAnyMouseButtonJustPressed(MouseButton.Left)) {
            if (!IsMouseOver) {
                _onClose?.Invoke();
                InputManager.IsMouseConsumed = true;
            }
        }
    }
}
