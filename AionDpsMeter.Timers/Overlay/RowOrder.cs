namespace AionDpsMeter.Timers.Overlay;

/// <summary>Order of the player rows: by damage (as given), optionally my row next to the header and/or first place at
/// the bottom. Each row keeps its rank by damage.</summary>
public static class RowOrder
{
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
