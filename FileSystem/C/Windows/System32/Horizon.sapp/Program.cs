using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using CefSharp;
using Microsoft.Xna.Framework.Input;
using TheGame.Core.Input;

namespace HorizonBrowser;

public class Program : Application {
    public static Application Main(string[] args) => new Program();

    protected override void OnLoad(string[] args) {
        MainWindow = CreateWindow<BrowserWindow>();
        MainWindow.Title = "Horizon";
        MainWindow.Size = new Vector2(1000, 700);

        Shell.Network.RegisterForNetwork(Process);
        Shell.Media.RegisterAsPlayer(Process);
    }
}