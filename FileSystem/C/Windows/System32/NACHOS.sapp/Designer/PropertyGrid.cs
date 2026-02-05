using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;
using TheGame.Core.UI;

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
            .OrderBy(p => p.Name);
            
        float y = 0;
        foreach (var prop in props) {
            if (ShouldSkipProperty(prop)) continue;
            
            var row = CreatePropertyRow(prop, element);
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
    
    private UIElement CreatePropertyRow(PropertyInfo prop, UIElement target) {
        var container = new PropertyRow(prop.Name, Size.X - 20);
        
        UIElement editor = null;
        if (prop.PropertyType == typeof(string)) {
            editor = CreateStringEditor(prop, target);
        } else if (prop.PropertyType == typeof(float)) {
            editor = CreateFloatEditor(prop, target);
        } else if (prop.PropertyType == typeof(int)) {
            editor = CreateIntEditor(prop, target);
        } else if (prop.PropertyType == typeof(bool)) {
            editor = CreateBoolEditor(prop, target);
        } else if (prop.PropertyType == typeof(Vector2)) {
            editor = CreateVector2Editor(prop, target);
        } else if (prop.PropertyType == typeof(Color)) {
            editor = CreateColorEditor(prop, target);
        } else if (prop.PropertyType.IsEnum) {
            editor = CreateEnumEditor(prop, target);
        }
        
        if (editor != null) {
            editor.Position = new Vector2(100, 0); // Label takes first 100px
            editor.Size = new Vector2(container.Size.X - 105, 20);
            container.AddChild(editor);
            container.Editor = editor;
        }
        
        return container;
    }
    
    private void RefreshValues() {
        foreach (var row in _editorRows.OfType<PropertyRow>()) {
            row.Refresh();
        }
    }

    #region Specialized Editors
    
    private UIElement CreateStringEditor(PropertyInfo prop, UIElement target) {
        var editor = new TextInput(Vector2.Zero, Vector2.Zero) { Value = prop.GetValue(target)?.ToString() ?? "" };
        editor.OnSubmit += (val) => prop.SetValue(target, val);
        editor.OnLostFocus += () => prop.SetValue(target, editor.Value);
        return editor;
    }
    
    private UIElement CreateFloatEditor(PropertyInfo prop, UIElement target) {
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
        return editor;
    }

    private UIElement CreateIntEditor(PropertyInfo prop, UIElement target) {
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
        return editor;
    }

    private UIElement CreateBoolEditor(PropertyInfo prop, UIElement target) {
        var editor = new Checkbox(Vector2.Zero) { Value = (bool)prop.GetValue(target) };
        editor.OnValueChanged += (val) => prop.SetValue(target, val);
        return editor;
    }

    private UIElement CreateVector2Editor(PropertyInfo prop, UIElement target) {
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
        return editor;
    }

    private UIElement CreateColorEditor(PropertyInfo prop, UIElement target) {
        // String-based color: "R, G, B, A"
        var c = (Color)prop.GetValue(target);
        var editor = new TextInput(Vector2.Zero, Vector2.Zero) { Value = $"{c.R}, {c.G}, {c.B}, {c.A}" };
        string lastValid = editor.Value;
        
        Action apply = () => {
            var parts = editor.Value.Split(',');
            if (parts.Length == 4 && 
                byte.TryParse(parts[0], out byte r) && 
                byte.TryParse(parts[1], out byte g) && 
                byte.TryParse(parts[2], out byte b) && 
                byte.TryParse(parts[3], out byte a)) {
                prop.SetValue(target, new Color(r, g, b, a));
                lastValid = editor.Value;
                editor.BorderColor = Color.Transparent;
            } else {
                editor.Value = lastValid;
                editor.BorderColor = Color.Red;
            }
        };
        
        editor.OnSubmit += (v) => apply();
        editor.OnLostFocus += apply;
        return editor;
    }

    private UIElement CreateEnumEditor(PropertyInfo prop, UIElement target) {
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
    
    public void Refresh() {
        // Logic to pull value from Target and update Editor text/state
        // This might need more plumbing to work universally across editor types
    }
    
    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        batch.FillRectangle(AbsolutePosition, Size, new Color(55, 55, 55));
        batch.BorderRectangle(AbsolutePosition, Size, new Color(40, 40, 40), 1f);
    }
}
