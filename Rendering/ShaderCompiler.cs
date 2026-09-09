using System.Runtime.InteropServices;
using System.Text;

namespace ShaderViewer.Rendering;

internal static class ShaderCompiler
{
    private const uint D3DCOMPILE_ENABLE_BACKWARDS_COMPATIBILITY = 1u << 14;
    private const uint D3DCOMPILE_OPTIMIZATION_LEVEL3 = 1u << 15;

    public static byte[] CompilePixelShader(string source, string entryPoint)
        => CompilePixelShader(source, entryPoint, "ps_2_0");

    public static byte[] CompilePixelShader(string source, string entryPoint, string targetProfile, string sourceName = "ShaderViewer.fx")
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        IntPtr codePtr = IntPtr.Zero;
        IntPtr errorPtr = IntPtr.Zero;
        var hr = D3DCompile(
            bytes,
            (nuint)bytes.Length,
            sourceName,
            IntPtr.Zero,
            IntPtr.Zero,
            entryPoint,
            targetProfile,
            D3DCOMPILE_ENABLE_BACKWARDS_COMPATIBILITY | D3DCOMPILE_OPTIMIZATION_LEVEL3,
            0,
            out codePtr,
            out errorPtr);

        try
        {
            if (hr < 0)
            {
                var message = errorPtr == IntPtr.Zero
                    ? $"Shader compile failed: 0x{hr:X8}"
                    : Marshal.PtrToStringAnsi(GetBlobPointer(errorPtr))?.Trim() ?? $"Shader compile failed: 0x{hr:X8}";
                throw new InvalidOperationException(message);
            }

            if (codePtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Shader compile returned a null bytecode blob.");
            }

            var code = (ID3DBlob)Marshal.GetObjectForIUnknown(codePtr);
            var size = checked((int)code.GetBufferSize());
            var result = new byte[size];
            Marshal.Copy(GetBlobPointer(codePtr), result, 0, size);
            return result;
        }
        finally
        {
            if (errorPtr != IntPtr.Zero)
            {
                Marshal.Release(errorPtr);
            }
            if (codePtr != IntPtr.Zero)
            {
                Marshal.Release(codePtr);
            }
        }
    }

    [DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi, ExactSpelling = true, PreserveSig = true)]
    private static extern int D3DCompile(
        [In] byte[] pSrcData,
        nuint SrcDataSize,
        string pSourceName,
        IntPtr pDefines,
        IntPtr pInclude,
        [MarshalAs(UnmanagedType.LPStr)] string pEntrypoint,
        [MarshalAs(UnmanagedType.LPStr)] string pTarget,
        uint Flags1,
        uint Flags2,
        out IntPtr ppCode,
        out IntPtr ppErrorMsgs);

    private static IntPtr GetBlobPointer(IntPtr blob)
    {
        var com = (ID3DBlob)Marshal.GetObjectForIUnknown(blob);
        return com.GetBufferPointer();
    }

    [ComImport]
    [Guid("8BA5FB08-5195-40E2-AC58-0D989C3A0102")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ID3DBlob
    {
        [PreserveSig]
        IntPtr GetBufferPointer();

        [PreserveSig]
        nuint GetBufferSize();
    }
}
