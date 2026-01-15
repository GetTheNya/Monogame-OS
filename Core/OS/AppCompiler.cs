using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace TheGame.Core.OS;

/// <summary>
/// Roslyn-based C# compiler for compiling app source code at runtime.
/// </summary>
public class AppCompiler {
    private static AppCompiler _instance;
    public static AppCompiler Instance => _instance ??= new AppCompiler();

    private readonly List<MetadataReference> _references;

    private AppCompiler() {
        // Gather required assembly references
        _references = new List<MetadataReference> {
            // .NET Core libraries
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Linq").Location),
            MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
            
            // MonoGame
            MetadataReference.CreateFromFile(typeof(Microsoft.Xna.Framework.Game).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch).Assembly.Location),

            // FontStashSharp
            MetadataReference.CreateFromFile(typeof(FontStashSharp.FontSystem).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("FontStashSharp.MonoGame").Location),
            
            // Our assemblies
            MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location),
        };
    }

    /// <summary>
    /// Compiles C# source files into an assembly.
    /// </summary>
    /// <param name="sourceFiles">Dictionary of filename -> source code</param>
    /// <param name="assemblyName">Name for the compiled assembly</param>
    /// <param name="diagnostics">Output compilation diagnostics</param>
    /// <returns>Compiled assembly or null if compilation failed</returns>
    public Assembly Compile(Dictionary<string, string> sourceFiles, string assemblyName, out List<string> diagnostics) {
        diagnostics = new List<string>();

        // Parse all source files into syntax trees
        var syntaxTrees = sourceFiles.Select(kvp =>
            CSharpSyntaxTree.ParseText(kvp.Value, path: kvp.Key)
        ).ToArray();

        // Create compilation
        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: syntaxTrees,
            references: _references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug,
                allowUnsafe: false
            )
        );

        // Compile to memory stream
        using var ms = new MemoryStream();
        EmitResult result = compilation.Emit(ms);

        if (!result.Success) {
            // Compilation failed, collect errors
            var failures = result.Diagnostics
                .Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

            foreach (var diagnostic in failures) {
                diagnostics.Add($"{diagnostic.Id}: {diagnostic.GetMessage()}");
            }

            return null;
        }

        // Load the compiled assembly
        ms.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(ms.ToArray());
    }
}
