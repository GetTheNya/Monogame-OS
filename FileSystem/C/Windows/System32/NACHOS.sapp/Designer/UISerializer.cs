using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using TheGame.Core.UI;
using TheGame.Core.Designer;

namespace NACHOS.Designer;

public static class UISerializer {
    public class UIElementData {
        public string Type { get; set; }
        public string Name { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
        public List<UIElementData> Children { get; set; } = new();
    }

    public static string Serialize(UIElement root) {
        var data = SerializeElement(root);
        return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
    }

    private static UIElementData SerializeElement(UIElement element) {
        var data = new UIElementData {
            Type = element.GetType().AssemblyQualifiedName,
            Name = element.Name
        };

        var props = element.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && !ShouldSkipProperty(p));

        foreach (var prop in props) {
            var val = prop.GetValue(element);
            if (val != null) {
                data.Properties[prop.Name] = ConvertToSerializable(val);
            }
        }

        foreach (var child in element.Children) {
            // Only serialize designable children
            if (DesignMode.IsDesignableElement(child)) {
                data.Children.Add(SerializeElement(child));
            }
        }

        return data;
    }

    public static UIElement Deserialize(string json) {
        var data = JsonSerializer.Deserialize<UIElementData>(json);
        return DeserializeElement(data);
    }

    private static UIElement DeserializeElement(UIElementData data) {
        var type = Type.GetType(data.Type);
        if (type == null) return null;

        var element = Activator.CreateInstance(type) as UIElement;
        if (element == null) return null;

        element.Name = data.Name;

        foreach (var kvp in data.Properties) {
            var prop = type.GetProperty(kvp.Key);
            if (prop != null) {
                var val = ConvertFromSerializable(kvp.Value, prop.PropertyType);
                prop.SetValue(element, val);
            }
        }

        foreach (var childData in data.Children) {
            var child = DeserializeElement(childData);
            if (child != null) {
                element.AddChild(child);
            }
        }

        return element;
    }

    private static bool ShouldSkipProperty(PropertyInfo prop) {
        string[] skip = { "Parent", "Children", "Tag", "ActiveWindow", "OwnerProcess" };
        if (skip.Contains(prop.Name)) return true;
        if (prop.PropertyType == typeof(System.Action)) return true;
        // Don't serialize properties that are also children (like Window chrome)
        return false;
    }

    private static object ConvertToSerializable(object val) {
        if (val is Vector2 v) return new { X = v.X, Y = v.Y };
        if (val is Color c) return new { R = c.R, G = c.G, B = c.B, A = c.A };
        if (val is Vector4 v4) return new { X = v4.X, Y = v4.Y, Z = v4.Z, W = v4.W };
        if (val is Enum e) return e.ToString();
        return val;
    }

    private static object ConvertFromSerializable(object val, Type targetType) {
        if (val == null) return null;
        
        var json = (JsonElement)val;
        
        if (targetType == typeof(Vector2)) {
            return new Vector2(json.GetProperty("X").GetSingle(), json.GetProperty("Y").GetSingle());
        }
        if (targetType == typeof(Color)) {
            return new Color(json.GetProperty("R").GetByte(), json.GetProperty("G").GetByte(), json.GetProperty("B").GetByte(), json.GetProperty("A").GetByte());
        }
        if (targetType == typeof(Vector4)) {
            return new Vector4(json.GetProperty("X").GetSingle(), json.GetProperty("Y").GetSingle(), json.GetProperty("Z").GetSingle(), json.GetProperty("W").GetSingle());
        }
        if (targetType.IsEnum) {
            return Enum.Parse(targetType, json.GetString());
        }
        
        // Basic types
        if (targetType == typeof(float)) return json.GetSingle();
        if (targetType == typeof(int)) return json.GetInt32();
        if (targetType == typeof(bool)) return json.GetBoolean();
        if (targetType == typeof(string)) return json.GetString();
        
        return val;
    }
}
