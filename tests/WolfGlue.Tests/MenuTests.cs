using WolfGlue.Native;

namespace WolfGlue.Tests;

public class MenuTests
{
    private static List<GlueMenuItem> Valid() =>
    [
        new() { Id = "copiar", Text = "Copiar", Shortcut = "Ctrl+C" },
        new() { Type = "separator" },
        new() { Text = "Mas", Items = [new() { Id = "a", Text = "A", Checked = true }, new() { Id = "b", Text = "B", Enabled = false }] },
    ];

    [Fact]
    public void ValidMenuPasses() => GlueMenuItem.Validate(Valid());

    [Fact]
    public void EmptyOrOnlySeparatorsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => GlueMenuItem.Validate(null));
        Assert.Throws<ArgumentException>(() => GlueMenuItem.Validate([]));
        Assert.Throws<ArgumentException>(() => GlueMenuItem.Validate([new() { Type = "separator" }]));
    }

    [Fact]
    public void ItemsNeedTextAndIdsUniqueAcrossSubmenus()
    {
        var m = Valid();
        m[0].Text = " ";
        Assert.Throws<ArgumentException>(() => GlueMenuItem.Validate(m));
        m = Valid();
        m[0].Id = null;
        Assert.Throws<ArgumentException>(() => GlueMenuItem.Validate(m));
        m = Valid();
        m[2].Items![0].Id = "copiar"; // repetido en otro nivel
        Assert.Throws<ArgumentException>(() => GlueMenuItem.Validate(m));
        m = Valid();
        m[1].Type = "otro";
        Assert.Throws<ArgumentException>(() => GlueMenuItem.Validate(m));
    }

    [Fact]
    public void EmptySubmenuAndTooDeepNestingAreRejected()
    {
        var m = Valid();
        m[2].Items = [];
        Assert.Throws<ArgumentException>(() => GlueMenuItem.Validate(m));

        GlueMenuItem Nest(int levels) => levels == 0
            ? new() { Id = "hoja", Text = "Hoja" }
            : new() { Text = "Nivel" + levels, Items = [Nest(levels - 1)] };
        GlueMenuItem.Validate([Nest(GlueMenuItem.MaxDepth - 1)]);
        Assert.Throws<ArgumentException>(() => GlueMenuItem.Validate([Nest(GlueMenuItem.MaxDepth)]));
    }
}

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class WindowMenuTests
{
    [Fact]
    public void BarNeedsTopLevelMenusWithItems()
    {
        GlueMenuItem.ValidateBar([new() { Text = "Archivo", Items = [new() { Id = "a", Text = "A" }] }]);
        Assert.Throws<ArgumentException>(() => GlueMenuItem.ValidateBar([new() { Id = "suelto", Text = "Suelto" }]));
        Assert.Throws<ArgumentException>(() => GlueMenuItem.ValidateBar([new() { Type = "separator" }, new() { Text = "M", Items = [new() { Id = "a", Text = "A" }] }]));
        Assert.Throws<ArgumentException>(() => GlueMenuItem.ValidateBar(null));
    }

    [Fact]
    public void ParseBarReadsJsonAndRejectsBadOnes()
    {
        var m = GlueMenuItem.ParseBar("""[{ "text": "&Archivo", "items": [ { "id": "a", "text": "Abrir", "shortcut": "Ctrl+O", "checked": true }, { "type": "separator" }, { "id": "b", "text": "B", "enabled": false } ] }]""");
        Assert.Equal("&Archivo", m[0].Text);
        Assert.Equal("Ctrl+O", m[0].Items![0].Shortcut);
        Assert.True(m[0].Items![0].Checked);
        Assert.False(m[0].Items![2].Enabled);
        Assert.Throws<FormatException>(() => GlueMenuItem.ParseBar("""[{ "id": "x", "text": "Suelto" }]"""));
        Assert.Throws<FormatException>(() => GlueMenuItem.ParseBar("no json"));
    }

    [Theory]
    [InlineData("&Archivo", "Archivo")]
    [InlineData("A&&B", "A&B")]
    [InlineData("Sin marca", "Sin marca")]
    [InlineData("&&&C", "&C")]
    public void MnemonicMarksAreHiddenInTheBar(string text, string expected) =>
        Assert.Equal(expected, WolfGlue.Platform.Windows.WinTitleBar.Plain(text));
}

public class AboutAndLifecycleTests
{
    [Fact]
    public void DefaultLifecycleIsMainWindow()
    {
        Assert.Equal(GlueQuitMode.MainWindowClosed, new GlueAppOptions { AppId = "x" }.QuitMode);
    }

    [Fact]
    public void GenericAboutShowsNameAuthorVersionIconAndPoweredBy()
    {
        var html = GlueAbout.Html(new GlueAppOptions
        {
            AppId = "app", AppName = "Mi <App>", Author = "Ana & Co", Version = "1.2.3",
            AboutIcon = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], PoweredBy = "htaW",
        });
        Assert.Contains("Mi &lt;App&gt;", html);
        Assert.Contains("Ana &amp; Co", html);
        Assert.Contains("1.2.3", html);
        Assert.Contains("data:image/png;base64,", html);
        Assert.Contains("htaW", html);
    }

    [Fact]
    public void GenericAboutOmitsMissingFields()
    {
        var html = GlueAbout.Html(new GlueAppOptions { AppId = "app" });
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("class=\"muted\">Version", html);
        Assert.Contains("WolfGlue", html); // pie por defecto
    }

    [Fact]
    public void AboutRoleGetsDefaultIdAndRejectsUnknownRoles()
    {
        var menu = new List<GlueMenuItem> { new() { Text = "Archivo", Items = [new() { Role = "about", Text = "Acerca de" }] } };
        GlueMenuItem.ValidateBar(menu);
        Assert.Equal(GlueMenuItem.AboutId, menu[0].Items![0].Id);
        menu[0].Items![0].Role = "otro";
        Assert.Throws<ArgumentException>(() => GlueMenuItem.ValidateBar(menu));
    }
}

public class AboutButtonTests
{
    [Fact]
    public void AboutRoleButtonGetsIconTextAndId()
    {
        var bar = GlueTitleBar.Parse("""{ "items": [ { "type": "button", "role": "about" } ] }""");
        var b = bar.Items[0];
        Assert.Equal("help", b.Icon);
        Assert.Equal(GlueMenuItem.AboutId, b.Id);
        Assert.False(string.IsNullOrEmpty(b.Text));
        Assert.Throws<FormatException>(() => GlueTitleBar.Parse("""{ "items": [ { "type": "button", "role": "otro", "icon": "open", "text": "X" } ] }"""));
    }
}

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class MenuOverflowTests
{
    private static readonly int[] Widths = [60, 60, 60, 60]; // 4 menus de 60 px, hueco de 2

    [Fact]
    public void AllFitWhenThereIsRoom() =>
        Assert.Equal(4, WolfGlue.Platform.Windows.WinTitleBar.FitCount(Widths, 2, 32, 300));

    [Fact]
    public void OverflowLeavesRoomForTheDotsButton()
    {
        // 300 no basta para 4 (246+...); con 200 caben 2 (2*62 = 124) + 60 + 2 + 32 = 218 > 200 -> solo 2 si 124+32 <= 200
        Assert.Equal(2, WolfGlue.Platform.Windows.WinTitleBar.FitCount(Widths, 2, 32, 200));
        Assert.Equal(1, WolfGlue.Platform.Windows.WinTitleBar.FitCount(Widths, 2, 32, 130));
        Assert.Equal(0, WolfGlue.Platform.Windows.WinTitleBar.FitCount(Widths, 2, 32, 40));
    }
}

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class MenuMnemonicTests
{
    [Theory]
    [InlineData("&Archivo", 'A')]
    [InlineData("Ar&chivo", 'C')]
    [InlineData("A&&B", (char)0)]
    [InlineData("A&&B &Fin", 'F')]
    [InlineData("Sin marca", (char)0)]
    [InlineData("Final&", (char)0)]
    [InlineData("&ñandú", 'Ñ')]
    public void MnemonicIsTheLetterAfterASingleAmpersand(string text, char expected) =>
        Assert.Equal(expected, WolfGlue.Platform.Windows.WinTitleBar.Mnemonic(text));
}
