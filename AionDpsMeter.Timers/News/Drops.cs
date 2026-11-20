// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace AionDpsMeter.Timers.News;

/// <summary>One step of a drops campaign: watch this long, get this many of the item (names as NC writes them, in English).</summary>
public sealed record DropReward(int Minutes, string Name, int Qty);

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
        Spaces.Replace(WebUtility.HtmlDecode(Tags.Replace(html, " ")).Replace(' ', ' '), " ").Trim();

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
                campaigns.Add(new DropCampaign($"{id}-{campaigns.Count}", Name(title), period.Start, period.End, claim, rewards, url));
            at = Math.Max(end, at + 10);
        }
        return (claim, campaigns);
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
            var n = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var minutes = m.Groups[2].Value.StartsWith("m", StringComparison.OrdinalIgnoreCase) ? n : n * 60;
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
