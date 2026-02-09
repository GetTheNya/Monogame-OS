using System;

namespace TheGame.Core.OS;

/// <summary>
/// Represents a single application's registration for a file type.
/// </summary>
public class FileAssociationHandler {
    /// <summary>
    /// Icon path relative to the application's root directory.
    /// Used for file icons in Explorer (document icon).
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// Friendly description of the file type (e.g., "Text Document").
    /// </summary>
    public string Description { get; set; }
}
