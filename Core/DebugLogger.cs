using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace TheGame.Core;

public static class DebugLogger {
    private static string _path = "debug_log.txt";
    private static List<string> _buffer = new List<string>();
    private static float _timeSinceFlush = 0f;
    private const float FLUSH_INTERVAL = 2f; // Flush every 2 seconds
    private const int MAX_BUFFER_SIZE = 100; // Or flush if buffer gets too large
    private static bool _isWriting = false;

    static DebugLogger() {
        try {
            if (File.Exists(_path)) {
                File.Delete(_path);
            }
            File.Create(_path).Close();
        } catch { }
    }

    public static void Log(string message) {
        try {
            var time = DateTime.Now.ToString("HH:mm:ss.fff");
            lock (_buffer) {
                _buffer.Add($"[{time}] {message}");
            }
        } catch { }
    }
    
    public static void Update(float deltaTime) {
        if (_isWriting) return; // Don't queue another write if one is in progress
        
        _timeSinceFlush += deltaTime;
        
        bool shouldFlush = _timeSinceFlush >= FLUSH_INTERVAL;
        
        List<string> toWrite = null;
        lock (_buffer) {
            if (_buffer.Count >= MAX_BUFFER_SIZE) shouldFlush = true;
            if (!shouldFlush || _buffer.Count == 0) return;
            
            toWrite = new List<string>(_buffer);
            _buffer.Clear();
            _timeSinceFlush = 0f;
        }
        
        if (toWrite != null && toWrite.Count > 0) {
            _isWriting = true;
            Task.Run(() => {
                try {
                    File.AppendAllLines(_path, toWrite);
                } catch { }
                finally {
                    _isWriting = false;
                }
            });
        }
    }
    
    public static void Flush() {
        lock (_buffer) {
            if (_buffer.Count == 0) return;
            try {
                File.AppendAllLines(_path, _buffer);
                _buffer.Clear();
            } catch { }
        }
    }
}

