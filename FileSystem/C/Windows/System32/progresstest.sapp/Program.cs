using System;
using Microsoft.Xna.Framework;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;

namespace ProgressTest;

public class Program : Window {
    public static Window CreateProcess(string[] args) {
        return new Program();
    }

    private float _progress = 0f;
    private Button _startButton;
    private bool _running = false;
    private ProgressBar _progressBar;

    public Program() : base(new Vector2(100, 100), new Vector2(300, 250)) {
        InitUI();
    }

    private void InitUI() {
        var panel = new Panel(Vector2.Zero, Size);
        AddChild(panel);

        _progressBar = new ProgressBar(new Vector2(10, 10), new Vector2(280, 40)) {
            TextFormat = "Downloading: {0}%",
            FillPadding = 3f,
            ProgressColor = Color.LightBlue
        };
        panel.AddChild(_progressBar);

        _startButton = new Button(new Vector2(10, 60), new Vector2(280, 40), "Start Progress") {
            OnClickAction = () => {
                _running = !_running;
                _startButton.Text = _running ? "Pause Progress" : "Resume Progress";
            }
        };
        panel.AddChild(_startButton);

        var resetButton = new Button(new Vector2(10, 110), new Vector2(280, 40), "Reset Progress") {
            OnClickAction = () => {
                _progress = 0f;
                _running = false;
                _startButton.Text = "Start Progress";
                _progressBar.Value = 0f;
                Shell.Taskbar.SetProgress(OwnerProcess, -1.0f);
            }
        };
        panel.AddChild(resetButton);

        var colorButton = new Button(new Vector2(10, 160), new Vector2(280, 40), "Toggle Color") {
            OnClickAction = () => {
                if (_progressBar.ProgressColor == Color.LightBlue) _progressBar.ProgressColor = Color.LimeGreen;
                else if (_progressBar.ProgressColor == Color.LimeGreen) _progressBar.ProgressColor = Color.Orange;
                else _progressBar.ProgressColor = Color.LightBlue;

                OwnerProcess.ProgressColor = _progressBar.ProgressColor;
            }
        };
        panel.AddChild(colorButton);
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);

        if (_running) {
            _progress += (float)gameTime.ElapsedGameTime.TotalSeconds * 0.1f;
            if (_progress > 1.0f) _progress = 1.0f;
            
            _progressBar.Value = _progress;
            Shell.Taskbar.SetProgress(OwnerProcess, _progress);
        }
    }
}
