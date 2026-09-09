using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ShaderViewer.ViewModels;

public sealed class TextureSlot : INotifyPropertyChanged
{
    private string _path;
    private ImageSource? _preview;
    private bool _isRequired;
    private string _warning = string.Empty;

    public TextureSlot(string label, string path)
    {
        Label = label;
        _path = path;
    }

    public string Label { get; }

    public string Path
    {
        get => _path;
        set
        {
            if (_path == value)
            {
                return;
            }

            _path = value;
            OnPropertyChanged();
        }
    }

    public ImageSource? Preview
    {
        get => _preview;
        set
        {
            if (ReferenceEquals(_preview, value))
            {
                return;
            }

            _preview = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets whether the currently compiled shader reads this sampler.  This is
    /// derived from the source and is intentionally not editable in the UI.
    /// </summary>
    public bool IsRequired
    {
        get => _isRequired;
        private set
        {
            if (_isRequired == value)
            {
                return;
            }

            _isRequired = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RequirementText));
        }
    }

    /// <summary>Human-readable reason why a required texture cannot be used.</summary>
    public string Warning
    {
        get => _warning;
        private set
        {
            if (_warning == value)
            {
                return;
            }

            _warning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasWarning));
        }
    }

    public bool HasWarning => !string.IsNullOrWhiteSpace(Warning);

    public string RequirementText => IsRequired ? "Required by the current shader" : "Optional / not read by the current shader";

    public void SetValidation(bool isRequired, string? warning)
    {
        IsRequired = isRequired;
        Warning = warning ?? string.Empty;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
