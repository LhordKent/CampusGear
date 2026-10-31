// CampusGear toasts: short notices that stack in the top-right corner and dismiss themselves.
// Server messages opt in with data-toast="success|error|warning|info" on their element; the element is replaced by a
// toast (or kept on the page as well when it also has data-toast-keep). Scripts call CampusGear.toast(message, type).
// Without JavaScript the server message simply stays where the view rendered it.
(() => {
    const icons = { success: 'check-circle', error: 'x-circle', warning: 'alert', info: 'info' };
    const durations = { success: 4500, info: 4500, warning: 6500, error: 8000 };
    const recent = new Map();
    let region;

    const getRegion = () => {
        if (region?.isConnected) return region;
        region = document.createElement('section');
        region.className = 'toast-region';
        region.setAttribute('aria-label', 'Notifications');
        document.body.appendChild(region);
        return region;
    };

    const dismiss = item => {
        if (!item.isConnected || item.dataset.leaving) return;
        item.dataset.leaving = 'true';
        clearTimeout(item._timer);
        item.classList.add('is-leaving');
        // Remove once the exit transition ends; the timeout covers reduced motion, where no transition runs.
        const remove = () => item.remove();
        item.addEventListener('transitionend', remove, { once: true });
        setTimeout(remove, 400);
    };

    const schedule = (item, ms) => {
        clearTimeout(item._timer);
        item._remaining = ms;
        item._started = Date.now();
        item._timer = setTimeout(() => dismiss(item), ms);
    };

    function toast(message, type = 'info', options = {}) {
        message = String(message ?? '').trim();
        if (!message) return null;
        type = icons[type] ? type : 'info';
        // One action shows one notice: the same text within a few seconds is not repeated.
        const key = `${type}:${message}`;
        const existing = recent.get(key);
        if (existing?.isConnected && !existing.dataset.leaving) { schedule(existing, durations[type]); return existing; }

        const item = document.createElement('div');
        item.className = `toast toast-${type}`;
        // Errors interrupt assistive technology; everything else waits its turn.
        item.setAttribute('role', type === 'error' ? 'alert' : 'status');
        item.innerHTML = `<svg class="icon toast-icon" aria-hidden="true"><use href="/icons.svg#${icons[type]}"></use></svg>
            <div class="toast-body">${options.title ? '<strong></strong>' : ''}<p></p></div>
            <button type="button" class="toast-close" aria-label="Dismiss notification"><svg class="icon" aria-hidden="true"><use href="/icons.svg#x"></use></svg></button>
            <span class="toast-timer" aria-hidden="true"></span>`;
        if (options.title) item.querySelector('strong').textContent = options.title;
        item.querySelector('p').textContent = message;
        item.querySelector('.toast-close').addEventListener('click', () => dismiss(item));
        const ms = options.duration ?? durations[type];
        item.style.setProperty('--toast-duration', `${ms}ms`);
        // Hovering or focusing a toast holds it open so it can be read; leaving resumes the countdown.
        const pause = () => { clearTimeout(item._timer); item._remaining -= Date.now() - item._started; item.classList.add('is-paused'); };
        const resume = () => { if (item.dataset.leaving) return; item.classList.remove('is-paused'); schedule(item, Math.max(item._remaining, 1500)); };
        item.addEventListener('mouseenter', pause);
        item.addEventListener('mouseleave', resume);
        item.addEventListener('focusin', pause);
        item.addEventListener('focusout', resume);

        const list = getRegion();
        list.prepend(item);
        // Keep at most four on screen; the oldest leaves first.
        [...list.querySelectorAll('.toast:not(.is-leaving)')].slice(4).forEach(dismiss);
        requestAnimationFrame(() => requestAnimationFrame(() => item.classList.add('is-visible')));
        schedule(item, ms);
        recent.set(key, item);
        return item;
    }

    const fromPage = () => {
        document.querySelectorAll('[data-toast]').forEach(element => {
            const text = element.dataset.toastText || element.textContent;
            toast(text.replace(/\s+/g, ' '), element.dataset.toast, { title: element.dataset.toastTitle });
            element.removeAttribute('data-toast');
            if (!element.hasAttribute('data-toast-keep')) element.remove();
        });
    };

    window.CampusGear = Object.assign(window.CampusGear || {}, { toast, toastsFromPage: fromPage });
    fromPage();
})();
