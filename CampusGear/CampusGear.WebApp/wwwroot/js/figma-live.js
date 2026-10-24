(() => {
    const menu = document.querySelector('.live-navigation');
    if (menu) {
        const mobile = window.matchMedia('(max-width: 1000px)');
        const resize = () => { menu.open = !mobile.matches; };
        resize();
        mobile.addEventListener('change', resize);
    }
    document.querySelectorAll('.live-badge').forEach(badge => {
        badge.dataset.state = badge.textContent.trim().toLowerCase();
    });
})();
