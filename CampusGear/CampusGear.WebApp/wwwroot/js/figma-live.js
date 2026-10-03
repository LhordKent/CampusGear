(() => {
    const menu = document.querySelector('.live-navigation');
    if (menu) {
        const mobile = window.matchMedia('(max-width: 1000px)');
        const resize = () => { menu.open = !mobile.matches; };
        resize();
        mobile.addEventListener('change', resize);
    }
    document.querySelectorAll('[data-summary-for]').forEach(summary => {
        const input = document.getElementById(summary.dataset.summaryFor);
        if (!input) return;
        const fallback = summary.textContent;
        const update = () => { summary.textContent = input.value.trim() || fallback; };
        input.addEventListener('input', update);
        input.addEventListener('change', update);
    });
    document.querySelectorAll('.live-badge').forEach(badge => {
        badge.dataset.state = badge.textContent.trim().toLowerCase();
    });
})();
