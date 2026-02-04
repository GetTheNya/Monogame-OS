using Microsoft.Xna.Framework;
using System;

namespace TheGame.Core.UI;

/// <summary>
/// A single page within a WizardWindow.
/// </summary>
/// <typeparam name="TData">The type of the shared data model.</typeparam>
public abstract class WizardStep<TData> : Panel {
    /// <summary>
    /// Reference to the parent WizardWindow.
    /// </summary>
    public WizardWindow<TData> Wizard { get; internal set; }

    /// <summary>
    /// Shortcut to the shared data object.
    /// </summary>
    public TData Data => Wizard.Data;

    /// <summary>
    /// Returns true if the user can proceed to the next step.
    /// Checked every frame by the WizardWindow.
    /// </summary>
    public virtual bool CanGoNext => true;

    /// <summary>
    /// Returns true if the user can go back to the previous step.
    /// Checked every frame by the WizardWindow.
    /// </summary>
    public virtual bool CanGoBack => true;

    public WizardStep() : base(Vector2.Zero, Vector2.Zero) {
        BackgroundColor = Color.Transparent;
        BorderColor = Color.Transparent;
    }

    /// <summary>
    /// Called when this step becomes the active step in the Wizard.
    /// </summary>
    public virtual void OnEnter() { }

    /// <summary>
    /// Called when the "Next" button is clicked, before moving to the next step.
    /// Use this for validation or data processing.
    /// </summary>
    public virtual void OnNext() { }

    /// <summary>
    /// Called when the "Back" button is clicked, before moving to the previous step.
    /// </summary>
    public virtual void OnBack() { }

    /// <summary>
    /// Returns the instance of the next step. Return null if this is the last step.
    /// This allows for dynamic branching logic.
    /// </summary>
    public virtual WizardStep<TData> GetNextStep() => null;

    /// <summary>
    /// Called if the wizard is canceled while this step is active.
    /// </summary>
    public virtual void OnCancel() { }
}
