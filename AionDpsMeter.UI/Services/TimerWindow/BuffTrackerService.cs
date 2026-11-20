// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Windows;
using System.Windows.Threading;
using AionDpsMeter.Core.Models;
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Services.Services.Session;
using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Buffs;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.UI.Pages;
using AionDpsMeter.UI.Services.Windowing;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>
    /// "Баффы": the player's own buffs from the packets, the panel with the active preset of the played class (shown in
    /// boss fights, every fight or always), and a sound or a spoken name when a watched buff runs out while it shows.
    /// </summary>
    public sealed class BuffTrackerService(TimersOptionsStore options, EntityTracker entities, CombatSessionManager sessions, BossCatalog catalog,
        AlertSoundPlayer sounds, IWindowManagerService windowManager)
    {
        /// <summary>Not in upstream's WindowKey enum, after the announcements (1011).</summary>
        public static readonly WindowKey Key = (WindowKey)1012;

        public BuffTracker Tracker { get; } = new();

        /// <summary>The panel can be dragged ("Расставить").</summary>
        public bool Editing { get; private set; }

        public event Action? Changed;

        /// <summary>Whether the panel is on screen now (for the settings page).</summary>
        public bool Showing => window is { IsVisible: true };

        private Window? window;
        private DispatcherTimer? timer;
        private int savedSeen;
        private DateTime lastSave;
        private bool started;

        public void Start()
        {
            if (started) return;
            started = true;
            Tracker.Remember(options.Current.SeenBuffs);
            savedSeen = Tracker.Seen().Count;
            CombatFeed.Buff += OnBuff;
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += (_, _) => Tick();
            timer.Start();
        }

        private Player? Me => entities.PlayerEntities.FirstOrDefault(p => p.IsUser);

        /// <summary>The played character's class (11–19), or null before the meter knows who you are.</summary>
        public int? MyClass => Me?.CharacterClass is { } c ? (int)c.Id : null;

        public BuffPreset? ActivePreset
        {
            get
            {
                if ((MyClass ?? options.Current.BuffClass) is not { } cls) return null;
                var mine = options.Current.BuffPresets.Where(p => p.ClassId == cls).ToList();
                return options.Current.ActiveBuffPreset.TryGetValue(cls, out var id) && mine.FirstOrDefault(p => p.Id == id) is { } chosen
                    ? chosen : mine.FirstOrDefault();
            }
        }

        private void OnBuff(BuffEvent e)
        {
            if (Me is not { } me || e.EntityId != me.Id) return;
            Tracker.OnBuff(e.BuffId, e.BuffName, e.BuffIcon, e.DurationMs, DateTimeOffset.Now);
        }

        public void ToggleEditing()
        {
            Editing = !Editing;
            if (window is not null)
            {
                if (Editing) windowManager.RestoreClickThrough(Key);
                else windowManager.SetClickThrough(Key);
            }
            Tick();
            Changed?.Invoke();
        }

        public void Drag()
        {
            if (!Editing || window is null) return;
            windowManager.Drag(Key);
            options.Current.BuffPanelLeft = window.Left;
            options.Current.BuffPanelTop = window.Top;
            options.Save();
        }

        /// <summary>A sound and/or the buff's name, as set in "Баффы".</summary>
        public void Announce(string name)
        {
            var o = options.Current;
            if (o.BuffSound) sounds.Play(AlertSound.Parse(null), o.BuffVolume);
            if (o.BuffSpeak) Speech.Say($"{name} спал", o.BuffVoice, o.BuffVolume);
        }

        private void Tick()
        {
            var o = options.Current;
            var preset = ActivePreset;
            var inFight = sessions.GetActiveTargetSession() is not null;
            var bossOrDummy = sessions.GetActiveTargetInfo() is { MobCode: > 0 } mob && (catalog.IsBoss(mob.MobCode) || catalog.IsDummy(mob.MobCode));
            // On screen whenever it should show, even with an empty preset (it then says what to do); "Расставить" always.
            var show = o.BuffTrackersOn && (Editing || BuffTracker.Shows(o.BuffTrackersShow, inFight, bossOrDummy));

            if (preset is not null)
                foreach (var gone in Tracker.Expired(preset, DateTimeOffset.Now))
                    if (show && !Editing) Announce(gone.Name);

            var was = Showing;
            if (show) ShowPanel(preset);
            else if (window is { IsVisible: true }) window.Hide();
            if (was != Showing) Changed?.Invoke();

            var seen = Tracker.Seen();
            if (seen.Count != savedSeen && DateTime.Now - lastSave > TimeSpan.FromSeconds(30))
            {
                savedSeen = seen.Count;
                lastSave = DateTime.Now;
                o.SeenBuffs = seen.ToList();
                options.Save();
            }
        }

        private void ShowPanel(BuffPreset? preset)
        {
            var o = options.Current;
            var size = Math.Clamp(o.BuffIconSize, 24, 80);
            var count = preset?.BuffIds.Count ?? 0;
            var width = count == 0 ? 280 : count * (size + 6) + 16;
            var height = size + 36;
            if (window is null)
            {
                window = new BlazorWindow(App.AppHost.Services, typeof(BuffPanelPage))
                {
                    ShowInTaskbar = false,
                    Title = "AION2 Баффы",
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = o.BuffPanelLeft ?? 640,
                    Top = o.BuffPanelTop ?? 160,
                    Width = width,
                    Height = height,
                };
                windowManager.Open(Key, window, isSingleton: true);
                if (!Editing) windowManager.SetClickThrough(Key);
            }
            if (window.Width != width) window.Width = width;
            if (window.Height != height) window.Height = height;
            if (!window.IsVisible) window.Show();
        }
    }
}
