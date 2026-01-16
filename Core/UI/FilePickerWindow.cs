using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheGame.Core.Input;
using TheGame.Core.OS;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;

namespace TheGame.Core.UI;

public enum FilePickerMode {
    Open,
    Save
}

public class FilePickerWindow : Window {
    private FilePickerMode _mode;
    private Action<string> _onFilePicked;
    private string _currentPath;
    private string _defaultName;
    private string[] _fileExtensions; // Filter by file extensions (e.g., [".jpg", ".png"])
    
    // UI Elements
    private Label _pathLabel;
    private ScrollPanel _fileListPanel;
    private TextInput _fileNameInput;
    private Button _actionButton;
    private Button _cancelButton;
    private Panel _topPanel;
    private Panel _bottomPanel;
    
    private const float TopHeight = 40f;
    private const float BottomHeight = 50f;

    public FilePickerWindow(string title, string defaultPath, string defaultName, FilePickerMode mode, Action<string> onFilePicked, string[] fileExtensions = null) 
        : base(Vector2.Zero, new Vector2(600, 450)) // Default size
    {
        Title = title;
        _mode = mode;
        _onFilePicked = onFilePicked;
        _currentPath = string.IsNullOrEmpty(defaultPath) ? "C:\\" : defaultPath;
        _defaultName = defaultName ?? "";
        _fileExtensions = fileExtensions;

        // Ensure path exists, default to C:\ if not
        if (!VirtualFileSystem.Instance.Exists(_currentPath)) {
            _currentPath = "C:\\";
        }

        // Center on screen
        var viewport = G.GraphicsDevice.Viewport;
        Vector2 targetPos = new Vector2(viewport.Width / 2 - Size.X / 2, viewport.Height / 2 - Size.Y / 2);
        
        // Opening animation - start small and centered, animate to full size
        Vector2 centerPos = new Vector2(viewport.Width / 2 - Size.X / 4, viewport.Height / 2 - Size.Y / 4);
        Vector2 startSize = Size * 0.5f;
        
        Position = centerPos;
        Size = startSize;
        Opacity = 0f;
        
        // Animate to final position and size
        Core.Animation.Tweener.To(this, p => Position = p, centerPos, targetPos, 0.25f, Core.Animation.Easing.EaseOutQuad);
        Core.Animation.Tweener.To(this, s => Size = s, startSize, new Vector2(600, 450), 0.25f, Core.Animation.Easing.EaseOutQuad);
        Core.Animation.Tweener.To(this, o => Opacity = o, 0f, 1f, 0.2f, Core.Animation.Easing.Linear);

        SetupUI();
        RefreshList();
        
        // Hook resize to update layout dynamically
        OnResize += UpdateLayout;
    }
    
    private void UpdateLayout() {
        _topPanel.Size = new Vector2(ClientSize.X, TopHeight);
        _fileListPanel.Size = new Vector2(ClientSize.X, ClientSize.Y - TopHeight - BottomHeight);
        _bottomPanel.Position = new Vector2(0, ClientSize.Y - BottomHeight);
        _bottomPanel.Size = new Vector2(ClientSize.X, BottomHeight);
        _actionButton.Position = new Vector2(ClientSize.X - 160, 10);
        _cancelButton.Position = new Vector2(ClientSize.X - 80, 10);
    }

    private void SetupUI() {
        // Top Navigation Bar
        _topPanel = new Panel(new Vector2(0, 0), new Vector2(ClientSize.X, TopHeight)) {
            BackgroundColor = new Color(40, 40, 40),
            BorderThickness = 0
        };
        AddChild(_topPanel);

        var upButton = new Button(new Vector2(5, 5), new Vector2(30, 30), "^") {
            OnClickAction = NavigateUp
        };
        _topPanel.AddChild(upButton);

        _pathLabel = new Label(new Vector2(45, 10), _currentPath) {
            Color = Color.White
        };
        _topPanel.AddChild(_pathLabel);

        // File List Area
        _fileListPanel = new ScrollPanel(new Vector2(0, TopHeight), new Vector2(ClientSize.X, ClientSize.Y - TopHeight - BottomHeight));
        AddChild(_fileListPanel);

        // Bottom Action Area
        _bottomPanel = new Panel(new Vector2(0, ClientSize.Y - BottomHeight), new Vector2(ClientSize.X, BottomHeight)) {
            BackgroundColor = new Color(40, 40, 40),
            BorderThickness = 0
        };
        AddChild(_bottomPanel);

        var nameLabel = new Label(new Vector2(10, 15), "File:");
        _bottomPanel.AddChild(nameLabel);

        _fileNameInput = new TextInput(new Vector2(50, 10), new Vector2(250, 30)) {
            Value = _defaultName
        };
        _bottomPanel.AddChild(_fileNameInput);

        string actionText = _mode == FilePickerMode.Save ? "Save" : "Open";
        _actionButton = new Button(new Vector2(ClientSize.X - 160, 10), new Vector2(70, 30), actionText) {
            OnClickAction = TrySubmit
        };
        _bottomPanel.AddChild(_actionButton);

        _cancelButton = new Button(new Vector2(ClientSize.X - 80, 10), new Vector2(70, 30), "Cancel") {
            OnClickAction = Close
        };
        _bottomPanel.AddChild(_cancelButton);
    }

    private void NavigateUp() {
        string parent = Path.GetDirectoryName(_currentPath.Replace('/', '\\').TrimEnd('\\'));
        if (!string.IsNullOrEmpty(parent)) {
            _currentPath = parent;
            if (!_currentPath.EndsWith("\\")) _currentPath += "\\";
            RefreshList();
        }
    }

    private void RefreshList() {
        _fileListPanel.Children.Clear();
        _pathLabel.Text = _currentPath;
        
        float y = 0;
        float itemHeight = 30f;
        float itemWidth = _fileListPanel.Size.X - 10; // Account for scrollbar space if any

        try {
            // Directories
            var dirs = VirtualFileSystem.Instance.GetDirectories(_currentPath);
            foreach (var dir in dirs) {
                string dirName = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(dirName)) continue; // Root volume case mostly

                var btn = new Button(new Vector2(5, y), new Vector2(itemWidth, itemHeight), dirName) {
                    BackgroundColor = Color.Transparent,
                    BorderColor = Color.Transparent,
                    HoverColor = new Color(60, 60, 60),
                    TextAlign = TheGame.Core.UI.Controls.TextAlign.Left,
                    Icon = GameContent.FolderIcon
                };
                
                string fullPath = dir;
                btn.OnClickAction = () => {
                    _currentPath = fullPath;
                    if (!_currentPath.EndsWith("\\")) _currentPath += "\\";
                    RefreshList();
                };
                
                _fileListPanel.AddChild(btn);
                y += itemHeight;
            }

            // Files with filtering
            var files = VirtualFileSystem.Instance.GetFiles(_currentPath);
            foreach (var file in files) {
                string fileName = Path.GetFileName(file);
                
                // Filter by extension if specified
                if (_fileExtensions != null && _fileExtensions.Length > 0) {
                    string ext = Path.GetExtension(file).ToLower();
                    if (!_fileExtensions.Contains(ext)) continue;
                }
                
                var btn = new Button(new Vector2(5, y), new Vector2(itemWidth, itemHeight), fileName) {
                    BackgroundColor = Color.Transparent,
                    BorderColor = Color.Transparent,
                    HoverColor = new Color(60, 60, 60),
                    TextAlign = TheGame.Core.UI.Controls.TextAlign.Left,
                    Icon = Shell.GetIcon(file)
                };

                btn.OnClickAction = () => {
                    _fileNameInput.Value = fileName;
                    if (_mode == FilePickerMode.Open) {
                        // Double click emulation via click for now
                    }
                };
                
                _fileListPanel.AddChild(btn);
                y += itemHeight;
            }
        } catch (Exception ex) {
            DebugLogger.Log($"Error Refreshing FilePicker: {ex.Message}");
        }

        _fileListPanel.UpdateContentHeight(y);
    }

    private void TrySubmit() {
        string filename = _fileNameInput.Value?.Trim();
        if (string.IsNullOrEmpty(filename)) return;

        string fullPath = Path.Combine(_currentPath, filename);

        if (_mode == FilePickerMode.Open) {
            if (VirtualFileSystem.Instance.Exists(fullPath)) {
                _onFilePicked?.Invoke(fullPath);
                Close();
            } else {
                DebugLogger.Log("File not found: " + fullPath);
            }
        } else {
            // Save mode
            _onFilePicked?.Invoke(fullPath);
            Close();
        }
    }
}
