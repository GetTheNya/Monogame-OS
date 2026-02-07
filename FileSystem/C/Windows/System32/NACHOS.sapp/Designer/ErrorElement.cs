using System.Linq;
using Microsoft.Xna.Framework;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;

namespace NACHOS.Designer;

public class ErrorElement : Panel {
    public UISerializer.UIElementData OriginalData { get; set; }
    
    [System.Obsolete("For Designer/Serialization use only")]
    public ErrorElement() : this(null) { }

    public ErrorElement(UISerializer.UIElementData data) : base(Vector2.Zero, new Vector2(150, 40)) {
        OriginalData = data;
        BackgroundColor = new Color(60, 0, 0, 200);
        BorderThickness = 1f;
        
        string typeName = data?.Type?.Split(',')[0].Split('.').Last() ?? "Unknown";
        var label = new TheGame.Core.UI.Controls.Label(new Vector2(5, 5), "?? " + typeName) {
            TextColor = Color.Salmon,
            FontSize = 14
        };
        AddChild(label);
        
        // Try to match size if possible
        if (data != null && data.Properties.TryGetValue("Size", out var sizeObj)) {
            if (sizeObj is Vector2 s) {
                Size = s;
            }
        }
    }
}
