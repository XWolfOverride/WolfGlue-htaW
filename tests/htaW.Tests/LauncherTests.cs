using HtaW.Security;

namespace HtaW.Tests;

public class LauncherTests
{
    [Theory]
    [InlineData(null, new string[0])]
    [InlineData("   ", new string[0])]
    [InlineData("a b  c", new[] { "a", "b", "c" })]
    [InlineData("\"con espacio\" -x", new[] { "con espacio", "-x" })]
    [InlineData("--name=\"a b\"", new[] { "--name=a b" })]
    [InlineData("\"\" x", new[] { "", "x" })]
    [InlineData("di\\\"jo", new[] { "di\"jo" })]
    public void SplitsArguments(string? line, string[] expected) => Assert.Equal(expected, CommandLine.Split(line));

    [Theory]
    [InlineData("\"C:\\Program Files\\htaw.exe\" \"%1\" %*", "C:\\Program Files\\htaw.exe")]
    [InlineData("C:\\htaw\\htaw.exe \"%1\"", "C:\\htaw\\htaw.exe")]
    [InlineData("", null)]
    [InlineData("\"", null)]
    public void ReadsAssociatedExe(string command, string? exe) => Assert.Equal(exe, FileAssociation.CommandExe(command));

    [Fact]
    public void ForgetAllEmptiesTheStore()
    {
        using var dir = new TempDir();
        var file = Path.Combine(dir.Path, "trust.json");
        var store = new TrustStore(file);
        store.Accept("aa", "com.a", "a.htaw", new HashSet<string> { "fs" });
        store.RememberApp("com.b", new HashSet<string>());
        Assert.Equal(["com.a", "com.b"], store.Apps.Keys.Order());
        Assert.Single(store.Files);

        Assert.Equal(3, store.ForgetAll());
        var again = new TrustStore(file);
        Assert.Empty(again.Apps);
        Assert.Empty(again.Files);
        Assert.Equal(0, again.ForgetAll());
    }
}
