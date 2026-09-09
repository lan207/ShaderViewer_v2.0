using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ShaderViewer.ViewModels;

/// <summary>
/// A named snapshot of the values sent through the common ScreenShaderData
/// adapter. Profiles are independent from the currently active values.
/// </summary>
public sealed class ScreenShaderDataProfile : INotifyPropertyChanged
{
    private string _name;

    public ScreenShaderDataProfile(string name, ScreenShaderDataSettings values)
    {
        _name = name;
        Values = Clone(values);
    }

    public string Name
    {
        get => _name;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "Unnamed scheme" : value.Trim();
            if (_name == normalized)
                return;

            _name = normalized;
            OnPropertyChanged();
        }
    }

    public ScreenShaderDataSettings Values { get; }

    /// <summary>Absolute path of the persisted profile, when available.</summary>
    [JsonIgnore]
    public string? FilePath { get; set; }

    public void ApplyTo(ScreenShaderDataSettings destination)
    {
        destination.Color = Values.Color;
        destination.SecondaryColor = Values.SecondaryColor;
        destination.Opacity = Values.Opacity;
        destination.Intensity = Values.Intensity;
        destination.Progress = Values.Progress;
        destination.Direction = Values.Direction;
        destination.TargetPosition = Values.TargetPosition;
        destination.ImageOffset = Values.ImageOffset;
        destination.ImageScale = Values.ImageScale;
        destination.GlobalOpacity = Values.GlobalOpacity;
        destination.ScreenPosition = Values.ScreenPosition;
        destination.Zoom = Values.Zoom;
    }

    public void UpdateFrom(ScreenShaderDataSettings source)
    {
        Values.Color = source.Color;
        Values.SecondaryColor = source.SecondaryColor;
        Values.Opacity = source.Opacity;
        Values.Intensity = source.Intensity;
        Values.Progress = source.Progress;
        Values.Direction = source.Direction;
        Values.TargetPosition = source.TargetPosition;
        Values.ImageOffset = source.ImageOffset;
        Values.ImageScale = source.ImageScale;
        Values.GlobalOpacity = source.GlobalOpacity;
        Values.ScreenPosition = source.ScreenPosition;
        Values.Zoom = source.Zoom;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static ScreenShaderDataSettings Clone(ScreenShaderDataSettings source)
    {
        var copy = new ScreenShaderDataSettings();
        copy.Color = source.Color;
        copy.SecondaryColor = source.SecondaryColor;
        copy.Opacity = source.Opacity;
        copy.Intensity = source.Intensity;
        copy.Progress = source.Progress;
        copy.Direction = source.Direction;
        copy.TargetPosition = source.TargetPosition;
        copy.ImageOffset = source.ImageOffset;
        copy.ImageScale = source.ImageScale;
        copy.GlobalOpacity = source.GlobalOpacity;
        copy.ScreenPosition = source.ScreenPosition;
        copy.Zoom = source.Zoom;
        return copy;
    }
}
