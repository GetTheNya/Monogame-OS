using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using TheGame.Core;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.Input;
using TheGame.Graphics;
using TheGame;
using TheGame.Core.OS.Terminal;

namespace TerminalApp;

public class Program : Application {
    public static Program CreateWindow() => new Program();

    protected override void OnLoad(string[] args) {
        var win = new TerminalWindow(new Vector2(100, 100), new Vector2(700, 450));
        MainWindow = win; 
    }
}

public class TerminalWindow : Window {
    private TerminalControl _terminal;

    public TerminalWindow(Vector2 position, Vector2 size) : base(position, size) {
        ShowInTaskbar = true;
        Title = "Terminal";
    }

    protected override void OnLoad() {
        _terminal = new TerminalControl(Vector2.Zero, ClientSize);
        AddChild(_terminal);
        _terminal.LoadSettings(OwnerProcess);

        Shell.Hotkeys.RegisterLocal(OwnerProcess, Keys.C, HotkeyModifiers.Ctrl, () => {
            DebugLogger.Log("Terminal: Ctrl+C Hotkey triggered");
            if (_terminal.Backend.IsProcessRunning) {
                DebugLogger.Log("Terminal: Ctrl+C Hotkey sended");
                _terminal.Backend.SendSignal("CTRL+C");
            }
        }, rewriteSystemHotkey:true);
        
        OnResize += () => {
            _terminal.Size = ClientSize;
        };
        
        OnCloseRequested += (callback) => {
            if (_terminal.Backend.IsProcessRunning) {
                var mb = new TheGame.Core.UI.MessageBox("Warning", "A process is still running. Do you want to terminate it and exit?", MessageBoxButtons.YesNo, (confirmed) => {
                    if (confirmed) {
                        _terminal.Backend.TerminateActiveProcess();
                        callback(true);
                    } else {
                        callback(false);
                    }
                });
                Shell.UI.OpenWindow(mb);
                return;
            }
            callback(true);
        };
    }

    protected override void OnUpdate(GameTime gameTime) {
        string dir = _terminal.CurrentDirectory;
        Title = $"Terminal - {dir}";
    }

    public override void Terminate() {
        //Terminate opened process
        base.Terminate();
    }
}
