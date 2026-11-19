// aion2-overlay fork: new file (see FORK_CHANGES.md).
// "Поиск по настройкам" in the settings header: while something is typed every section shows and the rows that do not
// contain the text hide (a row's title and description count). Only inline classes this script owns are touched, so
// Blazor's re-renders do not fight it.
(function () {
    const MISS = 'fork-search-miss';
    let query = '';

    function apply() {
        const shell = document.querySelector('.settings-shell');
        if (!shell) return;
        shell.classList.toggle('is-searching', query.length > 0);
        shell.querySelectorAll('.setting-row').forEach(row => {
            row.classList.toggle(MISS, query.length > 0 && !row.textContent.toLowerCase().includes(query));
        });
        shell.querySelectorAll('.settings-section-label').forEach(label => label.classList.toggle(MISS, query.length > 0));
        shell.querySelectorAll('.settings-group').forEach(group => {
            const rows = group.querySelectorAll('.setting-row');
            const anyShown = [...rows].some(r => !r.classList.contains(MISS));
            group.classList.toggle(MISS, query.length > 0 && !anyShown);
        });
    }

    document.addEventListener('input', e => {
        if (e.target && e.target.classList && e.target.classList.contains('fork-search')) {
            query = e.target.value.trim().toLowerCase();
            apply();
        }
    });
    // a click on a section ends the search
    document.addEventListener('click', e => {
        const tab = e.target.closest && e.target.closest('.settings-tab');
        const box = document.querySelector('.fork-search');
        if (tab && box && box.value) { box.value = ''; query = ''; apply(); }
    });
})();
