using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ShaderViewer.ViewModels;

public enum SceneFilterPriority
{
    VeryLow,
    Low,
    Medium,
    High,
    VeryHigh
}

/// <summary>
/// Serializable equivalent of Terraria's named Filter plus its
/// ScreenShaderData configuration. RuntimeOpacity is deliberately transient:
/// FilterManager reconstructs it through its one-second fade on activation.
/// </summary>
public sealed class SceneFilterDefinition : INotifyPropertyChanged
{
    private string _name = "Filter";
    private string _shaderPath = string.Empty;
    private string _shaderSource = string.Empty;
    private string _entryPoint = "main";
    private SceneFilterPriority _priority = SceneFilterPriority.VeryLow;
    private bool _active = true;
    private bool _isHidden;
    private float _runtimeOpacity;

    public string Name { get => _name; set => Set(ref _name, string.IsNullOrWhiteSpace(value) ? "Filter" : value.Trim()); }
    public string ShaderPath { get => _shaderPath; set => Set(ref _shaderPath, value ?? string.Empty); }
    public string ShaderSource { get => _shaderSource; set => Set(ref _shaderSource, value ?? string.Empty); }
    public string EntryPoint { get => _entryPoint; set => Set(ref _entryPoint, string.IsNullOrWhiteSpace(value) ? "main" : value.Trim()); }
    public SceneFilterPriority Priority { get => _priority; set => Set(ref _priority, value); }
    public bool Active { get => _active; set => Set(ref _active, value); }
    public bool IsHidden { get => _isHidden; set => Set(ref _isHidden, value); }
    public ScreenShaderDataSettings Values { get; set; } = new();
    public List<SceneFilterParameterDefinition> Parameters { get; set; } = new();
    public List<string> TexturePaths { get; set; } = new();

    [JsonIgnore]
    public string SourceSummary => string.IsNullOrWhiteSpace(ShaderPath)
        ? $"{EntryPoint} (unsaved source)"
        : $"{Path.GetFileName(ShaderPath)} → {EntryPoint}";

    [JsonIgnore]
    public float RuntimeOpacity { get => _runtimeOpacity; set => Set(ref _runtimeOpacity, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SceneFilterDefinition Clone()
    {
        return new SceneFilterDefinition
        {
            Name = Name,
            ShaderPath = ShaderPath,
            ShaderSource = ShaderSource,
            EntryPoint = EntryPoint,
            Priority = Priority,
            Active = Active,
            IsHidden = IsHidden,
            Values = Values.Clone(),
            Parameters = Parameters.Select(parameter => new SceneFilterParameterDefinition
            {
                Name = parameter.Name,
                Type = parameter.Type,
                Value = parameter.Value
            }).ToList(),
            TexturePaths = TexturePaths.ToList()
        };
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if (propertyName is nameof(ShaderPath) or nameof(EntryPoint))
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SourceSummary)));
    }
}

public sealed class SceneFilterParameterDefinition
{
    public string Name { get; set; } = "Parameter";
    public string Type { get; set; } = "float";
    public string Value { get; set; } = "0";
}

public sealed class SceneFilterStackDocument
{
    public int Version { get; set; } = 1;
    public int FilterLimit { get; set; } = 16;
    public SceneFilterPriority PriorityThreshold { get; set; } = SceneFilterPriority.VeryLow;
    public List<SceneFilterDefinition> Filters { get; set; } = new();
}
