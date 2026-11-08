using WolfGlue.Native;

namespace WolfGlue.Tests;

public class TrayTests
{
    private static GlueTray Valid() => new()
    {
        Tooltip = "t",
        Menu =
        [
            new() { Id = "abrir", Text = "Abrir", Default = true },
            new() { Type = "separator" },
            new() { Id = "salir", Text = "Salir" },
        ],
    };

    [Fact]
    public void ValidModelPasses() => Valid().Validate();

    [Fact]
    public void MenuIsMandatory()
    {
        Assert.Throws<ArgumentException>(() => new GlueTray().Validate());
        Assert.Throws<ArgumentException>(() => new GlueTray { Menu = [new() { Type = "separator" }] }.Validate());
    }

    [Fact]
    public void AtMostOneDefault()
    {
        var t = Valid();
        t.Menu[2].Default = true;
        Assert.Throws<ArgumentException>(t.Validate);
    }

    [Fact]
    public void ItemsNeedIdAndTextAndUniqueIds()
    {
        var t = Valid();
        t.Menu[0].Id = "";
        Assert.Throws<ArgumentException>(t.Validate);
        t = Valid();
        t.Menu[0].Text = " ";
        Assert.Throws<ArgumentException>(t.Validate);
        t = Valid();
        t.Menu[2].Id = "abrir";
        Assert.Throws<ArgumentException>(t.Validate);
        t = Valid();
        t.Menu[1].Type = "otro";
        Assert.Throws<ArgumentException>(t.Validate);
    }

    [Fact]
    public async Task RequestWithoutIconResolvesToAppIconModel()
    {
        var r = new GlueTrayRequest { Tooltip = "x", Color = true, Menu = Valid().Menu };
        var t = await r.ResolveAsync(null);
        Assert.Null(t.Icon);
        Assert.True(t.Color);
        Assert.Equal("x", t.Tooltip);
        await Assert.ThrowsAsync<ArgumentException>(() => new GlueTrayRequest { Icon = "a.png", Menu = Valid().Menu }.ResolveAsync(null));
    }
}
