using System.Runtime.Loader;
using System.IO;
using System;
using CefSharp;
using CefSharp.OffScreen;

AssemblyLoadContext.Default.Resolving += (context, assemblyName) => {
    string expectedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libs", $"{assemblyName.Name}.dll");
    if (File.Exists(expectedPath)) {
        return context.LoadFromAssemblyPath(expectedPath);
    }
    return null;
};

// Initialize CEF on the main thread
var settings = new CefSettings {
    CachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TheGame", "Cache"),
    WindowlessRenderingEnabled = true,
    MultiThreadedMessageLoop = true // Recommended for offscreen in some scenarios
};

if (Cef.IsInitialized != true) {
    Console.WriteLine($"[CEF] Initializing on thread {Environment.CurrentManagedThreadId}...");
    bool success = Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null);
    Console.WriteLine($"[CEF] Initialization {(success ? "succeeded" : "failed")}");
}

try {
    using var game = new TheGame.Game1();
    game.Run();
} finally {
    // Ensure CEF shuts down properly
    if (Cef.IsInitialized == true) {
        Cef.Shutdown();
    }
}