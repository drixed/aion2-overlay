namespace AionDpsMeter.Timers.Overlay;

/// <summary>What the main window shows next to a player's name: БМ (combat power), ГС (gear score), both or nothing.</summary>
public enum PowerColumn { CombatPower, GearScore, Both, None }

public static class PowerText
{
    /// <param name="combatPower">Upstream's formatted combat power ("100.11K"; "" or "0" when unknown).</param>
    /// <param name="gearScore">0 when unknown: the game sends it only for party members.</param>
    public static string Format(PowerColumn mode, string combatPower, int gearScore)
    {
        var cp = string.IsNullOrWhiteSpace(combatPower) || combatPower == "0" ? "" : combatPower;
        var gs = gearScore > 0 ? gearScore.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
        return mode switch
        {
            PowerColumn.CombatPower => cp,
            PowerColumn.GearScore => gs,
            PowerColumn.Both => string.Join(" · ", new[] { gs, cp }.Where(s => s.Length > 0)),
            _ => "",
        };
    }
}
