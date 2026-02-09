using System;
using System.Collections.Generic;

namespace TheGame.Core.OS;

/// <summary>
/// Represents all registered handlers and the default handler for a specific file extension.
/// </summary>
public class FileAssociationData {
    /// <summary>
    /// The AppId of the default handler for this extension.
    /// </summary>
    public string Default { get; set; }

    /// <summary>
    /// A dictionary of all registered handlers for this extension, keyed by AppId.
    /// </summary>
    public Dictionary<string, FileAssociationHandler> Handlers { get; set; } = new();
}
