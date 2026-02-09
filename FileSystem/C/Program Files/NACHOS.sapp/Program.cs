using System;
using System.Threading.Tasks;
using TheGame;
using TheGame.Core.OS;

//Native Application Creator for HentOS
namespace NACHOS;

public class Program : Application {
    public static Program Main(string[] args) {
        return new Program();
    }

    public override bool IsAsync => true;

    protected override async Task OnLoadAsync(string[] args) {
        string projectPath = (args != null && args.Length > 0) ? args[0] : null;

        // 1. Show Splash Screen
        var splash = CreateWindow<SplashScreen>();
        MainWindow = splash; 
        OpenWindow(splash);
        
        // 2. Initial delay/fake progress
        int steps = 5;
        for (int i = 0; i <= steps; i++) {
            if (splash.IsVisible) {
                splash.Progress = i / (float)steps;
            }
            await Task.Delay(100);
        }

        // 3. Show Welcome Screen if no path provided
        if (string.IsNullOrEmpty(projectPath)) {
            var welcome = CreateWindow<WelcomeWindow>();
            MainWindow = welcome;
            OpenWindow(welcome);
            splash.Close();
            projectPath = await welcome.WaitForSelectionAsync();
            if (string.IsNullOrEmpty(projectPath)) return;
        } else {
            splash.Close();
        }

        // 4. Create and Show Main Window
        var win = CreateWindow<MainWindow>();
        win.Initialize(projectPath);

        MainWindow = win;
        OpenWindow(win);

        // 5. Keep process alive until main window is closed
        var tcs = new TaskCompletionSource<bool>();
        win.OnClosed += () => tcs.TrySetResult(true);
        
        // Final safety check
        if (win.Parent == null && !win.IsVisible) tcs.TrySetResult(true);

        await tcs.Task;
    }
}
