namespace WolfGlue.Tests;

public class BackgroundTests
{
    [Theory]
    [InlineData("#fff", "#ffffff", "#ffffff")]
    [InlineData("#1E1e1E", "#1e1e1e", "#1e1e1e")]
    [InlineData(" light-dark(#f3f3f3, #202020) ", "#f3f3f3", "#202020")]
    [InlineData("LIGHT-DARK(#abc,#000)", "#aabbcc", "#000000")]
    public void ParsesSupportedForms(string text, string light, string dark)
    {
        Assert.True(GlueBackground.TryParse(text, out var b));
        Assert.Equal(light, b.Light.ToString());
        Assert.Equal(dark, b.Dark.ToString());
        Assert.Equal(dark, b.For(dark: true).ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("red")]
    [InlineData("#12")]
    [InlineData("#12345g")]
    [InlineData("#ffffff80")]
    [InlineData("light-dark(#fff)")]
    [InlineData("light-dark(#fff, azul)")]
    public void RejectsOthers(string? text) => Assert.False(GlueBackground.TryParse(text, out _));

    [Fact]
    public void ToStringRoundTrips()
    {
        var b = GlueBackground.Parse("light-dark(#f3f3f3, #202020)");
        Assert.Equal(b, GlueBackground.Parse(b.ToString()));
        Assert.Equal("#123456", GlueBackground.Parse("#123456").ToString());
    }

    [Fact]
    public void ThemeColorSingle()
    {
        var b = GlueColorParser.ThemeColorFromHtml("""<html><head><meta name="theme-color" content="#336699"></head></html>""");
        Assert.Equal(GlueBackground.Parse("#336699"), b);
    }

    [Fact]
    public void ThemeColorPerScheme()
    {
        var b = GlueColorParser.ThemeColorFromHtml("""
            <!doctype html><head>
            <meta content='#fafafa' name='theme-color' media='(prefers-color-scheme: light)'>
            <meta name=theme-color media="(prefers-color-scheme:dark)" content="#111">
            </head>
            """);
        Assert.Equal(GlueBackground.Parse("light-dark(#fafafa, #111111)"), b);
    }

    [Fact]
    public void ThemeColorOnlyDarkUsedForBoth()
    {
        var b = GlueColorParser.ThemeColorFromHtml("""<meta name="theme-color" media="(prefers-color-scheme: dark)" content="#000">""");
        Assert.Equal(GlueBackground.Parse("#000"), b);
    }

    [Theory]
    [InlineData("<html><head><title>x</title></head></html>")]
    [InlineData("""<head><meta name="description" content="#fff"></head>""")]
    [InlineData("""<head></head><body><meta name="theme-color" content="#fff"></body>""")]
    [InlineData("""<head><meta name="theme-color" content="red"></head>""")]
    public void ThemeColorAbsent(string html) => Assert.Null(GlueColorParser.ThemeColorFromHtml(html));

    [Fact]
    public void FadeScript()
    {
        Assert.Equal("", Shim.FadeIn(0));
        var js = Shim.FadeIn(180);
        Assert.Contains("const D=180;", js);
        Assert.Contains("prefers-reduced-motion", js);
    }
}
