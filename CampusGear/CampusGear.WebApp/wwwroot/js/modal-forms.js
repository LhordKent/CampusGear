// Modal dialogs whose forms save without leaving the page.
// [data-modal-open="id"] opens <dialog id>; [data-modal-close] or Escape closes it. A form marked data-modal-form posts
// with fetch and expects JSON { ok, message, field }: on success the dialog closes, a toast confirms, and every
// [data-refresh-region] on the page is reloaded in place; on failure the dialog stays open with the message shown
// above the fields, the named field marked, and everything the user typed intact.
(() => {
    const parser = new DOMParser();
    const toast = (message, type) => window.CampusGear?.toast?.(message, type);

    const resetForm = dialog => {
        dialog.querySelectorAll('form[data-modal-form]').forEach(form => {
            form.reset();
            const alert = form.querySelector('[data-modal-alert]');
            if (alert) { alert.hidden = true; alert.textContent = ''; }
            form.querySelectorAll('[aria-invalid]').forEach(field => field.removeAttribute('aria-invalid'));
        });
    };

    document.addEventListener('click', event => {
        const opener = event.target.closest('[data-modal-open]');
        if (opener) {
            const dialog = document.getElementById(opener.dataset.modalOpen);
            if (!dialog) return;
            dialog._opener = opener;
            dialog.showModal();
            dialog.querySelector('select, textarea, input:not([type="hidden"])')?.focus();
            return;
        }
        const closer = event.target.closest('[data-modal-close]');
        if (closer) { closer.closest('dialog')?.close(); return; }
        // A click on the dimmed backdrop (the dialog element itself, outside its content box) closes it.
        if (event.target instanceof HTMLDialogElement && event.target.open) {
            const box = event.target.getBoundingClientRect();
            const inside = event.clientX >= box.left && event.clientX <= box.right && event.clientY >= box.top && event.clientY <= box.bottom;
            if (!inside) event.target.close();
        }
    });

    // Closing discards unsaved input, so the next open starts clean, and returns focus to the button that opened it.
    document.addEventListener('close', event => {
        const dialog = event.target;
        if (!(dialog instanceof HTMLDialogElement)) return;
        resetForm(dialog);
        if (dialog._opener?.isConnected) dialog._opener.focus();
    }, true);

    async function refreshRegions() {
        const response = await fetch(location.href, { headers: { 'X-Requested-With': 'fetch', Accept: 'text/html' }, credentials: 'same-origin' });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const doc = parser.parseFromString(await response.text(), 'text/html');
        document.querySelectorAll('[data-refresh-region][id]').forEach(region => {
            const next = doc.getElementById(region.id);
            if (!next) return;
            region.replaceChildren(...next.childNodes);
            document.dispatchEvent(new CustomEvent('campusgear:content', { detail: { root: region } }));
        });
    }

    const showError = (form, message, field) => {
        const alert = form.querySelector('[data-modal-alert]');
        if (alert) { alert.textContent = message; alert.hidden = false; }
        form.querySelectorAll('[aria-invalid]').forEach(x => x.removeAttribute('aria-invalid'));
        const target = field ? form.querySelector(`[name="${CSS.escape(field)}"]`) : null;
        // The message shows inside the open dialog only, so one failed save never produces two notices.
        if (target) { target.setAttribute('aria-invalid', 'true'); target.focus(); }
    };

    document.addEventListener('submit', async event => {
        const form = event.target.closest('form[data-modal-form]');
        if (!form) return;
        event.preventDefault();
        // The browser's own checks run first, so an empty required field is pointed out without a round trip.
        if (!form.checkValidity()) {
            const invalid = form.querySelector(':invalid');
            showError(form, invalid?.validationMessage ? `${invalid.closest('label')?.querySelector('span')?.firstChild?.textContent.trim() ?? 'A field'}: ${invalid.validationMessage}` : 'Check the highlighted field.', invalid?.name);
            return;
        }
        const submit = form.querySelector('[type="submit"]');
        if (submit.disabled) return;
        submit.disabled = true;
        submit.classList.add('motion-busy');
        try {
            const response = await fetch(form.action, {
                method: 'POST', body: new FormData(form), credentials: 'same-origin',
                headers: { 'X-Requested-With': 'fetch', Accept: 'application/json' }
            });
            if (response.status === 401) { location.reload(); return; }
            const result = (response.headers.get('content-type') ?? '').includes('json') ? await response.json() : null;
            if (!result) throw new Error(`HTTP ${response.status}`);
            if (!result.ok) { showError(form, result.message, result.field); return; }
            form.closest('dialog')?.close();
            toast(result.message, 'success');
            try { await refreshRegions(); } catch { location.reload(); }
        } catch {
            showError(form, 'The change could not be saved. Check your connection and try again; nothing was lost.', null);
        } finally {
            submit.disabled = false;
            submit.classList.remove('motion-busy');
        }
    });

    // Repair notes are required only when closing a case; say so next to the field as the status changes.
    document.addEventListener('change', event => {
        const status = event.target.closest('[data-maintenance-status]');
        if (!status) return;
        const form = status.form;
        const notes = form.querySelector('textarea[name="Resolution"]');
        const closing = status.value === 'Closed';
        notes.required = closing;
        form.querySelector('[data-resolution-hint]')?.classList.toggle('is-required', closing);
    });
})();
