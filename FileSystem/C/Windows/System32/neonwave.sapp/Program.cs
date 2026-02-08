using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.Input;

namespace NeonWave;

public class Program : Application {
    public static readonly string[] SupportedExtensions = {
        ".mp3", ".wav", ".ogg" 
    };

    static Program() {
        // Register file type associations
        foreach (var ext in SupportedExtensions) {
            Shell.File.RegisterFileTypeHandler(ext);
        }
    }


    public static Application Main(string[] args) => new Program();

    // Shared Media State
    public List<string> Playlist { get; } = new();
    public List<string> History { get; } = new();
    public int CurrentIndex { get; set; } = -1;
    public string MediaId { get; set; }
    public bool IsShuffle { get; set; } = false;
    public bool IsRepeat { get; set; } = false;

    private TrayIcon _trayIcon;
    private string _playbackFinishedMediaId;

    protected override void OnLoad(string[] args) {
        ExitOnMainWindowClose = false;
        Shell.Media.RegisterAsPlayer(Process);

        // Initial Tray Icon
        _trayIcon = new TrayIcon(Shell.Images.LoadAppImage(Process, "Tray/play.png"), "NeonWave") {
            PersistAfterWindowClose = true,
            OnClick = () => TogglePlayPause(),
            OnDoubleClick = () => RestoreMainWindow(),
            OnRightClick = () => {
                Shell.ContextMenu.Show(InputManager.MousePosition.ToVector2(), new List<MenuItem> {
                    new MenuItem { Text = "Open NeonWave", Action = () => RestoreMainWindow(), IsDefault = true },
                    new MenuItem { Text = "Playlist / History", Action = () => ShowHistory() },
                    new MenuItem { Type = MenuItemType.Separator },
                    new MenuItem { Text = MediaId != null && Shell.Media.GetStatus(MediaId) == MediaStatus.Playing ? "Pause" : "Play", Action = () => TogglePlayPause() },
                    new MenuItem { Text = "Next", Action = () => PlayNext() },
                    new MenuItem { Text = "Previous", Action = () => PlayPrevious() },
                    new MenuItem { Type = MenuItemType.Separator },
                    new MenuItem { Text = "Exit", Action = () => Exit() }
                });
            }
        };
        Shell.SystemTray.AddIcon(Process, _trayIcon);

        // Handle Args
        if (args != null && args.Length > 0) {
            string path = args[0];
            if (VirtualFileSystem.Instance.Exists(path)) {
                if (VirtualFileSystem.Instance.IsDirectory(path)) {
                    LoadFolder(path);
                } else {
                    LoadFolder(Path.GetDirectoryName(path), path);
                }
            }
        }

        RestoreMainWindow();
    }

    public void RestoreMainWindow(Rectangle? startBounds = null) {
        if (MainWindow == null || MainWindow.IsClosing) {
            MainWindow = CreateWindow<MainWindow>();
            OpenMainWindow(startBounds);
        } else {
            MainWindow.IsVisible = true;
            MainWindow.HandleFocus();
        }
    }

    public void ShowHistory() {
        var win = CreateWindow<HistoryWindow>();
        OpenWindow(win);
    }

    public void PlayTrack(int index) {
        if (index < 0 || index >= Playlist.Count) return;

        if (MediaId != null) {
            Shell.Media.UnloadMedia(MediaId);
        }
        _playbackFinishedMediaId = null;

        CurrentIndex = index;
        string path = Playlist[CurrentIndex];
        MediaId = Shell.Media.LoadMedia(Process, path);

        if (MediaId != null) {
            Shell.Media.Play(MediaId);
            UpdateTrayIcon();
            
            // Add to history if not duplicate of last
            if (History.Count == 0 || History.Last() != path) {
                History.Add(path);
                if (History.Count > 50) History.RemoveAt(0);
            }

            // Store the MediaId for callback validation
            _playbackFinishedMediaId = MediaId;
            Shell.Media.RegisterPlaybackFinished(MediaId, OnPlaybackFinished);
        } else {
            // Media failed to load, try next track
            if (Playlist.Count > 0 && !IsRepeat) {
                PlayNext();
            }
        }
        
        // Notify all windows
        foreach (var win in Windows) {
            if (win is MainWindow mw) mw.OnTrackChanged();
            if (win is HistoryWindow hw) hw.RefreshList();
        }
    }

    private void OnPlaybackFinished() {
        // Safety check: only advance if callback is for current media
        if (_playbackFinishedMediaId != MediaId) return;
        
        if (IsRepeat) PlayTrack(CurrentIndex);
        else PlayNext();
    }

    public void PlayNext() {
        if (Playlist.Count == 0) return;
        int nextIndex;
        if (IsShuffle) {
            nextIndex = new Random().Next(Playlist.Count);
            if (nextIndex == CurrentIndex && Playlist.Count > 1) nextIndex = (nextIndex + 1) % Playlist.Count;
        } else {
            nextIndex = (CurrentIndex + 1) % Playlist.Count;
        }
        PlayTrack(nextIndex);
    }

    public void PlayPrevious() {
        if (Playlist.Count == 0) return;
        int prevIndex = (CurrentIndex - 1 + Playlist.Count) % Playlist.Count;
        PlayTrack(prevIndex);
    }

    public void TogglePlayPause() {
        if (Playlist.Count == 0) {
            // No tracks, open picker instead
            Shell.UI.PickFile("Select Music", "C:\\Users", (file) => {
                if (string.IsNullOrEmpty(file)) return;
                string dir = Path.GetDirectoryName(file);
                LoadFolder(dir, file);
            }, SupportedExtensions);
            return;
        }

        if (MediaId == null) {
            PlayTrack(0);
            return;
        }
        var status = Shell.Media.GetStatus(MediaId);
        if (status == MediaStatus.Playing) Shell.Media.Pause(MediaId);
        else Shell.Media.Play(MediaId);
        UpdateTrayIcon();
        
        foreach (var win in Windows) {
            if (win is MainWindow mw) mw.OnPlaybackStateChanged();
        }
    }

    public void LoadFolder(string dir, string startFile = null) {
        var files = VirtualFileSystem.Instance.GetFiles(dir)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLower()))
            .OrderBy(f => f)
            .ToList();

        if (files.Count == 0) return;

        Playlist.Clear();
        Playlist.AddRange(files);

        int startIndex = 0;
        if (startFile != null) startIndex = files.IndexOf(startFile);
        if (startIndex < 0) startIndex = 0;

        PlayTrack(startIndex);
    }

    public void StopPlayback() {
        if (MediaId != null) {
            Shell.Media.Stop(MediaId);
            UpdateTrayIcon();
            foreach (var win in Windows) {
                if (win is MainWindow mw) mw.OnPlaybackStateChanged();
            }
        }
    }

    public void UpdateTrayIcon() {
        if (_trayIcon == null) return;
        var status = MediaId != null ? Shell.Media.GetStatus(MediaId) : MediaStatus.Stopped;
        string iconPath = (status == MediaStatus.Playing) ? "Tray/play.png" : "Tray/pause.png";
        _trayIcon.SetIcon(Shell.Images.LoadAppImage(Process, iconPath));
        _trayIcon.Tooltip = (CurrentIndex >= 0 && CurrentIndex < Playlist.Count) 
            ? $"NeonWave: {Path.GetFileName(Playlist[CurrentIndex])}" 
            : "NeonWave";
    }

    protected override void OnInstanceReopened(string[] args, Rectangle? startBounds = null) {
        if (args != null && args.Length > 0) {
            string path = args[0];
            if (VirtualFileSystem.Instance.Exists(path)) {
                if (VirtualFileSystem.Instance.IsDirectory(path)) {
                    LoadFolder(path);
                } else {
                    LoadFolder(Path.GetDirectoryName(path), path);
                }
            }
        }
        RestoreMainWindow(startBounds);
    }
}
