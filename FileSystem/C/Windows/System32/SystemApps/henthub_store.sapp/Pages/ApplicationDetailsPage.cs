using System;
using Microsoft.Xna.Framework;
using TheGame.Core.OS;

namespace HentHub;

public class ApplicationDetailsPage : BaseDetailsPage {
    public ApplicationDetailsPage(StoreApp app, Process process) : base(app, process) {
    }

    protected override string GetDefaultInstallPath() {
        return AppInstaller.Instance.GetDefaultInstallPath(_app.TerminalOnly);
    }

    protected override void UpdateSizeLabel() {
        if (_sizeLabel == null) return;
        
        string sizeStr = _app.Size > 1024 * 1024 
            ? $"{_app.Size / (1024 * 1024f):F1} MB" 
            : $"{_app.Size / 1024f:F1} KB";
            
        string text = $"Size: {sizeStr}";
        
        if (_app.TerminalOnly) {
            text += " | Terminal Only";
        }
        
        _sizeLabel.Text = text;
    }

    protected override void OnPostInstallSuccess() {
        // Just refresh UI status
        UpdateStatus();
    }
}
