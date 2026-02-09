using TheGame.Core.OS;

namespace HentHub;

public class Program : Application {
    public static Application Main(string[] args) => new Program();

    protected override void OnLoad(string[] args) {
        Shell.Network.RegisterForNetwork(Process);
        MainWindow = CreateWindow<MainWindow>();
    }
}