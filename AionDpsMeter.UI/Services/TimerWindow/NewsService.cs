// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.IO;
using System.Net.Http;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.News;
using Microsoft.Extensions.Logging;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>
    /// NCSOFT's announcements for the 📣 window: one request to the public board every 15 minutes (while "Объявления"
    /// is on), the last answer kept in news-cache.json so the window is filled at once after a restart. "Новое" is what
    /// was posted after the window was last opened.
    /// </summary>
    public sealed class NewsService(TimersOptionsStore options, ILogger<NewsService> logger)
    {
        private static readonly TimeSpan Every = TimeSpan.FromMinutes(15);
        private static readonly HttpClient Http = CreateClient();
        private System.Threading.Timer? timer;
        private bool started;

        public IReadOnlyList<Announcement> Items { get; private set; } = [];

        public event Action? Changed;

        private static string CachePath => Path.Combine(AppContext.BaseDirectory, "news-cache.json");

        public bool HasNew => options.Current.ShowNews && Items.Any(IsNew);

        public bool IsNew(Announcement a) => a.Posted.ToUnixTimeSeconds() > options.Current.NewsSeenEpoch;

        public void Start()
        {
            if (started) return;
            started = true;
            try
            {
                if (File.Exists(CachePath)) Items = Announcements.Parse(File.ReadAllText(CachePath));
            }
            catch (IOException) { }
            timer = new System.Threading.Timer(_ => _ = RefreshAsync(), null, TimeSpan.FromSeconds(15), Every);
        }

        public async Task RefreshAsync()
        {
            if (!options.Current.ShowNews) return;
            try
            {
                var json = await Http.GetStringAsync(Announcements.ListUrl).ConfigureAwait(false);
                var items = Announcements.Parse(json);
                if (items.Count == 0) return; // a changed or broken answer keeps what we had
                Items = items;
                File.WriteAllText(CachePath, json);
                Changed?.Invoke();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                logger.LogDebug(ex, "Announcements could not be loaded");
            }
        }

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
    }
}
