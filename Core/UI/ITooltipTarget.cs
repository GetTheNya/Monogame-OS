using Microsoft.Xna.Framework;

namespace TheGame.Core.UI;

/// <summary>
/// Interface for objects that can display a tooltip.
/// </summary>
public interface ITooltipTarget {
    /// <summary>Tooltip text shown on hover.</summary>
    string Tooltip { get; }
    
    /// <summary>Delay in seconds before the tooltip is shown.</summary>
    float TooltipDelay { get; }
}
