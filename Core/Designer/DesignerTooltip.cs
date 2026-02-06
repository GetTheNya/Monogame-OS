using System;

namespace TheGame.Core.Designer;

[AttributeUsage(AttributeTargets.Property)]
public class DesignerTooltip : Attribute {
    public string Description { get; }

    public DesignerTooltip(string description) {
        Description = description;
    }
}
