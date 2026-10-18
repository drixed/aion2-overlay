"""aion2-overlay fork: builds the GitHub Pages site (docs/index.html) for the meter.

The meter window on the page is the meter's own markup and CSS (AionDpsMeter.UI/Pages/BossPanelPage.razor.css,
wwwroot/css/app.css), with made-up names and numbers, so it looks exactly like the real one. Run after changing the
meter's look:  python site/build.py
"""
import json
import os
import re
import shutil

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UI = os.path.join(ROOT, 'AionDpsMeter.UI')
OUT = os.path.join(ROOT, 'docs')
REPO = 'drixed/aion2-overlay'
UPSTREAM = 'https://github.com/Kuroukihime/AIon2-Dps-Meter'


def read(*parts):
    with open(os.path.join(*parts), encoding='utf-8') as f:
        return f.read()


METER_CSS = read(UI, 'wwwroot', 'css', 'app.css') + read(UI, 'Pages', 'BossPanelPage.razor.css').replace('::deep ', '')
EVENTS = read(UI, 'Pages', 'EventIcons.cs')


def svg(icon):
    m = re.search(r'"%s" => (Stroke|Fill)\("([^"]+)"(?:, ([\d.]+))?\)' % icon, EVENTS)
    kind, d, width = m.groups()
    if kind == 'Fill':
        return f'<svg width="15" height="15" viewBox="0 0 24 24" fill="currentColor" fill-rule="evenodd" aria-hidden="true"><path d="{d}"/></svg>'
    return (f'<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="{width}" '
            f'stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="{d}"/></svg>')


EV_COLORS = {'rift': '#b48cff', 'shugo': '#7cc8f0', 'arena': '#f07070', 'reset': '#7fdc9c'}


def chip(icon, state, name, text):
    inner = svg(icon) + (f'<span class="bp-ev-name">{name}</span>' if state != 'idle' else '') + (f'<b>{text}</b>' if text else '')
    return f'<span class="bp-ev bp-ev--{state}" style="--ev:{EV_COLORS[icon]}">{inner}</span>'


FOOTER = ('<div class="bp-footer"><div class="bp-footer-line"><span class="bp-events">'
          + chip('rift', 'next', 'Разлом', '12м') + chip('shugo', 'idle', '', '42м') + chip('arena', 'idle', '', '2ч12м')
          + chip('reset', 'idle', '', '9ч') + '</span><span class="bp-energy">⚡ 470<span class="bp-energy-extra">(+265)</span></span></div></div>')


def row(name, cls, bar, cp, dmg, dps, me=False, death=''):
    death_cls = 'bp-row-death bp-row-death--click' if death else 'bp-row-death'
    return (f'<div class="bp-row {"is-me-border bp-row--me" if me else ""}"><div class="bp-row-bar dps-class-{cls}" style="width:{bar}%"></div>'
            f'<span class="bp-row-icon" style="background-image:url(\'assets/classes/{cls}.png\')"></span><span class="bp-row-name">{name}</span>'
            f'<span class="bp-row-cp">{cp}</span><span class="{death_cls}">{death}</span>'
            f'<span class="bp-row-dmg">{dmg}</span><span class="bp-row-dps">{dps}<small>/s</small></span></div>')


def header(title, mode, time):
    return (f'<div class="bp-header"><span class="bp-dot bp-dot--fight"></span><span class="bp-title">{title}</span>'
            f'<div class="bp-mode-wrap"><span class="bp-mode">{mode} ▾</span></div><span class="bp-time">{time}</span>'
            '<span class="bp-ping" style="color:#7ee787">48 ms</span><div class="bp-controls"><span>☰</span><span>⚔</span><span>🗺</span>'
            '<span>⚙</span><span>_</span><span>✕</span></div></div>')


SCREENS = {
    'boss': header('Бой с боссом', 'DPS', '01:12') + '''
<div class="bp-card"><div class="bp-card-top"><span class="bp-skull">☠</span><span class="bp-name">Каменный страж</span>
<span class="bp-hp">2.71M / 5.22M · <b>52%</b></span></div><div class="bp-track"><div class="bp-fill" style="width:52%"></div></div>
<div class="bp-record">🏆 Рекорд 2:30 · твой 18.4K/s · <b class="bp-win">быстрее рекорда на 0:21</b></div>
<div class="bp-card-bottom"><span>До победы <b class="bp-win">1:05</b></span><span>Группа <b>52.4K/s</b></span><span>До ярости <b class="bp-enrage">3:48</b></span></div></div>
<div class="bp-pet"><span class="bp-pet-name">🐾 Сторожевой пёс</span><span class="bp-pet-stat">ур. 2 · 57 / 75</span><i class="bp-pet-bar" style="width:76%"></i></div>
<div class="bp-players">''' + row('Lyrien', 15, 100, 1712, '1.32M (34.8%)', '18.4K', me=True) + row('Mortavis', 13, 82, 1655, '1.09M (28.6%)', '15.1K')
    + row('Kaelix', 17, 55, 1598, '734K (19.3%)', '10.2K') + row('Sunra', 12, 38, 1620, '504K (13.2%)', '7.0K', death='💀1')
    + row('Veyla', 18, 12, 1540, '151K (4.0%)', '2.1K') + '</div>' + FOOTER,
    'dummy': header('Тренировочное чучело', 'Манекен', '00:19') + '''
<div class="bp-mode-status"><span>🎯 Замер: осталось 0:41</span></div><div class="bp-players">'''
    + row('Lyrien', 15, 100, 1712, '936K (77.1%)', '46.9K', me=True) + row('Kaelix', 17, 30, 1598, '278K (22.9%)', '13.9K') + '</div>' + FOOTER,
    'pvp': header('Ожидание боя', 'PvP', '00:48') + '''
<div class="bp-mode-status"><span>⚔ PvP-бой 0:48 · клик по нику — скиллы</span></div>
<div class="bp-pvp-kills"><div class="bp-pvp-kill is-mine"><span>☠ Ты убил</span><b>Vexa</b><span class="bp-pvp-kill-sk">Призрачная стрела</span></div>
<div class="bp-pvp-kill is-death"><span>💀 Тебя убил</span><b>Draven</b><span class="bp-pvp-kill-sk">Удар бури</span></div></div>
<div class="bp-recap bp-pvp"><div class="bp-recap-head"><b>⚔ Vexa</b><span class="bp-pvp-tabs"><span class="is-on">Нанёс</span><span>Получил</span></span><span class="bp-link">✕</span></div>
<div class="bp-recap-row"><span class="bp-recap-sk bp-pvp-skill">Удар бури</span><span class="bp-recap-t">12×</span><span class="bp-pvp-crit">✦4</span><span class="bp-recap-dmg">88.4K</span></div>
<div class="bp-recap-row"><span class="bp-recap-sk bp-pvp-skill">Разрез</span><span class="bp-recap-t">9×</span><span class="bp-pvp-crit">✦2</span><span class="bp-recap-dmg">51.0K</span></div></div>
<div class="bp-players">''' + row('Lyrien', 15, 100, 1712, '366K (52.1%)', '7.6K', me=True) + row('Vexa', 18, 48, 1690, '174K (24.8%)', '3.6K')
    + row('Draven', 11, 27, 1702, '98K (14.0%)', '2.0K') + '</div>' + FOOTER,
}

# (key, screen, part of the window to light up)
TOUR = [
    ('party', 'boss', '.bp-players'),
    ('boss', 'boss', '.bp-card'),
    ('records', 'boss', '.bp-record'),
    ('power', 'boss', '.bp-row-cp'),
    ('death', 'boss', '.bp-row-death--click'),
    ('pets', 'boss', '.bp-pet'),
    ('modes', 'boss', '.bp-mode'),
    ('dummy', 'dummy', '.bp-mode-status'),
    ('pvp', 'pvp', '.bp-pvp'),
    ('kills', 'pvp', '.bp-pvp-kills'),
    ('events', 'boss', '.bp-events'),
    ('energy', 'boss', '.bp-energy'),
]

TEXT = {
    'ru': {
        'lang': 'EN', 'nav_features': 'Возможности', 'nav_install': 'Установка', 'nav_faq': 'Вопросы',
        'hero_tag': 'Бесплатно · Windows 10/11 · open source',
        'hero_title': 'DPS-метр и таймеры для <b>AION 2</b>',
        'hero_text': 'Урон группы, боссы, ивенты, питомцы и PvP — поверх игры, в одном окне. Метр только читает трафик игры и ничего в неё не отправляет.',
        'download': 'Скачать', 'download_note': 'Архив для Windows, последняя версия', 'github': 'GitHub',
        'tour_tag': 'Где это в метре', 'tour_title': 'Нажми — покажу',
        'tour_text': 'Слева возможности, справа — настоящее окно метра (ники и цифры выдуманные).',
        't_party': ['Урон группы', 'Урон и DPS каждого в группе. Клик по нику — разбивка по скиллам: крит, перфект, удары в спину.'],
        't_boss': ['Босс и время до победы', 'HP босса, сколько осталось до победы при вашем DPS и сколько до ярости.'],
        't_records': ['Рекорды', 'Лучший бой по каждому боссу и темп прямо в бою: быстрее или медленнее рекорда.'],
        't_power': ['Сила (GS)', 'Сила снаряжения рядом с ником — сразу видно, кто тянет группу.'],
        't_death': ['Разбор смерти', 'Черепок у ника: клик — кто и чем бил за 6 секунд до смерти.'],
        't_pets': ['Питомцы', 'Бьёшь моба — видно, чей питомец с него падает и сколько душ из нужного.'],
        't_modes': ['Режимы', 'DPS · Урон · Хил · Манекен · Гринд · PvP — переключение в шапке окна.'],
        't_dummy': ['Манекен', 'Ударь тренировочное чучело — замер ровно минуту с твоего первого удара. Чужие удары его не сбивают.'],
        't_pvp': ['PvP', 'В бою с игроками окно само переключается: клик по нику — «Нанёс» и «Получил» по скиллам.'],
        't_kills': ['Убийства', 'Кого убил ты и кто убил тебя — и каким скиллом.'],
        't_events': ['Ивенты', 'Разлом, Шуго, Арена, сброс — отсчёт; ближайший подсвечен, перед началом мигает и звенит.'],
        't_energy': ['Энергия', 'Текущая и дополнительная энергия — не надо открывать меню.'],
        'more_tag': 'И ещё', 'more_title': 'Что есть кроме главного окна',
        'f1': ['⏰ Окно таймеров боссов', 'Когда появятся полевые боссы — отдельным окном, со звуком.'],
        'f2': ['🐾 Вкладка «Питомцы»', 'Все питомцы: уровень, души, с кого падают; сверху — кому меньше всего осталось.'],
        'f3': ['📜 История боёв', 'Каждый бой сохраняется: урон, скиллы, длительность.'],
        'f4': ['🎨 Внешний вид', 'Темы, полосы, шрифт, прозрачность, «только моя группа», компактный режим.'],
        'f5': ['⌨ Горячие клавиши', 'Скрыть, очистить, сбросить бой, сквозные клики, окно таймеров.'],
        'f6': ['⬆ Обновляется сам', 'Новая версия — уведомление в окне и обновление в один клик.'],
        'install_tag': 'Установка', 'install_title': 'Три шага, пара минут',
        's1': 'Поставь <a href="https://npcap.com/#download">Npcap</a> и отметь «Install Npcap in WinPcap API-compatible Mode».',
        's2': 'Скачай архив кнопкой выше и распакуй в любую папку (не в Program Files).',
        's3': 'Запусти <code>AionDpsMeter.UI.exe</code>, потом игру — метр подхватит бой сам.',
        'faq_tag': 'Вопросы', 'faq_title': 'Частые вопросы',
        'q1': ['За это банят?', 'Метр только читает сетевой трафик, который игра и так получает: ничего не отправляет на сервер, не трогает файлы и память игры. Так же работают и другие метры. Гарантий, что правила игры не изменятся, не может дать никто — решай сам.'],
        'q2': ['Windows пишет «защитил ваш компьютер»', 'Программа не подписана платным сертификатом. Нажми «Подробнее» → «Выполнить в любом случае». Исходный код открыт — его можно проверить.'],
        'q3': ['Вместо ника «Player_1234»', 'Игра присылает ники не в каждом пакете, а при телепорте или входе в данж. До этого метр показывает номер.'],
        'q4': ['Урон не появляется', 'Проверь, что Npcap стоит с галочкой WinPcap. Иногда нужен запуск от администратора. VPN может прятать трафик игры от метра.'],
        'q5': ['Лишний «Player_…» с уроном', 'Это призванное существо, чей призыв метр не видел (например, метр запустили позже). После нового призыва урон пойдёт хозяину.'],
        'foot': 'Автор — <b>DRIXED</b> · GPL-3.0 · <a href="https://github.com/{repo}">исходный код</a>. Не связан с NCSOFT; AION — товарный знак NCSOFT.',
    },
    'en': {
        'lang': 'RU', 'nav_features': 'Features', 'nav_install': 'Install', 'nav_faq': 'FAQ',
        'hero_tag': 'Free · Windows 10/11 · open source',
        'hero_title': 'DPS meter and timers for <b>AION 2</b>',
        'hero_text': 'Party damage, bosses, events, pets and PvP — on top of the game, in one window. The meter only reads the game traffic and never sends anything to it.',
        'download': 'Download', 'download_note': 'Windows zip, latest version', 'github': 'GitHub',
        'tour_tag': 'Where is it', 'tour_title': 'Click to see it in the meter',
        'tour_text': 'Features on the left, the real meter window on the right (names and numbers are made up). The meter itself is in Russian.',
        't_party': ['Party damage', 'Damage and DPS of everyone in the party. Click a name for the skill breakdown: crits, perfects, back attacks.'],
        't_boss': ['Boss and time to kill', 'Boss HP, time to kill at your DPS and time to enrage.'],
        't_records': ['Records', 'Your best fight on each boss and the pace live: ahead or behind the record.'],
        't_power': ['Gear score', 'Gear power next to each name — see who carries.'],
        't_death': ['Death recap', 'Skull next to a name: click to see who hit them with what in the last 6 seconds.'],
        't_pets': ['Pets', 'Hit a mob and see which pet drops from it and how many souls are left.'],
        't_modes': ['Modes', 'DPS · Damage · Heal · Dummy · Grind · PvP — switch in the window header.'],
        't_dummy': ['Dummy', 'Hit a training dummy — exactly one minute from your first hit. Other players do not reset it.'],
        't_pvp': ['PvP', 'Fighting players switches the window by itself; click a name for damage dealt and taken by skill.'],
        't_kills': ['Kills', 'Who you killed and who killed you — and with which skill.'],
        't_events': ['Events', 'Rift, Shugo, Arena, reset — countdowns; the next one is highlighted and pulses before it starts.'],
        't_energy': ['Energy', 'Current and extra energy without opening the menu.'],
        'more_tag': 'And more', 'more_title': 'Beyond the main window',
        'f1': ['⏰ Boss timers window', 'When field bosses spawn — in a separate window, with a sound.'],
        'f2': ['🐾 Pets tab', 'All pets: level, souls, where they drop; closest to the next level first.'],
        'f3': ['📜 Fight history', 'Every fight is saved: damage, skills, duration.'],
        'f4': ['🎨 Looks', 'Themes, bars, font, opacity, "only my party", compact mode.'],
        'f5': ['⌨ Hotkeys', 'Hide, clear, reset fight, click-through, timers window.'],
        'f6': ['⬆ Updates itself', 'A new version shows in the window and installs in one click.'],
        'install_tag': 'Install', 'install_title': 'Three steps, two minutes',
        's1': 'Install <a href="https://npcap.com/#download">Npcap</a> with "Install Npcap in WinPcap API-compatible Mode" checked.',
        's2': 'Download the zip with the button above and extract it anywhere (not Program Files).',
        's3': 'Run <code>AionDpsMeter.UI.exe</code>, then the game — the meter picks up fights by itself.',
        'faq_tag': 'FAQ', 'faq_title': 'Common questions',
        'q1': ['Can I get banned?', 'The meter only reads network traffic the game already receives: it sends nothing to the server and does not touch the game files or memory, like other meters. Nobody can promise the game rules will not change — your call.'],
        'q2': ['Windows says it "protected your PC"', 'The app is not signed with a paid certificate. Click "More info" → "Run anyway". The source code is open to check.'],
        'q3': ['"Player_1234" instead of a name', 'The game sends names only on teleports or entering a dungeon, not with every hit. Until then the meter shows a number.'],
        'q4': ['No damage shows up', 'Make sure Npcap is installed with the WinPcap option. Some systems need Run as administrator. A VPN can hide the game traffic from the meter.'],
        'q5': ['An extra "Player_…" with damage', 'A summon whose summoning the meter did not see (e.g. it was started later). After the next summon its damage goes to the owner.'],
        'foot': 'Made by <b>DRIXED</b> · GPL-3.0 · <a href="https://github.com/{repo}">source code</a>. Not affiliated with NCSOFT; AION is a trademark of NCSOFT.',
    },
}
for lang in TEXT.values():
    lang['foot'] = lang['foot'].replace('{up}', UPSTREAM).replace('{repo}', REPO)


def t(key):
    return f'data-i18n="{key}"'


def page():
    panels = ''.join(f'<div class="boss-panel tour-panel" data-screen="{n}"{"" if n == "boss" else " hidden"}>{html}</div>' for n, html in SCREENS.items())
    tour_buttons = ''.join(f'<button class="tour-btn" data-i="{i}" data-i18n="t_{k}.0"></button>' for i, (k, *_r) in enumerate(TOUR))
    cards = ''.join(f'<div class="card"><b data-i18n="f{i}.0"></b><p data-i18n="f{i}.1"></p></div>' for i in range(1, 7))
    steps = ''.join(f'<div class="step"><em>{i}</em><p data-i18n="s{i}"></p></div>' for i in range(1, 4))
    faq = ''.join(f'<details><summary data-i18n="q{i}.0"></summary><p data-i18n="q{i}.1"></p></details>' for i in range(1, 6))
    tour = [{'key': k, 'screen': s, 'sel': sel} for k, s, sel in TOUR]
    return f'''<!doctype html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>AION 2 Overlay — DPS-метр и таймеры</title>
<meta name="description" content="Бесплатный DPS-метр и таймеры для AION 2: урон группы, боссы, ивенты, питомцы, PvP.">
<meta property="og:title" content="AION 2 Overlay — DPS-метр и таймеры">
<meta property="og:description" content="Урон группы, боссы, ивенты, питомцы и PvP — поверх игры, в одном окне.">
<link rel="stylesheet" href="assets/meter.css">
<link rel="stylesheet" href="assets/site.css">
</head>
<body>
<header class="top"><div class="top-in">
  <a class="brand" href="#">AION 2 Overlay</a>
  <nav><a href="#tour" {t('nav_features')}></a><a href="#install" {t('nav_install')}></a><a href="#faq" {t('nav_faq')}></a>
  <a href="https://github.com/{REPO}" {t('github')}></a><button id="lang" class="lang" {t('lang')}></button></nav>
</div></header>

<section class="hero wrap">
  <div>
    <div class="tag" {t('hero_tag')}></div>
    <h1 {t('hero_title')}></h1>
    <p class="lead" {t('hero_text')}></p>
    <a id="dl" class="btn btn-gold" href="https://github.com/{REPO}/releases/latest">⬇ <span {t('download')}></span> <span id="ver"></span></a>
    <a class="btn btn-ghost" href="https://github.com/{REPO}" {t('github')}></a>
    <div class="note" {t('download_note')}></div>
  </div>
  <div class="hero-meter"><div class="boss-panel">{SCREENS['boss']}</div></div>
</section>

<section id="tour" class="wrap">
  <div class="tag" {t('tour_tag')}></div>
  <h2 {t('tour_title')}></h2>
  <p class="sub" {t('tour_text')}></p>
  <div class="tour">
    <div class="tour-list">{tour_buttons}</div>
    <div class="tour-stage">{panels}</div>
    <div class="tour-cap"><b id="cap-title"></b><p id="cap-text"></p></div>
  </div>
</section>

<section class="wrap">
  <div class="tag" {t('more_tag')}></div>
  <h2 {t('more_title')}></h2>
  <div class="cards">{cards}</div>
</section>

<section id="install" class="wrap">
  <div class="tag" {t('install_tag')}></div>
  <h2 {t('install_title')}></h2>
  <div class="steps">{steps}</div>
</section>

<section id="faq" class="wrap">
  <div class="tag" {t('faq_tag')}></div>
  <h2 {t('faq_title')}></h2>
  <div class="faq">{faq}</div>
</section>

<footer class="wrap foot" {t('foot')}></footer>

<script>
const TEXT = {json.dumps(TEXT, ensure_ascii=False)};
const TOUR = {json.dumps(tour)};
const REPO = {json.dumps(REPO)};
let lang = 'ru', current = 0;
try {{ lang = localStorage.getItem('lang') || (navigator.language.startsWith('ru') ? 'ru' : 'en'); }} catch (e) {{}}

function get(key) {{
  const [k, i] = key.split('.');
  const v = TEXT[lang][k];
  return i === undefined ? v : v[+i];
}}
function apply() {{
  document.documentElement.lang = lang;
  document.querySelectorAll('[data-i18n]').forEach(el => el.innerHTML = get(el.dataset.i18n));
  pick(current);
}}
function pick(i) {{
  current = i;
  const step = TOUR[i];
  document.querySelectorAll('.tour-btn').forEach((b, j) => b.classList.toggle('on', i === j));
  document.querySelectorAll('.lit').forEach(e => e.classList.remove('lit'));
  document.querySelectorAll('.tour-panel').forEach(p => {{
    p.hidden = p.dataset.screen !== step.screen;
    if (!p.hidden) p.querySelectorAll(step.sel).forEach(e => e.classList.add('lit'));
  }});
  const [title, text] = TEXT[lang]['t_' + step.key];
  document.getElementById('cap-title').textContent = title;
  document.getElementById('cap-text').textContent = text;
}}
document.querySelectorAll('.tour-btn').forEach((b, i) => b.onclick = () => pick(i));
document.getElementById('lang').onclick = () => {{
  lang = lang === 'ru' ? 'en' : 'ru';
  try {{ localStorage.setItem('lang', lang); }} catch (e) {{}}
  apply();
}};
apply();

// the latest release's zip: the button downloads it directly; without the API it opens the releases page
fetch('https://api.github.com/repos/' + REPO + '/releases/latest')
  .then(r => r.ok ? r.json() : null)
  .then(rel => {{
    if (!rel) return;
    const zip = (rel.assets || []).find(a => a.name.endsWith('.zip'));
    if (zip) document.getElementById('dl').href = zip.browser_download_url;
    document.getElementById('ver').textContent = rel.tag_name || '';
  }})
  .catch(() => {{}});
</script>
</body>
</html>
'''


def main():
    assets = os.path.join(OUT, 'assets')
    os.makedirs(os.path.join(assets, 'classes'), exist_ok=True)
    with open(os.path.join(assets, 'meter.css'), 'w', encoding='utf-8') as f:
        f.write('/* generated by site/build.py from the meter\'s own CSS */\n' + METER_CSS)
    shutil.copy(os.path.join(ROOT, 'site', 'site.css'), os.path.join(assets, 'site.css'))
    for cls in range(11, 19):
        shutil.copy(os.path.join(UI, 'wwwroot', 'images', 'classes', f'{cls}.png'), os.path.join(assets, 'classes', f'{cls}.png'))
    with open(os.path.join(OUT, 'index.html'), 'w', encoding='utf-8') as f:
        f.write(page())
    # GitHub Pages: serve files as they are (no Jekyll)
    open(os.path.join(OUT, '.nojekyll'), 'w').close()
    print('docs/index.html written')


if __name__ == '__main__':
    main()
