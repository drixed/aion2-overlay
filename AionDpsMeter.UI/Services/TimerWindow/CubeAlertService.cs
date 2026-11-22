// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Windows.Threading;
using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Map;
using AionDpsMeter.Timers.Replay;
using AionDpsMeter.Timers.Zones;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>
    /// "Куб рядом" (Карта → Прочее): when the player comes within the set distance of a hidden cube, a sound and the way to
    /// it spoken ("Скрытый куб, северо-восток, 40 метров"), with the map window closed too. Own position only; the game
    /// sends it with gaps, so the call can come a few seconds late.
    /// </summary>
    public sealed class CubeAlertService(TimersOptionsStore options, ZoneTracker zones, PositionLog positions, EntityTracker entities, AlertSoundPlayer sounds)
    {
        private readonly CubeRadar radar = new();
        private DispatcherTimer? timer;
        private DateTime lastFix;

        public void Start()
        {
            if (timer is not null) return;
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) => Tick();
            timer.Start();
        }

        private void Tick()
        {
            try
            {
                var metres = options.Current.Map.CubeAlertMetres;
                if (metres <= 0 || WorldMap.For(zones.CurrentMapId) is not { } layer) return;
                var fix = positions.Me;
                if (fix is null && entities.PlayerEntities.FirstOrDefault(p => p.IsUser) is { } user) fix = positions.Last(user.Id);
                if (fix is not { } f || f.At == lastFix) return; // only a new position
                lastFix = f.At;
                if (radar.Check(layer.Cubes, new MapPoint(f.X, f.Y), metres) is not { } near) return;
                var o = options.Current;
                sounds.Play(AlertSound.Parse(null), o.BuffVolume);
                Speech.Say($"Скрытый куб, {near.Direction}, {near.Metres} {Plural(near.Metres)}", o.BuffVoice, o.BuffVolume);
            }
            catch (Exception)
            {
                // a missed call never stops the map
            }
        }

        private static string Plural(int n) =>
            (n % 100) is >= 11 and <= 14 ? "метров" : (n % 10) switch { 1 => "метр", >= 2 and <= 4 => "метра", _ => "метров" };
    }
}
