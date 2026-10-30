(() => {
    // Navigation menus are always open on desktop and collapse behind their summary on small screens.
    const menus = [
        [document.querySelector('.live-navigation'), window.matchMedia('(max-width: 1000px)')],
        [document.querySelector('[data-site-menu]'), window.matchMedia('(max-width: 860px)')]
    ].filter(([menu]) => menu);
    menus.forEach(([menu, mobile]) => {
        const resize = () => { menu.open = !mobile.matches; };
        resize();
        mobile.addEventListener('change', resize);
    });
    // Badges take their colour from their text unless the view set data-state; content swapped in later is styled too.
    const badges = root => root.querySelectorAll('.live-badge').forEach(badge => {
        badge.dataset.state ??= badge.textContent.trim().toLowerCase();
    });
    badges(document);
    document.addEventListener('campusgear:content', event => {
        badges(event.detail.root);
        window.CampusGear?.toastsFromPage?.();
    });
})();
