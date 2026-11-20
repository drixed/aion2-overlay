// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace AionDpsMeter.Timers.News;

/// <summary>One step of a drops campaign: watch this long, get this many of the item (names as NC writes them, in English).</summary>
/// <param name="Icon">The item picture NC put in the notice's table row (NC's own CDN), shown from there.</param>
/// <param name="Contents">What a chest holds, from the notice's "Chest | Contents | Qty" table: "Item ×15, Item ×10".</param>
public sealed record DropReward(int Minutes, string Name, int Qty, string? Icon = null, string? Contents = null);

/// <summary>A live AION 2 stream with drops on.</summary>
public sealed record LiveStream(string Login, string Name, int Viewers, string Title, string Language);

/// <summary>
/// What is live with drops right now: Twitch's streams list for the AION 2 category (Helix API, an app token of the user's own
/// Twitch app), the streams tagged "Drops…" (localised: DropsEnabled, DropsВключены …). The category id and the tag rule from
/// SkeeveAN/Aion-DPS-Meter (MIT, twitchDrops.ts liveDrops).
/// </summary>
public static class LiveDrops
{
    public const string GameId = "771448419";
    public const string TokenUrl = "https://id.twitch.tv/oauth2/token";
    public const string StreamsUrl = "https://api.twitch.tv/helix/streams?game_id=" + GameId + "&first=100";

    public static IReadOnlyList<LiveStream> Parse(string json)
    {
        var list = new List<LiveStream>();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != System.Text.Json.JsonValueKind.Array) return list;
            foreach (var x in data.EnumerateArray())
            {
                if (!x.TryGetProperty("tags", out var tags) || tags.ValueKind != System.Text.Json.JsonValueKind.Array) continue;
                if (!tags.EnumerateArray().Any(t => t.ValueKind == System.Text.Json.JsonValueKind.String && t.GetString()!.StartsWith("drops", StringComparison.OrdinalIgnoreCase))) continue;
                list.Add(new LiveStream(Str(x, "user_login"), Str(x, "user_name"), x.TryGetProperty("viewer_count", out var v) && v.TryGetInt32(out var n) ? n : 0,
                    Str(x, "title"), Str(x, "language")));
            }
        }
        catch (System.Text.Json.JsonException) { return []; }
        return list.Where(s => s.Login.Length > 0).OrderByDescending(s => s.Viewers).ToList();
    }

    private static string Str(System.Text.Json.JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() ?? "" : "";
}

/// <param name="ClaimBy">The last moment the drops can be claimed in the Twitch inventory, when the notice says it.</param>
public sealed record DropCampaign(string Id, string Name, DateTimeOffset Start, DateTimeOffset End, DateTimeOffset? ClaimBy,
    IReadOnlyList<DropReward> Rewards, string Url);

/// <summary>
/// The Twitch Drops campaigns in an NC notice: a "Watch Time | Reward | Qty" table, its period from the "Event Schedule" right
/// before it (Pacific time, "PDT/PST:" or "PST:" in front, the zone after each time or not), and "Claim … before …".
/// Drops can be a part of another notice ("Ludra Awakens"), so any notice body may have them. Pattern ideas from
/// SkeeveAN/Aion-DPS-Meter (MIT, twitchDrops.ts parseDropsNotice), written anew here.
/// </summary>
public static class DropsParser
{
    private const string Month = "January|February|March|April|May|June|July|August|September|October|November|December";

    private static readonly Regex Tags = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex Row = new(@"(\d{1,3})\s*(minutes?|mins?|hours?|hrs?)\s+(.{2,90}?)\s+x\s?([\d,]{1,7})(?!\d)(?!\s*\()",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Date = new($@"({Month})\s+(\d{{1,2}}),\s*(\d{{4}})\s+at\s+(\d{{1,2}}):(\d{{2}})\s*(AM|PM)?\s*(PDT|PST|PT)?\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Schedule = new(@"Event Schedule\s*(?:(PDT/PST|PST/PDT|PDT|PST|PT)\s*:)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Claim = new($@"Claim[^.]{{0,90}}?before\s+(?=(?:{Month})\s)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TitleCut = new($@"\s*(?::|\(|\||\s-\s|\bon\s+(?:{Month})\b|\b(?:{Month})\s+\d).*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TitleNoise = new(@"\b(?:global|aion\s*2|twitch\s*drops?|twitch|event)\b|\[[^\]]*\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The page text of a notice body: no tags, entities decoded, one space between words.</summary>
    public static string Text(string html) =>
        Spaces.Replace(WebUtility.HtmlDecode(Tags.Replace(html, " ")).Replace(' ', ' ').Replace("\u200B", ""), " ").Trim();

    public static (DateTimeOffset? ClaimBy, IReadOnlyList<DropCampaign> Campaigns) Parse(string html, string id, DateTimeOffset posted, string title,
        string url = "")
    {
        var text = Text(html);
        var claim = Claim.Match(text) is { Success: true } cm && Date.Match(text, cm.Index + cm.Length) is { Success: true } dm && dm.Index == cm.Index + cm.Length
            ? Pacific(dm, null)
            : null;
        var campaigns = new List<DropCampaign>();
        var at = 0;
        while ((at = text.IndexOf("Watch Time", at, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var rewards = Rows(text, at, out var end);
            if (rewards.Count > 0 && Period(text, at) is { } period)
                campaigns.Add(new DropCampaign($"{id}-{campaigns.Count}", Name(title), period.Start, period.End, claim, Enrich(rewards, html), url));
            at = Math.Max(end, at + 10);
        }
        return (claim, campaigns);
    }

    private static readonly Regex TableRow = new(@"<tr\b[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Cell = new(@"<t([dh])\b[^>]*>(.*?)</t[dh]>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Img = new(@"<img\b[^>]*?\bsrc=""(https://[a-z0-9.-]+\.playnccdn\.com/[^""\s<>]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex QtyCell = new(@"^x\s?([\d,]{1,7})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TimeCell = new(@"^(\d{1,3})\s*(minutes?|mins?|hours?|hrs?)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The rewards with what the notice's tables add: the icon in the reward's row, and a chest's contents.</summary>
    private static List<DropReward> Enrich(List<DropReward> rewards, string html)
    {
        var icons = new Dictionary<(int, string), string>();
        var contents = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var inChests = false;
        string? chest = null;
        foreach (Match tr in TableRow.Matches(html))
        {
            var cells = Cell.Matches(tr.Groups[1].Value).Select(c => (Header: c.Groups[1].Value is "h" or "H", Text: Text(c.Groups[2].Value))).ToList();
            if (cells.Count == 0) continue;
            if (cells.Any(c => c.Header))
            {
                inChests = cells.Any(c => c.Text.Equals("Contents", StringComparison.OrdinalIgnoreCase));
                chest = null;
                continue;
            }
            var t = cells.Select(c => c.Text).ToList();
            if (t.Count >= 3 && TimeCell.Match(t[0]) is { Success: true } time && Img.Match(tr.Value) is { Success: true } img)
                icons.TryAdd((Minutes(time), t[1].ToLowerInvariant()), img.Groups[1].Value);
            if (!inChests) continue;
            if (t.Count >= 3 && QtyCell.Match(t[2]) is { Success: true } q3 && t[0].Length > 0)
            {
                chest = t[0];
                Add(chest, t[1], q3);
            }
            else if (chest is not null && t.Count >= 2 && QtyCell.Match(t[1]) is { Success: true } q2)
                Add(chest, t[0], q2);
        }
        return rewards.Select(r => r with
        {
            Icon = icons.GetValueOrDefault((r.Minutes, r.Name.ToLowerInvariant())),
            Contents = contents.TryGetValue(r.Name, out var inside) ? string.Join(", ", inside) : null,
        }).ToList();

        void Add(string box, string item, Match qty)
        {
            if (item.Length == 0) return;
            if (!contents.TryGetValue(box, out var list)) contents[box] = list = [];
            list.Add($"{item} ×{qty.Groups[1].Value}");
        }
    }

    private static int Minutes(Match time)
    {
        var n = int.Parse(time.Groups[1].Value, CultureInfo.InvariantCulture);
        return time.Groups[2].Value.StartsWith("m", StringComparison.OrdinalIgnoreCase) ? n : n * 60;
    }

    /// <summary>The table rows right after the header, one after another; the first gap ends the table.</summary>
    private static List<DropReward> Rows(string text, int header, out int end)
    {
        var rows = new List<DropReward>();
        var pos = header + "Watch Time".Length;
        var head = text.IndexOf("Qty", pos, StringComparison.OrdinalIgnoreCase);
        if (head >= 0 && head - pos < 30) pos = head + 3;
        end = pos;
        for (var m = Row.Match(text, pos); m.Success; m = m.NextMatch())
        {
            if (text.AsSpan(end, m.Index - end).Trim().Length > 0) break;
            var minutes = Minutes(m);
            var qty = int.Parse(m.Groups[4].Value.Replace(",", ""), CultureInfo.InvariantCulture);
            rows.Add(new DropReward(minutes, m.Groups[3].Value.Trim(), qty));
            end = m.Index + m.Length;
        }
        return rows;
    }

    /// <summary>The "Event Schedule" closest before the table: its first two dates.</summary>
    private static (DateTimeOffset Start, DateTimeOffset End)? Period(string text, int table)
    {
        Match? schedule = null;
        for (var m = Schedule.Match(text); m.Success && m.Index < table; m = m.NextMatch()) schedule = m;
        if (schedule is null) return null;
        var label = schedule.Groups[1].Value.ToUpperInvariant();
        var first = Date.Match(text, schedule.Index + schedule.Length);
        if (!first.Success || first.Index > table) return null;
        var second = first.NextMatch();
        if (!second.Success || second.Index > table) return null;
        return Pacific(first, label) is { } s && Pacific(second, label) is { } e && e > s ? (s, e) : null;
    }

    /// <summary>A Pacific date to UTC: the zone after the time, else the label's ("PST:"), else whichever is in force that day.</summary>
    private static DateTimeOffset? Pacific(Match m, string? label)
    {
        if (!DateTime.TryParseExact($"{m.Groups[1].Value} {m.Groups[2].Value} {m.Groups[3].Value}", "MMMM d yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var day)) return null;
        var hour = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
        var minute = int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture);
        var ampm = m.Groups[6].Value.ToUpperInvariant();
        if (ampm == "PM" && hour < 12) hour += 12;
        if (ampm == "AM" && hour == 12) hour = 0;
        if (hour > 23 || minute > 59) return null;
        var local = day.AddHours(hour).AddMinutes(minute);
        var zone = m.Groups[7].Value.ToUpperInvariant();
        if (zone is not ("PDT" or "PST")) zone = label is "PDT" or "PST" ? label : SummerTime(local) ? "PDT" : "PST";
        return new DateTimeOffset(local, TimeSpan.FromHours(zone == "PDT" ? -7 : -8)).ToUniversalTime();
    }

    /// <summary>US daylight saving: from the second Sunday of March to the first Sunday of November, 2:00.</summary>
    private static bool SummerTime(DateTime local)
    {
        static DateTime Sunday(int year, int month, int nth)
        {
            var d = new DateTime(year, month, 1);
            while (d.DayOfWeek != DayOfWeek.Sunday) d = d.AddDays(1);
            return d.AddDays(7 * (nth - 1)).AddHours(2);
        }
        return local >= Sunday(local.Year, 3, 2) && local < Sunday(local.Year, 11, 1);
    }

    /// <summary>"Global Halloween Twitch Drops: October 28 - …" → "Halloween"; "Ludra Awakens on November 4th" → "Ludra Awakens".</summary>
    public static string Name(string title)
    {
        var name = Spaces.Replace(TitleNoise.Replace(TitleCut.Replace(title, ""), " "), " ").Trim(' ', '-', '|', ':');
        return name.Length > 0 ? name : "Twitch Drops";
    }
}
