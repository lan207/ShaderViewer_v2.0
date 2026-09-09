using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ShaderViewer.Rendering;

internal static class ShaderSourceTools
{
    private static readonly Regex TechniqueCompileRegex = new(@"\bcompile\s+ps_[0-9]+_(?:[0-9]+|[ab])(?:_level_[0-9]+_[0-9]+)?\s+(?<name>[A-Za-z_]\w*)\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Float4EntryRegex = new(@"(?m)^\s*(?:inline\s+|static\s+|extern\s+)?(?:float|half|fixed|vec)4\s+(?<name>(?!__d3d_)[A-Za-z_]\w*)\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ShaderModelRegex = new(@"\bcompile\s+(?<model>ps_[0-9]+_(?:[0-9]+|[ab])(?:_level_[0-9]+_[0-9]+)?)\s+[A-Za-z_]\w*\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EntryPointMarkerRegex = new(@"/\*\s*ShaderViewer\s+Entry\s+Point\s*:\s*(?<name>[A-Za-z_]\w*)\s*\*/", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex VectorConstructorRegex = new(@"^(?:float|half|fixed|vec)(?<size>[2-4])\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ParamLineRegex = new(@"^\s*static\s+const\s+(?<type>[A-Za-z_]\w*)\s+(?<name>[A-Za-z_]\w*)\s*=\s*(?<value>.+?)\s*;\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex GlobalParameterDeclarationRegex = new(@"^\s*(?<qualifiers>(?:(?:uniform|extern|static|const|volatile|shared)\s+)*)(?<type>float(?:[2-4])?|half(?:[2-4])?|fixed(?:[2-4])?|int|bool|vec[2-4])\s+(?<name>[A-Za-z_]\w*)(?:\s*:\s*(?:register\s*\([^)]*\)|[A-Za-z_]\w*))?(?:\s*=\s*(?<value>.+?))?\s*;\s*(?://.*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
     private static readonly Regex BuiltInDeclarationRegex = new(@"^\s*(?:(?:uniform|extern|static|const|volatile|shared)\s+)*(?<type>[A-Za-z_]\w*)\s+(?<name>iInput|iChannel[0-2]|iTime|iResolutionX|iResolutionY|iResolution|uTime|uColor|uSecondaryColor|uSecondColor|uOpacity|uIntensity|uProgress|uDirection|uTargetPosition|uImageOffset|uImageScale|uGlobalOpacity|uImageSize[0-3]|uSourceRect|uScreenResolution|uScreenPosition|uZoom|_uColor|_uSecondaryColor|_uSecondColor|_uOpacity|_uIntensity|_uProgress|_uDirection|_uTargetPosition|_uImageOffset|_uImageScale|_uGlobalOpacity|_uImageSize[0-3])(?:\s*:\s*(?:register\s*\([^)]*\)|[A-Za-z_]\w*))?(?:\s*=\s*.+?)?\s*;\s*(?://.*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex GlobalSamplerDeclarationRegex = new(@"^\s*(?:(?:uniform|extern|static|const|volatile|shared)\s+)*(?<type>sampler(?:1D|2D|3D|CUBE)?)\s+(?<name>[A-Za-z_]\w*)(?:\s*:\s*register\s*\(\s*s(?<slot>\d+)\s*\))?(?:\s*=\s*.+?)?\s*;\s*(?://.*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PseudoFxHelperRegex = new(@"(?m)^(?<indent>\s*)(?!static\b)(?<signature>float(?:[1-4])?\s+__d3d_[A-Za-z_]\w*\s*\()", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // XnbFxDecompiler emits one TEXCOORD0 argument and then aliases both the
    // SpriteBatch vertex color (v0) and texture coordinates (t0) to it.  That
    // is convenient for a textual disassembly, but it is not the signature
    // used by an XNA SpriteBatch pixel shader: v0 is COLOR0 and t0 is
    // TEXCOORD0.  WPF ShaderEffect supplies only texture coordinates, so the
    // preview uses a white vertex color while retaining t0's normalized UVs.
    private static readonly Regex PseudoFxInputSignatureRegex = new(
        @"(?<return>float4\s+)(?<name>[A-Za-z_]\w*)\s*\(\s*float4\s+(?<parameter>[A-Za-z_]\w*)\s*:\s*TEXCOORD0\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PseudoFxVertexColorAliasRegex = new(
        @"(?m)^(?<indent>\s*)float4\s+v0\s*=\s*(?<parameter>[A-Za-z_]\w*)\s*;",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PseudoFxTexcoordAliasRegex = new(
        @"(?m)^(?<indent>\s*)float4\s+t0\s*=\s*(?<parameter>[A-Za-z_]\w*)\s*;",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> BuiltInParameterNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "iInput",
        "iChannel0",
        "iChannel1",
        "iChannel2",
        "iTime",
        "iResolutionX",
        "iResolutionY",
        "iResolution",
        "uTime",
        "uColor",
        "uSecondaryColor",
        "uSecondColor",
        "uOpacity",
        "uIntensity",
        "uProgress",
        "uDirection",
        "uTargetPosition",
        "uImageOffset",
        "uImageScale",
        "uGlobalOpacity",
         "uImageSize0",
         "uImageSize1",
         "uImageSize2",
         "uImageSize3",
         "uSourceRect",
         "uScreenResolution",
         "uScreenPosition",
         "uZoom",
        "_uColor",
        "_uSecondaryColor",
        "_uSecondColor",
        "_uOpacity",
        "_uIntensity",
        "_uProgress",
        "_uDirection",
        "_uTargetPosition",
        "_uImageOffset",
        "_uImageScale",
        "_uGlobalOpacity",
        "_uImageSize0",
        "_uImageSize1",
        "_uImageSize2",
        "_uImageSize3"
    };

    private static readonly string[] PreviewSamplerNames =
    {
        "iInput",
        "iChannel0",
        "iChannel1",
        "iChannel2"
    };

    public static ShaderDocument Import(string source)
    {
        source ??= string.Empty;

        // XNA effects name their samplers after effect parameters (for example
        // `Main : register(s0)`). ShaderEffect instead exposes fixed sampler
        // properties at s0..s3. Normalize those declarations before parsing so
        // both old pseudo-FX files and current decompiler output sample the
        // textures selected in the previewer.
        source = NormalizePreviewSamplers(source);
        source = NormalizePseudoFxInputs(source);
        source = PseudoFxHelperRegex.Replace(source, "${indent}static ${signature}");

        var parameters = ParseParameters(source);
        var entryPoint = DetectEntryPoint(source);
        var declaredShaderProfile = DetectShaderProfile(source);
        var shaderModel = GetShaderModel(declaredShaderProfile);

        source = RemoveMarkedBlock(source, ParametersBeginMarker, ParametersEndMarker);
        source = RemoveMarkedBlock(source, PreludeBeginMarker, PreludeEndMarker);
        var declaredParameters = ParseGlobalParameters(ref source);
        parameters = MergeParameters(parameters, declaredParameters);
        // Keep every source line intact. The compile-document mapper relies on
        // the normalized body having the same line numbers as the text the
        // user opened, even when unsupported FX technique blocks are skipped.
        source = EnsureEntryPointMarker(RemoveTechniqueBlocks(source), entryPoint);

        return new ShaderDocument(source, entryPoint, shaderModel, declaredShaderProfile, parameters);
    }

    public static string BuildCompileSource(string body, IReadOnlyList<ShaderParameterDefinition> parameters)
        => BuildCompileDocument(body, parameters).Source;

    /// <summary>
    /// Builds the code passed to D3DCompile and records where the editable
    /// source begins.  The prelude and UI parameter block are generated, so
    /// compiler line numbers need translating before they are shown to users.
    /// </summary>
    public static ShaderCompileDocument BuildCompileDocument(string body, IReadOnlyList<ShaderParameterDefinition> parameters)
    {
        var sb = new StringBuilder();
        sb.AppendLine(PreludeBeginMarker);
        sb.AppendLine(ShaderPrelude.TrimEnd());
        sb.AppendLine(PreludeEndMarker);
        sb.AppendLine();
        AppendParameterBlock(sb, parameters);
        if (sb.Length > 0)
        {
            sb.AppendLine();
        }

        // Preserve the exact text (including leading blank lines) displayed in
        // the Source tab. This keeps compiler locations mappable one-to-one.
        var sourceBody = body ?? string.Empty;
        var bodyStartLine = CountLines(sb.ToString()) + 1;
        sb.AppendLine(sourceBody);
        var bodyLineCount = sourceBody.Length == 0 ? 0 : CountLines(sourceBody) + 1;
        return new ShaderCompileDocument(sb.ToString(), bodyStartLine, bodyLineCount);
    }

    /// <summary>
    /// Finds deterministic floating-point failures before the GPU evaluates
    /// the shader. GPUs commonly turn these into INF/NaN instead of throwing
    /// an exception, which otherwise leaves the preview blank with no clue.
    /// This deliberately reports only expressions that can be proven invalid
    /// from the source (rather than warning on every dynamic division).
    /// </summary>
    public static IReadOnlyList<ShaderSourceDiagnostic> AnalyzeRuntimeRisks(string source)
    {
        source ??= string.Empty;
        var diagnostics = new List<ShaderSourceDiagnostic>();
        var knownZeroNames = new HashSet<string>(StringComparer.Ordinal);
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var zeroDeclaration = new Regex(@"^\s*(?:static\s+|const\s+|uniform\s+|extern\s+)*(?:float|half|fixed|int|bool)\s+(?<name>[A-Za-z_]\w*)\s*=\s*(?<value>[+-]?0+(?:\.0*)?(?:f)?)\s*;", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var literalZeroDivision = new Regex(@"/\s*(?<zero>[+-]?0+(?:\.0*)?(?:f)?)(?![\w.])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var namedZeroDivision = new Regex(@"/\s*\(?\s*(?<name>[A-Za-z_]\w*)\b(?!\s*\()\s*\)?", RegexOptions.Compiled);
        var invalidSqrt = new Regex(@"\b(?:sqrt|rsqrt)\s*\(\s*-(?:\s*\d|\s*\.\d)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var invalidLog = new Regex(@"\blog\s*\(\s*(?:[+-]?0+(?:\.0*)?|-\s*\d)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var zeroNormalize = new Regex(@"\bnormalize\s*\(\s*(?:float|half|fixed)[2-4]\s*\(\s*0(?:\.0*)?\s*,\s*0(?:\.0*)?(?:\s*,\s*0(?:\.0*)?){0,2}\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        for (var index = 0; index < lines.Length; index++)
        {
            var line = StripLineComment(lines[index]);
            var lineNumber = index + 1;
            var declaration = zeroDeclaration.Match(line);
            if (declaration.Success)
            {
                knownZeroNames.Add(declaration.Groups["name"].Value);
            }

            foreach (Match match in literalZeroDivision.Matches(line))
            {
                AddDiagnostic(diagnostics, "error", "Division by literal zero produces INF or NaN at runtime.", lineNumber, match.Index + match.Value.IndexOf(match.Groups["zero"].Value, StringComparison.Ordinal) + 1);
            }

            foreach (Match match in namedZeroDivision.Matches(line))
            {
                if (knownZeroNames.Contains(match.Groups["name"].Value))
                {
                    AddDiagnostic(diagnostics, "error", $"Division by '{match.Groups["name"].Value}', which is initialized to zero, produces INF or NaN at runtime.", lineNumber, match.Index + match.Value.IndexOf(match.Groups["name"].Value, StringComparison.Ordinal) + 1);
                }
                else
                {
                    AddDiagnostic(diagnostics, "warning", $"Division by '{match.Groups["name"].Value}' can produce INF or NaN when it becomes zero. Guard the denominator with max(abs({match.Groups["name"].Value}), epsilon).", lineNumber, match.Index + match.Value.IndexOf(match.Groups["name"].Value, StringComparison.Ordinal) + 1);
                }
            }

            AddMatches(diagnostics, invalidSqrt, line, lineNumber, "error", "sqrt/rsqrt of a negative value produces NaN at runtime.");
            AddMatches(diagnostics, invalidLog, line, lineNumber, "error", "log of zero or a negative value produces -INF or NaN at runtime.");
            AddMatches(diagnostics, zeroNormalize, line, lineNumber, "warning", "Normalizing a zero vector can produce NaN at runtime.");
        }

        return diagnostics;
    }

    /// <summary>Returns preview sampler slots read by texture sampling calls.</summary>
    public static IReadOnlySet<int> FindRequiredTextureSlots(string source)
    {
        source ??= string.Empty;
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var aliasPattern = new Regex(@"(?m)^\s*#define\s+(?<source>[A-Za-z_]\w*)\s+(?<target>iInput|iChannel[0-2])\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        foreach (Match alias in aliasPattern.Matches(source))
        {
            aliases[alias.Groups["source"].Value] = alias.Groups["target"].Value;
        }

        var required = new HashSet<int>();
        var samplePattern = new Regex(@"\btex(?:1D|2D|3D|CUBE)(?:bias|proj|lod|grad)?\s*\(\s*(?<sampler>[A-Za-z_]\w*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        foreach (Match sample in samplePattern.Matches(StripBlockAndLineComments(source)))
        {
            var sampler = sample.Groups["sampler"].Value;
            if (aliases.TryGetValue(sampler, out var aliasTarget))
            {
                sampler = aliasTarget;
            }

            var index = Array.FindIndex(PreviewSamplerNames, item => string.Equals(item, sampler, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                required.Add(index);
            }
        }

        return required;
    }

    public static string BuildExportSource(string body, string shaderModel, IReadOnlyList<ShaderParameterDefinition> parameters, out string entryPoint)
    {
        var import = Import(body);
        entryPoint = import.EntryPoint;

        var compileSource = BuildCompileSource(import.Source, parameters);
        var techniqueName = $"Technique_{SanitizeIdentifier(entryPoint)}";
        var passName = $"Pass_{SanitizeIdentifier(entryPoint)}";

        var sb = new StringBuilder(compileSource.Length + 192);
        sb.Append(compileSource);
        sb.AppendLine();
        sb.AppendLine($"technique {techniqueName}");
        sb.AppendLine("{");
        sb.AppendLine($"    pass {passName}");
        sb.AppendLine("    {");
        sb.AppendLine($"        PixelShader = compile {shaderModel} {entryPoint}();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string BuildMonoGameSpriteEffectSource(
        string body,
        IReadOnlyList<ShaderParameterDefinition> parameters,
        out string entryPoint)
    {
        var import = Import(body);
        entryPoint = import.EntryPoint;
        var compileSource = BuildCompileSource(import.Source, parameters);
        var entryTakesFloat2 = Regex.IsMatch(
            import.Source,
            $@"\bfloat4\s+{Regex.Escape(entryPoint)}\s*\(\s*float2\b",
            RegexOptions.IgnoreCase);
        var entryArgument = entryTakesFloat2
            ? "input.TextureCoordinate"
            : "float4(input.TextureCoordinate, 0.0, 0.0)";

        var sb = new StringBuilder(compileSource.Length + 768);
        sb.AppendLine(compileSource);
        sb.AppendLine("float4x4 MatrixTransform;");
        sb.AppendLine("struct ShaderViewerSpriteVertexInput");
        sb.AppendLine("{");
        sb.AppendLine("    float4 Position : POSITION0;");
        sb.AppendLine("    float4 Color : COLOR0;");
        sb.AppendLine("    float2 TextureCoordinate : TEXCOORD0;");
        sb.AppendLine("};");
        sb.AppendLine("struct ShaderViewerSpriteVertexOutput");
        sb.AppendLine("{");
        sb.AppendLine("    float4 Position : SV_Position;");
        sb.AppendLine("    float4 Color : COLOR0;");
        sb.AppendLine("    float2 TextureCoordinate : TEXCOORD0;");
        sb.AppendLine("};");
        sb.AppendLine("ShaderViewerSpriteVertexOutput ShaderViewerSpriteVS(ShaderViewerSpriteVertexInput input)");
        sb.AppendLine("{");
        sb.AppendLine("    ShaderViewerSpriteVertexOutput output;");
        sb.AppendLine("    output.Position = mul(input.Position, MatrixTransform);");
        sb.AppendLine("    output.Color = input.Color;");
        sb.AppendLine("    output.TextureCoordinate = input.TextureCoordinate;");
        sb.AppendLine("    return output;");
        sb.AppendLine("}");
        sb.AppendLine("float4 ShaderViewerSpritePS(ShaderViewerSpriteVertexOutput input) : COLOR0");
        sb.AppendLine("{");
        sb.AppendLine($"    return {SanitizeIdentifier(entryPoint)}({entryArgument}) * input.Color;");
        sb.AppendLine("}");
        sb.AppendLine("technique ShaderViewerMonoGameSprite");
        sb.AppendLine("{");
        sb.AppendLine("    pass P0");
        sb.AppendLine("    {");
        // Decompiled ps_2_x instructions often expand into several HLSL
        // operations (notably cmp/cnd and sincos). level_9_1 keeps the old
        // 64 arithmetic-slot ceiling and rejects otherwise valid previews;
        // level_9_3 remains a down-level D3D target while providing the SM3
        // instruction budget needed by Terraria's ForceField pass.
        sb.AppendLine("        VertexShader = compile vs_4_0_level_9_3 ShaderViewerSpriteVS();");
        sb.AppendLine("        PixelShader = compile ps_4_0_level_9_3 ShaderViewerSpritePS();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static void AppendParameterBlock(StringBuilder sb, IReadOnlyList<ShaderParameterDefinition> parameters)
    {
        if (parameters.Count == 0)
        {
            return;
        }

        sb.AppendLine(ParametersBeginMarker);
        foreach (var parameter in parameters)
        {
            var name = SanitizeIdentifier(parameter.Name);
            var type = SanitizeType(parameter.Type);
            var value = parameter.Value.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                value = DefaultValueForType(type);
            }

            sb.Append("static const ");
            sb.Append(type);
            sb.Append(' ');
            sb.Append(name);
            sb.Append(" = ");
            sb.Append(NormalizeValueLiteral(type, value));
            sb.AppendLine(";");
        }
        sb.AppendLine(ParametersEndMarker);
    }

    public static string ShaderPrelude => """
sampler2D iInput : register(s0);
sampler2D iChannel0 : register(s1);
sampler2D iChannel1 : register(s2);
sampler2D iChannel2 : register(s3);

float iTime : register(c0);
float iResolutionX : register(c1);
float iResolutionY : register(c2);

// ScreenShaderData-compatible constants.  The aliases with a leading
// underscore match Terraria's internal naming while the public names match
// the Effect parameters used by ScreenShaderData.
float3 uColor : register(c3);
float3 uSecondaryColor : register(c4);
float uOpacity : register(c5);
float uIntensity : register(c6);
float uProgress : register(c7);
float2 uDirection : register(c8);
float2 uTargetPosition : register(c9);
float2 uImageOffset : register(c10);
float2 uImageScale : register(c11);
float uGlobalOpacity : register(c12);
float2 uImageSize0 : register(c13);
float2 uImageSize1 : register(c14);
float2 uImageSize2 : register(c15);
float2 uImageSize3 : register(c16);
float2 uScreenPosition : register(c17);
float2 uZoom : register(c18);
// DrawData source rectangle.  Terraria supplies this per draw; WPF has no
// equivalent SpriteBatch rectangle, so the preview keeps the default origin.
float4 uSourceRect : register(c30);

#define _uColor uColor
#define _uSecondaryColor uSecondaryColor
#define uSecondColor uSecondaryColor
#define _uSecondColor uSecondaryColor
#define _uOpacity uOpacity
#define _uIntensity uIntensity
#define _uProgress uProgress
#define _uDirection uDirection
#define _uTargetPosition uTargetPosition
#define _uImageOffset uImageOffset
#define _uImageScale uImageScale
#define _uGlobalOpacity uGlobalOpacity
#define _uImageSize0 uImageSize0
#define _uImageSize1 uImageSize1
#define _uImageSize2 uImageSize2
#define _uImageSize3 uImageSize3
#define uTime iTime
#define uScreenResolution iResolution

#define vec2 float2
#define vec3 float3
#define vec4 float4
#define iResolution float2(iResolutionX, iResolutionY)
#define fragCoord(uv) ((uv) * iResolution)

float2 GetResolution()
{
    return iResolution;
}

float2 getResolution()
{
    return iResolution;
}
""";

    public const string PreludeBeginMarker = "/* ShaderViewer Prelude Begin */";
    public const string PreludeEndMarker = "/* ShaderViewer Prelude End */";
    public const string ParametersBeginMarker = "/* ShaderViewer Parameters Begin */";
    public const string ParametersEndMarker = "/* ShaderViewer Parameters End */";

    private static string? DetectShaderProfile(string source)
    {
        var match = ShaderModelRegex.Match(source);
        return match.Success ? match.Groups["model"].Value.ToLowerInvariant() : null;
    }

    private static ShaderModel GetShaderModel(string? shaderProfile)
    {
        if (shaderProfile is null)
        {
            return ShaderModel.Ps20;
        }

        var parts = shaderProfile.Split('_');
        if (parts.Length >= 2 && int.TryParse(parts[1], out var majorVersion))
        {
            if (majorVersion == 3)
            {
                return ShaderModel.Ps30;
            }

            if (majorVersion > 3)
            {
                return ShaderModel.HigherThanPs30;
            }
        }

        return ShaderModel.Ps20;
    }

    private static string DetectEntryPoint(string source)
    {
        var markerMatch = EntryPointMarkerRegex.Match(source);
        if (markerMatch.Success)
        {
            return markerMatch.Groups["name"].Value;
        }

        var techniqueMatch = TechniqueCompileRegex.Match(source);
        if (techniqueMatch.Success)
        {
            return techniqueMatch.Groups["name"].Value;
        }

        var float4Match = Float4EntryRegex.Match(source);
        if (float4Match.Success)
        {
            return float4Match.Groups["name"].Value;
        }

        return "main";
    }

    private static string EnsureEntryPointMarker(string source, string entryPoint)
    {
        var marker = $"/* ShaderViewer Entry Point: {SanitizeIdentifier(entryPoint)} */";
        return EntryPointMarkerRegex.IsMatch(source)
            ? EntryPointMarkerRegex.Replace(source, marker, 1)
            : source.TrimEnd() + Environment.NewLine + marker + Environment.NewLine;
    }

    private static IReadOnlyList<ShaderParameterDefinition> ParseParameters(string source)
    {
        var block = ExtractMarkedBlock(source, ParametersBeginMarker, ParametersEndMarker);
        if (block is null)
        {
            return Array.Empty<ShaderParameterDefinition>();
        }

        var items = new List<ShaderParameterDefinition>();
        using var reader = new StringReader(block);
        while (reader.ReadLine() is { } line)
        {
            var match = ParamLineRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            items.Add(new ShaderParameterDefinition(
                match.Groups["name"].Value,
                match.Groups["type"].Value,
                match.Groups["value"].Value.Trim()));
        }

        return items;
    }

    private static string NormalizePreviewSamplers(string source)
    {
        var lines = source.Split('\n');
        var rebuilt = new StringBuilder(source.Length);
        var braceDepth = 0;
        var nextImplicitSlot = 0;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var lineForMatch = line.TrimEnd('\r');
            var match = braceDepth == 0 ? GlobalSamplerDeclarationRegex.Match(lineForMatch) : Match.Empty;

            if (match.Success)
            {
                var sourceName = match.Groups["name"].Value;
                var slot = match.Groups["slot"].Success
                    ? int.Parse(match.Groups["slot"].Value, System.Globalization.CultureInfo.InvariantCulture)
                    : PreviewSamplerIndex(sourceName) ?? nextImplicitSlot;
                nextImplicitSlot = Math.Max(nextImplicitSlot, slot + 1);

                // WPF ShaderEffect has four bindable sampler inputs. Effects
                // using more samplers remain compilable by reusing the last
                // preview channel; their exact multi-texture result cannot be
                // represented by this four-texture UI.
                var previewName = PreviewSamplerNames[Math.Clamp(slot, 0, PreviewSamplerNames.Length - 1)];
                if (!string.Equals(sourceName, previewName, StringComparison.OrdinalIgnoreCase))
                {
                    // Put the alias on the original declaration line instead
                    // of adding a macro block at the top. That keeps D3D
                    // diagnostics aligned with the source FX file.
                    rebuilt.Append("#define ");
                    rebuilt.Append(sourceName);
                    rebuilt.Append(' ');
                    rebuilt.Append(previewName);
                }
            }
            else
            {
                rebuilt.Append(line);
            }

            if (index < lines.Length - 1)
            {
                rebuilt.Append('\n');
            }

            braceDepth = Math.Max(0, braceDepth + CountBraces(lineForMatch));
        }

        return rebuilt.ToString();
    }

    private static string NormalizePseudoFxInputs(string source)
    {
        // Only touch files carrying the decompiler's declarations.  A normal
        // user shader may legitimately use a float4 TEXCOORD0 input and must
        // keep its original semantics.
        if (!source.Contains("dcl_usage0 v0", StringComparison.OrdinalIgnoreCase)
            || !source.Contains("dcl_usage0 t0.xy", StringComparison.OrdinalIgnoreCase))
        {
            return source;
        }

        var signature = PseudoFxInputSignatureRegex.Match(source);
        if (!signature.Success
            || !PseudoFxVertexColorAliasRegex.IsMatch(source)
            || !PseudoFxTexcoordAliasRegex.IsMatch(source))
        {
            return source;
        }

        var parameter = signature.Groups["parameter"].Value;
        source = PseudoFxInputSignatureRegex.Replace(
            source,
            match => $"{match.Groups["return"].Value}{match.Groups["name"].Value}(float2 {parameter} : TEXCOORD0)",
            1);
        source = PseudoFxVertexColorAliasRegex.Replace(
            source,
            match => $"{match.Groups["indent"].Value}float4 v0 = float4(1.0, 1.0, 1.0, 1.0);",
            1);
        source = PseudoFxTexcoordAliasRegex.Replace(
            source,
            match => $"{match.Groups["indent"].Value}float4 t0 = float4({parameter}, 0.0, 0.0);",
            1);
        return source;
    }

    private static int? PreviewSamplerIndex(string name)
    {
        for (var index = 0; index < PreviewSamplerNames.Length; index++)
        {
            if (string.Equals(name, PreviewSamplerNames[index], StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return null;
    }

    private static IReadOnlyList<ShaderParameterDefinition> ParseGlobalParameters(ref string source)
    {
        var parameters = new List<ShaderParameterDefinition>();
        var lines = source.Split('\n');
        var rebuilt = new StringBuilder(source.Length);
        var braceDepth = 0;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var lineForMatch = line.TrimEnd('\r');
            var builtInMatch = braceDepth == 0 ? BuiltInDeclarationRegex.Match(lineForMatch) : Match.Empty;
            var match = braceDepth == 0 ? GlobalParameterDeclarationRegex.Match(lineForMatch) : Match.Empty;

            if (builtInMatch.Success)
            {
                // The preview prelude owns these registers and supplies their values.
                rebuilt.Append(line.EndsWith('\r') ? "\r" : string.Empty);
            }
            else if (match.Success && IsAutoParameterDeclaration(match))
            {
                var type = match.Groups["type"].Value;
                var value = match.Groups["value"].Success
                    ? match.Groups["value"].Value.Trim()
                    : DefaultValueForType(type);
                parameters.Add(new ShaderParameterDefinition(
                    match.Groups["name"].Value,
                    type,
                    value));

                // Keep the line break so diagnostics and subsequent parsing retain stable line numbers.
                rebuilt.Append(line.EndsWith('\r') ? "\r" : string.Empty);
            }
            else
            {
                rebuilt.Append(line);
            }

            if (index < lines.Length - 1)
            {
                rebuilt.Append('\n');
            }

            braceDepth = Math.Max(0, braceDepth + CountBraces(lineForMatch));
        }

        source = rebuilt.ToString();
        return parameters;
    }

    private static bool IsAutoParameterDeclaration(Match match)
    {
        var name = match.Groups["name"].Value;
        if (BuiltInParameterNames.Contains(name))
        {
            return false;
        }

        var qualifiers = match.Groups["qualifiers"].Value;
        var hasExplicitParameterQualifier = Regex.IsMatch(
            qualifiers,
            @"\b(?:uniform|extern)\b",
            RegexOptions.IgnoreCase);
        var isConst = Regex.IsMatch(qualifiers, @"\bconst\b", RegexOptions.IgnoreCase);
        return hasExplicitParameterQualifier || !isConst;
    }

    private static int CountBraces(string line)
    {
        var depth = 0;
        foreach (var ch in line)
        {
            if (ch == '{')
            {
                depth++;
            }
            else if (ch == '}')
            {
                depth--;
            }
        }

        return depth;
    }

    private static IReadOnlyList<ShaderParameterDefinition> MergeParameters(
        IReadOnlyList<ShaderParameterDefinition> markedParameters,
        IReadOnlyList<ShaderParameterDefinition> declaredParameters)
    {
        if (markedParameters.Count == 0)
        {
            return declaredParameters;
        }

        if (declaredParameters.Count == 0)
        {
            return markedParameters;
        }

        var merged = new List<ShaderParameterDefinition>(markedParameters.Count + declaredParameters.Count);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in markedParameters)
        {
            if (names.Add(parameter.Name))
            {
                merged.Add(parameter);
            }
        }

        foreach (var parameter in declaredParameters)
        {
            if (names.Add(parameter.Name))
            {
                merged.Add(parameter);
            }
        }

        return merged;
    }

    private static string RemoveMarkedBlock(string source, string beginMarker, string endMarker)
    {
        var block = ExtractMarkedBlock(source, beginMarker, endMarker);
        if (block is null)
        {
            return source;
        }

        var begin = source.IndexOf(beginMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, begin, StringComparison.Ordinal);
        if (begin < 0 || end < 0)
        {
            return source;
        }

        end += endMarker.Length;

        // Keep one newline for each removed source line. Exported FX files
        // contain previewer-generated blocks; blanking their contents instead
        // of compacting them preserves locations for the user's actual code.
        var removed = source.Substring(begin, end - begin);
        var preservedNewlines = new string(removed.Where(ch => ch is '\r' or '\n').ToArray());
        return source[..begin] + preservedNewlines + source[end..];
    }

    private static string? ExtractMarkedBlock(string source, string beginMarker, string endMarker)
    {
        var begin = source.IndexOf(beginMarker, StringComparison.Ordinal);
        if (begin < 0)
        {
            return null;
        }

        var start = begin + beginMarker.Length;
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        return source.Substring(start, end - start);
    }

    private static string RemoveTechniqueBlocks(string source)
    {
        var techniqueIndex = source.IndexOf("technique", StringComparison.OrdinalIgnoreCase);
        if (techniqueIndex < 0)
        {
            return source;
        }

        var sb = new StringBuilder(source.Length);
        var cursor = 0;

        while (cursor < source.Length)
        {
            var match = Regex.Match(source.AsSpan(cursor).ToString(), @"\btechnique\b", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                sb.Append(source, cursor, source.Length - cursor);
                break;
            }

            var matchIndex = cursor + match.Index;
            sb.Append(source, cursor, matchIndex - cursor);
            var openBrace = source.IndexOf('{', matchIndex);
            if (openBrace < 0)
            {
                cursor = matchIndex + match.Length;
                continue;
            }

            var closeBrace = FindMatchingBrace(source, openBrace);
            if (closeBrace < 0)
            {
                break;
            }

            // Retain all line breaks belonging to the technique. HLSL sees
            // blank lines, but its reported locations for the remaining FX
            // body still match the line numbers in the editor.
            for (var index = matchIndex; index <= closeBrace; index++)
            {
                if (source[index] == '\r' || source[index] == '\n')
                {
                    sb.Append(source[index]);
                }
            }

            cursor = closeBrace + 1;
        }

        return sb.ToString();
    }

    private static int FindMatchingBrace(string source, int openBraceIndex)
    {
        var depth = 0;
        for (var i = openBraceIndex; i < source.Length; i++)
        {
            var ch = source[i];
            if (ch == '{')
            {
                depth++;
            }
            else if (ch == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static string SanitizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Shader";
        }

        var sb = new StringBuilder(value.Length + 1);
        foreach (var ch in value)
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }

        if (sb.Length == 0 || (!char.IsLetter(sb[0]) && sb[0] != '_'))
        {
            sb.Insert(0, '_');
        }

        return sb.ToString();
    }

    private static string SanitizeType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "float";
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "vec2" => "float2",
            "vec3" => "float3",
            "vec4" => "float4",
            "float2" or "float3" or "float4" => value.Trim().ToLowerInvariant(),
            _ => value.Trim()
        };
    }

    private static string DefaultValueForType(string type)
    {
        var normalizedType = SanitizeType(type).ToLowerInvariant();
        return normalizedType switch
        {
            "bool" => "false",
            "int" => "0",
            "float2" or "half2" or "fixed2" => "float2(0.0, 0.0)",
            "float3" or "half3" or "fixed3" => "float3(0.0, 0.0, 0.0)",
            "float4" or "half4" or "fixed4" => "float4(0.0, 0.0, 0.0, 0.0)",
            _ => "0.0"
        };
    }

    private static string NormalizeValueLiteral(string type, string value)
    {
        var normalizedType = SanitizeType(type);
        var lower = normalizedType.ToLowerInvariant();
        if (lower is "bool" or "int" or "float" or "half" or "fixed")
        {
            return value;
        }

        var constructor = VectorConstructorRegex.Match(value);
        if (constructor.Success)
        {
            var constructorSize = constructor.Groups["size"].Value;
            if (lower.EndsWith(constructorSize, StringComparison.Ordinal))
            {
                return normalizedType + value[(constructor.Length - 1)..];
            }
        }

        if (value.Contains(','))
        {
            return $"{normalizedType}({value})";
        }

        return value;
    }

    private static int CountLines(string value)
    {
        if (value.Length == 0)
        {
            return 0;
        }

        var count = 0;
        foreach (var ch in value)
        {
            if (ch == '\n')
            {
                count++;
            }
        }

        return count;
    }

    private static string StripLineComment(string value)
    {
        var comment = value.IndexOf("//", StringComparison.Ordinal);
        return comment >= 0 ? value[..comment] : value;
    }

    private static string StripBlockAndLineComments(string value)
        => Regex.Replace(value, @"/\*.*?\*/|//[^\r\n]*", string.Empty, RegexOptions.Singleline);

    private static void AddMatches(
        List<ShaderSourceDiagnostic> diagnostics,
        Regex expression,
        string line,
        int lineNumber,
        string severity,
        string message)
    {
        foreach (Match match in expression.Matches(line))
        {
            AddDiagnostic(diagnostics, severity, message, lineNumber, match.Index + 1);
        }
    }

    private static void AddDiagnostic(
        List<ShaderSourceDiagnostic> diagnostics,
        string severity,
        string message,
        int line,
        int column)
    {
        if (diagnostics.Any(item => item.Line == line && item.Column == column && item.Message == message))
        {
            return;
        }

        diagnostics.Add(new ShaderSourceDiagnostic(severity, message, line, column));
    }
}

internal enum ShaderModel
{
    Ps20,
    Ps30,
    HigherThanPs30
}

internal sealed record ShaderParameterDefinition(string Name, string Type, string Value);

internal sealed record ShaderDocument(
    string Source,
    string EntryPoint,
    ShaderModel ShaderModel,
    string? DeclaredShaderProfile,
    IReadOnlyList<ShaderParameterDefinition> Parameters);

internal sealed record ShaderCompileDocument(string Source, int BodyStartLine, int BodyLineCount)
{
    public ShaderSourceLocation MapCompilerLocation(int compilerLine, int? compilerColumn)
    {
        if (compilerLine < BodyStartLine || compilerLine >= BodyStartLine + BodyLineCount)
        {
            return new ShaderSourceLocation(null, null, true);
        }

        // D3DCompile reports positions in the generated document. Before the
        // editable body we add only whole lines, so the column is unchanged.
        // The only exception is the generated entry-point marker appended at
        // the end of imported FX code; it is not part of the editable body.
        return new ShaderSourceLocation(compilerLine - BodyStartLine + 1, compilerColumn, false);
    }
}

internal sealed record ShaderSourceDiagnostic(string Severity, string Message, int Line, int Column);

internal sealed record ShaderSourceLocation(int? Line, int? Column, bool IsGeneratedCode);
