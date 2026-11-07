using WolfGlue;

namespace WolfGlue.Tests;

public class TitleBarTests
{
    private static GlueTitleBar P(string json) => GlueTitleBar.Parse(json);

    [Fact]
    public void ParsesFullToolbar()
    {
        var bar = P("""
        { "mode": "labels", "items": [
            { "type": "button", "id": "open", "icon": "open", "text": "Abrir" },
            { "type": "group", "text": "Editar", "items": [
                { "type": "button", "id": "cut", "icon": "cut", "text": "Cortar" },
                { "type": "button", "id": "copy", "icon": "copy", "text": "Copiar" } ] },
            { "type": "panel", "id": "repo", "text": "WolfGlue", "subtitle": "main", "center": true, "width": 260 },
            { "type": "button", "id": "sync", "icon": "sync", "text": "Sincronizar", "split": true,
              "menu": [ { "id": "pull", "text": "Traer" }, { "type": "separator" }, { "id": "push", "text": "Enviar" } ] },
            { "type": "space" },
            { "type": "search", "id": "q", "placeholder": "Buscar" } ] }
        """);
        Assert.Equal(GlueTitleBar.ModeLabels, bar.Mode);
        Assert.Equal(6, bar.Items.Count);
        Assert.NotNull(bar.Find("copy"));
        Assert.True(bar.Items[3].Split);
    }

    [Fact]
    public void DefaultModeIsCompact() => Assert.Equal(GlueTitleBar.ModeCompact, P("""{ "items": [] }""").Mode);

    [Theory]
    [InlineData("""{ "mode": "big" }""")]
    [InlineData("""{ "items": [ { "type": "button", "id": "a", "icon": "save" } ] }""")] // sin text
    [InlineData("""{ "items": [ { "type": "button", "id": "a", "text": "A" } ] }""")] // sin icon
    [InlineData("""{ "items": [ { "type": "button", "id": "a", "text": "A", "icon": "nope" } ] }""")]
    [InlineData("""{ "items": [ { "type": "button", "id": "a", "text": "A", "icon": "save", "split": true } ] }""")]
    [InlineData("""{ "items": [ { "type": "button", "id": "a", "text": "A", "icon": "font:E001" } ] }""")] // sin iconFont
    [InlineData("""{ "items": [ { "type": "button", "id": "a", "text": "A", "icon": "../x.png" } ] }""")]
    [InlineData("""{ "items": [ { "type": "separator" } ] }""")]
    [InlineData("""{ "items": [ { "type": "panel", "text": "P", "width": 50 } ] }""")]
    [InlineData("""{ "items": [ { "type": "panel" } ] }""")]
    [InlineData("""{ "items": [ { "type": "search" } ] }""")]
    [InlineData("""{ "items": [ { "type": "search", "id": "q", "center": true } ] }""")]
    [InlineData("""{ "items": [ { "type": "search", "id": "a" }, { "type": "search", "id": "b" } ] }""")]
    [InlineData("""{ "items": [ { "type": "image", "icon": "logo.svg" } ] }""")]
    [InlineData("""{ "items": [ { "type": "space", "center": true } ] }""")]
    [InlineData("""{ "items": [ { "type": "panel", "text": "A", "center": true }, { "type": "panel", "text": "B", "center": true } ] }""")]
    [InlineData("""{ "items": [ { "type": "group", "items": [ { "type": "panel", "text": "P" } ] } ] }""")]
    [InlineData("""{ "items": [ { "type": "group", "items": [ { "type": "group", "items": [] } ] } ] }""")]
    [InlineData("""{ "items": [ { "type": "button", "id": "a", "icon": "save", "text": "A" }, { "type": "button", "id": "a", "icon": "open", "text": "B" } ] }""")]
    [InlineData("""{ "mode": "title", "items": [ { "type": "space" } ] }""")]
    [InlineData("""{ "mode": "title", "items": [ { "type": "button", "id": "a", "icon": "save", "text": "A" }, { "type": "button", "id": "b", "icon": "save", "text": "B" }, { "type": "button", "id": "c", "icon": "save", "text": "C" }, { "type": "button", "id": "d", "icon": "save", "text": "D" } ] }""")]
    [InlineData("""{ "items": [ { "type": "button", "id": "a", "icon": "save", "text": "A", "menu": [ { "text": "sin id" } ] } ] }""")]
    public void RejectsInvalid(string json) => Assert.Throws<FormatException>(() => P(json));

    [Fact]
    public void TitleModeAllowsThreeButtons() =>
        P("""{ "mode": "title", "items": [ { "type": "button", "id": "e", "icon": "emoji", "text": "Opinar" } ] }""");

    [Theory]
    [InlineData("save", true)]
    [InlineData("img/abrir.png", true)]
    [InlineData("icono.ICO", true)]
    [InlineData("data:image/png;base64,AAAA", true)]
    [InlineData("font:E001", true)]
    [InlineData("font:ZZ", false)]
    [InlineData("glyph:E713", false)]
    [InlineData("c:/x.png", false)]
    [InlineData("img/a.svg", false)]
    public void IconValidity(string icon, bool valid) => Assert.Equal(valid, GlueTitleBarIcons.IsValid(icon));

    [Fact]
    public void FontIconNeedsDeclaredFont() =>
        P("""{ "iconFont": "fonts/iconos.ttf", "items": [ { "type": "button", "id": "a", "icon": "font:E001", "text": "A" } ] }""");

    [Fact]
    public void FontInfoReadsFamilyName()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        if (!File.Exists(path)) return;
        Assert.Equal("Segoe UI", GlueFontInfo.FamilyName(File.ReadAllBytes(path)));
    }

    [Fact]
    public void FontInfoRejectsGarbage()
    {
        Assert.Null(GlueFontInfo.FamilyName(new byte[] { 1, 2, 3 }));
        Assert.Null(GlueFontInfo.FamilyName(new byte[64]));
    }
}
