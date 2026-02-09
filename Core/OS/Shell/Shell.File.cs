using System.Reflection;

using System;
using System.Collections.Generic;
using System.Linq;

namespace TheGame.Core.OS;

public static partial class Shell {
    public static class File {
        private static string NormalizeExtension(string extension) {
            if (string.IsNullOrEmpty(extension)) return "";
            extension = extension.ToLower();
            if (!extension.StartsWith(".")) extension = "." + extension;
            return extension;
        }

        private static FileAssociationData LoadAssociationData(string extension) {
            extension = NormalizeExtension(extension);
            string key = $"{Shell.Registry.FileAssociations}\\{extension}";
            
            // Try to load new format first
            var data = TheGame.Core.OS.Registry.GetValue<FileAssociationData>(key, null);
            if (data != null) return data;
            
            // Check for old format (single string value)
            string oldValue = TheGame.Core.OS.Registry.GetValue<string>(key, null);
            if (!string.IsNullOrEmpty(oldValue)) {
                // Migrate to new format
                data = new FileAssociationData {
                    Default = oldValue,
                    Handlers = new Dictionary<string, FileAssociationHandler> {
                        [oldValue] = new FileAssociationHandler {
                            Icon = null,
                            Description = null
                        }
                    }
                };
                // Save in new format
                TheGame.Core.OS.Registry.SetValue(key, data);
                return data;
            }
            
            // No association exists
            return new FileAssociationData { 
                Handlers = new Dictionary<string, FileAssociationHandler>() 
            };
        }

        private static void SaveAssociationData(string extension, FileAssociationData data) {
            extension = NormalizeExtension(extension);
            string key = $"{Shell.Registry.FileAssociations}\\{extension}";
            TheGame.Core.OS.Registry.SetValue(key, data);
        }

        /// <summary>
        /// Registers an application as a handler for a file extension.
        /// </summary>
        public static void RegisterFileTypeHandler(TheGame.Core.OS.Process process, string extension, string iconPath = null, string description = null) {
            RegisterFileTypeHandler(process.AppId, extension, iconPath, description);
        }

        /// <summary>
        /// Registers an application as a handler for a file extension using its AppId.
        /// </summary>
        public static void RegisterFileTypeHandler(string appId, string extension, string iconPath = null, string description = null) {
            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(extension)) return;
            appId = appId.ToUpper();
            extension = NormalizeExtension(extension);

            DebugLogger.Log($"Registering file type handler: {extension} -> {appId} (Icon: {iconPath ?? "default"}, Desc: {description ?? "none"})");
            
            var data = LoadAssociationData(extension);
            
            bool isNewHandler = !data.Handlers.ContainsKey(appId);
            if (isNewHandler) {
                data.Handlers[appId] = new FileAssociationHandler();
            }
            
            var handler = data.Handlers[appId];
            if (iconPath != null) handler.Icon = iconPath;
            if (description != null) handler.Description = description;
            
            // Android-style behavior: if a NEW app registers and there were already other apps,
            // clear the default so the user is prompted to choose again.
            if (isNewHandler && data.Handlers.Count > 1) {
                if (!string.IsNullOrEmpty(data.Default)) {
                    DebugLogger.Log($"Shell.File: New handler {appId} registered for {extension}. Clearing default '{data.Default}' to prompt user.");
                    data.Default = null;
                }
            }
            
            // If no default is set (first app or just cleared), make this the default only if it's the only one
            if (string.IsNullOrEmpty(data.Default) && data.Handlers.Count == 1) {
                data.Default = appId;
            }
            
            SaveAssociationData(extension, data);
        }

        /// <summary>
        /// Unregisters an application from handling a file extension.
        /// </summary>
        public static void UnregisterFileTypeHandler(TheGame.Core.OS.Process process, string extension) {
            UnregisterFileTypeHandler(process.AppId, extension);
        }

        /// <summary>
        /// Unregisters an application from handling a file extension using its AppId.
        /// </summary>
        public static void UnregisterFileTypeHandler(string appId, string extension) {
            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(extension)) return;
            appId = appId.ToUpper();
            extension = NormalizeExtension(extension);

            var data = LoadAssociationData(extension);
            if (data.Handlers.Remove(appId)) {
                if (data.Default == appId) {
                    data.Default = data.Handlers.Keys.FirstOrDefault();
                }
                SaveAssociationData(extension, data);
                DebugLogger.Log($"Unregistered file type handler: {extension} -> {appId}");
            }
        }

        /// <summary>
        /// Gets all registered application IDs for a specific file extension.
        /// Performs self-healing to remove orphaned (deleted) applications.
        /// </summary>
        public static List<string> GetFileTypeHandlers(string extension) {
            extension = NormalizeExtension(extension);
            var data = LoadAssociationData(extension);
            
            bool changed = false;
            var appIds = data.Handlers.Keys.ToList();
            
            foreach (var appId in appIds) {
                if (AppLoader.Instance.GetAppDirectory(appId) == null) {
                    DebugLogger.Log($"File.GetFileTypeHandlers: Removing orphaned app {appId} from {extension}");
                    data.Handlers.Remove(appId);
                    if (data.Default == appId) data.Default = null;
                    changed = true;
                }
            }
            
            if (changed) {
                if (string.IsNullOrEmpty(data.Default)) {
                    data.Default = data.Handlers.Keys.FirstOrDefault();
                }
                SaveAssociationData(extension, data);
            }
            
            return data.Handlers.Keys.ToList();
        }

        /// <summary>
        /// Gets the default application ID for a specific file extension.
        /// Performs self-healing if the default application is missing.
        /// </summary>
        public static string GetFileTypeHandler(string extension) => GetDefaultFileTypeHandler(extension);

        /// <summary>
        /// Gets the default application ID for a specific file extension.
        /// Performs self-healing if the default application is missing.
        /// </summary>
        public static string GetDefaultFileTypeHandler(string extension) {
            extension = NormalizeExtension(extension);
            var data = LoadAssociationData(extension);
            
            if (string.IsNullOrEmpty(data.Default)) return null;
            
            // Self-healing: check if default app still exists
            if (AppLoader.Instance.GetAppDirectory(data.Default) == null) {
                DebugLogger.Log($"File.GetDefaultFileTypeHandler: Orphaned default app {data.Default} detected for {extension}. Triggering cleanup.");
                // GetFileTypeHandlers will perform the full cleanup
                var handlers = GetFileTypeHandlers(extension);
                if (handlers.Count == 0) return null;
                
                // Reload data after cleanup
                data = LoadAssociationData(extension);
            }
            
            return data.Default;
        }

        /// <summary>
        /// Sets the default application ID for a specific file extension.
        /// </summary>
        public static void SetDefaultFileTypeHandler(string extension, string appId) {
            if (string.IsNullOrEmpty(extension) || string.IsNullOrEmpty(appId)) return;
            appId = appId.ToUpper();
            extension = NormalizeExtension(extension);
            
            var data = LoadAssociationData(extension);
            if (data.Handlers.ContainsKey(appId)) {
                data.Default = appId;
                SaveAssociationData(extension, data);
                DebugLogger.Log($"Set default file type handler: {extension} -> {appId}");
            }
        }

        /// <summary>
        /// Updates the file icon registration for an application.
        /// </summary>
        public static void UpdateFileTypeIcon(TheGame.Core.OS.Process process, string extension, string iconPath) {
            UpdateFileTypeIcon(process.AppId, extension, iconPath);
        }

        /// <summary>
        /// Updates the file icon registration for an application using its AppId.
        /// </summary>
        public static void UpdateFileTypeIcon(string appId, string extension, string iconPath) {
            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(extension)) return;
            appId = appId.ToUpper();
            extension = NormalizeExtension(extension);
            
            var data = LoadAssociationData(extension);
            if (data.Handlers.TryGetValue(appId, out var handler)) {
                handler.Icon = iconPath;
                SaveAssociationData(extension, data);
            }
        }

        /// <summary>
        /// Updates the friendly description for a file extension.
        /// </summary>
        public static void UpdateFileTypeDescription(TheGame.Core.OS.Process process, string extension, string description) {
            UpdateFileTypeDescription(process.AppId, extension, description);
        }

        /// <summary>
        /// Updates the friendly description for a file extension using its AppId.
        /// </summary>
        public static void UpdateFileTypeDescription(string appId, string extension, string description) {
            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(extension)) return;
            appId = appId.ToUpper();
            extension = NormalizeExtension(extension);
            
            var data = LoadAssociationData(extension);
            if (data.Handlers.TryGetValue(appId, out var handler)) {
                handler.Description = description;
                SaveAssociationData(extension, data);
            }
        }

        /// <summary>
        /// Gets the friendly description for a file extension from the default handler.
        /// </summary>
        public static string GetFileTypeDescription(string extension) {
            extension = NormalizeExtension(extension);
            var data = LoadAssociationData(extension);
            
            string appId = data.Default;
            if (string.IsNullOrEmpty(appId)) {
                appId = data.Handlers.Keys.FirstOrDefault();
            }
            
            if (appId != null && data.Handlers.TryGetValue(appId, out var handler)) {
                return handler.Description;
            }
            
            return null;
        }

        [Obsolete("Use RegisterFileTypeHandler(Process, string, string, string) instead")]
        public static void RegisterFileTypeHandler(string extension, string appId = null) {
            if (string.IsNullOrEmpty(appId)) {
                var callingAssembly = Assembly.GetCallingAssembly();
                appId = AppLoader.Instance.GetAppIdFromAssembly(callingAssembly);
            }
            // Explicitly call the 4-argument overload to avoid recursion
            RegisterFileTypeHandler(appId, extension, null, null);
        }
    }
}
