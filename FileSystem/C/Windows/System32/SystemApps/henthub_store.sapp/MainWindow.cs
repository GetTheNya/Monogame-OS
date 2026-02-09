using Microsoft.Xna.Framework;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;

namespace HentHub;

public class MainWindow : Window {
    private PageStack _pageStack;
    private Label _headerLabel;

    public MainWindow() : base(new Vector2(100, 100), new Vector2(600, 500)) {
        Title = "HentHub Store";
        
        OnResize += () => {
            if (_pageStack != null) {
                _pageStack.Size = new Vector2(ClientSize.X, ClientSize.Y - 40);
            }
        };
    }

    protected override void OnLoad() {
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
    }
}