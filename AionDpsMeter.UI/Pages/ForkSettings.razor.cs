// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Overlay;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AionDpsMeter.UI.Pages
{
    /// <summary>The fork's settings as blocks of RATmeter's settings window: "appearance", "tracking", "hotkeys", "timers".</summary>
    public partial class ForkSettings(TimersOptionsStore store) : ComponentBase
    {
        private sealed record HotkeyRow(string Id, string Title, string Description, Func<string> Get, Action<string> Set);

        [Parameter] public string Section { get; set; } = "";

        private string? capturing;

        private TimersOptions O => store.Current;

        private IReadOnlyList<HotkeyRow> Hotkeys =>
        [
            new("hide", "Скрыть оверлей", "Прячет все окна метра; повторное нажатие возвращает их", () => O.HideOverlayHotkey, v => Set(o => o.HideOverlayHotkey = v)),
            new("reset", "Сбросить бой", "Завершает текущий бой (он уходит в историю)", () => O.ResetFightHotkey, v => Set(o => o.ResetFightHotkey = v)),
            new("clear", "Очистить метр", "Полностью обнуляет метр", () => O.ClearMeterHotkey, v => Set(o => o.ClearMeterHotkey = v)),
            new("copy", "Копировать сводку", "Итог боя в буфер обмена — для чата игры", () => O.CopySummaryHotkey, v => Set(o => o.CopySummaryHotkey = v)),
            new("compact", "Компактная полоса", "Включает и выключает компактную полосу", () => O.CompactHotkey, v => Set(o => o.CompactHotkey = v)),
            new("click", "Сквозной клик", "Клики проходят сквозь окна метра в игру", () => O.ClickThroughHotkey, v => Set(o => o.ClickThroughHotkey = v)),
        ];

        private string Label(HotkeyRow row) =>
            capturing == row.Id ? "Нажми сочетание…" : string.IsNullOrEmpty(row.Get()) ? "Не задано" : row.Get();

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
            FeedKind.Boss => "Полевые боссы",
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
