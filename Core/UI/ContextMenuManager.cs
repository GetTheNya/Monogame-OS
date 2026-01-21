using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using TheGame.Core.OS;

namespace TheGame.Core.UI;

public class ContextMenuManager {
    private static ContextMenuManager _instance;
    public static ContextMenuManager Instance => _instance ??= new ContextMenuManager();

    private readonly List<IContextMenuProvider> _globalProviders = new();

    private ContextMenuManager() { }

    public void RegisterGlobalProvider(IContextMenuProvider provider) {
        if (!_globalProviders.Contains(provider)) {
            _globalProviders.Add(provider);
        }
    }

    public void UnregisterGlobalProvider(IContextMenuProvider provider) {
        _globalProviders.Remove(provider);
    }

    public void Show(ContextMenuContext context) {
        var items = new List<MenuItem>();

        // 1. Bubbling population
        UIElement current = context.Target;
        while (current != null && !context.Handled) {
            current.PopulateContextMenu(context, items);
            current = current.Parent;
        }

        // 2. Global providers
        foreach (var provider in _globalProviders) {
            provider.PopulateContextMenu(context, items);
        }

        Show(context.Position, items);
    }

    public void Show(Vector2 position, List<MenuItem> items) {
        var finalItems = ProcessItems(items);

        if (finalItems.Count > 0) {
            Shell.GlobalContextMenu.Show(position, finalItems);
        }
    }

    private List<MenuItem> ProcessItems(List<MenuItem> items) {
        // Deduplication and Filtering
        var processed = new List<MenuItem>();
        foreach (var item in items) {
            if (!item.IsVisible) continue;
            
            // Deduplicate based on Text and Action (and Type)
            if (item.Type == MenuItemType.Separator) {
                // Remove consecutive separators or leading/trailing separators later
                processed.Add(item);
                continue;
            }

            if (!processed.Any(p => p.Equals(item))) {
                processed.Add(item);
            }
        }

        // Clean up separators
        processed = CleanSeparators(processed);

        // Sorting
        return processed.OrderByDescending(i => i.Priority).ToList();
    }

    private List<List<MenuItem>> GroupByPriority(List<MenuItem> items) {
         // Future: implementation for logical group separation
         return new List<List<MenuItem>> { items };
    }

    private List<MenuItem> CleanSeparators(List<MenuItem> items) {
        var result = new List<MenuItem>();
        bool lastWasSeparator = true; // Avoid leading separator

        foreach (var item in items) {
            if (item.Type == MenuItemType.Separator) {
                if (!lastWasSeparator) {
                    result.Add(item);
                    lastWasSeparator = true;
                }
            } else {
                result.Add(item);
                lastWasSeparator = false;
            }
        }

        // Remove trailing separator
        if (result.Count > 0 && result.Last().Type == MenuItemType.Separator) {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }
}
