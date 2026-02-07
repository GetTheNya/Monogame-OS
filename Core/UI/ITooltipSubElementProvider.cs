using Microsoft.Xna.Framework;

namespace TheGame.Core.UI;

/// <summary>
/// Interface for UI elements that contain logical sub-elements with their own tooltips
/// (e.g., a SystemTray containing multiple TrayIcons).
/// </summary>
public interface ITooltipSubElementProvider {
    /// <summary>
    /// Finds a tooltip target within this element at the specified screen position.
    /// </summary>
    ITooltipTarget FindTooltipSubElement(Vector2 mousePos);
}
