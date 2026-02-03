namespace TheGame.Core.OS;

public class FileSystemAPI : BaseAPI {
    public FileSystemAPI(Process process) : base(process) {
    }

    /// <summary> Reads all text from a file. </summary>
    public string ReadAllText(string path) => VirtualFileSystem.Instance.ReadAllText(path);

    /// <summary> Writes text to a file. </summary>
    public void WriteAllText(string path, string content) => VirtualFileSystem.Instance.WriteAllText(path, content);

    /// <summary> Checks if a file or directory exists. </summary>
    public bool Exists(string path) => VirtualFileSystem.Instance.Exists(path);

    /// <summary> Deletes a file or directory. </summary>
    public void Delete(string path) => VirtualFileSystem.Instance.Delete(path);

    /// <summary> Moves or renames a file or directory. </summary>
    public void Move(string source, string dest) => VirtualFileSystem.Instance.Move(source, dest);

    /// <summary> Moves a file or directory to the Recycle Bin. </summary>
    public void Recycle(string path) => VirtualFileSystem.Instance.Recycle(path);

    /// <summary> Returns an array of file paths in a directory. </summary>
    public string[] GetFiles(string path) => VirtualFileSystem.Instance.GetFiles(path);

    /// <summary> Returns an array of subdirectory paths in a directory. </summary>
    public string[] GetDirectories(string path) => VirtualFileSystem.Instance.GetDirectories(path);

    /// <summary> Creates an empty file. </summary>
    public void CreateFile(string path) => VirtualFileSystem.Instance.CreateFile(path);

    /// <summary> Creates a directory. </summary>
    public void CreateDirectory(string path) => VirtualFileSystem.Instance.CreateDirectory(path);

    /// <summary> Gets metadata for a file or directory. </summary>
    public VirtualFileInfo GetFileInfo(string path) => VirtualFileSystem.Instance.GetFileInfo(path);

    /// <summary> Checks if a path points to a directory. </summary>
    public bool IsDirectory(string path) => VirtualFileSystem.Instance.IsDirectory(path);
    
    /// <summary> Resolves a target path relative to the process working directory. </summary>
    public string ResolvePath(string target) => VirtualFileSystem.Instance.ResolvePath(OwningProcess.WorkingDirectory, target);

    /// <summary> Resolves a target path relative to a specific current directory. </summary>
    public string ResolvePath(string current, string target) => VirtualFileSystem.Instance.ResolvePath(current, target);

    /// <summary> Normalizes a path (e.g. collapsing '..'). </summary>
    public string NormalizePath(string path) => VirtualFileSystem.Instance.NormalizePath(path);
}
