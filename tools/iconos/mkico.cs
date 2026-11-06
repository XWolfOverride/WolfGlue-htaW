#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#:property PublishAot=false
// Convierte una imagen en icono de Windows (aplicacion de un solo fichero de .NET 10).
//
//   dotnet run tools/iconos/mkico.cs -- <entrada.png> <salida.ico> <salida-256.png> [recorte] [radio]
//
// recorte: fraccion del lado que se quita en cada borde (0..0.4, por defecto 0), o un cuadro en pixeles del
//          original con la forma x,y,lado (para objetos que no estan centrados).
// radio:   radio de las esquinas redondeadas, fraccion del lado (0..0.5, por defecto 0.2).
// Escribe un ICO de 16 a 256 px (DIB de 32 bits con alfa hasta 128 y PNG en 256) y una
// vista previa PNG de 256 px. Ver DOC/ICONOS.md.
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

var input = args[0];
var icoPath = args[1];
var pngPath = args[2];
var cropArg = args.Length > 3 ? args[3] : "0";
var radius = args.Length > 4 ? double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : 0.2;
int[] sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];

using var src = new Bitmap(input);
Rectangle rect;
if (cropArg.Contains(','))
{
    var p = cropArg.Split(',').Select(int.Parse).ToArray();
    rect = new Rectangle(p[0], p[1], p[2], p[2]);
}
else
{
    var m = (int)(Math.Min(src.Width, src.Height) * double.Parse(cropArg, System.Globalization.CultureInfo.InvariantCulture));
    rect = new Rectangle(m, m, src.Width - 2 * m, src.Height - 2 * m);
}

// Maestro de 1024 con esquinas redondeadas (suavizadas).
const int Master = 1024;
using var master = new Bitmap(Master, Master, PixelFormat.Format32bppArgb);
using (var g = Graphics.FromImage(master))
{
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.Clear(Color.Transparent);
    using var path = RoundRect(new RectangleF(0, 0, Master, Master), (float)(Master * radius));
    using var brush = new TextureBrush(Resize(src, rect, Master), WrapMode.Clamp);
    g.FillPath(brush, path);
}

var frames = sizes.Select(s => (s, bmp: Downscale(master, s))).ToList();
frames[^1].bmp.Save(pngPath, ImageFormat.Png);

using (var fs = File.Create(icoPath))
using (var w = new BinaryWriter(fs))
{
    // 256 en PNG (lo habitual; lo usa el Explorador) y el resto en DIB con alfa.
    var images = frames.Select(f => f.s >= 256 ? Png(f.bmp) : Dib(f.bmp)).ToList();
    w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)frames.Count);
    var offset = 6 + 16 * frames.Count;
    for (var i = 0; i < frames.Count; i++)
    {
        var s = frames[i].s;
        w.Write((byte)(s >= 256 ? 0 : s)); w.Write((byte)(s >= 256 ? 0 : s));
        w.Write((byte)0); w.Write((byte)0);
        w.Write((ushort)1); w.Write((ushort)32);
        w.Write(images[i].Length); w.Write(offset);
        offset += images[i].Length;
    }
    foreach (var img in images)
        w.Write(img);
}
Console.WriteLine($"{icoPath}: {string.Join(", ", sizes)}");

static Bitmap Resize(Bitmap src, Rectangle from, int size)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    using var attr = new ImageAttributes();
    attr.SetWrapMode(WrapMode.TileFlipXY);
    g.DrawImage(src, new Rectangle(0, 0, size, size), from.X, from.Y, from.Width, from.Height, GraphicsUnit.Pixel, attr);
    return bmp;
}

// Reducción por mitades para que los tamaños pequeños no salgan con dientes.
static Bitmap Downscale(Bitmap master, int size)
{
    var current = master;
    while (current.Width / 2 >= size)
    {
        var next = Resize(current, new Rectangle(0, 0, current.Width, current.Height), current.Width / 2);
        if (current != master) current.Dispose();
        current = next;
    }
    if (current.Width == size) return current == master ? new Bitmap(master) : current;
    var result = Resize(current, new Rectangle(0, 0, current.Width, current.Height), size);
    if (current != master) current.Dispose();
    return result;
}

static GraphicsPath RoundRect(RectangleF r, float rad)
{
    var p = new GraphicsPath();
    var d = rad * 2;
    p.AddArc(r.X, r.Y, d, d, 180, 90);
    p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
    p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
    p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
    p.CloseFigure();
    return p;
}

static byte[] Png(Bitmap bmp)
{
    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    return ms.ToArray();
}

// BITMAPINFOHEADER + BGRA de abajo arriba + máscara AND (a cero: manda el alfa).
static byte[] Dib(Bitmap bmp)
{
    int s = bmp.Width, maskStride = (s + 31) / 32 * 4;
    using var ms = new MemoryStream();
    using var w = new BinaryWriter(ms);
    w.Write(40); w.Write(s); w.Write(s * 2); w.Write((ushort)1); w.Write((ushort)32);
    w.Write(0); w.Write(s * s * 4 + maskStride * s); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
    for (var y = s - 1; y >= 0; y--)
        for (var x = 0; x < s; x++)
        {
            var c = bmp.GetPixel(x, y);
            w.Write(c.B); w.Write(c.G); w.Write(c.R); w.Write(c.A);
        }
    w.Write(new byte[maskStride * s]);
    return ms.ToArray();
}
