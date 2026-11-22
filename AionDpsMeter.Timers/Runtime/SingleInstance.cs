// aion2-overlay fork: new file (see FORK_CHANGES.md).
namespace AionDpsMeter.Timers.Runtime;

/// <summary>
/// One meter at a time: the first copy holds a named mutex (on a thread of its own, a mutex belongs to the thread that took
/// it) and listens for "show yourself". A second copy waits a little (the old one may still be closing after an update), then
/// asks the running one to show itself and does not start.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly ManualResetEvent stop = new(false);
    private readonly Thread owner;

    private SingleInstance(Thread owner) => this.owner = owner;

    /// <summary>This copy may run (keep the result until exit), or null: another copy runs and was asked to show itself.</summary>
    public static SingleInstance? TryStart(string name, TimeSpan wait, Action onShowRequested)
    {
        var show = new EventWaitHandle(false, EventResetMode.AutoReset, name + "-show");
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        SingleInstance? self = null;
        var thread = new Thread(() =>
        {
            using var mutex = new Mutex(false, name);
            bool got;
            try
            {
                got = mutex.WaitOne(0);
                if (!got)
                {
                    show.Set(); // the running copy shows itself now, not after the wait
                    got = mutex.WaitOne(wait); // ...unless it is closing (an update restarting the meter): then this one runs
                }
            }
            catch (AbandonedMutexException) { got = true; } // the old copy died without letting go: ours now
            acquired.SetResult(got);
            if (!got) return;
            try
            {
                while (WaitHandle.WaitAny([self!.stop, show]) == 1)
                {
                    try { onShowRequested(); } catch { /* a failed show never stops the listener */ }
                }
            }
            finally
            {
                mutex.ReleaseMutex();
                show.Dispose();
            }
        }) { IsBackground = true, Name = "single-instance" };
        self = new SingleInstance(thread);
        thread.Start();
        if (acquired.Task.GetAwaiter().GetResult()) return self;
        show.Dispose();
        return null;
    }

    public void Dispose()
    {
        stop.Set();
        owner.Join(TimeSpan.FromSeconds(2));
    }
}
