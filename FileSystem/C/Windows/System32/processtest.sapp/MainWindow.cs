// Main Window - The primary UI for the Process Test app
using System;
using Microsoft.Xna.Framework;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;

namespace ProcessTestApp;

/// <summary>
/// Main window demonstrating process features via buttons.
/// </summary>
public class MainWindow : Window {
    public MainWindow() : base(new Vector2(100, 100), new Vector2(400, 450)) {
        Title = "Process Test";
        AppId = "PROCESSTEST";
        
        SetupUI();
    }

    private void SetupUI() {
        float y = 10;
        float buttonHeight = 35;
        float gap = 5;
        
        // Section: Process Info
        AddChild(new Label(new Vector2(10, y), "== Current Process Info ==") { TextColor = Color.Cyan });
        y += 25;
        
        var infoLabel = new Label(new Vector2(10, y), GetProcessInfo()) { 
            TextColor = Color.White,
            FontSize = 14
        };
        AddChild(infoLabel);
        y += 60;
        
        // Section: Window Management
        AddChild(new Label(new Vector2(10, y), "== Window Management ==") { TextColor = Color.Yellow });
        y += 25;
        
        AddButton("Create New Window", y, () => {
            var win = Shell.Process.CreateWindow<SecondaryWindow>();
            if (win != null) {
                Shell.UI.OpenWindow(win);
                Shell.Notifications.Show("Success", $"Created window: {win.Title}");
            } else {
                Shell.Notifications.Show("Error", "Failed to create window (no active process?)");
            }
        });
        y += buttonHeight + gap;
        
        AddButton("Show Modal Dialog", y, () => {
            var dialog = new ModalDialogWindow();
            Shell.Process.ShowModal(dialog);
        });
        y += buttonHeight + gap;
        
        // Section: Process Control
        AddChild(new Label(new Vector2(10, y), "== Process Control ==") { TextColor = Color.LimeGreen });
        y += 25;
        
        AddButton("Go To Background (5 sec)", y, () => {
            Shell.Notifications.Show("Background Mode", "App will hide and continue running for 5 seconds...");
            // Demonstrate background mode - app stays alive but hidden
            Shell.Process.GoToBackground();
        });
        y += buttonHeight + gap;
        
        AddButton("Exit Process", y, () => {
            Shell.Notifications.Show("Exiting", "Process will terminate now.");
            Shell.Process.Exit();
        });
        y += buttonHeight + gap;
        
        // Section: Process List
        AddChild(new Label(new Vector2(10, y), "== All Running Processes ==") { TextColor = Color.Orange });
        y += 25;
        
        AddButton("Show Running Processes", y, () => {
            var processes = Shell.Process.GetAll();
            string info = "";
            foreach (var p in processes) {
                string shortPid = p.ProcessId.Length > 8 ? p.ProcessId.Substring(0, 8) : p.ProcessId;
                info += $"• {p.AppId} ({shortPid}...) - {p.State}, {p.Windows.Count} windows\n";
            }
            if (string.IsNullOrEmpty(info)) info = "No processes running";
            
            // Create window owned by this process so it doesn't block main window
            var infoWindow = Shell.Process.CreateWindow<ProcessListWindow>();
            if (infoWindow != null) {
                infoWindow.SetProcessInfo(info);
                Shell.UI.OpenWindow(infoWindow);
            }
        });
        y += buttonHeight + gap;
        
        // Section: Registry Startup
        AddChild(new Label(new Vector2(10, y), "== Startup Management ==") { TextColor = Color.Magenta });
        y += 25;
        
        AddButton("Add to Startup", y, () => {
            Registry.SetValue("Startup\\PROCESSTEST", true);
            Shell.Notifications.Show("Startup", "PROCESSTEST added to startup apps!");
        });
        y += buttonHeight + gap;
        
        AddButton("Remove from Startup", y, () => {
            Registry.SetValue("Startup\\PROCESSTEST", false);
            Shell.Notifications.Show("Startup", "PROCESSTEST removed from startup apps.");
        });
    }
    
    private void AddButton(string text, float y, Action onClick) {
        var btn = new Button(new Vector2(20, y), new Vector2(ClientSize.X - 40, 35), text) {
            BackgroundColor = new Color(50, 50, 60),
            HoverColor = new Color(70, 70, 85)
        };
        btn.OnClickAction = onClick;
        AddChild(btn);
    }
    
    private string GetProcessInfo() {
        var process = Shell.Process.Current;
        if (process == null) return "No process context\n(App is still initializing)";
        
        string shortPid = process.ProcessId.Length > 8 ? process.ProcessId.Substring(0, 8) : process.ProcessId;
        return $"AppId: {process.AppId}\n" +
               $"ProcessId: {shortPid}...\n" +
               $"State: {process.State}\n" +
               $"Windows: {process.Windows.Count}";
    }
}
