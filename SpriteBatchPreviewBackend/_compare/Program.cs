using ShaderViewer.Rendering;
var input = args.Length > 0
    ? Path.GetFullPath(args[0])
    : @"D:\Other\XnbFxDecompiler_v2.0\pseudo-fx-split\PixelShader.ForceField0.fx";
var import = ShaderSourceTools.Import(File.ReadAllText(input));
var source = ShaderSourceTools.BuildMonoGameSpriteEffectSource(import.Source, import.Parameters, out _);
var output = args.Length > 1
    ? Path.GetFullPath(args[1])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "reference-output", "decompiled"));
Directory.CreateDirectory(output);
File.WriteAllText(Path.Combine(output, Path.GetFileNameWithoutExtension(input) + ".native.fx"), source);
