// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Reflection;
using System.Text.Json;
using AionDpsMeter.Services.Models;

namespace AionDpsMeter.Services.PacketProcessing.Fork
{
    /// <summary>
    /// The character's last stats packet, kept for the stat calculator. The game sends it on login and when the stats
    /// change, and upstream forgets it on every meter reset — so the calculator was usually empty. Kept across resets and
    /// restarts in player-stats.json next to the meter.
    /// </summary>
    public static class StatsMemory
    {
        private sealed record Stored(DateTime CapturedAt, Dictionary<ushort, int> Values);

        private static readonly FieldInfo? ValuesField =
            typeof(PlayerCharacterStats).GetField("Values", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly object Gate = new();
        private static PlayerStatSnapshot? last;
        private static bool loaded;

        private static string FilePath => Path.Combine(AppContext.BaseDirectory, "player-stats.json");

        /// <summary>The last stats seen, also from before a reset or a restart; null until the game sent them once.</summary>
        public static PlayerStatSnapshot? Last
        {
            get
            {
                lock (Gate)
                {
                    if (!loaded) { loaded = true; last ??= Load(); }
                    return last;
                }
            }
        }

        public static void Remember(PlayerCharacterStats stats, DateTime capturedAt)
        {
            lock (Gate)
            {
                loaded = true;
                last = PlayerStatSnapshot.Create(stats, capturedAt);
                try
                {
                    if (ValuesField?.GetValue(stats) is IReadOnlyDictionary<ushort, int> values && values.Count > 0)
                        File.WriteAllText(FilePath, JsonSerializer.Serialize(new Stored(capturedAt, values.ToDictionary())));
                }
                catch
                {
                    // the calculator still has them in memory
                }
            }
        }

        private static PlayerStatSnapshot? Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(FilePath));
                return stored?.Values is { Count: > 0 } v ? PlayerStatSnapshot.Create(new PlayerCharacterStats(v), stored.CapturedAt) : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
