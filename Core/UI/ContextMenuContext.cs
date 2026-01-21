using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TheGame.Core.UI;

public class ContextMenuContext {
    public UIElement Target { get; set; }
    public Vector2 Position { get; set; }
    public Dictionary<string, object> Properties { get; private set; } = new();
    public bool Handled { get; set; } = false;

    public ContextMenuContext(UIElement target, Vector2 position) {
        Target = target;
        Position = position;
    }

    public T GetProperty<T>(string key, T defaultValue = default) {
        if (Properties.TryGetValue(key, out var val) && val is T typedVal) {
            return typedVal;
        }
        return defaultValue;
    }

    public void SetProperty(string key, object value) {
        Properties[key] = value;
    }
}
