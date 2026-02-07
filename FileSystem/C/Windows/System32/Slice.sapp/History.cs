using System;
using System.Collections.Generic;

namespace ScreenCapture;

public class HistoryItem {
    public string Path { get; set; }
    public DateTime Timestamp { get; set; }
}

public class CaptureHistory {
    public List<HistoryItem> Items { get; set; } = new List<HistoryItem>();
}
