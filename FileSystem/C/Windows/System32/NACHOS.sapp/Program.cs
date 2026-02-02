using Microsoft.Xna.Framework;
using System;
using TheGame.Core.OS;

//Native Application Creator for HentOS
namespace NACHOS;

public class Program : Application {
    public static Program Main(string[] args) {
        return new Program();
    }

    protected override void OnLoad(string[] args) {
        string projectPath = (args != null && args.Length > 0) ? args[0] : null;
        var win = CreateWindow<MainWindow>();
        win.Initialize(projectPath);
        MainWindow = win;
    }
}
