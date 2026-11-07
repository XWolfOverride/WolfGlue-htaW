using HtaW.Api;

namespace HtaW.Tests;

public class SingleInstanceTests
{
    [Fact]
    public void NameDependsOnApp()
    {
        Assert.Equal(SingleInstance.NameFor("a.b"), SingleInstance.NameFor("a.b"));
        Assert.NotEqual(SingleInstance.NameFor("a.b"), SingleInstance.NameFor("a.c"));
    }

    [Fact]
    public async Task SecondInstanceForwardsArgsToFirst()
    {
        var id = "pruebas.unica." + Guid.NewGuid().ToString("N");
        using var first = SingleInstance.Acquire(id, ["uno"]);
        Assert.NotNull(first);
        var got = new TaskCompletionSource<SecondInstanceInfo>();
        first!.Listen(info => got.TrySetResult(info));

        // Otro hilo hace de segunda instancia: el mutex ya existe, asi que pasa sus argumentos y no arranca.
        SingleInstance? second = null;
        var t = new Thread(() => second = SingleInstance.Acquire(id, ["dos", "tres"]));
        t.Start();
        Assert.True(t.Join(10000));
        Assert.Null(second);
        var info = await got.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(["dos", "tres"], info.Args);
        Assert.Equal(Environment.CurrentDirectory, info.Cwd);
    }

    [Fact]
    public void AfterDisposeANewInstanceIsPrimary()
    {
        var id = "pruebas.unica." + Guid.NewGuid().ToString("N");
        SingleInstance.Acquire(id, [])!.Dispose();
        SingleInstance? again = null;
        var t = new Thread(() => { again = SingleInstance.Acquire(id, []); again?.Dispose(); });
        t.Start();
        Assert.True(t.Join(10000));
        Assert.NotNull(again);
    }
}
