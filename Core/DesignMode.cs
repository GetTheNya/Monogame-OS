using System;
using TheGame.Core.UI;

namespace TheGame.Core.Designer;

public interface IDesignerContext { }

public static class DesignMode {
    public static bool IsEnabled { get; private set; }
    
    // Event fired when design mode is toggled
    public static event Action<bool> OnModeChanged;
    
    public static void SetEnabled(bool enabled) {
        if (IsEnabled != enabled) {
            IsEnabled = enabled;
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
        // For now, most things are designable.
        // We might want to exclude certain internal layers.
        return true;
    }
}
