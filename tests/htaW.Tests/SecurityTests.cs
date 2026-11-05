using HtaW.Package;
using HtaW.Security;
using WolfGlue.Bridge;

namespace HtaW.Tests;

public class PermissionTests
{
    [Fact]
    public void ParsesSimpleAndExec()
    {
        var p = PermissionSet.FromManifest(Json.Array("""["fs", "dialog", {"shell.exec": ["git", "ffmpeg"]}]"""));
        Assert.True(p.Has("fs"));
        Assert.True(p.Has("shell.exec"));
        Assert.False(p.Has("http"));
        Assert.False(p.IsDangerous);
        Assert.Equal(["dialog", "fs", "shell.exec:ffmpeg", "shell.exec:git"], p.Canonical());
        var e = Assert.Throws<GlueException>(() => p.Require("http"));
        Assert.Equal("PermissionDenied", e.Code);
    }

    [Theory]
    [InlineData("""[{"shell.exec": "*.*"}]""")]
    [InlineData("""[{"shell.exec": ""}]""")]
    [InlineData("""[{"shell.exec": ["git", "*.*"]}]""")]
    [InlineData("""[{"shell.exec": ["powershell"]}]""")]
    [InlineData("""[{"shell.exec": ["python3.12"]}]""")]
    [InlineData("""[{"shell.exec": ["cmd.exe"]}]""")]
    public void DangerousExec(string json) => Assert.True(PermissionSet.FromManifest(Json.Array(json)).IsDangerous);

    [Theory]
    [InlineData("opencv", false)]
    [InlineData("nodemon", false)]
    [InlineData("node", true)]
    [InlineData("pwsh-preview", true)]
    [InlineData("BASH.EXE", true)]
    public void Interpreters(string name, bool expected) => Assert.Equal(expected, ExecPolicy.IsInterpreter(name));

    private static (ExecPolicy policy, TempDir dir, string pathEnv) Policy(string json)
    {
        var dir = new TempDir();
        dir.File(Path.Combine("bin", "tool.exe"), "");
        dir.File(Path.Combine("bin", "otro.exe"), "");
        var p = PermissionSet.FromManifest(Json.Array(json)).Exec!;
        return (p, dir, Path.Combine(dir.Path, "bin"));
    }

    [Fact]
    public void ResolveAllowedNameInPath()
    {
        var (p, dir, path) = Policy("""[{"shell.exec": ["tool"]}]""");
        using (dir)
        {
            Assert.Equal(Path.Combine(path, "tool.exe"), p.Resolve("tool", path, ".EXE"), ignoreCase: true);
            Assert.Equal(Path.Combine(path, "tool.exe"), p.Resolve("tool.exe", path, ".EXE"), ignoreCase: true);
            Assert.Equal("PermissionDenied", Assert.Throws<GlueException>(() => p.Resolve("otro", path, ".EXE")).Code);
            // Un permiso por nombre no autoriza una ruta concreta
            Assert.Equal("PermissionDenied", Assert.Throws<GlueException>(() => p.Resolve(Path.Combine(path, "tool.exe"), path, ".EXE")).Code);
        }
    }

    [Fact]
    public void ResolveExplicitPathAndAll()
    {
        var (_, dir, path) = Policy("[]");
        using (dir)
        {
            var exe = Path.Combine(path, "otro.exe");
            var byPath = PermissionSet.FromManifest(Json.Array($$"""[{"shell.exec": [{{System.Text.Json.JsonSerializer.Serialize(exe)}}]}]""")).Exec!;
            Assert.Equal(exe, byPath.Resolve(exe, path, ".EXE"), ignoreCase: true);

            var all = PermissionSet.FromManifest(Json.Array("""[{"shell.exec": "*.*"}]""")).Exec!;
            Assert.Equal(exe, all.Resolve("otro", path, ".EXE"), ignoreCase: true);
            Assert.Throws<FileNotFoundException>(() => all.Resolve("noexiste", path, ".EXE"));
        }
    }

    [Theory]
    [InlineData("a.exe", true)]
    [InlineData("b.CMD", true)]
    [InlineData("c.lnk", true)]
    [InlineData("d.htaw", true)]
    [InlineData("e.txt", false)]
    [InlineData("f.pdf", false)]
    public void ExecutableFiles(string name, bool expected) => Assert.Equal(expected, ExecPolicy.IsExecutableFile(name));
}

public class TrustTests
{
    [Theory]
    [InlineData("[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=https://x", 3)]
    [InlineData("[ZoneTransfer]\nZoneId=2", 2)]
    [InlineData("basura", 0)]
    public void ZoneId(string content, int expected) => Assert.Equal(expected, OriginDetector.ParseZoneId(content));

    [Fact]
    public void MarkOfTheWeb()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var dir = new TempDir();
        var f = dir.File("app.htaw", "<html></html>");
        Assert.Equal(FileOrigin.Local, OriginDetector.Detect(f));
        File.WriteAllText(f + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n");
        Assert.Equal(FileOrigin.External, OriginDetector.Detect(f));
    }

    [Fact]
    public void PromptRules()
    {
        using var dir = new TempDir();
        var store = new TrustStore(Path.Combine(dir.Path, "trust.json"));
        var p1 = new SortedSet<string> { "fs" };
        var p2 = new SortedSet<string> { "fs", "http" };

        Assert.False(store.NeedsPrompt("h1", "app", p1, FileOrigin.Local));        // local nuevo: no pregunta
        Assert.True(store.NeedsPrompt("h1", "app", p1, FileOrigin.External));      // descargado: pregunta
        store.Accept("h1", "app", "x", p1);
        Assert.False(store.NeedsPrompt("h1", "app", p1, FileOrigin.External));     // ya aceptado
        Assert.True(store.NeedsPrompt("h2", "app", p1, FileOrigin.External));      // otro contenido descargado
        Assert.True(store.NeedsPrompt("h1", "app", p2, FileOrigin.Local));         // pide mas que lo aceptado

        // Se guarda en disco
        var again = new TrustStore(Path.Combine(dir.Path, "trust.json"));
        Assert.False(again.NeedsPrompt("h1", "app", p1, FileOrigin.External));

        Assert.Equal(2, again.Forget(null, "app"));
        Assert.True(again.NeedsPrompt("h1", "app", p1, FileOrigin.External));
    }

    [Fact]
    public void LocalAppsAreRememberedToDetectNewPermissions()
    {
        using var dir = new TempDir();
        var store = new TrustStore(Path.Combine(dir.Path, "trust.json"));
        store.RememberApp("app", new SortedSet<string> { "fs" });
        Assert.False(store.NeedsPrompt("h", "app", new SortedSet<string> { "fs" }, FileOrigin.Local));
        Assert.True(store.NeedsPrompt("h", "app", new SortedSet<string> { "fs", "shell.exec:*" }, FileOrigin.Local));
    }
}
