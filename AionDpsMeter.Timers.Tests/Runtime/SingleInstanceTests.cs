using AionDpsMeter.Timers.Runtime;

namespace AionDpsMeter.Timers.Tests.Runtime;

public class SingleInstanceTests
{
    private static string Name() => "aion2-overlay-test-" + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task A_second_copy_does_not_start_and_asks_the_first_to_show_itself()
    {
        var name = Name();
        using var shown = new ManualResetEventSlim();
        using var first = SingleInstance.TryStart(name, TimeSpan.Zero, () => shown.Set());
        Assert.NotNull(first);

        // another copy (another thread stands in for another process: a mutex belongs to its thread)
        var second = await Task.Run(() => SingleInstance.TryStart(name, TimeSpan.FromMilliseconds(200), () => { }), TestContext.Current.CancellationToken);
        Assert.Null(second);
        Assert.True(shown.Wait(TimeSpan.FromSeconds(5)), "the running copy was not asked to show itself");
    }

    [Fact]
    public async Task A_copy_started_while_the_old_one_is_closing_waits_for_it()
    {
        var name = Name();
        var first = await Task.Run(() => SingleInstance.TryStart(name, TimeSpan.Zero, () => { }));
        Assert.NotNull(first);
        var second = Task.Run(() => SingleInstance.TryStart(name, TimeSpan.FromSeconds(5), () => { }));
        await Task.Delay(300, TestContext.Current.CancellationToken);
        await Task.Run(() => first!.Dispose(), TestContext.Current.CancellationToken); // the old copy exits (an update restarting the meter)
        using var started = await second;
        Assert.NotNull(started);
    }
}
