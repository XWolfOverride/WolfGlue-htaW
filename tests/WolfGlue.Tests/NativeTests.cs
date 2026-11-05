using System.Text.Json;
using WolfGlue.Bridge;
using WolfGlue.Native;

namespace WolfGlue.Tests;

public class NativeTests
{
    [Theory]
    [InlineData(new[] { "txt" }, "*.txt")]
    [InlineData(new[] { ".md", "*.markdown" }, "*.md;*.markdown")]
    [InlineData(new[] { "  ", "" }, "*")]
    [InlineData(new string[0], "*")]
    [InlineData(new[] { "data?.bin", "*" }, "data?.bin;*")]
    public void FilterSpec(string[] patterns, string expected) =>
        Assert.Equal(expected, new FileFilter("x", patterns).Spec());

    private static JsonSerializerOptions Options => new GlueBridge().JsonOptions;

    [Fact]
    public void MessageOptionsReadFromWebShape()
    {
        var o = JsonSerializer.Deserialize("""{"text":"hola","kind":"question","buttons":"yesNoCancel"}""",
            (System.Text.Json.Serialization.Metadata.JsonTypeInfo<MessageOptions>)Options.GetTypeInfo(typeof(MessageOptions)))!;
        Assert.Equal("hola", o.Text);
        Assert.Equal(MessageKind.Question, o.Kind);
        Assert.Equal(MessageButtons.YesNoCancel, o.Buttons);
    }

    [Fact]
    public void EnumsAndRecordsWrittenInCamelCase()
    {
        var opts = Options;
        Assert.Equal("\"yes\"", JsonSerializer.Serialize(MessageResult.Yes, opts.GetTypeInfo(typeof(MessageResult))));

        var theme = new ThemeInfo(true, "#0067C0", "Segoe UI", 12, false);
        var t = JsonDocument.Parse(JsonSerializer.Serialize(theme, opts.GetTypeInfo(typeof(ThemeInfo)))).RootElement;
        Assert.True(t.GetProperty("dark").GetBoolean());
        Assert.Equal("#0067C0", t.GetProperty("accent").GetString());
        Assert.Equal("Segoe UI", t.GetProperty("fontFamily").GetString());
        Assert.False(t.GetProperty("highContrast").GetBoolean());

        ScreenInfo[] screens = [new(new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), 1.25, true)];
        var s = JsonDocument.Parse(JsonSerializer.Serialize(screens, opts.GetTypeInfo(typeof(ScreenInfo[])))).RootElement[0];
        Assert.Equal(1040, s.GetProperty("workArea").GetProperty("height").GetInt32());
        Assert.Equal(1.25, s.GetProperty("scale").GetDouble());
        Assert.True(s.GetProperty("primary").GetBoolean());
    }

    [Fact]
    public void FileDialogOptionsReadFromWebShape()
    {
        var o = JsonSerializer.Deserialize(
            """{"title":"Abrir","multiple":true,"filters":[{"name":"Texto","patterns":["txt","md"]}],"defaultPath":"C:\\tmp"}""",
            (System.Text.Json.Serialization.Metadata.JsonTypeInfo<FileDialogOptions>)Options.GetTypeInfo(typeof(FileDialogOptions)))!;
        Assert.True(o.Multiple);
        Assert.Equal("Abrir", o.Title);
        Assert.Equal("*.txt;*.md", Assert.Single(o.Filters).Spec());
        Assert.Equal(@"C:\tmp", o.DefaultPath);
    }
}
