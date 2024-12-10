using System;

public class NotifyingProperty<T>
{
    public NotifyingProperty()
    {
        _value = default(T);
    }

    public NotifyingProperty(T initialValue)
    {
        _value = initialValue;
    }

    public T value
    {
        get
        {
            return _value;
        }
        set
        {
            if (_value.Equals(value))
                return;

            var oldValue = _value;
            _value = value;
            onChanged?.Invoke(value, oldValue);
        }
    }

    public void Set(T value)
    {
        this.value = value;
    }

    private T _value;

    public event Action<T, T> onChanged;
}

public class PropertyBinding<T> : IDisposable
{
    private Action<T, T> _action;
    private NotifyingProperty<T> _property;

    public PropertyBinding(NotifyingProperty<T> property, Action<T, T> onChanged)
    {
        property.onChanged += onChanged;
        _property = property;
        _action = onChanged;
    }

    public PropertyBinding(NotifyingProperty<T> property, Action<T, T> onChanged, Action<T> onBind) : this(property, onChanged)
    {
        onBind?.Invoke(property.value);
    }

    public PropertyBinding(NotifyingProperty<T> property, Action<T, T> onChanged, Action<T, T> onBind) : this(property, onChanged)
    {
        onBind?.Invoke(property.value, property.value);
    }

    private bool _disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        _property.onChanged -= _action;
        _action = null;
        _property = null;

        _disposed = true;
    }

    ~PropertyBinding()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
