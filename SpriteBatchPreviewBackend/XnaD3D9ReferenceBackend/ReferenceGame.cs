using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace XnaD3D9ReferenceBackend;

internal sealed class ReferenceGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly Dictionary<string, string> _arguments;
    private SpriteBatch _spriteBatch;
    private ContentManager _terrariaContent;
    private Texture2D _perlin;
    private Effect _effect;
    private RenderTarget2D _sceneTarget;
    private Texture2D _pixel;
    private string _mode;
    private string _capturePath;
    private int _frame;

    public ReferenceGame(string[] args)
    {
        _arguments = ParseArguments(args);
        _mode = Get("mode", "forcefield").ToLowerInvariant();
        _capturePath = Get("capture", string.Empty);
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = ParseInt("width", 800),
            PreferredBackBufferHeight = ParseInt("height", 480),
            GraphicsProfile = GraphicsProfile.Reach,
            SynchronizeWithVerticalRetrace = false
        };
        IsFixedTimeStep = false;
        IsMouseVisible = true;
        Activated += (sender, eventArgs) => Log("Activated");
        Deactivated += (sender, eventArgs) => Log("Deactivated");
        Exiting += (sender, eventArgs) => Log("Exiting");
        Window.Title = "XNA/D3D9 Terraria Shader Reference";
        Log("constructed");
    }

    protected override void Initialize()
    {
        Log("Initialize begin");
        base.Initialize();
        Log("Initialize complete");
    }

    protected override void BeginRun()
    {
        Log("BeginRun");
        base.BeginRun();
    }

    protected override void Update(GameTime gameTime)
    {
        if (_frame == 0) Log("Update");
        base.Update(gameTime);
    }

    protected override void LoadContent()
    {
        Log("LoadContent begin");
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        var contentRoot = Get("content-root", @"E:\Steam\steamapps\common\Terraria\Content");
        _terrariaContent = new ContentManager(Services, contentRoot);
        _perlin = _terrariaContent.Load<Texture2D>("Images/Misc/Perlin");
        _effect = _terrariaContent.Load<Effect>(_mode == "filtertower" ? "ScreenShader" : "PixelShader");
        _sceneTarget = new RenderTarget2D(GraphicsDevice,
            GraphicsDevice.PresentationParameters.BackBufferWidth,
            GraphicsDevice.PresentationParameters.BackBufferHeight,
            false, SurfaceFormat.Color, DepthFormat.None);
        Log("LoadContent complete");
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_frame == 0) Log("first Draw");
        if (_mode == "filtertower")
            DrawFilterTower(gameTime);
        else
            DrawForceField(gameTime);

        base.Draw(gameTime);
        if (!string.IsNullOrWhiteSpace(_capturePath) && ++_frame >= 3)
        {
            Capture(_capturePath);
            Log("capture complete");
            Exit();
        }
    }

    private void DrawForceField(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(32, 32, 48));
        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
            SamplerState.PointWrap, DepthStencilState.Default, RasterizerState.CullNone);
        Set("uColor", ParseVector3("color", new Vector3(1.5f)));
        Set("uSecondaryColor", ParseVector3("secondary-color", Vector3.One));
        Set("uTime", ParseFloat("time", (float)gameTime.TotalGameTime.TotalSeconds));
        Set("uOpacity", ParseFloat("opacity", 1f));
        Set("uSourceRect", new Vector4(0f, 0f, 600f, 600f));
        Set("uImageSize0", new Vector2(_perlin.Width, _perlin.Height));
        _effect.CurrentTechnique.Passes["ForceField"].Apply();
        var viewport = GraphicsDevice.Viewport;
        _spriteBatch.Draw(_perlin,
            new Vector2(viewport.Width * 0.5f, viewport.Height * 0.5f),
            new Rectangle(0, 0, 600, 600), Color.White, 0f,
            new Vector2(300f, 300f), new Vector2(0.6f, 0.3f),
            SpriteEffects.None, 0f);
        _spriteBatch.End();
    }

    private void DrawFilterTower(GameTime gameTime)
    {
        var width = _sceneTarget.Width;
        var height = _sceneTarget.Height;
        GraphicsDevice.SetRenderTarget(_sceneTarget);
        GraphicsDevice.Clear(new Color(32, 32, 48));
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone);
        _spriteBatch.Draw(_perlin, new Rectangle(0, 0, width, height), Color.White);
        _spriteBatch.End();

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(new Color(32, 32, 48));
        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone);
        Set("uColor", ParseVector3("color", new Vector3(0f, 0.7f, 0.7f)));
        Set("uSecondaryColor", ParseVector3("secondary-color", Vector3.One));
        Set("uOpacity", ParseFloat("opacity", 0.5f) * ParseFloat("global-opacity", 1f));
        Set("uTime", ParseFloat("time", (float)gameTime.TotalGameTime.TotalSeconds));
        Set("uScreenResolution", new Vector2(width, height));
        Set("uScreenPosition", ParseVector2("screen-position", Vector2.Zero));
        Set("uTargetPosition", ParseVector2("target-position", new Vector2(width, height) * 0.5f));
        Set("uImageOffset", ParseVector2("image-offset", Vector2.Zero));
        Set("uIntensity", ParseFloat("intensity", 1f));
        Set("uProgress", ParseFloat("progress", 0f));
        Set("uDirection", ParseVector2("direction", new Vector2(0f, 1f)));
        Set("uZoom", Vector2.One);
        for (var slot = 1; slot <= 3; slot++)
        {
            GraphicsDevice.Textures[slot] = _perlin;
            GraphicsDevice.SamplerStates[slot] = SamplerState.LinearWrap;
            Set("uImageSize" + slot, new Vector2(_perlin.Width, _perlin.Height));
        }
        _effect.CurrentTechnique.Passes["FilterTower"].Apply();
        _spriteBatch.Draw(_sceneTarget, Vector2.Zero, Color.White);
        _spriteBatch.End();
    }

    private void Capture(string path)
    {
        var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
        var height = GraphicsDevice.PresentationParameters.BackBufferHeight;
        var data = new Color[width * height];
        GraphicsDevice.GetBackBufferData(data);
        using (var texture = new Texture2D(GraphicsDevice, width, height))
        {
            texture.SetData(data);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using (var stream = File.Create(path))
                texture.SaveAsPng(stream, width, height);
        }
    }

    private void Set(string name, float value) { var parameter = _effect.Parameters[name]; if (parameter != null) parameter.SetValue(value); }
    private void Set(string name, Vector2 value) { var parameter = _effect.Parameters[name]; if (parameter != null) parameter.SetValue(value); }
    private void Set(string name, Vector3 value) { var parameter = _effect.Parameters[name]; if (parameter != null) parameter.SetValue(value); }
    private void Set(string name, Vector4 value) { var parameter = _effect.Parameters[name]; if (parameter != null) parameter.SetValue(value); }

    private string Get(string name, string fallback) => _arguments.TryGetValue(name, out var value) ? value : fallback;
    private int ParseInt(string name, int fallback) => int.TryParse(Get(name, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    private float ParseFloat(string name, float fallback) => float.TryParse(Get(name, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    private Vector2 ParseVector2(string name, Vector2 fallback) => TryVector(Get(name, ""), 2, out var v) ? new Vector2(v[0], v[1]) : fallback;
    private Vector3 ParseVector3(string name, Vector3 fallback) => TryVector(Get(name, ""), 3, out var v) ? new Vector3(v[0], v[1], v[2]) : fallback;

    private static bool TryVector(string text, int count, out float[] result)
    {
        var normalized = (text ?? string.Empty).Trim();
        var open = normalized.IndexOf('(');
        if (open >= 0 && normalized.EndsWith(")")) normalized = normalized.Substring(open + 1, normalized.Length - open - 2);
        var parts = normalized.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        result = new float[count];
        if (parts.Length != count) return false;
        for (var i = 0; i < count; i++)
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i])) return false;
        return true;
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i + 1 < args.Length; i += 2)
            if (args[i].StartsWith("--", StringComparison.Ordinal)) result[args[i].Substring(2)] = args[i + 1];
        return result;
    }

    private void Log(string message)
    {
        if (string.IsNullOrWhiteSpace(_capturePath)) return;
        File.AppendAllText(_capturePath + ".log", DateTime.Now.ToString("O", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine);
    }
}
