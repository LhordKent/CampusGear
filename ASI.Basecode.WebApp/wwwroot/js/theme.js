// Light and dark themes. _ThemeHead.cshtml sets <html data-theme> before first paint (saved choice, else the system
// setting); this script switches it from any [data-theme-toggle] button, saves the choice, and follows the system
// setting for as long as the user has not chosen.
(() => {
    const root = document.documentElement;
    const meta = document.querySelector('meta[name="theme-color"]');
    const system = window.matchMedia('(prefers-color-scheme: light)');
    const saved = () => { try { return localStorage.getItem('cg-theme'); } catch { return null; } };

    const sync = () => {
        const light = root.dataset.theme === 'light';
        const label = light ? 'Switch to dark mode' : 'Switch to light mode';
        if (meta) meta.content = light ? '#f3f6f8' : '#071b2b';
        document.querySelectorAll('[data-theme-toggle]').forEach(toggle => {
            toggle.setAttribute('aria-label', label);
            toggle.setAttribute('aria-pressed', String(light));
            toggle.title = label;
        });
    };
    const apply = theme => { root.dataset.theme = theme; sync(); };

    document.addEventListener('click', event => {
        if (!event.target.closest('[data-theme-toggle]')) return;
        const next = root.dataset.theme === 'light' ? 'dark' : 'light';
        try { localStorage.setItem('cg-theme', next); } catch { }
        // Crossfade where View Transitions are supported and motion is welcome; elsewhere the switch is instant.
        const calm = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        if (document.startViewTransition && !calm) document.startViewTransition(() => apply(next));
        else apply(next);
    });
    // Another tab changed the theme: follow it.
    addEventListener('storage', event => { if (event.key === 'cg-theme' && /^(light|dark)$/.test(event.newValue ?? '')) apply(event.newValue); });
    system.addEventListener('change', event => { if (!saved()) apply(event.matches ? 'light' : 'dark'); });
    sync();
})();
