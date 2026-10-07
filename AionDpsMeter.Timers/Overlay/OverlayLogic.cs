using System.Globalization;

namespace AionDpsMeter.Timers.Overlay;

/// <summary>Effective DPS: damage over the whole fight (upstream's number). Active: over the player's own time from
/// first to last hit, so time spent dead or running does not lower it.</summary>
public enum DpsMode { Effective, Active }

public static class DpsMath
{
    public static double Active(long totalDamage, DateTime firstHit, DateTime lastHit)
    {
        if (totalDamage <= 0) return 0;
        var seconds = Math.Max(1, (lastHit - firstHit).TotalSeconds);
        return totalDamage / seconds;
    }
}

public sealed record SummaryLine(string Name, double Dps, double Percent);

/// <summary>The fight as text for the game chat.</summary>
public static class FightSummary
{
    public static string Format(string target, TimeSpan duration, IEnumerable<SummaryLine> players, bool multiline)
    {
        var head = $"{target} {(int)duration.TotalMinutes}:{duration.Seconds:00}";
        var lines = players.Select(p => $"{p.Name} {Short(p.Dps)}/s {p.Percent.ToString("0", CultureInfo.InvariantCulture)}%");
        return multiline
            ? string.Join("\n", new[] { head }.Concat(lines))
            : string.Join(" · ", new[] { head }.Concat(lines));
    }

    public static string Short(double value) => value switch
    {
        >= 1_000_000_000 => (value / 1_000_000_000).ToString("0.##", CultureInfo.InvariantCulture) + "B",
        >= 1_000_000 => (value / 1_000_000).ToString("0.##", CultureInfo.InvariantCulture) + "M",
        >= 1_000 => (value / 1_000).ToString("0.##", CultureInfo.InvariantCulture) + "K",
        _ => value.ToString("0", CultureInfo.InvariantCulture),
    };
}

/// <summary>"Only over the game": the overlay is topmost while AION 2 (or the meter itself, e.g. while dragging it)
/// is the foreground window; otherwise other programs may cover it.</summary>
public static class OverlayFocus
{
    private static readonly string[] Ours = ["AION2", "AionDpsMeter.UI", "msedgewebview2"];

    public static bool StaysOnTop(string? foregroundProcessName) =>
        foregroundProcessName is not null && Ours.Any(n => string.Equals(n, foregroundProcessName, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Turns a key pressed in the settings page into upstream's hotkey syntax ("Ctrl+Shift+R").</summary>
public static class HotkeyText
{
    /// <param name="key">The browser's KeyboardEvent.key.</param>
    /// <returns>The hotkey; "" for Escape (no hotkey); null while only a modifier is held.</returns>
    public static string? FromKey(string key, bool ctrl, bool shift, bool alt)
    {
        if (key is "Escape") return "";
        if (key is "Control" or "Shift" or "Alt" or "Meta") return null;
        string main;
        if (key.Length == 1 && char.IsLetter(key[0])) main = key.ToUpperInvariant();
        else if (key.Length == 1 && char.IsDigit(key[0])) main = "D" + key; // WPF's Key names digits D0–D9
        else if (key.Length is >= 2 and <= 3 && key[0] == 'F' && int.TryParse(key[1..], out var f) && f is >= 1 and <= 24) main = key;
        else return null;
        var parts = new List<string>();
        if (ctrl) parts.Add("Ctrl");
        if (shift) parts.Add("Shift");
        if (alt) parts.Add("Alt");
        parts.Add(main);
        return string.Join("+", parts);
    }
}
