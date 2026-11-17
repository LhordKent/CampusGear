// Automatic table search and filtering, in place.
// A form marked data-live-search updates the region named by its data-live-target as the user types (debounced) or
// changes a filter. The page is fetched with the same query string a normal submit would use, and only that region is
// swapped, the way the availability calendar updates. Pager links inside the region load in place too, and a
// data-live-reset link clears the filters. The address bar follows each change, so reload, back, and bookmarks work.
// Without JavaScript the form still submits and the links still navigate.
(() => {
    const typingDelay = 300;
    const parser = new DOMParser();

    const announce = (form, text) => {
        let status = form._status;
        if (!status) {
            status = document.createElement('p');
            status.className = 'visually-hidden';
            status.setAttribute('role', 'status');
            form.after(status);
            form._status = status;
        }
        status.textContent = text;
    };

    const setBusy = (form, region, busy) => {
        region.setAttribute('aria-busy', String(busy));
        form.classList.toggle('is-searching', busy);
    };

    /** The address the form would submit to, without empty fields. */
    const formUrl = form => {
        const url = new URL(form.getAttribute('action') || location.pathname, location.href);
        url.search = '';
        for (const [name, value] of new FormData(form)) {
            if (typeof value === 'string' && value.trim() !== '') url.searchParams.append(name, value.trim());
        }
        return url;
    };

    // Fades and lifts freshly swapped results in (css: .is-entering). Restarting the class replays it each time.
    const enter = region => {
        region.classList.remove('is-entering');
        void region.offsetWidth;
        region.classList.add('is-entering');
        region.addEventListener('animationend', () => region.classList.remove('is-entering'), { once: true });
    };

    async function load(form, url, { replaceForm = false, scroll = false, swap = [], animate = false } = {}) {
        const region = document.querySelector(form.dataset.liveTarget);
        if (!region) { location.href = url; return; }
        form._controller?.abort();
        const controller = form._controller = new AbortController();
        setBusy(form, region, true);
        try {
            const response = await fetch(url, {
                headers: { 'X-Requested-With': 'fetch', Accept: 'text/html' },
                credentials: 'same-origin',
                signal: controller.signal
            });
            // A lapsed session answers 401 to background requests; reloading sends the browser to sign-in.
            if (response.status === 401) { location.reload(); return; }
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            const doc = parser.parseFromString(await response.text(), 'text/html');
            const next = doc.querySelector(form.dataset.liveTarget);
            if (!next) throw new Error('The response had no results region.');
            region.replaceChildren(...next.childNodes);
            if (replaceForm) {
                const nextForm = doc.querySelector(`form[data-live-target="${form.dataset.liveTarget}"]`);
                if (nextForm) form.replaceChildren(...nextForm.childNodes);
            }
            // Other parts of the page that depend on the query, such as queue tabs and their heading.
            for (const selector of swap) {
                const current = document.querySelector(selector);
                const replacement = doc.querySelector(selector);
                if (current && replacement) {
                    current.replaceChildren(...replacement.childNodes);
                    document.dispatchEvent(new CustomEvent('campusgear:content', { detail: { root: current } }));
                }
            }
            if (animate) enter(region);
            history.replaceState(history.state, '', url.pathname + url.search);
            document.dispatchEvent(new CustomEvent('campusgear:content', { detail: { root: region } }));
            const count = region.querySelector('[data-live-count]');
            announce(form, count ? count.textContent.trim() : 'Results updated.');
            if (scroll && region.getBoundingClientRect().top < 0) region.scrollIntoView({ block: 'start' });
        } catch (error) {
            if (error.name === 'AbortError') return;
            window.CampusGear?.toast?.('The list could not be updated. Check your connection and try again.', 'error');
        } finally {
            if (form._controller === controller) setBusy(form, region, false);
        }
    }

    document.querySelectorAll('form[data-live-search]').forEach(form => {
        const region = document.querySelector(form.dataset.liveTarget);
        if (!region) return;
        region.setAttribute('aria-live', 'off');
        let timer;
        const run = () => { clearTimeout(timer); load(form, formUrl(form)); };

        form.addEventListener('input', event => {
            if (!event.target.matches('input[type="search"], input[type="text"], input:not([type])')) return;
            clearTimeout(timer);
            timer = setTimeout(run, typingDelay);
        });
        form.addEventListener('change', event => {
            if (event.target.matches('input[type="search"], input[type="text"], input:not([type])')) return;
            // A date being typed is not complete until it parses; an empty or valid date applies at once.
            if (event.target.type === 'date' && event.target.value === '' && !event.target.validity.valid) return;
            run();
        });
        // Enter applies the search immediately instead of reloading the page.
        form.addEventListener('submit', event => { event.preventDefault(); run(); });

        form.addEventListener('click', event => {
            const reset = event.target.closest('a[data-live-reset]');
            if (!reset) return;
            event.preventDefault();
            clearTimeout(timer);
            load(form, new URL(reset.href), { replaceForm: true });
        });
        region.addEventListener('click', event => {
            const link = event.target.closest('a[data-live-page]');
            if (!link || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey) return;
            event.preventDefault();
            load(form, new URL(link.href), { scroll: true });
        });
    });

    // Queue tabs (nav[data-live-tabs="#region"]) switch in place: the tab link is fetched, and the results, the form
    // (its hidden status and reset link), the tabs themselves, and any data-live-swap selectors are replaced.
    // Inside a .queue-tabs wrapper, its .queue-tabs-pill slides to the active tab: at once on click, then again if the
    // refreshed counts change the tab widths.
    document.querySelectorAll('nav[data-live-tabs]').forEach(nav => {
        const form = document.querySelector(`form[data-live-target="${nav.dataset.liveTabs}"]`);
        if (!form) return;
        const tabSelector = `nav[data-live-tabs="${nav.dataset.liveTabs}"]`;
        const extra = (nav.dataset.liveSwap || '').split(',').map(s => s.trim()).filter(Boolean);
        const wrapper = nav.closest('.queue-tabs');
        const pill = wrapper?.querySelector('.queue-tabs-pill');

        const placePill = () => {
            if (!pill) return;
            const active = nav.querySelector('a[aria-current="page"]');
            wrapper.classList.toggle('has-pill', Boolean(active));
            if (!active) return;
            pill.style.setProperty('--pill-x', `${nav.offsetLeft + active.offsetLeft}px`);
            pill.style.setProperty('--pill-w', `${active.offsetWidth}px`);
        };
        if (pill) {
            placePill();
            // Turn on the slide only after the first placement, so the pill does not fly in from the left on load.
            requestAnimationFrame(() => requestAnimationFrame(() => wrapper.classList.add('is-ready')));
            new ResizeObserver(placePill).observe(nav);
            document.addEventListener('campusgear:content', event => {
                if (event.detail?.root === nav || event.detail?.root?.contains?.(nav)) placePill();
            });
        }

        nav.addEventListener('click', event => {
            const tab = event.target.closest('a[href]');
            if (!tab || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey) return;
            event.preventDefault();
            if (tab.getAttribute('aria-current') === 'page') return;
            // Show the new tab as active right away (the pill starts sliding now); the swap confirms it with fresh counts.
            nav.querySelectorAll('a').forEach(a => { if (a !== tab) a.removeAttribute('aria-current'); });
            tab.setAttribute('aria-current', 'page');
            placePill();
            load(form, new URL(tab.href), { replaceForm: true, swap: [tabSelector, ...extra], animate: true });
        });
    });
})();
