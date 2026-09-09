using Microsoft.Win32;
using ShaderViewer.Rendering;
using ShaderViewer.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;

namespace ShaderViewer;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly ShaderPreviewEffect _effect = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private bool _isReady;
    private string? _currentShaderPath;
    private string _currentEntryPoint = "main";
    private string _currentShaderModel = "ps_2_0";
    private ShaderModel _requestedShaderModel = ShaderModel.Ps20;
    private string? _declaredShaderProfile;
    private Process? _spriteBatchProcess;
    private string? _spriteBatchEffectDirectory;
    private bool _geometryUserSelected;
    private int _nextScreenShaderDataProfileNumber = 1;
    private string? _sceneFilterStackPath;
    private int _sceneFilterLimit = 16;
    private SceneFilterPriority _sceneFilterPriorityThreshold = SceneFilterPriority.VeryLow;
    private static readonly HashSet<string> ScreenShaderParameterNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "uColor", "uSecondaryColor", "uSecondColor", "uOpacity", "uIntensity",
        "uProgress", "uDirection", "uTargetPosition", "uImageOffset", "uImageScale",
        "uGlobalOpacity", "uScreenPosition", "uZoom"
    };
    private static readonly JsonSerializerOptions ScreenShaderDataProfileJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Draw state used by the SpriteBatch-compatible preview surface.  The
    /// current WPF backend renders one sprite, so ordering/depth are retained
    /// as state and displayed to the user while blend/sampler are applied by a
    /// future native SpriteBatch backend.
    /// </summary>
    public SpriteBatchSettings DrawSettings { get; } = new();
    public ScreenShaderDataSettings ScreenShaderData { get; } = new();

    public ObservableCollection<TextureSlot> TextureSlots { get; } = new();
    public ObservableCollection<ShaderParameterSlot> ParameterSlots { get; } = new();
    public ObservableCollection<ShaderDiagnosticItem> Diagnostics { get; } = new();
    public ObservableCollection<ScreenShaderDataProfile> ScreenShaderDataProfiles { get; } = new();
    public ObservableCollection<SceneFilterDefinition> SceneFilters { get; } = new();
    public IReadOnlyList<SceneFilterPriority> SceneFilterPriorities { get; } = Enum.GetValues<SceneFilterPriority>();
    public int SceneFilterLimit
    {
        get => _sceneFilterLimit;
        set
        {
            var normalized = Math.Clamp(value, 0, 16);
            if (_sceneFilterLimit == normalized) return;
            _sceneFilterLimit = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SceneFilterLimit)));
        }
    }
    public SceneFilterPriority SceneFilterPriorityThreshold
    {
        get => _sceneFilterPriorityThreshold;
        set
        {
            if (_sceneFilterPriorityThreshold == value) return;
            _sceneFilterPriorityThreshold = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SceneFilterPriorityThreshold)));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    private string DisplaySourceName => string.IsNullOrWhiteSpace(_currentShaderPath)
        ? "Source.fx"
        : Path.GetFileName(_currentShaderPath);

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        BuildSlots();
        PreviewHost.Effect = _effect;
        PreviewHost.Background = Brushes.Transparent;
        ApplyPreviewBackground();
        PreviewCanvas.SizeChanged += (_, _) => ApplyPreviewGeometry();

        SourceBox.Text = DefaultShaderSource;
        Loaded += (_, _) =>
        {
            _isReady = true;
            ApplyPreviewGeometry();
            ApplyAll();
            Dispatcher.BeginInvoke(ApplyPreviewGeometry, System.Windows.Threading.DispatcherPriority.Loaded);
        };
        CompositionTarget.Rendering += (_, _) => UpdateTime();
        Closed += (_, _) => CloseSpriteBatchPreview();
    }

    private void BuildSlots()
    {
        TextureSlots.Add(new TextureSlot("iInput (s0)", ""));
        TextureSlots.Add(new TextureSlot("iChannel0 (s1)", ""));
        TextureSlots.Add(new TextureSlot("iChannel1 (s2)", ""));
        TextureSlots.Add(new TextureSlot("iChannel2 (s3)", ""));

        ResetParameterSlots();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _currentShaderPath = null;
        _currentEntryPoint = "main";
        _currentShaderModel = "ps_2_0";
        _requestedShaderModel = ShaderModel.Ps20;
        _declaredShaderProfile = null;
        _geometryUserSelected = false;
        ScreenShaderDataProfiles.Clear();
        SceneFilters.Clear();
        _sceneFilterStackPath = null;
        SceneFilterLimit = 16;
        SceneFilterPriorityThreshold = SceneFilterPriority.VeryLow;
        _nextScreenShaderDataProfileNumber = 1;
        SourceBox.Text = DefaultShaderSource;
        ScreenShaderData.Reset();
        ApplyScreenShaderData();
        DrawSettings.SortMode = SpriteSortMode.Deferred;
        DrawSettings.BlendState = SpriteBlendState.AlphaBlend;
        DrawSettings.SamplerState = SpriteSamplerState.LinearClamp;
        DrawSettings.DepthStencilState = SpriteDepthStencilState.None;
        if (SpriteSortModeBox is not null) SpriteSortModeBox.SelectedItem = DrawSettings.SortMode;
        if (SpriteBlendStateBox is not null) SpriteBlendStateBox.SelectedItem = DrawSettings.BlendState;
        if (SpriteSamplerStateBox is not null) SpriteSamplerStateBox.SelectedItem = DrawSettings.SamplerState;
        if (SpriteDepthStencilStateBox is not null) SpriteDepthStencilStateBox.SelectedItem = DrawSettings.DepthStencilState;
        if (BackgroundModeBox is not null) BackgroundModeBox.SelectedIndex = 0;
        if (BackgroundColorBox is not null) BackgroundColorBox.Text = "#FFFFFFFF";
        if (PreviewGeometryBox is not null)
        {
            PreviewGeometryBox.SelectedIndex = 0;
            _geometryUserSelected = false;
        }
        ApplyPreviewBackground();
        ApplyPreviewGeometry();
        for (var i = 0; i < TextureSlots.Count; i++)
        {
            TextureSlots[i].Path = "";
            TextureSlots[i].Preview = CreateSolidPreview(Colors.White);
        }
        ResetParameterSlots();
        ApplyAll();
    }

    private void OpenShader_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "HLSL shader (*.hlsl;*.fx;*.txt)|*.hlsl;*.fx;*.txt|All files (*.*)|*.*",
            Title = "Open Shader Source"
        };

        if (dialog.ShowDialog(this) == true)
        {
            LoadShaderFile(dialog.FileName);
        }
    }

    private void LoadTerrariaVortexFilter_Click(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Terraria.FilterTower.fx");
        if (!File.Exists(path))
        {
            path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Terraria.FilterTower.fx"));
        }
        if (!File.Exists(path))
        {
            MessageBox.Show(this, "Terraria.FilterTower.fx was not found.",
                "Built-in Terraria filter", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        LoadShaderFile(path);
    }

    private void LoadShaderFile(string path)
    {
        _currentShaderPath = path;
        var imported = ShaderSourceTools.Import(File.ReadAllText(path));
        SourceBox.Text = imported.Source;
        _currentEntryPoint = imported.EntryPoint;
        _requestedShaderModel = imported.ShaderModel;
        _declaredShaderProfile = imported.DeclaredShaderProfile;
        _currentShaderModel = GetPreviewShaderModelLabel(imported.ShaderModel);
        SelectAutomaticGeometry(imported.Source, imported.EntryPoint);
        if (IsForceFieldSource(imported.Source, imported.EntryPoint))
        {
            ApplyForceFieldPreset();
        }
        else if (IsSceneFilterSource(imported.EntryPoint))
        {
            ApplySceneFilterPreset(imported.EntryPoint);
        }
        ResetParameterSlots();
        if (imported.Parameters.Count > 0)
        {
            LoadParameterSlots(imported.Parameters);
        }

        StatusText.Text = $"{Path.GetFileName(path)} -> {_currentEntryPoint}";
        ApplyAll();
    }

    private void Compile_Click(object sender, RoutedEventArgs e) => ApplyAll();

    private void OpenSpriteBatch_Click(object sender, RoutedEventArgs e)
    {
        CloseSpriteBatchPreview();
        var executable = Path.Combine(AppContext.BaseDirectory, "SpriteBatchPreviewBackend.exe");
        if (!File.Exists(executable))
        {
            var projectExecutable = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..",
                "SpriteBatchPreviewBackend", "bin", "Debug", "net9.0-windows", "SpriteBatchPreviewBackend.exe"));
            executable = File.Exists(projectExecutable) ? projectExecutable : executable;
        }

        if (!File.Exists(executable))
        {
            StatusText.Text = "SpriteBatch backend not found. Build SpriteBatchPreviewBackend first.";
            return;
        }

        var background = BackgroundModeBox.SelectedIndex == 0 ? "checkerboard" : "solid";
        var color = BackgroundColorBox.Text.Trim();
        var geometry = GetPreviewGeometryTag();
        var nativeFilterStack = geometry == "scene-filter" && SceneFilters.Count > 0
            ? PrepareNativeFilterStack()
            : null;
        var nativeEffect = nativeFilterStack is null ? PrepareNativeEffect() : null;
        var arguments = string.Join(" ",
            $"--owner-hwnd {QuoteProcessArg(new WindowInteropHelper(this).Handle.ToInt64().ToString(CultureInfo.InvariantCulture))}",
            $"--sort {QuoteProcessArg(DrawSettings.SortMode.ToString())}",
            $"--blend {QuoteProcessArg(DrawSettings.BlendState.ToString())}",
            $"--sampler {QuoteProcessArg(DrawSettings.SamplerState.ToString())}",
            $"--depth {QuoteProcessArg(DrawSettings.DepthStencilState.ToString())}",
            $"--background {QuoteProcessArg(background)}",
            $"--color {QuoteProcessArg(color)}",
            $"--filter-color {QuoteProcessArg(ScreenShaderData.Color)}",
            $"--secondary-color {QuoteProcessArg(ScreenShaderData.SecondaryColor)}",
            $"--opacity {QuoteProcessArg(ScreenShaderData.Opacity)}",
            $"--intensity {QuoteProcessArg(ScreenShaderData.Intensity)}",
            $"--progress {QuoteProcessArg(ScreenShaderData.Progress)}",
            $"--direction {QuoteProcessArg(ScreenShaderData.Direction)}",
            $"--target-position {QuoteProcessArg(ScreenShaderData.TargetPosition)}",
            $"--image-offset {QuoteProcessArg(ScreenShaderData.ImageOffset)}",
            $"--image-scale {QuoteProcessArg(ScreenShaderData.ImageScale)}",
            $"--global-opacity {QuoteProcessArg(ScreenShaderData.GlobalOpacity)}",
            $"--screen-position {QuoteProcessArg(ScreenShaderData.ScreenPosition)}",
            $"--zoom {QuoteProcessArg(ScreenShaderData.Zoom)}",
            $"--geometry {QuoteProcessArg(geometry)}",
            $"--sprite-scale {QuoteProcessArg("1.0")}");
        if (nativeEffect is not null)
        {
            arguments += $" --effect {QuoteProcessArg(nativeEffect)}";
        }
        if (nativeFilterStack is not null)
        {
            arguments += $" --filter-stack {QuoteProcessArg(nativeFilterStack)}";
        }
        if (TextureSlots.Count > 0 && File.Exists(TextureSlots[0].Path))
        {
            arguments += $" --texture {QuoteProcessArg(TextureSlots[0].Path)}";
        }
        for (var index = 1; index < Math.Min(TextureSlots.Count, 4); index++)
        {
            if (File.Exists(TextureSlots[index].Path))
                arguments += $" --channel{index - 1} {QuoteProcessArg(TextureSlots[index].Path)}";
        }

        _spriteBatchProcess = Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(executable)!
        });
        StatusText.Text = nativeFilterStack is not null
            ? $"Opened Terraria-style Filters.Scene preview with {SceneFilters.Count} registered filter(s)."
            : nativeEffect is null
            ? "Opened MonoGame SpriteBatch floating preview window (texture-only fallback; MGFX compilation unavailable)."
            : "Opened MonoGame SpriteBatch floating preview window with compiled effect.";
    }

    private void CloseSpriteBatchPreview()
    {
        if (_spriteBatchProcess is null)
        {
            return;
        }

        try
        {
            if (!_spriteBatchProcess.HasExited)
            {
                _spriteBatchProcess.CloseMainWindow();
                if (!_spriteBatchProcess.WaitForExit(500))
                {
                    _spriteBatchProcess.Kill(entireProcessTree: true);
                }
            }
        }
        catch
        {
            // The preview may have already closed independently.
        }
        finally
        {
            _spriteBatchProcess.Dispose();
            _spriteBatchProcess = null;
        }
    }

    private string GetPreviewGeometryTag()
        => PreviewGeometryBox?.SelectedItem is System.Windows.Controls.ComboBoxItem item
            && item.Tag is string tag
            && !string.IsNullOrWhiteSpace(tag)
            ? tag
            : "surface";

    private string? PrepareNativeEffect()
    {
        var mgcb = FindMgcbTool();
        if (mgcb is null)
        {
            return null;
        }

        try
        {
            _spriteBatchEffectDirectory = Path.Combine(
                Path.GetTempPath(), "ShaderViewer", $"sprite-effect-{Guid.NewGuid():N}");
            var outputDirectory = Path.Combine(_spriteBatchEffectDirectory, "out");
            var intermediateDirectory = Path.Combine(_spriteBatchEffectDirectory, "obj");
            Directory.CreateDirectory(outputDirectory);
            Directory.CreateDirectory(intermediateDirectory);

            var effectSourcePath = Path.Combine(_spriteBatchEffectDirectory, "Preview.fx");
            var effectSource = ShaderSourceTools.BuildMonoGameSpriteEffectSource(
                SourceBox.Text,
                GetEditableParameters(),
                out var nativeEntryPoint);
            File.WriteAllText(effectSourcePath, effectSource, Encoding.UTF8);

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = string.Join(" ",
                    QuoteCommandLine(mgcb),
                    "/platform:Windows",
                    $"/outputDir:{QuoteCommandLine(outputDirectory)}",
                    $"/intermediateDir:{QuoteCommandLine(intermediateDirectory)}",
                    $"/build:{QuoteCommandLine(effectSourcePath)}"),
                WorkingDirectory = _spriteBatchEffectDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            if (process is null || !process.WaitForExit(30000) || process.ExitCode != 0)
            {
                process?.Kill(entireProcessTree: true);
                return null;
            }

            var xnbPath = Path.Combine(outputDirectory, "Preview.xnb");
            return File.Exists(xnbPath) ? xnbPath : null;
        }
        catch
        {
            return null;
        }
    }

    private string? PrepareNativeFilterStack()
    {
        var mgcb = FindMgcbTool();
        if (mgcb is null || SceneFilters.Count == 0)
            return null;

        try
        {
            _spriteBatchEffectDirectory = Path.Combine(
                Path.GetTempPath(), "ShaderViewer", $"filter-stack-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_spriteBatchEffectDirectory);
            var runtimeFilters = new List<NativeSceneFilterDocument>();

            for (var index = 0; index < SceneFilters.Count; index++)
            {
                var filter = SceneFilters[index];
                if (string.IsNullOrWhiteSpace(filter.ShaderSource))
                    throw new InvalidDataException($"Filter '{filter.Name}' has no shader source.");

                var filterDirectory = Path.Combine(_spriteBatchEffectDirectory, $"filter-{index:D2}");
                var outputDirectory = Path.Combine(filterDirectory, "out");
                var intermediateDirectory = Path.Combine(filterDirectory, "obj");
                Directory.CreateDirectory(outputDirectory);
                Directory.CreateDirectory(intermediateDirectory);
                var sourcePath = Path.Combine(filterDirectory, $"Filter{index:D2}.fx");
                var taggedSource = $"/* ShaderViewer Entry Point: {filter.EntryPoint} */{Environment.NewLine}{filter.ShaderSource}";
                var nativeParameters = filter.Parameters
                    .Select(parameter => new ShaderParameterDefinition(parameter.Name, parameter.Type, parameter.Value))
                    .ToList();
                var nativeSource = ShaderSourceTools.BuildMonoGameSpriteEffectSource(
                    taggedSource, nativeParameters, out _);
                File.WriteAllText(sourcePath, nativeSource, Encoding.UTF8);

                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = string.Join(" ",
                        QuoteCommandLine(mgcb),
                        "/platform:Windows",
                        $"/outputDir:{QuoteCommandLine(outputDirectory)}",
                        $"/intermediateDir:{QuoteCommandLine(intermediateDirectory)}",
                        $"/build:{QuoteCommandLine(sourcePath)}"),
                    WorkingDirectory = filterDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }) ?? throw new InvalidOperationException("Could not start MGCB.");
                var standardOutput = process.StandardOutput.ReadToEndAsync();
                var standardError = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(30000))
                {
                    process.Kill(entireProcessTree: true);
                    throw new TimeoutException($"Compiling filter '{filter.Name}' timed out.");
                }
                Task.WaitAll(standardOutput, standardError);
                if (process.ExitCode != 0)
                    throw new InvalidDataException($"Filter '{filter.Name}' failed to compile:{Environment.NewLine}{standardOutput.Result}{standardError.Result}");

                var xnbPath = Path.Combine(outputDirectory, $"Filter{index:D2}.xnb");
                if (!File.Exists(xnbPath))
                    throw new FileNotFoundException($"MGCB did not produce an XNB for filter '{filter.Name}'.", xnbPath);

                runtimeFilters.Add(new NativeSceneFilterDocument
                {
                    Name = filter.Name,
                    EffectPath = xnbPath,
                    EntryPoint = filter.EntryPoint,
                    Priority = (int)filter.Priority,
                    Active = filter.Active,
                    IsHidden = filter.IsHidden,
                    Values = filter.Values.Clone(),
                    TexturePaths = filter.TexturePaths.ToList()
                });
            }

            var runtime = new NativeSceneFilterStackDocument
            {
                FilterLimit = SceneFilterLimit,
                PriorityThreshold = (int)SceneFilterPriorityThreshold,
                Filters = runtimeFilters
            };
            var runtimePath = Path.Combine(_spriteBatchEffectDirectory, "Filters.Scene.runtime.json");
            File.WriteAllText(runtimePath,
                JsonSerializer.Serialize(runtime, ScreenShaderDataProfileJsonOptions), Encoding.UTF8);
            return runtimePath;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not prepare filter stack", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
    }

    private static string? FindMgcbTool()
    {
        var packageRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (string.IsNullOrWhiteSpace(packageRoot))
        {
            packageRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget", "packages");
        }

        var toolRoot = Path.Combine(packageRoot, "dotnet-mgcb");
        if (!Directory.Exists(toolRoot))
        {
            return null;
        }

        return Directory.EnumerateFiles(toolRoot, "mgcb.dll", SearchOption.AllDirectories)
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string QuoteCommandLine(string value)
        => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private static string QuoteProcessArg(string value)
        => QuoteCommandLine(value);

    private void SpriteBatchSetting_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SpriteSortModeBox?.SelectedItem is SpriteSortMode sortMode)
        {
            DrawSettings.SortMode = sortMode;
        }

        if (SpriteBlendStateBox?.SelectedItem is SpriteBlendState blendState)
        {
            DrawSettings.BlendState = blendState;
        }

        if (SpriteSamplerStateBox?.SelectedItem is SpriteSamplerState samplerState)
        {
            DrawSettings.SamplerState = samplerState;
        }

        if (SpriteDepthStencilStateBox?.SelectedItem is SpriteDepthStencilState depthStencilState)
        {
            DrawSettings.DepthStencilState = depthStencilState;
        }

        if (_isReady)
        {
            StatusText.Text = $"SpriteBatch.Begin({DrawSettings.SortMode}, {DrawSettings.BlendState}, {DrawSettings.SamplerState}, {DrawSettings.DepthStencilState})";
        }
    }

    private void BackgroundMode_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => ApplyPreviewBackground();

    private void BackgroundColor_Apply(object sender, RoutedEventArgs e)
        => ApplyPreviewBackground();

    private void PreviewGeometry_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isReady)
        {
            _geometryUserSelected = true;
            ApplyTextures();
        }

        ApplyPreviewGeometry();
    }

    private void ScreenShaderData_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isReady)
        {
            ApplyScreenShaderData();
        }
    }

    private void ResetScreenShaderData_Click(object sender, RoutedEventArgs e)
    {
        ScreenShaderData.Reset();
        ApplyScreenShaderData();
    }

    private void RegisterCurrentSceneFilter_Click(object sender, RoutedEventArgs e)
    {
        var definition = CaptureCurrentSceneFilter($"Filter {SceneFilters.Count + 1}");
        SceneFilters.Add(definition);
        if (PreviewGeometryBox is not null)
            PreviewGeometryBox.SelectedIndex = 3;
        StatusText.Text = $"Registered Filters.Scene[\"{definition.Name}\"] from {definition.SourceSummary}.";
    }

    private SceneFilterDefinition CaptureCurrentSceneFilter(string name)
    {
        return new SceneFilterDefinition
        {
            Name = name,
            ShaderPath = _currentShaderPath ?? string.Empty,
            ShaderSource = SourceBox.Text,
            EntryPoint = _currentEntryPoint,
            Priority = SceneFilterPriority.VeryLow,
            Active = true,
            Values = ScreenShaderData.Clone(),
            Parameters = GetEditableParameters().Select(parameter => new SceneFilterParameterDefinition
            {
                Name = parameter.Name,
                Type = parameter.Type,
                Value = parameter.Value
            }).ToList(),
            TexturePaths = TextureSlots.Select(slot => slot.Path ?? string.Empty).ToList()
        };
    }

    private void EditSceneFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not SceneFilterDefinition filter)
            return;

        _currentShaderPath = string.IsNullOrWhiteSpace(filter.ShaderPath) ? null : filter.ShaderPath;
        SourceBox.Text = filter.ShaderSource;
        _currentEntryPoint = filter.EntryPoint;
        var imported = ShaderSourceTools.Import(filter.ShaderSource);
        _requestedShaderModel = imported.ShaderModel;
        _declaredShaderProfile = imported.DeclaredShaderProfile;
        _currentShaderModel = GetPreviewShaderModelLabel(imported.ShaderModel);
        filter.Values.CopyTo(ScreenShaderData);
        for (var i = 0; i < TextureSlots.Count; i++)
            TextureSlots[i].Path = i < filter.TexturePaths.Count ? filter.TexturePaths[i] : string.Empty;
        ResetParameterSlots();
        IReadOnlyList<ShaderParameterDefinition> parameters = filter.Parameters.Count > 0
            ? filter.Parameters.Select(parameter => new ShaderParameterDefinition(parameter.Name, parameter.Type, parameter.Value)).ToList()
            : imported.Parameters;
        if (parameters.Count > 0)
            LoadParameterSlots(parameters);
        ApplyAll();
        StatusText.Text = $"Editing Filters.Scene[\"{filter.Name}\"]. Use Capture current to update its snapshot.";
    }

    private void UpdateSceneFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not SceneFilterDefinition filter)
            return;

        filter.ShaderPath = _currentShaderPath ?? string.Empty;
        filter.ShaderSource = SourceBox.Text;
        filter.EntryPoint = _currentEntryPoint;
        filter.Values = ScreenShaderData.Clone();
        filter.Parameters = GetEditableParameters().Select(parameter => new SceneFilterParameterDefinition
        {
            Name = parameter.Name,
            Type = parameter.Type,
            Value = parameter.Value
        }).ToList();
        filter.TexturePaths = TextureSlots.Select(slot => slot.Path ?? string.Empty).ToList();
        StatusText.Text = $"Updated registered filter: {filter.Name}.";
    }

    private void RemoveSceneFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is SceneFilterDefinition filter)
        {
            SceneFilters.Remove(filter);
            StatusText.Text = $"Unregistered Filters.Scene[\"{filter.Name}\"].";
        }
    }

    private void DeactivateAllSceneFilters_Click(object sender, RoutedEventArgs e)
    {
        foreach (var filter in SceneFilters)
            filter.Active = false;
        StatusText.Text = "All registered scene filters will fade out at 1 opacity unit per second.";
    }

    private void SaveSceneFilterStack_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Scene filter stack (*.filters.json)|*.filters.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Save Terraria-style Filter Stack",
            FileName = _sceneFilterStackPath is null ? "Filters.Scene.filters.json" : Path.GetFileName(_sceneFilterStackPath),
            DefaultExt = ".filters.json",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var document = new SceneFilterStackDocument
            {
                FilterLimit = SceneFilterLimit,
                PriorityThreshold = SceneFilterPriorityThreshold,
                Filters = SceneFilters.Select(filter => filter.Clone()).ToList()
            };
            _sceneFilterStackPath = Path.GetFullPath(dialog.FileName);
            File.WriteAllText(_sceneFilterStackPath,
                JsonSerializer.Serialize(document, ScreenShaderDataProfileJsonOptions), Encoding.UTF8);
            StatusText.Text = $"Saved filter stack: {_sceneFilterStackPath}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not save filter stack", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadSceneFilterStack_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Scene filter stack (*.filters.json)|*.filters.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Load Terraria-style Filter Stack"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var document = JsonSerializer.Deserialize<SceneFilterStackDocument>(
                File.ReadAllText(dialog.FileName), ScreenShaderDataProfileJsonOptions)
                ?? throw new InvalidDataException("The file does not contain a filter stack.");
            SceneFilters.Clear();
            foreach (var filter in document.Filters)
                SceneFilters.Add(filter);
            SceneFilterLimit = document.FilterLimit;
            SceneFilterPriorityThreshold = document.PriorityThreshold;
            _sceneFilterStackPath = Path.GetFullPath(dialog.FileName);
            StatusText.Text = $"Loaded {SceneFilters.Count} filter(s) from {_sceneFilterStackPath}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not load filter stack", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddScreenShaderDataProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = new ScreenShaderDataProfile(
            $"Custom scheme {_nextScreenShaderDataProfileNumber}",
            ScreenShaderData);
        if (!SaveScreenShaderDataProfileAs(profile))
            return;

        _nextScreenShaderDataProfileNumber++;
        ScreenShaderDataProfiles.Add(profile);
        StatusText.Text = $"Saved ScreenShaderData scheme: {profile.FilePath}";
    }

    private void LoadScreenShaderDataProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "ScreenShaderData scheme (*.screen.json)|*.screen.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Load ScreenShaderData Scheme",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        var loaded = 0;
        var errors = new List<string>();
        foreach (var path in dialog.FileNames)
        {
            try
            {
                var document = JsonSerializer.Deserialize<ScreenShaderDataProfileDocument>(
                    File.ReadAllText(path),
                    ScreenShaderDataProfileJsonOptions);
                if (document?.Values is null || string.IsNullOrWhiteSpace(document.Name))
                    throw new InvalidDataException("The file does not contain a valid scheme name and Values object.");

                var fullPath = Path.GetFullPath(path);
                var existing = ScreenShaderDataProfiles.FirstOrDefault(item =>
                    string.Equals(item.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
                if (existing is not null)
                {
                    existing.Name = document.Name;
                    existing.UpdateFrom(document.Values);
                }
                else
                {
                    ScreenShaderDataProfiles.Add(new ScreenShaderDataProfile(document.Name, document.Values)
                    {
                        FilePath = fullPath
                    });
                }
                loaded++;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        StatusText.Text = errors.Count == 0
            ? $"Loaded {loaded} ScreenShaderData scheme file(s)."
            : $"Loaded {loaded}; failed {errors.Count}.";
        if (errors.Count > 0)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, errors),
                "ScreenShaderData scheme load error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ApplyScreenShaderDataProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not ScreenShaderDataProfile profile)
            return;

        profile.ApplyTo(ScreenShaderData);
        ApplyScreenShaderData();
        StatusText.Text = $"Applied ScreenShaderData scheme: {profile.Name}";
    }

    private void UpdateScreenShaderDataProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not ScreenShaderDataProfile profile)
            return;

        profile.UpdateFrom(ScreenShaderData);
        if (profile.FilePath is null)
        {
            if (!SaveScreenShaderDataProfileAs(profile))
                return;
        }
        else
        {
            if (!TryWriteScreenShaderDataProfile(profile))
                return;
        }
        StatusText.Text = $"Updated ScreenShaderData scheme file: {profile.FilePath}";
    }

    private void DeleteScreenShaderDataProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not ScreenShaderDataProfile profile)
            return;

        if (profile.FilePath is not null && File.Exists(profile.FilePath))
        {
            var choice = MessageBox.Show(
                this,
                $"Delete this scheme file?{Environment.NewLine}{profile.FilePath}",
                "Delete ScreenShaderData scheme",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (choice != MessageBoxResult.Yes)
                return;

            try
            {
                File.Delete(profile.FilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not delete scheme file",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        ScreenShaderDataProfiles.Remove(profile);
        StatusText.Text = $"Deleted ScreenShaderData scheme: {profile.Name}";
    }

    private bool SaveScreenShaderDataProfileAs(ScreenShaderDataProfile profile)
    {
        var safeName = string.Concat(profile.Name.Select(ch =>
            Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        var dialog = new SaveFileDialog
        {
            Filter = "ScreenShaderData scheme (*.screen.json)|*.screen.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Save ScreenShaderData Scheme",
            FileName = safeName + ".screen.json",
            DefaultExt = ".screen.json",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true)
            return false;

        profile.FilePath = Path.GetFullPath(dialog.FileName);
        if (TryWriteScreenShaderDataProfile(profile))
            return true;

        profile.FilePath = null;
        return false;
    }

    private bool TryWriteScreenShaderDataProfile(ScreenShaderDataProfile profile)
    {
        try
        {
            WriteScreenShaderDataProfile(profile);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not save scheme file",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private static void WriteScreenShaderDataProfile(ScreenShaderDataProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.FilePath))
            throw new InvalidOperationException("The ScreenShaderData scheme has no file path.");

        var document = new ScreenShaderDataProfileDocument
        {
            Name = profile.Name,
            Values = profile.Values
        };
        File.WriteAllText(
            profile.FilePath,
            JsonSerializer.Serialize(document, ScreenShaderDataProfileJsonOptions),
            Encoding.UTF8);
    }

    private void ApplyScreenShaderData()
    {
        var errors = new List<string>();
        if (TryParseColorVector(ScreenShaderData.Color, out var color))
            _effect.ScreenColor = color;
        else
            errors.Add("uColor");

        if (TryParseColorVector(ScreenShaderData.SecondaryColor, out var secondaryColor))
            _effect.SecondaryColor = secondaryColor;
        else
            errors.Add("uSecondaryColor");

        var hasOpacity = TryParseScalar(ScreenShaderData.Opacity, out var opacity);
        var hasGlobalOpacity = TryParseScalar(ScreenShaderData.GlobalOpacity, out var globalOpacity);
        if (hasOpacity && hasGlobalOpacity)
            _effect.Opacity = opacity * globalOpacity;
        else
            errors.Add("uOpacity");

        if (TryParseScalar(ScreenShaderData.Intensity, out var intensity))
            _effect.Intensity = intensity;
        else
            errors.Add("uIntensity");

        if (TryParseScalar(ScreenShaderData.Progress, out var progress))
            _effect.Progress = progress;
        else
            errors.Add("uProgress");

        if (TryParseVector(ScreenShaderData.Direction, out var direction))
            _effect.Direction = direction;
        else
            errors.Add("uDirection");

        if (TryParseVector(ScreenShaderData.TargetPosition, out var targetPosition))
            _effect.TargetPosition = targetPosition;
        else
            errors.Add("uTargetPosition");

        if (TryParseVector(ScreenShaderData.ImageOffset, out var imageOffset))
            _effect.ImageOffset = imageOffset;
        else
            errors.Add("uImageOffset");

        if (TryParseVector(ScreenShaderData.ImageScale, out var imageScale))
            _effect.ImageScale = imageScale;
        else
            errors.Add("uImageScale");

        if (hasGlobalOpacity)
            _effect.GlobalOpacity = globalOpacity;
        else
            errors.Add("uGlobalOpacity");

        if (TryParseVector(ScreenShaderData.ScreenPosition, out var screenPosition))
            _effect.ScreenPosition = screenPosition;
        else
            errors.Add("uScreenPosition");

        if (TryParseVector(ScreenShaderData.Zoom, out var zoom))
            _effect.Zoom = zoom;
        else
            errors.Add("uZoom");

        ScreenShaderDataHint.Text = errors.Count == 0
            ? string.Empty
            : $"Invalid value(s): {string.Join(", ", errors)}. Colors use float3(r, g, b); vectors use x, y.";
        RefreshScreenShaderDataBuiltIns();
    }

    private static bool TryParseScalar(string? text, out double value)
        => double.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static bool TryParseColorVector(string? text, out Color color)
    {
        color = Colors.White;
        var normalized = (text ?? string.Empty).Trim();
        // Keep accepting the old format so existing settings do not break, but
        // expose and persist the ScreenShaderData values as float3.
        if (normalized.StartsWith('#') && TryParseBackgroundColor(normalized, out color))
            return true;

        var open = normalized.IndexOf('(');
        if (open >= 0 && normalized.EndsWith(')'))
            normalized = normalized[(open + 1)..^1];
        var parts = normalized.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var r)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var g)
            || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
            return false;

        // Terraria's MiscShaderData deliberately sends HDR values above 1
        // (ForceField uses 1.0..1.5).  ScRGB preserves those values for WPF's
        // pixel-shader constant callback; converting through byte RGB turned
        // 1.5 into byte value 2, effectively black.
        color = Color.FromScRgb(1f, (float)r, (float)g, (float)b);
        return true;
    }

    private static bool TryParseVector(string? text, out Point value)
    {
        value = new Point();
        var normalized = (text ?? string.Empty).Trim();
        var open = normalized.IndexOf('(');
        if (open >= 0 && normalized.EndsWith(')'))
            normalized = normalized[(open + 1)..^1];

        var parts = normalized.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            return false;

        value = new Point(x, y);
        return true;
    }

    private void RefreshScreenShaderDataBuiltIns()
    {
        SetBuiltInValue("uColor", ScreenShaderData.Color);
        SetBuiltInValue("uSecondaryColor", ScreenShaderData.SecondaryColor);
        SetBuiltInValue("uSecondColor", ScreenShaderData.SecondaryColor);
        SetBuiltInValue("uOpacity", ScreenShaderData.Opacity);
        SetBuiltInValue("uIntensity", ScreenShaderData.Intensity);
        SetBuiltInValue("uProgress", ScreenShaderData.Progress);
        SetBuiltInValue("uDirection", ScreenShaderData.Direction);
        SetBuiltInValue("uTargetPosition", ScreenShaderData.TargetPosition);
        SetBuiltInValue("uImageOffset", ScreenShaderData.ImageOffset);
        SetBuiltInValue("uImageScale", ScreenShaderData.ImageScale);
        SetBuiltInValue("uGlobalOpacity", ScreenShaderData.GlobalOpacity);
        SetBuiltInValue("uScreenPosition", ScreenShaderData.ScreenPosition);
        SetBuiltInValue("uZoom", ScreenShaderData.Zoom);
    }

    private void ApplyPreviewBackground()
    {
        if (PreviewCanvas is null || BackgroundModeBox is null || BackgroundColorHint is null)
        {
            return;
        }

        if (BackgroundModeBox.SelectedIndex == 0)
        {
            PreviewCanvas.Background = (Brush)FindResource("CheckerboardBrush");
            BackgroundColorHint.Text = string.Empty;
            return;
        }

        if (BackgroundColorBox is null || !TryParseBackgroundColor(BackgroundColorBox.Text, out var color))
        {
            BackgroundColorHint.Text = "Invalid color. Use #AARRGGBB (for example #80FF0000).";
            return;
        }

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        PreviewCanvas.Background = brush;
        BackgroundColorHint.Text = $"Using ARGB: #{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static bool TryParseBackgroundColor(string? text, out Color color)
    {
        color = Colors.Transparent;
        var value = (text ?? string.Empty).Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        if (value.Length == 6)
        {
            value = "FF" + value;
        }

        if (value.Length != 8 || !uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
        {
            return false;
        }

        color = Color.FromArgb(
            (byte)(argb >> 24),
            (byte)(argb >> 16),
            (byte)(argb >> 8),
            (byte)argb);
        return true;
    }

    private void ExportShader_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "FX shader (*.fx)|*.fx|HLSL shader (*.hlsl)|*.hlsl|All files (*.*)|*.*",
            Title = "Export Shader",
            FileName = string.IsNullOrWhiteSpace(_currentShaderPath)
                ? $"Shader_{_currentEntryPoint}.fx"
                : Path.GetFileNameWithoutExtension(_currentShaderPath) + ".fx"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var exported = ShaderSourceTools.BuildExportSource(SourceBox.Text, _currentShaderModel, GetEditableParameters(), out _currentEntryPoint);
        File.WriteAllText(dialog.FileName, exported, Encoding.UTF8);
        StatusText.Text = $"Exported {Path.GetFileName(dialog.FileName)} as {_currentEntryPoint} ({_currentShaderModel})";
        LogSuccess(StatusText.Text);
    }

    private void AddParam_Click(object sender, RoutedEventArgs e)
    {
        ParameterSlots.Add(new ShaderParameterSlot($"uParam{ParameterSlots.Count}", "float", "0.0"));
    }

    private void RemoveParam_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not ShaderParameterSlot slot)
        {
            return;
        }

        if (!slot.CanRemove || ParameterSlots.Count <= 1)
        {
            return;
        }

        ParameterSlots.Remove(slot);
    }

    private void BrowseTexture_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not TextureSlot slot)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tga|All files (*.*)|*.*",
            Title = $"Open {slot.Label}"
        };

        if (dialog.ShowDialog(this) == true)
        {
            slot.Path = dialog.FileName;
            slot.Preview = LoadBitmap(dialog.FileName);
            ApplyAll();
        }
    }

    private void ApplyAll()
    {
        if (!_isReady)
        {
            return;
        }

        try
        {
            Diagnostics.Clear();
            var imported = ShaderSourceTools.Import(SourceBox.Text);
            _currentEntryPoint = imported.EntryPoint;
            if (imported.Parameters.Count > 0)
            {
                LoadParameterSlots(imported.Parameters);
                if (!string.Equals(SourceBox.Text, imported.Source, StringComparison.Ordinal))
                {
                    SourceBox.Text = imported.Source;
                }
            }

            if (imported.DeclaredShaderProfile is not null)
            {
                _requestedShaderModel = imported.ShaderModel;
                _declaredShaderProfile = imported.DeclaredShaderProfile;
            }

            if (!string.Equals(SourceBox.Text, imported.Source, StringComparison.Ordinal))
            {
                SourceBox.Text = imported.Source;
            }

            SyncScreenShaderDataFromSlots();
            RefreshBuiltInParameterValues();
            ApplyScreenShaderData();
            if (!_geometryUserSelected)
            {
                SelectAutomaticGeometry(imported.Source, imported.EntryPoint);
            }
            ApplyPreviewGeometry();
            RefreshTextureValidation(imported.Source);
            AddRuntimeSafetyDiagnostics(imported.Source);

            var compileDocument = ShaderSourceTools.BuildCompileDocument(imported.Source, GetEditableParameters());
            var bytecode = CompileWithFallback(
                compileDocument.Source,
                _currentEntryPoint,
                _requestedShaderModel,
                _declaredShaderProfile,
                DisplaySourceName,
                out _currentShaderModel,
                out var compileMessage);
            StatusText.Text = compileMessage;
            try
            {
                _effect.LoadShader(bytecode);
                StatusText.Text = $"{compileMessage} | loaded";
            }
            catch (Exception ex)
            {
                LogFailure("load", ex);
                throw;
            }

            try
            {
                ApplyTextures();
                StatusText.Text = "Textures applied";
            }
            catch (Exception ex)
            {
                LogFailure("apply", ex);
                throw;
            }

            var success = string.IsNullOrWhiteSpace(_currentShaderPath)
                ? $"Compiled default shader ({_currentShaderModel})"
                : $"Compiled {_currentShaderPath} ({_currentShaderModel})";
            success += $" | SpriteBatch {DrawSettings.SortMode}/{DrawSettings.BlendState}/{DrawSettings.SamplerState}/{DrawSettings.DepthStencilState}";
            var warningCount = Diagnostics.Count(item => string.Equals(item.Severity, "warning", StringComparison.OrdinalIgnoreCase));
            StatusText.Text = warningCount == 0
                ? success
                : string.Concat(success, " - ", warningCount.ToString(CultureInfo.InvariantCulture), " warning(s)");
            LogSuccess(StatusText.Text);
        }
        catch (Exception ex)
        {
            LogFailure("compile", ex);
            // The compiler only knows about the generated document. Map its
            // locations back to the displayed/original FX source whenever the
            // error came from the user source body.
            var source = ShaderSourceTools.Import(SourceBox.Text).Source;
            var compileDocument = ShaderSourceTools.BuildCompileDocument(source, GetEditableParameters());
            AddCompilerDiagnostics(ex, compileDocument, DisplaySourceName);
            EditorTabs.SelectedItem = DiagnosticsTab;
            StatusText.Text = ex.Message;
        }
    }

    private static byte[] CompileWithFallback(
        string source,
        string entryPoint,
        ShaderModel requestedShaderModel,
        string? declaredShaderProfile,
        string sourceName,
        out string shaderModel,
        out string status)
    {
        if (requestedShaderModel is ShaderModel.Ps30 or ShaderModel.HigherThanPs30)
        {
            if (IsPs30Supported())
            {
                try
                {
                    var bytecode = ShaderCompiler.CompilePixelShader(source, entryPoint, "ps_3_0", sourceName);
                    shaderModel = "ps_3_0";
                    status = requestedShaderModel == ShaderModel.HigherThanPs30
                        ? $"Compiled bytecode: {bytecode.Length} bytes ({declaredShaderProfile} -> ps_3_0 compatibility)"
                        : $"Compiled bytecode: {bytecode.Length} bytes (ps_3_0)";
                    return bytecode;
                }
                catch (InvalidOperationException ex) when (requestedShaderModel == ShaderModel.HigherThanPs30)
                {
                    throw CreateHigherProfileCompatibilityError(declaredShaderProfile, ex);
                }
            }

            try
            {
                var bytecode = ShaderCompiler.CompilePixelShader(source, entryPoint, "ps_2_0", sourceName);
                shaderModel = "ps_2_0";
                status = requestedShaderModel == ShaderModel.HigherThanPs30
                    ? $"Compiled bytecode: {bytecode.Length} bytes ({declaredShaderProfile} -> ps_2_0 hardware downgrade)"
                    : $"Compiled bytecode: {bytecode.Length} bytes (ps_2_0 hardware fallback)";
                return bytecode;
            }
            catch (InvalidOperationException ex)
            {
                var reason = requestedShaderModel == ShaderModel.HigherThanPs30
                    ? $"{declaredShaderProfile} cannot be previewed by the current WPF renderer. WPF supports at most ps_3_0, and this renderer does not support ps_3_0."
                    : "The current WPF renderer does not support ps_3_0, and the shader could not be downgraded to ps_2_0.";
                throw new InvalidOperationException($"{reason}{Environment.NewLine}{ex.Message}", ex);
            }
        }

        try
        {
            var bytecode = ShaderCompiler.CompilePixelShader(source, entryPoint, "ps_2_0", sourceName);
            shaderModel = "ps_2_0";
            status = $"Compiled bytecode: {bytecode.Length} bytes (ps_2_0)";
            return bytecode;
        }
        catch (InvalidOperationException ex) when (LooksLikeInstructionLimitError(ex.Message) && IsPs30Supported())
        {
            var bytecode = ShaderCompiler.CompilePixelShader(source, entryPoint, "ps_3_0", sourceName);
            shaderModel = "ps_3_0";
            status = $"Compiled bytecode: {bytecode.Length} bytes (ps_3_0 fallback)";
            return bytecode;
        }
    }

    private static bool IsPs30Supported()
        => RenderCapability.IsPixelShaderVersionSupported(3, 0);

    private static string GetPreviewShaderModelLabel(ShaderModel shaderModel)
        => shaderModel switch
        {
            ShaderModel.Ps30 => "ps_3_0",
            ShaderModel.HigherThanPs30 => "ps_3_0 compatibility",
            _ => "ps_2_0"
        };

    private static InvalidOperationException CreateHigherProfileCompatibilityError(
        string? declaredShaderProfile,
        InvalidOperationException exception)
    {
        var profile = string.IsNullOrWhiteSpace(declaredShaderProfile) ? "a shader profile above ps_3_0" : declaredShaderProfile;
        return new InvalidOperationException(
            $"{profile} cannot be previewed directly by WPF ShaderEffect. The previewer tried to compile it as ps_3_0, but this source uses syntax or features that ps_3_0 does not support. Replace modern Texture2D/SamplerState/SV_* usage with ps_3_0-compatible HLSL, or use a Direct3D 11/12 preview backend.{Environment.NewLine}{exception.Message}",
            exception);
    }

    private static bool LooksLikeInstructionLimitError(string message)
        => message.Contains("too many arithmetic instruction slots", StringComparison.OrdinalIgnoreCase)
           || message.Contains("instruction slots", StringComparison.OrdinalIgnoreCase);

    private void ApplyTextures()
    {
        var useForceFieldFallback = IsForceFieldGeometry()
            && TextureSlots.Count > 0
            && string.IsNullOrWhiteSpace(TextureSlots[0].Path);
        _effect.Input = useForceFieldFallback
            ? CreateBrush(CreateForceFieldPreview())
            : TextureSlots.Count > 0 ? CreateBrush(TextureSlots[0].Preview) : CreateWhiteBrush();
        _effect.Channel0 = TextureSlots.Count > 1 ? CreateBrush(TextureSlots[1].Preview) : CreateWhiteBrush();
        _effect.Channel1 = TextureSlots.Count > 2 ? CreateBrush(TextureSlots[2].Preview) : CreateWhiteBrush();
        _effect.Channel2 = TextureSlots.Count > 3 ? CreateBrush(TextureSlots[3].Preview) : CreateWhiteBrush();
    }

    private void SelectAutomaticGeometry(string source, string entryPoint)
    {
        if (_geometryUserSelected || PreviewGeometryBox is null)
        {
            return;
        }

        var desiredIndex = IsForceFieldSource(source, entryPoint)
            ? 2
            : IsSceneFilterSource(entryPoint) ? 3 : 0;
        if (PreviewGeometryBox.SelectedIndex != desiredIndex)
        {
            // Changing SelectedIndex raises SelectionChanged synchronously;
            // keep that internal change from being mistaken for an explicit
            // user override.
            _geometryUserSelected = false;
            PreviewGeometryBox.SelectedIndex = desiredIndex;
            _geometryUserSelected = false;
        }
    }

    private bool IsForceFieldGeometry()
        => PreviewGeometryBox?.SelectedItem is System.Windows.Controls.ComboBoxItem item
            && string.Equals(item.Tag as string, "forcefield", StringComparison.OrdinalIgnoreCase);

    private static bool IsForceFieldSource(string source, string entryPoint)
        => string.Equals(entryPoint, "ForceField", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(source, @"\bForceField\s*\(", RegexOptions.IgnoreCase)
            || source.Contains("Original effect pass: Technique1.ForceField", StringComparison.OrdinalIgnoreCase);

    private static bool IsSceneFilterSource(string entryPoint)
        => entryPoint.StartsWith("Filter", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entryPoint, "MonoFix", StringComparison.OrdinalIgnoreCase);

    private void ApplyForceFieldPreset()
    {
        // Mirrors ForceField.PreDraw and Terraria's vanilla force-field draw
        // state.  A shield ratio of 1 produces UseColor(float3(1.5)); the
        // secondary color remains MiscShaderData's white default.
        DrawSettings.SortMode = SpriteSortMode.Immediate;
        DrawSettings.BlendState = SpriteBlendState.AlphaBlend;
        DrawSettings.SamplerState = SpriteSamplerState.PointWrap;
        DrawSettings.DepthStencilState = SpriteDepthStencilState.Default;
        ScreenShaderData.Color = "float3(1.5, 1.5, 1.5)";
        ScreenShaderData.SecondaryColor = "float3(1.0, 1.0, 1.0)";

        if (SpriteSortModeBox is not null) SpriteSortModeBox.SelectedItem = DrawSettings.SortMode;
        if (SpriteBlendStateBox is not null) SpriteBlendStateBox.SelectedItem = DrawSettings.BlendState;
        if (SpriteSamplerStateBox is not null) SpriteSamplerStateBox.SelectedItem = DrawSettings.SamplerState;
        if (SpriteDepthStencilStateBox is not null) SpriteDepthStencilStateBox.SelectedItem = DrawSettings.DepthStencilState;
    }

    private void ApplySceneFilterPreset(string entryPoint)
    {
        DrawSettings.SortMode = SpriteSortMode.Immediate;
        DrawSettings.BlendState = SpriteBlendState.AlphaBlend;
        DrawSettings.SamplerState = SpriteSamplerState.LinearClamp;
        DrawSettings.DepthStencilState = SpriteDepthStencilState.None;
        ScreenShaderData.ScreenPosition = "0.0, 0.0";
        ScreenShaderData.Zoom = "1.0, 1.0";
        ScreenShaderData.Direction = "0.0, 1.0";
        var targetX = Math.Max(1, PreviewCanvas?.ActualWidth ?? 800) * 0.5;
        var targetY = Math.Max(1, PreviewCanvas?.ActualHeight ?? 480) * 0.5;
        ScreenShaderData.TargetPosition = $"{targetX.ToString("0.###", CultureInfo.InvariantCulture)}, {targetY.ToString("0.###", CultureInfo.InvariantCulture)}";

        if (string.Equals(entryPoint, "FilterTower", StringComparison.OrdinalIgnoreCase))
        {
            // ScreenEffectInitializer: Filters.Scene["Vortex"]
            ScreenShaderData.Color = "float3(0.0, 0.7, 0.7)";
            ScreenShaderData.SecondaryColor = "float3(1.0, 1.0, 1.0)";
            ScreenShaderData.Opacity = "0.5";
            ScreenShaderData.Intensity = "1.0";
            ScreenShaderData.Progress = "0.0";
            ScreenShaderData.GlobalOpacity = "1.0";
        }

        if (SpriteSortModeBox is not null) SpriteSortModeBox.SelectedItem = DrawSettings.SortMode;
        if (SpriteBlendStateBox is not null) SpriteBlendStateBox.SelectedItem = DrawSettings.BlendState;
        if (SpriteSamplerStateBox is not null) SpriteSamplerStateBox.SelectedItem = DrawSettings.SamplerState;
        if (SpriteDepthStencilStateBox is not null) SpriteDepthStencilStateBox.SelectedItem = DrawSettings.DepthStencilState;
    }

    private void ApplyPreviewGeometry()
    {
        if (PreviewCanvas is null || PreviewHost is null || PreviewGeometryBox is null)
        {
            return;
        }

        var item = PreviewGeometryBox.SelectedItem as System.Windows.Controls.ComboBoxItem;
        var tag = item?.Tag as string ?? "surface";
        if (string.Equals(tag, "surface", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tag, "scene-filter", StringComparison.OrdinalIgnoreCase))
        {
            PreviewHost.HorizontalAlignment = HorizontalAlignment.Stretch;
            PreviewHost.VerticalAlignment = VerticalAlignment.Stretch;
            PreviewHost.Width = double.NaN;
            PreviewHost.Height = double.NaN;
            return;
        }

        var canvasWidth = Math.Max(1, PreviewCanvas.ActualWidth);
        var canvasHeight = Math.Max(1, PreviewCanvas.ActualHeight);
        var aspect = string.Equals(tag, "forcefield", StringComparison.OrdinalIgnoreCase) ? 2.0 : 1.0;
        var width = Math.Min(canvasWidth * 0.90, canvasHeight * 0.90 * aspect);
        var height = width / aspect;
        if (height > canvasHeight * 0.90)
        {
            height = canvasHeight * 0.90;
            width = height * aspect;
        }

        PreviewHost.HorizontalAlignment = HorizontalAlignment.Center;
        PreviewHost.VerticalAlignment = VerticalAlignment.Center;
        PreviewHost.Width = Math.Max(1, width);
        PreviewHost.Height = Math.Max(1, height);
    }

    private void UpdateTime()
    {
        if (!_isReady)
        {
            return;
        }

        _effect.Time = _clock.Elapsed.TotalSeconds;
        var resolution = GetEffectivePreviewResolution();
        _effect.ResolutionX = resolution.X;
        _effect.ResolutionY = resolution.Y;
        RefreshBuiltInParameterValues();
    }

    private Point GetEffectivePreviewResolution()
    {
        var width = Math.Max(1, PreviewHost.ActualWidth);
        var height = Math.Max(1, PreviewHost.ActualHeight);
        if (string.Equals(GetPreviewGeometryTag(), "scene-filter", StringComparison.OrdinalIgnoreCase)
            && TryParseVector(ScreenShaderData.Zoom, out var zoom))
        {
            width /= Math.Max(Math.Abs(zoom.X), 0.0001);
            height /= Math.Max(Math.Abs(zoom.Y), 0.0001);
        }
        return new Point(width, height);
    }

    private static ImageSource LoadBitmap(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static ImageSource CreateSolidPreview(Color color)
    {
        var pixels = new byte[] { color.B, color.G, color.R, color.A };
        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static ImageSource CreateForceFieldPreview()
    {
        // Terraria's ForceField projectile samples the 600x600 Perlin asset.
        // The asset is an internal XNB and is not normally available to this
        // standalone viewer, so provide a deterministic shield/noise fallback.
        // Users can still select an extracted Perlin texture in the Textures
        // tab to inspect the exact source art.
        const int size = 256;
        var pixels = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        {
            var v = y / (size - 1f) * 2f - 1f;
            for (var x = 0; x < size; x++)
            {
                var u = x / (size - 1f) * 2f - 1f;
                var radius = Math.Sqrt(u * u + v * v);
                var shell = Math.Clamp((1.0 - radius) / 0.24, 0.0, 1.0);
                var center = Math.Clamp((0.96 - radius) / 0.96, 0.0, 1.0);
                var noise = 0.5 + 0.5 * Math.Sin(u * 31.0 + Math.Sin(v * 7.0)) * Math.Sin(v * 27.0 + u * 3.0);
                var value = Math.Clamp(0.16 + noise * 0.28 + shell * 0.56, 0.0, 1.0);
                // The original Terraria Perlin.xnb is opaque.  Keep this
                // fallback opaque as well: ForceField's pixel shader derives
                // its output alpha from the sampled texture alpha, so a
                // transparent procedural edge would otherwise make the whole
                // preview appear empty on some WPF drivers.
                var alpha = 1.0;
                var offset = (y * size + x) * 4;
                var channel = (byte)Math.Round(value * 255.0);
                pixels[offset] = channel;
                pixels[offset + 1] = channel;
                pixels[offset + 2] = channel;
                pixels[offset + 3] = (byte)Math.Round(alpha * 255.0);
            }
        }

        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static Brush CreateWhiteBrush() => new ImageBrush(CreateSolidPreview(Colors.White));

    private static Brush CreateBrush(ImageSource? source)
    {
        if (source is null)
        {
            return CreateWhiteBrush();
        }

        var brush = new ImageBrush(source)
        {
            Stretch = Stretch.UniformToFill,
            TileMode = TileMode.None,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center
        };
        brush.Freeze();
        return brush;
    }

    private static void LogFailure(string stage, Exception ex)
    {
        var path = Path.Combine(Path.GetTempPath(), "ShaderViewer.log");
        File.AppendAllText(path, $"[{DateTime.Now:O}] stage={stage}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
    }

    private static void LogSuccess(string message)
    {
        var path = Path.Combine(Path.GetTempPath(), "ShaderViewer.log");
        File.AppendAllText(path, $"[{DateTime.Now:O}] stage=success {message}{Environment.NewLine}{Environment.NewLine}");
    }

    private void LoadParameterSlots(IReadOnlyList<ShaderParameterDefinition> parameters)
    {
        ParameterSlots.Clear();
        foreach (var parameter in parameters)
        {
            ParameterSlots.Add(new ShaderParameterSlot(parameter.Name, parameter.Type, parameter.Value));
        }

        AddBuiltInParameterSlots();
    }

    private void ResetParameterSlots()
    {
        ParameterSlots.Clear();
        ParameterSlots.Add(new ShaderParameterSlot("uScale", "float", "0.35"));
        ParameterSlots.Add(new ShaderParameterSlot("uSpeed", "float", "1.0"));
        ParameterSlots.Add(new ShaderParameterSlot("uPattern", "float", "8.0"));
        ParameterSlots.Add(new ShaderParameterSlot("uTint", "float3", "float3(1.0, 0.55, 0.15)"));
        AddBuiltInParameterSlots();
    }

    private IReadOnlyList<ShaderParameterDefinition> GetEditableParameters()
    {
        var list = new List<ShaderParameterDefinition>(ParameterSlots.Count);
        foreach (var slot in ParameterSlots)
        {
            if (!slot.IsReadOnly && !ScreenShaderParameterNames.Contains(slot.Name))
            {
                list.Add(new ShaderParameterDefinition(slot.Name, slot.Type, slot.Value));
            }
        }

        return list;
    }

    private void AddBuiltInParameterSlots()
    {
        ParameterSlots.Add(new ShaderParameterSlot(
            "iInput", "sampler2D", "Texture slot s0", isReadOnly: true,
            description: "Primary preview texture. Choose an image in the Textures tab."));
        ParameterSlots.Add(new ShaderParameterSlot(
            "iChannel0", "sampler2D", "Texture slot s1", isReadOnly: true,
            description: "Additional preview texture. Choose an image in the Textures tab."));
        ParameterSlots.Add(new ShaderParameterSlot(
            "iChannel1", "sampler2D", "Texture slot s2", isReadOnly: true,
            description: "Additional preview texture. Choose an image in the Textures tab."));
        ParameterSlots.Add(new ShaderParameterSlot(
            "iChannel2", "sampler2D", "Texture slot s3", isReadOnly: true,
            description: "Additional preview texture. Choose an image in the Textures tab."));
        ParameterSlots.Add(new ShaderParameterSlot(
            "iTime", "float", "0.000", isReadOnly: true,
            description: "Preview clock in seconds; updates while the preview is running."));
        ParameterSlots.Add(new ShaderParameterSlot(
            "iResolutionX", "float", "1", isReadOnly: true,
            description: "Preview width in pixels; controlled by the preview surface."));
        ParameterSlots.Add(new ShaderParameterSlot(
            "iResolutionY", "float", "1", isReadOnly: true,
            description: "Preview height in pixels; controlled by the preview surface."));
        ParameterSlots.Add(new ShaderParameterSlot(
            "iResolution", "float2", "float2(1, 1)", isReadOnly: true,
            description: "Read-only convenience value: float2(iResolutionX, iResolutionY)."));
        ParameterSlots.Add(new ShaderParameterSlot(
            "uTime", "float", "0.000", isReadOnly: true,
            description: "ScreenShaderData-compatible alias for the preview clock."));
        ParameterSlots.Add(new ShaderParameterSlot(
            "uScreenResolution", "float2", "float2(1, 1)", isReadOnly: true,
            description: "ScreenShaderData-compatible screen resolution alias."));
        ParameterSlots.Add(new ShaderParameterSlot(
            "uColor", "float3", ScreenShaderData.Color,
            description: "ScreenShaderData UseColor value.", canRemove: false));
        ParameterSlots.Add(new ShaderParameterSlot(
            "uSecondColor", "float3", ScreenShaderData.SecondaryColor,
            description: "ScreenShaderData UseSecondaryColor value (uSecondaryColor alias).", canRemove: false));
        ParameterSlots.Add(new ShaderParameterSlot(
            "uOpacity", "float", ScreenShaderData.Opacity,
            description: "ScreenShaderData UseOpacity value.", canRemove: false));
        ParameterSlots.Add(new ShaderParameterSlot(
            "uIntensity", "float", ScreenShaderData.Intensity,
            description: "ScreenShaderData UseIntensity value.", canRemove: false));
        ParameterSlots.Add(new ShaderParameterSlot(
            "uProgress", "float", ScreenShaderData.Progress,
            description: "ScreenShaderData UseProgress value.", canRemove: false));
        ParameterSlots.Add(new ShaderParameterSlot(
            "uDirection", "float2", ScreenShaderData.Direction,
            description: "ScreenShaderData UseDirection value.", canRemove: false));
        ParameterSlots.Add(new ShaderParameterSlot(
            "uTargetPosition", "float2", ScreenShaderData.TargetPosition,
            description: "ScreenShaderData UseTargetPosition value in world/screen pixels.", canRemove: false));
        ParameterSlots.Add(new ShaderParameterSlot(
            "uImageOffset", "float2", ScreenShaderData.ImageOffset,
            description: "ScreenShaderData UseImageOffset value.", canRemove: false));
        ParameterSlots.Add(new ShaderParameterSlot(
            "uImageScale", "float2", ScreenShaderData.ImageScale,
            description: "ScreenShaderData UseImageScale value.", canRemove: false));
        ParameterSlots.Add(new ShaderParameterSlot(
            "uGlobalOpacity", "float", ScreenShaderData.GlobalOpacity,
            description: "Global filter opacity multiplier.", canRemove: false));
        ParameterSlots.Add(new ShaderParameterSlot(
            "uScreenPosition", "float2", ScreenShaderData.ScreenPosition,
            description: "World-space top-left of the captured screen, matching ScreenShaderData.Apply.", canRemove: false));
        ParameterSlots.Add(new ShaderParameterSlot(
            "uZoom", "float2", ScreenShaderData.Zoom,
            description: "GameViewMatrix zoom supplied to Filters.Scene shaders.", canRemove: false));
        ParameterSlots.Add(new ShaderParameterSlot(
            "uImageSize0", "float2", "float2(1, 1)", isReadOnly: true,
            description: "Captured scene or primary texture size in pixels."));
        ParameterSlots.Add(new ShaderParameterSlot(
            "uImageSize1", "float2", "float2(1, 1)", isReadOnly: true,
            description: "iChannel0 size after ScreenShaderData UseImageScale."));
        ParameterSlots.Add(new ShaderParameterSlot(
            "uImageSize2", "float2", "float2(1, 1)", isReadOnly: true,
            description: "iChannel1 size after ScreenShaderData UseImageScale."));
        ParameterSlots.Add(new ShaderParameterSlot(
            "uImageSize3", "float2", "float2(1, 1)", isReadOnly: true,
            description: "iChannel2 size after ScreenShaderData UseImageScale."));
    }

    private void RefreshBuiltInParameterValues()
    {
        var resolution = GetEffectivePreviewResolution();
        var width = resolution.X;
        var height = resolution.Y;
        SetBuiltInValue("iTime", _clock.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture));
        SetBuiltInValue("iResolutionX", width.ToString("0.###", CultureInfo.InvariantCulture));
        SetBuiltInValue("iResolutionY", height.ToString("0.###", CultureInfo.InvariantCulture));
        SetBuiltInValue(
            "iResolution",
            $"float2({width.ToString("0.###", CultureInfo.InvariantCulture)}, {height.ToString("0.###", CultureInfo.InvariantCulture)})");
        SetBuiltInValue("iInput", GetTexturePreviewValue(0, "s0"));
        SetBuiltInValue("iChannel0", GetTexturePreviewValue(1, "s1"));
        SetBuiltInValue("iChannel1", GetTexturePreviewValue(2, "s2"));
        SetBuiltInValue("iChannel2", GetTexturePreviewValue(3, "s3"));
        SetBuiltInValue("uTime", _clock.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture));
        SetBuiltInValue("uScreenResolution", $"float2({width.ToString("0.###", CultureInfo.InvariantCulture)}, {height.ToString("0.###", CultureInfo.InvariantCulture)})");
        var imageSizes = new Point[4];
        var imageScale = TryParseVector(ScreenShaderData.ImageScale, out var parsedImageScale)
            ? parsedImageScale
            : new Point(1, 1);
        for (var index = 0; index < imageSizes.Length; index++)
        {
            imageSizes[index] = index < TextureSlots.Count && TextureSlots[index].Preview is BitmapSource bitmap
                ? new Point(bitmap.PixelWidth, bitmap.PixelHeight)
                : index == 0 && IsForceFieldGeometry() ? new Point(256, 256) : new Point(1, 1);
            if (index > 0)
                imageSizes[index] = new Point(imageSizes[index].X * imageScale.X, imageSizes[index].Y * imageScale.Y);
            SetBuiltInValue(
                $"uImageSize{index}",
                $"float2({imageSizes[index].X.ToString(CultureInfo.InvariantCulture)}, {imageSizes[index].Y.ToString(CultureInfo.InvariantCulture)})");
        }
        _effect.ImageSize0 = imageSizes[0];
        _effect.ImageSize1 = imageSizes[1];
        _effect.ImageSize2 = imageSizes[2];
        _effect.ImageSize3 = imageSizes[3];
        _effect.SourceRect = Colors.Transparent;
    }

    private void SetBuiltInValue(string name, string value)
    {
        var slot = ParameterSlots.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (slot is not null)
        {
            slot.Value = value;
        }
    }

    private void SyncScreenShaderDataFromSlots()
    {
        foreach (var slot in ParameterSlots)
        {
            switch (slot.Name.ToLowerInvariant())
            {
                case "ucolor": ScreenShaderData.Color = slot.Value; break;
                case "usecondarycolor":
                case "usecondcolor": ScreenShaderData.SecondaryColor = slot.Value; break;
                case "uopacity": ScreenShaderData.Opacity = slot.Value; break;
                case "uintensity": ScreenShaderData.Intensity = slot.Value; break;
                case "uprogress": ScreenShaderData.Progress = slot.Value; break;
                case "udirection": ScreenShaderData.Direction = slot.Value; break;
                case "utargetposition": ScreenShaderData.TargetPosition = slot.Value; break;
                case "uimageoffset": ScreenShaderData.ImageOffset = slot.Value; break;
                case "uimagescale": ScreenShaderData.ImageScale = slot.Value; break;
                case "uglobalopacity": ScreenShaderData.GlobalOpacity = slot.Value; break;
                case "uscreenposition": ScreenShaderData.ScreenPosition = slot.Value; break;
                case "uzoom": ScreenShaderData.Zoom = slot.Value; break;
            }
        }
    }

    private string GetTexturePreviewValue(int index, string register)
    {
        if (index >= TextureSlots.Count || string.IsNullOrWhiteSpace(TextureSlots[index].Path))
        {
            return $"{register}: white fallback";
        }

        return $"{register}: {Path.GetFileName(TextureSlots[index].Path)}";
    }

    private void RefreshTextureValidation(string normalizedSource)
    {
        var requiredSlots = ShaderSourceTools.FindRequiredTextureSlots(normalizedSource);
        for (var index = 0; index < TextureSlots.Count; index++)
        {
            var slot = TextureSlots[index];
            var isRequired = requiredSlots.Contains(index);
            string? warning = null;
            if (!string.IsNullOrWhiteSpace(slot.Path) && slot.Preview is null && File.Exists(slot.Path))
            {
                try
                {
                    slot.Preview = LoadBitmap(slot.Path);
                }
                catch (Exception ex)
                {
                    warning = $"Warning: required texture could not be decoded ({ex.Message}). White fallback is being previewed.";
                }
            }

            if (isRequired)
            {
                warning ??= string.IsNullOrWhiteSpace(slot.Path)
                    ? IsForceFieldGeometry()
                        ? "Info: this texture is sampled by ForceField, but no image has been selected. A procedural Perlin-like shield texture is being previewed; select an extracted Terraria Perlin image for exact sampling."
                        : "Warning: this texture is sampled by the shader, but no image has been selected. White fallback is being previewed."
                    : !File.Exists(slot.Path)
                        ? $"Warning: required texture file was not found: {slot.Path}. White fallback is being previewed."
                        : slot.Preview is null
                            ? "Warning: the selected required texture could not be loaded. White fallback is being previewed."
                            : null;
            }

            if (IsForceFieldGeometry() && isRequired && string.IsNullOrWhiteSpace(slot.Path))
            {
                // The standalone viewer has a deterministic Perlin-like
                // fallback for Terraria's internal Perlin.xnb asset, so this
                // is informational rather than a missing-required-input
                // warning.
                warning = warning?.Replace("Warning:", "Info:", StringComparison.OrdinalIgnoreCase);
            }

            slot.SetValidation(isRequired, warning);
            if (warning is not null)
            {
                var severity = IsForceFieldGeometry()
                    && string.IsNullOrWhiteSpace(slot.Path)
                    && isRequired
                    ? "info"
                    : "warning";
                Diagnostics.Add(new ShaderDiagnosticItem(severity, warning, DisplaySourceName, null, null));
            }
        }
    }

    private void AddRuntimeSafetyDiagnostics(string source)
    {
        foreach (var diagnostic in ShaderSourceTools.AnalyzeRuntimeRisks(source))
        {
            Diagnostics.Add(new ShaderDiagnosticItem(
                diagnostic.Severity,
                diagnostic.Message,
                DisplaySourceName,
                diagnostic.Line,
                diagnostic.Column));
        }
    }

    private void AddCompilerDiagnostics(Exception exception, ShaderCompileDocument compileDocument, string sourceFile)
    {
        var addedDiagnostics = 0;
        foreach (var rawLine in exception.Message.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (!TryParseCompilerDiagnostic(rawLine, out var compilerDiagnostic))
            {
                continue;
            }

            var sourceLocation = compileDocument.MapCompilerLocation(compilerDiagnostic.GeneratedLine, compilerDiagnostic.Column);
            var message = sourceLocation.IsGeneratedCode
                ? $"{compilerDiagnostic.Message} (reported in previewer-generated support code; no original FX location exists)"
                : compilerDiagnostic.Message;
            Diagnostics.Add(new ShaderDiagnosticItem(
                compilerDiagnostic.Severity,
                message,
                sourceFile,
                sourceLocation.Line,
                sourceLocation.Column));
            addedDiagnostics++;
        }

        if (addedDiagnostics == 0)
        {
            Diagnostics.Add(new ShaderDiagnosticItem("error", exception.Message.Trim(), sourceFile, null, null));
        }
    }

    private static bool TryParseCompilerDiagnostic(string rawLine, out CompilerDiagnostic diagnostic)
    {
        // d3dcompiler normally emits one of the following forms:
        //   File.fx(12,8): error X3000: message
        //   File.fx(12): warning X3206: message
        // The expression intentionally also handles an absolute file path and
        // localized severity labels by treating the portion after ')' as the
        // message when the compiler does not use the English words.
        diagnostic = default;
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return false;
        }

        var locationMatch = Regex.Match(rawLine, @"\((?<line>\d+)(?:\s*,\s*(?<column>\d+))?\)");
        if (!locationMatch.Success)
        {
            return false;
        }

        if (!int.TryParse(locationMatch.Groups["line"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var line)
            || line < 1)
        {
            return false;
        }

        int? column = null;
        if (locationMatch.Groups["column"].Success)
        {
            if (!int.TryParse(locationMatch.Groups["column"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedColumn)
                || parsedColumn < 1)
            {
                return false;
            }

            column = parsedColumn;
        }

        var remainder = rawLine[(locationMatch.Index + locationMatch.Length)..].Trim();
        if (remainder.StartsWith(':'))
        {
            remainder = remainder[1..].Trim();
        }

        if (remainder.Length == 0)
        {
            return false;
        }

        var severity = remainder.Contains("warning", StringComparison.OrdinalIgnoreCase)
            ? "warning"
            : remainder.Contains("error", StringComparison.OrdinalIgnoreCase)
                ? "error"
                : "error";
        var colon = remainder.IndexOf(':');
        var message = colon >= 0 && colon < remainder.Length - 1
            ? remainder[(colon + 1)..].Trim()
            : remainder;
        diagnostic = new CompilerDiagnostic(line, column, severity, message);
        return true;
    }

    private readonly record struct CompilerDiagnostic(int GeneratedLine, int? Column, string Severity, string Message);

    private const string DefaultShaderSource = """
float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float2 res = GetResolution();
    float2 frag = fragCoord(uv);
    float2 p = (frag - res * 0.5) / max(min(res.x, res.y), 1.0);
    float2 q = p / max(uScale, 0.001);

    float square = step(max(abs(q.x), abs(q.y)), 0.36);
    float border = step(max(abs(q.x), abs(q.y)), 0.36) - step(max(abs(q.x), abs(q.y)), 0.31);
    float stripes = 0.5 + 0.5 * sin((frag.x + frag.y) * uPattern * 0.05 + iTime * uSpeed);
    float4 tex = tex2D(iInput, uv);

    float3 bg = float3(0.05, 0.06, 0.08);
    float3 fill = lerp(uTint, tex.rgb, 0.45);
    float3 col = lerp(bg, fill, square);
    col = lerp(col, float3(1.0, 1.0, 1.0), border);
    col += stripes * square * 0.07;

    return float4(col, 1.0);
}
""";
}

internal sealed class ScreenShaderDataProfileDocument
{
    public string Name { get; set; } = "Unnamed scheme";
    public ScreenShaderDataSettings Values { get; set; } = new();
}

internal sealed class NativeSceneFilterStackDocument
{
    public int Version { get; set; } = 1;
    public int FilterLimit { get; set; } = 16;
    public int PriorityThreshold { get; set; }
    public List<NativeSceneFilterDocument> Filters { get; set; } = new();
}

internal sealed class NativeSceneFilterDocument
{
    public string Name { get; set; } = "Filter";
    public string EffectPath { get; set; } = string.Empty;
    public string EntryPoint { get; set; } = "main";
    public int Priority { get; set; }
    public bool Active { get; set; } = true;
    public bool IsHidden { get; set; }
    public ScreenShaderDataSettings Values { get; set; } = new();
    public List<string> TexturePaths { get; set; } = new();
}
