using System;
using TheGame.Core.OS;

namespace HentHub;

public class Program : Application {
    public static Application Main(string[] args) => new Program();

    protected override void OnLoad(string[] args) {
        Console.WriteLine($"[Program] Launch args: {string.Join(", ", args)}");
        Shell.Network.RegisterForNetwork(Process);
        
        // Register custom URI scheme
        Shell.Protocols.Register("henthub", "HENTHUB_STORE", "URL:HentHub Protocol", "icon.png");

        var window = CreateWindow<MainWindow>();
        if (args.Length > 0) {
            window.StartupAppId = args[0].Trim();
            Console.WriteLine($"[Program] Set StartupAppId: {window.StartupAppId}");
        }
        MainWindow = window;
    }
}