(() => {
    const root = document.documentElement;
    const meta = document.querySelector('meta[name="theme-color"]');
    const sync = () => {
        const light = root.dataset.theme === 'light';
        const label = light ? 'Switch to dark mode' : 'Switch to light mode';
        if (meta) meta.content = light ? '#f4f7f9' : '#071b2b';
        document.querySelectorAll('[data-theme-toggle]').forEach(toggle => {
            toggle.setAttribute('aria-label', label);
            toggle.title = label;
        });
    };
    const toggle = () => {
        root.dataset.theme = root.dataset.theme === 'light' ? 'dark' : 'light';
        try { localStorage.setItem('cg-theme', root.dataset.theme); } catch { }
        sync();
    };
    document.addEventListener('click', event => {
        if (!event.target.closest('[data-theme-toggle]')) return;
        // Crossfade where View Transitions are supported; elsewhere the switch is instant.
        if (document.startViewTransition) document.startViewTransition(toggle);
        else toggle();
    });
    sync();
})();
