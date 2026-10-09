using AionDpsMeter.Timers.Overlay;

namespace AionDpsMeter.Timers.Tests.Overlay;

public class PowerColumnTests
{
    [Theory]
    [InlineData(PowerColumn.CombatPower, "100.11K", 2650, "100.11K")]
    [InlineData(PowerColumn.GearScore, "100.11K", 2650, "2650")]
    [InlineData(PowerColumn.Both, "100.11K", 2650, "2650 · 100.11K")]
    [InlineData(PowerColumn.None, "100.11K", 2650, "")]
    public void Shows_what_the_user_picked(PowerColumn mode, string combatPower, int gearScore, string expected) =>
        Assert.Equal(expected, PowerText.Format(mode, combatPower, gearScore));

    /// <summary>The game sends gear score only for party members; combat power can be missing too.</summary>
    [Theory]
    [InlineData(PowerColumn.GearScore, "100.11K", 0, "")]
    [InlineData(PowerColumn.Both, "100.11K", 0, "100.11K")]
    [InlineData(PowerColumn.Both, "0", 2650, "2650")]
    [InlineData(PowerColumn.CombatPower, "", 2650, "")]
    public void Leaves_out_what_is_unknown(PowerColumn mode, string combatPower, int gearScore, string expected) =>
        Assert.Equal(expected, PowerText.Format(mode, combatPower, gearScore));
}
