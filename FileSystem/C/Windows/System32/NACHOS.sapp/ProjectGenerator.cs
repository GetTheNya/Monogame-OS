using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using System.Text.RegularExpressions;
using TheGame.Core.OS;

namespace NACHOS;

public static class ProjectGenerator {
    public static string CreateProject(Process ownerProcess, ProjectSettings settings) {
        string projectName = settings.Name;
        if (!projectName.EndsWith(".sapp", StringComparison.OrdinalIgnoreCase)) {
            projectName += ".sapp";
        }

        string vTemplatePath = VirtualFileSystem.Instance.GetAppResourcePath("NACHOS", "Templates/Projects/" + settings.Type.ToString());
        string vProjectPath = Path.Combine(settings.Location, projectName);

        if (!VirtualFileSystem.Instance.Exists(vProjectPath)) {
            VirtualFileSystem.Instance.CreateDirectory(vProjectPath);
        }

        var replacements = new Dictionary<string, string> {
            { "{projectName}", settings.Name },
            { "{namespace}", settings.Namespace },
            { "{windowTitle}", settings.WindowTitle },
            { "{windowWidth}", settings.WindowWidth.ToString() },
            { "{windowHeight}", settings.WindowHeight.ToString() },
            { "{windowResizable}", settings.IsResizable.ToString().ToLower() }
        };

        // Copy template files
        foreach (string file in VirtualFileSystem.Instance.GetFiles(vTemplatePath)) {
            string fileName = Path.GetFileName(file);
            string targetFileName = fileName;

            if (fileName.EndsWith(".txt")) {
                if (fileName.StartsWith("manifest")) targetFileName = fileName.Replace(".txt", ".json");
                else targetFileName = fileName.Replace(".txt", ".cs");
            }

            string content = VirtualFileSystem.Instance.ReadAllText(file);

            // Process tags: <sample>...</sample> and <noSample>...</noSample>
            if (settings.CreateSampleCode) {
                // Keep sample, remove tags, remove noSample entirely
                content = Regex.Replace(content, @"(?m)^[ \t]*<sample>[ \t]*\r?\n?", "");
                content = Regex.Replace(content, @"(?m)^[ \t]*<\/sample>[ \t]*\r?\n?", "");
                content = Regex.Replace(content, @"(?ms)^[ \t]*<noSample>.*?<\/noSample>[ \t]*\r?\n?", "");
            } else {
                // Keep noSample, remove tags, remove sample entirely
                content = Regex.Replace(content, @"(?m)^[ \t]*<noSample>[ \t]*\r?\n?", "");
                content = Regex.Replace(content, @"(?m)^[ \t]*<\/noSample>[ \t]*\r?\n?", "");
                content = Regex.Replace(content, @"(?ms)^[ \t]*<sample>.*?<\/sample>[ \t]*\r?\n?", "");
            }

            foreach (var kvp in replacements) {
                content = content.Replace(kvp.Key, kvp.Value);
            }

            VirtualFileSystem.Instance.WriteAllText(Path.Combine(vProjectPath, targetFileName), content);
        }

        // Include Assets
        if (settings.IncludeAssets) {
            string vContentPath = Path.Combine(vProjectPath, "Content");
            if (!VirtualFileSystem.Instance.Exists(vContentPath)) {
                VirtualFileSystem.Instance.CreateDirectory(vContentPath);
            }
        }

        // Create .nproj file
        string vNprojPath = Path.Combine(vProjectPath, settings.Name + ".nproj");
        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        VirtualFileSystem.Instance.WriteAllText(vNprojPath, json);

        return vProjectPath;
    }
}
