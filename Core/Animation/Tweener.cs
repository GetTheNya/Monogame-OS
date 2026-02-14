using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TheGame.Core.Animation;

public static class Tweener {
    private static List<Tween> _tweens = new List<Tween>();

    public static void Update(float dt) {
        if (_tweens.Count == 0) return;

        // Copy to avoid modification issues (e.g., CancelAll called during OnComplete)
        var currentTweens = _tweens.ToArray();
        foreach (var tween in currentTweens) {
            tween.Update(dt);
        }

        // Clean up completed ones
        _tweens.RemoveAll(t => t.IsComplete);
    }

    public static Tween To(object target, Action<float> setter, float start, float end, float duration, Easing easing = Easing.Linear) {
        var tween = new Tween(target, setter, start, end, duration, easing);
        _tweens.Add(tween);
        return tween;
    }

    public static Tween To(object target, Action<Vector2> setter, Vector2 start, Vector2 end, float duration, Easing easing = Easing.Linear) {
        var tween = new Tween(target, setter, start, end, duration, easing);
        _tweens.Add(tween);
        return tween;
    }
    
    public static void CancelAll(object target) {
        for (int i = _tweens.Count - 1; i >= 0; i--) {
            if (_tweens[i].Target == target) {
                _tweens.RemoveAt(i);
            }
        }
    }

    public static void CancelAll(object target, string tag) {
        for (int i = _tweens.Count - 1; i >= 0; i--) {
            if (_tweens[i].Target == target && _tweens[i].Tag == tag) {
                _tweens.RemoveAt(i);
            }
        }
    }

    public static bool IsAnimating(object target) {
        for (int i = 0; i < _tweens.Count; i++) {
            if (_tweens[i].Target == target) return true;
        }
        return false;
    }

    public static bool IsAnimating(object target, string tag) {
        for (int i = 0; i < _tweens.Count; i++) {
            if (_tweens[i].Target == target && _tweens[i].Tag == tag) return true;
        }
        return false;
    }

    public static Tween Delay(float duration, Action onComplete) {
        // We use a dummy tween to represent a delay
        var tween = new Tween(null, (float _) => { }, 0, 0, duration, Easing.Linear);
        tween.OnComplete = onComplete;
        _tweens.Add(tween);
        return tween;
    }
}
