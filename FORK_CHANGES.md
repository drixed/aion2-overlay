# Правки апстрима в форке aion2-overlay

Форк `drixed/aion2-overlay` от [Kuroukihime/AIon2-Dps-Meter](https://github.com/Kuroukihime/AIon2-Dps-Meter).
Чтобы слияния с апстримом проходили без конфликтов, файлы апстрима правятся только здесь:

| Файл | Правка |
|---|---|
| `AionDpsMeter.slnx` | +2 проекта: `AionDpsMeter.Timers`, `AionDpsMeter.Timers.Tests` |
| `AionDpsMeter.UI/AionDpsMeter.UI.csproj` | +1 `ProjectReference` на `AionDpsMeter.Timers` |
| `AionDpsMeter.UI/App.xaml.cs` | +2 строки с пометкой `// aion2-overlay fork`: `AddTimersOverlay(services)` и `TimersWindowController.Open()` |
| `AionDpsMeter.Services/Services/Update/UpdateCheckerService.cs` | `ReleasesApiUrl` → релизы `drixed/aion2-overlay` |
| `AionDpsMeter.UI/Pages/MainDpsPage.razor` | +2 строки: `<ForkMainSwitch>` и `</ForkMainSwitch>` вокруг блока стилей апстрима (сами строки апстрима не тронуты) — панель боя или стили апстрима, переключается сразу |
| `AionDpsMeter.Core/Models/Mob.cs` | `IsBoss`: цель без кода (спаун не увиден — метр запущен посреди данжа) считается боссом, если её HP хоть раз был ≥ 1 млн (`HpMaxSeen`, `UnknownBossHp`): у боссов 3,6 млн+, у обычных мобов до ~300 тыс.; `HpTotal` не бывает меньше `HpMaxSeen` — разбор пакета моба иногда даёт мусор (67), и полоса HP босса стояла на 100% |
| `AionDpsMeter.UI/Pages/SettingsPage.razor` | +4 строки с пометкой `aion2-overlay fork`: `<ForkSettings Section="…"/>` в конце вкладок appearance, hotkeys, tracking и новые секции `fork-timers` и `fork-records` (`<ForkRecords/>`); `@inject TimersOptionsStore`; в «Window layout» третий вариант «Панель боя», выбор стиля апстрима переключает на него (`ForkUpstreamStyle`, блок `@code` в конце) |
| `AionDpsMeter.UI/Pages/SettingsPage.razor.css` | в конце: превью «Панели боя», три колонки в `.style-picker`, вкладки переносятся на вторую строку (их больше, чем влезает в окно, а прокрутка у апстрима скрыта) |
| `AionDpsMeter.Core/Models/Player.cs` | +`GearScore` (ГС) |
| `AionDpsMeter.Services/PacketProcessing/Processors/PartyProcessor.cs` | +1 строка: `GearScore` из списка группы (апстрим его читал, но выбрасывал) |
| `AionDpsMeter.Services/Services/Entity/EntityTracker.cs` | +3 строки: `GearScore` переносится вместе с `CombatPower` |
| `AionDpsMeter.UI/Pages/SettingsPage.razor.cs` | +2 строки в `_groups`: вкладки `fork-timers` ("Timers") и `fork-records` ("Records") |
| `AionDpsMeter.UI/Converters/SafeImageSourceConverter.cs` | иконка умения, которой ещё нет в кэше, грузится прямо по ссылке (`LoadRemote`): обновление после скачивания у апстрима не срабатывало, и иконки появлялись только при повторном открытии окна |
| `AionDpsMeter.UI/wwwroot/index.html` | +2 строки: `<script src="js/ru.js">` и `<script src="js/fit.js">` перед `blazor.webview.js` |

Новые файлы внутри проектов апстрима (с ними конфликтов не бывает, помечены первой строкой `aion2-overlay fork`):

- `AionDpsMeter.Services/PacketProcessing/Fork/IFieldBossListListener.cs`
- `AionDpsMeter.Services/PacketProcessing/Processors/FieldBossListForwarder.cs` — обработчик опкода `0x9101` (байты `01 91`)
- `AionDpsMeter.Services/PacketProcessing/Fork/IEnergyListener.cs`, `Processors/EnergyForwarder.cs` — опкод `0x610C` (байты `0C 61`, энергия)
- `AionDpsMeter.Services/PacketProcessing/Fork/IMapLoadListener.cs`, `Processors/MapLoadForwarder.cs` — опкод `0x3621` (байты `21 36`, загрузка карты)
- `AionDpsMeter.UI/Pages/TimersOverlay.razor`, `.razor.cs`, `.razor.css`
- `AionDpsMeter.UI/Services/TimerWindow/*`
- `AionDpsMeter.UI/Pages/BossPanelPage.razor`, `.razor.cs`, `.razor.css` — главное окно «Бой с боссом»
- `AionDpsMeter.UI/wwwroot/js/ru.js` — русский словарь для Blazor-окон апстрима
- `AionDpsMeter.UI/wwwroot/js/fit.js` — высота главного окна по содержимому
- `AionDpsMeter.UI/Pages/ForkSettings.*` — настройки форка внутри окна настроек RATmeter
- `AionDpsMeter.UI/Pages/ForkMainSwitch.razor` — панель боя или стили апстрима в главном окне
- `AionDpsMeter.UI/Pages/ForkRecords.*` — вкладка «Рекорды» (рекорды по боссам; сами рекорды — `AionDpsMeter.Timers/Records`, файл `boss-records.json`; хранилище истории апстрима обёрнуто `RecordingHistoryStore`)
- `AionDpsMeter.UI/Services/TimerWindow/CubeMapWindow.cs` — окно «Карта кубов» (🗺)
- `AionDpsMeter.UI/Services/TimerWindow/WindowBoundsKeeper.cs` — сохраняет место и размер главного окна, таймеров и карты кубов через секунду после перемещения или изменения размера (upstream сохранял главное окно только при закрытии, остальные — только при перетаскивании)

Всё остальное — свои файлы: `.github/README.md` (GitHub показывает его вместо корневого README апстрима),
`AionDpsMeter.Timers/`, `AionDpsMeter.Timers.Tests/`, `schedule.json`,
`.github/workflows/sync-upstream.yml`, `docs/`.

## Если автообновление упало с конфликтом

1. `git fetch upstream && git merge upstream/master`
2. В конфликтующем файле оставить версию апстрима и заново внести нашу правку из таблицы выше.
3. `dotnet build AionDpsMeter.slnx -c Release && dotnet test AionDpsMeter.Timers.Tests`
4. `git commit` и `git push` — workflow сам соберёт релиз (issue закрыть вручную).

Если упал тест `Only_the_fork_forwarder_handles_the_field_boss_list_opcode` — апстрим сам начал обрабатывать
`01 91`: удалить `FieldBossListForwarder.cs` и подписаться на их обработчик.
