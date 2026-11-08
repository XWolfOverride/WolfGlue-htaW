using HtaW.Package;
using HtaW.Security;

namespace HtaW.Tests;

public class TrayAndNotifyTests
{
    [Fact]
    public void NewPermissionsAreKnownAndLabelled()
    {
        var p = PermissionSet.FromManifest(Json.Array("""["notifications","tray"]"""));
        Assert.True(p.Has("notifications"));
        Assert.True(p.Has("tray"));
        Assert.Equal(["notifications", "tray"], p.Canonical());
        Assert.NotEqual("notifications", Strings.PermissionLabel("notifications"));
        Assert.NotEqual("tray", Strings.PermissionLabel("tray"));
        Assert.False(PermissionSet.FromManifest(Json.Array("""["fs"]""")).Has("tray"));
    }

    [Fact]
    public void ManifestReadsTrayKeepRunning()
    {
        var m = ManifestReader.Parse("""{ "entry": "index.html", "tray": { "keepRunning": true } }""", []);
        Assert.True(m.Tray.KeepRunning);
        Assert.False(ManifestReader.Parse("""{ "entry": "index.html" }""", []).Tray.KeepRunning);
    }
}
