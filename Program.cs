using System.Runtime.Loader;
using System.IO;
using System;

AssemblyLoadContext.Default.Resolving += (context, assemblyName) => {
    string expectedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libs", $"{assemblyName.Name}.dll");
    if (File.Exists(expectedPath)) {
        return context.LoadFromAssemblyPath(expectedPath);
    }
    return null;
};

using var game = new TheGame.Game1();
game.Run();
