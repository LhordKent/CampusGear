// CampusGear motion helpers. They only add classes for CSS (see css/motion.css);
// nothing here cancels, delays, or blocks a click, submit, or navigation.
(() => {
    const root = document.documentElement;
    const bar = document.createElement('div');
    bar.className = 'motion-progress';
    bar.setAttribute('aria-hidden', 'true');
    document.body.appendChild(bar);

    const startLoading = button => {
        root.classList.add('motion-navigating');
        if (button) button.classList.add('motion-busy');
    };
    const stopLoading = () => {
        root.classList.remove('motion-navigating');
        document.querySelectorAll('.motion-busy').forEach(el => el.classList.remove('motion-busy'));
    };

    // Same-tab navigation to another page of this site shows the progress bar.
    document.addEventListener('click', event => {
        if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        const link = event.target.closest('a[href]');
        if (!link || link.target && link.target !== '_self' || link.hasAttribute('download')) return;
        const url = new URL(link.href, location.href);
        if (url.origin !== location.origin || url.pathname === location.pathname && url.search === location.search && url.hash) return;
        startLoading();
    });

    // A form that is really being submitted shows the bar and a spinner on its submit button.
    document.addEventListener('submit', event => {
        if (event.defaultPrevented || event.target.target === '_blank') return;
        const button = event.submitter || event.target.querySelector('[type="submit"]');
        startLoading(button && !button.closest('.auth-inline-form') ? button : null);
    });

    // Back/forward cache restores the page as it was left; clear any loading state.
    addEventListener('pageshow', stopLoading);

    // Browser-blocked submits (empty required field, bad format) shake the offending field.
    document.addEventListener('invalid', event => {
        const field = event.target;
        field.classList.remove('motion-shake');
        void field.offsetWidth;
        field.classList.add('motion-shake');
        field.addEventListener('animationend', () => field.classList.remove('motion-shake'), { once: true });
    }, true);
})();
