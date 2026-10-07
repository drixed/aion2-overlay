using System.Globalization;
using System.Text.Json;

namespace AionDpsMeter.Timers.Schedule;

/// <summary>Reads schedule.json. A broken file throws; a broken event is skipped so one typo cannot hide the rest.</summary>
public static class ScheduleJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ScheduleData Parse(string json)
    {
        FileDto file;
        try
        {
            file = JsonSerializer.Deserialize<FileDto>(json, Options) ?? throw new FormatException("schedule.json is empty");
        }
        catch (JsonException ex)
        {
            throw new FormatException("schedule.json is not valid JSON", ex);
        }

        var events = new List<ScheduledEvent>();
        foreach (var dto in file.Events ?? [])
            if (TryEvent(dto, out var e)) events.Add(e);

        var maps = new Dictionary<int, FieldBossMapInfo>();
        foreach (var (key, map) in file.FieldBossMaps ?? new())
            if (int.TryParse(key, out var id) && map.Block > 0) maps[id] = new FieldBossMapInfo(map.Block, map.Name ?? "");

        var respawn = new Dictionary<int, int>();
        foreach (var (key, minutes) in file.RespawnMinutes ?? new())
            if (int.TryParse(key, out var code) && minutes > 0) respawn[code] = minutes;

        return new ScheduleData(events, maps, respawn);
    }

    private static bool TryEvent(EventDto dto, out ScheduledEvent e)
    {
        e = null!;
        if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.Name)) return false;
        if (!TryZone(dto.TimeZone ?? "UTC", out var zone)) return false;
        var duration = TimeSpan.FromMinutes(Math.Max(0, dto.DurationMinutes));
        var category = dto.Category ?? "event";

        switch (dto.Kind?.ToLowerInvariant())
        {
            case "fixed":
                var times = new List<TimeOnly>();
                foreach (var t in dto.Times ?? [])
                {
                    if (!TimeOnly.TryParseExact(t, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) return false;
                    times.Add(time);
                }
                if (times.Count == 0) return false;

                HashSet<DayOfWeek>? days = null;
                if (dto.Days is { Count: > 0 })
                {
                    days = new HashSet<DayOfWeek>();
                    foreach (var d in dto.Days)
                    {
                        if (!Enum.TryParse<DayOfWeek>(d, ignoreCase: true, out var day)) return false;
                        days.Add(day);
                    }
                }
                e = new ScheduledEvent(dto.Id, dto.Name, category, ScheduleKind.Fixed, zone, times, days, null, null, duration, dto.Verified);
                return true;

            case "interval":
                if (dto.PeriodMinutes is not > 0) return false;
                if (!DateTimeOffset.TryParse(dto.Anchor, CultureInfo.InvariantCulture, DateTimeStyles.None, out var anchor)) return false;
                e = new ScheduledEvent(dto.Id, dto.Name, category, ScheduleKind.Interval, zone, [], null,
                    anchor, TimeSpan.FromMinutes(dto.PeriodMinutes.Value), duration, dto.Verified);
                return true;

            default:
                return false;
        }
    }

    /// <summary>IANA ("Europe/Berlin") or Windows ids; "UTC" always works.</summary>
    internal static bool TryZone(string id, out TimeZoneInfo zone)
    {
        zone = TimeZoneInfo.Utc;
        if (id.Equals("UTC", StringComparison.OrdinalIgnoreCase)) return true;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId))
        {
            try
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                return true;
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { }
        }
        zone = TimeZoneInfo.Utc;
        return false;
    }

    internal sealed class FileDto
    {
        public List<EventDto>? Events { get; set; }
        public Dictionary<string, MapDto>? FieldBossMaps { get; set; }
        public Dictionary<string, int>? RespawnMinutes { get; set; }
    }

    internal sealed class EventDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Kind { get; set; }
        public string? TimeZone { get; set; }
        public List<string>? Times { get; set; }
        public List<string>? Days { get; set; }
        public string? Anchor { get; set; }
        public int? PeriodMinutes { get; set; }
        public int DurationMinutes { get; set; }
        public bool Verified { get; set; } = true;
    }

    internal sealed class MapDto
    {
        public int Block { get; set; }
        public string? Name { get; set; }
    }
}
