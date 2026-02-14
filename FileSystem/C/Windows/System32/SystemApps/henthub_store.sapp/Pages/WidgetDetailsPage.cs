using System;
using Microsoft.Xna.Framework;
using TheGame.Core.OS;

namespace HentHub;

public class WidgetDetailsPage : BaseDetailsPage {
    public WidgetDetailsPage(StoreApp app, Process process) : base(app, process) {
    }

    protected override string GetDefaultInstallPath() {
        return Shell.Widgets.WidgetDirectory;
    }

    protected override void UpdateSizeLabel() {
        if (_sizeLabel == null) return;
        
        string sizeStr = _app.Size > 1024 * 1024 
            ? $"{_app.Size / (1024 * 1024f):F1} MB" 
            : $"{_app.Size / 1024f:F1} KB";
            
        string text = $"Size: {sizeStr}";
        
        if (_app.DefaultSize != null) {
            text += $" | Dimensions: {(int)_app.DefaultSize.Width}x{(int)_app.DefaultSize.Height}";
        }
        
        _sizeLabel.Text = text;
    }

    protected override void OnPostInstallSuccess() {
        // Refresh Widget Loader
        WidgetLoader.Instance.ReloadDynamicWidgets();
        if (Shell.Widgets.RefreshWidgets != null) Shell.Widgets.RefreshWidgets.Invoke();
    }

    protected override void UpdateStatus() {
        base.UpdateStatus();
        
        // DeskToys prerequisite check
        bool deskToysInstalled = AppInstaller.Instance.IsAppInstalled("DESKTOYS");
        if (!deskToysInstalled) {
            _installBtn.IsEnabled = false;
            _installBtn.Text = "DeskToys Required";
            _installBtn.BackgroundColor = Color.Gray;
        }
    }

    protected override void UpdatePathLabel() {
        base.UpdatePathLabel();
        _changePathBtn.IsVisible = false;
    }
}
