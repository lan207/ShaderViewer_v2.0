using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;

if (args.Length != 3)
    throw new ArgumentException("usage: <XnbFxDecompiler.dll> <PixelShader.xnb> <reference-bytecode.bin>");

var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[0]));
var readerType = assembly.GetType("XnbFxDecompiler.XnbReader", throwOnError: true)!;
var read = readerType.GetMethod("Read", BindingFlags.Public | BindingFlags.Static)!;
var result = read.Invoke(null, new object[] { Path.GetFullPath(args[1]) })!;
var effect = result.GetType().GetProperty("Effect")!.GetValue(result)!;
var shaders = (System.Collections.IEnumerable)effect.GetType().GetProperty("Shaders")!.GetValue(effect)!;
var content = (byte[])result.GetType().GetProperty("Content")!.GetValue(result)!;
var rootDataOffset = (int)effect.GetType().GetProperty("RootDataOffset")!.GetValue(effect)!;
var rootDataLength = (int)effect.GetType().GetProperty("RootDataLength")!.GetValue(effect)!;
var effectData = content.AsSpan(rootDataOffset, rootDataLength).ToArray();
var reference = File.ReadAllBytes(args[2]);
var referenceHash = Convert.ToHexString(SHA256.HashData(reference));

Console.WriteLine($"Reference length={reference.Length} SHA256={referenceHash}");
foreach (var shader in shaders)
{
    var type = shader!.GetType();
    var bytes = (byte[])type.GetProperty("Bytecode")!.GetValue(shader)!;
    var hash = Convert.ToHexString(SHA256.HashData(bytes));
    if (bytes.Length >= 8 && BitConverter.ToUInt32(bytes, 4) == 0x0030fffe)
        Console.WriteLine($"CTAB30 index={type.GetProperty("Index")!.GetValue(shader)} stage={type.GetProperty("Stage")!.GetValue(shader)} offset={type.GetProperty("Offset")!.GetValue(shader)} length={bytes.Length} SHA256={hash}");
    if (bytes.Length == reference.Length)
        Console.WriteLine($"SAME-LENGTH index={type.GetProperty("Index")!.GetValue(shader)} stage={type.GetProperty("Stage")!.GetValue(shader)} offset={type.GetProperty("Offset")!.GetValue(shader)} SHA256={hash}");
    if (bytes.AsSpan().SequenceEqual(reference))
    {
        Console.WriteLine(
            $"MATCH index={type.GetProperty("Index")!.GetValue(shader)} " +
            $"stage={type.GetProperty("Stage")!.GetValue(shader)} " +
            $"offset={type.GetProperty("Offset")!.GetValue(shader)} " +
            $"length={bytes.Length} SHA256={hash}");
    }
}

DumpD3dxPassResources(effectData);

static void DumpD3dxPassResources(byte[] data)
{
    static uint U32(byte[] bytes, int offset) => BitConverter.ToUInt32(bytes, offset);
    static string Name(byte[] bytes, int baseOffset, uint relativeOffset)
    {
        if (relativeOffset > int.MaxValue || baseOffset + (int)relativeOffset + 4 > bytes.Length) return "<bad-name>";
        var offset = baseOffset + (int)relativeOffset;
        var length = (int)U32(bytes, offset);
        if (length <= 0 || offset + 4 + length > bytes.Length) return "<unnamed>";
        return System.Text.Encoding.ASCII.GetString(bytes, offset + 4, length).TrimEnd('\0');
    }

    if (data.Length < 24 || U32(data, 0) != 0xBCF00BCF) return;
    Console.WriteLine("Header words=" + string.Join(",", Enumerable.Range(0, 16).Select(i => $"0x{U32(data, i * 4):X8}")));
    Console.WriteLine("Pre-table nonzero=" + string.Join(",", Enumerable.Range(2, ((int)U32(data, 4) + 8) / 4 - 2).Where(i => U32(data, i * 4) != 0).Select(i => $"{i * 4:X}:0x{U32(data, i * 4):X8}")));
    var d3dxOffset = checked((int)U32(data, 4));
    if (U32(data, d3dxOffset) != 0xFEFF0901) throw new InvalidDataException("Embedded D3DX9 effect signature not found.");
    var baseOffset = d3dxOffset + 8;
    var cursor = checked(baseOffset + (int)U32(data, d3dxOffset + 4));
    Console.WriteLine("Table words=" + string.Join(",", Enumerable.Range(0, 48).Select(i => $"0x{U32(data, cursor + i * 4):X8}")));
    var parameterCount = checked((int)U32(data, cursor)); cursor += 4;
    var techniqueCount = checked((int)U32(data, cursor)); cursor += 4;
    var shaderCount = U32(data, cursor); cursor += 4;
    var objectCount = U32(data, cursor); cursor += 4;
    Console.WriteLine($"D3DX metadata base={baseOffset} table={cursor - 16} parameters={parameterCount} techniques={techniqueCount} shaderSlots={shaderCount} objects={objectCount}");

    for (var p = 0; p < parameterCount; p++)
    {
        cursor += 12;
        var annotationCount = checked((int)U32(data, cursor)); cursor += 4;
        cursor += checked(annotationCount * 8);
    }

    var states = new Dictionary<(int Technique, int Pass, int State), (string TechniqueName, string PassName, uint Operation, uint Type, uint Class, uint ObjectId)>();
    for (var t = 0; t < techniqueCount; t++)
    {
        var techniqueName = Name(data, baseOffset, U32(data, cursor)); cursor += 4;
        var annotationCount = checked((int)U32(data, cursor)); cursor += 4;
        var passCount = checked((int)U32(data, cursor)); cursor += 4;
        cursor += checked(annotationCount * 8);
        for (var p = 0; p < passCount; p++)
        {
            var passName = Name(data, baseOffset, U32(data, cursor)); cursor += 4;
            var passAnnotationCount = checked((int)U32(data, cursor)); cursor += 4;
            var stateCount = checked((int)U32(data, cursor)); cursor += 4;
            cursor += checked(passAnnotationCount * 8);
            for (var s = 0; s < stateCount; s++)
            {
                var operation = U32(data, cursor); cursor += 4;
                _ = U32(data, cursor); cursor += 4; // state index
                var typeOffset = U32(data, cursor); cursor += 4;
                var valueOffset = U32(data, cursor); cursor += 4;
                var typePos = checked(baseOffset + (int)typeOffset);
                var valuePos = checked(baseOffset + (int)valueOffset);
                var type = U32(data, typePos);
                var @class = U32(data, typePos + 4);
                var objectId = U32(data, valuePos);
                states[(t, p, s)] = (techniqueName, passName, operation, type, @class, objectId);
                if (passName == "ForceField")
                    Console.WriteLine($"ForceField state={s} operation=0x{operation:X} type={type} class={@class} objectId={objectId}");
            }
        }
    }

    var stringCount = checked((int)U32(data, cursor)); cursor += 4;
    var resourceCount = checked((int)U32(data, cursor)); cursor += 4;
    Console.WriteLine($"tablesEnd={cursor - 8} strings={stringCount} resources={resourceCount}");
    for (var i = 0; i < stringCount; i++)
    {
        var id = U32(data, cursor); cursor += 4;
        var size = checked((int)U32(data, cursor)); cursor += 4;
        cursor += checked((size + 3) & ~3);
        if (size > 0) Console.WriteLine($"stringObject id={id} size={size}");
    }

    for (var i = 0; i < resourceCount; i++)
    {
        var technique = U32(data, cursor); cursor += 4;
        var index = U32(data, cursor); cursor += 4;
        var element = U32(data, cursor); cursor += 4;
        var stateIndex = U32(data, cursor); cursor += 4;
        var usage = U32(data, cursor); cursor += 4;
        var size = checked((int)U32(data, cursor)); cursor += 4;
        var blobOffset = cursor;
        cursor += checked((size + 3) & ~3);
        if (technique != uint.MaxValue && states.TryGetValue(((int)technique, (int)index, (int)stateIndex), out var state) && state.PassName == "ForceField")
        {
            var prefix = Convert.ToHexString(data.AsSpan(blobOffset, Math.Min(size, 32)));
            Console.WriteLine($"ForceField resource={i} usage={usage} element={element} objectId={state.ObjectId} size={size} offset={blobOffset} prefix={prefix}");
        }
    }
}
