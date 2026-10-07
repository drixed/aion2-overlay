// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Globalization;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>The system culture, but with a dot as the decimal separator: "99.38M", not "99,38M" (asked for by
    /// the user). Applies to upstream's numbers too; dates and everything else keep the system's format.</summary>
    public static class ForkCulture
    {
        public static void Apply()
        {
            var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = ".";
            culture.NumberFormat.PercentDecimalSeparator = ".";
            culture.NumberFormat.CurrencyDecimalSeparator = ".";
            CultureInfo.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
        }
    }
}
