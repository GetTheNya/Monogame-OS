using System;
using System.Collections.Generic;

namespace NACHOS;

public enum SnippetCategory {
    Statement,   // if, for, class, prop → StatementStart only
    Expression   // nameof, new, await → Assignment, Argument
}

public class SnippetItem {
    public string Shortcut { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Body { get; set; }
    public string FilePath { get; set; }
    public SnippetCategory Category { get; set; }

    public SnippetItem(string shortcut, string title, string description, string body, string filePath, SnippetCategory category = SnippetCategory.Statement) {
        Shortcut = shortcut;
        Title = title;
        Description = description;
        Body = body;
        FilePath = filePath;
        Category = category;
    }
}

