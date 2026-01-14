using System;
using System.IO;

namespace TheGame.Core;

public static class DebugLogger {
    private static string _path = "debug_log.txt";

    static DebugLogger() {
        if (File.Exists(_path)) {
            File.Delete(_path);
            File.Create(_path).Close();
        }
    }

    public static void Log(string message) {
        try {
            var time = DateTime.Now.ToString("HH:mm:ss.fff");
            File.AppendAllText(_path, $"[{time}] {message}\n");
        } catch { } // Ignore file errors
    }
}
