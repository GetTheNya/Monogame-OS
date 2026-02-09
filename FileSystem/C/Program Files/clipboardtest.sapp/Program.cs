using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;

namespace ClipboardTest;

public class Program {
    public static Window CreateWindow(string[] args) {
        return new MainWindow();
    }
}

public class MainWindow : Window {
    private TextArea _textArea;
    private Label _historyLabel;

    public MainWindow() : base(new Vector2(100, 100), new Vector2(500, 400)) {
        Title = "Clipboard Test";
        AppId = "CLIPBOARDTEST";

        SetupUI();
    }

    private void SetupUI() {
        var menuBar = new MenuBar(Vector2.Zero, new Vector2(ClientSize.X, 26));
        menuBar.AddMenu("Clipboard", m => {
            m.AddItem("Copy Test Text", () => {
                Shell.Clipboard.SetText("Hello from Clipboard Test!");
            });
            m.AddItem("Copy File List", () => {
                Shell.Clipboard.SetFiles(new[] { "C:\\Windows\\System32\\notepad.sapp", "C:\\Windows\\System32\\explorer.sapp" });
            });
            m.AddItem("Clear History", () => {
                Shell.Clipboard.Clear();
            });
        });
        AddChild(menuBar);

        AddChild(new Label(new Vector2(10, 40), "Text Area (Paste here):"));
        _textArea = new TextArea(new Vector2(10, 60), new Vector2(480, 100)) {
            Placeholder = "Press Ctrl+V or use the menu in Notepad to paste..."
        };
        AddChild(_textArea);

        var copyBtn = new Button(new Vector2(10, 170), new Vector2(120, 30), "Copy TextArea") {
            OnClickAction = () => {
                Shell.Clipboard.SetText(_textArea.Text);
            }
        };
        AddChild(copyBtn);

        var pasteBtn = new Button(new Vector2(140, 170), new Vector2(120, 30), "Paste Text") {
            OnClickAction = () => {
                string text = Shell.Clipboard.GetText();
                if (text != null) _textArea.Text += text;
            }
        };
        AddChild(pasteBtn);

        AddChild(new Label(new Vector2(10, 210), "History Quick View:"));
        _historyLabel = new Label(new Vector2(10, 230), "No history") {
            TextColor = Color.Gray
        };
        AddChild(_historyLabel);

        Shell.Clipboard.OnChanged += RefreshHistoryPreview;
        RefreshHistoryPreview();
    }

    private void RefreshHistoryPreview() {
        var items = Shell.Clipboard.GetHistory();
        if (items.Count == 0) {
            _historyLabel.Text = "No history";
        } else {
            _historyLabel.Text = string.Join("\n", items.Take(5).Select(i => $"[{i.Type}] {i.PreviewText}"));
        }
    }
}
