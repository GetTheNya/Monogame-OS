using System;
using System.Collections.Generic;

namespace NACHOS;

public class SnippetItem {
    public string Shortcut { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Body { get; set; }
    public string FilePath { get; set; }

    public SnippetItem(string shortcut, string title, string description, string body, string filePath) {
        Shortcut = shortcut;
        Title = title;
        Description = description;
        Body = body;
        FilePath = filePath;
    }
}
