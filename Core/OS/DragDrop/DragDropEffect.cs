namespace TheGame.Core.OS.DragDrop;

/// <summary>
/// Effects that can occur during a drag-drop operation.
/// Used to provide visual feedback to the user.
/// </summary>
[System.Flags]
public enum DragDropEffect {
    /// <summary>No operation will occur. Drop is not allowed.</summary>
    None = 0,
    
    /// <summary>Data will be copied to the target.</summary>
    Copy = 1,
    
    /// <summary>Data will be moved to the target.</summary>
    Move = 2,
    
    /// <summary>A shortcut/link will be created.</summary>
    Link = 4,
    
    /// <summary>All effects are allowed (used for capability checking).</summary>
    All = Copy | Move | Link
}
