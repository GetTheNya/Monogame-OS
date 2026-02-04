using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using TheGame.Core.Animation;
using TheGame.Core.UI.Controls;

namespace TheGame.Core.UI;

public class WizardWindow<TData> : Window {
    public TData Data { get; private set; }
    public event Action<TData> OnFinished;

    protected Stack<WizardStep<TData>> _navigationStack = new();
    public WizardStep<TData> CurrentStep => _navigationStack.Count > 0 ? _navigationStack.Peek() : null;

    private Panel _stepContainer;
    private Button _backButton;
    private Button _nextButton;
    private Button _cancelButton;
    
    // Progress indicator (dots)
    private Panel _progressPanel;
    private List<Panel> _dots = new();

    private bool _isAnimating;
    private bool _isFinishing;
    private int _lastTotalCount = -1;
    private int _lastCurrentIdx = -1;
    private float _lastPanelWidth = -1;
    private float _dotUpdateTimer = 0;

    public WizardWindow(string title, TData initialData, WizardStep<TData> firstStep) : base(Vector2.Zero, new Vector2(500, 400)) {
        Title = title;
        Data = initialData;
        CanResize = false;

        SetupUI();
        
        // Center on screen
        var viewport = G.GraphicsDevice.Viewport;
        Position = new Vector2(
            (viewport.Width - Size.X) / 2,
            (viewport.Height - Size.Y - 40) / 2
        );

        OnResize += UpdateLayout;
        
        // Setup close interception
        OnCloseRequested += HandleCloseRequested;

        PushStep(firstStep, animated: false);
        
        // Final layout update after first step is pushed
        UpdateLayout();
    }

    private void SetupUI() {
        _stepContainer = new Panel(Vector2.Zero, Vector2.Zero) {
            BackgroundColor = Color.Transparent,
            BorderColor = Color.Transparent
        };
        AddChild(_stepContainer);

        _progressPanel = new Panel(Vector2.Zero, Vector2.Zero) {
            BackgroundColor = Color.Transparent,
            BorderColor = Color.Transparent
        };
        AddChild(_progressPanel);

        _cancelButton = new Button(Vector2.Zero, new Vector2(100, 30), "Cancel") {
            OnClickAction = Cancel
        };
        AddChild(_cancelButton);

        _nextButton = new Button(Vector2.Zero, new Vector2(100, 30), "Next >") {
            OnClickAction = Next
        };
        AddChild(_nextButton);

        _backButton = new Button(Vector2.Zero, new Vector2(100, 30), "< Back") {
            OnClickAction = Back
        };
        AddChild(_backButton);

        UpdateLayout();
    }

    private void UpdateLayout() {
        if (_stepContainer == null) return;

        // Content container for steps
        _stepContainer.Position = new Vector2(20, 50);
        _stepContainer.Size = new Vector2(ClientSize.X - 40, ClientSize.Y - 110);

        // Progress dots container
        _progressPanel.Position = new Vector2(0, 10);
        _progressPanel.Size = new Vector2(ClientSize.X, 30);

        // Buttons
        float btnWidth = 100;
        float btnHeight = 30;
        float spacing = 10;
        float bottomMargin = 20;

        _cancelButton.Position = new Vector2(ClientSize.X - btnWidth - 20, ClientSize.Y - btnHeight - bottomMargin);
        _nextButton.Position = new Vector2(_cancelButton.Position.X - btnWidth - spacing, _cancelButton.Position.Y);
        _backButton.Position = new Vector2(_nextButton.Position.X - btnWidth - spacing, _nextButton.Position.Y);

        // Update current step size
        if (CurrentStep != null) {
            CurrentStep.Size = _stepContainer.Size;
        }

        UpdateProgressDots();
    }

    protected override void OnUpdate(GameTime gameTime) {
        if (CurrentStep != null && !_isAnimating) {
            _nextButton.IsEnabled = CurrentStep.CanGoNext;
            _backButton.IsEnabled = _navigationStack.Count > 1 && CurrentStep.CanGoBack;
            
            // If it's the last step (GetNextStep returns null), change text to Finish
            _nextButton.Text = CurrentStep.GetNextStep() == null ? "Finish" : "Next >";

            _dotUpdateTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_dotUpdateTimer > 0.2f) {
                _dotUpdateTimer = 0;
                UpdateProgressDots();
            }
        }
    }

    private void PushStep(WizardStep<TData> step, bool animated = true) {
        if (step == null) return;

        var oldStep = CurrentStep;
        step.Wizard = this;
        step.Size = _stepContainer.Size;
        step.Position = Vector2.Zero;
        _navigationStack.Push(step);

        if (animated && oldStep != null) {
            _isAnimating = true;
            step.Position = new Vector2(_stepContainer.Size.X, 0);
            _stepContainer.AddChild(step);
            step.OnEnter();

            Tweener.To(oldStep, v => oldStep.Position = v, oldStep.Position, new Vector2(-_stepContainer.Size.X, 0), 0.4f, Easing.EaseInOutQuad);
            Tweener.To(step, v => step.Position = v, step.Position, Vector2.Zero, 0.4f, Easing.EaseInOutQuad).OnComplete = () => {
                _stepContainer.RemoveChild(oldStep);
                _isAnimating = false;
            };
        } else {
            if (oldStep != null) _stepContainer.RemoveChild(oldStep);
            _stepContainer.AddChild(step);
            step.OnEnter();
        }

        UpdateProgressDots();
    }

    private void PopStep() {
        if (_navigationStack.Count <= 1) return;

        _isAnimating = true;
        var oldStep = _navigationStack.Pop();
        var prevStep = _navigationStack.Peek();

        _stepContainer.AddChild(prevStep);
        prevStep.Position = new Vector2(-_stepContainer.Size.X, 0);
        prevStep.OnEnter();

        Tweener.To(oldStep, v => oldStep.Position = v, oldStep.Position, new Vector2(_stepContainer.Size.X, 0), 0.4f, Easing.EaseInOutQuad);
        Tweener.To(prevStep, v => prevStep.Position = v, prevStep.Position, Vector2.Zero, 0.4f, Easing.EaseInOutQuad).OnComplete = () => {
            _stepContainer.RemoveChild(oldStep);
            _isAnimating = false;
        };

        UpdateProgressDots();
    }

    private void Next() {
        if (_isAnimating || CurrentStep == null) return;

        CurrentStep.OnNext();
        var nextStep = CurrentStep.GetNextStep();

        if (nextStep != null) {
            PushStep(nextStep);
        } else {
            Finish();
        }
    }

    private void Back() {
        if (_isAnimating || CurrentStep == null) return;
        
        CurrentStep.OnBack();
        PopStep();
    }

    private void Cancel() {
        Close();
    }

    private void Finish() {
        _isFinishing = true;
        OnFinished?.Invoke(Data);
        Close();
    }

    private void HandleCloseRequested(Action<bool> callback) {
        if (_isFinishing) {
            callback(true);
            return;
        }

        var msgBox = new MessageBox("Confirm Exit", "Are you sure you want to cancel the wizard?", MessageBoxButtons.YesNo, (confirmed) => {
            if (confirmed) {
                CurrentStep?.OnCancel();
                callback(true);
            } else {
                callback(false);
            }
        });
        // Modals need to be added to desktop/shell usually, but WindowBase handles showing them as blockers if added to ChildWindows
        AddChildWindow(msgBox);
    }
    
    private void AddChildWindow(WindowBase window) {
        window.ParentWindow = this;
        ChildWindows.Add(window);
        Parent?.AddChild(window);
    }

    private void UpdateProgressDots() {
        if (CurrentStep == null) return;

        int currentIdx = _navigationStack.Count - 1;
        
        // Lookahead to estimate total steps
        int futureCount = 0;
        var tempStep = CurrentStep;
        for (int i = 0; i < 10; i++) {
            var next = tempStep.GetNextStep();
            if (next == null) break;
            
            // Assign wizard context so the step can access 'Data' if needed for branching logic
            next.Wizard = this; 
            
            futureCount++;
            tempStep = next;
        }

        int totalCount = currentIdx + 1 + futureCount;
        float currentWidth = _progressPanel.Size.X;

        // Only rebuild if something changed (including width)
        if (totalCount == _lastTotalCount && currentIdx == _lastCurrentIdx && Math.Abs(currentWidth - _lastPanelWidth) < 0.1f) return;
        _lastTotalCount = totalCount;
        _lastCurrentIdx = currentIdx;
        _lastPanelWidth = currentWidth;

        _progressPanel.ClearChildren();
        _dots.Clear();

        float dotSize = 10;
        float spacing = 15;
        float totalWidth = (totalCount * dotSize) + ((totalCount - 1) * spacing);
        float startX = (_progressPanel.Size.X - totalWidth) / 2;

        for (int i = 0; i < totalCount; i++) {
            Color dotColor;
            if (i == totalCount - 1 && i == currentIdx) dotColor = Color.Gold;        //Finish
            else if (i < currentIdx) dotColor = new Color(150, 150, 150); // Completed
            else if (i == currentIdx) dotColor = Color.White;         // Current
            else dotColor = new Color(70, 70, 70);                   // Future

            var dot = new Panel(new Vector2(startX + i * (dotSize + spacing), 10), new Vector2(dotSize, dotSize)) {
                BackgroundColor = dotColor,
                CornerRadius = dotSize / 2f,
                BorderColor = (i == currentIdx) ? Color.White * 0.5f : Color.Transparent
            };
            _progressPanel.AddChild(dot);
            _dots.Add(dot);
        }
    }
}
