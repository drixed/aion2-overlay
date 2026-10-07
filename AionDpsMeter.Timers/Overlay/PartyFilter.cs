namespace AionDpsMeter.Timers.Overlay;

/// <param name="CharacterLevel">Upstream fills it only from the party packet, so it marks party members.</param>
public sealed record PartyRow(long PlayerId, bool IsUser, int CharacterLevel, long Damage);

/// <summary>"Only my party": me plus the players the party packet named; shares and bars are recomputed within them.</summary>
public static class PartyFilter
{
    public static bool IsMember(PartyRow row) => row.IsUser || row.CharacterLevel > 0;

    /// <summary>Player id → percent of the party's damage.</summary>
    public static IReadOnlyDictionary<long, double> Shares(IEnumerable<PartyRow> rows)
    {
        var members = rows.Where(IsMember).ToList();
        var total = members.Sum(m => (double)m.Damage);
        return members.ToDictionary(m => m.PlayerId, m => total > 0 ? m.Damage / total * 100 : 0);
    }

    /// <summary>Player id → bar width, the top member at 100%.</summary>
    public static IReadOnlyDictionary<long, double> Bars(IEnumerable<PartyRow> rows)
    {
        var members = rows.Where(IsMember).ToList();
        var top = members.Count > 0 ? members.Max(m => (double)m.Damage) : 0;
        return members.ToDictionary(m => m.PlayerId, m => top > 0 ? m.Damage / top * 100 : 0);
    }
}
