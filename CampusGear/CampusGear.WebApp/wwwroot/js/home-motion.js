// Borrower homepage: sections and their cards fade up as they scroll into view, in a short stagger.
// Content is only hidden once this script runs (the .home-reveal-ready class), and nothing moves for people who ask
// their system for reduced motion; see "Borrower homepage motion" in css/workspace.css.
(() => {
    const home = document.querySelector('.home');
    if (!home || !('IntersectionObserver' in window) || matchMedia('(prefers-reduced-motion: reduce)').matches) return;

    const targets = [...home.querySelectorAll('[data-reveal]')];
    // Cards in the same list follow one another by 60 ms, capped so a long list never feels slow.
    home.querySelectorAll('[data-reveal-group]').forEach(group => {
        [...group.children].forEach((child, index) => {
            child.setAttribute('data-reveal', '');
            child.style.setProperty('--reveal-delay', `${Math.min(index, 6) * 60}ms`);
            targets.push(child);
        });
    });

    const observer = new IntersectionObserver(entries => {
        entries.forEach(entry => {
            if (!entry.isIntersecting) return;
            entry.target.classList.add('is-inview');
            observer.unobserve(entry.target);
        });
    }, { rootMargin: '0px 0px -8% 0px', threshold: 0.12 });

    // Anything already on screen at load shows at once rather than waiting for a scroll.
    document.documentElement.classList.add('home-reveal-ready');
    targets.forEach(target => observer.observe(target));
})();
