namespace AionDpsMeter.Timers.Overlay;

/// <summary>Order of the player rows: by damage (as given), optionally my row next to the header and/or first place at
/// the bottom. Each row keeps its rank by damage.</summary>
public static class RowOrder
{
    /// <summary>The first <paramref name="max"/> rows by damage; my row takes the last place when it is below them.</summary>
    public static IReadOnlyList<T> Limit<T>(IReadOnlyList<T> byDamage, Func<T, bool> isMe, int max)
    {
        if (byDamage.Count <= max) return byDamage;
        var top = byDamage.Take(max).ToList();
        if (!top.Any(isMe) && byDamage.FirstOrDefault(isMe) is { } mine) top[^1] = mine;
        return top;
    }

    public static IReadOnlyList<(T Row, int Rank)> Apply<T>(IEnumerable<T> byDamage, Func<T, bool> isMe, bool meOnTop, bool reverse)
    {
        var ranked = byDamage.Select((row, i) => (Row: row, Rank: i + 1)).ToList();
        if (reverse) ranked.Reverse();
        if (!meOnTop) return ranked;
        var mine = ranked.Where(r => isMe(r.Row)).ToList();
        var others = ranked.Where(r => !isMe(r.Row)).ToList();
        // "on top" means next to the header: the end of the list when first place is at the bottom.
        return reverse ? [.. others, .. mine] : [.. mine, .. others];
    }
}
