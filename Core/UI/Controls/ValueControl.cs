using Microsoft.Xna.Framework;

namespace TheGame.Core.UI.Controls;

public abstract class ValueControl<T> : UIControl {
    private T _value;

    public T Value {
        get => _value;
        set {
            if (Equals(_value, value)) return;
            _value = value;
            OnValueChanged?.Invoke(_value);
        }
    }

    public System.Action<T> OnValueChanged { get; set; }

    protected ValueControl(Vector2 position, Vector2 size, T defaultValue = default) : base(position, size) {
        _value = defaultValue;
    }
}
