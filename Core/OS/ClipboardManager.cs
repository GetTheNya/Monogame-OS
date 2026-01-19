using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Core.OS;

public enum ClipboardContentType {
    Text,
    FileList,
    Image,
    Custom
}

public class ClipboardItem {
    public string Id { get; } = Guid.NewGuid().ToString();
    public ClipboardContentType Type { get; set; }
    public object Data { get; set; }
    public string SourceApp { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsPinned { get; set; }

    public string PreviewText {
        get {
            if (Type == ClipboardContentType.Text) return Data as string;
            if (Type == ClipboardContentType.FileList) {
                var files = Data as IEnumerable<string>;
                return files != null ? string.Join(", ", files.Select(System.IO.Path.GetFileName)) : "Empty File List";
            }
            return Type.ToString();
        }
    }
}

public class ClipboardManager {
    private static ClipboardManager _instance;
    public static ClipboardManager Instance => _instance ??= new ClipboardManager();

    private List<ClipboardItem> _history = new();
    private const int MaxHistoryItems = 20;

    public event Action OnClipboardChanged;

    private ClipboardManager() { }

    public IReadOnlyList<ClipboardItem> History => _history;

    public void SetData(object data, ClipboardContentType type, string sourceApp = null) {
        if (data == null) return;
        
        if (type == ClipboardContentType.Text && data is string s && string.IsNullOrWhiteSpace(s)) {
            return;
        }

        // Deduplication: If we already have this exact data, move it to top
        var existing = _history.FirstOrDefault(i => Equals(i.Data, data) && i.Type == type);
        if (existing != null) {
            _history.Remove(existing);
            existing.Timestamp = DateTime.Now;
            _history.Insert(0, existing);
        } else {
            var item = new ClipboardItem {
                Data = data,
                Type = type,
                SourceApp = sourceApp
            };
            _history.Insert(0, item);
        }

        // Limit history
        if (_history.Count > MaxHistoryItems) {
            _history.RemoveAt(_history.Count - 1);
        }

        OnClipboardChanged?.Invoke();
        DebugLogger.Log($"Clipboard: Added {type} from {sourceApp ?? "Unknown"}");
    }

    public T GetData<T>(ClipboardContentType type) {
        var item = _history.FirstOrDefault(i => i.Type == type);
        return item != null ? (T)item.Data : default;
    }

    public ClipboardItem GetCurrent() => _history.Count > 0 ? _history[0] : null;

    public void Clear() {
        _history.RemoveAll(i => !i.IsPinned);
        OnClipboardChanged?.Invoke();
    }

    public void RemoveItem(string id) {
        var item = _history.FirstOrDefault(i => i.Id == id);
        if (item != null) {
            _history.Remove(item);
            OnClipboardChanged?.Invoke();
        }
    }

    public void PinToTop(string id) {
        var item = _history.FirstOrDefault(i => i.Id == id);
        if (item != null) {
            _history.Remove(item);
            _history.Insert(0, item);
            OnClipboardChanged?.Invoke();
        }
    }

    public void TogglePin(string id) {
        var item = _history.FirstOrDefault(i => i.Id == id);
        if (item != null) {
            item.IsPinned = !item.IsPinned;
            OnClipboardChanged?.Invoke();
        }
    }

    public IReadOnlyList<ClipboardItem> GetHistory() {
        // Sort pinned items to the top, then by timestamp
        return _history
            .OrderByDescending(i => i.IsPinned)
            .ThenByDescending(i => i.Timestamp)
            .ToList();
    }
}
