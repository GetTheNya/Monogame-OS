using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;
using TheGame;

namespace NeonWave;

public class MainWindow : Window {
    private Program App => OwnerProcess.Application as Program;

    // UI Controls
    private Slider _seekBar;
    private Slider _volumeSlider;
    private Label _timeLabel;
    private ScrollingLabel _trackTitle;
    private ScrollingLabel _artistLabel;
    
    private CircleButton _playPauseBtn;
    private CircleButton _shuffleBtn;
    private CircleButton _repeatBtn;
    private CircleButton _prevBtn;
    private CircleButton _nextBtn;
    private CircleButton _historyBtn;
    private Button _openBtn;

    // Spectrum Data
    private float[] _spectrumBuffer = new float[64];
    private float[] _spectrumVisual = new float[64];
    private float _dynamicGain = 1.0f;

    // Colors from mockup
    private Color _accentCyan = new Color(0, 255, 255);
    private Color _accentMagenta = new Color(255, 0, 255);
    private Color _bgDark = new Color(5, 5, 5);

    public MainWindow() {
        Title = "NeonWave";
        Size = new Vector2(800, 490);
        BackgroundColor = _bgDark * 0.95f;
        CanResize = false;
    }

    protected override void OnLoad() {
        SetupUI();
        OnTrackChanged();
        OnPlaybackStateChanged();
    }

    private void SetupUI() {
        float w = Size.X;
        float h = Size.Y;

        // Meta Info (Middle) - Move up slightly
        _trackTitle = new ScrollingLabel(new Vector2(30, 210), "Choose a track...", w - 60) {
            FontSize = 36,
            Color = Color.White
        };
        AddChild(_trackTitle);

        _artistLabel = new ScrollingLabel(new Vector2(30, 260), "NeonWave System", w - 60) {
            FontSize = 20,
            Color = _accentCyan * 0.8f
        };
        AddChild(_artistLabel);

        // Seek Bar (Bottom area) - Move up
        _seekBar = new Slider(new Vector2(30, 310), w - 60) {
            AccentColor = _accentCyan,
            BackgroundColor = Color.White * 0.1f
        };
        _seekBar.OnValueChanged += (v) => {
            if (App.MediaId != null && _seekBar.IsDragging) {
                double dur = Shell.Media.GetDuration(App.MediaId);
                Shell.Media.Seek(App.MediaId, v * dur);
            }
        };
        AddChild(_seekBar);

        _timeLabel = new Label(new Vector2(w - 120, 335), "0:00 / 0:00") {
            FontSize = 14,
            Color = Color.Gray
        };
        AddChild(_timeLabel);

        // Control Row (Bottom)
        float controlY = 360; // Moved up from 370
        float centerX = w / 2f;

        // Left controls
        _shuffleBtn = new CircleButton(new Vector2(30, controlY + 10), new Vector2(40, 40), "\uf074") {
            FontSize = 18,
            NeonColor = App.IsShuffle ? _accentMagenta : Color.Gray,
            OnClickAction = () => { App.IsShuffle = !App.IsShuffle; _shuffleBtn.NeonColor = App.IsShuffle ? _accentMagenta : Color.Gray; },
            Tooltip = "Shuffle"
        };
        AddChild(_shuffleBtn);

        _repeatBtn = new CircleButton(new Vector2(80, controlY + 10), new Vector2(40, 40), "\uf01e") {
            FontSize = 18,
            NeonColor = App.IsRepeat ? _accentMagenta : Color.Gray,
            OnClickAction = () => { App.IsRepeat = !App.IsRepeat; _repeatBtn.NeonColor = App.IsRepeat ? _accentMagenta : Color.Gray; },
            Tooltip = "Repeat"
        };
        AddChild(_repeatBtn);

        // Center controls
        _prevBtn = new CircleButton(new Vector2(centerX - 110, controlY), new Vector2(50, 50), "\uf048") {
            FontSize = 20,
            NeonColor = _accentMagenta,
            OnClickAction = App.PlayPrevious
        };
        AddChild(_prevBtn);

        _playPauseBtn = new CircleButton(new Vector2(centerX - 40, controlY - 10), new Vector2(80, 80), "\uf04b") {
            FontSize = 32,
            NeonColor = _accentCyan,
            OnClickAction = App.TogglePlayPause
        };
        AddChild(_playPauseBtn);

        _nextBtn = new CircleButton(new Vector2(centerX + 60, controlY), new Vector2(50, 50), "\uf051") {
            FontSize = 20,
            NeonColor = _accentMagenta,
            OnClickAction = App.PlayNext
        };
        AddChild(_nextBtn);

        // Right controls
        var volIcon = new Label(new Vector2(w - 280, controlY + 15), "\uf028") { FontSize = 18, Color = _accentCyan };
        AddChild(volIcon);

        _volumeSlider = new Slider(new Vector2(w - 250, controlY + 15), 80) {
            AccentColor = _accentCyan
        };
        _volumeSlider.SetValue(Shell.Media.GetProcessVolume(OwnerProcess));
        _volumeSlider.OnValueChanged += (v) => {
            Shell.Media.SetProcessVolume(OwnerProcess, v);
        };
        AddChild(_volumeSlider);

        _historyBtn = new CircleButton(new Vector2(w - 150, controlY + 10), new Vector2(40, 40), "\uf001") {
            FontSize = 18,
            NeonColor = Color.White,
            OnClickAction = App.ShowHistory
        };
        _historyBtn.Tooltip = "Playlist / History";
        AddChild(_historyBtn);

        _openBtn = new CircleButton(new Vector2(w - 70, controlY + 10), new Vector2(40, 40), "\uf07c") {
            FontSize = 18,
            NeonColor = _accentCyan,
            OnClickAction = OpenFolderPicker
        };
        _openBtn.Tooltip = "Open Folder";
        AddChild(_openBtn);
    }

    private void OpenFolderPicker() {
        Shell.UI.PickFile("Select Music", "C:\\Users", (file) => {
            if (string.IsNullOrEmpty(file)) return;
            string dir = Path.GetDirectoryName(file);
            App.LoadFolder(dir, file);
        }, Program.SupportedExtensions);
    }

    public void OnTrackChanged() {
        if (App.CurrentIndex >= 0 && App.CurrentIndex < App.Playlist.Count) {
            string path = App.Playlist[App.CurrentIndex];
            _trackTitle.Text = Path.GetFileNameWithoutExtension(path);
            _artistLabel.Text = "NeonWave Player"; 
            _trackTitle.Color = Color.White;
        }
    }

    public void OnPlaybackStateChanged() {
        if (App.MediaId == null) {
            _playPauseBtn.Text = "\uf04b";
            return;
        }
        var status = Shell.Media.GetStatus(App.MediaId);
        _playPauseBtn.Text = (status == MediaStatus.Playing) ? "\uf04c" : "\uf04b";
    }

    protected override void OnDraw(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
        // Draw Spectrum at the top
        float specH = 150; // Reduced height for more gap
        float specW = Size.X - 40;
        float barW = specW / _spectrumBuffer.Length;
        Vector2 specPos = new Vector2(20, 40); // Move down to avoid title bar area

        // Background grid for spectrum
        for (int i = 0; i < _spectrumBuffer.Length; i++) {
            shapeBatch.FillRectangle(new Vector2(specPos.X + i * barW, specPos.Y), new Vector2(1, specH), Color.White * 0.05f);
        }

        if (App.MediaId != null) {
            for (int i = 0; i < _spectrumBuffer.Length; i++) {
                // Apply dynamic gain + frequency-dependent gain
                float visualGain = (1.0f + (float)i / _spectrumBuffer.Length * 3.0f) * _dynamicGain;
                float val = _spectrumVisual[i] * visualGain;

                float h = val * specH * 3.5f; // Reduced multiplier from 4.5
                h = Math.Min(h, specH - 10); // Leave 10px headroom at the very top
                
                Color c = Color.Lerp(_accentCyan, _accentMagenta, (float)i / _spectrumBuffer.Length);
                shapeBatch.FillRectangle(new Vector2(specPos.X + i * barW, specPos.Y + specH - h), new Vector2(barW - 1, h), c * 0.8f);
                
                // Glow point at top
                if (h > 2) {
                    shapeBatch.FillCircle(new Vector2(specPos.X + i * barW + (barW-1)/2f, specPos.Y + specH - h), 2, Color.White * 0.5f);
                }
            }
        }
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (App.MediaId != null) {
            double pos = Shell.Media.GetPosition(App.MediaId);
            double dur = Shell.Media.GetDuration(App.MediaId);
            
            if (!_seekBar.IsDragging && dur > 0) {
                _seekBar.SetValue((float)(pos / dur), false);
            }

            TimeSpan tp = TimeSpan.FromSeconds(pos);
            TimeSpan td = TimeSpan.FromSeconds(dur);
            _timeLabel.Text = $"{tp:m\\:ss} / {td:m\\:ss}";

            // Smooth Spectrum Update
            if (Shell.Media.GetStatus(App.MediaId) == MediaStatus.Playing) {
                Shell.Media.GetSpectrumData(OwnerProcess, _spectrumBuffer);
                
                float framePeak = 0;
                for (int i = 0; i < _spectrumBuffer.Length; i++) {
                    float target = _spectrumBuffer[i];
                    framePeak = Math.Max(framePeak, target);
                    
                    // Fast rise (45), snappy fall (12) for rhythmic feel
                    float lerpSpeed = (target > _spectrumVisual[i]) ? 45f : 12f;
                    _spectrumVisual[i] = MathHelper.Lerp(_spectrumVisual[i], target, Math.Min(1.0f, lerpSpeed * dt));
                }

                // Dynamic Normalization (AGC)
                // Recalibrated for FFT/FftSize normalization
                float targetPeak = 0.04f; // Lower target for normalized FFT data
                if (framePeak > 0.0001f) {
                    float idealGain = targetPeak / framePeak;
                    // Faster expansion for quiet tracks, fast compression for loud ones
                    float gainSpeed = (idealGain < _dynamicGain) ? 15f : 5.0f; // Fast compress, faster expand
                    _dynamicGain = MathHelper.Lerp(_dynamicGain, idealGain, Math.Min(1.0f, gainSpeed * dt));
                }
                
                // Allow more aggressive gain for quiet tracks
                _dynamicGain = MathHelper.Clamp(_dynamicGain, 0.5f, 10.0f);

            } else {
                // Decay to zero
                for (int i = 0; i < _spectrumBuffer.Length; i++) {
                    _spectrumVisual[i] = MathHelper.Lerp(_spectrumVisual[i], 0, Math.Min(1.0f, 10f * dt));
                }
                _dynamicGain = MathHelper.Lerp(_dynamicGain, 1.0f, Math.Min(1.0f, 2f * dt));
            }
        }
    }
}
