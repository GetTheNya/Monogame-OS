using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TheGame.Core.OS;

namespace NACHOS;

public static class UsageTracker {
    private static Dictionary<string, int> _sessionUsage = new();
    private static Dictionary<string, int> _persistentUsage = new();
    private static int _selectionsSinceLastSave = 0;
    private const int SaveThreshold = 10;
    
    // Call this when a project is opened
    // Call this when a project is opened
    public static void Initialize() {
        _sessionUsage.Clear();
        _persistentUsage.Clear();
        LoadPersistent();
    }
    
    public static void RecordSelection(string label) {
        if (string.IsNullOrEmpty(label)) return;
        
        // Update session usage
        if (_sessionUsage.ContainsKey(label)) {
            _sessionUsage[label]++;
        } else {
            _sessionUsage[label] = 1;
        }
        
        // Update persistent usage
        if (_persistentUsage.ContainsKey(label)) {
            _persistentUsage[label]++;
        } else {
            _persistentUsage[label] = 1;
        }
        
        // Debounced save
        _selectionsSinceLastSave++;
        if (_selectionsSinceLastSave >= SaveThreshold) {
            SavePersistent();
            _selectionsSinceLastSave = 0;
        }
    }
    
    public static int GetScore(string label) {
        int sessionCount = GetSessionCount(label);
        int persistentCount = GetPersistentCount(label);
        int totalCount = sessionCount + persistentCount;
        
        // Dynamic scaling: 1-4 uses → +10 to +40, 5+ uses → +50 to +300
        if (totalCount >= 100) return 300;
        if (totalCount >= 50) return 200;
        if (totalCount >= 20) return 150;
        if (totalCount >= 10) return 100;
        if (totalCount >= 5) return 50;
        return Math.Min(40, totalCount * 10);
    }
    
    public static int GetSessionCount(string label) {
        return _sessionUsage.TryGetValue(label, out int count) ? count : 0;
    }
    
    public static int GetPersistentCount(string label) {
        return _persistentUsage.TryGetValue(label, out int count) ? count : 0;
    }
    
    private static void LoadPersistent() {
        try {
            if (!ProjectMetadataManager.Exists("usage.json")) return;
            
            string json = ProjectMetadataManager.ReadMetadata("usage.json");
            if (!string.IsNullOrWhiteSpace(json)) {
                _persistentUsage = JsonSerializer.Deserialize<Dictionary<string, int>>(json) 
                    ?? new Dictionary<string, int>();
            }
        } catch (Exception ex) {
            // Silent fail, just log if needed
            TheGame.Core.DebugLogger.Log($"UsageTracker: Failed to load persistent data: {ex.Message}");
        }
    }
    
    private static void SavePersistent() {
        try {
            string json = JsonSerializer.Serialize(_persistentUsage, new JsonSerializerOptions { 
                WriteIndented = true 
            });
            ProjectMetadataManager.WriteMetadata("usage.json", json);
        } catch (Exception ex) {
            TheGame.Core.DebugLogger.Log($"UsageTracker: Failed to save persistent data: {ex.Message}");
        }
    }
    
    public static void Shutdown() {
        SavePersistent();
    }
    
    public static void ClearSession() {
        _sessionUsage.Clear();
    }
}
