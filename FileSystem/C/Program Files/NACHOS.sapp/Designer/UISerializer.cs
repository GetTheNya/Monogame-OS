using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using TheGame.Core;
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

    public static UIElement CloneElement(UIElement element, Assembly overrideAssembly = null) {
        string json = Serialize(element);
        return Deserialize(json, overrideAssembly);
    }

    private static UIElementData SerializeElement(UIElement element) {
        if (element is ErrorElement err) {
            return err.OriginalData;
        }

        var type = element.GetType();
        string typeName = type.AssemblyQualifiedName;

        // Redirect DesignerWindow to Window for serialization compatibility
        if (element is DesignerWindow) {
            typeName = typeof(Window).AssemblyQualifiedName;
        }

        var data = new UIElementData {
            Type = typeName,
            Name = element.Name
        };

        var props = GetSerializableProperties(element);
        foreach (var prop in props) {
            var val = prop.GetValue(element);
            DebugLogger.Log($"Property found: {prop.Name}, Value: {val ?? "NULL"}");
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

    public static UIElement Deserialize(string json, Assembly overrideAssembly = null) {
        var data = JsonSerializer.Deserialize<UIElementData>(json);
        if (data == null) return null;
        return DeserializeElement(data, overrideAssembly);
    }

    private static UIElement DeserializeElement(UIElementData data, Assembly overrideAssembly = null) {
        string typeName = data.Type;
        
        // Redirect Window to DesignerWindow for the designer
        if (typeName.Contains("TheGame.Core.UI.Window") && !typeName.Contains("DesignerWindow")) {
            typeName = typeof(DesignerWindow).AssemblyQualifiedName;
        }

        Type type = null;
        
        // Try to resolve from override assembly first (for user components)
        if (overrideAssembly != null) {
            // Extract the simple name if it's an assembly-qualified name
            string simpleName = typeName.Split(',')[0].Trim();
            type = overrideAssembly.GetType(simpleName);
        }

        // Fallback to type resolution by name if assembly didn't find it exactly
        if (type == null) {
            type = Type.GetType(typeName);
        }

        if (type == null) return new ErrorElement(data);

        UIElement element = null;
        try {
            element = Activator.CreateInstance(type) as UIElement;
        } catch { }

        if (element == null) element = new ErrorElement(data);

        element.Name = data.Name;

        // Restore basic properties even for ErrorElement
        if (data.Properties.TryGetValue("Position", out var pos)) {
            try { element.Position = (Vector2)ConvertFromSerializable(pos, typeof(Vector2)); } catch { }
        }
        if (data.Properties.TryGetValue("Size", out var size)) {
            try { element.Size = (Vector2)ConvertFromSerializable(size, typeof(Vector2)); } catch { }
        }

        if (!(element is ErrorElement)) {
            foreach (var kvp in data.Properties) {
                var prop = type.GetProperty(kvp.Key);
                if (prop != null) {
                    var val = ConvertFromSerializable(kvp.Value, prop.PropertyType);
                    prop.SetValue(element, val);
                }
            }
        }

        foreach (var childData in data.Children) {
            var child = DeserializeElement(childData, overrideAssembly);
            if (child != null) {
                element.AddChild(child);
            }
        }

        return element;
    }

    public static IEnumerable<PropertyInfo> GetSerializableProperties(UIElement element) {
        var props = element.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !Attribute.IsDefined(p, typeof(DesignerIgnoreJsonSerialization)))
            .Where(p => p.CanRead && p.CanWrite && !ShouldSkipProperty(p));

        foreach (var prop in props) {
            // Skip Panel-specific properties for DesignerWindow as it serializes as Window
            if (element is DesignerWindow && (prop.Name == "BorderThickness" || prop.Name == "CornerRadius")) continue;
            yield return prop;
        }
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
