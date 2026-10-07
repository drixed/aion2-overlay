// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Overlay;
using AionDpsMeter.UI.Services.TimerWindow;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AionDpsMeter.UI.Pages
{
    public partial class OverlaySettingsPage(TimersOptionsStore store, OverlaySettingsWindow window) : ComponentBase
    {
        private sealed record HotkeyRow(string Id, string Title, Func<string> Get);

        private string? capturing;

        private TimersOptions O => store.Current;

        private IReadOnlyList<HotkeyRow> Hotkeys =>
        [
            new("reset", "Сбросить бой", () => O.ResetFightHotkey),
            new("copy", "Копировать сводку", () => O.CopySummaryHotkey),
        ];

        private string Label(HotkeyRow row) =>
            capturing == row.Id ? "Нажми сочетание…" : HasHotkey(row) ? row.Get() : "Не задано";

        private static bool HasHotkey(HotkeyRow row) => !string.IsNullOrEmpty(row.Get());

        private void Drag() => window.Drag();
        private void Close() => window.Close();

        private void Set(Action<TimersOptions> change)
        {
            change(O);
            store.Save();
        }

        private void SetScale(object? value)
        {
            if (int.TryParse(value?.ToString(), out var percent)) Set(o => o.UiScale = Math.Clamp(percent, 80, 150));
        }

        private void OnKey(string id, KeyboardEventArgs e)
        {
            if (capturing != id) return;
            var text = HotkeyText.FromKey(e.Key, e.CtrlKey, e.ShiftKey, e.AltKey);
            if (text is null) return; // a modifier alone: keep waiting
            SetHotkey(id, text);
            capturing = null;
        }

        private void SetHotkey(string id, string text) => Set(o =>
        {
            if (id == "reset") o.ResetFightHotkey = text;
            else o.CopySummaryHotkey = text;
        });
    }
}
