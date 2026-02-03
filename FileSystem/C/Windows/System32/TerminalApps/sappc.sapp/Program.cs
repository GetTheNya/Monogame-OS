using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TheGame.Core.OS;

namespace Compiler;

public class Program : TerminalApplication {
    private bool _verbose = false;
    private Process _childProcess = null;

    public static Program Main(string[] args) {
        return new Program();
    }

    protected override void Run(string[] args) {
        if (args == null || args.Length == 0) {
            PrintUsage();
            return;
        }

        string targetPath = null;
        bool runAfterCompile = false;
        List<string> appArgs = new List<string>();

        // Improved parsing
        for (int i = 0; i < args.Length; i++) {
            if (args[i].Equals("init", StringComparison.OrdinalIgnoreCase)) {
                if (i + 1 < args.Length) {
                    bool isApp = args.Skip(i).Any(a => a.Equals("-app", StringComparison.OrdinalIgnoreCase));
                    InitProject(args[i + 1], isApp);
                    return;
                }
                WriteLine("Error: Missing name for init command.", Color.Red);
                return;
            } else if (args[i].Equals("-run", StringComparison.OrdinalIgnoreCase)) {
                runAfterCompile = true;
                for (int j = i + 1; j < args.Length; j++) {
                    appArgs.Add(args[j]);
                }
                break;
            } else if (args[i].Equals("-v", StringComparison.OrdinalIgnoreCase)) {
                _verbose = true;
            } else if (targetPath == null) {
                targetPath = args[i];
            }
        }

        if (targetPath == null) {
            PrintUsage();
            return;
        }

        CompileAndRun(targetPath, runAfterCompile, appArgs.ToArray());
    }

    private void PrintUsage() {
        WriteLine("SAPP Compiler (sappc) version 1.2.0", Color.Cyan);
        WriteLine("Usage:", Color.White);
        WriteLine("  sappc <path> [-run [args...]] [-v]");
        WriteLine("  sappc init <name> [-app]");
        WriteLine("");
        WriteLine("Options:", Color.White);
        WriteLine("  -run    Execute the app after successful compilation");
        WriteLine("  -v      Verbose mode (show manifest and dependencies)");
        WriteLine("  -app    For 'init', scaffolds a GUI Application instead of TerminalApplication");
    }

    private void InitProject(string name, bool isApp) {
        string folderName = name.EndsWith(".sapp") ? name : name + ".sapp";
        string fullPath = VirtualFileSystem.Instance.ResolvePath(Process.WorkingDirectory, folderName);

        if (VirtualFileSystem.Instance.Exists(fullPath)) {
            WriteLine($"Error: Folder '{folderName}' already exists.", Color.Red);
            return;
        }

        VirtualFileSystem.Instance.CreateDirectory(fullPath);

        // Create manifest.json
        var manifest = new AppManifest {
            AppId = name.ToUpper(),
            Name = name,
            EntryClass = $"{name}.Program",
            EntryMethod = "Main",
            TerminalOnly = !isApp
        };
        VirtualFileSystem.Instance.WriteAllText(Path.Combine(fullPath, "manifest.json"), manifest.ToJson());

        // Create Program.cs
        string template;
        if (isApp) {
            template = $@"using System;
using Microsoft.Xna.Framework;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;

namespace {name};

public class Program : Application {{
    public static Application Main(string[] args) {{
        return new Program();
    }}

    protected override void OnLoad(string[] args) {{
        MainWindow = CreateWindow<MainWindow>();
        MainWindow.Title = ""{name}"";
    }}
}}

public class MainWindow : Window {{
    public MainWindow() {{
        Title = ""{name}"";
        Size = new Vector2(400, 300);
    }}

    protected override void OnLoad() {{
        AddChild(new Label(new Vector2(20, 20), ""Hello from {name}!"") {{ Color = Color.White }});
    }}
}}
";
        } else {
            template = $@"using System;
using Microsoft.Xna.Framework;
using TheGame.Core.OS;

namespace {name};

public class Program : TerminalApplication {{
    public static Program Main(string[] args) {{
        return new Program();
    }}

    protected override void Run(string[] args) {{
        WriteLine(""Hello from {name}!"");
    }}
}}
";
        }
        VirtualFileSystem.Instance.WriteAllText(Path.Combine(fullPath, "Program.cs"), template);

        WriteLine($"Project '{name}' ({(isApp ? "GUI" : "Terminal")}) initialized successfully in {folderName}", Color.Green);
    }

    private void CompileAndRun(string targetPath, bool run, string[] appArgs) {
        string resolvedPath = VirtualFileSystem.Instance.ResolvePath(Process.WorkingDirectory, targetPath);
        if (!VirtualFileSystem.Instance.Exists(resolvedPath)) {
            WriteLine($"Error: Path '{targetPath}' does not exist.", Color.Red);
            return;
        }

        string hostPath = VirtualFileSystem.Instance.ToHostPath(resolvedPath);
        if (!Directory.Exists(hostPath)) {
            WriteLine($"Error: '{targetPath}' is not a directory.", Color.Red);
            return;
        }

        // Read manifest
        string manifestPath = Path.Combine(hostPath, "manifest.json");
        if (!File.Exists(manifestPath)) {
            WriteLine($"Error: manifest.json not found in {targetPath}", Color.Red);
            return;
        }

        AppManifest manifest;
        try {
            manifest = AppManifest.FromJson(File.ReadAllText(manifestPath));
        } catch (Exception ex) {
            WriteLine($"Error: Failed to parse manifest.json: {ex.Message}", Color.Red);
            return;
        }

        if (_verbose) {
            WriteLine("--- Manifest ---", Color.Cyan);
            WriteLine($"AppId: {manifest.AppId}");
            WriteLine($"Name: {manifest.Name}");
            WriteLine($"Version: {manifest.Version}");
            WriteLine($"Entry: {manifest.EntryClass}.{manifest.EntryMethod}");
            WriteLine($"TerminalOnly: {manifest.TerminalOnly}");
            WriteLine($"SingleInstance: {manifest.SingleInstance}");
            WriteLine("----------------", Color.Cyan);
        }

        // Handle Single Instance
        if (run && manifest.SingleInstance) {
            var existing = ProcessManager.Instance.GetProcessesByApp(manifest.AppId);
            if (existing.Count > 0) {
                WriteLine($"App {manifest.AppId} is already running (SingleInstance=true). Connect to existing instance not supported yet.", Color.Yellow);
                return;
            }
        }

        // Gather source files
        var sourceFiles = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(hostPath, "*.cs", SearchOption.AllDirectories)) {
            string relative = Path.GetRelativePath(hostPath, file);
            sourceFiles[relative] = File.ReadAllText(file);
        }

        if (sourceFiles.Count == 0) {
            WriteLine("Error: No source files (*.cs) found.", Color.Red);
            return;
        }

        if (_verbose) WriteLine($"Compiling {sourceFiles.Count} source files...", Color.Cyan);

        IEnumerable<Diagnostic> diagnostics;
        if (run) {
            Assembly assembly = AppCompiler.Instance.Compile(sourceFiles, manifest.AppId, out diagnostics, manifest.References);
            if (assembly == null) {
                WriteLine("Compilation failed.", Color.Red);
                ReportErrors(diagnostics);
                return;
            }

            WriteLine("Compilation successful. Executing app...", Color.Green);
            ExecuteApp(assembly, manifest, resolvedPath, appArgs);
        } else {
            bool success = AppCompiler.Instance.Validate(sourceFiles, manifest.AppId, out diagnostics, manifest.References);
            if (!success) {
                WriteLine("Compilation failed.", Color.Red);
                ReportErrors(diagnostics);
                return;
            }
            WriteLine($"{manifest.Name}: Compilation successful.", Color.Green);
        }
    }

    private void ReportErrors(IEnumerable<Diagnostic> diagnostics) {
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error || d.IsWarningAsError);
        foreach (var diag in errors) {
            var lineSpan = diag.Location.GetLineSpan();
            string fileName = Path.GetFileName(lineSpan.Path ?? "Source.cs");
            int line = lineSpan.StartLinePosition.Line + 1;
            int col = lineSpan.StartLinePosition.Character + 1;
            
            // classic format: file.cs(10,5): error CS0103: The name 'x' does not exist in the current context.
            WriteLine($"{fileName}({line},{col}): {diag.Severity.ToString().ToLower()} {diag.Id}: {diag.GetMessage()}", Color.Red);
        }
    }

    private void ExecuteApp(Assembly assembly, AppManifest manifest, string virtualPath, string[] args) {
        try {
            // Validate Entry Point
            Type entryType = assembly.GetTypes().FirstOrDefault(t => t.FullName == manifest.EntryClass);
            if (entryType == null) {
                WriteLine($"Error: Entry class '{manifest.EntryClass}' not found in assembly.", Color.Red);
                return;
            }

            MethodInfo entryMethod = entryType.GetMethod(manifest.EntryMethod, BindingFlags.Public | BindingFlags.Static);
            if (entryMethod == null) {
                WriteLine($"Error: Static entry method '{manifest.EntryMethod}' not found in '{manifest.EntryClass}'.", Color.Red);
                return;
            }

            if (_verbose) WriteLine($"Starting {manifest.AppId}...", Color.Cyan);

            // Execute entry point
            object result;
            try {
                result = entryMethod.Invoke(null, new object[] { args });
            } catch (TargetParameterCountException) {
                result = entryMethod.Invoke(null, null);
            }

            if (result is Application app) {
                StartChild(app, manifest, virtualPath, args);
            } else if (result is Process process) {
                StartChild(process, manifest, virtualPath, args);
            } else {
                WriteLine("Error: Entry point must return an Application, TerminalApplication or Process.", Color.Red);
                if (result != null) WriteLine($"Actually returned: {result.GetType().Name}", Color.Yellow);
            }

        } catch (Exception ex) {
            WriteLine($"Execution Error: {ex.Message}", Color.Red);
            if (ex.InnerException != null) WriteLine($"  {ex.InnerException.Message}", Color.Red);
        }
    }

    private void StartChild(object appOrProcess, AppManifest manifest, string virtualPath, string[] args) {
        Process process;
        
        if (appOrProcess is Application app) {
            process = new Process {
                AppId = manifest.AppId.ToUpper(),
                Application = app,
                WorkingDirectory = virtualPath,
                IsAsync = app.IsAsync
            };
            app.Process = process;
        } else if (appOrProcess is Process p) {
            process = p;
            process.AppId = manifest.AppId.ToUpper();
            process.WorkingDirectory = virtualPath;
        } else return;

        // Pipe I/O
        process.IO.In = IO.In;
        process.IO.Out = IO.Out;
        process.IO.Error = IO.Error;

        _childProcess = process;
        ProcessManager.Instance.RegisterProcess(process);
        ProcessManager.Instance.OnProcessTerminated += OnChildExited;

        try {
            process.Initialize(args);

            // If a window was created during initialization, open it
            if (process.MainWindow != null) {
                Shell.UI.OpenWindow(process.MainWindow);
            }
        } catch (Exception ex) {
            WriteLine($"Process Crashed: {ex.Message}", Color.Red);
            process.Terminate();
        }

        // Wait for exit
        while (_childProcess != null && _childProcess.State != ProcessState.Terminated) {
            System.Threading.Thread.Sleep(50);
        }

        if (_verbose) WriteLine($"Application '{manifest.AppId}' exited with code {process.ExitCode}.", Color.Cyan);
    }

    private void OnChildExited(Process p) {
        if (_childProcess != null && p.ProcessId == _childProcess.ProcessId) {
            _childProcess = null;
            ProcessManager.Instance.OnProcessTerminated -= OnChildExited;
        }
    }

    protected override void OnCancel() {
        if (_childProcess != null) {
             WriteLine("\nForce terminating child process...", Color.Yellow);
            _childProcess.Terminate();
            _childProcess = null;
        }
        base.OnCancel();
    }
}
