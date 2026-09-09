using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ShaderViewer.ViewModels;

public sealed class ShaderParameterSlot : INotifyPropertyChanged
{
    private string _name;
    private string _type;
    private string _value;
    private readonly bool _isReadOnly;
    private readonly string _description;
    private readonly bool _canRemove;

    public ShaderParameterSlot(string name, string type, string value, bool isReadOnly = false, string? description = null, bool canRemove = true)
    {
        _name = name;
        _type = type;
        _value = value;
        _isReadOnly = isReadOnly;
        _description = description ?? string.Empty;
        _canRemove = canRemove;
    }

    public bool IsReadOnly => _isReadOnly;

    public bool IsEditable => !_isReadOnly;

    public bool CanRemove => _canRemove && IsEditable;

    public bool IsNameReadOnly => _isReadOnly || !_canRemove;

    public bool IsTypeEditable => IsEditable && _canRemove;

    public string Description => _description;

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            OnPropertyChanged();
        }
    }

    public string Type
    {
        get => _type;
        set
        {
            if (_type == value)
            {
                return;
            }

            _type = value;
            OnPropertyChanged();
        }
    }

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value)
            {
                return;
            }

            _value = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
