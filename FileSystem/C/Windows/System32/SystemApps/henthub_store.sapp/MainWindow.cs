using System;
using Microsoft.Xna.Framework;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using System.Threading.Tasks;

namespace HentHub;

public class MainWindow : Window {
    private PageStack _pageStack;
    private Label _headerLabel;
    public string StartupAppId { get; set; }

    public MainWindow() : base(new Vector2(100, 100), new Vector2(600, 500)) {
        Title = "HentHub Store";
        
        OnResize += () => {
            if (_pageStack != null) {
                _pageStack.Size = new Vector2(ClientSize.X, ClientSize.Y - 40);
            }
        };
    }

    protected override async void OnLoad() {
        await Task.Yield(); // Defer to allow Program.OnLoad to set StartupAppId
        Shell.Network.RegisterForNetwork(OwnerProcess);

        _headerLabel = new Label(new Vector2(15, 10), "HentHub Store") {
            FontSize = 20,
            UseBoldFont = true
        };
        AddChild(_headerLabel);

        _pageStack = new PageStack(new Vector2(0, 40), new Vector2(ClientSize.X, ClientSize.Y - 40));
        AddChild(_pageStack);

        _pageStack.OnPageChanged += (page) => {
            _headerLabel.Text = page.PageTitle;
        };

        _pageStack.Push(new MainPage());

        if (!string.IsNullOrEmpty(StartupAppId)) {
            string appId = StartupAppId;
            
            // Handle protocol URI (e.g., henthub://id?=xxx)
            if (appId.StartsWith("henthub://", StringComparison.OrdinalIgnoreCase)) {
                if (appId.Contains("id?=")) {
                    appId = appId.Split("id?=")[1].Split('&')[0];
                } else {
                    // Fallback: strip scheme and use the rest as ID
                    appId = appId.Substring(10).Trim('/');
                }
            }

            bool loaded = await StoreManager.Instance.LoadManifestAsync(OwnerProcess);
            if (loaded) {
                var app = StoreManager.Instance.GetApp(appId);
                if (app != null) {
                    _pageStack.Push(new DetailsPage(app, OwnerProcess));
                }
            }
        }
    }
}