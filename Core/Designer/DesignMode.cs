using System;
using TheGame.Core.UI;

namespace TheGame.Core.Designer;

public interface IDesignerContext { }

public static class DesignMode {
    public static bool IsEnabled { get; private set; }
    public static bool IsToolboxGeneration { get; set; }
    
    // Event fired when design mode is toggled
    public static event Action<bool> OnModeChanged;
    
    public static void SetEnabled(bool enabled) {
        if (IsEnabled != enabled) {
            IsEnabled = enabled;
            DebugLogger.Log($"DesignMode: {(enabled ? "ENABLED" : "DISABLED")}");
            OnModeChanged?.Invoke(enabled);
        }
    }
    
    /// <summary>
    /// Checks if the element's normal input logic should be suppressed in favor of designer logic.
    /// </summary>
    public static bool SuppressNormalInput(UIElement element) {
        if (!IsEnabled) return false;
        
        // Traverse up to find if we are inside a designer context
        var current = element.Parent;
        while (current != null) {
            if (current is IDesignerContext) return true;
            current = current.Parent;
        }
        
        return false;
    }
    
    public static bool IsDesignableElement(UIElement element) {
        // Exclude internal chrome components
        if (element.Name != null && element.Name.StartsWith("__chrome_")) return false;
        
        // For now, most things are designable.
        return true;
    }
}
