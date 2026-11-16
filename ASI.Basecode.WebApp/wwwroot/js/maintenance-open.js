// "Open maintenance" dialog (Views/Maintenance/Index.cshtml): search and filter the equipment list, keep the footer
// summary in sync, add issue starters, and check both steps before modal-forms.js posts the form.
// The equipment list is a [data-refresh-region] that is replaced after each save, so everything is delegated.
(() => {
    const formOf = target => target?.closest?.('form[data-maint-open]');

    const applyFilter = form => {
        const term = (form.querySelector('[data-maint-search]')?.value || '').trim().toLowerCase();
        const mode = form.querySelector('[data-maint-filter][aria-pressed="true"]')?.dataset.maintFilter || '';
        let shown = 0;
        form.querySelectorAll('[data-maint-option]').forEach(option => {
            const visible = (!term || option.dataset.search.includes(term)) && (mode !== 'attention' || option.dataset.attention === 'true');
            option.hidden = !visible;
            if (visible) shown++;
        });
        // A group heading shows only when something is listed under it.
        form.querySelectorAll('[data-maint-group]').forEach(heading => {
            let next = heading.nextElementSibling, any = false;
            while (next && !next.matches('[data-maint-group]')) { if (!next.hidden) { any = true; break; } next = next.nextElementSibling; }
            heading.hidden = !any;
        });
        const empty = form.querySelector('[data-maint-empty]');
        if (empty) empty.hidden = shown > 0;
    };

    const setError = (form, name, show) => {
        const error = form.querySelector(`[data-maint-error="${name}"]`);
        if (error) error.hidden = !show;
        if (name === 'equipment') form.querySelector('[data-maint-list]')?.classList.toggle('is-invalid', show);
        if (name === 'description') {
            const field = form.elements.description;
            if (show) field.setAttribute('aria-invalid', 'true'); else field.removeAttribute('aria-invalid');
        }
    };

    const updateSummary = form => {
        const checked = form.querySelector('input[name="equipmentItemId"]:checked');
        const summary = form.querySelector('[data-maint-summary]');
        if (!summary) return;
        const label = checked?.closest('[data-maint-option]')?.dataset.label;
        summary.textContent = label ? `Selected: ${label}` : 'No equipment selected';
        summary.classList.toggle('is-set', !!label);
    };

    document.addEventListener('input', event => {
        const form = formOf(event.target);
        if (!form) return;
        if (event.target.matches('[data-maint-search]')) applyFilter(form);
        if (event.target.name === 'description' && event.target.value.trim()) setError(form, 'description', false);
    });

    // After a pick, bring step 2 into view. Only for a click/tap or Enter: arrow keys also change the radio while
    // the admin is still browsing the list, and jumping away then would get in the way. Touch scrolls without
    // focusing, so the on-screen keyboard doesn't cover the list.
    let pickedBy = null;
    let pickedAt = 0;
    const goToIssue = (form, focus) => {
        const step = form.elements.description.closest('.maint-step');
        const behavior = matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth';
        requestAnimationFrame(() => {
            step.scrollIntoView({ block: 'nearest', behavior });
            if (focus) form.elements.description.focus({ preventScroll: true });
        });
    };
    document.addEventListener('pointerdown', event => {
        if (!event.target.closest?.('[data-maint-option]:not(.is-blocked)')) return;
        pickedBy = event.pointerType || 'mouse';
        pickedAt = performance.now();
    });

    document.addEventListener('change', event => {
        const form = formOf(event.target);
        if (!form || event.target.name !== 'equipmentItemId') return;
        setError(form, 'equipment', false);
        updateSummary(form);
        // A pointer press counts only if this change follows it (pressing the already-picked card fires no change).
        const fresh = pickedBy === 'keyboard' || (pickedBy && performance.now() - pickedAt < 1000);
        if (fresh) goToIssue(form, pickedBy !== 'touch' && !form.elements.description.value.trim());
        pickedBy = null;
    });

    document.addEventListener('keydown', event => {
        const form = formOf(event.target);
        // Enter in the search box picks the only match instead of submitting the form.
        if (!form || !event.target.matches('[data-maint-search]') || event.key !== 'Enter') return;
        event.preventDefault();
        const enabled = [...form.querySelectorAll('[data-maint-option]:not([hidden]) input:not(:disabled)')];
        if (enabled.length === 1) {
            // A single match is picked and the admin moves straight on to describing the issue.
            pickedBy = 'keyboard';
            enabled[0].checked = true;
            enabled[0].dispatchEvent(new Event('change', { bubbles: true }));
            return;
        }
        enabled[0]?.focus();
    });

    document.addEventListener('click', event => {
        const form = formOf(event.target);
        if (!form) return;
        const filter = event.target.closest('[data-maint-filter]');
        if (filter) {
            form.querySelectorAll('[data-maint-filter]').forEach(chip => chip.setAttribute('aria-pressed', String(chip === filter)));
            applyFilter(form);
            return;
        }
        const starter = event.target.closest('[data-maint-starter]');
        if (starter) {
            // Start (or extend) the description with a common issue, then let the admin keep typing.
            const field = form.elements.description;
            const text = field.value.trim();
            field.value = text ? `${text}${/[.!?]$/.test(text) ? '' : '.'} ${starter.dataset.maintStarter}: ` : `${starter.dataset.maintStarter}: `;
            field.focus();
            field.setSelectionRange(field.value.length, field.value.length);
            setError(form, 'description', false);
        }
    });

    // Runs before modal-forms.js (capture phase): point at the step that is missing instead of a generic browser message.
    document.addEventListener('submit', event => {
        const form = formOf(event.target);
        if (!form) return;
        const chosen = form.querySelector('input[name="equipmentItemId"]:checked');
        const described = form.elements.description.value.trim().length > 0;
        setError(form, 'equipment', !chosen);
        setError(form, 'description', !described);
        if (chosen && described) return;
        event.preventDefault();
        event.stopImmediatePropagation();
        if (!chosen) {
            const first = form.querySelector('[data-maint-option]:not([hidden]) input:not(:disabled)');
            (first || form.querySelector('[data-maint-search]'))?.focus();
            form.querySelector('[data-maint-list]')?.scrollIntoView({ block: 'nearest' });
        } else {
            form.elements.description.focus();
        }
    }, true);

    // The dialog closing resets the form; show the whole list again and clear the messages.
    document.addEventListener('reset', event => {
        const form = formOf(event.target);
        if (!form) return;
        setTimeout(() => {
            form.querySelectorAll('[data-maint-filter]').forEach(chip => chip.setAttribute('aria-pressed', String(chip.dataset.maintFilter === '')));
            applyFilter(form);
            setError(form, 'equipment', false);
            setError(form, 'description', false);
            updateSummary(form);
            form.querySelector('[data-maint-list]')?.scrollTo(0, 0);
        });
    });
})();
