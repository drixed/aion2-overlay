// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AionDpsMeter.Timers.News;

public enum NewsKind { Maintenance, Outage, NewServers, ServerTransfer, Compensation, Account, TwitchDrops, KnownIssues, Other }

/// <param name="Done">The title ends in "(Completed)" / "(Resolved)": the maintenance or outage is over.</param>
public sealed record Announcement(string Id, string Title, string Summary, DateTimeOffset Posted, NewsKind Kind, bool Done, string Url);

/// <summary>
/// NCSOFT's official AION 2 announcements from the public community board (no login): the newest ten, English only (NC
/// publishes no Russian ones). NC has no category field, so the kind is read from the title.
/// </summary>
public static class Announcements
{
    public const string ListUrl = "https://api-global-community.plaync.com/aion2_global/board/notice_en/article";
    private const string PagePattern = "https://aion2.plaync.com/en-us/board/notice/view?articleId={articleId}";

    // Order matters: "compensation for the outage" is a compensation, "server transfer" is not a new server.
    private static readonly (NewsKind Kind, string[] Words)[] Keywords =
    [
        (NewsKind.TwitchDrops, ["twitch"]),
        (NewsKind.KnownIssues, ["known issue"]),
        (NewsKind.Compensation, ["compensation"]),
        (NewsKind.Account, ["account restriction", "restrictions"]),
        (NewsKind.ServerTransfer, ["server transfer", "character transfer"]),
        (NewsKind.NewServers, ["new server"]),
        (NewsKind.Maintenance, ["maintenance"]),
        (NewsKind.Outage, ["outage", "affected by", "connection issue", "instability"]),
    ];

    public static NewsKind KindOf(string title)
    {
        var t = title.ToLowerInvariant();
        foreach (var (kind, words) in Keywords)
            if (words.Any(t.Contains)) return kind;
        return NewsKind.Other;
    }

    public static string Label(NewsKind kind) => kind switch
    {
        NewsKind.Maintenance => "Техработы",
        NewsKind.Outage => "Сбой",
        NewsKind.NewServers => "Новые серверы",
        NewsKind.ServerTransfer => "Перенос",
        NewsKind.Compensation => "Компенсация",
        NewsKind.Account => "Блокировки",
        NewsKind.TwitchDrops => "Twitch Drops",
        NewsKind.KnownIssues => "Известные ошибки",
        _ => "Новости",
    };

    private static readonly Regex Tag = new(@"^\s*\[[^\]]{1,24}\]\s*", RegexOptions.Compiled);
    private static readonly Regex DoneMark = new(@"\s*[(（]\s*(completed|resolved|complete|done)\s*[.]?\s*[)）]\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    /// <summary>The board's list, newest first; entries without an id or a date are left out. Broken text gives nothing.</summary>
    public static IReadOnlyList<Announcement> Parse(string json)
    {
        var items = new List<Announcement>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("contentList", out var list) || list.ValueKind != JsonValueKind.Array) return items;
            foreach (var x in list.EnumerateArray())
            {
                var id = Text(x, "id");
                var raw = Text(x, "title");
                if (id.Length == 0 || raw.Length == 0) continue;
                if (!x.TryGetProperty("timestamps", out var ts) || !DateTimeOffset.TryParse(Text(ts, "postedAt"), CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var posted)) continue;
                var pattern = x.TryGetProperty("categoryBoard", out var board) && Text(board, "boardUrlPattern") is { Length: > 0 } p ? p : PagePattern;
                var done = DoneMark.IsMatch(raw);
                var title = Spaces.Replace(DoneMark.Replace(Tag.Replace(raw, ""), ""), " ").Trim();
                var summary = Spaces.Replace(WebUtility.HtmlDecode(Text(x, "summary")).Replace(' ', ' '), " ").Trim();
                items.Add(new Announcement(id, title, summary, posted, KindOf(raw), done, pattern.Replace("{articleId}", Uri.EscapeDataString(id))));
            }
        }
        catch (JsonException)
        {
            return [];
        }
        return items.OrderByDescending(i => i.Posted).ToList();
    }

    private static string Text(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
