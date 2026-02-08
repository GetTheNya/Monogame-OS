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

// Initialize CEF
var settings = new CefSettings {
    CachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TheGame", "Cache"),
    WindowlessRenderingEnabled = true,
};

// Set subprocess path if needed, but for .NET Core it usually defaults to the current exe
// Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null);
if (Cef.IsInitialized == false) {
    Cef.Initialize(settings);
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