using System;
using System.Linq;
using Microsoft.Xna.Framework;
using TheGame;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;

namespace NotepadApp;

public class AppSettings {
    // App-specific settings only
}

public class Program {
    static Program() {
        // Register file type associations - AppId is auto-detected
        foreach (var extension in NotepadWindow.SupportedExtensions) {
            Shell.File.RegisterFileTypeHandler(extension);
        }
    }

    public static Window CreateWindow(string[] args) {
        var settings = Shell.AppSettings.Load<AppSettings>();
        string filePath = args != null && args.Length > 0 ? args[0] : null;
        return new NotepadWindow(new Vector2(100, 100), new Vector2(700, 500), settings, filePath);
    }
}

public class NotepadWindow : Window {
    public static readonly string[] SupportedExtensions = new[] { ".txt", ".log", ".json", ".cs" };

    private AppSettings _settings;
    private TextArea _textArea;
    private MenuBar _menuBar;
    private string _currentFilePath = null;
    private bool _isModified = false;
    private const float MenuBarHeight = 26f;

    public NotepadWindow(Vector2 pos, Vector2 size, AppSettings settings, string filePath = null) : base(pos, size) {
        Title = "Untitled - Notepad";
        _settings = settings;
        AppId = "NOTEPAD"; // Required for automatic persistence
        _filePath = filePath;

        OnResize += () => LayoutUI();
    }
    
    private string _filePath;
    
    protected override void OnLoad() {
        SetupUI();

        // Load file if provided
        if (!string.IsNullOrEmpty(_filePath) && VirtualFileSystem.Instance.Exists(_filePath)) {
            LoadFile(_filePath);
        }
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
            m.AddItem("Cut", () => _textArea.Cut(), "Ctrl+X");
            m.AddItem("Copy", () => _textArea.Copy(), "Ctrl+C");
            m.AddItem("Paste", () => _textArea.Paste(), "Ctrl+V");
            m.AddItem("Delete", () => { if (_textArea.HasSelection()) _textArea.DeleteSelection(); }, "Del");
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
                Shell.Notifications.Show("Notepad", "A simple text editor for TheGame OS.");
            });
        });

        AddChild(_menuBar);

        _menuBar.RegisterHotkeys(OwnerProcess);

        // Text Area
        _textArea = new TextArea(new Vector2(0, MenuBarHeight), new Vector2(ClientSize.X, ClientSize.Y - MenuBarHeight)) {
            Placeholder = "Start typing...",
            DrawBackground = false  // Allow window blur to show through
        };
        _textArea.OnValueChanged += (text) => {
            if (!_isModified) {
                _isModified = true;
                UpdateTitle();
            }
        };
        AddChild(_textArea);
    }

    private void LayoutUI() {
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

    public void LoadFile(string path) {
        try {
            string content = VirtualFileSystem.Instance.ReadAllText(path);
            _textArea.Text = content ?? "";
            _currentFilePath = path;
            _isModified = false;
            UpdateTitle();
        } catch (Exception ex) {
            Shell.Notifications.Show("Error", $"Failed to open file: {ex.Message}");
        }
    }

    private void OpenFile() {
        var picker = new FilePickerWindow(
            "Select file",
            "C:\\",
            "",
            FilePickerMode.Open,
            (selectedPath) => {
                LoadFile(selectedPath);
            },
            SupportedExtensions
        );
        Shell.UI.OpenWindow(picker, owner: this.OwnerProcess);
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

        var picker = new FilePickerWindow(
            "Save As",
            "C:\\",
            defaultName,
            FilePickerMode.Save,
            (selectedPath) => {
                if (string.IsNullOrEmpty(selectedPath)) return;
                DoSave(selectedPath);
            },
            SupportedExtensions
        );
        Shell.UI.OpenWindow(picker, owner: this.OwnerProcess);
    }

    private void DoSave(string path) {
        try {
            VirtualFileSystem.Instance.WriteAllText(path, _textArea.Text);
            _currentFilePath = path;
            _isModified = false;
            UpdateTitle();
            Shell.Notifications.Show("Notepad", $"Saved: {System.IO.Path.GetFileName(path)}");
        } catch (Exception ex) {
            Shell.Notifications.Show("Error", $"Failed to save: {ex.Message}");
        }
    }

    private void SelectAll() {
        _textArea.SelectAll();
    }
}
