using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ShaderViewer.ViewModels;

public sealed class UniformSlot : INotifyPropertyChanged
{
    private string _value;

    public UniformSlot(string label, string value)
    {
        Label = label;
        _value = value;
    }

    public string Label { get; }

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
