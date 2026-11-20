`npcs.ru.json` — таблица NPC (код → имя, isBoss, isDummy) с русскими именами. Взята из
[cyberbadger6969/aion2-dps-meter](https://github.com/cyberbadger6969/aion2-dps-meter) `data/npcs/ru.json`,
коммит `535edf3`, GPL-3.0; там она в свою очередь из [taengu/A2Tools-DPS-Meter](https://github.com/taengu/A2Tools-DPS-Meter).
Данные игры © NCSOFT.

`schedule.json` → `fieldBossSlots` (какой босс на каком месте игрового списка полевых боссов карт 1010 «Бертрон» и
1110 «Альтгард») — порядок боссов из [SkeeveAN/Aion-DPS-Meter](https://github.com/SkeeveAN/Aion-DPS-Meter)
`Client/assets/aion2/bosses/field_bosses.json`, коммит `f6ef644`, MIT License, Copyright (c) 2026 SkeeveTV.
Проверено по нашим захватам: координаты живых мест совпадают с координатами из этой таблицы.

`schedule.json` → события `argo-spirit-king`, `guardian-lord-nahma`, `abyss-executors`, `artifact-siege`
— время из [SkeeveAN/Aion-DPS-Meter](https://github.com/SkeeveAN/Aion-DPS-Meter)
`Client/assets/aion2/schedule/events.json`, коммит `f6ef644`, MIT License, Copyright (c) 2026 SkeeveTV.

`items.ru.json` — предметы AION 2 (код → русское название, слот, грейд, тир, уровень предмета, имя иконки), собрано из
`Client/assets/aion2/items/item_info.json`, `item_names.json`, `item_icons.json` проекта
[SkeeveAN/Aion-DPS-Meter](https://github.com/SkeeveAN/Aion-DPS-Meter), коммит `f6ef644`, MIT License, Copyright (c) 2026 SkeeveTV.
Формат пакета экипировки (11 56) — по их `Aion2FrameDecoder.DecodeEquipment`, код написан заново. Иконки берутся с
публичного CDN NCSOFT (assets.playnccdn.com), как и иконки умений. Данные игры © NCSOFT.

