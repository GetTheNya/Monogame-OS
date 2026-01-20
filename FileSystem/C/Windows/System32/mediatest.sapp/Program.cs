using System;
using Microsoft.Xna.Framework;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;

namespace MediaTestApp;

public class Program {
    public static Window CreateWindow() {
        return new MediaTestApp(new Vector2(100, 100), new Vector2(400, 300));
    }
}

public class MediaTestApp : Window {
    private string _mediaId;
    private Slider _seekSlider;
    private Label _timeLabel;
    private float _lastSeekValue = -1f;

    public MediaTestApp(Vector2 pos, Vector2 size) : base(pos, size) {
        Title = "Media test";
        AppId = "MEDIATEST";

        SetupUI();
    }

    private void SetupUI() {
        float y = 40;
        float x = 20;

        AddChild(new Label(new Vector2(x, 10), "Media API Tester") { FontSize = 30 });

        var loadButton = new Button(new Vector2(x, y), new Vector2(100, 30), "Load");
        loadButton.OnClickAction = () => {
            if (_mediaId == null) {
                _mediaId = Shell.Media.LoadMedia(OwnerProcess, "C:\\Users\\Admin\\Documents\\test.mp3", true);
                if (_mediaId == null) {
                    Shell.Notifications.Show("Error", "Could not load test.mp3. Ensure it exists in C:\\Users\\Admin\\Documents\\");
                }
            }
        };
        AddChild(loadButton);
        x += 120;

        var unloadButton = new Button(new Vector2(x, y), new Vector2(100, 30), "Unload");
        unloadButton.OnClickAction = () => {
            if (_mediaId != null) {
                Shell.Media.UnloadMedia(_mediaId); 
                _mediaId = null;
                _timeLabel.Text = "Time: 0:00 / 0:00";
                _seekSlider.SetValue(0f, false);
            }
        };
        AddChild(unloadButton);
        y += 40;
        x -= 120;
        
        var playButton = new Button(new Vector2(x, y), new Vector2(100, 30), "Play");
        playButton.OnClickAction = () => {
            if (_mediaId != null) {
                Shell.Media.RegisterPlaybackFinished(_mediaId, () => {
                   Shell.Notifications.Show("Finished", "Media finished playing!");
                });
                Shell.Media.Play(_mediaId);
            }
        };
        AddChild(playButton);
        x += 120;

        var pauseButton = new Button(new Vector2(x, y), new Vector2(100, 30), "Pause");
        pauseButton.OnClickAction = () => {
            if (_mediaId != null) Shell.Media.Pause(_mediaId);
        };
        AddChild(pauseButton);
        x += 120;

        var stopButton = new Button(new Vector2(x, y), new Vector2(100, 30), "Stop");
        stopButton.OnClickAction = () => {
            if (_mediaId != null) Shell.Media.Stop(_mediaId);
        };
        AddChild(stopButton);
        y += 40;
        x -= 240;

        AddChild(new Label(new Vector2(x, y), "Volume:"));
        y += 25;

        var volumeSlider = new Slider(new Vector2(x, y), 360);
        volumeSlider.OnValueChanged += (val) => {
            if (_mediaId != null) {
                Shell.Media.SetVolume(_mediaId, val);
            }
        };
        y += 30;
        AddChild(volumeSlider);

        AddChild(new Label(new Vector2(x, y), "Seek:"));
        y += 25;
        _seekSlider = new Slider(new Vector2(x, y), 360);
        _seekSlider.OnValueChanged += (val) => {
            if (_mediaId != null) {
                // Throttle: only seek if value changed by more than 0.5% or if it's a discrete click
                if (Math.Abs(val - _lastSeekValue) > 0.005f || !_seekSlider.IsDragging) {
                    double duration = Shell.Media.GetDuration(_mediaId);
                    Shell.Media.Seek(_mediaId, val * duration);
                    _lastSeekValue = val;
                }
            }
        };


        AddChild(_seekSlider);
        y += 30;

        _timeLabel = new Label(new Vector2(x, y), "Time: 0:00 / 0:00");
        AddChild(_timeLabel);
        y += 40;
        
        AddChild(new Label(new Vector2(x, y), "One-Shot Test:"));
        y += 25;
        var oneShotBtn = new Button(new Vector2(x, y), new Vector2(200, 30), "Play Alert Sound");
        oneShotBtn.OnClickAction = () => Shell.Audio.PlaySound("C:\\Windows\\Media\\startup.wav");
        AddChild(oneShotBtn);
        y += 40;

        AddChild(new Label(new Vector2(x, y), "Global Controls:"));
        y += 25;
        AddChild(new Label(new Vector2(x, y), "Master Volume:"));
        var masterSlider = new Slider(new Vector2(x + 140, y), 200);
        masterSlider.SetValue(Shell.Media.GetMasterVolume(), false);
        masterSlider.OnValueChanged += (val) => Shell.Media.SetMasterVolume(val);
        AddChild(masterSlider);
        y += 30;

        AddChild(new Label(new Vector2(x, y), "App Volume:"));
        var appSlider = new Slider(new Vector2(x + 140, y), 200);
        appSlider.SetValue(Shell.Media.GetProcessVolume(OwnerProcess), false);
        appSlider.OnValueChanged += (val) => Shell.Media.SetProcessVolume(OwnerProcess, val);
        AddChild(appSlider);
        y += 40;

        var checkLoadBtn = new Button(new Vector2(x, y), new Vector2(200, 30), "Check If Loaded");
        checkLoadBtn.OnClickAction = () => {
             bool loaded = _mediaId != null && Shell.Media.IsLoaded(_mediaId);
             Shell.Notifications.Show("Status", $"Media Loaded: {loaded}");
        };
        AddChild(checkLoadBtn);
    }

    protected override void OnOwnerProcessSet() {
        base.OnOwnerProcessSet();

        Shell.Media.RegisterAsPlayer(OwnerProcess);
        
        // Preload the notification sound for instant one-shot playback
        Shell.Media.Preload("C:\\Windows\\Media\\startup.wav");
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);

        if (_mediaId == null) return;

        double pos = Shell.Media.GetPosition(_mediaId);
        double dur = Shell.Media.GetDuration(_mediaId);

        TimeSpan tPos = TimeSpan.FromSeconds(pos);
        TimeSpan tDur = TimeSpan.FromSeconds(dur);
            
        _timeLabel.Text = $"Time: {tPos:m\\:ss} / {tDur:m\\:ss}";

        // Don't update the slider if the user is currently interacting with it
        if (!_seekSlider.IsDragging && Shell.Media.GetStatus(_mediaId) == MediaStatus.Playing) {         
            float sliderPosition = 0;
            if (dur > 0) {
                sliderPosition = (float)(pos / dur);
                sliderPosition = MathHelper.Clamp(sliderPosition, 0f, 1f);
            }

            _seekSlider.SetValue(sliderPosition, false);
        }
    }
}
