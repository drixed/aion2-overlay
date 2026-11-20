// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.IO;
using System.Net.Http;
using System.Text.Json;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.News;
using Microsoft.Extensions.Logging;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>
    /// NCSOFT's announcements for the 📣 window: the board every 15 minutes (while "Объявления" is on), kept in news-archive.json
    /// with the older pages fetched back (the board's "more") until a page brings nothing new, so the history and the counts per
    /// kind are there. Notices that may hold Twitch Drops (the drops ones and the event ones) are read in full once (again when
    /// their title changes) and their campaigns kept in drops.json. "Новое" is what was posted after the window was last opened.
    /// </summary>
    public sealed class NewsService(TimersOptionsStore options, ILogger<NewsService> logger)
    {
        private static readonly TimeSpan Every = TimeSpan.FromMinutes(15);
        private const int MaxPagesBack = 12;
        private const int MaxBodiesPerRefresh = 40;
        private const int ParserVersion = 2;
        private static readonly HttpClient Http = CreateClient();
        private readonly SemaphoreSlim busy = new(1, 1);
        private System.Threading.Timer? timer;
        private bool started;
        private bool historyChecked;
        private DropsFile drops = new();

        public IReadOnlyList<Announcement> Items { get; private set; } = [];

        /// <summary>The drops campaigns found in the notices, the latest first.</summary>
        public IReadOnlyList<DropCampaign> Drops { get; private set; } = [];

        public event Action? Changed;

        private static string ArchivePath => Path.Combine(AppContext.BaseDirectory, "news-archive.json");
        private static string OldCachePath => Path.Combine(AppContext.BaseDirectory, "news-cache.json");
        private static string DropsPath => Path.Combine(AppContext.BaseDirectory, "drops.json");

        public bool HasNew => options.Current.ShowNews && Items.Any(IsNew);

        public bool IsNew(Announcement a) => a.Posted.ToUnixTimeSeconds() > options.Current.NewsSeenEpoch;

        public void Start()
        {
            if (started) return;
            started = true;
            try
            {
                if (File.Exists(ArchivePath)) Items = JsonSerializer.Deserialize<List<Announcement>>(File.ReadAllText(ArchivePath)) ?? [];
                else if (File.Exists(OldCachePath)) Items = Announcements.Parse(File.ReadAllText(OldCachePath));
                if (File.Exists(DropsPath)) drops = JsonSerializer.Deserialize<DropsFile>(File.ReadAllText(DropsPath)) ?? new();
                if (drops.Version < ParserVersion) drops.Scanned.Clear();
                drops.Version = ParserVersion;
                Drops = Sorted(drops.Campaigns);
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
            {
                logger.LogDebug(ex, "The announcement archive could not be read");
            }
            timer = new System.Threading.Timer(_ => _ = RefreshAsync(), null, TimeSpan.FromSeconds(15), Every);
        }

        public async Task RefreshAsync()
        {
            if (!options.Current.ShowNews || !await busy.WaitAsync(0).ConfigureAwait(false)) return;
            try
            {
                var json = await Http.GetStringAsync(Announcements.ListUrl).ConfigureAwait(false);
                var fresh = Announcements.Parse(json);
                if (fresh.Count == 0) return; // a changed or broken answer keeps what we had
                Publish(Announcements.Merge(Items, fresh));
                if (!historyChecked && Announcements.HasMore(json)) await FetchHistoryAsync(fresh[^1].Id).ConfigureAwait(false);
                historyChecked = true;
                await ScanDropsAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or JsonException)
            {
                logger.LogDebug(ex, "Announcements could not be loaded");
            }
            finally
            {
                busy.Release();
            }
        }

        /// <summary>The pages before the newest ten, until a page brings nothing new (the archive already has the rest).</summary>
        private async Task FetchHistoryAsync(string beforeId)
        {
            for (var page = 0; page < MaxPagesBack; page++)
            {
                var json = await Http.GetStringAsync(Announcements.MoreUrl(beforeId)).ConfigureAwait(false);
                var older = Announcements.Parse(json);
                if (older.Count == 0) return;
                var known = Items.Select(i => i.Id).ToHashSet();
                var anyNew = older.Any(a => !known.Contains(a.Id));
                Publish(Announcements.Merge(Items, older));
                if (!anyNew || !Announcements.HasMore(json)) return;
                beforeId = older[^1].Id;
                await Task.Delay(300).ConfigureAwait(false);
            }
        }

        /// <summary>Reads the bodies of the notices that may hold drops: the drops ones and the event ones ("Прочее"), each once per title.</summary>
        private async Task ScanDropsAsync()
        {
            var todo = Items.Where(a => a.Kind is NewsKind.TwitchDrops or NewsKind.Other && drops.Scanned.GetValueOrDefault(a.Id) != a.Title)
                .Take(MaxBodiesPerRefresh).ToList();
            if (todo.Count == 0) return;
            try
            {
                foreach (var a in todo)
                {
                    var html = Announcements.ArticleHtml(await Http.GetStringAsync(Announcements.ArticleUrl(a.Id)).ConfigureAwait(false));
                    if (html.Length == 0) continue;
                    var (_, campaigns) = DropsParser.Parse(html, a.Id, a.Posted, a.Title, a.Url);
                    drops.Campaigns.RemoveAll(c => c.Id.StartsWith(a.Id + "-", StringComparison.Ordinal));
                    drops.Campaigns.AddRange(campaigns);
                    drops.Scanned[a.Id] = a.Title;
                    await Task.Delay(250).ConfigureAwait(false);
                }
            }
            finally
            {
                // what was read before a failure is kept
                Drops = Sorted(drops.Campaigns);
                try { File.WriteAllText(DropsPath, JsonSerializer.Serialize(drops)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                Changed?.Invoke();
            }
        }

        private void Publish(IReadOnlyList<Announcement> items)
        {
            Items = items;
            try { File.WriteAllText(ArchivePath, JsonSerializer.Serialize(items)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            Changed?.Invoke();
        }

        private static List<DropCampaign> Sorted(IEnumerable<DropCampaign> campaigns) => campaigns.OrderByDescending(c => c.Start).ToList();

        /// <summary>The window was opened: what is listed now is no longer new.</summary>
        public void MarkSeen()
        {
            if (Items.Count == 0) return;
            var newest = Items.Max(i => i.Posted.ToUnixTimeSeconds());
            if (newest <= options.Current.NewsSeenEpoch) return;
            options.Current.NewsSeenEpoch = newest;
            options.Save();
            Changed?.Invoke();
        }

        private static HttpClient CreateClient()
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("aion2-overlay");
            return http;
        }

        /// <summary>The streams live with drops (the user's own Twitch app in the settings), at most every 5 minutes; null when not set up or failed.</summary>
        public IReadOnlyList<LiveStream>? Live { get; private set; }

        public DateTimeOffset? LiveCheckedAt { get; private set; }

        public string? LiveError { get; private set; }

        public bool LiveConfigured => options.Current.TwitchClientId.Trim().Length > 0 && options.Current.TwitchClientSecret.Trim().Length > 0;

        private string? twitchToken;
        private DateTimeOffset twitchTokenUntil;
        private string? twitchTokenFor;

        public async Task RefreshLiveAsync(bool force = false)
        {
            if (!LiveConfigured)
            {
                Live = null;
                LiveError = null;
                return;
            }
            if (!force && LiveCheckedAt is { } at && DateTimeOffset.UtcNow - at < TimeSpan.FromMinutes(5)) return;
            var id = options.Current.TwitchClientId.Trim();
            var secret = options.Current.TwitchClientSecret.Trim();
            try
            {
                if (twitchToken is null || twitchTokenFor != id + secret || DateTimeOffset.UtcNow >= twitchTokenUntil)
                {
                    using var form = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["client_id"] = id, ["client_secret"] = secret, ["grant_type"] = "client_credentials",
                    });
                    using var tokenAnswer = await Http.PostAsync(LiveDrops.TokenUrl, form).ConfigureAwait(false);
                    if (!tokenAnswer.IsSuccessStatusCode)
                    {
                        LiveError = "Twitch не принял Client ID / Client Secret — проверьте их в настройках";
                        LiveCheckedAt = DateTimeOffset.UtcNow;
                        Changed?.Invoke();
                        return;
                    }
                    using var doc = JsonDocument.Parse(await tokenAnswer.Content.ReadAsStringAsync().ConfigureAwait(false));
                    twitchToken = doc.RootElement.GetProperty("access_token").GetString();
                    var seconds = doc.RootElement.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var s) ? s : 3600;
                    twitchTokenUntil = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, seconds - 300));
                    twitchTokenFor = id + secret;
                }
                using var request = new HttpRequestMessage(HttpMethod.Get, LiveDrops.StreamsUrl);
                request.Headers.Add("Client-Id", id);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", twitchToken);
                using var answer = await Http.SendAsync(request).ConfigureAwait(false);
                if ((int)answer.StatusCode == 401) twitchToken = null;
                answer.EnsureSuccessStatusCode();
                Live = LiveDrops.Parse(await answer.Content.ReadAsStringAsync().ConfigureAwait(false));
                LiveError = null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
            {
                logger.LogDebug(ex, "Live drops could not be loaded");
                LiveError = "Не удалось получить стримы с Twitch — попробую ещё раз позже";
            }
            LiveCheckedAt = DateTimeOffset.UtcNow;
            Changed?.Invoke();
        }

        /// <summary>drops.json: the notices already read (id → their title then) and the campaigns found.</summary>
        private sealed class DropsFile
        {
            /// <summary>Below <see cref="ParserVersion"/>: read every notice again (the reader learned icons and chest contents).</summary>
            public int Version { get; set; }
            public Dictionary<string, string> Scanned { get; set; } = [];
            public List<DropCampaign> Campaigns { get; set; } = [];
        }
    }
}
