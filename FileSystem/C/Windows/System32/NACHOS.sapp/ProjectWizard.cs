using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame;

namespace NACHOS;

public enum ProjectType {
    Console,
    GUI
}

public class ProjectSettings {
    public ProjectType Type { get; set; } = ProjectType.GUI;
    public string Name { get; set; } = "MyNewProject";
    public string Location { get; set; } = "C:\\";
    public string Namespace { get; set; } = "MyNewProject";
    
    // GUI Specific
    public string WindowTitle { get; set; } = "My Application";
    public int WindowWidth { get; set; } = 800;
    public int WindowHeight { get; set; } = 600;
    public bool IsResizable { get; set; } = true;
    
    // Additional
    public bool IncludeAssets { get; set; } = true;
    public bool CreateSampleCode { get; set; } = true;
}

public class ProjectWizardWindow : WizardWindow<ProjectSettings> {
    public ProjectWizardWindow(ProjectSettings initialData) 
        : base("New Project Wizard", initialData, new TemplateSelectionStep()) {
        Size = new Vector2(600, 500);
        // Recenter after size change
        var viewport = TheGame.G.GraphicsDevice.Viewport;
        Position = new Vector2(
            (viewport.Width - Size.X) / 2,
            (viewport.Height - Size.Y - 40) / 2
        );
    }
}

// --- Steps ---

public class TemplateSelectionStep : WizardStep<ProjectSettings> {
    public override void OnEnter() {
        ClearChildren();
        AddChild(new Label(new Vector2(0, 0), "Step 1: Template Selection") { FontSize = 24, TextColor = Color.Gold });
        AddChild(new Label(new Vector2(0, 40), "What type of application do you want to create?"));

        Button consoleBtn = null;
        Button guiBtn = null;

        guiBtn = new Button(new Vector2(0, 80), new Vector2(560, 70), "GUI Application") {
            OnClickAction = () => { Data.Type = ProjectType.GUI; UpdateSelection(); }
        };
        guiBtn.AddChild(new Label(new Vector2(10, 50), "A standard windowed application with UI components.") { FontSize = 12, TextColor = Color.Gray });
        AddChild(guiBtn);

        consoleBtn = new Button(new Vector2(0, 160), new Vector2(560, 70), "Console Application") {
            OnClickAction = () => { Data.Type = ProjectType.Console; UpdateSelection(); }
        };
        consoleBtn.AddChild(new Label(new Vector2(10, 50), "For command-line tools and utilities.") { FontSize = 12, TextColor = Color.Gray });
        AddChild(consoleBtn);
        
        UpdateSelection();

        void UpdateSelection() {
            if (consoleBtn != null) consoleBtn.BorderColor = Data.Type == ProjectType.Console ? Color.Gold : Color.Transparent;
            if (guiBtn != null) guiBtn.BorderColor = Data.Type == ProjectType.GUI ? Color.Gold : Color.Transparent;
        }
    }

    public override WizardStep<ProjectSettings> GetNextStep() => new NameAndLocationStep();
}

public class NameAndLocationStep : WizardStep<ProjectSettings> {
    private TextInput _nameInput;
    private TextInput _locationInput;
    private TextInput _namespaceInput;

    public override bool CanGoNext => !string.IsNullOrWhiteSpace(Data.Name) && !string.IsNullOrWhiteSpace(Data.Location);

    public override void OnEnter() {
        ClearChildren();
        AddChild(new Label(new Vector2(0, 0), "Step 2: Name and Location") { FontSize = 24, TextColor = Color.Gold });

        AddChild(new Label(new Vector2(0, 40), "Project Name:"));
        _nameInput = new TextInput(new Vector2(0, 65), new Vector2(400, 30)) { Value = Data.Name };
        _nameInput.OnValueChanged += (val) => { 
            Data.Name = val; 
            if (_namespaceInput != null) {
                Data.Namespace = val.Replace(" ", "");
                _namespaceInput.Value = Data.Namespace;
            }
        };
        AddChild(_nameInput);

        AddChild(new Label(new Vector2(0, 110), "Location:"));
        _locationInput = new TextInput(new Vector2(0, 135), new Vector2(400, 30)) { Value = Data.Location };
        _locationInput.OnValueChanged += (val) => Data.Location = val;
        AddChild(_locationInput);

        var browseBtn = new Button(new Vector2(410, 135), new Vector2(100, 30), "Browse...") {
            OnClickAction = () => {
                var fp = new FilePickerWindow("Select Project Location", Data.Location, "", FilePickerMode.ChooseDirectory, (path) => {
                    Data.Location = path;
                    _locationInput.Value = path;
                });
                Shell.UI.OpenWindow(fp);
            }
        };
        AddChild(browseBtn);

        AddChild(new Label(new Vector2(0, 180), "Main Namespace:"));
        _namespaceInput = new TextInput(new Vector2(0, 205), new Vector2(400, 30)) { Value = Data.Namespace };
        _namespaceInput.OnValueChanged += (val) => Data.Namespace = val;
        AddChild(_namespaceInput);
    }

    public override WizardStep<ProjectSettings> GetNextStep() => new ConfigurationStep();
}

public class ConfigurationStep : WizardStep<ProjectSettings> {
    public override void OnEnter() {
        ClearChildren();
        AddChild(new Label(new Vector2(0, 0), "Step 3: Detailed Configuration") { FontSize = 24, TextColor = Color.Gold });

        if (Data.Type == ProjectType.GUI) {
            AddChild(new Label(new Vector2(0, 40), "Window Title:"));
            var titleInput = new TextInput(new Vector2(0, 65), new Vector2(400, 30)) { Value = Data.WindowTitle };
            titleInput.OnValueChanged += (val) => Data.WindowTitle = val;
            AddChild(titleInput);

            AddChild(new Label(new Vector2(0, 110), "Initial Size:"));
            var widthInput = new TextInput(new Vector2(0, 135), new Vector2(100, 30)) { Value = Data.WindowWidth.ToString() };
            widthInput.OnValueChanged += (val) => { if (int.TryParse(val, out int w)) Data.WindowWidth = w; };
            AddChild(widthInput);
            AddChild(new Label(new Vector2(110, 140), "x"));
            var heightInput = new TextInput(new Vector2(130, 135), new Vector2(100, 30)) { Value = Data.WindowHeight.ToString() };
            heightInput.OnValueChanged += (val) => { if (int.TryParse(val, out int h)) Data.WindowHeight = h; };
            AddChild(heightInput);

            var resizableCheck = new Checkbox(new Vector2(0, 180), "Resizable Window") { Value = Data.IsResizable };
            resizableCheck.OnValueChanged = (val) => Data.IsResizable = val;
            AddChild(resizableCheck);
        } else {
            AddChild(new Label(new Vector2(0, 60), "Console applications use standard settings."));
            AddChild(new Label(new Vector2(0, 100), "No additional configuration required for this type.") { TextColor = Color.Gray });
        }
    }

    public override WizardStep<ProjectSettings> GetNextStep() => new DependenciesStep();
}

public class DependenciesStep : WizardStep<ProjectSettings> {
    public override void OnEnter() {
        ClearChildren();
        AddChild(new Label(new Vector2(0, 0), "Step 4: Dependencies and Resources") { FontSize = 24, TextColor = Color.Gold });

        var assetsCheck = new Checkbox(new Vector2(0, 60), "Include Assets (Content folder)") { Value = Data.IncludeAssets };
        assetsCheck.OnValueChanged = (val) => Data.IncludeAssets = val;
        AddChild(assetsCheck);

        var sampleCheck = new Checkbox(new Vector2(0, 100), "Create 'Hello World' example code") { Value = Data.CreateSampleCode };
        sampleCheck.OnValueChanged = (val) => Data.CreateSampleCode = val;
        AddChild(sampleCheck);
    }

    public override WizardStep<ProjectSettings> GetNextStep() => new SummaryStep();
}

public class SummaryStep : WizardStep<ProjectSettings> {
    public override void OnEnter() {
        ClearChildren();
        AddChild(new Label(new Vector2(0, 0), "Step 5: Summary") { FontSize = 24, TextColor = Color.Gold });

        string projectName = Data.Name;
        if (!projectName.EndsWith(".sapp", StringComparison.OrdinalIgnoreCase)) projectName += ".sapp";

        string fullPath = Path.Combine(Data.Location, projectName);
        AddChild(new Label(new Vector2(0, 40), $"You are about to create the {projectName} project in:"));
        AddChild(new Label(new Vector2(10, 65), fullPath) { TextColor = Color.Cyan, FontSize = 12 });

        int fileCount = Data.Type == ProjectType.GUI ? 3 : 2;
        AddChild(new Label(new Vector2(0, 100), $"{fileCount} files will be generated based on the template."));

        // File Tree Preview
        AddChild(new Label(new Vector2(0, 140), "File Tree Preview:"));
        var treePanel = new Panel(new Vector2(0, 165), new Vector2(ClientSize.X, 150)) {
            BackgroundColor = new Color(35, 35, 35),
            BorderColor = new Color(60, 60, 60)
        };
        AddChild(treePanel);

        float y = 10;
        treePanel.AddChild(new Label(new Vector2(10, y), $"[D] {projectName}/") { TextColor = Color.LightBlue }); y += 20;
        treePanel.AddChild(new Label(new Vector2(30, y), "manifest.json") { TextColor = Color.LightGreen }); y += 20;
        treePanel.AddChild(new Label(new Vector2(30, y), "Program.cs") { TextColor = Color.LightGreen }); y += 20;
        if (Data.Type == ProjectType.GUI) {
            treePanel.AddChild(new Label(new Vector2(30, y), "MainWindow.cs") { TextColor = Color.LightGreen }); y += 20;
        }
        if (Data.IncludeAssets) {
            treePanel.AddChild(new Label(new Vector2(30, y), "[D] Content/") { TextColor = Color.LightBlue }); y += 20;
        }
    }

    public override WizardStep<ProjectSettings> GetNextStep() => null;
}
