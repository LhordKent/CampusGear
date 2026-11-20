// In-site confirmation for destructive forms, replacing the browser's confirm() box.
// <form data-confirm="Message" data-confirm-title="Title" data-confirm-ok="Delete" data-confirm-cancel="Keep">
// or, to ask only when a switch is turned off: <input type="checkbox" data-confirm-on-uncheck="Message">
// Submitting the form opens the dialog; confirming submits it for real (so data-modal-form forms still post with
// fetch). Cancel, Escape, or a click on the backdrop leaves everything as it was. Without JavaScript the form submits
// directly, as it always could.
// Scripts can ask the same question: `await CampusGear.confirm({ title, message, ok, cancel })` resolves true or false.
(() => {
    let dialog, title, message, ok, cancel, pending;

    const settle = answer => {
        const current = pending;
        pending = null;
        current?.resolve(answer);
        // Cancelled: put focus back where the question came from. Confirmed: the caller decides what happens next.
        if (!answer && current?.returnFocus?.isConnected) current.returnFocus.focus();
    };

    const build = () => {
        dialog = document.createElement('dialog');
        dialog.className = 'live-modal confirm-dialog';
        dialog.setAttribute('role', 'alertdialog');
        dialog.setAttribute('aria-labelledby', 'confirm-dialog-title');
        dialog.setAttribute('aria-describedby', 'confirm-dialog-message');
        dialog.innerHTML = `
            <div class="confirm-dialog-body">
                <span class="confirm-dialog-icon" aria-hidden="true">!</span>
                <div>
                    <h2 id="confirm-dialog-title"></h2>
                    <p id="confirm-dialog-message"></p>
                </div>
            </div>
            <div class="live-modal-actions">
                <button class="live-button secondary" type="button" data-confirm-cancel></button>
                <button class="live-button danger" type="button" data-confirm-ok></button>
            </div>`;
        title = dialog.querySelector('h2');
        message = dialog.querySelector('p');
        ok = dialog.querySelector('[data-confirm-ok]');
        cancel = dialog.querySelector('[data-confirm-cancel]');

        cancel.addEventListener('click', () => dialog.close());
        ok.addEventListener('click', () => {
            const current = pending;
            pending = null;
            dialog.close();
            current?.resolve(true);
        });
        // A click on the dimmed backdrop (outside the dialog box) cancels.
        dialog.addEventListener('click', event => {
            if (event.target !== dialog) return;
            const box = dialog.getBoundingClientRect();
            const inside = event.clientX >= box.left && event.clientX <= box.right && event.clientY >= box.top && event.clientY <= box.bottom;
            if (!inside) dialog.close();
        });
        // Closing any other way (Cancel, Escape, backdrop) is a "no".
        dialog.addEventListener('close', () => { if (pending) settle(false); });
        document.body.append(dialog);
    };

    const ask = ({ title: heading, message: text, ok: okLabel, cancel: cancelLabel, returnFocus } = {}) => {
        if (!dialog) build();
        if (pending) settle(false);
        title.textContent = heading || 'Are you sure?';
        message.textContent = text || '';
        ok.textContent = okLabel || 'Confirm';
        cancel.textContent = cancelLabel || 'Cancel';
        return new Promise(resolve => {
            pending = { resolve, returnFocus: returnFocus ?? document.activeElement };
            dialog.showModal();
            // The safe choice has focus, so Enter never confirms by accident.
            cancel.focus();
        });
    };

    window.CampusGear = window.CampusGear || {};
    window.CampusGear.confirm = ask;

    // A checkbox with data-confirm-on-uncheck="Message" asks only when it is submitted switched off (the first such
    // message wins); the form's data-confirm-title/-ok/-cancel still label the dialog.
    const questionFor = form => {
        const switched = [...form.querySelectorAll('[data-confirm-on-uncheck]')].find(x => !x.checked && !x.disabled);
        return switched?.dataset.confirmOnUncheck || form.dataset.confirm;
    };

    // Capture phase, so the question comes before any other submit handler (such as modal-forms.js).
    document.addEventListener('submit', async event => {
        const form = event.target.closest('form');
        const question = form && questionFor(form);
        if (!question) return;
        if (form._confirmed) { form._confirmed = false; return; }
        event.preventDefault();
        event.stopImmediatePropagation();
        const submitter = event.submitter;
        const confirmed = await ask({
            title: form.dataset.confirmTitle,
            message: question,
            ok: form.dataset.confirmOk,
            cancel: form.dataset.confirmCancel,
            returnFocus: submitter,
        });
        if (!confirmed || !form.isConnected) return;
        form._confirmed = true;
        if (form.requestSubmit) form.requestSubmit(submitter?.form === form ? submitter : undefined);
        else form.submit();
    }, true);
})();
