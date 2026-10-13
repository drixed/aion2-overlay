// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Overlay;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AionDpsMeter.UI.Pages
{
    /// <summary>The fork's settings as blocks of RATmeter's settings window: "appearance", "tracking", "hotkeys", "timers".</summary>
    public partial class ForkSettings(TimersOptionsStore store, AionDpsMeter.Timers.Schedule.ScheduleSource schedule, AionDpsMeter.UI.Services.TimerWindow.AlertSoundPlayer soundPlayer) : ComponentBase
    {
        private static readonly (BarStyle Style, string Label)[] BarStyles =
            [(BarStyle.Classic, "Классика"), (BarStyle.Thick, "Толстые"), (BarStyle.Slim, "Тонкие"), (BarStyle.Gauge, "Шкала"), (BarStyle.Table, "Таблица")];

        private static readonly (PanelTheme Theme, string Label)[] Themes =
            [(PanelTheme.Default, "Обычная"), (PanelTheme.Minimal, "Минимум"), (PanelTheme.Solid, "Плотная"), (PanelTheme.Light, "Светлая")];

        private static readonly (PanelFont Font, string Label)[] Fonts =
            [(PanelFont.System, "Системный"), (PanelFont.Narrow, "Узкий"), (PanelFont.Mono, "Моно"), (PanelFont.Wide, "Широкий")];

        private static void SetInt(object? value, Action<int> apply)
        {
            if (int.TryParse(value?.ToString(), out var v)) apply(v);
        }

        private static readonly (PowerColumn Mode, string Label)[] PowerModes =
            [(PowerColumn.CombatPower, "БМ"), (PowerColumn.GearScore, "ГС"), (PowerColumn.Both, "Оба"), (PowerColumn.None, "Нет")];

        private sealed record AlertRow(string MuteKey, string Title, string Description, FeedKind Kind, bool OnByDefault = true);

        /// <summary>One row per scheduled event (from schedule.json) plus one for all field bosses.</summary>
        private IReadOnlyList<AlertRow> AlertRows =>
            schedule.Current.Events
                .Select(e =>
                {
                    var kind = e.Category.Equals("rift", StringComparison.OrdinalIgnoreCase) ? FeedKind.Rift : FeedKind.Event;
                    return new AlertRow(e.Id, e.Name, "Звук и жёлтая строка заранее", kind, e.AlertByDefault);
                })
                .Append(new AlertRow(TimersOptions.AllFieldBosses, "Полевые боссы", "Перед респауном", FeedKind.Boss))
                .ToList();

        private const string PickFile = "pick-file";

        private static readonly (string Value, string Label)[] WindowsSounds =
        [
            ("", "Стандартный"),
            (AlertSound.System(WindowsSound.Asterisk).ToString(), "Звёздочка"),
            (AlertSound.System(WindowsSound.Beep).ToString(), "Сигнал"),
            (AlertSound.System(WindowsSound.Question).ToString(), "Вопрос"),
            (AlertSound.System(WindowsSound.Hand).ToString(), "Ошибка"),
        ];

        private static string FileLabel(AlertSound sound) => "♪ " + System.IO.Path.GetFileName(sound.File);

        private void OnSoundPicked(string key, object? value)
        {
            var text = value?.ToString() ?? "";
            if (text != PickFile)
            {
                Set(o => o.SetSound(key, AlertSound.Parse(text)));
                soundPlayer.Play(AlertSound.Parse(text));
                return;
            }
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Звук уведомления",
                Filter = "Звук (*.wav;*.mp3)|*.wav;*.mp3|Все файлы (*.*)|*.*",
            };
            if (dialog.ShowDialog() == true)
            {
                var sound = AlertSound.FromFile(dialog.FileName);
                Set(o => o.SetSound(key, sound));
                soundPlayer.Play(sound);
            }
            else
            {
                StateHasChanged(); // the select goes back to the saved sound
            }
        }

        private void PlaySound(string key) => soundPlayer.Play(O.SoundFor(key));

        /// <summary>On unless muted; events quiet by default (air raid, arena, reset) only once switched on.</summary>
        private bool AlertOn(AlertRow row) =>
            !O.MutedIds.Contains(row.MuteKey) && (row.OnByDefault || O.EnabledIds.Contains(row.MuteKey));

        private void SetAlert(string key, bool on) => Set(o => o.SetAlert(key, on));

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
            new("timers", "Таймеры боссов", "Показывает и прячет окно таймеров полевых боссов", () => O.ToggleBossTimersHotkey, v => Set(o => o.ToggleBossTimersHotkey = v)),
        ];

        private string Label(HotkeyRow row) =>
            capturing == row.Id ? "Нажми сочетание…" : string.IsNullOrEmpty(row.Get()) ? "Не задано" : HotkeyText.Normalize(row.Get());

        private void Set(Action<TimersOptions> change)
        {
            change(O);
            store.Save();
        }

        private void SetScale(object? value)
        {
            if (int.TryParse(value?.ToString(), out var percent)) Set(o => o.UiScale = Math.Clamp(percent, 80, 150));
        }

        /// <summary>Minutes ahead for this one event (the others keep theirs).</summary>
        private void SetLeadFor(string key, object? value)
        {
            if (int.TryParse(value?.ToString(), out var minutes)) Set(o => o.SetLead(key, minutes));
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

        /// <summary>Per event: shown unless hidden by id; field bosses all at once by their kind.</summary>
        private bool Shown(AlertRow row) => row.Kind == FeedKind.Boss
            ? !O.HiddenKinds.Contains(FeedKind.Boss)
            : !O.HiddenKinds.Contains(row.Kind) && !O.HiddenIds.Contains(row.MuteKey);

        private void SetShown(AlertRow row, bool show) => Set(o =>
        {
            if (row.Kind == FeedKind.Boss)
            {
                if (show) o.HiddenKinds.Remove(FeedKind.Boss); else o.HiddenKinds.Add(FeedKind.Boss);
                return;
            }
            if (o.HiddenKinds.Remove(row.Kind)) // the whole kind was hidden (older setting): keep its other events hidden
                foreach (var other in AlertRows.Where(r => r.Kind == row.Kind && r.MuteKey != row.MuteKey)) o.HiddenIds.Add(other.MuteKey);
            if (show) o.HiddenIds.Remove(row.MuteKey); else o.HiddenIds.Add(row.MuteKey);
        });

        private static string KindLabel(FeedKind kind) => kind switch
        {
            FeedKind.Rift => "Разломы",
            FeedKind.Boss => "Полевые боссы",
            _ => "Ивенты",
        };

        private void OnKey(HotkeyRow row, KeyboardEventArgs e)
        {
            if (capturing != row.Id) return;
            var text = HotkeyText.FromKey(e.Key, e.Code, e.CtrlKey, e.ShiftKey, e.AltKey);
            if (text is null) return; // a modifier alone: keep waiting
            row.Set(text);
            capturing = null;
        }
    }
}
