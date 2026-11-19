// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Windows;
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.UI.Pages;
using AionDpsMeter.UI.Services.Windowing;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>The damage tab's views in their own windows, like the reference meter: "Хронология умений",
    /// "Хронология баффов" and "Баффы по умениям". One window per view; asking again shows another fight or player in it.</summary>
    public sealed class HistoryViewWindow(IWindowManagerService windowManager, WindowBoundsKeeper bounds)
    {
        public const string Rotation = "rotation";
        public const string Buffs = "buffs";
        public const string PerSkill = "per-skill";

        /// <summary>Not in upstream's WindowKey enum, next to the history browser (1006).</summary>
        private static WindowKey KeyOf(string view) => (WindowKey)(view switch { Rotation => 1007, Buffs => 1008, _ => 1009 });

        public static string Title(string view) => view switch
        {
            Rotation => "Хронология умений",
            Buffs => "Хронология баффов",
            _ => "Баффы по умениям",
        };

        private readonly Dictionary<string, (Guid? Session, long Player)> requests = [];

        /// <summary>What a view shows: a saved fight (or the current one when null) and a player in it.</summary>
        public (Guid? Session, long Player) Request(string view) => requests.GetValueOrDefault(view);

        public event Action<string>? Requested;

        public void Open(string view, Guid? session, long player)
        {
            requests[view] = (session, player);
            var key = KeyOf(view);
            if (!windowManager.IsOpen(key))
            {
                var w = new BlazorWindow(App.AppHost.Services, typeof(HistoryViewPage))
                {
                    Width = view == PerSkill ? 1000 : 1150,
                    Height = view == PerSkill ? 720 : 780,
                    ShowInTaskbar = false,
                    Title = "AION2 " + Title(view),
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                };
                w.BlazorWebView.RootComponents[0].Parameters = new Dictionary<string, object?> { [nameof(HistoryViewPage.View)] = view };
                windowManager.Open(key, w, isSingleton: true, persistenceMode: WindowPersistenceMode.Bounds);
                bounds.Keep(key, w);
            }
            else
            {
                windowManager.Focus(key);
            }
            Requested?.Invoke(view);
        }

        public void Drag(string view) => windowManager.Drag(KeyOf(view));

        public void Close(string view) => windowManager.Close(KeyOf(view));
    }
}
