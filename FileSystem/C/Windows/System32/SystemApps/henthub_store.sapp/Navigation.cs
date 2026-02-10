using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TheGame.Core.UI;

namespace HentHub;

public abstract class StorePage : Panel {
    public string PageTitle { get; protected set; }
    public PageStack Stack { get; internal set; }

    public StorePage(string title) : base(Vector2.Zero, Vector2.Zero) {
        PageTitle = title;
        BackgroundColor = Color.Transparent;
    }

    public virtual void OnNavigatedTo() { }
    public virtual void OnNavigatedFrom() { }
}

public class PageStack : Panel {
    private Stack<StorePage> _history = new();
    public StorePage CurrentPage => _history.Count > 0 ? _history.Peek() : null;

    public event Action<StorePage> OnPageChanged;

    public PageStack(Vector2 pos, Vector2 size) : base(pos, size) {
        BackgroundColor = Color.Transparent;
        OnResize += () => {
            if (CurrentPage != null) CurrentPage.Size = ClientSize;
        };
    }

    public void Push(StorePage page) {
        if (CurrentPage != null) {
            CurrentPage.OnNavigatedFrom();
            RemoveChild(CurrentPage);
        }

        page.Stack = this;
        page.Size = ClientSize;
        _history.Push(page);
        AddChild(page);
        page.OnNavigatedTo();

        OnPageChanged?.Invoke(page);
    }

    public void Pop() {
        if (_history.Count <= 1) return;

        var oldPage = _history.Pop();
        oldPage.OnNavigatedFrom();
        RemoveChild(oldPage);
        
        if (oldPage is IDisposable disposable) {
            disposable.Dispose();
        }

        var newPage = _history.Peek();
        newPage.Size = ClientSize; // Force recalculate size
        AddChild(newPage);
        newPage.OnNavigatedTo();

        OnPageChanged?.Invoke(newPage);
    }
}
