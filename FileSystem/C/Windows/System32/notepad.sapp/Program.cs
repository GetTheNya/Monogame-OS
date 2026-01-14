using System;
using Microsoft.Xna.Framework;
using TheGame;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;

namespace NotepadApp;

public class AppSettings {
    public float WindowX { get; set; } = 200;
    public float WindowY { get; set; } = 150;
    public float WindowWidth { get; set; } = 700;
    public float WindowHeight { get; set; } = 500;
}

public class Program {
    public static Window CreateWindow() {
        var settings = Shell.LoadSettings<AppSettings>();
        var window = new NotepadWindow(
            new Vector2(settings.WindowX, settings.WindowY),
            new Vector2(settings.WindowWidth, settings.WindowHeight),
            settings
        );
        return window;
    }
}

public class NotepadWindow : Window {
    private AppSettings _settings;
    private TextArea _textArea;
    private MenuBar _menuBar;
    private string _currentFilePath = null;
    private bool _isModified = false;
    private bool _needsSave = false;
    private const float MenuBarHeight = 26f;

    public NotepadWindow(Vector2 pos, Vector2 size, AppSettings settings) : base(pos, size) {
        Title = "Untitled - Notepad";
        _settings = settings;

        SetupUI();

        OnResize += () => {
            LayoutUI();
            _settings.WindowWidth = Size.X;
            _settings.WindowHeight = Size.Y;
            _needsSave = true;
        };

        OnMove += () => {
            if (Opacity < 0.9f) return;
            _settings.WindowX = Position.X;
            _settings.WindowY = Position.Y;
            _needsSave = true;
        };
    }

    private void SetupUI() {
        // Menu Bar
        _menuBar = new MenuBar(Vector2.Zero, new Vector2(ClientSize.X, MenuBarHeight));

        _menuBar.AddMenu("File", m => {
            m.AddItem("New", NewFile, "Ctrl+N");
            m.AddItem("Open...", OpenFile, "Ctrl+O");
            m.AddItem("Save", SaveFile, "Ctrl+S");
            m.AddItem("Save As...", SaveFileAs);
            m.AddSeparator();
            m.AddItem("Exit", Close);
        });

        _menuBar.AddMenu("Edit", m => {
            m.AddItem("Undo", () => { /* TODO */ }, "Ctrl+Z");
            m.AddSeparator();
            m.AddItem("Cut", () => { /* TODO */ }, "Ctrl+X");
            m.AddItem("Copy", () => { /* TODO */ }, "Ctrl+C");
            m.AddItem("Paste", () => { /* TODO */ }, "Ctrl+V");
            m.AddItem("Delete", () => { /* TODO */ }, "Del");
            m.AddSeparator();
            m.AddItem("Select All", SelectAll, "Ctrl+A");
        });

        _menuBar.AddMenu("View", m => {
            m.AddItem("Word Wrap", () => { /* TODO */ });
            m.AddItem("Zoom In", () => { _textArea.FontSize += 2; });
            m.AddItem("Zoom Out", () => { if (_textArea.FontSize > 8) _textArea.FontSize -= 2; });
        });

        _menuBar.AddMenu("Help", m => {
            m.AddItem("About Notepad", () => {
                NotificationManager.Instance.ShowNotification("Notepad", "A simple text editor for TheGame OS.");
            });
        });

        AddChild(_menuBar);

        // Text Area
        _textArea = new TextArea(new Vector2(0, MenuBarHeight), new Vector2(ClientSize.X, ClientSize.Y - MenuBarHeight)) {
            Placeholder = "Start typing...",
            DrawBackground = false  // Allow window blur to show through
        };
        _textArea.OnTextChanged += (text) => {
            if (!_isModified) {
                _isModified = true;
                UpdateTitle();
            }
        };
        AddChild(_textArea);
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        if (_needsSave && !TheGame.Core.Input.InputManager.IsMouseButtonDown(TheGame.Core.Input.MouseButton.Left)) {
            Shell.SaveSettings(_settings);
            _needsSave = false;
        }
    }

    private void LayoutUI() {
        _menuBar.Size = new Vector2(ClientSize.X, MenuBarHeight);
        _textArea.Position = new Vector2(0, MenuBarHeight);
        _textArea.Size = new Vector2(ClientSize.X, ClientSize.Y - MenuBarHeight);
    }

    private void UpdateTitle() {
        string filename = string.IsNullOrEmpty(_currentFilePath)
            ? "Untitled"
            : System.IO.Path.GetFileName(_currentFilePath);
        Title = (_isModified ? "*" : "") + filename + " - Notepad";
    }

    private void NewFile() {
        _currentFilePath = null;
        _textArea.Text = "";
        _isModified = false;
        UpdateTitle();
    }

    private void OpenFile() {
        Shell.PickFile("Open", "C:\\Users\\Admin\\Documents", (path) => {
            if (string.IsNullOrEmpty(path)) return;
            try {
                string content = VirtualFileSystem.Instance.ReadAllText(path);
                _textArea.Text = content ?? "";
                _currentFilePath = path;
                _isModified = false;
                UpdateTitle();
            } catch (Exception ex) {
                NotificationManager.Instance.ShowNotification("Error", $"Failed to open file: {ex.Message}");
            }
        });
    }

    private void SaveFile() {
        if (string.IsNullOrEmpty(_currentFilePath)) {
            SaveFileAs();
        } else {
            DoSave(_currentFilePath);
        }
    }

    private void SaveFileAs() {
        string defaultName = string.IsNullOrEmpty(_currentFilePath)
            ? "Untitled.txt"
            : System.IO.Path.GetFileName(_currentFilePath);

        Shell.SaveFile("Save As", "C:\\Users\\Admin\\Documents", defaultName, (path) => {
            if (!string.IsNullOrEmpty(path)) {
                DoSave(path);
            }
        });
    }

    private void DoSave(string path) {
        try {
            VirtualFileSystem.Instance.WriteAllText(path, _textArea.Text);
            _currentFilePath = path;
            _isModified = false;
            UpdateTitle();
            NotificationManager.Instance.ShowNotification("Notepad", $"Saved: {System.IO.Path.GetFileName(path)}");
        } catch (Exception ex) {
            NotificationManager.Instance.ShowNotification("Error", $"Failed to save: {ex.Message}");
        }
    }

    private void SelectAll() {
        _textArea.SelectAll();
    }
}
