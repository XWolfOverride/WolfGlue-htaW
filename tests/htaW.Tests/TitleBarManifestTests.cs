using HtaW.Api;
using HtaW.Package;

namespace HtaW.Tests;

public class TitleBarManifestTests
{
    [Fact]
    public void ManifestTitlebarIsParsed()
    {
        var m = ManifestReader.Parse("""
        { "window": { "titlebar": { "mode": "title", "items": [ { "type": "button", "id": "fb", "icon": "emoji", "text": "Opinar" } ] } } }
        """, []);
        Assert.Equal("title", m.Window.ParsedTitleBar!.Mode);
    }

    [Fact]
    public void BadManifestTitlebarIsAFormatError() =>
        Assert.Throws<HtawFormatException>(() => ManifestReader.Parse("""{ "window": { "titlebar": { "mode": "x" } } }""", []));

    [Fact]
    public void PageBlockGivesItsTitlebar()
    {
        var bar = HtawHost.PageTitleBar("""
        <html><head><script type="application/htaw+json">
        { "window": { "titlebar": { "items": [ { "type": "search", "id": "q" } ] } } }
        </script></head><body></body></html>
        """);
        Assert.NotNull(bar);
        Assert.Equal("search", bar!.Items[0].Type);
    }

    [Theory]
    [InlineData("<html><head></head></html>")]
    [InlineData("""<head><script type="application/htaw+json">{ "window": { "title": "x" } }</script></head>""")]
    [InlineData("""<head><script type="application/htaw+json">{ "window": { "titlebar": { "mode": "x" } } }</script></head>""")]
    [InlineData("""<head><script type="application/htaw+json">no es json</script></head>""")]
    public void PageWithoutValidBlockHasNone(string html) => Assert.Null(HtawHost.PageTitleBar(html));
}
