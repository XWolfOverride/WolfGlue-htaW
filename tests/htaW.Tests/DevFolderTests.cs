using HtaW.Security;

namespace HtaW.Tests;

public class DevFolderTests
{
    [Fact]
    public void FolderArgumentIsDevMode()
    {
        using var dir = new TempDir();
        var c = CliCommand.Parse([dir.Path, "--x", "y"]);
        Assert.Equal(CliAction.Dev, c.Action);
        Assert.Equal(dir.Path, c.Path);
        Assert.Equal(["--x", "y"], c.AppArgs);
    }

    [Fact]
    public void ManifestArgumentIsDevModeOfItsFolder()
    {
        using var dir = new TempDir();
        var mf = dir.File("app/htaw.json", "{}");
        var app = Path.GetDirectoryName(mf)!;
        Assert.Equal((CliAction.Dev, app), (CliCommand.Parse([mf]).Action, CliCommand.Parse([mf]).Path));
        Assert.Equal(app, CliCommand.Parse(["--dev", mf]).Path);
        Assert.Equal(app, CliCommand.Parse(["pack", mf]).Path);
        Assert.Equal(app, CliCommand.Parse(["bundle", mf]).Path);
        Assert.Equal(app, CliCommand.Parse([Path.Combine(app, "HTAW.JSON")]).Path);
    }

    [Fact]
    public void FilesAndMissingPathsAreStillRun()
    {
        using var dir = new TempDir();
        var f = dir.File("app.htaw", "<p>x</p>");
        Assert.Equal(CliAction.Run, CliCommand.Parse([f]).Action);
        // Un htaw.json que no existe no se convierte en carpeta: el error sera "no existe" al abrirlo.
        Assert.Equal(CliAction.Run, CliCommand.Parse([Path.Combine(dir.Path, "no", "htaw.json")]).Action);
        Assert.Equal(CliAction.Run, CliCommand.Parse([Path.Combine(dir.Path, "no-existe")]).Action);
    }

    [Fact]
    public void RecentsKeepOrderWithoutDuplicatesAndLimit()
    {
        using var dir = new TempDir();
        var r = new DevRecents(Path.Combine(dir.Path, "sub", "recent-dev.json"));
        Assert.Empty(r.Load());
        for (int i = 0; i < DevRecents.Max + 3; i++) r.Add(Path.Combine(dir.Path, "app" + i));
        var list = r.Load();
        Assert.Equal(DevRecents.Max, list.Count);
        Assert.Equal(Path.Combine(dir.Path, "app" + (DevRecents.Max + 2)), list[0]);

        r.Add(Path.Combine(dir.Path, "app5") + Path.DirectorySeparatorChar);
        if (OperatingSystem.IsWindows()) r.Add(Path.Combine(dir.Path, "APP5"));
        list = r.Load();
        Assert.Equal(DevRecents.Max, list.Count);
        Assert.Single(list, p => p.EndsWith("app5", StringComparison.OrdinalIgnoreCase));
        Assert.EndsWith("5", list[0]);

        Assert.True(r.Contains(list[0]));
        Assert.True(r.Remove(list[0]));
        Assert.False(r.Remove(list[0] + "-no"));
        Assert.Equal(DevRecents.Max - 1, r.Load().Count);
    }

    [Fact]
    public void DamagedRecentsFileStartsEmpty()
    {
        using var dir = new TempDir();
        var f = dir.File("recent-dev.json", "{ esto no es una lista");
        var r = new DevRecents(f);
        Assert.Empty(r.Load());
        r.Add(dir.Path);
        Assert.Single(r.Load());
    }
}
