using System;
using System.Reflection;
using System.Linq;
using TheGame.Core.OS.History;
using TheGame.Core.UI;

namespace NACHOS.Designer;

public class SetPropertyCommand : ICommand {
    private UIElement _target;
    private string _propertyName;
    private object _oldValue;
    private object _newValue;

    public string Description => $"Set {_propertyName}";
    public bool ModifiesDocument => true;

    public SetPropertyCommand(UIElement target, string propertyName, object newValue) {
        _target = target;
        _propertyName = propertyName;
        _newValue = newValue;
        
        var prop = _target.GetType().GetProperty(_propertyName);
        _oldValue = prop.GetValue(_target);
    }

    public void Execute() {
        var prop = _target.GetType().GetProperty(_propertyName);
        prop.SetValue(_target, _newValue);
        _target.OnDesignSelect?.Invoke(); // Refresh selection or UI
    }

    public void Undo() {
        var prop = _target.GetType().GetProperty(_propertyName);
        prop.SetValue(_target, _oldValue);
        _target.OnDesignSelect?.Invoke();
    }

    public bool CanMerge(ICommand other) {
        if (other is SetPropertyCommand next && next._target == _target && next._propertyName == _propertyName) {
            return true; 
        }
        return false;
    }

    public void MergeWith(ICommand other) {
        var next = (SetPropertyCommand)other;
        _newValue = next._newValue;
    }

    public void Dispose() {
        _target = null;
    }
}

public class AddElementCommand : ICommand {
    private UIElement _parent;
    private UIElement _element;

    public string Description => $"Add {_element.GetType().Name}";
    public bool ModifiesDocument => true;

    public AddElementCommand(UIElement parent, UIElement element) {
        _parent = parent;
        _element = element;
    }

    public void Execute() {
        _parent.AddChild(_element);
    }

    public void Undo() {
        _parent.RemoveChild(_element);
    }

    public bool CanMerge(ICommand other) => false;
    public void MergeWith(ICommand other) => throw new NotSupportedException();
    
    public void Dispose() {
        _parent = null;
        _element = null;
    }
}

public class RemoveElementCommand : ICommand {
    private UIElement _parent;
    private UIElement _element;
    private int _index;

    public string Description => $"Remove {_element.GetType().Name}";
    public bool ModifiesDocument => true;

    public RemoveElementCommand(UIElement parent, UIElement element) {
        _parent = parent;
        _element = element;
        // Capture index for restoration
        _index = _parent.Children.ToList().IndexOf(_element);
    }

    public void Execute() {
        _parent.RemoveChild(_element);
    }

    public void Undo() {
        if (_index >= 0) _parent.InsertChild(_index, _element);
        else _parent.AddChild(_element);
    }

    public bool CanMerge(ICommand other) => false;
    public void MergeWith(ICommand other) => throw new NotSupportedException();

    public void Dispose() {
        _parent = null;
        _element = null;
    }
}

public class InsertElementCommand : ICommand {
    private UIElement _parent;
    private UIElement _element;
    private int _index;

    public string Description => $"Insert {_element.GetType().Name}";
    public bool ModifiesDocument => true;

    public InsertElementCommand(UIElement parent, int index, UIElement element) {
        _parent = parent;
        _index = index;
        _element = element;
    }

    public void Execute() {
        _parent.InsertChild(_index, _element);
    }

    public void Undo() {
        _parent.RemoveChild(_element);
    }

    public bool CanMerge(ICommand other) => false;
    public void MergeWith(ICommand other) => throw new NotSupportedException();

    public void Dispose() {
        _parent = null;
        _element = null;
    }
}
