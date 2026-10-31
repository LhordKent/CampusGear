// Availability calendars (Views/Borrower/Calendar.cshtml and Views/Custodian/Calendar.cshtml).
// Choosing equipment in the picker, changing month, or picking a day fetches the same calendar URL and swaps only the regions
// marked data-calendar-region, so the page never reloads or jumps to the top. The server renders exactly what a
// normal navigation would, and without JavaScript the links and the form still work as plain requests.
(() => {
    const root = document.querySelector('[data-calendar]');
    if (!root || !window.fetch || !window.DOMParser) return;

    const form = root.querySelector('[data-calendar-form]');
    const picker = root.querySelector('[data-equipment-picker]');
    const status = root.querySelector('[data-calendar-status]');
    const regions = () => [...root.querySelectorAll('[data-calendar-region]')];
    let request = null;
    let slowTimer = 0;

    const setBusy = busy => {
        regions().forEach(region => region.setAttribute('aria-busy', String(busy)));
        clearTimeout(slowTimer);
        if (!status) return;
        if (busy) {
            // Only announce loading when it takes long enough to notice.
            slowTimer = setTimeout(() => {
                status.classList.remove('is-error');
                status.textContent = 'Loading schedule…';
                status.hidden = false;
            }, 150);
        } else if (!status.classList.contains('is-error')) {
            status.hidden = true;
            status.textContent = '';
        }
    };

    const showError = message => {
        if (!status) return;
        status.classList.add('is-error');
        status.textContent = message;
        status.hidden = false;
    };

    const load = async (url, focus) => {
        request?.abort();
        request = new AbortController();
        status?.classList.remove('is-error');
        setBusy(true);
        try {
            const response = await fetch(url, {
                signal: request.signal,
                credentials: 'same-origin',
                headers: { 'X-Requested-With': 'fetch' }
            });
            // A login redirect or error page is not a calendar; fall back to a normal navigation.
            if (!response.ok || new URL(response.url).pathname !== new URL(url, location.href).pathname) {
                location.assign(url);
                return;
            }
            const next = new DOMParser().parseFromString(await response.text(), 'text/html');
            regions().forEach(region => {
                const replacement = next.querySelector(`[data-calendar-region="${region.dataset.calendarRegion}"]`);
                if (replacement) region.replaceWith(document.importNode(replacement, true));
            });
            root.querySelectorAll('.live-badge:not([data-state])').forEach(badge => {
                badge.dataset.state = badge.textContent.trim().toLowerCase();
            });
            // Keep the form's month and equipment in step with what is now shown.
            const nextForm = next.querySelector('[data-calendar-form]');
            nextForm?.querySelectorAll('input[type=hidden]').forEach(input => {
                const current = form?.elements[input.name];
                if (current) current.value = input.value;
            });
            markSelected(form?.elements.equipmentItemId?.value);
            // The address bar follows the calendar so a refresh or shared link shows the same view.
            history.replaceState(history.state, '', url);
            setBusy(false);
            focus?.();
        } catch (error) {
            if (error.name === 'AbortError') return;
            setBusy(false);
            showError('The schedule could not be loaded. Check your connection and try again.');
        }
    };

    // The picker card for the unit on show is highlighted; its links keep the month that is on screen.
    function markSelected(id) {
        picker?.querySelectorAll('[data-picker-link]').forEach(link => {
            const selected = link.dataset.id === id;
            link.classList.toggle('is-selected', selected);
            if (selected) link.setAttribute('aria-current', 'true'); else link.removeAttribute('aria-current');
        });
    }
    picker?.addEventListener('click', event => {
        const link = event.target.closest('a[data-picker-link]');
        if (!link || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        if (form?.elements.equipmentItemId) form.elements.equipmentItemId.value = link.dataset.id;
        markSelected(link.dataset.id);
        const query = new URLSearchParams(new FormData(form));
        load(`${form.getAttribute('action')}?${query}`, () => {
            // On a narrow screen the calendar sits below the picker: bring it into view.
            const calendar = root.querySelector('[data-calendar-region="month"]');
            if (calendar && calendar.getBoundingClientRect().top > innerHeight * 0.6) calendar.scrollIntoView({ behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth', block: 'start' });
        });
    });
    form?.addEventListener('submit', event => {
        event.preventDefault();
        load(`${form.getAttribute('action')}?${new URLSearchParams(new FormData(form))}`);
    });

    root.addEventListener('click', event => {
        const link = event.target.closest('a[data-calendar-link]');
        if (!link || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        if (link.hasAttribute('data-calendar-day')) {
            // Show the new selection at once; the server response confirms it.
            root.querySelectorAll('[data-calendar-day][aria-current="date"]').forEach(day => {
                day.removeAttribute('aria-current');
                day.classList.remove('selected', 'day-selected');
            });
            link.setAttribute('aria-current', 'date');
            link.classList.add(link.classList.contains('borrower-day') ? 'day-selected' : 'selected');
            load(link.href, () => root.querySelector('[data-calendar-day][aria-current="date"]')?.focus({ preventScroll: true }));
        } else {
            const label = link.getAttribute('aria-label');
            load(link.href, () => root.querySelector(`a[data-calendar-link][aria-label="${label}"]`)?.focus({ preventScroll: true }));
        }
    });
})();
