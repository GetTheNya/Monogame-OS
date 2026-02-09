using System;
using TheGame.Core.OS;
using TheGame.Core.UI;

namespace ProcessManagerApp;

public class Program : Application {
    public static Application Main(string[] args) => new Program();

    protected override void OnLoad(string[] args) {
        MainWindow = CreateWindow<MainWindow>();
    }
}
