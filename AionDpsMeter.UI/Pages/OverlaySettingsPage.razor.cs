// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Overlay;
using AionDpsMeter.UI.Services.TimerWindow;
using AionDpsMeter.UI.Services.Windowing;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AionDpsMeter.UI.Pages
{
    /// <summary>The one settings window behind ⚙: the fork's numbers, overlay, timers, hotkeys — and a way into
    /// RATmeter's own settings.</summary>
    public partial class OverlaySettingsPage(TimersOptionsStore store, OverlaySettingsWindow window, WindowHelper windowHelper) : ComponentBase
    {
        private sealed record HotkeyRow(string Id, string Title, Func<string> Get, Action<string> Set);

        private string? capturing;

        private TimersOptions O => store.Current;

        private IReadOnlyList<HotkeyRow> Hotkeys =>
        [
            new("hide", "Скрыть оверлей", () => O.HideOverlayHotkey, v => Set(o => o.HideOverlayHotkey = v)),
            new("reset", "Сбросить бой", () => O.ResetFightHotkey, v => Set(o => o.ResetFightHotkey = v)),
            new("clear", "Очистить метр", () => O.ClearMeterHotkey, v => Set(o => o.ClearMeterHotkey = v)),
            new("copy", "Копировать сводку", () => O.CopySummaryHotkey, v => Set(o => o.CopySummaryHotkey = v)),
            new("compact", "Переключить компактную полосу", () => O.CompactHotkey, v => Set(o => o.CompactHotkey = v)),
            new("click", "Переключить сквозной клик", () => O.ClickThroughHotkey, v => Set(o => o.ClickThroughHotkey = v)),
        ];

        private string Label(HotkeyRow row) =>
            capturing == row.Id ? "Нажми сочетание…" : HasHotkey(row) ? row.Get() : "Не задано";

        private static bool HasHotkey(HotkeyRow row) => !string.IsNullOrEmpty(row.Get());

        private void Drag() => window.Drag();
        private void Close() => window.Close();
        private void OpenMeterSettings() => windowHelper.OpenSettings();

        private void Set(Action<TimersOptions> change)
        {
            change(O);
            store.Save();
        }

        private void SetScale(object? value)
        {
            if (int.TryParse(value?.ToString(), out var percent)) Set(o => o.UiScale = Math.Clamp(percent, 80, 150));
        }

        private void SetLead(FeedKind kind, object? value)
        {
            if (!int.TryParse(value?.ToString(), out var minutes)) return;
            minutes = Math.Clamp(minutes, 0, 60);
            Set(o =>
            {
                switch (kind)
                {
                    case FeedKind.Boss: o.BossLeadMinutes = minutes; break;
                    case FeedKind.Rift: o.RiftLeadMinutes = minutes; break;
                    default: o.EventLeadMinutes = minutes; break;
                }
            });
        }

        private void ToggleKind(FeedKind kind, bool show) => Set(o =>
        {
            if (show) o.HiddenKinds.Remove(kind);
            else o.HiddenKinds.Add(kind);
        });

        private void ShowHidden() => Set(o => o.HiddenIds.Clear());

        private static string KindLabel(FeedKind kind) => kind switch
        {
            FeedKind.Rift => "Разломы",
            FeedKind.Boss => "Боссы",
            _ => "Ивенты",
        };

        private void OnKey(HotkeyRow row, KeyboardEventArgs e)
        {
            if (capturing != row.Id) return;
            var text = HotkeyText.FromKey(e.Key, e.CtrlKey, e.ShiftKey, e.AltKey);
            if (text is null) return; // a modifier alone: keep waiting
            row.Set(text);
            capturing = null;
        }
    }
}
