// Date and time picker for the reservation dialog (Views/Borrower/Reservation.cshtml).
// It replaces the two datetime-local inputs with buttons that open a calendar and a list of times. Days and
// times when too few units are free are grayed out, using the busy times from /borrower/reservation/schedule,
// which follow the same rule as the booking check. The server still checks every request on submit.
//
// All times are Manila wall-clock strings ("yyyy-MM-ddTHH:mm"). They are turned into minute counts with
// Date.UTC so the math never depends on the browser's own time zone.
window.createReservationPicker = ({ form, start, end, quantity }) => {
    const STEP = 30;
    const DAY = 1440;
    const MONTHS_AHEAD = 12;
    const pad = n => String(n).padStart(2, '0');
    const toMin = value => {
        const [date, time = '00:00'] = value.split('T');
        const [y, m, d] = date.split('-').map(Number);
        const [h, mi] = time.split(':').map(Number);
        return Date.UTC(y, m - 1, d, h, mi) / 60000;
    };
    const toInput = min => {
        const d = new Date(min * 60000);
        return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())}T${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}`;
    };
    const dayOf = min => Math.floor(min / DAY) * DAY;
    const monthOf = min => { const d = new Date(min * 60000); return Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), 1) / 60000; };
    const addMonths = (month, n) => { const d = new Date(month * 60000); return Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + n, 1) / 60000; };
    const format = options => new Intl.DateTimeFormat('en-US', { timeZone: 'UTC', ...options });
    const timeText = (f => min => f.format(min * 60000))(format({ hour: 'numeric', minute: '2-digit' }));
    const shortDay = (f => min => f.format(min * 60000))(format({ weekday: 'short', month: 'short', day: 'numeric' }));
    const longDay = (f => min => f.format(min * 60000))(format({ weekday: 'long', month: 'long', day: 'numeric' }));
    const monthText = (f => min => f.format(min * 60000))(format({ month: 'long', year: 'numeric' }));
    const duration = minutes => {
        const h = Math.floor(minutes / 60), m = minutes % 60;
        return h && m ? `${h} hr ${m} min` : h ? `${h} hr` : `${m} min`;
    };

    /* ---------- Schedule data ---------- */
    // data: { bookable, now, to, busy: [{ unit, s, e }] } in minutes; null while loading or after a failure.
    const cache = new Map();
    let itemId = null;
    let data = null;
    let failed = false;
    let loading = null;

    const nowMin = () => data ? data.now : toMin(start.min) - 1;
    const need = () => Math.max(1, Math.round(Number(quantity.value)) || 1);
    const freeUnits = (a, b) => {
        if (!data) return Infinity;
        const taken = new Set();
        for (const x of data.busy) if (x.s < b && x.e > a) taken.add(x.unit);
        return data.bookable - taken.size;
    };
    // A start time works when the first 30 minutes have enough free units; every return time is at least that far out.
    // Fair-use limits from the server (BorrowingLimits): how far ahead a booking may start, and how long it may last.
    const latestStart = () => data && data.maxAdvance ? data.now + data.maxAdvance : Infinity;
    const startState = t => {
        if (t <= nowMin()) return 'past';
        if (t > latestStart()) return 'beyond';
        if (!data) return failed ? 'open' : 'unknown';
        if (t + STEP > data.to) return 'unknown';
        return freeUnits(t, t + STEP) >= need() ? 'open' : 'booked';
    };
    // The latest return time before the next booking takes away too many units (no limit until bookings load).
    const endLimit = s => {
        if (!data) return Infinity;
        const longest = data.maxDuration ? s + data.maxDuration : Infinity;
        let e = s;
        while (e + STEP <= data.to && e + STEP <= longest && freeUnits(s, e + STEP) >= need()) e += STEP;
        return e;
    };

    // WBS 25.00 | Request the busy intervals used to disable times in the picker.
    const fetchWindow = async (id, fromDay) => {
        const query = new URLSearchParams({ equipmentItemId: id });
        if (fromDay !== undefined) query.set('from', toInput(fromDay).slice(0, 10));
        const response = await fetch(`/borrower/reservation/schedule?${query}`, { headers: { Accept: 'application/json' } });
        if (!response.ok) throw new Error(`Schedule request failed (${response.status})`);
        const json = await response.json();
        return {
            bookable: json.bookableUnits,
            now: toMin(json.now),
            to: toMin(json.to),
            maxAdvance: json.maxAdvanceMinutes || 0,
            maxDuration: json.maxDurationMinutes || 0,
            busy: json.busy.map(x => ({ unit: x.unit, s: toMin(x.start), e: toMin(x.end) }))
        };
    };
    const load = id => {
        loading = fetchWindow(id)
            .then(result => { cache.set(id, result); if (id === itemId) { data = result; failed = false; } })
            .catch(() => { if (id === itemId) failed = true; })
            .finally(() => { if (id === itemId) { loading = null; render(); } });
    };
    // Months past the loaded window are fetched as the borrower navigates to them.
    const ensure = until => {
        if (!data || loading || until <= data.to) return;
        const id = itemId;
        const current = data;
        loading = fetchWindow(id, dayOf(current.to))
            .then(more => {
                current.busy = current.busy.concat(more.busy);
                current.to = more.to;
                current.now = more.now;
                current.maxAdvance = more.maxAdvance;
                current.maxDuration = more.maxDuration;
            })
            .catch(() => { /* Keep what is loaded; the server still checks on submit. */ })
            .finally(() => { if (id === itemId) { loading = null; render(); } });
    };

    /* ---------- Markup ---------- */
    const triggers = {};
    const makeTrigger = (input, which) => {
        const label = input.closest('label');
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'when-trigger';
        button.id = `${input.id}-picker`;
        button.setAttribute('aria-expanded', 'false');
        button.setAttribute('aria-controls', 'reserve-when');
        button.innerHTML = '<svg class="when-trigger-icon" viewBox="0 0 24 24" aria-hidden="true"><rect x="3.5" y="5" width="17" height="15" rx="2.5"/><path d="M3.5 9.5h17M8 3v4M16 3v4"/></svg>'
            + '<span class="when-trigger-text"></span>'
            + '<svg class="when-trigger-chevron" viewBox="0 0 24 24" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg>';
        input.type = 'hidden';
        input.after(button);
        if (label) label.htmlFor = button.id;
        button.addEventListener('click', () => (mode === which && !panel.hidden ? close() : open(which)));
        triggers[which] = button;
        return button;
    };
    makeTrigger(start, 'start');
    makeTrigger(end, 'end');

    const panel = document.createElement('div');
    panel.className = 'when-panel';
    panel.id = 'reserve-when';
    panel.hidden = true;
    panel.setAttribute('role', 'group');
    panel.setAttribute('aria-labelledby', 'reserve-when-title');
    panel.innerHTML = `
        <div class="when-head">
            <div><p class="when-title" id="reserve-when-title"></p><p class="when-sub" data-when="sub"></p></div>
            <button type="button" class="when-close" aria-label="Close date picker" data-when="close"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18"/></svg></button>
        </div>
        <div class="when-body">
            <div class="when-calendar">
                <div class="when-month">
                    <button type="button" class="when-nav" aria-label="Previous month" data-when="prev"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="m15 6-6 6 6 6"/></svg></button>
                    <p class="when-month-label" aria-live="polite" data-when="month"></p>
                    <button type="button" class="when-nav" aria-label="Next month" data-when="next"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="m9 6 6 6-6 6"/></svg></button>
                </div>
                <div class="when-weekdays" aria-hidden="true"><span>Mo</span><span>Tu</span><span>We</span><span>Th</span><span>Fr</span><span>Sa</span><span>Su</span></div>
                <div class="when-days" data-when="days"></div>
            </div>
            <div class="when-times">
                <p class="when-day-label" data-when="day"></p>
                <div class="when-slots" data-when="slots"></div>
            </div>
        </div>
        <div class="when-foot">
            <ul class="when-legend" data-when="legend"></ul>
            <p class="when-status" role="status" data-when="status"></p>
        </div>`;
    const part = name => panel.querySelector(`[data-when="${name}"]`);
    const title = panel.querySelector('.when-title');
    const days = part('days');
    const slots = part('slots');
    const status = part('status');
    // The panel sits under the date fields, inside the same fieldset, so it is disabled with them.
    (form.querySelector('.reserve-grid') || end.closest('label')).after(panel);

    /* ---------- Keeping the picker in view ---------- */
    // The dialog body scrolls; each step of the picker scrolls it for the borrower, so they never hunt for the next step.
    const scroller = panel.closest('.reserve-scroll');
    const behavior = () => (matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth');
    const isStacked = () => getComputedStyle(panel.querySelector('.when-body')).gridTemplateColumns.trim().split(/\s+/).length === 1;
    // Scrolls just enough to show everything from `top` to `bottom`; if that is taller than the view, shows `top` first.
    const reveal = (top, bottom = top, margin = 12) => {
        if (!top || !bottom) return;
        if (!scroller) { bottom.scrollIntoView({ block: 'nearest', behavior: behavior() }); return; }
        const box = scroller.getBoundingClientRect();
        const from = top.getBoundingClientRect().top - box.top + scroller.scrollTop - margin;
        const to = bottom.getBoundingClientRect().bottom - box.top + scroller.scrollTop + margin;
        const view = scroller.clientHeight;
        let target = scroller.scrollTop;
        if (to - from > view || from < target) target = from;
        else if (to > target + view) target = to - view;
        if (Math.abs(target - scroller.scrollTop) > 1) scroller.scrollTo({ top: target, behavior: behavior() });
    };
    const fieldOf = which => triggers[which].closest('label') || triggers[which];
    // Show the field with the whole picker under it; on short screens, show the picker itself in full.
    const revealPanel = which => requestAnimationFrame(() => {
        const label = fieldOf(which);
        const tooTall = scroller && panel.getBoundingClientRect().bottom - label.getBoundingClientRect().top + 24 > scroller.clientHeight;
        reveal(tooTall ? panel : label, panel);
    });

    /* ---------- State ---------- */
    let mode = 'start';
    let viewMonth = 0;
    let day = 0;
    const chosenStart = () => (start.value ? toMin(start.value) : null);
    const chosenEnd = () => (end.value ? toMin(end.value) : null);
    const today = () => dayOf(nowMin());

    const updateTriggers = () => {
        const s = chosenStart(), e = chosenEnd();
        triggers.start.querySelector('.when-trigger-text').textContent = s === null ? 'Choose a date and time' : `${shortDay(s)} · ${timeText(s)}`;
        triggers.end.querySelector('.when-trigger-text').textContent = e === null ? 'Choose a date and time' : `${shortDay(e)} · ${timeText(e)}`;
        triggers.start.classList.toggle('is-empty', s === null);
        triggers.end.classList.toggle('is-empty', e === null);
    };

    /* ---------- Day and time states ---------- */
    const startDayState = d => {
        let future = 0, open = 0, booked = 0, unknown = 0;
        for (let t = d; t < d + DAY; t += STEP) {
            const state = startState(t);
            if (state === 'past' || state === 'beyond') continue;
            future++;
            if (state === 'open') open++; else if (state === 'booked') booked++; else unknown++;
        }
        // A day wholly past the booking window looks like a past day (not selectable) but is described differently.
        if (future === 0) return d > latestStart() ? 'beyond' : 'past';
        if (unknown === future) return 'unknown';
        if (open === 0 && unknown === 0) return 'full';
        return booked > 0 ? 'partial' : 'free';
    };
    const endDayState = (d, s, limit) => {
        let after = 0, open = 0;
        for (let t = d; t < d + DAY; t += STEP) {
            if (t <= s) continue;
            after++;
            if (t <= limit) open++;
        }
        if (after === 0) return 'past';
        if (open === 0) return 'full';
        return open < after ? 'partial' : 'free';
    };
    const dayDescription = (state, limit) => {
        if (mode === 'end') {
            return { past: 'before the borrowing time', full: 'unavailable, the item is booked before then',
                partial: `available until ${timeText(limit)}`, free: 'available', unknown: 'loading' }[state];
        }
        return { past: 'past', beyond: 'too far ahead to book', full: 'fully booked', partial: 'partly booked', free: 'available', unknown: 'loading' }[state];
    };

    /* ---------- Rendering ---------- */
    const legend = () => {
        const items = mode === 'end'
            ? [['free', 'Available'], ['full', 'Not possible: booked in between']]
            : [['free', 'Available'], ['partial', 'Partly booked'], ['full', 'Fully booked']];
        part('legend').innerHTML = items.map(([state, text]) => `<li><i data-state="${state}" aria-hidden="true"></i>${text}</li>`).join('');
    };

    const renderDays = (s, limit) => {
        const first = new Date(viewMonth * 60000);
        const gridStart = viewMonth - ((first.getUTCDay() + 6) % 7) * DAY;
        part('month').textContent = monthText(viewMonth);
        part('prev').disabled = viewMonth <= monthOf(today());
        // Return times can run into the month after the booking window, so the end picker may go one month further.
        const lastMonth = Number.isFinite(latestStart())
            ? monthOf(mode === 'end' && s !== null ? s + (data.maxDuration || 0) : latestStart())
            : addMonths(monthOf(today()), MONTHS_AHEAD);
        part('next').disabled = viewMonth >= lastMonth;
        const fragment = document.createDocumentFragment();
        for (let i = 0; i < 42; i++) {
            const d = gridStart + i * DAY;
            const state = mode === 'end' ? endDayState(d, s, limit) : startDayState(d);
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'when-day';
            button.dataset.day = String(d);
            button.dataset.state = state;
            button.textContent = String(new Date(d * 60000).getUTCDate());
            button.tabIndex = d === day ? 0 : -1;
            button.classList.toggle('is-outside', new Date(d * 60000).getUTCMonth() !== first.getUTCMonth());
            button.classList.toggle('is-today', d === today());
            button.setAttribute('aria-pressed', String(d === day));
            button.setAttribute('aria-label', `${longDay(d)}, ${dayDescription(state, limit)}`);
            if (state === 'past' || state === 'beyond' || state === 'full') button.setAttribute('aria-disabled', 'true');
            // Styled like a past day: greyed and not selectable.
            if (state === 'beyond') button.dataset.state = 'past';
            fragment.append(button);
        }
        days.replaceChildren(fragment);
        if (!days.querySelector('[tabindex="0"]')) {
            (days.querySelector('.when-day:not(.is-outside):not([aria-disabled])') || days.firstElementChild).tabIndex = 0;
        }
    };

    const renderSlots = (s, limit) => {
        part('day').textContent = longDay(day);
        const chosen = mode === 'end' ? chosenEnd() : chosenStart();
        const fragment = document.createDocumentFragment();
        let focusTarget = null;
        for (let t = day; t < day + DAY; t += STEP) {
            let state, tag = '';
            if (mode === 'end') {
                if (t <= s) continue;
                state = t <= limit ? 'open' : 'booked';
                const tooLong = data && data.maxDuration && t - s > data.maxDuration;
                tag = state === 'open' ? duration(t - s) : tooLong ? `Over ${data.maxDuration / DAY}-day limit` : 'Unavailable';
            } else {
                state = startState(t);
                if (state === 'past' || state === 'beyond') continue;
                if (state === 'booked') tag = 'Booked';
                else if (state === 'open' && data && data.bookable > 1) {
                    const free = freeUnits(t, t + STEP);
                    if (free < data.bookable) tag = `${free} of ${data.bookable} free`;
                }
            }
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'when-slot';
            button.dataset.time = String(t);
            button.dataset.state = state;
            button.tabIndex = -1;
            button.innerHTML = `<span class="when-slot-time"></span><span class="when-slot-tag"></span>`;
            button.firstChild.textContent = timeText(t);
            button.lastChild.textContent = tag;
            button.setAttribute('aria-pressed', String(t === chosen));
            button.setAttribute('aria-label', `${timeText(t)}${tag ? `, ${tag.toLowerCase()}` : ''}`);
            if (state !== 'open') button.setAttribute('aria-disabled', 'true');
            if (t === chosen) focusTarget = button;
            else if (!focusTarget && state === 'open') focusTarget = button;
            fragment.append(button);
        }
        slots.replaceChildren(fragment);
        if (!slots.firstElementChild) {
            const note = document.createElement('p');
            note.className = 'when-empty';
            note.textContent = mode === 'end' ? 'No return times on this day.' : 'No times left on this day.';
            slots.append(note);
            return;
        }
        (focusTarget || slots.firstElementChild).tabIndex = 0;
        // Scroll the time list itself so the chosen time sits a row down (earlier options still peek in above it),
        // or the first open time sits at the top. Booked mornings never have to be scrolled past by hand.
        if (focusTarget) {
            const row = focusTarget.offsetTop - slots.offsetTop;
            const isChosen = focusTarget.getAttribute('aria-pressed') === 'true';
            slots.scrollTop = Math.max(0, row - (isChosen ? focusTarget.offsetHeight + 14 : 4));
        }
    };

    const render = () => {
        updateTriggers();
        if (panel.hidden) return;
        const s = mode === 'end' ? chosenStart() : null;
        const limit = s === null ? null : endLimit(s);
        title.textContent = mode === 'end' ? 'Choose when you’ll return it' : 'Choose when to borrow';
        const units = need();
        part('sub').textContent = mode === 'end'
            ? `Borrowing ${shortDay(s)} at ${timeText(s)}. Gray times would overlap another booking.`
            : `Showing times when ${units === 1 ? 'a unit is' : `${units} units are`} free. Gray days and times are already booked.`;
        status.textContent = failed
            ? 'Bookings couldn’t be loaded, so times aren’t grayed out. The desk still checks your request.'
            : !data ? 'Loading bookings…' : '';
        legend();
        renderDays(s, limit);
        renderSlots(s, limit);
        ensure(addMonths(viewMonth, 1) + 7 * DAY);
    };

    /* ---------- Opening, closing, picking ---------- */
    const open = which => {
        if (which === 'end' && chosenStart() === null) which = 'start';
        mode = which;
        const reference = which === 'end' ? (chosenEnd() ?? chosenStart()) : chosenStart();
        day = reference !== null && reference > nowMin() ? dayOf(reference) : today();
        viewMonth = monthOf(day);
        panel.hidden = false;
        triggers.start.setAttribute('aria-expanded', String(which === 'start'));
        triggers.end.setAttribute('aria-expanded', String(which === 'end'));
        render();
        revealPanel(which);
    };
    const close = () => {
        if (panel.hidden) return;
        panel.hidden = true;
        triggers.start.setAttribute('aria-expanded', 'false');
        triggers.end.setAttribute('aria-expanded', 'false');
    };

    const pickStart = t => {
        start.value = toInput(t);
        start.dispatchEvent(new Event('change', { bubbles: true }));
        // The catalog script moves the return time forward if needed; keep it inside the free window too.
        const limit = endLimit(t);
        const e = chosenEnd();
        if (e === null || e <= t || e > limit) {
            end.value = toInput(Math.min(t + 120, limit));
            end.dispatchEvent(new Event('change', { bubbles: true }));
        }
        mode = 'end';
        day = dayOf(chosenEnd());
        viewMonth = monthOf(day);
        triggers.start.setAttribute('aria-expanded', 'false');
        triggers.end.setAttribute('aria-expanded', 'true');
        render();
        status.textContent = `Borrowing time set to ${shortDay(t)}, ${timeText(t)}. Now choose a return time.`;
        // preventScroll: the picker does its own smooth scrolling instead of the browser's jump to the focused time.
        slots.querySelector('[tabindex="0"]')?.focus({ preventScroll: true });
        revealPanel('end');
    };
    const pickEnd = t => {
        end.value = toInput(t);
        end.dispatchEvent(new Event('change', { bubbles: true }));
        close();
        updateTriggers();
        triggers.end.focus({ preventScroll: true });
        // Both times are set: bring the next field into view, with the chosen times still visible above it.
        requestAnimationFrame(() => {
            const next = form.elements.Purpose?.closest('label') || form.elements.Purpose;
            reveal(fieldOf('start'), next || fieldOf('end'));
        });
    };

    days.addEventListener('click', event => {
        const button = event.target.closest('.when-day');
        if (!button || button.getAttribute('aria-disabled') === 'true') return;
        day = Number(button.dataset.day);
        if (monthOf(day) !== viewMonth) viewMonth = monthOf(day);
        render();
        days.querySelector(`[data-day="${day}"]`)?.focus({ preventScroll: true });
        // On phones the times sit under the calendar: bring them up as soon as a day is tapped.
        if (isStacked()) requestAnimationFrame(() => reveal(part('day'), slots));
    });
    slots.addEventListener('click', event => {
        const button = event.target.closest('.when-slot');
        if (!button || button.getAttribute('aria-disabled') === 'true') return;
        (mode === 'end' ? pickEnd : pickStart)(Number(button.dataset.time));
    });
    part('prev').addEventListener('click', () => { viewMonth = addMonths(viewMonth, -1); render(); });
    part('next').addEventListener('click', () => { viewMonth = addMonths(viewMonth, 1); render(); });
    part('close').addEventListener('click', () => { const which = mode; close(); triggers[which].focus(); });

    // Arrow keys move through days (by day and by week) and through times; Tab leaves each group.
    const rove = (container, selector, columns) => container.addEventListener('keydown', event => {
        const items = [...container.querySelectorAll(selector)];
        const index = items.indexOf(document.activeElement);
        if (index < 0) return;
        const step = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: -columns(), ArrowDown: columns(), Home: -index, End: items.length - 1 - index }[event.key];
        if (step === undefined) return;
        event.preventDefault();
        const next = items[Math.max(0, Math.min(items.length - 1, index + step))];
        items.forEach(item => { item.tabIndex = -1; });
        next.tabIndex = 0;
        next.focus();
    });
    rove(days, '.when-day', () => 7);
    rove(slots, '.when-slot', () => {
        const items = slots.querySelectorAll('.when-slot');
        return items.length > 1 && items[1].offsetTop === items[0].offsetTop
            ? Math.round(slots.clientWidth / items[0].offsetWidth) || 1 : 1;
    });
    // Escape closes the picker without closing the whole dialog (the dialog's cancel handler also checks isOpen).
    const closeAndReturn = () => { const which = mode; close(); triggers[which].focus(); };
    panel.addEventListener('keydown', event => {
        if (event.key !== 'Escape') return;
        event.preventDefault();
        event.stopPropagation();
        closeAndReturn();
    });

    updateTriggers();
    return {
        // Called when the dialog shows another product.
        setItem(id) {
            if (id === itemId) { render(); return; }
            itemId = id;
            close();
            data = cache.get(id) || null;
            failed = false;
            loading = null;
            if (!data && id) load(id);
            updateTriggers();
        },
        refresh: render,
        close,
        isOpen: () => !panel.hidden,
        triggerFor: name => (name === 'StartLocal' ? triggers.start : name === 'EndLocal' ? triggers.end : null),
        setInvalid(name, invalid) {
            const trigger = name === 'StartLocal' ? triggers.start : name === 'EndLocal' ? triggers.end : null;
            trigger?.setAttribute('aria-invalid', invalid ? 'true' : 'false');
        }
    };
};
