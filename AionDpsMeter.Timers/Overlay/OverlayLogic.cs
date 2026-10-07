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

/// <summary>Turns a key pressed in the settings page into upstream's hotkey syntax ("Ctrl+Shift+R"), by the key's place
/// on the keyboard: on a Russian layout "к" is the R key, and Windows registers hotkeys by key, not by letter.</summary>
public static class HotkeyText
{
    // Russian ЙЦУКЕН letters → the Latin letter on the same key (letters on punctuation keys are left out).
    private const string Russian = "йцукенгшщзфывапролдячсмитьЙЦУКЕНГШЩЗФЫВАПРОЛДЯЧСМИТЬ";
    private const string Latin = "QWERTYUIOPASDFGHJKLZXCVBNMQWERTYUIOPASDFGHJKLZXCVBNM";

    /// <param name="key">The browser's KeyboardEvent.key (layout-dependent).</param>
    /// <param name="code">The browser's KeyboardEvent.code (the physical key: "KeyR", "Digit1", "F9").</param>
    /// <returns>The hotkey; "" for Escape (no hotkey); null while only a modifier is held.</returns>
    public static string? FromKey(string key, string code, bool ctrl, bool shift, bool alt)
    {
        if (key is "Escape" || code is "Escape") return "";
        if (key is "Control" or "Shift" or "Alt" or "Meta") return null;
        string main;
        if (code.Length == 4 && code.StartsWith("Key", StringComparison.Ordinal)) main = code[3].ToString();
        else if (code.Length == 6 && code.StartsWith("Digit", StringComparison.Ordinal)) main = "D" + code[5]; // WPF's Key names digits D0–D9
        else if (code.Length is >= 2 and <= 3 && code[0] == 'F' && int.TryParse(code[1..], out var f) && f is >= 1 and <= 24) main = code;
        else return null;
        var parts = new List<string>();
        if (ctrl) parts.Add("Ctrl");
        if (shift) parts.Add("Shift");
        if (alt) parts.Add("Alt");
        parts.Add(main);
        return string.Join("+", parts);
    }

    /// <summary>A saved hotkey with Russian letters (written by an earlier version) → the same keys in Latin.</summary>
    public static string Normalize(string hotkey)
    {
        if (string.IsNullOrEmpty(hotkey)) return hotkey;
        var chars = hotkey.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var at = Russian.IndexOf(chars[i]);
            if (at >= 0) chars[i] = Latin[at];
        }
        return new string(chars);
    }
}
