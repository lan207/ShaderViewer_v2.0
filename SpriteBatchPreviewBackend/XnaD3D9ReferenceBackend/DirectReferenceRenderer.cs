using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace XnaD3D9ReferenceBackend;

internal sealed class DirectReferenceRenderer : IDisposable
{
    private readonly Dictionary<string, string> _arguments;
    private readonly Form _window;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly GraphicsDeviceService _graphicsService;
    private readonly ContentManager _content;
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _perlin;
    private readonly Effect _effect;
    private readonly int _width;
    private readonly int _height;
    private readonly string _mode;

    public DirectReferenceRenderer(string[] args)
    {
        _arguments = ParseArguments(args);
        _width = ParseInt("width", 800);
        _height = ParseInt("height", 480);
        _mode = Get("mode", "forcefield").ToLowerInvariant();

        _window = new Form
        {
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.FixedToolWindow,
            StartPosition = FormStartPosition.Manual,
            Location = new System.Drawing.Point(-32000, -32000),
            ClientSize = new System.Drawing.Size(_width, _height)
        };
        _window.CreateControl();

        var presentation = new PresentationParameters
        {
            BackBufferWidth = _width,
            BackBufferHeight = _height,
            BackBufferFormat = SurfaceFormat.Color,
            DepthStencilFormat = DepthFormat.Depth24Stencil8,
            DeviceWindowHandle = _window.Handle,
            IsFullScreen = false,
            PresentationInterval = PresentInterval.Immediate,
            RenderTargetUsage = RenderTargetUsage.PreserveContents
        };
        var requestedProfile = string.Equals(Get("profile", "hidef"), "reach", StringComparison.OrdinalIgnoreCase)
            ? GraphicsProfile.Reach
            : GraphicsProfile.HiDef;
        _graphicsDevice = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, requestedProfile, presentation);
        _graphicsService = new GraphicsDeviceService(_graphicsDevice);
        var services = new SimpleServiceProvider(_graphicsService);
        _content = new ContentManager(services, Get("content-root", @"E:\Steam\steamapps\common\Terraria\Content"));
        _spriteBatch = new SpriteBatch(_graphicsDevice);
        _perlin = _content.Load<Texture2D>("Images/Misc/Perlin");
        DumpTextureStats(_perlin, Get("capture", Path.Combine(Path.GetTempPath(), "xna-d3d9-reference.png")) + ".texture.txt");
        var dumpPerlin = Get("dump-perlin", string.Empty);
        if (!string.IsNullOrWhiteSpace(dumpPerlin))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dumpPerlin)));
            using (var stream = File.Create(dumpPerlin))
                _perlin.SaveAsPng(stream, _perlin.Width, _perlin.Height);
        }
        _effect = _content.Load<Effect>(_mode == "filtertower" ? "ScreenShader" : "PixelShader");
    }

    public void Run()
    {
        var dumpPerlin = Get("dump-perlin", string.Empty);
        if (!string.IsNullOrWhiteSpace(dumpPerlin))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dumpPerlin)));
            using (var stream = File.Create(dumpPerlin))
                _perlin.SaveAsPng(stream, _perlin.Width, _perlin.Height);
        }
        using (var output = new RenderTarget2D(_graphicsDevice, _width, _height, false,
            SurfaceFormat.Color, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.PreserveContents))
        {
            if (_mode == "filtertower")
                RenderFilterTower(output);
            else if (_mode == "raw")
                RenderRaw(output);
            else
                RenderForceField(output);
            Save(output, Get("capture", Path.Combine(Path.GetTempPath(), "xna-d3d9-reference.png")));
        }
    }

    private void RenderRaw(RenderTarget2D output)
    {
        _graphicsDevice.SetRenderTarget(output);
        _graphicsDevice.Clear(Color.Transparent);
        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
            SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone);
        _spriteBatch.Draw(_perlin, new Rectangle(144, 0, 512, 480), Color.White);
        _spriteBatch.End();
        _graphicsDevice.SetRenderTarget(null);
    }

    private void RenderForceField(RenderTarget2D output)
    {
        Texture2D temporaryInput = null;
        var input = _perlin;
        if (string.Equals(Get("input", "perlin"), "white", StringComparison.OrdinalIgnoreCase))
        {
            temporaryInput = new Texture2D(_graphicsDevice, 512, 512);
            var white = new Color[512 * 512];
            for (var i = 0; i < white.Length; i++) white[i] = Color.White;
            temporaryInput.SetData(white);
            input = temporaryInput;
        }
        _graphicsDevice.SetRenderTarget(output);
        _graphicsDevice.Clear(new Color(32, 32, 48));
        var blend = string.Equals(Get("blend", "alpha"), "opaque", StringComparison.OrdinalIgnoreCase)
            ? BlendState.Opaque
            : BlendState.AlphaBlend;
        _spriteBatch.Begin(SpriteSortMode.Immediate, blend,
            SamplerState.PointWrap, DepthStencilState.Default, RasterizerState.CullNone);
        DumpSpriteEffect(_spriteBatch, Get("capture", Path.Combine(Path.GetTempPath(), "xna-d3d9-reference.pngroch slightly.png")) + ".spriteeffect.txt");
        DumpObject(_spriteBatch, Get("capture", Path.Combine(Path.GetTempPath(), "xna-d3d9-reference.png")) + ".spritebatch.txt");
        Set("uColor", ParseVector3("color", new Vector3(1.5f)));
        Set("uSecondaryColor", ParseVector3("secondary-color", Vector3.One));
        Set("uTime", ParseFloat("time", 1f));
        Set("uOpacity", ParseFloat("opacity", 1f));
        Set("uSourceRect", new Vector4(0f, 0f, 600f, 600f));
        Set("uImageSize0", ParseVector2("image-size", new Vector2(input.Width, input.Height)));
        var forceFieldPass = _effect.CurrentTechnique.Passes["ForceField"];
        DumpObject(forceFieldPass, Get("capture", Path.Combine(Path.GetTempPath(), "xna-d3d9-reference.png")) + ".pass.txt");
        forceFieldPass.Apply();
        var scale = ParseVector2("scale", new Vector2(0.6f, 0.3f));
        _spriteBatch.Draw(input, new Vector2(_width * 0.5f, _height * 0.5f),
            new Rectangle(0, 0, 600, 600), Color.White, 0f, new Vector2(300f, 300f),
            scale, SpriteEffects.None, 0f);
        DumpSpriteVertices(_spriteBatch, Get("capture", Path.Combine(Path.GetTempPath(), "xna-d3d9-reference.png")) + ".vertices.txt");
        _spriteBatch.End();
        _graphicsDevice.SetRenderTarget(null);
        if (temporaryInput != null) temporaryInput.Dispose();
    }

    private void RenderFilterTower(RenderTarget2D output)
    {
        using (var scene = new RenderTarget2D(_graphicsDevice, _width, _height, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents))
        {
            _graphicsDevice.SetRenderTarget(scene);
            _graphicsDevice.Clear(new Color(32, 32, 48));
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone);
            _spriteBatch.Draw(_perlin, new Rectangle(0, 0, _width, _height), Color.White);
            _spriteBatch.End();

            _graphicsDevice.SetRenderTarget(output);
            _graphicsDevice.Clear(new Color(32, 32, 48));
            _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone);
            Set("uColor", ParseVector3("color", new Vector3(0f, 0.7f, 0.7f)));
            Set("uSecondaryColor", ParseVector3("secondary-color", Vector3.One));
            Set("uOpacity", ParseFloat("opacity", 0.5f) * ParseFloat("global-opacity", 1f));
            Set("uTime", ParseFloat("time", 1f));
            Set("uScreenResolution", new Vector2(_width, _height));
            Set("uScreenPosition", ParseVector2("screen-position", Vector2.Zero));
            Set("uTargetPosition", ParseVector2("target-position", new Vector2(_width, _height) * 0.5f));
            Set("uImageOffset", ParseVector2("image-offset", Vector2.Zero));
            Set("uIntensity", ParseFloat("intensity", 1f));
            Set("uProgress", ParseFloat("progress", 0f));
            Set("uDirection", ParseVector2("direction", new Vector2(0f, 1f)));
            Set("uZoom", Vector2.One);
            for (var slot = 1; slot <= 3; slot++)
            {
                _graphicsDevice.Textures[slot] = _perlin;
                _graphicsDevice.SamplerStates[slot] = SamplerState.LinearWrap;
                Set("uImageSize" + slot, new Vector2(_perlin.Width, _perlin.Height));
            }
            _effect.CurrentTechnique.Passes["FilterTower"].Apply();
            _spriteBatch.Draw(scene, Vector2.Zero, Color.White);
            _spriteBatch.End();
            _graphicsDevice.SetRenderTarget(null);
        }
    }

    private static void Save(RenderTarget2D target, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        using (var stream = File.Create(path))
            target.SaveAsPng(stream, target.Width, target.Height);
    }

    public void Dispose()
    {
        _effect.Dispose();
        _perlin.Dispose();
        _spriteBatch.Dispose();
        _content.Dispose();
        _graphicsService.Dispose();
        _graphicsDevice.Dispose();
        _window.Dispose();
    }

    private void Set(string name, float value) { var p = _effect.Parameters[name]; if (p != null) p.SetValue(value); }
    private void Set(string name, Vector2 value) { var p = _effect.Parameters[name]; if (p != null) p.SetValue(value); }
    private void Set(string name, Vector3 value) { var p = _effect.Parameters[name]; if (p != null) p.SetValue(value); }
    private void Set(string name, Vector4 value) { var p = _effect.Parameters[name]; if (p != null) p.SetValue(value); }
    private string Get(string name, string fallback) => _arguments.TryGetValue(name, out var value) ? value : fallback;
    private int ParseInt(string name, int fallback) => int.TryParse(Get(name, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    private float ParseFloat(string name, float fallback) => float.TryParse(Get(name, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    private Vector2 ParseVector2(string name, Vector2 fallback) => TryVector(Get(name, ""), 2, out var v) ? new Vector2(v[0], v[1]) : fallback;
    private Vector3 ParseVector3(string name, Vector3 fallback) => TryVector(Get(name, ""), 3, out var v) ? new Vector3(v[0], v[1], v[2]) : fallback;
    private static bool TryVector(string text, int count, out float[] result)
    {
        var normalized = (text ?? "").Trim();
        var open = normalized.IndexOf('(');
        if (open >= 0 && normalized.EndsWith(")")) normalized = normalized.Substring(open + 1, normalized.Length - open - 2);
        var parts = normalized.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        result = new float[count];
        if (parts.Length != count) return false;
        for (var i = 0; i < count; i++) if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i])) return false;
        return true;
    }
    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i + 1 < args.Length; i += 2) if (args[i].StartsWith("--")) result[args[i].Substring(2)] = args[i + 1];
        return result;
    }

    private static unsafe void DumpObject(object value, string path)
    {
        using (var writer = new StreamWriter(path, false))
        {
            var type = value.GetType();
            writer.WriteLine(type.AssemblyQualifiedName);
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                object fieldValue;
                try { fieldValue = field.GetValue(value); }
                catch (Exception ex) { fieldValue = ex.GetType().Name; }
                var display = fieldValue is Pointer
                    ? "0x" + ((ulong)(void*)Pointer.Unbox(fieldValue)).ToString("X")
                    : (fieldValue ?? "<null>").ToString();
                writer.WriteLine(field.FieldType.FullName + " " + field.Name + " = " + display);
                if (fieldValue is Pointer && (field.Name == "pVertexShaderCode" || field.Name == "pPixelShaderCode"))
                {
                    var words = (uint*)Pointer.Unbox(fieldValue);
                    if (words != null)
                    {
                        for (var i = 0; i < 8; i++)
                            writer.WriteLine("  word[" + i + "]=0x" + words[i].ToString("X8"));
                        var bytecode = ReadShaderBytecode(words);
                        File.WriteAllBytes(path + "." + field.Name + ".bin", bytecode);
                        writer.WriteLine("  bytecodeLength=" + bytecode.Length);
                    }
                }
                if (fieldValue is Array array)
                {
                    writer.WriteLine("  array length=" + array.Length);
                    for (var i = 0; i < Math.Min(array.Length, 32); i++)
                        writer.WriteLine("  [" + i + "]=" + (array.GetValue(i) ?? "<null>"));
                }
            }
        }
    }

    private static void DumpTextureStats(Texture2D texture, string path)
    {
        var pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);
        long sum = 0;
        var min = 255;
        var max = 0;
        var zero = 0;
        foreach (var color in pixels)
        {
            sum += color.A;
            min = Math.Min(min, color.A);
            max = Math.Max(max, color.A);
            if (color.A == 0) zero++;
        }
        File.WriteAllText(path,
            "Size=" + texture.Width + "x" + texture.Height + Environment.NewLine +
            "AverageAlpha=" + (sum / (double)pixels.Length).ToString("R", CultureInfo.InvariantCulture) + Environment.NewLine +
            "MinAlpha=" + min + Environment.NewLine +
            "MaxAlpha=" + max + Environment.NewLine +
            "ZeroAlpha=" + zero);
    }

    private static void DumpSpriteEffect(SpriteBatch spriteBatch, string path)
    {
        var field = typeof(SpriteBatch).GetField("spriteEffect", BindingFlags.Instance | BindingFlags.NonPublic);
        var effect = field == null ? null : field.GetValue(spriteBatch) as Effect;
        if (effect == null)
        {
            File.WriteAllText(path, "SpriteBatch.spriteEffect not found.");
            return;
        }
        using (var writer = new StreamWriter(path, false))
        {
            writer.WriteLine("Technique=" + effect.CurrentTechnique.Name);
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                writer.WriteLine("Pass=" + pass.Name);
                var passType = pass.GetType();
                foreach (var name in new[] { "pVertexShaderCode", "pPixelShaderCode" })
                {
                    var codeField = passType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                    var boxed = codeField == null ? null : codeField.GetValue(pass);
                    unsafe
                    {
                        var words = boxed is Pointer ? (uint*)Pointer.Unbox(boxed) : null;
                        writer.WriteLine(name + "=" + (words == null ? "null" : "0x" + ((ulong)words).ToString("X")));
                        if (words != null)
                        {
                            var bytecode = ReadShaderBytecode(words);
                            for (var i = 0; i < Math.Min(bytecode.Length / 4, 1024); i++)
                                writer.WriteLine("  word[" + i + "]=0x" + words[i].ToString("X8"));
                            File.WriteAllBytes(path + "." + name + ".bin", bytecode);
                        }
                    }
                }
            }
        }
    }

    private static unsafe byte[] ReadShaderBytecode(uint* words)
    {
        var result = new List<byte>();
        void Append(uint word) => result.AddRange(BitConverter.GetBytes(word));
        Append(words[0]); // shader version
        var index = 1;
        while (index < 16384)
        {
            var token = words[index];
            Append(token);
            if (token == 0x0000FFFF)
                return result.ToArray();

            var opcode = token & 0xFFFF;
            var operandCount = opcode == 0xFFFE
                ? (int)((token >> 16) & 0x7FFF) // COMMENT payload DWORDs
                : (int)((token >> 24) & 0x0F);  // SM2/SM3 instruction length
            if (operandCount <= 0 || index + operandCount >= 16384)
                throw new InvalidDataException("Invalid D3D9 shader instruction length.");
            for (var operand = 0; operand < operandCount; operand++)
                Append(words[index + 1 + operand]);
            index += 1 + operandCount;
        }
        throw new InvalidDataException("D3D9 shader END token was not found.");
    }

    private static void DumpSpriteVertices(SpriteBatch spriteBatch, string path)
    {
        var field = typeof(SpriteBatch).GetField("outputVertices", BindingFlags.Instance | BindingFlags.NonPublic);
        var vertices = field == null ? null : field.GetValue(spriteBatch) as VertexPositionColorTexture[];
        using (var writer = new StreamWriter(path, false))
        {
            if (vertices == null) { writer.WriteLine("outputVertices not found"); return; }
            var nonZero = 0;
            for (var i = 0; i < vertices.Length; i++)
            {
                var vertex = vertices[i];
                if (vertex.Color.PackedValue == 0 && vertex.Position == Vector3.Zero && vertex.TextureCoordinate == Vector2.Zero) continue;
                writer.WriteLine(i + ": " + vertex);
                if (++nonZero >= 16) break;
            }
            writer.WriteLine("nonZeroShown=" + nonZero);
        }
    }

    private sealed class SimpleServiceProvider : IServiceProvider
    {
        private readonly IGraphicsDeviceService _service;
        public SimpleServiceProvider(IGraphicsDeviceService service) { _service = service; }
        public object GetService(Type serviceType) => serviceType == typeof(IGraphicsDeviceService) ? _service : null;
    }

    private sealed class GraphicsDeviceService : IGraphicsDeviceService, IDisposable
    {
        public GraphicsDeviceService(GraphicsDevice device) { GraphicsDevice = device; }
        public GraphicsDevice GraphicsDevice { get; }
        public event EventHandler<EventArgs> DeviceCreated { add { } remove { } }
        public event EventHandler<EventArgs> DeviceDisposing { add { } remove { } }
        public event EventHandler<EventArgs> DeviceReset { add { } remove { } }
        public event EventHandler<EventArgs> DeviceResetting { add { } remove { } }
        public void Dispose() { }
    }
}
