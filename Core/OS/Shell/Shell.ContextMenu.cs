using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TheGame.Core.Input;
using TheGame.Core.UI;

namespace TheGame.Core.OS;

public static partial class Shell {
    /// <summary>
    /// Context Menu API.
    /// </summary>
    public static class ContextMenu {
        public static void Show(ContextMenuContext context) => ContextMenuManager.Instance.Show(context);
        public static void Show(Vector2 position, List<MenuItem> items) => ContextMenuManager.Instance.Show(position, items);

        public static void Show(UIElement target) {
            var context = new ContextMenuContext(target, InputManager.MousePosition.ToVector2());
            Show(context);
        }

        public static void RegisterProvider(IContextMenuProvider provider) {
            ContextMenuManager.Instance.RegisterGlobalProvider(provider);
        }
    }
}
