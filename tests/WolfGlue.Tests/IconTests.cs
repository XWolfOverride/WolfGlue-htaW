using WolfGlue.Platform.Windows;

namespace WolfGlue.Tests;

public class IconTests
{
    /// <summary>Iconos del propio repositorio (src/htaW/icon): un PNG de 350 px y un .ico con varios tamanos.</summary>
    private static byte[] Asset(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return File.ReadAllBytes(Path.Combine(dir!.FullName, "src", "htaW", "icon", name));
    }

    [Fact]
    public void DetectsFormats()
    {
        Assert.Equal(GlueIconFormat.Png, GlueIcon.Detect(Asset("icon.png")));
        Assert.Equal(GlueIconFormat.Ico, GlueIcon.Detect(Asset("icon.ico")));
        Assert.Equal(GlueIconFormat.Unknown, GlueIcon.Detect("<svg/>"u8));
        Assert.Equal(GlueIconFormat.Unknown, GlueIcon.Detect([]));
        Assert.Equal((350, 350), GlueIcon.PngSize(Asset("icon.png")));
    }

    [Fact]
    public void ReadsIcoDirectoryAndPicksBestSize()
    {
        var images = GlueIcon.IcoImages(Asset("icon.ico"));
        Assert.NotEmpty(images);
        var sizes = images.Select(i => i.Width).ToHashSet();
        foreach (var want in new[] { 16, 20, 24, 32, 48, 64, 300 })
        {
            var best = GlueIcon.Best(images, want);
            var expected = sizes.Where(s => s >= want).DefaultIfEmpty(sizes.Max()).Min();
            Assert.Equal(expected, best.Width);
        }
    }

    [Fact]
    public void RejectsBrokenIco()
    {
        byte[] bad = [0, 0, 1, 0, 1, 0, 16, 16, 0, 0, 1, 0, 32, 0, 0xFF, 0xFF, 0, 0, 22, 0, 0, 0];
        Assert.Throws<FormatException>(() => GlueIcon.IcoImages(bad));
    }

    [Theory]
    [InlineData("icon.png", 16)]
    [InlineData("icon.png", 32)]
    [InlineData("icon.ico", 20)]
    [InlineData("icon.ico", 48)]
    public void CreatesHiconAtRequestedSize(string asset, int size)
    {
        if (!OperatingSystem.IsWindows()) return;
        var h = WinIcon.Create(Asset(asset), size);
        Assert.NotEqual(0, h);
        try { Assert.Equal(size, WinIcon.Width(h)); }
        finally { WinIcon.DestroyIcon(h); }
    }

    [Fact]
    public void InvalidDataGivesNoIcon()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Equal(0, WinIcon.Create("hola"u8.ToArray(), 32));
    }
}
