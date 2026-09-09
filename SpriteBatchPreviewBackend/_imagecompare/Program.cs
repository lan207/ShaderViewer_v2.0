using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

if (args.Length != 2) throw new ArgumentException("usage: reference.png candidate.png");
using var expected = new Bitmap(args[0]);
using var actual = new Bitmap(args[1]);
if (expected.Size != actual.Size) throw new InvalidDataException("image sizes differ");
var a = Read(expected);
var b = Read(actual);
double absolute = 0, squared = 0;
var maximum = 0;
long differentPixels = 0;
for (var p = 0; p < a.Length; p += 4)
{
    var changed = false;
    for (var c = 0; c < 4; c++)
    {
        var difference = Math.Abs(a[p + c] - b[p + c]);
        absolute += difference;
        squared += difference * difference;
        maximum = Math.Max(maximum, difference);
        changed |= difference != 0;
    }
    if (changed) differentPixels++;
}
var samples = a.Length;
var pixels = samples / 4;
Console.WriteLine($"Size={expected.Width}x{expected.Height}");
Console.WriteLine($"MAE={absolute / samples:F6}");
Console.WriteLine($"RMSE={Math.Sqrt(squared / samples):F6}");
Console.WriteLine($"MaxError={maximum}");
Console.WriteLine($"DifferentPixels={differentPixels}/{pixels} ({differentPixels * 100.0 / pixels:F4}%)");

static byte[] Read(Bitmap source)
{
    using var copy = source.Clone(new Rectangle(Point.Empty, source.Size), PixelFormat.Format32bppArgb);
    var data = copy.LockBits(new Rectangle(Point.Empty, copy.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    try
    {
        var result = new byte[data.Stride * data.Height];
        Marshal.Copy(data.Scan0, result, 0, result.Length);
        return result;
    }
    finally { copy.UnlockBits(data); }
}
