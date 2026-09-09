using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Input;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace SpriteBatchPreviewBackend;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    public SpriteSortMode SortMode { get; set; } = SpriteSortMode.Deferred;
    public BlendState BlendState { get; set; } = BlendState.AlphaBlend;
    public SamplerState SamplerState { get; set; } = SamplerState.LinearClamp;
    public DepthStencilState DepthStencilState { get; set; } = DepthStencilState.None;
    public bool UseCheckerboardBackground { get; set; } = true;
    public Color SolidBackground { get; set; } = Color.Transparent;
    public Effect? SpriteEffect { get; set; }
    public string? TexturePath { get; set; }
    public Vector3 FilterColor { get; set; } = Vector3.One;
    public Vector3 FilterSecondaryColor { get; set; } = Vector3.One;
    public float FilterOpacity { get; set; } = 1f;
    public float FilterIntensity { get; set; } = 1f;
    public float FilterProgress { get; set; }
    public Vector2 FilterDirection { get; set; } = new(0f, 1f);
    public Vector2 FilterTargetPosition { get; set; } = Vector2.One;
    public Vector2 FilterImageOffset { get; set; }
    public Vector2 FilterImageScale { get; set; } = Vector2.One;
    public float FilterGlobalOpacity { get; set; } = 1f;
    public float? FilterTimeOverride { get; set; }
    public Vector2 FilterScreenPosition { get; set; }
    public Vector2 FilterZoom { get; set; } = Vector2.One;
    private IntPtr _ownerHandle;
    private Texture2D? _spriteTexture;
    private readonly Texture2D?[] _channelTextures = new Texture2D?[3];
    private readonly string?[] _channelPaths = new string?[3];
    private bool _centeredSprite;
    private float _spriteScale = 1f;
    private float _spriteRotation;
    private string? _capturePath;
    private int _drawCount;
    private RenderTarget2D? _sceneTarget1;
    private RenderTarget2D? _sceneTarget2;
    private string? _filterStackPath;
    private readonly List<SceneFilterRuntime> _sceneFilters = new();
    private readonly List<ContentManager> _filterContentManagers = new();
    private int _filterLimit = 16;
    private int _filterPriorityThreshold;
    public bool ForceFieldGeometry { get; private set; }
    public bool SceneFilterGeometry { get; private set; }
    public string? EffectPath { get; set; }

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    public void ConfigureFromArgs(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i + 1 < args.Length; i += 2)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
                values[args[i][2..]] = args[i + 1];
        }

        if (values.TryGetValue("sort", out var sort) && Enum.TryParse<SpriteSortMode>(sort, true, out var parsedSort))
            SortMode = parsedSort;
        if (values.TryGetValue("blend", out var blend))
            BlendState = blend.ToLowerInvariant() switch
            {
                "additive" => Microsoft.Xna.Framework.Graphics.BlendState.Additive,
                "opaque" => Microsoft.Xna.Framework.Graphics.BlendState.Opaque,
                "nonpremultiplied" => Microsoft.Xna.Framework.Graphics.BlendState.NonPremultiplied,
                _ => Microsoft.Xna.Framework.Graphics.BlendState.AlphaBlend
            };
        var parsedSampler = SamplerState.LinearClamp;
        if (values.TryGetValue("sampler", out var sampler) && TryGetSampler(sampler, out parsedSampler))
            SamplerState = parsedSampler;
        if (values.TryGetValue("depth", out var depth))
            DepthStencilState = depth.ToLowerInvariant() switch
            {
                "default" => Microsoft.Xna.Framework.Graphics.DepthStencilState.Default,
                "depthread" => Microsoft.Xna.Framework.Graphics.DepthStencilState.DepthRead,
                _ => Microsoft.Xna.Framework.Graphics.DepthStencilState.None
            };
        if (values.TryGetValue("background", out var background))
            UseCheckerboardBackground = !string.Equals(background, "solid", StringComparison.OrdinalIgnoreCase);
        var parsedColor = Color.Transparent;
        if (values.TryGetValue("color", out var color) && TryParseColor(color, out parsedColor))
            SolidBackground = parsedColor;
        if (values.TryGetValue("texture", out var texture))
            TexturePath = texture;
        if (values.TryGetValue("effect", out var effect))
            EffectPath = effect;
        if (values.TryGetValue("filter-stack", out var filterStack))
        {
            _filterStackPath = filterStack;
            SceneFilterGeometry = true;
            _centeredSprite = false;
        }
        if (values.TryGetValue("geometry", out var geometry))
        {
            _centeredSprite = string.Equals(geometry, "sprite", StringComparison.OrdinalIgnoreCase)
                || string.Equals(geometry, "forcefield", StringComparison.OrdinalIgnoreCase);
            ForceFieldGeometry = string.Equals(geometry, "forcefield", StringComparison.OrdinalIgnoreCase);
            SceneFilterGeometry = string.Equals(geometry, "scene-filter", StringComparison.OrdinalIgnoreCase);
        }
        _spriteScale = ParseFloat(values, "sprite-scale", _spriteScale);
        _spriteRotation = ParseFloat(values, "sprite-rotation", _spriteRotation);
        if (values.TryGetValue("capture", out var capture))
            _capturePath = capture;
        if (values.TryGetValue("filter-color", out var filterColor) && TryParseVector3Color(filterColor, out var parsedFilterColor))
            FilterColor = parsedFilterColor;
        if (values.TryGetValue("secondary-color", out var secondaryColor) && TryParseVector3Color(secondaryColor, out var parsedSecondaryColor))
            FilterSecondaryColor = parsedSecondaryColor;
        FilterOpacity = ParseFloat(values, "opacity", FilterOpacity);
        FilterIntensity = ParseFloat(values, "intensity", FilterIntensity);
        FilterProgress = ParseFloat(values, "progress", FilterProgress);
        if (values.TryGetValue("direction", out var direction) && TryParseVector2(direction, out var parsedDirection))
            FilterDirection = parsedDirection;
        if (values.TryGetValue("target-position", out var target) && TryParseVector2(target, out var parsedTarget))
            FilterTargetPosition = parsedTarget;
        if (values.TryGetValue("image-offset", out var offset) && TryParseVector2(offset, out var parsedOffset))
            FilterImageOffset = parsedOffset;
        if (values.TryGetValue("image-scale", out var scale) && TryParseVector2(scale, out var parsedScale))
            FilterImageScale = parsedScale;
        FilterGlobalOpacity = ParseFloat(values, "global-opacity", FilterGlobalOpacity);
        if (values.TryGetValue("time", out var fixedTime)
            && float.TryParse(fixedTime, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsedTime))
            FilterTimeOverride = parsedTime;
        if (values.TryGetValue("screen-position", out var screenPosition) && TryParseVector2(screenPosition, out var parsedScreenPosition))
            FilterScreenPosition = parsedScreenPosition;
        if (values.TryGetValue("zoom", out var zoom) && TryParseVector2(zoom, out var parsedZoom))
            FilterZoom = parsedZoom;
        for (var index = 0; index < _channelPaths.Length; index++)
        {
            if (values.TryGetValue($"channel{index}", out var channelPath))
                _channelPaths[index] = channelPath;
        }
        if (values.TryGetValue("owner-hwnd", out var owner)
            && long.TryParse(owner, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var ownerValue))
            _ownerHandle = new IntPtr(ownerValue);
    }

    private static bool TryGetSampler(string value, out SamplerState state)
    {
        state = value.ToLowerInvariant() switch
        {
            "pointclamp" => Microsoft.Xna.Framework.Graphics.SamplerState.PointClamp,
            "pointwrap" => Microsoft.Xna.Framework.Graphics.SamplerState.PointWrap,
            "linearwrap" => Microsoft.Xna.Framework.Graphics.SamplerState.LinearWrap,
            "anisotropicclamp" => Microsoft.Xna.Framework.Graphics.SamplerState.AnisotropicClamp,
            "anisotropicwrap" => Microsoft.Xna.Framework.Graphics.SamplerState.AnisotropicWrap,
            _ => Microsoft.Xna.Framework.Graphics.SamplerState.LinearClamp
        };
        return true;
    }

    private static bool TryParseColor(string value, out Color color)
    {
        color = Color.Transparent;
        value = value.Trim().TrimStart('#');
        if (value.Length == 6) value = "FF" + value;
        if (value.Length != 8 || !uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var argb)) return false;
        color = new Color((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));
        return true;
    }

    private static float ParseFloat(Dictionary<string, string> values, string key, float fallback)
        => values.TryGetValue(key, out var value)
            && float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static bool TryParseVector2(string value, out Vector2 vector)
    {
        vector = Vector2.Zero;
        var normalized = value.Trim();
        var open = normalized.IndexOf('(');
        if (open >= 0 && normalized.EndsWith(')'))
            normalized = normalized[(open + 1)..^1];
        var parts = normalized.Trim('(', ')').Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2
            || !float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
            || !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y))
            return false;
        vector = new Vector2(x, y);
        return true;
    }

    private static bool TryParseVector3Color(string value, out Vector3 vector)
    {
        vector = Vector3.One;
        var normalized = value.Trim().TrimStart('#');
        if (normalized.Length == 6 || normalized.Length == 8)
        {
            if (normalized.Length == 8) normalized = normalized[2..];
            if (uint.TryParse(normalized, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            {
                vector = new Vector3((rgb >> 16 & 0xff) / 255f, (rgb >> 8 & 0xff) / 255f, (rgb & 0xff) / 255f);
                return true;
            }
        }

        var open = normalized.IndexOf('(');
        if (open >= 0 && normalized.EndsWith(')'))
            normalized = normalized[(open + 1)..^1];
        var parts = normalized.Trim('(', ')').Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3
            || !float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r)
            || !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var g)
            || !float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var b))
            return false;
        vector = new Vector3(r, g, b);
        return true;
    }

    protected override void Initialize()
    {
        SetFloatingOwner();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        if (!string.IsNullOrWhiteSpace(TexturePath) && File.Exists(TexturePath))
        {
            using var stream = File.OpenRead(TexturePath);
            _spriteTexture = Texture2D.FromStream(GraphicsDevice, stream);
        }
        if (_centeredSprite && _spriteTexture is null)
        {
            _spriteTexture = CreateForceFieldTexture();
        }
        for (var index = 0; index < _channelPaths.Length; index++)
        {
            var path = _channelPaths[index];
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;

            using var stream = File.OpenRead(path);
            _channelTextures[index] = Texture2D.FromStream(GraphicsDevice, stream);
        }
        if (!string.IsNullOrWhiteSpace(EffectPath) && File.Exists(EffectPath))
        {
            if (string.Equals(Path.GetExtension(EffectPath), ".xnb", StringComparison.OrdinalIgnoreCase))
            {
                // MGCB writes a complete XNB container.  ContentManager knows
                // how to read the platform-specific MGFX payload inside it;
                // the raw Effect(byte[]) constructor is for MGFX bytes only.
                var previousRoot = Content.RootDirectory;
                Content.RootDirectory = Path.GetDirectoryName(Path.GetFullPath(EffectPath))!;
                SpriteEffect = Content.Load<Effect>(Path.GetFileNameWithoutExtension(EffectPath));
                Content.RootDirectory = previousRoot;
            }
            else
            {
                SpriteEffect = new Effect(GraphicsDevice, File.ReadAllBytes(EffectPath));
            }
        }
        LoadSceneFilterStack();

        // TODO: use this.Content to load your game content here
    }

    private void LoadSceneFilterStack()
    {
        if (string.IsNullOrWhiteSpace(_filterStackPath) || !File.Exists(_filterStackPath))
            return;

        var document = JsonSerializer.Deserialize<SceneFilterStackRuntimeDocument>(
            File.ReadAllText(_filterStackPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("The runtime filter stack is empty.");

        var registrationIndex = 0;
        foreach (var filter in document.Filters)
        {
            if (string.IsNullOrWhiteSpace(filter.EffectPath) || !File.Exists(filter.EffectPath))
                throw new FileNotFoundException($"Compiled effect for filter '{filter.Name}' was not found.", filter.EffectPath);

            var manager = new ContentManager(Services, Path.GetDirectoryName(Path.GetFullPath(filter.EffectPath))!);
            _filterContentManagers.Add(manager);
            var effect = manager.Load<Effect>(Path.GetFileNameWithoutExtension(filter.EffectPath));
            var channels = new Texture2D?[3];
            for (var channel = 0; channel < channels.Length; channel++)
            {
                // TexturePaths[0] describes iInput, which FilterManager replaces
                // with the captured scene. Additional paths map to s1..s3.
                var pathIndex = channel + 1;
                if (pathIndex >= filter.TexturePaths.Count)
                    continue;
                var path = filter.TexturePaths[pathIndex];
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) ||
                    string.Equals(Path.GetExtension(path), ".xnb", StringComparison.OrdinalIgnoreCase))
                    continue;
                using var stream = File.OpenRead(path);
                channels[channel] = Texture2D.FromStream(GraphicsDevice, stream);
            }
            _sceneFilters.Add(new SceneFilterRuntime(filter, effect, channels, registrationIndex++));
        }

        _sceneFilters.Sort((left, right) =>
        {
            var priority = left.Definition.Priority.CompareTo(right.Definition.Priority);
            return priority != 0 ? priority : left.RegistrationIndex.CompareTo(right.RegistrationIndex);
        });
        _filterLimit = Math.Clamp(document.FilterLimit, 0, 16);
        _filterPriorityThreshold = Math.Clamp(document.PriorityThreshold, 0, 4);
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        // TODO: Add your update logic here

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        var viewport = GraphicsDevice.Viewport;
        if (SceneFilterGeometry && (SpriteEffect is not null || _sceneFilters.Count > 0))
        {
            DrawSceneFilter(gameTime, viewport);
            base.Draw(gameTime);
            CaptureIfRequested();
            return;
        }

        GraphicsDevice.Clear(UseCheckerboardBackground ? Color.Transparent : SolidBackground);
        if (UseCheckerboardBackground)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, Microsoft.Xna.Framework.Graphics.BlendState.Opaque, SamplerState, DepthStencilState);
            DrawCheckerboard();
            _spriteBatch.End();
        }

        ApplyEffectParameters(gameTime);
        if (ForceFieldGeometry && SpriteEffect is not null)
        {
            DrawForceFieldPrimitive(viewport);
            base.Draw(gameTime);
            CaptureIfRequested();
            return;
        }

        // Terraria's MiscShaderData path does not pass the shader to Begin.
        // It begins an Immediate batch with a null effect, applies the pass,
        // then lets DrawData bind texture slot s0 immediately before drawing.
        // Passing this reconstructed effect to Begin applies its sampler state
        // after SpriteBatch binds the texture, replacing s0 with an empty
        // effect sampler and producing a flat gray/transparent rectangle.
        _spriteBatch.Begin(SortMode, BlendState, SamplerState, DepthStencilState,
            RasterizerState.CullNone, SpriteEffect);
        if (_centeredSprite)
        {
            DrawCenteredSprite(viewport);
        }
        else
        {
            _spriteBatch.Draw(_spriteTexture ?? _pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.White);
        }
        _spriteBatch.End();

        // TODO: Add your drawing code here

        base.Draw(gameTime);
        CaptureIfRequested();
    }

    private void DrawSceneFilter(GameTime gameTime, Viewport viewport)
    {
        EnsureSceneTarget(viewport.Width, viewport.Height);
        GraphicsDevice.SetRenderTarget(_sceneTarget1);
        GraphicsDevice.Clear(UseCheckerboardBackground ? Color.Transparent : SolidBackground);
        if (UseCheckerboardBackground)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, Microsoft.Xna.Framework.Graphics.BlendState.Opaque,
                Microsoft.Xna.Framework.Graphics.SamplerState.PointClamp, DepthStencilState.None);
            DrawCheckerboard();
            _spriteBatch.End();
        }

        if (_spriteTexture is not null)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, Microsoft.Xna.Framework.Graphics.BlendState.AlphaBlend,
                Microsoft.Xna.Framework.Graphics.SamplerState.LinearClamp, DepthStencilState.None);
            _spriteBatch.Draw(_spriteTexture, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.White);
            _spriteBatch.End();
        }

        if (_sceneFilters.Count > 0)
        {
            DrawManagedSceneFilters(gameTime, viewport);
            return;
        }

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(UseCheckerboardBackground ? Color.Transparent : SolidBackground);
        ApplyEffectParameters(gameTime);
        DrawScreenFilterPrimitive(viewport, _sceneTarget1!);
    }

    private void DrawManagedSceneFilters(GameTime gameTime, Viewport viewport)
    {
        var elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var eligible = _sceneFilters
            .Where(filter => filter.Definition.Priority >= _filterPriorityThreshold &&
                (filter.Definition.Active || filter.Opacity > 0f))
            .ToList();
        var firstWithinLimit = Math.Max(0, eligible.Count - _filterLimit);
        for (var index = 0; index < eligible.Count; index++)
        {
            var filter = eligible[index];
            var isWithinLimit = index >= firstWithinLimit;
            filter.Opacity = filter.Definition.Active && isWithinLimit
                ? Math.Min(1f, filter.Opacity + elapsed)
                : Math.Max(0f, filter.Opacity - elapsed);
        }

        var visible = eligible
            .Skip(firstWithinLimit)
            .Where(filter => !filter.Definition.IsHidden && filter.Opacity > 0f)
            .ToList();
        Texture2D source = _sceneTarget1!;
        for (var index = 0; index < visible.Count; index++)
        {
            var filter = visible[index];
            var isLast = index == visible.Count - 1;
            RenderTarget2D? output = isLast
                ? null
                : ReferenceEquals(source, _sceneTarget1) ? _sceneTarget2 : _sceneTarget1;
            GraphicsDevice.SetRenderTarget(output);
            GraphicsDevice.Clear(UseCheckerboardBackground ? Color.Transparent : SolidBackground);
            ApplyManagedEffectParameters(filter, gameTime, viewport, source);
            DrawScreenFilterPrimitive(viewport, source, filter.Effect, filter.Channels);
            if (!isLast)
                source = output!;
        }

        if (visible.Count == 0)
        {
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(UseCheckerboardBackground ? Color.Transparent : SolidBackground);
            _spriteBatch.Begin(SpriteSortMode.Immediate, Microsoft.Xna.Framework.Graphics.BlendState.AlphaBlend,
                Microsoft.Xna.Framework.Graphics.SamplerState.LinearClamp, DepthStencilState.None,
                RasterizerState.CullNone);
            _spriteBatch.Draw(source, Vector2.Zero, Color.White);
            _spriteBatch.End();
        }
    }

    private void BindScreenShaderImages()
        => BindScreenShaderImages(_channelTextures);

    private void BindScreenShaderImages(IReadOnlyList<Texture2D?> channels)
    {
        for (var index = 0; index < 3; index++)
        {
            var texture = index < channels.Count ? channels[index] ?? _pixel : _pixel;
            GraphicsDevice.Textures[index + 1] = texture;
            GraphicsDevice.SamplerStates[index + 1] = IsPowerOfTwo(texture.Width) && IsPowerOfTwo(texture.Height)
                ? Microsoft.Xna.Framework.Graphics.SamplerState.LinearWrap
                : Microsoft.Xna.Framework.Graphics.SamplerState.AnisotropicClamp;
        }
    }

    private void EnsureSceneTarget(int width, int height)
    {
        if (_sceneTarget1 is not null && _sceneTarget2 is not null &&
            _sceneTarget1.Width == width && _sceneTarget1.Height == height)
            return;

        _sceneTarget1?.Dispose();
        _sceneTarget2?.Dispose();
        _sceneTarget1 = new RenderTarget2D(GraphicsDevice, width, height, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        _sceneTarget2 = new RenderTarget2D(GraphicsDevice, width, height, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
    }

    private void DrawScreenFilterPrimitive(Viewport viewport, Texture2D capturedScene)
        => DrawScreenFilterPrimitive(viewport, capturedScene, SpriteEffect!, _channelTextures);

    private void DrawScreenFilterPrimitive(
        Viewport viewport,
        Texture2D capturedScene,
        Effect effect,
        IReadOnlyList<Texture2D?> channels)
    {
        var vertices = new[]
        {
            new VertexPositionColorTexture(new Vector3(0f, 0f, 0f), Color.White, new Vector2(0f, 0f)),
            new VertexPositionColorTexture(new Vector3(viewport.Width, 0f, 0f), Color.White, new Vector2(1f, 0f)),
            new VertexPositionColorTexture(new Vector3(viewport.Width, viewport.Height, 0f), Color.White, new Vector2(1f, 1f)),
            new VertexPositionColorTexture(new Vector3(0f, viewport.Height, 0f), Color.White, new Vector2(0f, 1f))
        };
        var indices = new short[] { 0, 1, 2, 0, 2, 3 };

        GraphicsDevice.BlendState = Microsoft.Xna.Framework.Graphics.BlendState.AlphaBlend;
        GraphicsDevice.DepthStencilState = Microsoft.Xna.Framework.Graphics.DepthStencilState.None;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        SetParameter(effect, "MatrixTransform", Matrix.CreateOrthographicOffCenter(
            0f, viewport.Width, viewport.Height, 0f, 0f, 1f));
        effect.CurrentTechnique.Passes[0].Apply();
        GraphicsDevice.Textures[0] = capturedScene;
        GraphicsDevice.SamplerStates[0] = Microsoft.Xna.Framework.Graphics.SamplerState.LinearClamp;
        BindScreenShaderImages(channels);
        GraphicsDevice.DrawUserIndexedPrimitives(
            PrimitiveType.TriangleList, vertices, 0, vertices.Length, indices, 0, 2);
    }

    private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;

    private void CaptureIfRequested()
    {
        if (string.IsNullOrWhiteSpace(_capturePath) || ++_drawCount < 3)
            return;

        CaptureBackBuffer(_capturePath);
        Exit();
    }

    private void DrawForceFieldPrimitive(Viewport viewport)
    {
        var texture = _spriteTexture ?? _pixel;
        const float sourceWidth = 600f;
        const float sourceHeight = 600f;
        var fitScale = Math.Min(
            viewport.Width / (sourceWidth * 2f),
            viewport.Height / sourceHeight) * 0.9f;
        fitScale = Math.Max(0.001f, fitScale * Math.Max(0.001f, _spriteScale));
        var halfWidth = sourceWidth * fitScale;
        var halfHeight = sourceHeight * fitScale * 0.5f;
        var center = new Vector2(viewport.Width * 0.5f, viewport.Height * 0.5f);
        var left = center.X - halfWidth;
        var right = center.X + halfWidth;
        var top = center.Y - halfHeight;
        var bottom = center.Y + halfHeight;
        var maxU = sourceWidth / texture.Width;
        var maxV = sourceHeight / texture.Height;
        var vertices = new[]
        {
            new VertexPositionColorTexture(new Vector3(left, top, 0f), Color.White, new Vector2(0f, 0f)),
            new VertexPositionColorTexture(new Vector3(right, top, 0f), Color.White, new Vector2(maxU, 0f)),
            new VertexPositionColorTexture(new Vector3(right, bottom, 0f), Color.White, new Vector2(maxU, maxV)),
            new VertexPositionColorTexture(new Vector3(left, bottom, 0f), Color.White, new Vector2(0f, maxV))
        };
        var indices = new short[] { 0, 1, 2, 0, 2, 3 };

        GraphicsDevice.BlendState = BlendState;
        GraphicsDevice.DepthStencilState = DepthStencilState;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        SetParameter("MatrixTransform", Matrix.CreateOrthographicOffCenter(
            0f, viewport.Width, viewport.Height, 0f, 0f, 1f));
        SpriteEffect!.CurrentTechnique.Passes[0].Apply();
        // Apply() owns shaders and constants; the DrawData step then owns s0.
        GraphicsDevice.Textures[0] = texture;
        GraphicsDevice.SamplerStates[0] = SamplerState;
        GraphicsDevice.DrawUserIndexedPrimitives(
            PrimitiveType.TriangleList,
            vertices,
            0,
            vertices.Length,
            indices,
            0,
            2);
    }

    private void CaptureBackBuffer(string path)
    {
        var viewport = GraphicsDevice.Viewport;
        var pixels = new Color[viewport.Width * viewport.Height];
        GraphicsDevice.GetBackBufferData(pixels);
        using var copy = new Texture2D(GraphicsDevice, viewport.Width, viewport.Height, false, SurfaceFormat.Color);
        copy.SetData(pixels);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path);
        copy.SaveAsPng(stream, viewport.Width, viewport.Height);
    }

    private void DrawCenteredSprite(Viewport viewport)
    {
        var texture = _spriteTexture ?? _pixel;
        // ForceField deliberately draws a 600x600 source rectangle from the
        // 512x512 Perlin texture while PointWrap is active. UVs above 1.0
        // wrap into the noise image; clamping changes the DrawData behavior.
        var sourceWidth = ForceFieldGeometry ? 600 : texture.Width;
        var sourceHeight = ForceFieldGeometry ? 600 : texture.Height;
        var source = new Rectangle(0, 0, Math.Max(1, sourceWidth), Math.Max(1, sourceHeight));
        var aspect = ForceFieldGeometry ? 2f : 1f;
        var fitScale = Math.Min(
            viewport.Width / (source.Width * aspect),
            viewport.Height / (float)source.Height) * 0.9f;
        fitScale = Math.Max(0.001f, fitScale * Math.Max(0.001f, _spriteScale));
        var destinationScale = ForceFieldGeometry
            ? new Vector2(fitScale * 2f, fitScale)
            : new Vector2(fitScale, fitScale);
        var center = new Vector2(viewport.Width * 0.5f, viewport.Height * 0.5f);
        var tint = Color.White;
        _spriteBatch.Draw(texture, center, source, tint, _spriteRotation,
            new Vector2(source.Width * 0.5f, source.Height * 0.5f), destinationScale,
            SpriteEffects.None, 0f);
    }
    private void DrawCheckerboard()
    {
        var viewport = GraphicsDevice.Viewport;
        const int tile = 24;
        var light = new Color(232, 232, 232, 255);
        var dark = new Color(208, 208, 208, 255);
        for (var y = 0; y < viewport.Height; y += tile)
            for (var x = 0; x < viewport.Width; x += tile)
                _spriteBatch.Draw(_pixel, new Rectangle(x, y, tile, tile), ((x / tile + y / tile) & 1) == 0 ? light : dark);
    }

    private Texture2D CreateForceFieldTexture()
    {
        const int size = 256;
        var texture = new Texture2D(GraphicsDevice, size, size, false, SurfaceFormat.Color);
        var pixels = new Color[size * size];
        for (var y = 0; y < size; y++)
        {
            var v = y / (size - 1f) * 2f - 1f;
            for (var x = 0; x < size; x++)
            {
                var u = x / (size - 1f) * 2f - 1f;
                var radius = MathF.Sqrt(u * u + v * v);
                var shell = MathHelper.Clamp((1f - radius) / 0.24f, 0f, 1f);
                var center = MathHelper.Clamp((0.96f - radius) / 0.96f, 0f, 1f);
                var noise = 0.5f + 0.5f * MathF.Sin(u * 31f + MathF.Sin(v * 7f)) * MathF.Sin(v * 27f + u * 3f);
                var value = MathHelper.Clamp(0.16f + noise * 0.28f + shell * 0.56f, 0f, 1f);
                // Terraria's Perlin texture is opaque.  The ForceField pixel
                // shader uses its sampled alpha as the final alpha, so an
                // opaque fallback is required for visibility.
                pixels[y * size + x] = new Color(value, value, value, 1f);
            }
        }

        texture.SetData(pixels);
        return texture;
    }

    private void ApplyManagedEffectParameters(
        SceneFilterRuntime filter,
        GameTime gameTime,
        Viewport viewport,
        Texture2D input)
    {
        var effect = filter.Effect;
        var values = filter.Definition.Values;
        var color = TryParseVector3Color(values.Color, out var parsedColor) ? parsedColor : Vector3.One;
        var secondColor = TryParseVector3Color(values.SecondaryColor, out var parsedSecondColor) ? parsedSecondColor : Vector3.One;
        var direction = TryParseVector2(values.Direction, out var parsedDirection) ? parsedDirection : new Vector2(0f, 1f);
        var target = TryParseVector2(values.TargetPosition, out var parsedTarget) ? parsedTarget : Vector2.One;
        var imageOffset = TryParseVector2(values.ImageOffset, out var parsedOffset) ? parsedOffset : Vector2.Zero;
        var imageScale = TryParseVector2(values.ImageScale, out var parsedScale) ? parsedScale : Vector2.One;
        var screenPosition = TryParseVector2(values.ScreenPosition, out var parsedScreenPosition) ? parsedScreenPosition : Vector2.Zero;
        var zoom = TryParseVector2(values.Zoom, out var parsedZoom) ? parsedZoom : Vector2.One;
        var opacity = ParseInvariantFloat(values.Opacity, 1f) * filter.Opacity;
        var time = FilterTimeOverride ?? (float)gameTime.TotalGameTime.TotalSeconds;

        SetParameter(effect, "uColor", color);
        SetParameter(effect, "_uColor", color);
        SetParameter(effect, "uSecondaryColor", secondColor);
        SetParameter(effect, "uSecondColor", secondColor);
        SetParameter(effect, "_uSecondaryColor", secondColor);
        SetParameter(effect, "uOpacity", opacity);
        SetParameter(effect, "_uOpacity", opacity);
        SetParameter(effect, "uIntensity", ParseInvariantFloat(values.Intensity, 1f));
        SetParameter(effect, "uProgress", ParseInvariantFloat(values.Progress, 0f));
        SetParameter(effect, "uDirection", direction);
        SetParameter(effect, "uTargetPosition", target);
        SetParameter(effect, "uImageOffset", imageOffset);
        SetParameter(effect, "uImageScale", imageScale);
        SetParameter(effect, "uGlobalOpacity", filter.Opacity);
        SetParameter(effect, "uTime", time);
        SetParameter(effect, "iTime", time);
        var safeZoom = new Vector2(Math.Max(Math.Abs(zoom.X), 0.0001f), Math.Max(Math.Abs(zoom.Y), 0.0001f));
        SetParameter(effect, "uScreenResolution", new Vector2(viewport.Width, viewport.Height) / safeZoom);
        SetParameter(effect, "uScreenPosition", screenPosition);
        SetParameter(effect, "uZoom", zoom);
        SetParameter(effect, "uImageSize0", new Vector2(input.Width, input.Height));
        for (var index = 0; index < filter.Channels.Length; index++)
        {
            var texture = filter.Channels[index];
            var size = texture is null ? Vector2.One : new Vector2(texture.Width, texture.Height);
            SetParameter(effect, $"uImageSize{index + 1}", size * imageScale);
        }
        SetParameter(effect, "uSourceRect", Vector4.Zero);
    }

    private static float ParseInvariantFloat(string? value, float fallback)
        => float.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private void ApplyEffectParameters(GameTime gameTime)
    {
        if (SpriteEffect is null)
            return;

        var viewport = GraphicsDevice.Viewport;

        SetParameter("uColor", FilterColor);
        SetParameter("_uColor", FilterColor);
        SetParameter("uSecondaryColor", FilterSecondaryColor);
        SetParameter("_uSecondaryColor", FilterSecondaryColor);
        SetParameter("uSecondColor", FilterSecondaryColor);
        SetParameter("_uSecondColor", FilterSecondaryColor);
        var combinedOpacity = FilterOpacity * FilterGlobalOpacity;
        SetParameter("uOpacity", combinedOpacity);
        SetParameter("_uOpacity", combinedOpacity);
        SetParameter("uIntensity", FilterIntensity);
        SetParameter("_uIntensity", FilterIntensity);
        SetParameter("uProgress", FilterProgress);
        SetParameter("_uProgress", FilterProgress);
        SetParameter("uDirection", FilterDirection);
        SetParameter("_uDirection", FilterDirection);
        SetParameter("uTargetPosition", FilterTargetPosition);
        SetParameter("_uTargetPosition", FilterTargetPosition);
        SetParameter("uImageOffset", FilterImageOffset);
        SetParameter("_uImageOffset", FilterImageOffset);
        SetParameter("uImageScale", FilterImageScale);
        SetParameter("_uImageScale", FilterImageScale);
        SetParameter("uGlobalOpacity", FilterGlobalOpacity);
        SetParameter("_uGlobalOpacity", FilterGlobalOpacity);
        var time = FilterTimeOverride ?? (float)gameTime.TotalGameTime.TotalSeconds;
        SetParameter("uTime", time);
        SetParameter("iTime", time);
        var safeZoom = new Vector2(
            Math.Max(Math.Abs(FilterZoom.X), 0.0001f),
            Math.Max(Math.Abs(FilterZoom.Y), 0.0001f));
        SetParameter("uScreenResolution", new Vector2(viewport.Width, viewport.Height) / safeZoom);
        SetParameter("uScreenPosition", FilterScreenPosition);
        SetParameter("uZoom", FilterZoom);
        var inputSize = SceneFilterGeometry && _sceneTarget1 is not null
            ? new Vector2(_sceneTarget1.Width, _sceneTarget1.Height)
            : _spriteTexture is null
                ? Vector2.One
                : new Vector2(_spriteTexture.Width, _spriteTexture.Height);
        SetParameter("uImageSize0", inputSize);
        for (var index = 0; index < _channelTextures.Length; index++)
        {
            var texture = _channelTextures[index];
            var size = texture is null ? Vector2.One : new Vector2(texture.Width, texture.Height);
            SetParameter($"uImageSize{index + 1}", size * FilterImageScale);
        }
        SetParameter("uSourceRect", ForceFieldGeometry
            ? new Vector4(0f, 0f, 600f, 600f)
            : Vector4.Zero);
        SetTextureParameter("PreviewTexture", _spriteTexture ?? _pixel);
        SetTextureParameter("Texture", _spriteTexture ?? _pixel);
    }

    private void SetParameter(string name, float value)
        => SpriteEffect?.Parameters[name]?.SetValue(value);

    private void SetParameter(string name, Vector2 value)
        => SpriteEffect?.Parameters[name]?.SetValue(value);

    private void SetParameter(string name, Vector3 value)
        => SpriteEffect?.Parameters[name]?.SetValue(value);

    private void SetParameter(string name, Vector4 value)
        => SpriteEffect?.Parameters[name]?.SetValue(value);

    private void SetParameter(string name, Matrix value)
        => SpriteEffect?.Parameters[name]?.SetValue(value);

    private static void SetParameter(Effect effect, string name, float value)
        => effect.Parameters[name]?.SetValue(value);

    private static void SetParameter(Effect effect, string name, Vector2 value)
        => effect.Parameters[name]?.SetValue(value);

    private static void SetParameter(Effect effect, string name, Vector3 value)
        => effect.Parameters[name]?.SetValue(value);

    private static void SetParameter(Effect effect, string name, Vector4 value)
        => effect.Parameters[name]?.SetValue(value);

    private static void SetParameter(Effect effect, string name, Matrix value)
        => effect.Parameters[name]?.SetValue(value);

    private void SetTextureParameter(string name, Texture2D value)
        => SpriteEffect?.Parameters[name]?.SetValue(value);

    protected override void UnloadContent()
    {
        foreach (var filter in _sceneFilters)
            foreach (var texture in filter.Channels)
                texture?.Dispose();
        _sceneFilters.Clear();
        foreach (var manager in _filterContentManagers)
            manager.Dispose();
        _filterContentManagers.Clear();
        _sceneTarget1?.Dispose();
        _sceneTarget1 = null;
        _sceneTarget2?.Dispose();
        _sceneTarget2 = null;
        base.UnloadContent();
    }

    private sealed class SceneFilterRuntime(
        SceneFilterRuntimeDefinition definition,
        Effect effect,
        Texture2D?[] channels,
        int registrationIndex)
    {
        public SceneFilterRuntimeDefinition Definition { get; } = definition;
        public Effect Effect { get; } = effect;
        public Texture2D?[] Channels { get; } = channels;
        public int RegistrationIndex { get; } = registrationIndex;
        public float Opacity { get; set; }
    }

    private void SetFloatingOwner()
    {
        if (_ownerHandle == IntPtr.Zero || Window.Handle == IntPtr.Zero)
        {
            return;
        }

        // Assigning an owner keeps this native MonoGame window above the WPF
        // main window, removes it from the taskbar, and makes it follow the
        // owner's minimize/restore/close lifecycle.
        SetWindowLongPtr(Window.Handle, GWL_HWNDPARENT, _ownerHandle);
        var style = GetWindowLongPtr(Window.Handle, GWL_EXSTYLE).ToInt64();
        style |= WS_EX_TOOLWINDOW;
        style &= ~WS_EX_APPWINDOW;
        SetWindowLongPtr(Window.Handle, GWL_EXSTYLE, new IntPtr(style));
        SetWindowPos(Window.Handle, HWND_TOP, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_FRAMECHANGED);
    }

    private const int GWL_EXSTYLE = -20;
    private const int GWL_HWNDPARENT = -8;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;
    private const long WS_EX_APPWINDOW = 0x00040000L;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private static readonly IntPtr HWND_TOP = IntPtr.Zero;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}

internal sealed class SceneFilterStackRuntimeDocument
{
    public int Version { get; set; } = 1;
    public int FilterLimit { get; set; } = 16;
    public int PriorityThreshold { get; set; }
    public List<SceneFilterRuntimeDefinition> Filters { get; set; } = new();
}

internal sealed class SceneFilterRuntimeDefinition
{
    public string Name { get; set; } = "Filter";
    public string EffectPath { get; set; } = string.Empty;
    public string EntryPoint { get; set; } = "main";
    public int Priority { get; set; }
    public bool Active { get; set; } = true;
    public bool IsHidden { get; set; }
    public ScreenShaderRuntimeValues Values { get; set; } = new();
    public List<string> TexturePaths { get; set; } = new();
}

internal sealed class ScreenShaderRuntimeValues
{
    public string Color { get; set; } = "float3(1,1,1)";
    public string SecondaryColor { get; set; } = "float3(1,1,1)";
    public string Opacity { get; set; } = "1";
    public string Intensity { get; set; } = "1";
    public string Progress { get; set; } = "0";
    public string Direction { get; set; } = "0,1";
    public string TargetPosition { get; set; } = "1,1";
    public string ImageOffset { get; set; } = "0,0";
    public string ImageScale { get; set; } = "1,1";
    public string GlobalOpacity { get; set; } = "1";
    public string ScreenPosition { get; set; } = "0,0";
    public string Zoom { get; set; } = "1,1";
}
