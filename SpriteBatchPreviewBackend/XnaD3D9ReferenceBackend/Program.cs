using System;
using System.IO;

namespace XnaD3D9ReferenceBackend;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("XNA/D3D9 reference backend starting.");
            using (var renderer = new DirectReferenceRenderer(args))
                renderer.Run();
            Console.WriteLine("XNA/D3D9 reference backend completed.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            var path = Path.Combine(Path.GetTempPath(), "XnaD3D9ReferenceBackend.error.log");
            File.WriteAllText(path, ex.ToString());
            Environment.ExitCode = 1;
        }
    }
}
