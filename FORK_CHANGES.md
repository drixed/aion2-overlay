# Правки апстрима в форке aion2-overlay

Форк `drixed/aion2-overlay` от [Kuroukihime/AIon2-Dps-Meter](https://github.com/Kuroukihime/AIon2-Dps-Meter).
Чтобы слияния с апстримом проходили без конфликтов, файлы апстрима правятся только здесь:

| Файл | Правка |
|---|---|
| `AionDpsMeter.slnx` | +2 проекта: `AionDpsMeter.Timers`, `AionDpsMeter.Timers.Tests` |
| `AionDpsMeter.UI/AionDpsMeter.UI.csproj` | +1 `ProjectReference` на `AionDpsMeter.Timers` |
| `AionDpsMeter.UI/App.xaml.cs` | +2 строки с пометкой `// aion2-overlay fork`: `AddTimersOverlay(services)` и `TimersWindowController.Open()` |
| `AionDpsMeter.Services/Services/Update/UpdateCheckerService.cs` | `ReleasesApiUrl` → релизы `drixed/aion2-overlay` |

Новые файлы внутри проектов апстрима (с ними конфликтов не бывает, помечены первой строкой `aion2-overlay fork`):

- `AionDpsMeter.Services/PacketProcessing/Fork/IFieldBossListListener.cs`
- `AionDpsMeter.Services/PacketProcessing/Processors/FieldBossListForwarder.cs` — обработчик опкода `0x9101` (байты `01 91`)
- `AionDpsMeter.UI/Pages/TimersOverlay.razor`, `.razor.cs`, `.razor.css`
- `AionDpsMeter.UI/Services/TimerWindow/*`

Всё остальное — свои файлы: `AionDpsMeter.Timers/`, `AionDpsMeter.Timers.Tests/`, `schedule.json`,
`.github/workflows/sync-upstream.yml`, `docs/`.

## Если автообновление упало с конфликтом

1. `git fetch upstream && git merge upstream/master`
2. В конфликтующем файле оставить версию апстрима и заново внести нашу правку из таблицы выше.
3. `dotnet build AionDpsMeter.slnx -c Release && dotnet test AionDpsMeter.Timers.Tests`
4. `git commit` и `git push` — workflow сам соберёт релиз (issue закрыть вручную).

Если упал тест `Only_the_fork_forwarder_handles_the_field_boss_list_opcode` — апстрим сам начал обрабатывать
`01 91`: удалить `FieldBossListForwarder.cs` и подписаться на их обработчик.
