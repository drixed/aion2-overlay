using System.Net;
using AionDpsMeter.Timers.Schedule;

namespace AionDpsMeter.Timers.Tests.Schedule;

public class ScheduleSourceTests
{
    private const string Remote = """{ "events": [ { "id": "remote", "name": "R", "kind": "fixed", "timeZone": "UTC", "times": ["12:00"] } ] }""";
    private const string Cached = """{ "events": [ { "id": "cached", "name": "C", "kind": "fixed", "timeZone": "UTC", "times": ["12:00"] } ] }""";
    private const string Embedded = """{ "events": [ { "id": "embedded", "name": "E", "kind": "fixed", "timeZone": "UTC", "times": ["12:00"] } ] }""";

    private sealed class StubHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond());
    }

    private static ScheduleSource Source(TempDir dir, Func<HttpResponseMessage> respond) =>
        new(new HttpClient(new StubHandler(respond)), new Uri("https://example.test/schedule.json"), dir.File("cache.json"), () => Embedded);

    private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    [Fact]
    public async Task Refresh_takes_the_remote_file_and_caches_it()
    {
        using var dir = new TempDir();
        var source = Source(dir, () => Ok(Remote));
        var changed = 0;
        source.Changed += () => changed++;

        Assert.True(await source.RefreshAsync());

        Assert.Equal("remote", source.Current.Events.Single().Id);
        Assert.Equal("remote", source.Origin);
        Assert.Equal(Remote, File.ReadAllText(dir.File("cache.json")));
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task Network_failure_keeps_what_was_loaded()
    {
        using var dir = new TempDir();
        var source = Source(dir, () => throw new HttpRequestException("offline"));
        source.LoadLocal();

        Assert.False(await source.RefreshAsync());

        Assert.Equal("embedded", source.Current.Events.Single().Id);
        Assert.Equal("embedded", source.Origin);
    }

    [Fact]
    public async Task A_broken_remote_file_does_not_overwrite_the_cache()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("cache.json"), Cached);
        var source = Source(dir, () => Ok("{ broken"));
        source.LoadLocal();

        Assert.False(await source.RefreshAsync());

        Assert.Equal("cached", source.Current.Events.Single().Id);
        Assert.Equal(Cached, File.ReadAllText(dir.File("cache.json")));
    }

    [Fact]
    public async Task Http_error_status_is_a_failed_refresh()
    {
        using var dir = new TempDir();
        var source = Source(dir, () => new HttpResponseMessage(HttpStatusCode.NotFound));
        Assert.False(await source.RefreshAsync());
    }

    [Fact]
    public void LoadLocal_prefers_the_cache_and_falls_back_to_embedded_when_it_is_corrupt()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("cache.json"), Cached);
        var source = Source(dir, () => Ok(Remote));
        source.LoadLocal();
        Assert.Equal("cached", source.Current.Events.Single().Id);

        File.WriteAllText(dir.File("cache.json"), "garbage");
        source.LoadLocal();
        Assert.Equal("embedded", source.Current.Events.Single().Id);
    }
}
