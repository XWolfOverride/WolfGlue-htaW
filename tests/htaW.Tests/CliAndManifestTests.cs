using HtaW.Package;

namespace HtaW.Tests;

public class CliTests
{
    [Fact]
    public void NoArgumentsChoosesFile() => Assert.Equal(CliAction.Choose, CliCommand.Parse([]).Action);

    [Fact]
    public void RunKeepsAppArgumentsEvenWithDashes()
    {
        var c = CliCommand.Parse(["app.htaw", "-x", "--y", "z"]);
        Assert.Equal(CliAction.Run, c.Action);
        Assert.Equal("app.htaw", c.Path);
        Assert.Equal(["-x", "--y", "z"], c.AppArgs);
    }

    [Theory]
    [InlineData(new[] { "--help" }, CliAction.Help)]
    [InlineData(new[] { "-h" }, CliAction.Help)]
    [InlineData(new[] { "--version" }, CliAction.Version)]
    [InlineData(new[] { "--register" }, CliAction.Register)]
    [InlineData(new[] { "--unregister" }, CliAction.Unregister)]
    [InlineData(new[] { "--forget", "x" }, CliAction.Forget)]
    [InlineData(new[] { "--dev", "carpeta" }, CliAction.Dev)]
    [InlineData(new[] { "pack", "carpeta" }, CliAction.Pack)]
    [InlineData(new[] { "--dev" }, CliAction.Error)]
    [InlineData(new[] { "pack" }, CliAction.Error)]
    [InlineData(new[] { "pack", "c", "--rara" }, CliAction.Error)]
    [InlineData(new[] { "pack", "c", "-o" }, CliAction.Error)]
    [InlineData(new[] { "--nope" }, CliAction.Error)]
    public void Actions(string[] argv, CliAction expected) => Assert.Equal(expected, CliCommand.Parse(argv).Action);

    [Fact]
    public void PackOutput()
    {
        var c = CliCommand.Parse(["pack", "app", "-o", "salida.htaw"]);
        Assert.Equal(("app", "salida.htaw"), (c.Path, c.Output));
    }
}

public class ManifestTests
{
    [Theory]
    [InlineData("<html><head><script type=\"application/htaw+json\">{\"id\":\"a\"}</script></head>", "{\"id\":\"a\"}")]
    [InlineData("<HEAD><SCRIPT TYPE='application/htaw+json' >\n{\"id\":\"b\"}\n</SCRIPT></HEAD>", "\n{\"id\":\"b\"}\n")]
    [InlineData("<head><script>var x</script><script type=\"application/htaw+json\">{}</script>", "{}")]
    [InlineData("<head></head><body><script type=\"application/htaw+json\">{}</script>", null)]
    [InlineData("<p>sin head</p>", null)]
    public void ExtractFromHtml(string html, string? expected) => Assert.Equal(expected, ManifestReader.ExtractFromHtml(html));

    [Fact]
    public void DefaultsAndWarnings()
    {
        var w = new List<string>();
        var m = ManifestReader.Parse("""
            { // comentarios y comas finales permitidos
              "name": "X", "extra": 1,
              "window": { "frame": "none", "rara": true },
            }
            """, w);
        Assert.Equal("X", m.Name);
        Assert.Equal(800, m.Window.Width);
        Assert.Equal("native", m.Theme);
        Assert.Equal(3, w.Count); // extra, window.rara, frame none
    }

    [Theory]
    [InlineData("""{"id":"Com.Mayus"}""")]
    [InlineData("""{"id":"a..b"}""")]
    [InlineData("""{"id":"-a"}""")]
    [InlineData("""{"theme":"azul"}""")]
    [InlineData("""{"window":{"width":10}}""")]
    [InlineData("""{"entry":"../fuera.html"}""")]
    [InlineData("""{"permissions":["inventado"]}""")]
    [InlineData("""{"permissions":["shell.exec"]}""")]
    [InlineData("""{"permissions":[{"shell.exec":"git"}]}""")]
    [InlineData("""{"permissions":[{"otro":[]}]}""")]
    [InlineData("no es json")]
    public void InvalidManifests(string json) =>
        Assert.Throws<HtawFormatException>(() => ManifestReader.Parse(json, []));

    [Theory]
    [InlineData("com.ejemplo.app", true)]
    [InlineData("a", true)]
    [InlineData("a-b.c9", true)]
    [InlineData("a.", false)]
    [InlineData("a_b", false)]
    [InlineData("", false)]
    public void Ids(string id, bool ok) => Assert.Equal(ok, ManifestReader.IsValidId(id));
}
