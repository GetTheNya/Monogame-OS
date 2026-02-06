using System;
using System.IO;
using System.Text.Json;
using TheGame.Core.OS;
using NACHOS;

namespace NACHOS;

public static class ProjectMetadataManager {
    private static string _projectPath;
    private const string MetadataFolderName = ".nachos";

    public static void Initialize(string projectPath) {
        _projectPath = projectPath;
        if (string.IsNullOrEmpty(_projectPath)) return;

        string metaPath = Path.Combine(_projectPath, MetadataFolderName);
        if (!VirtualFileSystem.Instance.Exists(metaPath)) {
            VirtualFileSystem.Instance.CreateDirectory(metaPath);
        }
    }

    public static string GetMetadataPath(string fileName) {
        if (string.IsNullOrEmpty(_projectPath)) return null;
        return Path.Combine(_projectPath, MetadataFolderName, fileName);
    }

    public static bool Exists(string fileName) {
        string path = GetMetadataPath(fileName);
        return !string.IsNullOrEmpty(path) && VirtualFileSystem.Instance.Exists(path);
    }

    public static string ReadMetadata(string fileName) {
        string path = GetMetadataPath(fileName);
        if (string.IsNullOrEmpty(path) || !VirtualFileSystem.Instance.Exists(path)) return null;
        return VirtualFileSystem.Instance.ReadAllText(path);
    }

    public static void WriteMetadata(string fileName, string content) {
        string path = GetMetadataPath(fileName);
        if (string.IsNullOrEmpty(path)) return;
        VirtualFileSystem.Instance.WriteAllText(path, content);
    }

    public static void SaveProjectFile(ProjectSettings settings) {
        if (string.IsNullOrEmpty(_projectPath)) return;
        
        string vNprojPath = Path.Combine(_projectPath, settings.Name + ".nproj");
        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        VirtualFileSystem.Instance.WriteAllText(vNprojPath, json);
    }
}
