namespace WolfGlue.Tests;

public class BackdropTests
{
    [Theory]
    [InlineData("solid", GlueBackdrop.Solid)]
    [InlineData("tinted", GlueBackdrop.Tinted)]
    [InlineData("blurred", GlueBackdrop.Blurred)]
    public void NamesRoundTrip(string name, GlueBackdrop value)
    {
        Assert.True(GlueBackdropExtensions.TryParse(name, out var parsed));
        Assert.Equal(value, parsed);
        Assert.Equal(name, value.ToName());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("mica")]
    [InlineData("Tinted")]
    public void UnknownNamesAreRejected(string? name) => Assert.False(GlueBackdropExtensions.TryParse(name, out _));

    [Fact]
    public void DefaultsToSolid() => Assert.Equal(GlueBackdrop.Solid, default(GlueBackdrop));

    [Fact]
    public void ShimCarriesBackdropInInit()
    {
        Assert.Contains("backdrop:\"tinted\"", Shim.For("w1", "windows", "tinted"));
        Assert.Contains("backdrop:\"solid\"", Shim.For("w1", "windows"));
    }
}
