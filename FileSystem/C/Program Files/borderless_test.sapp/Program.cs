using Microsoft.Xna.Framework;
using TheGame.Core.UI;
using TheGame.Core.OS;

namespace BorderlessTestApp;

public class Program : Application {
    public static Application Main() => new Program();

    protected override void OnLoad(string[] args) {
        MainWindow = CreateWindow<MainWindow>();
    }
}
