using System.Text;
using WolfGlue.Content;

namespace WolfGlue.Tests;

public class ContentTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("a/b.html", "a/b.html")]
    [InlineData("a//./b.html", "a/b.html")]
    [InlineData("a\\b.html", "a/b.html")]
    [InlineData("../x", null)]
    [InlineData("a/../../x", null)]
    [InlineData("/etc/passwd", null)]
    [InlineData("C:/Windows/win.ini", null)]
    public void Normalize(string input, string? expected) => Assert.Equal(expected, ContentPath.Normalize(input));

    [Theory]
    [InlineData("/", "", "")]
    [InlineData("/index.html", "index.html", "")]
    [InlineData("/a%20b/c.js?x=1#frag", "a b/c.js", "x=1")]
    [InlineData("/p#h?nope", "p", "")]
    public void SplitPath(string input, string path, string query)
    {
        var (p, q) = GlueRequest.SplitPath(input);
        Assert.Equal(path, p);
        Assert.Equal(query, q);
    }

    [Theory]
    [InlineData("a.HTML", "text/html; charset=utf-8")]
    [InlineData("x/y.js", "text/javascript; charset=utf-8")]
    [InlineData("img.png", "image/png")]
    [InlineData("noext", "application/octet-stream")]
    public void Mime(string path, string expected) => Assert.Equal(expected, MimeTypes.For(path));

    private static GlueRequest Get(string path) => new("GET", path, "", ReadOnlyMemory<byte>.Empty);

    [Fact]
    public async Task EmbeddedServesIndexAndSubfolders()
    {
        var c = new EmbeddedContent(typeof(ContentTests).Assembly);
        var index = await c.GetAsync(Get(""));
        Assert.NotNull(index);
        Assert.Contains("hola", Encoding.UTF8.GetString(index.Body.Span));
        Assert.Equal("text/html; charset=utf-8", index.ContentType);

        var css = await c.GetAsync(Get("sub/a.css"));
        Assert.NotNull(css);
        Assert.StartsWith("text/css", css.ContentType);

        Assert.Null(await c.GetAsync(Get("nope.txt")));
        Assert.Null(await c.GetAsync(Get("../web/index.html")));
    }

    [Fact]
    public async Task FolderDoesNotEscapeRoot()
    {
        var root = Directory.CreateTempSubdirectory("wolfglue-test-");
        try
        {
            var inner = Directory.CreateDirectory(Path.Combine(root.FullName, "site"));
            File.WriteAllText(Path.Combine(inner.FullName, "index.html"), "ok");
            File.WriteAllText(Path.Combine(root.FullName, "secret.txt"), "no");
            var c = new FolderContent(inner.FullName);

            var ok = await c.GetAsync(Get(""));
            Assert.NotNull(ok);
            Assert.Equal("ok", Encoding.UTF8.GetString(ok.Body.Span));
            Assert.Null(await c.GetAsync(Get("../secret.txt")));
            Assert.Null(await c.GetAsync(Get("..%2Fsecret.txt"))); // ya decodificado por SplitPath en el uso real
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task CompositeTriesInOrder()
    {
        var c = new CompositeContent(new EmbeddedContent(typeof(ContentTests).Assembly, "nada/"),
            new EmbeddedContent(typeof(ContentTests).Assembly));
        Assert.NotNull(await c.GetAsync(Get("index.html")));
    }

    [Theory]
    [InlineData("https://app.local/", true)]
    [InlineData("https://app.local", true)]
    [InlineData("https://app.local/x?y", true)]
    [InlineData("https://APP.local/x", true)]
    [InlineData("https://app.local.evil.com/", false)]
    [InlineData("https://app.localhost/", false)]
    [InlineData("http://app.local/", false)]
    public void SameOrigin(string url, bool expected) =>
        Assert.Equal(expected, GlueWindow.IsSameOrigin(url, "https://app.local"));
}
