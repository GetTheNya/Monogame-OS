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

    public Program() : base(new Vector2(100, 100), new Vector2(300, 200)) {
        InitUI();
    }

    private void InitUI() {
        var panel = new Panel(Vector2.Zero, Size);
        AddChild(panel);

        _startButton = new Button(new Vector2(10, 10), new Vector2(280, 40), "Start Progress") {
            OnClickAction = () => {
                _running = !_running;
                _startButton.Text = _running ? "Pause Progress" : "Resume Progress";
            }
        };
        panel.AddChild(_startButton);

        var resetButton = new Button(new Vector2(10, 60), new Vector2(280, 40), "Reset Progress") {
            OnClickAction = () => {
                _progress = 0f;
                _running = false;
                _startButton.Text = "Start Progress";
                Shell.Taskbar.SetProgress(OwnerProcess, -1.0f);
            }
        };
        panel.AddChild(resetButton);

        var colorButton = new Button(new Vector2(10, 110), new Vector2(280, 40), "Toggle Color") {
            OnClickAction = () => {
                if (OwnerProcess.ProgressColor == Color.Green) OwnerProcess.ProgressColor = Color.Yellow;
                else if (OwnerProcess.ProgressColor == Color.Yellow) OwnerProcess.ProgressColor = Color.Red;
                else OwnerProcess.ProgressColor = Color.Green;
            }
        };
        panel.AddChild(colorButton);
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);

        if (_running) {
            _progress += (float)gameTime.ElapsedGameTime.TotalSeconds * 0.1f;
            if (_progress > 1.0f) _progress = 1.0f;
            
            Shell.Taskbar.SetProgress(OwnerProcess, _progress);
        }
    }
}
