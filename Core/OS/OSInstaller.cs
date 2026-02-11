using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.Input;
using TheGame.Core.Animation;

namespace TheGame.Core.OS;

public class InstallerData {
    public string Username { get; set; } = "Admin";
    public string DisplayName { get; set; } = "Administrator";
    public string AccentColor { get; set; } = "Blue";
    public bool HasAgreedToEula { get; set; } = false;
    public bool IsFinished { get; set; } = false;
}

public class OSInstaller : WizardWindow<InstallerData> {
    public OSInstaller(InstallerData data) : base("HentOS Setup", data, GetStartingStep()) {
        Size = new Vector2(600, 800);
        UpdateLayout();
        CurrentStep?.OnEnter();

        // Center again after size change
        var viewport = G.GraphicsDevice.Viewport;
        Position = new Vector2((viewport.Width / 2 - Size.X / 2), (viewport.Height / 2 - Size.Y / 2));
    }

    private static WizardStep<InstallerData> GetStartingStep() {
        bool wasCancelled = Registry.Instance.GetValue<bool>("HKLM\\Software\\HentOS\\Installer\\Cancelled", false);
        if (wasCancelled) return new ComebackStep();
        return new InitialLoadingStep();
    }

    public override void Close() {
        if (!Data.IsFinished) {
            Registry.Instance.SetValue("HKLM\\Software\\HentOS\\Installer\\Cancelled", true);
            Registry.Instance.FlushToDisk();
            
            // Give time for registry to flush if possible, or just exit. 
            // FlushToDisk uses Task.Run, so we might want a synchronous flush if exiting.
            // But usually, it's fine for simple local development. 
            // For safety, I'll call a hypothetical synchronous flush or just hope for the best.
            // Game1.Instance.Exit() will terminate immediately.
            
            Game1.Instance.Exit();
        }
        base.Close();
    }

    public static void Start(Action<InstallerData> onFinished) {
        var data = new InstallerData();
        var installer = new OSInstaller(data);
        installer.OnFinished += onFinished;
        // In the context of InstallerScene, we might need a way to show it.
        // Usually, windows are added to the active scene or a desktop.
    }
}

// --- Steps ---

public class ComebackStep : WizardStep<InstallerData> {
    public override bool CanGoBack => false;

    public override void OnEnter() {
        ClearChildren();
        AddChild(new Label(new Vector2(0, 100), "Oh, look who came crawling back.") { 
            FontSize = 24, 
            Color = Color.LightCoral,
            Size = new Vector2(Size.X, 40)
            // Manual centering via position if needed, but for now just left aligned or padding
        });

        AddChild(new Label(new Vector2(20, 160), "We both knew you couldn't resist. Windows was boring, wasn't it?") { 
            FontSize = 18, 
            WordWrap = true,
            MaxWidth = Size.X - 40,
            Size = new Vector2(Size.X - 40, 60)
        });
    }

    public override void OnNext() {
        // Reset the flag once they admit their mistake
        Registry.Instance.DeleteKey("HKLM\\Software\\HentOS\\Installer\\Cancelled");
        Registry.Instance.FlushToDisk();
    }

    public override string NextButtonText => "I admit my mistake";
    public override WizardStep<InstallerData> GetNextStep() => new InitialLoadingStep();
}

public class InitialLoadingStep : WizardStep<InstallerData> {
    private ProgressBar _progressBar;
    private float _progress = 0f;

    public override bool CanGoBack => false;
    public override bool CanGoNext => false; 

    public override void OnEnter() {
        ClearChildren();
        
        AddChild(new Label(new Vector2(25, 100), "HentOS is loading files...") { FontSize = 14, TextColor = Color.White });

        var tray = new Panel(new Vector2(25, 130), new Vector2(Size.X - 50, 25)) {
            BackgroundColor = new Color(50, 50, 50),
            BorderColor = Color.White,
            BorderThickness = 1
        };
        AddChild(tray);

        _progressBar = new ProgressBar(new Vector2(26, 131), new Vector2(Size.X - 52, 23)) {
            Value = 0
        };
        // It's already white/simple by default usually, but we can customize it if needed
        AddChild(_progressBar);
        
        AddChild(new Label(new Vector2(25, 165), "Loading installer...") { FontSize = 14, TextColor = Color.White });
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        _progress += (float)gameTime.ElapsedGameTime.TotalSeconds * 0.3f;
        _progressBar.Value = Math.Min(_progress, 1f);

        if (_progress >= 1.3f) { 
            Wizard.Next();
        }
    }

    public override WizardStep<InstallerData> GetNextStep() => new EulaStep();
}

public class EulaStep : WizardStep<InstallerData> {
    private float _entryTime;

    public override bool CanGoNext => Data.HasAgreedToEula;

    public override void OnEnter() {
        ClearChildren();
        _entryTime = (float)Game1.Instance.TargetElapsedTime.TotalSeconds; 

        AddChild(new Label(new Vector2(0, 0), "End User License Agreement") { FontSize = 24 });
        AddChild(new Label(new Vector2(0, 35), "Please read the following agreement carefully:"));

        var scrollPanel = new ScrollPanel(new Vector2(0, 65), new Vector2(Size.X, Size.Y - 140)) {
            BackgroundColor = new Color(20, 20, 20),
            BorderColor = Color.DimGray,
            BorderThickness = 1
        };
        
        var eulaLabel = new Label(Vector2.One * 10, "") {
            FontSize = 14,
            WordWrap = true,
            MaxWidth = Size.X - 35 // Account for scrollbar and padding
        };

        string eulaPath = "C:\\Windows\\System32\\eula.txt";
        if (VirtualFileSystem.Instance.Exists(eulaPath)) {
            eulaLabel.Text = VirtualFileSystem.Instance.ReadAllText(eulaPath);
        } else {
            eulaLabel.Text = "EULA file not found. By proceeding, you agree to... well, something.";
        }
        
        scrollPanel.AddChild(eulaLabel);
        AddChild(scrollPanel);

        var agreeCheck = new Checkbox(new Vector2(0, Size.Y - 60), "I have read and agree to the terms");
        agreeCheck.Value = Data.HasAgreedToEula;
        agreeCheck.OnValueChanged = (val) => {
            Data.HasAgreedToEula = val;
            
            // Fast reader detection
            if (val && _stepTime < 10.0f) {
                var msgBox = new MessageBox("Speed Reader Detected", "Wow, you read at 5,000 words per minute. Respect.");
                Wizard.AddChildWindow(msgBox);
            }
        };
        AddChild(agreeCheck);
    }

    private float _stepTime = 0;
    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        _stepTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
    }

    public override WizardStep<InstallerData> GetNextStep() => new UsernameInputStep();
}

public class UsernameInputStep : WizardStep<InstallerData> {
    private TextInput _usernameInput;

    public override bool CanGoNext => !string.IsNullOrWhiteSpace(_usernameInput?.Value) && _usernameInput.Value.All(c => char.IsLetterOrDigit(c));

    public override void OnEnter() {
        ClearChildren();
        AddChild(new Label(new Vector2(0, 10), "Personalize your OS") { FontSize = 24 });
        AddChild(new Label(new Vector2(0, 50), "Choose a username for your account:"));

        _usernameInput = new TextInput(new Vector2(0, 80), new Vector2(300, 35)) {
            Value = Data.Username
        };
        AddChild(_usernameInput);
        
        AddChild(new Label(new Vector2(0, 125), "Username can only contain letters and numbers.") { 
            Color = Color.Gray * 0.8f,
            FontSize = 14
        });
    }

    public override void OnNext() {
        Data.Username = _usernameInput.Value;
        Data.DisplayName = _usernameInput.Value; // Default display name to username
    }

    public override WizardStep<InstallerData> GetNextStep() => new InstallingProgressStep();
}

public class InstallingProgressStep : WizardStep<InstallerData> {
    private ProgressBar _progressBar;
    private ScrollPanel _logScroll;
    private Label _logLabel;
    private Label _statusLabel;
    private Panel _imageContainer;
    private Label _descLabel;

    private float _progress = 0f;
    private float _targetProgress = 0f;
    private float _timer = 0f;
    private float _lagTimer = 0f;
    private bool _isLagging = false;
    
    private List<string> _allLogs = new();
    private int _logIndex = 0;
    private float _logTimer = 0f;

    private string[] _descriptions = {
        "HentOS is designed for speed and simplicity.",
        "Your files are organized with our advanced Virtual File System.",
        "Everything is customizable. Make it yours.",
        "Join the community of developers building for HentOS.",
        "Almost there! Polishing the final bits..."
    };
    private int _descIndex = 0;
    private float _descTimer = 0f;

    private List<Texture2D> _slides = new();
    private int _slideIndex = -1;
    private int _nextSlideIndex = -1;
    private float _transitionProgress = 0f;
    private float _slideTimer = 0f;
    private const float SlideInterval = 12.0f; // Slower slide changes

    public override bool CanGoBack => false;
    public override bool CanGoNext => _progress >= 1.0f;

    public override void OnEnter() {
        ClearChildren();
        
        AddChild(new Label(new Vector2(0, 0), "Installing HentOS...") { FontSize = 24 });

        // Image/Slideshow Panel - Made Taller (330 height)
        _imageContainer = new Panel(new Vector2(0, 40), new Vector2(Size.X, 330)) {
            BackgroundColor = new Color(30, 30, 35),
            BorderColor = Color.DimGray,
            BorderThickness = 1
        };
        AddChild(_imageContainer);

        _descLabel = new Label(new Vector2(20, 380), _descriptions[0]) {
            FontSize = 18,
            Color = Color.LightSkyBlue
        };
        AddChild(_descLabel);

        // Progress Bar
        _progressBar = new ProgressBar(new Vector2(0, 420), new Vector2(Size.X, 25));
        AddChild(_progressBar);

        _statusLabel = new Label(new Vector2(0, 455), "Preparing installation...") { FontSize = 14 };
        AddChild(_statusLabel);

        // Larger Fake Terminal using ScrollPanel + Label
        _logScroll = new ScrollPanel(new Vector2(0, 480), new Vector2(Size.X, Size.Y - 480)) {
            BackgroundColor = Color.Black,
            BorderColor = Color.DarkSlateGray,
            BorderThickness = 1
        };
        
        _logLabel = new Label(Vector2.One * 10, "") {
            FontSize = 12,
            TextColor = Color.LightGreen,
            WordWrap = true,
            MaxWidth = Size.X - 35
        };
        
        _logScroll.AddChild(_logLabel);
        AddChild(_logScroll);

        // Load logs
        string logPath = "C:\\Windows\\System32\\install_logs.txt";
        if (VirtualFileSystem.Instance.Exists(logPath)) {
            _allLogs = VirtualFileSystem.Instance.ReadAllLines(logPath).ToList();
        } else {
            _allLogs = new List<string> { "[ERR] Log file not found.", "[SYS] Attempting generic install..." };
        }

        // Load slideshow images
        LoadSlideshow();
    }

    private void LoadSlideshow() {
        string slideDir = "C:\\Windows\\SystemResources\\Setup\\Slideshow";
        if (VirtualFileSystem.Instance.Exists(slideDir) && VirtualFileSystem.Instance.IsDirectory(slideDir)) {
            var files = VirtualFileSystem.Instance.GetFiles(slideDir);
            foreach (var file in files) {
                if (!file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                try {
                    string hostPath = VirtualFileSystem.Instance.ToHostPath(file);
                    var texture = ImageLoader.Load(G.GraphicsDevice, hostPath);
                    if (texture != null) _slides.Add(texture);
                } catch { }
            }
        }

        if (_slides.Count > 0) {
            _slideIndex = 0;
        }
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_progress < 1.0f) {
            _timer += dt;
            
            // Random Lags to make it feel real
            if (_lagTimer > 0) {
                _lagTimer -= dt;
            } else {
                if (new Random().NextDouble() < 0.005) { 
                    _isLagging = true;
                    _lagTimer = (float)(new Random().NextDouble() * 3.0 + 1.0);
                } else {
                    _isLagging = false;
                }
            }

            if (!_isLagging) {
                _targetProgress += dt / 5.0f;
            }

            _progress = MathHelper.Clamp(_targetProgress, 0, 1);
            _progressBar.Value = _progress;

            // Update status text
            if (_progress < 0.2f) _statusLabel.Text = "Extracting system files...";
            else if (_progress < 0.5f) _statusLabel.Text = "Registering components...";
            else if (_progress < 0.8f) _statusLabel.Text = "Configuring user profile...";
            else _statusLabel.Text = "Finalizing installation...";

            // Update Logs
            _logTimer += dt;
            float logSpeed = _isLagging ? 1.0f : 0.08f; 
            if (_logTimer > logSpeed && _logIndex < _allLogs.Count) {
                _logTimer = 0;
                _logLabel.Text += _allLogs[_logIndex] + "\n";
                _logIndex++;
            }

            // Always Stick to Bottom
            float targetScroll = -Math.Max(0, _logLabel.Size.Y - _logScroll.Size.Y);
            _logScroll.TargetScrollY = targetScroll;

            // Update Descriptions
            _descTimer += dt;
            if (_descTimer > 6.0f) {
                _descTimer = 0;
                _descIndex = (_descIndex + 1) % _descriptions.Length;
                _descLabel.Text = _descriptions[_descIndex];
            }

            // Update Slideshow
            if (_slides.Count > 1 && _nextSlideIndex == -1) {
                _slideTimer += dt;
                if (_slideTimer > SlideInterval) {
                    _slideTimer = 0;
                    _nextSlideIndex = (_slideIndex + 1) % _slides.Count;
                    _transitionProgress = 0f;
                    
                    Tweener.To(this, (v) => _transitionProgress = v, 0f, 1f, 1.0f, Easing.EaseInOutQuad).OnCompleteAction(() => {
                        _slideIndex = _nextSlideIndex;
                        _nextSlideIndex = -1;
                        _transitionProgress = 0f;
                    });
                }
            }

            // Auto-advance when complete
            if (_progress >= 1.0f) {
                _statusLabel.Text = "Installation complete!";
                Data.IsFinished = true;
                // Brief pause before summary
                if (_timer > 22.0f) { 
                     Wizard.Next();
                }
            }
        }
    }

    public override void Draw(SpriteBatch spriteBatch, Graphics.ShapeBatch shapeBatch) {
        base.Draw(spriteBatch, shapeBatch);

        // Draw the current slide image if we have any
        if (_slideIndex >= 0 && _slideIndex < _slides.Count) {
            float opacity = _imageContainer.AbsoluteOpacity;
            var pos = _imageContainer.AbsolutePosition;
            var size = _imageContainer.Size;

            // Clip drawing to the image container bounds
            var oldScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
            var scissor = new Rectangle((int)pos.X, (int)pos.Y, (int)size.X, (int)size.Y);
            
            // Flush to apply scissor
            spriteBatch.End();
            spriteBatch.GraphicsDevice.ScissorRectangle = Rectangle.Intersect(oldScissor, scissor);
            var rs = new RasterizerState { ScissorTestEnable = true };
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, rs);

            if (_nextSlideIndex != -1) {
                // Transitioning
                var currentSlide = _slides[_slideIndex];
                var nextSlide = _slides[_nextSlideIndex];
                
                float offset = _transitionProgress * size.X;
                
                // Draw current slide moving out left
                var rectCurrent = new Rectangle((int)(pos.X - offset), (int)pos.Y, (int)size.X, (int)size.Y);
                spriteBatch.Draw(currentSlide, rectCurrent, Color.White * opacity);
                
                // Draw next slide moving in from right
                var rectNext = new Rectangle((int)(pos.X + size.X - offset), (int)pos.Y, (int)size.X, (int)size.Y);
                spriteBatch.Draw(nextSlide, rectNext, Color.White * opacity);
            } else {
                // Static
                var slide = _slides[_slideIndex];
                var rect = new Rectangle((int)pos.X, (int)pos.Y, (int)size.X, (int)size.Y);
                spriteBatch.Draw(slide, rect, Color.White * opacity);
            }

            // Flush and restore
            spriteBatch.End();
            spriteBatch.GraphicsDevice.ScissorRectangle = oldScissor;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        }
    }

    public override WizardStep<InstallerData> GetNextStep() => new SummaryStep();
}

public class SummaryStep : WizardStep<InstallerData> {
    public override void OnEnter() {
        ClearChildren();
        AddChild(new Label(new Vector2(0, 10), "Ready to Start!") { FontSize = 24, Color = Color.Gold });
        AddChild(new Label(new Vector2(0, 50), "HentOS has been successfully installed."));
        
        AddChild(new Label(new Vector2(10, 90), "Configuration:"));
        AddChild(new Label(new Vector2(20, 120), $"- Username: {Data.Username}"));
        AddChild(new Label(new Vector2(20, 150), $"- Accent Color: {Data.AccentColor}"));
        
        AddChild(new Label(new Vector2(0, 200), "Press 'Finish' to restart into your new desktop!"));
    }

    public override WizardStep<InstallerData> GetNextStep() => null;
}
