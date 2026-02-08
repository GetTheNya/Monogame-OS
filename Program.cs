using System.Runtime.Loader;
using System.IO;
using System;
using CefSharp;
using CefSharp.OffScreen;
using TheGame.Core;

AssemblyLoadContext.Default.Resolving += (context, assemblyName) => {
    string expectedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libs", $"{assemblyName.Name}.dll");
    if (File.Exists(expectedPath)) {
        return context.LoadFromAssemblyPath(expectedPath);
    }
    return null;
};

// Initialize CEF on the main thread
// string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cef_debug.log");
var settings = new CefSettings {
    CachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TheGame", "Cache"),
    WindowlessRenderingEnabled = true,
    MultiThreadedMessageLoop = true, // Recommended for offscreen in some scenarios
    LogSeverity = LogSeverity.Info,
    // LogFile = logPath
};

// Enable audio output and bypass autoplay restrictions for offscreen
settings.CefCommandLineArgs["enable-audio"] = "1";
settings.CefCommandLineArgs["enable-audio-output"] = "1";
settings.CefCommandLineArgs["autoplay-policy"] = "no-user-gesture-required";
settings.CefCommandLineArgs["mute-audio"] = "0";

// Force audio service in-process and disable sandboxing to ensure IAudioHandler works
settings.CefCommandLineArgs["disable-features"] = "AudioServiceOutOfProcess,UnifiedAutoplay";
settings.CefCommandLineArgs["disable-audio-service-sandbox"] = "1";

settings.CefCommandLineArgs.Add("enable-media-stream", "1");// Щоб відео грало саме
settings.CefCommandLineArgs.Remove("mute-audio");

// Спроба увімкнути все, що можна
settings.CefCommandLineArgs.Add("enable-speech-input");


// Іноді це допомагає змусити його використовувати системні кодеки (якщо встановлені в Windows)
settings.CefCommandLineArgs.Add("use-gl", "desktop");

if (Cef.IsInitialized != true) {
    DebugLogger.Log($"[CEF] Initializing on thread {Environment.CurrentManagedThreadId}...");
    bool success = Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null);
    DebugLogger.Log($"[CEF] Initialization {(success ? "succeeded" : "failed")}");
    
    if (!success) {
        DebugLogger.Log("[CEF] Initialization failed! Check if dependencies are missing in libs folder.");
    }
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