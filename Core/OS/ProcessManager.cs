using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace TheGame.Core.OS;

/// <summary>
/// Manages all running processes in the OS.
/// </summary>
public class ProcessManager {
    private static ProcessManager _instance;
    public static ProcessManager Instance => _instance ??= new ProcessManager();
    
    private readonly Dictionary<string, Process> _processes = new();
    
    private ProcessManager() { }
    
    /// <summary>
    /// Starts a new process for the given app.
    /// </summary>
    public Process StartProcess(string appId, string[] args = null) {
        if (string.IsNullOrEmpty(appId)) return null;
        
        string upperAppId = appId.ToUpper();
        
        // Create new process via AppLoader (single instance check is done in CreateAppWindow)
        var window = Shell.UI.CreateAppWindow(upperAppId, args ?? Array.Empty<string>());
        if (window == null) {
            // Might be single instance - check if process exists
            var existing = GetProcessesByApp(upperAppId).FirstOrDefault(p => p.State != ProcessState.Terminated);
            return existing;
        }
        
        // Open the window
        Shell.UI.OpenWindow(window);
        
        // Get or create process for this window
        var process = window.OwnerProcess;
        if (process == null) {
            // Legacy window without process - create wrapper
            process = new Process { AppId = upperAppId };
            process.Windows.Add(window);
            process.MainWindow = window;
            window.OwnerProcess = process;
            RegisterProcess(process);
        }
        
        return process;
    }
    
    /// <summary>
    /// Gets a process by its ProcessId.
    /// </summary>
    public Process GetProcess(string processId) {
        if (string.IsNullOrEmpty(processId)) return null;
        _processes.TryGetValue(processId, out var process);
        return process;
    }
    
    /// <summary>
    /// Gets all processes for a given app ID.
    /// </summary>
    public List<Process> GetProcessesByApp(string appId) {
        if (string.IsNullOrEmpty(appId)) return new List<Process>();
        string upperAppId = appId.ToUpper();
        return _processes.Values
            .Where(p => p.AppId?.ToUpper() == upperAppId && p.State != ProcessState.Terminated)
            .ToList();
    }
    
    /// <summary>
    /// Gets all running processes.
    /// </summary>
    public IEnumerable<Process> GetAllProcesses() {
        return _processes.Values.Where(p => p.State != ProcessState.Terminated);
    }
    
    /// <summary>
    /// Terminates a process by its ProcessId.
    /// </summary>
    public void TerminateProcess(string processId) {
        var process = GetProcess(processId);
        process?.Terminate();
    }
    
    /// <summary>
    /// Updates all running processes.
    /// </summary>
    public void Update(GameTime gameTime) {
        var toRemove = new List<string>();
        
        foreach (var kvp in _processes) {
            var process = kvp.Value;
            
            if (process.State == ProcessState.Terminated) {
                toRemove.Add(kvp.Key);
                continue;
            }
            
            // Update process state based on window visibility
            process.UpdateState();
            
            // Foreground processes (with visible windows) always update at full rate
            if (process.State == ProcessState.Running) {
                process.OnUpdate(gameTime);
                continue;
            }
            
            // Background processes use priority throttling
            if (process.State == ProcessState.Background) {
                if (process.UpdateInterval <= 0) {
                    // High priority - every frame
                    process.OnUpdate(gameTime);
                } else {
                    process.UpdateAccumulator += gameTime.ElapsedGameTime.TotalSeconds;
                    if (process.UpdateAccumulator >= process.UpdateInterval) {
                        // Create a virtual GameTime that accounts for all time passed since last update
                        // This fixed the bug where throttled processes received only a single frame's time
                        var virtualGameTime = new GameTime(gameTime.TotalGameTime, TimeSpan.FromSeconds(process.UpdateAccumulator));
                        process.UpdateAccumulator = 0;
                        process.OnUpdate(virtualGameTime);
                    }
                }
            }
        }
        
        // Clean up terminated processes
        foreach (var key in toRemove) {
            _processes.Remove(key);
        }
    }
    
    /// <summary>
    /// Loads and starts apps registered for startup.
    /// </summary>
    public void LoadStartupApps() {
        try {
            var startupApps = Registry.GetAllValues<bool>("Startup");
            foreach (var kvp in startupApps) {
                if (kvp.Value) {
                    DebugLogger.Log($"Starting startup app: {kvp.Key}");
                    StartProcess(kvp.Key);
                }
            }
        } catch (Exception ex) {
            DebugLogger.Log($"Error loading startup apps: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Registers a process with the manager.
    /// </summary>
    internal void RegisterProcess(Process process) {
        if (process == null || string.IsNullOrEmpty(process.ProcessId)) return;
        _processes[process.ProcessId] = process;
        process.State = ProcessState.Running;
        DebugLogger.Log($"Process registered: {process.AppId} ({process.ProcessId})");
    }
    
    internal void UnregisterProcess(Process process) {
        if (process == null || string.IsNullOrEmpty(process.ProcessId)) return;
        _processes.Remove(process.ProcessId);
        DebugLogger.Log($"Process unregistered: {process.AppId} ({process.ProcessId})");
    }
}
