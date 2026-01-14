using System;
using Microsoft.Xna.Framework;

namespace TheGame.Core.Animation;

public enum Easing {
    Linear,
    EaseInQuad,
    EaseOutQuad,
    EaseInOutQuad,
    EaseOutBounce,
    EaseOutBack
}

public class Tween {
    public object Target { get; private set; }
    public Action<float> FloatSetter { get; private set; }
    public Action<Vector2> VectorSetter { get; private set; }
    
    public float StartFloat { get; private set; }
    public float EndFloat { get; private set; }
    
    public Vector2 StartVector { get; private set; }
    public Vector2 EndVector { get; private set; }
    
    public float Duration { get; private set; }
    public float Elapsed { get; private set; }
    public bool IsComplete { get; private set; }
    public Easing EasingFunction { get; private set; }
    
    public Action OnComplete { get; set; }
    public string Tag { get; set; }

    // Float Constructor
    public Tween(object target, Action<float> setter, float start, float end, float duration, Easing easing) {
        Target = target;
        FloatSetter = setter;
        StartFloat = start;
        EndFloat = end;
        Duration = duration;
        EasingFunction = easing;
    }

    // Vector2 Constructor
    public Tween(object target, Action<Vector2> setter, Vector2 start, Vector2 end, float duration, Easing easing) {
        Target = target;
        VectorSetter = setter;
        StartVector = start;
        EndVector = end;
        Duration = duration;
        EasingFunction = easing;
    }

    public void Update(float dt) {
        if (IsComplete) return;

        Elapsed += dt;
        if (Elapsed >= Duration) {
            Elapsed = Duration;
            IsComplete = true;
        }

        float t = Elapsed / Duration;
        float easedT = ApplyEasing(t, EasingFunction);

        if (FloatSetter != null) {
            float val = MathHelper.Lerp(StartFloat, EndFloat, easedT);
            FloatSetter(val);
        } else if (VectorSetter != null) {
            Vector2 val = Vector2.Lerp(StartVector, EndVector, easedT);
            VectorSetter(val);
        }

        if (IsComplete) {
            OnComplete?.Invoke();
        }
    }

    public Tween OnCompleteAction(Action action) {
        OnComplete = action;
        return this;
    }

    private float ApplyEasing(float t, Easing easing) {
        switch (easing) {
            case Easing.EaseInQuad: return t * t;
            case Easing.EaseOutQuad: return t * (2 - t);
            case Easing.EaseInOutQuad: return t < .5f ? 2 * t * t : -1 + (4 - 2 * t) * t;
            case Easing.EaseOutBounce:
                if (t < 1 / 2.75f) return 7.5625f * t * t;
                else if (t < 2 / 2.75f) return 7.5625f * (t -= 1.5f / 2.75f) * t + 0.75f;
                else if (t < 2.5 / 2.75f) return 7.5625f * (t -= 2.25f / 2.75f) * t + 0.9375f;
                else return 7.5625f * (t -= 2.625f / 2.75f) * t + 0.984375f;
            case Easing.EaseOutBack:
                const float c1 = 1.70158f;
                const float c3 = c1 + 1f;
                return 1f + c3 * (float)Math.Pow(t - 1f, 3f) + c1 * (float)Math.Pow(t - 1f, 2f);
            default: return t;
        }
    }
}
