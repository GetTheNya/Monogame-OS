using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
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

    private readonly List<MetadataReference> _baseReferences;
    private readonly Dictionary<string, MetadataReference> _optionalReferences;
    public IEnumerable<string> AvailableReferences => _optionalReferences.Keys.OrderBy(k => k);

    private AppCompiler() {
        // Base references that EVERY app needs
        _baseReferences = new List<MetadataReference> {
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

            // Our core OS / UI assemblies
            MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location),
        };

        // Optional references requested via manifest
        _optionalReferences = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase) {
            { "Microsoft.CodeAnalysis", MetadataReference.CreateFromFile(typeof(MetadataReference).Assembly.Location) },
            { "Microsoft.CodeAnalysis.CSharp", MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location) },
            { "System.Collections.Immutable", MetadataReference.CreateFromFile(typeof(System.Collections.Immutable.ImmutableArray).Assembly.Location) },
            { "System.ComponentModel.TypeConverter", MetadataReference.CreateFromFile(typeof(System.Timers.Timer).Assembly.Location) },
            { "System.ComponentModel.Primitives", MetadataReference.CreateFromFile(typeof(System.ComponentModel.Component).Assembly.Location) },
            { "NAudio", MetadataReference.CreateFromFile(typeof(NAudio.Wave.WaveOutEvent).Assembly.Location) },
            { "FontStashSharp", MetadataReference.CreateFromFile(typeof(FontStashSharp.FontSystem).Assembly.Location) },
            { "FontStashSharp.MonoGame", MetadataReference.CreateFromFile(Assembly.Load("FontStashSharp.MonoGame").Location) },
            { "System.Text.Json", MetadataReference.CreateFromFile(typeof(JsonSerializer).Assembly.Location)},
            { "System.Text.Encodings.Web", MetadataReference.CreateFromFile(Assembly.Load("System.Text.Encodings.Web").Location)},
            { "System.Memory", MetadataReference.CreateFromFile(Assembly.Load("System.Memory").Location)},
            { "System.Text.RegularExpressions", MetadataReference.CreateFromFile(Assembly.Load("System.Text.RegularExpressions").Location)}
        };
    }

    private List<MetadataReference> GetFullReferences(IEnumerable<string> extraNames) {
        var refs = new List<MetadataReference>(_baseReferences);
        if (extraNames != null) {
            foreach (var name in extraNames) {
                if (_optionalReferences.TryGetValue(name, out var metaRef)) {
                    refs.Add(metaRef);
                }
            }
        }
        return refs;
    }

    /// <summary>
    /// Compiles C# source files and returns diagnostics without loading the resulting assembly.
    /// Useful for compilation checks without memory overhead.
    /// </summary>
    public CSharpCompilation Validate(Dictionary<string, string> sourceFiles, string assemblyName, out IEnumerable<Diagnostic> diagnostics, IEnumerable<string> extraReferences = null) {
        var syntaxTrees = sourceFiles.Select(kvp =>
            CSharpSyntaxTree.ParseText(kvp.Value, path: kvp.Key)
        ).ToArray();
 
        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: syntaxTrees,
            references: GetFullReferences(extraReferences),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug,
                allowUnsafe: false
            )
        );
        EmitResult result = compilation.Emit(Stream.Null);
        diagnostics = result.Diagnostics;

        return compilation;
    }

    /// <summary>
    /// Compiles C# source files into an assembly.
    /// </summary>
    /// <param name="sourceFiles">Dictionary of filename -> source code</param>
    /// <param name="assemblyName">Name for the compiled assembly</param>
    /// <param name="diagnostics">Output compilation diagnostics</param>
    /// <param name="extraReferences">Optional assembly names from manifest</param>
    /// <returns>Compiled assembly or null if compilation failed</returns>
    public Assembly Compile(Dictionary<string, string> sourceFiles, string assemblyName, out IEnumerable<Diagnostic> diagnostics, IEnumerable<string> extraReferences = null) {
        var compilation = Validate(sourceFiles, assemblyName, out diagnostics, extraReferences);

        // Compile to memory stream
        using var ms = new MemoryStream();
        EmitResult result = compilation.Emit(ms);

        diagnostics = result.Diagnostics;

        if (!result.Success) {
            return null;
        }

        // Load the compiled assembly
        ms.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(ms.ToArray());
    }
}
