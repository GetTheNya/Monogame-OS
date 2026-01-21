using System.Collections.Generic;

namespace TheGame.Core.UI;

public interface IContextMenuProvider {
    void PopulateContextMenu(ContextMenuContext context, List<MenuItem> items);
}
