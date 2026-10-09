// aion2-overlay fork: new file (see FORK_CHANGES.md).
// Russian for upstream's Blazor windows. Texts and title/placeholder attributes found in the dictionary are replaced
// whenever the page changes; anything not in the dictionary stays English (a new upstream string never breaks).
(function () {
    const ru = {
        "Settings": "Настройки", "Close": "Закрыть", "Close settings": "Закрыть настройки",
        "Appearance": "Вид", "Hotkeys": "Клавиши", "Tracking": "Учёт", "Developer": "Отладка",
        "[BETA] Overlays": "Оверлеи β", "Timers": "Таймеры", "Records": "Рекорды",
        "Window layout": "Расположение окон", "WINDOW LAYOUT": "РАСПОЛОЖЕНИЕ ОКОН", "WINDOW": "ОКНО",
        "PLAYER LIST": "СПИСОК ИГРОКОВ", "HISTORY": "ИСТОРИЯ", "BUFF OVERLAY": "ОВЕРЛЕЙ БАФФОВ",
        "SKILL COOLDOWNS": "ПЕРЕЗАРЯДКА УМЕНИЙ", "Settings groups": "Разделы настроек",
        "Boss Encounters Only": "Только бои с боссами",
        "Ignore damage dealt to trash mobs and only track boss fights": "Не считать урон по обычным мобам, только бои с боссами",
        "Cap Dummy Parses at 1 Minute": "Манекен — не дольше 1 минуты",
        "Stop counting damage on training dummies after 60 seconds": "Прекращать подсчёт урона по манекену через 60 секунд",
        "Combine Summon Damage": "Объединять урон призывов",
        "Merge all pet and summon damage into the owner's row": "Урон питомцев и призывов добавлять в строку владельца",
        "Hide Player Names": "Скрыть имена игроков",
        "Replace names with obfuscated names — useful before sharing screenshots": "Заменять имена — удобно перед тем, как делиться скриншотами",
        "Show Death Count": "Показывать смерти",
        "Display a skull icon with death count on each row": "Иконка черепа с числом смертей в каждой строке",
        "Relative Damage Bars": "Полосы относительно лидера",
        "Scale bars against the top player instead of absolute damage": "Длина полос — относительно лучшего игрока, а не абсолютного урона",
        "Use class colors": "Цвета классов",
        "Use class colors for player rows instead of highlighting me": "Красить строки в цвета классов вместо выделения себя",
        "Row Size": "Размер строки", "Size of each player row": "Высота строки игрока",
        "Window Opacity": "Прозрачность окна", "How see-through the overlay is (10–100%)": "Насколько прозрачен оверлей (10–100%)",
        "Toggle Visibility": "Показать / скрыть",
        "Click the field, then press the key combo you want to use": "Нажми на поле, затем нужное сочетание клавиш",
        "Press a key...": "Нажми клавишу...",
        "Keep History For": "Хранить историю",
        "Older encounters are automatically deleted after this many days": "Старые бои удаляются автоматически через столько дней",
        "days": "дн.",
        "Log Raw Packets": "Записывать сырые пакеты",
        "Save incoming network packets to the PacketLogs folder": "Сохранять входящие пакеты в папку PacketLogs",
        "Enabled": "Включено",
        "Show a floating overlay with active buff timers": "Плавающее окно с таймерами активных баффов",
        "Show a floating overlay with active skill cd timers": "Плавающее окно с перезарядкой умений",
        "Icon Size": "Размер иконок", "Size of each buff icon (20–50px)": "Размер иконки баффа (20–50 px)",
        "Size of each skill icon (20–50px)": "Размер иконки умения (20–50 px)",
        "Sort Order": "Порядок", "How tracked buffs are ordered by remaining time": "Сортировка баффов по оставшемуся времени",
        "How tracked skills are ordered by remaining cd time": "Сортировка умений по оставшейся перезарядке",
        "Least time": "Сначала меньше", "Most time": "Сначала больше",
        "Tracked Buffs": "Отслеживаемые баффы", "Tracked skills": "Отслеживаемые умения",
        "Search skills…": "Поиск умений…", "No skills found": "Умения не найдены", "Add skill": "Добавить умение",
        "Standard": "Обычный", "Compact": "Компактный",
        "History": "История", "Stat Eff Calculator": "Калькулятор статов", "Hide": "Свернуть",
        "What's New": "Что нового"
    };

    const attributes = ["title", "placeholder"];

    function translate(text) {
        if (!text) return null;
        const key = text.trim();
        const value = Object.prototype.hasOwnProperty.call(ru, key) ? ru[key] : undefined;
        return value === undefined ? null : text.replace(key, value);
    }

    function fixText(node) {
        const t = translate(node.nodeValue);
        if (t !== null && t !== node.nodeValue) node.nodeValue = t;
    }

    function fixAttribute(element, name) {
        const v = element.getAttribute(name);
        const t = translate(v);
        if (t !== null && t !== v) element.setAttribute(name, t);
    }

    function walk(root) {
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
        for (let n = walker.nextNode(); n; n = walker.nextNode()) fixText(n);
        const elements = [root, ...root.querySelectorAll("[title],[placeholder]")];
        for (const el of elements) if (el.getAttribute) for (const a of attributes) if (el.hasAttribute(a)) fixAttribute(el, a);
    }

    function start() {
        walk(document.body);
        new MutationObserver(function (mutations) {
            for (const m of mutations) {
                if (m.type === "characterData") fixText(m.target);
                else if (m.type === "attributes") fixAttribute(m.target, m.attributeName);
                else for (const n of m.addedNodes) {
                    if (n.nodeType === Node.TEXT_NODE) fixText(n);
                    else if (n.nodeType === Node.ELEMENT_NODE) walk(n);
                }
            }
        }).observe(document.body, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: attributes });
    }

    if (typeof module !== "undefined") module.exports = { translate };
    else if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", start);
    else start();
})();
