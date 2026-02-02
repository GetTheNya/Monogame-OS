using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core;
using TheGame.Graphics;

namespace ProcessManagerApp;

public class MainWindow : Window {
    private TextInput _searchBox;
    private ScrollPanel _scrollPanel;
    private List<ProcessRow> _rows = new();
    private HashSet<string> _expandedIds = new();
    private string _filter = "";
    private float _refreshTimer = 0;
    private const float RefreshInterval = 1.0f;

    private Panel _headerPanel;

    public MainWindow() {
        Title = "Process Manager";
        Size = new Vector2(600, 600);
    }

    protected override void OnLoad() {
        // Search Header
        var searchLabel = new Label(new Vector2(10, 10), "Search:") { Color = Color.Gray };
        AddChild(searchLabel);

        _searchBox = new TextInput(new Vector2(80, 5), new Vector2(ClientSize.X - 90, 30)) {
            Placeholder = "Process name, PID, or window title..."
        };
        _searchBox.OnValueChanged += (val) => {
            _filter = val.ToLower();
            RefreshList(force: true);
        };
        AddChild(_searchBox);

        // Column Header
        _headerPanel = new Panel(new Vector2(0, 40), new Vector2(ClientSize.X, 25)) {
            BackgroundColor = new Color(35, 35, 35),
            BorderThickness = 0
        };
        AddChild(_headerPanel);

        var lblProc = new Label(new Vector2(35, 5), "PROCESS") { FontSize = 12, Color = Color.Gray };
        var lblType = new Label(new Vector2(ClientSize.X - 240, 5), "TYPE") { FontSize = 12, Color = Color.Gray, Name = "HeaderType" };
        var lblPri = new Label(new Vector2(ClientSize.X - 150, 5), "PRIORITY") { FontSize = 12, Color = Color.Gray, Name = "HeaderPriority" };
        var lblStat = new Label(new Vector2(ClientSize.X - 70, 5), "STATUS") { FontSize = 12, Color = Color.Gray, Name = "HeaderStatus" };
        _headerPanel.AddChild(lblProc);
        _headerPanel.AddChild(lblType);
        _headerPanel.AddChild(lblPri);
        _headerPanel.AddChild(lblStat);

        // Scroll Panel for list
        _scrollPanel = new ScrollPanel(new Vector2(0, 65), new Vector2(ClientSize.X, ClientSize.Y - 65)) {
            BackgroundColor = new Color(20, 20, 20, 100)
        };
        AddChild(_scrollPanel);

        OnResize = HandleResize;
        RefreshList(force: true);
    }

    private void HandleResize() {
        if (_searchBox != null) _searchBox.Size = new Vector2(ClientSize.X - 90, 30);
        if (_headerPanel != null) {
            _headerPanel.Size = new Vector2(ClientSize.X, 25);
            var lblType = _headerPanel.GetChild<Label>("HeaderType");
            var lblPri = _headerPanel.GetChild<Label>("HeaderPriority");
            var lblStat = _headerPanel.GetChild<Label>("HeaderStatus");
            if (lblType != null) lblType.Position = new Vector2(ClientSize.X - 240, 5);
            if (lblPri != null) lblPri.Position = new Vector2(ClientSize.X - 150, 5);
            if (lblStat != null) lblStat.Position = new Vector2(ClientSize.X - 70, 5);
        }
        if (_scrollPanel != null) _scrollPanel.Size = new Vector2(ClientSize.X, ClientSize.Y - 65);
        
        UpdateLayout();
    }

    protected override void OnUpdate(GameTime gameTime) {
        _refreshTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_refreshTimer >= RefreshInterval) {
            _refreshTimer = 0;
            RefreshList();
        }
    }

    public void RefreshList(bool force = false) {
        var allProcesses = ProcessManager.Instance.GetAllProcesses().ToList();
        
        // Filter processes
        var filtered = allProcesses.Where(p => {
            if (string.IsNullOrEmpty(_filter)) return true;
            if (p.AppId.ToLower().Contains(_filter)) return true;
            if (p.ProcessId.ToLower().Contains(_filter)) return true;
            if (p.Windows.Any(w => w.Title.ToLower().Contains(_filter))) return true;
            return false;
        }).ToList();

        // Delta check for UI rebuild
        var currentIds = filtered.Select(p => p.ProcessId).OrderBy(id => id).ToList();
        var existingIds = _rows.Select(r => r.Process.ProcessId).OrderBy(id => id).ToList();

        if (force || !currentIds.SequenceEqual(existingIds)) {
            RebuildUI(filtered);
        } else {
            // Just update dynamic status in existing rows
            foreach (var row in _rows) {
                row.UpdateStatus();
            }
        }
    }

    private void RebuildUI(List<Process> processes) {
        _scrollPanel.ClearChildren();
        _rows.Clear();

        foreach (var p in processes) {
            var row = new ProcessRow(p, Vector2.Zero, ClientSize.X - 10, this);
            if (_expandedIds.Contains(p.ProcessId)) {
                row.IsExpanded = true;
            }
            _scrollPanel.AddChild(row);
            _rows.Add(row);
        }
        UpdateLayout();
    }

    public void UpdateLayout() {
        float y = 5;
        foreach (var row in _rows) {
            row.Position = new Vector2(5, y);
            row.Size = new Vector2(ClientSize.X - 10, row.Size.Y);
            y += row.TotalHeight + 5;
        }
    }

    public void OnExpansionChanged(ProcessRow row) {
        if (row.IsExpanded) {
            _expandedIds.Add(row.Process.ProcessId);
        } else {
            _expandedIds.Remove(row.Process.ProcessId);
        }
        UpdateLayout();
    }
}

public class WindowRow : Panel {
    private WindowBase _window;
    private Icon _icon;
    private Label _label;
    private Button _focusBtn;

    public WindowRow(WindowBase window, Vector2 pos, float width) : base(pos, new Vector2(width, 28)) {
        _window = window;

        BackgroundColor = new Color(30, 30, 30, 150);
        CornerRadius = 4;
        
        SetupUI();
    }

    private void SetupUI() {
        // Hierarchy lines would be drawn by parent panel or with a dedicated control if needed, 
        // but for now let's just use indentation and icons.
        _icon = new Icon(new Vector2(10, 4), new Vector2(20, 20), GameContent.FileIcon) { Tint = Color.LightSkyBlue * 0.8f };
        AddChild(_icon);

        _label = new Label(new Vector2(35, 4), _window.Title) {
            FontSize = 14,
            Color = Color.LightGray
        };
        AddChild(_label);

        _focusBtn = new Button(new Vector2(Size.X - 60, 4), new Vector2(50, 20), "Focus") {
            FontSize = 12,
            BackgroundColor = new Color(50, 50, 50),
            OnClickAction = () => Shell.UI.OpenWindow(_window)
        };
        AddChild(_focusBtn);
    }

    protected override void UpdateInput() {
        base.UpdateInput();
        BackgroundColor = IsMouseOver ? new Color(60, 60, 60, 180) : new Color(30, 30, 30, 150);
        _label.Color = IsMouseOver ? Color.White : Color.LightGray;
    }

    public override void PopulateContextMenu(ContextMenuContext context, List<MenuItem> items) {
        items.Add(new MenuItem { 
            Text = $"Window: {_window.Title}", 
            IsEnabled = false,
            Icon = GameContent.FileIcon // Assuming this exists as per previous check
        });

        items.Add(new MenuItem { 
            Text = "Focus", 
            Action = () => Shell.UI.OpenWindow(_window)
        });

        items.Add(new MenuItem { 
            Text = "Close", 
            Action = () => _window.Close()
        });

        items.Add(new MenuItem { Type = MenuItemType.Separator });

        base.PopulateContextMenu(context, items);
    }
}

public class ProcessRow : Panel {
    public Process Process { get; }
    private MainWindow _owner;
    private Label _nameLabel;
    private Label _typeLabel;
    private Label _statusLabel;
    private Label _priorityLabel;
    private Button _expandBtn;
    private Panel _windowsPanel;
    private bool _expanded = false;
    public bool IsExpanded {
        get => _expanded;
        set {
            if (_expanded == value) return;
            _expanded = value;
            OnExpansionChanged();
        }
    }
    public float TotalHeight => _expanded ? 35 + (_windowsPanel?.Size.Y ?? 0) : 35;

    public ProcessRow(Process process, Vector2 pos, float width, MainWindow owner) : base(pos, new Vector2(width, 35)) {
        Process = process;
        _owner = owner;
        BackgroundColor = new Color(45, 45, 45);
        BorderColor = Color.Gray * 0.2f;
        BorderThickness = 1;
        CornerRadius = 6;
        ConsumesInput = true;

        SetupUI();
        OnResize = UpdateInternalLayout;
    }

    protected override void UpdateInput() {
        base.UpdateInput();
        
        if (IsMouseOver) {
            BackgroundColor = new Color(70, 70, 70);
            BorderColor = Color.LightSkyBlue * 0.4f;
            BorderThickness = 2;
        } else {
            BackgroundColor = new Color(45, 45, 45);
            BorderColor = Color.Gray * 0.2f;
            BorderThickness = 1;
        }
    }

    private void SetupUI() {
        _expandBtn = new Button(new Vector2(5, 7), new Vector2(20, 20), "▶") {
            BackgroundColor = Color.Transparent,
            BorderColor = Color.Transparent,
            OnClickAction = ToggleExpand,
            IsVisible = Process.Windows.Count > 0
        };
        AddChild(_expandBtn);

        var iconTex = Process.Icon ?? GameContent.FolderIcon;
        AddChild(new Icon(new Vector2(30, 7), new Vector2(22, 22), iconTex));

        _nameLabel = new Label(new Vector2(58, 7), $"{Process.AppId} ({Process.ProcessId.Substring(0, 8)})") {
            FontSize = 16,
            Color = Color.White
        };
        AddChild(_nameLabel);

        _typeLabel = new Label(new Vector2(Size.X - 240, 7), GetProcessType()) {
            FontSize = 14,
            Color = Color.LightSteelBlue
        };
        AddChild(_typeLabel);

        _priorityLabel = new Label(new Vector2(Size.X - 150, 7), $"[{Process.Priority}]") {
            FontSize = 14,
            Color = Color.Gray
        };
        AddChild(_priorityLabel);

        _statusLabel = new Label(new Vector2(Size.X - 70, 7), Process.State.ToString()) {
            FontSize = 14,
            Color = GetStateColor(Process.State)
        };
        AddChild(_statusLabel);
    }

    private void UpdateInternalLayout() {
        if (_typeLabel != null) _typeLabel.Position = new Vector2(Size.X - 240, 7);
        if (_priorityLabel != null) _priorityLabel.Position = new Vector2(Size.X - 150, 7);
        if (_statusLabel != null) _statusLabel.Position = new Vector2(Size.X - 70, 7);
        if (_windowsPanel != null) _windowsPanel.Size = new Vector2(Size.X - 40, _windowsPanel.Size.Y);
    }

    private string GetProcessType() {
        if (Process.Application is TerminalApplication) return "Terminal";
        if (Process.State == ProcessState.Background) return "Background";
        return "GUI";
    }

    private Color GetStateColor(ProcessState state) => state switch {
        ProcessState.Running => Color.LimeGreen,
        ProcessState.Background => Color.Orange,
        ProcessState.Starting => Color.Cyan,
        _ => Color.Gray
    };

    private void ToggleExpand() {
        IsExpanded = !IsExpanded;
    }

    private void OnExpansionChanged() {
        _expandBtn.Text = _expanded ? "▼" : "▶";
        
        if (_expanded) {
            _windowsPanel = new Panel(new Vector2(30, 35), new Vector2(Size.X - 40, 0)) {
                BackgroundColor = Color.Transparent,
                BorderThickness = 0
            };
            AddChild(_windowsPanel);
            RefreshWindows();
        } else {
            if (_windowsPanel != null) {
                RemoveChild(_windowsPanel);
                _windowsPanel = null;
            }
            Size = new Vector2(Size.X, 35);
        }

        _owner.OnExpansionChanged(this);
    }

    private void RefreshWindows() {
        if (_windowsPanel == null) return;
        _windowsPanel.ClearChildren();
        
        float y = 5;
        foreach (var win in Process.Windows) {
            var winRow = new WindowRow(win, new Vector2(10, y), _windowsPanel.Size.X - 15);
            _windowsPanel.AddChild(winRow);
            y += 32;
        }
        _windowsPanel.Size = new Vector2(_windowsPanel.Size.X, y + 5);
        Size = new Vector2(Size.X, 35 + _windowsPanel.Size.Y);
    }

    public void UpdateStatus() {
        _expandBtn.IsVisible = Process.Windows.Count > 0;
        _statusLabel.Text = Process.State.ToString();
        _statusLabel.Color = GetStateColor(Process.State);
        _typeLabel.Text = GetProcessType();
        _priorityLabel.Text = $"[{Process.Priority}]";
        
        UpdateInternalLayout();
        
        if (_expanded) {
            RefreshWindows();
        }
    }

    public override void PopulateContextMenu(ContextMenuContext context, List<MenuItem> items) {
        bool isTargetingChild = context.Target != this;
        
        if (!isTargetingChild) {
            items.Add(new MenuItem { Text = Process.AppId, IsEnabled = false, Icon = Process.Icon ?? GameContent.FolderIcon });
            items.Add(new MenuItem { Type = MenuItemType.Separator });
        }

        // Priority Submenu
        items.Add(new MenuItem {
            Text = "Priority",
            Icon = GameContent.DiskIcon,
            SubItems = new List<MenuItem> {
                CreatePriorityItem("High", ProcessPriority.High),
                CreatePriorityItem("Normal", ProcessPriority.Normal),
                CreatePriorityItem("Low", ProcessPriority.Low)
            }
        });

        if (!isTargetingChild) {
            items.Add(new MenuItem {
                Text = "Bring All to Front",
                Action = () => {
                    foreach (var win in Process.Windows) {
                        Shell.UI.OpenWindow(win);
                    }
                }
            });
        }

        items.Add(new MenuItem { Type = MenuItemType.Separator });
        items.Add(new MenuItem {
            Text = "Terminate",
            Action = () => {
                var mb = new MessageBox("Terminate Process", 
                    $"Are you sure you want to terminate {Process.AppId}?\nUnsaved data will be lost.", 
                    MessageBoxButtons.YesNo, (confirmed) => {
                        if (confirmed) {
                            Process.Terminate();
                            _owner.RefreshList(force: true);
                        }
                    });
                Shell.UI.OpenWindow(mb);
            }
        });

        base.PopulateContextMenu(context, items);
    }

    private MenuItem CreatePriorityItem(string text, ProcessPriority priority) {
        return new MenuItem {
            Text = text,
            Type = MenuItemType.Checkbox,
            IsChecked = Process.Priority == priority,
            Action = () => {
                Process.Priority = priority;
                Shell.Notifications.Show("Process Priority", $"{Process.AppId} set to {priority}");
            }
        };
    }
}
