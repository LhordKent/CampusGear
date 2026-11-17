// In-site confirmation for destructive forms, replacing the browser's confirm() box.
// <form data-confirm="Message" data-confirm-title="Title" data-confirm-ok="Delete" data-confirm-cancel="Keep">
// Submitting the form opens the dialog; confirming submits it for real (so data-modal-form forms still post with
// fetch). Cancel, Escape, or a click on the backdrop leaves everything as it was. Without JavaScript the form submits
// directly, as it always could.
(() => {
    let dialog, title, message, ok, cancel, pending;

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
            const { form, submitter } = pending ?? {};
            pending = null;
            dialog.close();
            if (!form?.isConnected) return;
            form._confirmed = true;
            if (form.requestSubmit) form.requestSubmit(submitter?.form === form ? submitter : undefined);
            else form.submit();
        });
        // A click on the dimmed backdrop (outside the dialog box) cancels.
        dialog.addEventListener('click', event => {
            if (event.target !== dialog) return;
            const box = dialog.getBoundingClientRect();
            const inside = event.clientX >= box.left && event.clientX <= box.right && event.clientY >= box.top && event.clientY <= box.bottom;
            if (!inside) dialog.close();
        });
        dialog.addEventListener('close', () => {
            // Cancelled (not confirmed): put focus back on the button that asked.
            if (pending?.submitter?.isConnected) pending.submitter.focus();
            pending = null;
        });
        document.body.append(dialog);
    };

    // Capture phase, so the question comes before any other submit handler (such as modal-forms.js).
    document.addEventListener('submit', event => {
        const form = event.target.closest('form[data-confirm]');
        if (!form) return;
        if (form._confirmed) { form._confirmed = false; return; }
        event.preventDefault();
        event.stopImmediatePropagation();
        if (!dialog) build();
        pending = { form, submitter: event.submitter };
        title.textContent = form.dataset.confirmTitle || 'Are you sure?';
        message.textContent = form.dataset.confirm;
        ok.textContent = form.dataset.confirmOk || 'Confirm';
        cancel.textContent = form.dataset.confirmCancel || 'Cancel';
        dialog.showModal();
        // The safe choice has focus, so Enter never confirms by accident.
        cancel.focus();
    }, true);
})();
