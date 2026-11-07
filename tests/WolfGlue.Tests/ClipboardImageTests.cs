using WolfGlue.Native;
using WolfGlue.Platform.Windows;

namespace WolfGlue.Tests;

public class ClipboardImageTests
{
    private static byte[] Sample(int w, int h)
    {
        var px = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                px[i] = (byte)(x * 40); px[i + 1] = (byte)(y * 60); px[i + 2] = 200; px[i + 3] = (byte)(x == 0 ? 128 : 255);
            }
        return px;
    }

    [Fact]
    public void PngRoundTripsThroughGdiPlus()
    {
        if (!OperatingSystem.IsWindows()) return;
        var px = Sample(5, 3);
        var png = PngEncoder.FromBgra(px, 5, 3, 20);
        Assert.True(PngEncoder.IsPng(png));
        Gdip.Start();
        var img = Gdip.Load(png);
        Assert.NotEqual(0, img);
        var (w, h, back) = Gdip.Pixels(img);
        Gdip.Dispose(img);
        Assert.Equal((5, 3), (w, h));
        Assert.Equal(px, back);
    }

    [Fact]
    public void Dib24BitBecomesOpaquePng()
    {
        if (!OperatingSystem.IsWindows()) return;
        // 2x2, 24 bits, de abajo arriba, filas alineadas a 4 bytes (6 + 2 de relleno).
        var dib = new byte[40 + 16];
        BitConverter.TryWriteBytes(dib.AsSpan(0), 40);
        BitConverter.TryWriteBytes(dib.AsSpan(4), 2);
        BitConverter.TryWriteBytes(dib.AsSpan(8), 2);
        BitConverter.TryWriteBytes(dib.AsSpan(12), (ushort)1);
        BitConverter.TryWriteBytes(dib.AsSpan(14), (ushort)24);
        byte[] bottom = [255, 0, 0, 0, 255, 0, 0, 0], top = [0, 0, 255, 255, 255, 255, 0, 0];
        bottom.CopyTo(dib, 40);
        top.CopyTo(dib, 48);
        var png = WinServices.DibToPng(dib);
        Assert.NotNull(png);
        Gdip.Start();
        var img = Gdip.Load(png!);
        var (w, h, px) = Gdip.Pixels(img);
        Gdip.Dispose(img);
        Assert.Equal((2, 2), (w, h));
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, px[0..4]);     // arriba izquierda: rojo
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, px[8..12]);    // abajo izquierda: azul
    }
}