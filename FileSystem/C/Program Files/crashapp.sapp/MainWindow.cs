using Microsoft.Xna.Framework;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;
using System;

namespace CrashApp;

public class MainWindow : Window{
    private const float MenuBarHeight = 26f;

    public MainWindow() : base(new Vector2(100, 100), new Vector2(400, 450)) {
        Title = "Crash App";
        AppId = "CRASHAPP";
        SetupUI();
    }

    private void SetupUI() {
        var menuBar = new MenuBar(Vector2.Zero, new Vector2(ClientSize.X, MenuBarHeight));

        menuBar.AddMenu("File", m => {
            m.AddItem("Crash", () => {
                throw new System.Exception("User wants some crash!");
            }, "Ctrl+N");
            m.AddItem("Exit", Close);
        });
        AddChild(menuBar);


        AddChild(new Label(new Vector2(10, 40), "Crash App") { TextColor = Color.Red });

        var button = new Button(new Vector2(10, 80), new Vector2(100, 30), "Crash") {
            OnClickAction = () => {
                var zero = 0;
                var summ = 1 / zero;
            }
        };
        AddChild(button);

        var checkBox = new Checkbox(new Vector2(10, 120), "Enable crash") {
            OnValueChanged = (b) => {
                throw new System.Exception("User wants some crash!");
            }
        };
        AddChild(checkBox);

        var comboBox = new ComboBox(new Vector2(10, 150), new Vector2(200, 30));
        comboBox.Items.Add("Crash");
        comboBox.Items.Add("Or not to crash");

        comboBox.OnValueChanged = (value) => {
            if (comboBox.Items[value] == "Crash") {
                throw new SystemException("System has crashed. Lol no :) ~probably~");        
            }
        };
        AddChild(comboBox);
        
        var slider = new Slider(new Vector2(10, 190), 200) {
            OnValueChanged = (value) => {
                if (value >= 0.9) {
                    throw new System.Exception("User wants some crash!");
                }
            }
        };
        AddChild(slider);

        var @switch = new Switch(new Vector2(10, 220), "Enable crash") {
            OnValueChanged = (value) => {
                if (value) {
                    throw new System.Exception("User wants some crash!");
                }
            } 
        };
        AddChild(@switch);

        var textInput = new TextInput(new Vector2(10, 250), new Vector2(200, 30)) {
            OnValueChanged = (value) => {
                if (value.ToLower() == "crash") {
                    throw new System.Exception("User wants some crash!");
                }
            }
        };
        AddChild(textInput);
    }
}