using System.Security.Cryptography;
using System.Text;
using HtaW.Package;
using WolfGlue;
using WolfGlue.Content;

namespace HtaW.Tests;

public class BundleTests
{
    private static GlueRequest Get(string path) => new("GET", path, "", ReadOnlyMemory<byte>.Empty);

    /// <summary>Iconos del repositorio (src/htaW/icon).</summary>
    private static byte[] Asset(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return File.ReadAllBytes(Path.Combine(dir!.FullName, "src", "htaW", "icon", name));
    }

    private static string Html(string manifest) =>
        $"""<!doctype html><html><head><script type="application/htaw+json">{manifest}</script></head><body>hola</body></html>""";

    // ------------------------------------------------------------- formato incrustado
    [Fact]
    public void TrailerRoundTrip()
    {
        using var ms = new MemoryStream();
        ms.Write("MZ ejecutable falso"u8);
        EmbeddedApp.Append(ms, "contenido"u8, EmbeddedKind.Html);
        var app = EmbeddedApp.Find(ms);
        Assert.Equal(new EmbeddedApp(19, 9, EmbeddedKind.Html), app);
    }

    [Fact]
    public void NoTrailerOrDamagedTrailer()
    {
        Assert.Null(EmbeddedApp.Find(new MemoryStream("MZ sin app"u8.ToArray())));
        Assert.Null(EmbeddedApp.Find(new MemoryStream()));
        using var ms = new MemoryStream();
        ms.Write("MZ"u8);
        EmbeddedApp.Append(ms, "x"u8, EmbeddedKind.Zip);
        var bytes = ms.ToArray();
        bytes[^12] = 9; // version desconocida
        Assert.Null(EmbeddedApp.Find(new MemoryStream(bytes)));
        Assert.Null(EmbeddedApp.Find(Path.Combine(Path.GetTempPath(), "no-existe-" + Guid.NewGuid())));
    }

    [Fact]
    public async Task OpensEmbeddedHtml()
    {
        using var dir = new TempDir();
        dir.File("img/a.png", "png");
        var exe = Path.Combine(dir.Path, "app.exe");
        var html = Encoding.UTF8.GetBytes(Html("""{"id":"com.emb","name":"Emb"}"""));
        using (var f = File.Create(exe))
        {
            f.Write("MZ base"u8);
            EmbeddedApp.Append(f, html, EmbeddedKind.Html);
        }
        var app = EmbeddedApp.Find(exe)!;
        using var pkg = HtawPackage.OpenEmbedded(exe, app);
        Assert.True(pkg.Embedded);
        Assert.Equal(PackageKind.Html, pkg.Kind);
        Assert.Equal("com.emb", pkg.Id);
        Assert.Equal("index.html", pkg.Entry);
        Assert.Equal(dir.Path, pkg.Directory);
        Assert.Equal(exe, pkg.SourcePath);
        Assert.Contains("hola", Encoding.UTF8.GetString((await pkg.Content.GetAsync(Get("index.html")))!.Body.Span));
        Assert.NotNull(await pkg.Content.GetAsync(Get("img/a.png")));
        Assert.Equal(SHA256.HashData(html), pkg.ComputeHash());
    }

    [Fact]
    public async Task BundleFolderWithNonPeBaseAndOpenIt()
    {
        using var dir = new TempDir();
        var src = Path.Combine(dir.Path, "notas");
        dir.File("notas/htaw.json", """{"id":"com.notas","entry":"index.html","icon":"img/icono.png"}""");
        dir.File("notas/index.html", "<h1>notas</h1>");
        File.WriteAllBytes(Path.Combine(src, "img", "icono.png").Also(p => Directory.CreateDirectory(Path.GetDirectoryName(p)!)), Asset("icon.png"));
        var baseExe = dir.File("base-htaw", "ELF base de prueba");

        var r = Bundler.Bundle(src, baseExe: baseExe);
        Assert.Equal(Path.Combine(dir.Path, "notas.bin"), r.Output); // la base no es PE y "notas" es la carpeta
        Assert.False(r.IconSet);
        Assert.StartsWith("ELF base de prueba", File.ReadAllText(r.Output, Encoding.Latin1));

        // Rehacerlo usando como base el propio resultado: se quita la app anterior, no se acumula.
        var again = Bundler.Bundle(src, Path.Combine(dir.Path, "otra"), r.Output);
        Assert.Equal(r.Size, again.Size);

        var app = EmbeddedApp.Find(r.Output)!;
        Assert.Equal(EmbeddedKind.Zip, app.Kind);
        Assert.Equal("ELF base de prueba".Length, app.Offset);
        using var pkg = HtawPackage.OpenEmbedded(r.Output, app);
        Assert.Equal(PackageKind.Zip, pkg.Kind);
        Assert.Null(pkg.Directory);
        Assert.Equal(Asset("icon.png"), pkg.Icon);
        Assert.Contains("notas", Encoding.UTF8.GetString((await pkg.Content.GetAsync(Get("index.html")))!.Body.Span));
    }

    [Fact]
    public void BundleRejectsSameFileAndMissingBase()
    {
        using var dir = new TempDir();
        var f = dir.File("app.htaw", Html("{}"));
        Assert.Throws<HtawFormatException>(() => Bundler.Bundle(f, f, dir.File("base", "x")));
        Assert.Throws<HtawFormatException>(() => Bundler.Bundle(f, baseExe: Path.Combine(dir.Path, "no-existe")));
    }

    [Fact]
    public void BundleWithWindowsBaseSetsIcon()
    {
        if (!OperatingSystem.IsWindows()) return;
        // Base PE real: el lanzador de htaw compilado junto a las pruebas (lleva el icono de htaW).
        var host = Path.Combine(AppContext.BaseDirectory, "htaw.exe");
        Assert.True(File.Exists(host), host);
        using var dir = new TempDir();
        var ico = Convert.ToBase64String(Asset("icon.ico"));
        var f = dir.File("rojo.htaw", Html($$"""{"id":"com.rojo","icon":"data:image/x-icon;base64,{{ico}}"}"""));

        var r = Bundler.Bundle(f, baseExe: host);
        Assert.Equal(Path.Combine(dir.Path, "rojo.exe"), r.Output);
        Assert.True(r.IconSet);
        var (groups, icons) = ExeIcon.List(r.Output);
        Assert.Single(groups);
        Assert.Equal(GlueIcon.IcoImages(Asset("icon.ico")).Length, icons.Count);
        Assert.NotNull(EmbeddedApp.Find(r.Output));

        // Con un PNG: una sola imagen.
        var g = dir.File("verde.htaw", Html($$"""{"icon":"data:image/png;base64,{{Convert.ToBase64String(Asset("icon.png"))}}"}"""));
        var r2 = Bundler.Bundle(g, baseExe: r.Output);
        Assert.Single(ExeIcon.List(r2.Output).icons);
    }

    // ------------------------------------------------------------- icono del manifiesto
    [Fact]
    public void IconFromDataUrlAndRelativeFile()
    {
        using var dir = new TempDir();
        var png = Convert.ToBase64String(Asset("icon.png"));
        using (var p = HtawPackage.Open(dir.File("a.htaw", Html($$"""{"icon":"data:image/png;base64,{{png}}"}"""))))
        {
            Assert.Equal(Asset("icon.png"), p.Icon);
            Assert.Null(p.IconProblem);
        }
        File.WriteAllBytes(Path.Combine(dir.Path, "i.ico"), Asset("icon.ico"));
        using (var p = HtawPackage.Open(dir.File("b.htaw", Html("""{"icon":"i.ico"}"""))))
            Assert.Equal(Asset("icon.ico"), p.Icon);
    }

    [Fact]
    public void UnusableIconIsWarningOnOpenAndErrorOnPack()
    {
        using var dir = new TempDir();
        using (var p = HtawPackage.Open(dir.File("a.htaw", Html("""{"icon":"falta.png"}"""))))
        {
            Assert.Null(p.Icon);
            Assert.NotNull(p.IconProblem);
            Assert.Contains(p.Warnings, w => w.Contains("falta.png"));
        }
        dir.File("b.svg", "<svg/>");
        using (var p = HtawPackage.Open(dir.File("b.htaw", Html("""{"icon":"b.svg"}"""))))
            Assert.NotNull(p.IconProblem);

        var src = Path.Combine(dir.Path, "app");
        dir.File("app/htaw.json", """{"entry":"index.html","icon":"no.png"}""");
        dir.File("app/index.html", "x");
        Assert.Throws<HtawFormatException>(() => Packer.Pack(src));
        Assert.Throws<HtawFormatException>(() => Bundler.Bundle(src, baseExe: dir.File("base", "x")));
    }

    [Theory]
    [InlineData("../fuera.png")]
    [InlineData("data:image/png;base64,@@@")]
    public void InvalidIconSpecIsFormatError(string icon)
    {
        using var dir = new TempDir();
        var f = dir.File("a.htaw", Html($$"""{"icon":"{{icon}}"}"""));
        Assert.Throws<HtawFormatException>(() => HtawPackage.Open(f));
    }

    [Fact]
    public void ParsesBundleCommand()
    {
        var c = CliCommand.Parse(["bundle", "app", "-o", "app.exe", "--base", "htaw.exe"]);
        Assert.Equal(CliAction.Bundle, c.Action);
        Assert.Equal(("app", "app.exe", "htaw.exe"), (c.Path, c.Output, c.Base));
        Assert.Equal(CliAction.Error, CliCommand.Parse(["bundle"]).Action);
        Assert.Equal(CliAction.Error, CliCommand.Parse(["bundle", "app", "--base"]).Action);
        Assert.Equal(CliAction.Error, CliCommand.Parse(["pack", "app", "--base", "x"]).Action);
    }
}

internal static class TestExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
