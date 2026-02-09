using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Graphics;
using TheGame.Core.UI.Controls;
using TheGame.Core;
using TheGame;

namespace WizardDemoApp;

public class Program : Application {
    public static Application Main(string[] args) => new Program();

    protected override void OnLoad(string[] args) {
        MainWindow = CreateWindow<WizardDemoMainWindow>();
        MainWindow.Title = "Wizard Demo Launcher";
    }
}

public class DemoData {
    public string Name { get; set; } = "";
    public bool IsAdvanced { get; set; } = false;
    public string FavoriteColor { get; set; } = "Blue";
}

public class WizardDemoMainWindow : Window {
    private Label _resultLabel;

    public WizardDemoMainWindow() : base(new Vector2(100, 100), new Vector2(400, 300)) {
    }

    protected override void OnLoad() {
        var btn = new Button(new Vector2(100, 100), new Vector2(200, 50), "Open Wizard") {
            OnClickAction = OpenWizard
        };
        AddChild(btn);

        _resultLabel = new Label(new Vector2(20, 180), "No data yet.") {
            FontSize = 18,
            Color = Color.LightGreen
        };
        AddChild(_resultLabel);
    }

    private void OpenWizard() {
        var data = new DemoData();
        var firstStep = new WelcomeStep();
        var wizard = new WizardWindow<DemoData>("Special Setup Wizard", data, firstStep);
        
        wizard.OnFinished += (finalData) => {
            _resultLabel.Text = $"Wizard Finished!\nName: {finalData.Name}\nAdvanced: {finalData.IsAdvanced}\nColor: {finalData.FavoriteColor}";
        };

        // Open as modal dialog via the Application API
        OwnerProcess.Application.OpenModal(wizard, Bounds);
    }
}

// --- Steps ---

public class WelcomeStep : WizardStep<DemoData> {
    private TextInput _nameInput;

    public override bool CanGoNext => !string.IsNullOrWhiteSpace(_nameInput?.Value);

    public override void OnEnter() {
        ClearChildren();

        AddChild(new Label(new Vector2(0, 10), "Welcome to the Wizard!") { FontSize = 24 });
        AddChild(new Label(new Vector2(0, 50), "Please enter your name to continue:"));

        _nameInput = new TextInput(new Vector2(0, 80), new Vector2(300, 35));
        _nameInput.Value = Data.Name;
        AddChild(_nameInput);
    }

    public override void OnNext() {
        Data.Name = _nameInput.Value;
    }

    public override WizardStep<DemoData> GetNextStep() {
        return new ChoiceStep();
    }
}

public class ChoiceStep : WizardStep<DemoData> {
    private Checkbox _advancedCheck;

    public override void OnEnter() {
        ClearChildren();
        AddChild(new Label(new Vector2(0, 10), $"Hello, {Data.Name}!") { FontSize = 20 });
        AddChild(new Label(new Vector2(0, 50), "Choose your installation type:"));

        _advancedCheck = new Checkbox(new Vector2(0, 80), "Enable Advanced Settings");
        _advancedCheck.Value = Data.IsAdvanced;
        _advancedCheck.OnValueChanged = (val) => Data.IsAdvanced = val;
        AddChild(_advancedCheck);
    }

    public override void OnNext() {
        Data.IsAdvanced = _advancedCheck.Value;
    }

    public override WizardStep<DemoData> GetNextStep() {
        if (Data.IsAdvanced) {
            return new AdvancedStep();
        } else {
            return new SummaryStep();
        }
    }
}

public class AdvancedStep : WizardStep<DemoData> {
    private Label _colorLabel;

    public override void OnEnter() {
        ClearChildren();
        AddChild(new Label(new Vector2(0, 10), "Advanced Settings") { FontSize = 20 });
        AddChild(new Label(new Vector2(0, 50), "Pick your favorite color:"));

        string[] colors = { "Red", "Green", "Blue", "Yellow" };
        for (int i = 0; i < colors.Length; i++) {
            string color = colors[i];
            var btn = new Button(new Vector2(0, 80 + i * 35), new Vector2(100, 30), color) {
                OnClickAction = () => { 
                    Data.FavoriteColor = color;
                    UpdateLabel();
                }
            };
            AddChild(btn);
        }

        _colorLabel = new Label(new Vector2(120, 85), $"Selected: {Data.FavoriteColor}");
        AddChild(_colorLabel);
    }

    private void UpdateLabel() {
        if (_colorLabel != null)
            _colorLabel.Text = $"Selected: {Data.FavoriteColor}";
    }

    public override WizardStep<DemoData> GetNextStep() {
        return new SummaryStep();
    }
}

public class SummaryStep : WizardStep<DemoData> {
    public override void OnEnter() {
        ClearChildren();
        AddChild(new Label(new Vector2(0, 10), "All Done!") { FontSize = 24, Color = Color.Gold });
        AddChild(new Label(new Vector2(0, 50), "Summary of your selection:"));
        AddChild(new Label(new Vector2(10, 80), $"- Name: {Data.Name}"));
        AddChild(new Label(new Vector2(10, 110), $"- Advanced: {Data.IsAdvanced}"));
        if (Data.IsAdvanced) {
            AddChild(new Label(new Vector2(10, 140), $"- Favorite Color: {Data.FavoriteColor}"));
        }
        
        AddChild(new Label(new Vector2(0, 200), "Press 'Finish' to complete."));
    }

    public override WizardStep<DemoData> GetNextStep() => null;
}
