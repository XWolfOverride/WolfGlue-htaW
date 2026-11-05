using System.Text;
using HtaW.Api;
using HtaW.Package;
using WolfGlue.Content;

namespace HtaW.Tests;

public class PackageTests
{
    private static GlueRequest Get(string path) => new("GET", path, "", ReadOnlyMemory<byte>.Empty);

    private const string Manifest = """{ "id": "com.test.app", "name": "Test", "entry": "index.html", "permissions": ["fs"] }""";

    [Fact]
    public async Task HtmlWithManifest()
    {
        using var dir = new TempDir();
        var f = dir.File("app.htaw", """<!doctype html><html><head><script type="application/htaw+json">{"id":"com.x","name":"X"}</script></head><body>hola</body></html>""");
        dir.File("img/a.png", "png");
        using var pkg = HtawPackage.Open(f);
        Assert.Equal(PackageKind.Html, pkg.Kind);
        Assert.Equal("com.x", pkg.Id);
        Assert.Equal("com.x.htaw.local", pkg.Host);
        Assert.Equal("app.htaw", pkg.Entry);
        Assert.Equal(dir.Path, pkg.Directory);
        Assert.Contains("hola", Encoding.UTF8.GetString((await pkg.Content.GetAsync(Get("app.htaw")))!.Body.Span));
        Assert.NotNull(await pkg.Content.GetAsync(Get("")));
        Assert.NotNull(await pkg.Content.GetAsync(Get("img/a.png")));
        Assert.Null(await pkg.Content.GetAsync(Get("../fuera.txt")));
    }

    [Fact]
    public void HtmlWithoutManifestGetsStableDerivedId()
    {
        using var dir = new TempDir();
        var a = dir.File("a.htaw", "<p>a</p>");
        var b = dir.File("b.htaw", "<p>b</p>");
        using var pa = HtawPackage.Open(a);
        using var pa2 = HtawPackage.Open(a);
        using var pb = HtawPackage.Open(b);
        Assert.StartsWith("local-", pa.Id);
        Assert.Equal(pa.Id, pa2.Id);
        Assert.NotEqual(pa.Id, pb.Id);
        Assert.Contains(pa.Warnings, w => w.Contains("application/htaw+json"));
    }

    [Fact]
    public async Task PackThenOpenZip()
    {
        using var dir = new TempDir();
        var src = Path.Combine(dir.Path, "app");
        dir.File("app/htaw.json", Manifest);
        dir.File("app/index.html", "<h1>zip</h1>");
        dir.File("app/js/app.js", "1");
        dir.File("app/.git/config", "no");
        dir.File("app/node_modules/x/y.js", "no");
        dir.File("app/viejo.htaw", "no");

        var (output, count) = Packer.Pack(src);
        Assert.Equal(src + ".htaw", output);
        Assert.Equal(3, count);
        Assert.True(HtawPackage.IsZip(output));

        using var pkg = HtawPackage.Open(output);
        Assert.Equal(PackageKind.Zip, pkg.Kind);
        Assert.Equal("com.test.app", pkg.Id);
        Assert.Null(pkg.Directory);
        Assert.True(pkg.Permissions.Has("fs"));
        Assert.Contains("zip", Encoding.UTF8.GetString((await pkg.Content.GetAsync(Get("index.html")))!.Body.Span));
        Assert.NotNull(await pkg.Content.GetAsync(Get("js/app.js")));
        Assert.Null(await pkg.Content.GetAsync(Get(".git/config")));
    }

    [Fact]
    public void ZipErrors()
    {
        using var dir = new TempDir();
        dir.File("a/htaw.json", """{ "entry": "falta.html" }""");
        Assert.Throws<HtawFormatException>(() => Packer.Pack(Path.Combine(dir.Path, "a")));
        dir.File("b/index.html", "x");
        Assert.Throws<HtawFormatException>(() => Packer.Pack(Path.Combine(dir.Path, "b")));
        dir.File("c/htaw.json", """{ "id": "c" }""");
        Assert.Throws<HtawFormatException>(() => Packer.Pack(Path.Combine(dir.Path, "c"))); // sin entry
    }

    [Fact]
    public void FolderNeedsDevFlag()
    {
        using var dir = new TempDir();
        dir.File("htaw.json", Manifest);
        dir.File("index.html", "x");
        Assert.Throws<HtawFormatException>(() => HtawPackage.Open(dir.Path));
        using var pkg = HtawPackage.Open(dir.Path, allowFolder: true);
        Assert.Equal(PackageKind.Folder, pkg.Kind);
    }
}

public class InjectionTests
{
    private static string Run(string html) =>
        Encoding.UTF8.GetString(HtmlInjector.Inject(Encoding.UTF8.GetBytes(html), "<X>"));

    [Theory]
    [InlineData("<!doctype html><html><head><title>t</title></head>", "<!doctype html><html><head><X><title>t</title></head>")]
    [InlineData("<!DOCTYPE html><HTML lang=es><BODY>", "<!DOCTYPE html><HTML lang=es><X><BODY>")]
    [InlineData("<!doctype html><p>x", "<!doctype html><X><p>x")]
    [InlineData("<p>x", "<X><p>x")]
    [InlineData("﻿<p>x", "﻿<X><p>x")]
    [InlineData("<header>no es head</header><head>", "<header>no es head</header><head><X>")]
    public void InsertsAfterHeadNeverBeforeDoctype(string html, string expected) => Assert.Equal(expected, Run(html));

    [Theory]
    [InlineData("#000000", "#ffffff")]
    [InlineData("#FFFFFF", "#000000")]
    [InlineData("#0067C0", "#ffffff")]
    [InlineData("#FFD700", "#000000")]
    [InlineData("rojo", "#ffffff")]
    public void ContrastText(string accent, string expected) => Assert.Equal(expected, HtmlInjector.ContrastText(accent));

    [Fact]
    public void ThemeStyleCannotBreakOut()
    {
        var css = HtmlInjector.ThemeStyle(new WolfGlue.Native.ThemeInfo(false, "#123456", "Mala\"</style><script>", 12, false));
        Assert.DoesNotContain("<script>", css);
        Assert.Equal(1, css.Split("</style>").Length - 1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("utf-8")]
    [InlineData("latin1")]
    public void Encodings(string? name) => Assert.NotNull(Processes.ResolveEncoding(name));

    [Fact]
    public void UnknownEncoding() =>
        Assert.Equal("InvalidArgument", Assert.Throws<WolfGlue.Bridge.GlueException>(() => Processes.ResolveEncoding("klingon")).Code);
}
