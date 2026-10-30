// Borrower equipment catalog (Views/Borrower/Reservation.cshtml): filtering, the request dialog,
// live availability, and client-side checks. The server repeats every check on submit.
(() => {
    const catalog = document.querySelector('[data-catalog]');
    const dialog = document.querySelector('[data-reserve-dialog]');
    if (!catalog || !dialog) return;

    const cards = [...catalog.querySelectorAll('[data-card]')];
    const form = dialog.querySelector('[data-reserve-form]');
    const field = name => dialog.querySelector(`[data-field="${name}"]`);
    const quantity = field('quantity');
    const start = form.elements.StartLocal;
    const end = form.elements.EndLocal;
    const purpose = form.elements.Purpose;
    const submit = form.querySelector('[data-submit]');
    const alertBox = dialog.querySelector('[data-reserve-alert]');
    const availabilityHint = dialog.querySelector('[data-availability-hint]');
    const eligible = dialog.dataset.eligible === 'true';
    let current = cards.find(card => card.dataset.id === field('id').value) || null;
    let freeUnits = null;
    let submitting = false;

    /* ---------- Filtering ---------- */
    const filters = catalog.querySelector('[data-catalog-filters]');
    const search = catalog.querySelector('[data-catalog-search]');
    const availableOnly = catalog.querySelector('[data-catalog-available]');
    const chips = [...catalog.querySelectorAll('.catalog-chip')];
    const count = catalog.querySelector('[data-catalog-count]');
    const empty = catalog.querySelector('[data-catalog-empty]');
    let category = '';
    filters.hidden = false;

    const applyFilters = () => {
        const term = search.value.trim().toLowerCase();
        let shown = 0;
        cards.forEach(card => {
            const text = `${card.dataset.name} ${card.dataset.category} ${card.dataset.description}`.toLowerCase();
            const visible = (!term || text.includes(term)) &&
                (!category || card.dataset.category === category) &&
                (!availableOnly.checked || card.dataset.requestable === 'true');
            card.hidden = !visible;
            if (visible) shown++;
        });
        count.textContent = `${shown} ${shown === 1 ? 'item' : 'items'}`;
        empty.hidden = shown > 0;
    };
    search.addEventListener('input', applyFilters);
    availableOnly.addEventListener('change', applyFilters);
    chips.forEach(chip => chip.addEventListener('click', () => {
        category = chip.dataset.category;
        chips.forEach(other => other.setAttribute('aria-pressed', String(other === chip)));
        applyFilters();
    }));
    catalog.querySelector('[data-catalog-reset]').addEventListener('click', () => {
        search.value = '';
        availableOnly.checked = false;
        category = '';
        chips.forEach(chip => chip.setAttribute('aria-pressed', String(chip.dataset.category === '')));
        applyFilters();
        search.focus();
    });

    /* ---------- Dialog content ---------- */
    const statusClass = availability =>
        availability === 'Available' ? 'is-available' : availability === 'In use' ? 'is-in-use' : 'is-unavailable';

    const renderMedia = card => {
        const media = field('media');
        media.replaceChildren();
        if (card.dataset.image) {
            const img = document.createElement('img');
            img.src = card.dataset.image;
            img.alt = card.dataset.name;
            media.append(img);
        } else {
            const holder = document.createElement('span');
            holder.className = 'catalog-placeholder';
            holder.setAttribute('aria-hidden', 'true');
            holder.innerHTML = '<svg viewBox="0 0 24 24"><use></use></svg>';
            holder.querySelector('use').setAttribute('href', `#cg-icon-${card.dataset.icon}`);
            media.append(holder);
        }
    };

    const clearErrors = () => {
        alertBox.hidden = true;
        alertBox.replaceChildren();
        dialog.querySelectorAll('[data-error-for]').forEach(el => { el.textContent = ''; });
    };

    const fill = card => {
        const switching = card !== current;
        current = card;
        const requestable = card.dataset.requestable === 'true';
        field('id').value = card.dataset.id;
        field('status-host').className = `reserve-equipment ${statusClass(card.dataset.availability)}`;
        field('category').textContent = card.dataset.category;
        field('name').textContent = card.dataset.name;
        field('availability').textContent = card.dataset.availability;
        field('availability-detail').textContent = card.dataset.availabilityDetail;
        field('description').textContent = card.dataset.description || 'No description provided yet.';
        field('quantity-text').textContent = `${card.dataset.bookable} of ${card.dataset.total}`;
        field('condition').textContent = card.dataset.condition;
        field('location').textContent = card.dataset.location;
        field('unavailable').hidden = requestable;
        field('fields').disabled = !requestable;
        submit.disabled = !requestable || !eligible;
        renderMedia(card);
        if (switching) {
            clearErrors();
            quantity.value = '1';
        }
        freeUnits = null;
        updateQuantityLimits();
    };

    /* ---------- Quantity stepper ---------- */
    const maxQuantity = () => {
        const cardMax = Number(current?.dataset.max || 1);
        return Math.max(1, freeUnits === null ? cardMax : Math.min(cardMax, freeUnits));
    };
    const updateQuantityLimits = () => {
        const max = maxQuantity();
        quantity.max = String(max);
        field('quantity-hint').textContent = max === 1 ? 'Only 1 unit can be requested' : `Up to ${max} per request`;
        const value = Number(quantity.value);
        dialog.querySelector('[data-step="-1"]').disabled = !(value > 1);
        dialog.querySelector('[data-step="1"]').disabled = !(value < max);
    };
    dialog.querySelectorAll('[data-step]').forEach(button => button.addEventListener('click', () => {
        const next = Math.min(maxQuantity(), Math.max(1, (Number(quantity.value) || 1) + Number(button.dataset.step)));
        quantity.value = String(next);
        updateQuantityLimits();
        setError('Quantity', '');
    }));
    quantity.addEventListener('input', updateQuantityLimits);
    quantity.addEventListener('blur', () => {
        const value = Math.round(Number(quantity.value));
        quantity.value = String(Number.isFinite(value) ? Math.min(maxQuantity(), Math.max(1, value)) : 1);
        updateQuantityLimits();
    });

    /* ---------- Live availability for the chosen time ---------- */
    let availabilityTimer;
    let availabilityRequest;
    const showAvailability = (state, text) => {
        availabilityHint.dataset.state = state;
        availabilityHint.textContent = text;
        availabilityHint.hidden = false;
    };
    const checkAvailability = () => {
        clearTimeout(availabilityTimer);
        availabilityRequest?.abort();
        if (!current || current.dataset.requestable !== 'true' || !start.value || !end.value) {
            availabilityHint.hidden = true;
            return;
        }
        availabilityTimer = setTimeout(async () => {
            availabilityRequest = new AbortController();
            showAvailability('checking', 'Checking availability for this time…');
            const query = new URLSearchParams({ equipmentItemId: current.dataset.id, start: start.value, end: end.value });
            try {
                const response = await fetch(`/borrower/reservation/availability?${query}`, {
                    headers: { Accept: 'application/json' }, signal: availabilityRequest.signal
                });
                const result = await response.json();
                if (!response.ok) {
                    freeUnits = null;
                    showAvailability('warn', result.message || 'Check the selected dates.');
                } else {
                    freeUnits = result.freeUnits;
                    showAvailability(result.freeUnits > 0 ? 'ok' : 'warn', result.freeUnits > 0
                        ? `${result.freeUnits} of ${result.bookableUnits} ${result.bookableUnits === 1 ? 'unit is' : 'units are'} free for this time.`
                        : 'No units are free for this time. Choose another borrowing or return time.');
                    if (Number(quantity.value) > maxQuantity()) quantity.value = String(maxQuantity());
                }
                updateQuantityLimits();
                submit.disabled = !eligible || current.dataset.requestable !== 'true' || freeUnits === 0;
            } catch (error) {
                // Offline or aborted: hide the hint; the server still validates the request.
                if (error.name !== 'AbortError') availabilityHint.hidden = true;
            }
        }, 250);
    };
    start.addEventListener('change', () => {
        end.min = start.value || start.min;
        // Keep the return time after the borrowing time by moving it forward with the same length.
        if (start.value && (!end.value || end.value <= start.value)) {
            const next = new Date(`${start.value}:00`);
            next.setHours(next.getHours() + 2);
            const pad = n => String(n).padStart(2, '0');
            end.value = `${next.getFullYear()}-${pad(next.getMonth() + 1)}-${pad(next.getDate())}T${pad(next.getHours())}:${pad(next.getMinutes())}`;
        }
        setError('StartLocal', '');
        setError('EndLocal', '');
        checkAvailability();
    });
    end.addEventListener('change', () => { setError('EndLocal', ''); checkAvailability(); });
    purpose.addEventListener('input', () => setError('Purpose', ''));

    /* ---------- Opening and closing ---------- */
    let opener = null;
    const open = (card, trigger) => {
        fill(card);
        opener = trigger || null;
        dialog.classList.remove('is-closing');
        if (dialog.open && !dialog.matches(':modal')) dialog.close();
        if (!dialog.open) dialog.showModal();
        dialog.querySelector('.reserve-scroll').scrollTop = 0;
        checkAvailability();
    };
    const close = () => {
        if (!dialog.open || dialog.classList.contains('is-closing')) return;
        availabilityRequest?.abort();
        const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;
        if (reduced) { dialog.close(); return; }
        dialog.classList.add('is-closing');
        dialog.addEventListener('animationend', () => {
            dialog.classList.remove('is-closing');
            dialog.close();
        }, { once: true });
    };
    dialog.addEventListener('close', () => {
        if (opener?.isConnected) opener.focus();
        opener = null;
    });
    dialog.addEventListener('cancel', event => { event.preventDefault(); if (!submitting) close(); });
    dialog.querySelectorAll('[data-close-dialog]').forEach(link => link.addEventListener('click', event => {
        event.preventDefault();
        if (!submitting) close();
    }));
    // A press that starts and ends on the backdrop closes the dialog; drags out of a text field do not.
    let pressedBackdrop = false;
    dialog.addEventListener('pointerdown', event => { pressedBackdrop = event.target === dialog; });
    dialog.addEventListener('click', event => {
        if (pressedBackdrop && event.target === dialog && !submitting) close();
        pressedBackdrop = false;
    });
    cards.forEach(card => card.querySelector('[data-open-card]').addEventListener('click', event => {
        if (event.metaKey || event.ctrlKey || event.shiftKey || event.button !== 0) return;
        event.preventDefault();
        open(card, event.currentTarget);
    }));

    /* ---------- Submit ---------- */
    const setError = (name, message) => {
        const target = dialog.querySelector(`[data-error-for="${name}"]`);
        if (target) target.textContent = message;
        const input = form.elements[name];
        if (input) input.setAttribute('aria-invalid', message ? 'true' : 'false');
    };
    const validate = () => {
        const errors = [];
        const max = maxQuantity();
        const value = Number(quantity.value);
        if (!Number.isInteger(value) || value < 1 || value > max)
            errors.push(['Quantity', max === 1 ? 'Only 1 unit can be requested.' : `Request between 1 and ${max} units.`]);
        if (!start.value) errors.push(['StartLocal', 'Choose a borrowing date and time.']);
        else if (start.min && start.value < start.min) errors.push(['StartLocal', 'Choose a future borrowing time.']);
        if (!end.value) errors.push(['EndLocal', 'Choose an expected return date and time.']);
        else if (start.value && end.value <= start.value) errors.push(['EndLocal', 'The return time must be after the borrowing time.']);
        if (!purpose.value.trim()) errors.push(['Purpose', 'Enter the purpose of your request.']);
        ['Quantity', 'StartLocal', 'EndLocal', 'Purpose'].forEach(name => setError(name, ''));
        errors.forEach(([name, message]) => setError(name, message));
        return errors;
    };
    form.addEventListener('submit', event => {
        if (submitting) { event.preventDefault(); return; }
        const errors = validate();
        if (errors.length > 0 || submit.disabled) {
            event.preventDefault();
            const first = form.elements[errors[0]?.[0]];
            first?.focus();
            first?.classList.remove('motion-shake');
            void first?.offsetWidth;
            first?.classList.add('motion-shake');
            return;
        }
        submitting = true;
        // Disable after the submit event so the button's own value is not lost; the request ID also makes retries safe.
        requestAnimationFrame(() => {
            submit.disabled = true;
            submit.textContent = 'Submitting…';
        });
    });
    addEventListener('pageshow', event => {
        if (!event.persisted) return;
        submitting = false;
        submit.textContent = 'Submit Request';
        if (current) fill(current);
    });

    /* ---------- Initial state ---------- */
    applyFilters();
    if (current) {
        // Keep values posted back by the server, but refresh limits and availability.
        const keep = quantity.value;
        fill(current);
        quantity.value = keep;
        updateQuantityLimits();
    }
    if (dialog.dataset.openOnLoad === 'true' && current) {
        dialog.close();
        dialog.showModal();
        checkAvailability();
        const firstError = dialog.querySelector('[data-error-for]:not(:empty)');
        if (firstError) form.elements[firstError.dataset.errorFor]?.focus();
    } else if (dialog.open) {
        dialog.close();
    }
})();
