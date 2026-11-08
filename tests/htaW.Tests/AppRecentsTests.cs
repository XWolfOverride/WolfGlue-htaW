using HtaW.Security;

namespace HtaW.Tests;

public class AppRecentsTests
{
    [Fact]
    public void KeepsOrderWithoutDuplicatesAndLimit()
    {
        using var dir = new TempDir();
        var r = new AppRecents(Path.Combine(dir.Path, "sub", "recent.json"));
        Assert.Empty(r.Load());
        for (int i = 0; i < AppRecents.Max + 3; i++) r.Add(Path.Combine(dir.Path, "a" + i + ".htaw"), "App " + i, null);
        var list = r.Load();
        Assert.Equal(AppRecents.Max, list.Count);
        Assert.Equal("App " + (AppRecents.Max + 2), list[0].Name);

        r.Add(Path.Combine(dir.Path, "a5.htaw"), "Cinco", null);
        list = r.Load();
        Assert.Equal(AppRecents.Max, list.Count);
        Assert.Single(list, x => x.Path.EndsWith("a5.htaw", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Cinco", list[0].Name);

        Assert.True(r.Contains(list[0].Path));
        Assert.True(r.Remove(list[0].Path));
        Assert.False(r.Remove(list[0].Path + "-no"));
        Assert.Equal(AppRecents.Max - 1, r.Load().Count);
    }

    [Fact]
    public void StoresNameIconAndDate()
    {
        using var dir = new TempDir();
        var r = new AppRecents(Path.Combine(dir.Path, "recent.json"));
        byte[] png = [0x89, (byte)'P', (byte)'N', (byte)'G', 1, 2, 3];
        var when = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        r.Add(Path.Combine(dir.Path, "x.htaw"), "Equis", png, when);
        var e = Assert.Single(r.Load());
        Assert.Equal("Equis", e.Name);
        Assert.Equal(when, e.Date);
        Assert.StartsWith("data:image/png;base64,", e.Icon);
    }

    [Fact]
    public void IconUriDependsOnTypeAndSize()
    {
        Assert.Null(AppRecents.IconUri(null));
        Assert.Null(AppRecents.IconUri([]));
        Assert.Null(AppRecents.IconUri(new byte[AppRecents.MaxIconBytes + 1]));
        Assert.StartsWith("data:image/x-icon;base64,", AppRecents.IconUri([0, 0, 1, 0, 1]));
    }

    [Fact]
    public void DamagedFileStartsEmpty()
    {
        using var dir = new TempDir();
        var f = dir.File("recent.json", "{ no es una lista");
        var r = new AppRecents(f);
        Assert.Empty(r.Load());
        r.Add(Path.Combine(dir.Path, "x.htaw"), "X", null);
        Assert.Single(r.Load());
    }
}
