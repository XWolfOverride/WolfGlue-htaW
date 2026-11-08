using HtaW.Api;
using HtaW.Package;
using WolfGlue;

namespace HtaW.Tests;

public class BackdropTests
{
    [Fact]
    public void ManifestBackdropIsParsedAndValidated()
    {
        var m = ManifestReader.Parse("""{ "entry": "index.html", "window": { "backdrop": "tinted" } }""", []);
        Assert.Equal("tinted", m.Window.Backdrop);
        Assert.Null(ManifestReader.Parse("""{ "entry": "index.html" }""", []).Window.Backdrop);
        Assert.Throws<HtawFormatException>(() => ManifestReader.Parse("""{ "entry": "index.html", "window": { "backdrop": "mica" } }""", []));
    }

    [Fact]
    public void PageBlockDeclaresBackdrop()
    {
        const string html = """<!doctype html><html><head><script type="application/htaw+json">{ "window": { "backdrop": "blurred" } }</script></head><body></body></html>""";
        Assert.Equal(GlueBackdrop.Blurred, HtawHost.PageBackdrop(html));
        Assert.Null(HtawHost.PageBackdrop("<html><head></head></html>"));
        Assert.Null(HtawHost.PageBackdrop("""<script type="application/htaw+json">{ "window": { "backdrop": "x" } }</script>"""));
    }
}

public class WindowMenuManifestTests
{
    [Fact]
    public void ManifestMenuIsParsedAndValidated()
    {
        var m = ManifestReader.Parse("""{ "entry": "index.html", "window": { "menu": [ { "text": "Archivo", "items": [ { "id": "a", "text": "A" } ] } ] } }""", []);
        Assert.Equal("Archivo", m.Window.ParsedMenu![0].Text);
        Assert.Throws<HtawFormatException>(() => ManifestReader.Parse("""{ "entry": "index.html", "window": { "menu": [ { "id": "x", "text": "Suelto" } ] } }""", []));
    }

    [Fact]
    public void PageBlockDeclaresMenu()
    {
        const string html = """<script type="application/htaw+json">{ "window": { "menu": [ { "text": "Ver", "items": [ { "id": "z", "text": "Zoom" } ] } ] } }</script>""";
        Assert.Equal("Ver", HtawHost.PageMenu(html)![0].Text);
        Assert.Null(HtawHost.PageMenu("<html></html>"));
        Assert.Null(HtawHost.PageMenu("""<script type="application/htaw+json">{ "window": { "menu": [ { "id": "x", "text": "Suelto" } ] } }</script>"""));
    }
}

public class AboutManifestTests
{
    [Fact]
    public void AuthorAboutAndLifecycleAreParsed()
    {
        var m = ManifestReader.Parse("""{ "entry": "index.html", "author": "Ana", "about": "acerca.html" }""", []);
        Assert.Equal("Ana", m.Author);
        Assert.Equal("main", m.Lifecycle);
        var m2 = ManifestReader.Parse("""{ "entry": "index.html", "lifecycle": "last", "about": { "page": "a.html", "width": 300, "height": 200 } }""", []);
        Assert.Equal("last", m2.Lifecycle);
        Assert.Equal(("a.html", 300, 200), HtaW.Package.Content.ReadAbout(m2.About!.Value));
    }

    [Fact]
    public void BadLifecycleOrAboutAreFormatErrors()
    {
        Assert.Throws<HtawFormatException>(() => ManifestReader.Parse("""{ "entry": "index.html", "lifecycle": "siempre" }""", []));
        Assert.Throws<HtawFormatException>(() => ManifestReader.Parse("""{ "entry": "index.html", "about": "../fuera.html" }""", []));
        Assert.Throws<HtawFormatException>(() => ManifestReader.Parse("""{ "entry": "index.html", "about": 5 }""", []));
    }
}
