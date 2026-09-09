using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ShaderViewer.ViewModels;

/// <summary>
/// Editor-facing representation of the values commonly populated by
/// Terraria's ScreenShaderData. Strings are intentional: they let the user
/// type vector/color expressions without losing an invalid value before it is
/// parsed by the renderer.
/// </summary>
public sealed class ScreenShaderDataSettings : INotifyPropertyChanged
{
    private string _color = "float3(1.0, 1.0, 1.0)";
    private string _secondaryColor = "float3(1.0, 1.0, 1.0)";
    private string _opacity = "1.0";
    private string _intensity = "1.0";
    private string _progress = "0.0";
    private string _direction = "0.0, 1.0";
    private string _targetPosition = "1.0, 1.0";
    private string _imageOffset = "0.0, 0.0";
    private string _imageScale = "1.0, 1.0";
    private string _globalOpacity = "1.0";
    private string _screenPosition = "0.0, 0.0";
    private string _zoom = "1.0, 1.0";

    public string Color { get => _color; set => Set(ref _color, value); }
    public string SecondaryColor { get => _secondaryColor; set => Set(ref _secondaryColor, value); }
    public string Opacity { get => _opacity; set => Set(ref _opacity, value); }
    public string Intensity { get => _intensity; set => Set(ref _intensity, value); }
    public string Progress { get => _progress; set => Set(ref _progress, value); }
    public string Direction { get => _direction; set => Set(ref _direction, value); }
    public string TargetPosition { get => _targetPosition; set => Set(ref _targetPosition, value); }
    public string ImageOffset { get => _imageOffset; set => Set(ref _imageOffset, value); }
    public string ImageScale { get => _imageScale; set => Set(ref _imageScale, value); }
    public string GlobalOpacity { get => _globalOpacity; set => Set(ref _globalOpacity, value); }
    public string ScreenPosition { get => _screenPosition; set => Set(ref _screenPosition, value); }
    public string Zoom { get => _zoom; set => Set(ref _zoom, value); }

    public void Reset()
    {
        Color = "float3(1.0, 1.0, 1.0)";
        SecondaryColor = "float3(1.0, 1.0, 1.0)";
        Opacity = "1.0";
        Intensity = "1.0";
        Progress = "0.0";
        Direction = "0.0, 1.0";
        TargetPosition = "1.0, 1.0";
        ImageOffset = "0.0, 0.0";
        ImageScale = "1.0, 1.0";
        GlobalOpacity = "1.0";
        ScreenPosition = "0.0, 0.0";
        Zoom = "1.0, 1.0";
    }

    public ScreenShaderDataSettings Clone()
    {
        return new ScreenShaderDataSettings
        {
            Color = Color,
            SecondaryColor = SecondaryColor,
            Opacity = Opacity,
            Intensity = Intensity,
            Progress = Progress,
            Direction = Direction,
            TargetPosition = TargetPosition,
            ImageOffset = ImageOffset,
            ImageScale = ImageScale,
            GlobalOpacity = GlobalOpacity,
            ScreenPosition = ScreenPosition,
            Zoom = Zoom
        };
    }

    public void CopyTo(ScreenShaderDataSettings destination)
    {
        destination.Color = Color;
        destination.SecondaryColor = SecondaryColor;
        destination.Opacity = Opacity;
        destination.Intensity = Intensity;
        destination.Progress = Progress;
        destination.Direction = Direction;
        destination.TargetPosition = TargetPosition;
        destination.ImageOffset = ImageOffset;
        destination.ImageScale = ImageScale;
        destination.GlobalOpacity = GlobalOpacity;
        destination.ScreenPosition = ScreenPosition;
        destination.Zoom = Zoom;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
