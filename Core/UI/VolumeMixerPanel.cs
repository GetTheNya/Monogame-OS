using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.OS;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;
using TheGame.Core.Animation;
using TheGame.Core.Input;

namespace TheGame.Core.UI;

public class VolumeMixerPanel : Panel {
    private const float PanelWidth = 400f;
    private const float PanelHeight = 350f;
    private const float Padding = 10f;

    private ScrollPanel _scrollPanel;
    private List<VolumeControlRow> _rows = new();
    
    private float _showAnim = 0f;
    private bool _isOpen = false;
    private bool _isAnimating = false;
    private bool _isPinned = false;
    private Button _pinButton;

    public VolumeMixerPanel() : base(Vector2.Zero, new Vector2(PanelWidth, PanelHeight)) {
        BackgroundColor = new Color(30, 30, 30, 240);
        BorderThickness = 1f;
        BorderColor = Color.White * 0.1f;
        
        IsVisible = false;
        Opacity = 0f;
        CanFocus = false;

        var titleLabel = new Label(new Vector2(Padding, 10), "Volume Mixer") {
            FontSize = 20,
            Color = Color.White,
            CanFocus = false
        };
        AddChild(titleLabel);

        _scrollPanel = new ScrollPanel(new Vector2(10, 40), new Vector2(PanelWidth - 20, PanelHeight - 50));
        _scrollPanel.BackgroundColor = Color.Transparent;
        _scrollPanel.BorderThickness = 0;
        _scrollPanel.CanFocus = false;
        AddChild(_scrollPanel);

        _pinButton = new Button(new Vector2(PanelWidth - 70, 10), new Vector2(60, 25), "PIN") {
            FontSize = 12,
            CanFocus = false,
            OnClickAction = () => {
                _isPinned = !_isPinned;
                _pinButton.Text = _isPinned ? "UNPIN" : "PIN";
                _pinButton.BackgroundColor = _isPinned ? new Color(100, 100, 100) : new Color(40, 40, 40);
            }
        };
        AddChild(_pinButton);

        AudioManager.Instance.OnProcessRegistered += RefreshRows;
        AudioManager.Instance.OnProcessUnregistered += RefreshRows;

        RefreshRows();
    }

    public void Toggle() {
        if (_isOpen) Close();
        else Open();
    }

    public void Open() {
        if (_isOpen && !_isAnimating) return;
        IsVisible = true;
        _isOpen = true;
        _isAnimating = true;
        RefreshRows();
        UpdatePosition();
        Parent?.BringToFront(this);
        
        Tweener.CancelAll(this, "show_anim");
        var tween = Tweener.To(this, (v) => _showAnim = v, _showAnim, 1f, 0.2f, Easing.EaseOutQuad);
        tween.Tag = "show_anim";
        tween.OnCompleteAction(() => _isAnimating = false);
    }

    public void Close() {
        if (!_isOpen && !_isAnimating) return;
        _isOpen = false;
        _isAnimating = true;
        
        Tweener.CancelAll(this, "show_anim");
        var tween = Tweener.To(this, (v) => _showAnim = v, _showAnim, 0f, 0.2f, Easing.EaseOutQuad);
        tween.Tag = "show_anim";
        tween.OnCompleteAction(() => {
            IsVisible = false;
            _isAnimating = false;
        });
    }

    private void UpdatePosition() {
        // Position it near the tray if possible, or just stay as managed by SystemTray
        // For now, SystemTray will set its position.
    }

    private void RefreshRows() {
        _scrollPanel.ClearChildren();
        _rows.Clear();

        float yOffset = 0;
        float rowHeight = 60;

        // 1. Global Master Volume
        var masterRow = new VolumeControlRow(
            new Vector2(0, yOffset), 
            new Vector2(_scrollPanel.ClientSize.X, rowHeight),
            "Master Volume",
            null,
            () => Shell.Media.GetMasterVolume(),
            (v) => Shell.Media.SetMasterVolume(v),
            () => Shell.Media.GetMasterLevel(),
            () => Shell.Media.GetMasterPeak()
        );
        _scrollPanel.AddChild(masterRow);
        _rows.Add(masterRow);
        yOffset += rowHeight + 10;

        // 2. System Sounds
        var systemProcess = AudioManager.Instance.SystemProcess;
        if (systemProcess != null) {
            var systemRow = new VolumeControlRow(
                new Vector2(0, yOffset),
                new Vector2(_scrollPanel.ClientSize.X, rowHeight),
                "System Sounds",
                GameContent.PCIcon,
                () => Shell.Media.GetProcessVolume(systemProcess),
                (v) => Shell.Media.SetProcessVolume(systemProcess, v),
                () => Shell.Media.GetSystemLevel(),
                () => Shell.Media.GetSystemPeak()
            );
            _scrollPanel.AddChild(systemRow);
            _rows.Add(systemRow);
            yOffset += rowHeight + 10;
        }

        // 3. Applications
        var apps = AudioManager.Instance.RegisteredProcesses
            .Where(p => p != systemProcess)
            .ToList();

        foreach (var app in apps) {
            Texture2D appIcon = app.MainWindow?.Icon ?? GameContent.FileIcon;
            
            var appRow = new VolumeControlRow(
                new Vector2(0, yOffset),
                new Vector2(_scrollPanel.ClientSize.X, rowHeight),
                app.MainWindow?.Title ?? app.AppId,
                appIcon,
                () => Shell.Media.GetProcessVolume(app),
                (v) => Shell.Media.SetProcessVolume(app, v),
                () => Shell.Media.GetProcessLevel(app),
                () => Shell.Media.GetProcessPeak(app)
            );
            _scrollPanel.AddChild(appRow);
            _rows.Add(appRow);
            yOffset += rowHeight + 10;
        }

        _scrollPanel.UpdateContentHeight(yOffset);
    }

    public override void Update(GameTime gameTime) {
        if (!IsVisible) return;

        base.Update(gameTime);
        
        foreach (var row in _rows) {
            row.UpdateVolumeFromSource();
        }

        // Close if clicking outside the panel (and not pinned)
        if (_isOpen && !_isPinned && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
            if (!Bounds.Contains(InputManager.MousePosition) && !InputManager.IsMouseConsumed) {
                Close();
            }
        }
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (_showAnim < 0.01f && !IsVisible) return;

        float oldOpacity = Opacity;
        Vector2 oldPos = Position;
        
        Opacity = _showAnim;
        // Slide up animation (since it's usually at the bottom)
        Position = oldPos + new Vector2(0, (1f - _showAnim) * 20f);

        base.Draw(spriteBatch, batch);

        Opacity = oldOpacity;
        Position = oldPos;
    }

    private class VolumeControlRow : Panel {
        private Label _nameLabel;
        private Slider _slider;
        private LevelMeter _meter;
        private Texture2D _icon;
        private Func<float> _getter;
        private Action<float> _setter;
        private Func<float> _levelGetter;
        private Func<float> _peakGetter;
        private bool _isUpdatingInternally = false;

        public VolumeControlRow(Vector2 pos, Vector2 size, string name, Texture2D icon, Func<float> getter, Action<float> setter, Func<float> levelGetter, Func<float> peakGetter) : base(pos, size) {
            BackgroundColor = new Color(50, 50, 50, 100);
            _icon = icon;
            _getter = getter;
            _setter = setter;
            _levelGetter = levelGetter;
            _peakGetter = peakGetter;
            CanFocus = false;

            _nameLabel = new Label(new Vector2(50, 5), name) { FontSize = 14, CanFocus = false };
            AddChild(_nameLabel);

            _slider = new Slider(new Vector2(50, 25), size.X - 70) { CanFocus = false };
            _slider.OnValueChanged += (val) => {
                if (!_isUpdatingInternally) {
                    _setter(val);
                }
            };
            AddChild(_slider);

            _meter = new LevelMeter(new Vector2(50, 48), new Vector2(size.X - 70, 6)) { CanFocus = false };
            AddChild(_meter);
            
            UpdateVolumeFromSource();
        }

        public void UpdateVolumeFromSource() {
            if (!_slider.IsDragging) {
                _isUpdatingInternally = true;
                _slider.Value = _getter();
                _isUpdatingInternally = false;
            }
            _meter.Level = _levelGetter();
            _meter.Peak = _peakGetter();
        }

        protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
            base.DrawSelf(spriteBatch, batch);
            
            float iconSize = 32;
            Vector2 iconPos = AbsolutePosition + new Vector2(10, (Size.Y - iconSize) / 2f);
            
            if (_icon != null) {
                batch.DrawTexture(_icon, iconPos, Color.White * AbsoluteOpacity, iconSize / _icon.Width);
            } else {
                var masterIcon = GetVolumeIcon(Shell.Media.GetMasterVolume());
                batch.DrawTexture(masterIcon, iconPos, Color.White * AbsoluteOpacity, iconSize / masterIcon.Width);
            }
        }

        private Texture2D GetVolumeIcon(float volume) {
            if (volume <= 0) return GameContent.VolumeIcons[0];
            if (volume < 0.33f) return GameContent.VolumeIcons[2];
            if (volume < 0.66f) return GameContent.VolumeIcons[3];
            return GameContent.VolumeIcons[4];
        }
    }
}
