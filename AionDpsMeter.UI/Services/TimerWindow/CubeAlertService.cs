// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Windows;
using System.Windows.Threading;
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Map;
using AionDpsMeter.Timers.Replay;
using AionDpsMeter.Timers.Zones;
using AionDpsMeter.UI.Pages;
using AionDpsMeter.UI.Services.Windowing;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>The compass window's view: the nearest cube, how far, the arrow's angle (0 = north) and the compass point.</summary>
    public sealed record CubeCompass(MapPoint Cube, int Metres, double Bearing, string Direction, bool Stale);

    /// <summary>
    /// Hidden cubes near the player (Карта → Прочее), with the map window closed too:
    /// "Сигнал, когда куб ближе" — a sound and the way spoken once per cube ("Скрытый куб, северо-восток, 40 метров");
    /// "Окно-компас" — a small window with an arrow to the nearest cube, north up (the camera's facing is not known).
    /// Own position only; the game sends it with gaps, so both can lag a few seconds.
    /// </summary>
    public sealed class CubeAlertService(TimersOptionsStore options, ZoneTracker zones, PositionLog positions, EntityTracker entities,
        AlertSoundPlayer sounds, IWindowManagerService windowManager)
    {
        /// <summary>Not in upstream's WindowKey enum, after the boss casts (1018).</summary>
        public static readonly WindowKey CompassKey = (WindowKey)1019;

        private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(40);
        private readonly CubeRadar radar = new();
        private DispatcherTimer? timer;
        private DateTime lastFix;
        private Window? compass;

        public CubeCompass? Compass { get; private set; }

        public event Action? Changed;

        public void Start()
        {
            if (timer is not null) return;
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) => Tick();
            timer.Start();
        }

        public void DragCompass()
        {
            if (compass is null) return;
            windowManager.Drag(CompassKey);
            options.Current.Map.CompassLeft = compass.Left;
            options.Current.Map.CompassTop = compass.Top;
            options.Save();
        }

        public void TurnCompassOff()
        {
            options.Current.Map.CubeCompass = false;
            options.Save();
            Tick();
        }

        private void Tick()
        {
            try
            {
                var map = options.Current.Map;
                var layer = WorldMap.For(zones.CurrentMapId);
                var fix = positions.Me;
                if (fix is null && entities.PlayerEntities.FirstOrDefault(p => p.IsUser) is { } user) fix = positions.Last(user.Id);
                MapPoint? me = fix is { } f ? new MapPoint(f.X, f.Y) : null;

                // the compass: only on a map with cubes
                if (map.CubeCompass && layer is { Cubes.Count: > 0 })
                {
                    Compass = me is { } at && WorldMap.Nearest(layer.Cubes, at) is { } n
                        ? new CubeCompass(n.Point, n.Metres, CubeRadar.Bearing(at, n.Point), CubeRadar.Direction(at, n.Point), DateTime.Now - fix!.Value.At > StaleAfter)
                        : null;
                    ShowCompass();
                    Changed?.Invoke();
                }
                else
                {
                    Compass = null;
                    if (compass is { IsVisible: true }) compass.Hide();
                }

                // the call: once per cube, only on a new position
                if (map.CubeAlertMetres <= 0 || layer is null || fix is not { } nf || nf.At == lastFix || me is not { } pos) return;
                lastFix = nf.At;
                if (radar.Check(layer.Cubes, pos, map.CubeAlertMetres) is not { } near) return;
                var o = options.Current;
                sounds.Play(AlertSound.Parse(null), o.BuffVolume);
                Speech.Say($"Скрытый куб, {near.Direction}, {near.Metres} {Plural(near.Metres)}", o.BuffVoice, o.BuffVolume);
            }
            catch (Exception)
            {
                // a missed tick never stops the map
            }
        }

        private void ShowCompass()
        {
            if (compass is null)
            {
                var map = options.Current.Map;
                compass = new BlazorWindow(App.AppHost.Services, typeof(CubeCompassPage))
                {
                    ShowInTaskbar = false,
                    Title = "AION2 Компас к кубу",
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = map.CompassLeft ?? 80,
                    Top = map.CompassTop ?? 240,
                    Width = 210,
                    Height = 100,
                    Topmost = true,
                };
                compass.Closed += (_, _) => compass = null;
                windowManager.Open(CompassKey, compass, isSingleton: true);
            }
            if (!compass.IsVisible) compass.Show();
        }

        private static string Plural(int n) =>
            (n % 100) is >= 11 and <= 14 ? "метров" : (n % 10) switch { 1 => "метр", >= 2 and <= 4 => "метра", _ => "метров" };
    }
}
