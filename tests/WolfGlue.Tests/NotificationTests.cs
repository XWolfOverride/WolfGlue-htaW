using WolfGlue.Native;
using WolfGlue.Platform.Windows;

namespace WolfGlue.Tests;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class NotificationTests
{
    [Fact]
    public void PInterfaceIdOfActivatedHandler()
    {
        // IID conocido de TypedEventHandler<ToastNotification, IInspectable>.
        Assert.Equal(new Guid("ab54de2d-97d9-5528-b6ad-105afe156530"), WinToast.IidActivatedHandler);
    }

    [Fact]
    public void ValidationRejectsBadModels()
    {
        Assert.Throws<ArgumentException>(() => NativeNotifications.Validate(new GlueNotification { Title = " " }));
        Assert.Throws<ArgumentException>(() => NativeNotifications.Validate(new GlueNotification
        {
            Title = "t",
            Buttons = [new() { Id = "a", Text = "A" }, new() { Id = "b", Text = "B" }, new() { Id = "c", Text = "C" }],
        }));
        Assert.Throws<ArgumentException>(() => NativeNotifications.Validate(new GlueNotification
        {
            Title = "t",
            Buttons = [new() { Id = "", Text = "A" }],
        }));
        NativeNotifications.Validate(new GlueNotification { Title = "t", Buttons = [new() { Id = "a", Text = "A" }] });
    }

    [Fact]
    public void XmlHasTextButtonsAndEscapes()
    {
        var xml = WinToast.BuildXml("id1", new GlueNotification
        {
            Title = "A & B <x>",
            Body = "cuerpo \"q\"",
            Silent = true,
            Buttons = [new() { Id = "ok", Text = "Vale" }],
        });
        var doc = System.Xml.Linq.XDocument.Parse(xml); // XML bien formado
        Assert.Equal("toast", doc.Root!.Name.LocalName);
        Assert.Equal(["A & B <x>", "cuerpo \"q\""], doc.Descendants("text").Select(t => t.Value));
        Assert.Equal("Vale", doc.Descendants("action").Single().Attribute("content")!.Value);
        Assert.Equal("true", doc.Descendants("audio").Single().Attribute("silent")!.Value);
    }

    [Fact]
    public void ActivationArgumentsRoundTrip()
    {
        Assert.True(WinToast.TryParseArgs(WinToast.Args("c", "a|b c", null), out var id, out var button));
        Assert.Equal(("a|b c", null), (id, button));
        Assert.True(WinToast.TryParseArgs(WinToast.Args("b", "x:1", "bt|2"), out id, out button));
        Assert.Equal(("x:1", "bt|2"), (id, button));
        Assert.False(WinToast.TryParseArgs("", out _, out _));
        Assert.False(WinToast.TryParseArgs("z|a", out _, out _));
    }
}
