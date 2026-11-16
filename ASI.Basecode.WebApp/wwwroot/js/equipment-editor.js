// Add/Edit equipment dialog (Views/AdminInventory/_EquipmentEditor.cshtml): the live catalog preview, the description
// counter, and the photo picker.
// Photo: drop, click, choose or paste; it is previewed in the catalog's 4:3 crop. A photo the server would refuse
// (over 5 MB, larger than needed, or HEIC/AVIF/BMP that the browser can still open) is resized and saved as JPEG here,
// then put back into the file input, so the normal form post uploads the smaller file. The server still checks it.
// The dialog is loaded on demand, so everything is delegated from the document.
(() => {
    const MAX_EDGE = 2000;          // longest side kept; catalog cards and the dialog never show more
    const LOW_EDGE = 600;           // below this a photo looks soft on large screens
    const ACCEPTED = ['image/jpeg', 'image/png', 'image/webp', 'image/gif'];
    const sizeText = bytes => (bytes >= 1048576 ? `${(bytes / 1048576).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`);

    const parts = root => {
        if (!root._parts) {
            const q = name => root.querySelector(`[data-photo-${name}]`);
            root._parts = {
                frame: q('frame'), img: q('img'), empty: q('empty'), emptyTitle: q('empty-title'), emptyText: q('empty-text'),
                busy: q('busy'), name: q('name'), meta: q('meta'), choose: q('choose'), chooseLabel: q('choose-label'),
                remove: q('remove'), undo: q('undo'), error: q('error'), input: q('input'), flag: q('remove-flag')
            };
            root._photo = {
                hadPhoto: root.dataset.hasPhoto === 'true',
                currentSrc: root._parts.img.getAttribute('src') || '',
                file: null, info: '', removed: false, blob: null
            };
        }
        return root._parts;
    };

    const render = root => {
        const p = parts(root), s = root._photo;
        if (s.blob && !s.file) { URL.revokeObjectURL(s.blob); s.blob = null; }
        const showing = s.file ? 'new' : s.removed ? 'removed' : s.hadPhoto ? 'current' : 'none';
        const src = showing === 'new' ? s.blob : showing === 'current' ? s.currentSrc : '';
        if (src) p.img.src = src; else p.img.removeAttribute('src');
        p.img.hidden = !src;
        p.empty.hidden = !!src;
        p.frame.classList.toggle('has-photo', !!src);
        p.frame.classList.toggle('is-removed', showing === 'removed');
        p.flag.value = showing === 'removed' ? 'true' : 'false';
        p.chooseLabel.textContent = src ? 'Replace' : 'Upload photo';
        p.remove.hidden = !src;
        p.undo.hidden = showing !== 'removed';
        p.emptyTitle.textContent = showing === 'removed' ? 'Photo will be removed' : 'Add a photo';
        p.emptyText.textContent = showing === 'removed'
            ? 'It goes when you save. Choose Undo to keep it.'
            : 'Drop it here, click to browse, or paste.';
        p.name.textContent = { new: s.file?.name || 'New photo', current: 'Current photo', removed: 'No photo after saving', none: 'No photo yet' }[showing];
        p.meta.textContent = {
            new: `${s.info} Not saved yet.`,
            current: 'Borrowers see it cropped like this.',
            removed: 'Borrowers will see an icon for this type of equipment.',
            none: 'Without one, borrowers see an icon for this type of equipment.'
        }[showing];
    };

    const setError = (root, message) => {
        const p = parts(root);
        p.error.textContent = message || '';
        p.error.hidden = !message;
        p.frame.classList.toggle('is-invalid', !!message);
        if (message) p.input.setAttribute('aria-invalid', 'true'); else p.input.removeAttribute('aria-invalid');
    };

    const busy = (root, on) => {
        const p = parts(root);
        p.busy.hidden = !on;
        p.choose.disabled = on;
        root.classList.toggle('is-busy', on);
    };

    // Shrink an already-decoded photo to MAX_EDGE and encode it as JPEG, lowering quality until it fits.
    const shrink = async (bitmap, file, maxBytes) => {
        const scale = Math.min(1, MAX_EDGE / Math.max(bitmap.width, bitmap.height));
        let width = Math.round(bitmap.width * scale), height = Math.round(bitmap.height * scale);
        for (const quality of [0.86, 0.78, 0.7, 0.62]) {
            const canvas = document.createElement('canvas');
            canvas.width = width; canvas.height = height;
            const context = canvas.getContext('2d');
            context.fillStyle = '#fff';             // JPEG has no transparency: cut-out product shots get a white backdrop
            context.fillRect(0, 0, width, height);
            context.imageSmoothingQuality = 'high';
            context.drawImage(bitmap, 0, 0, width, height);
            const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', quality));
            if (blob && blob.size <= maxBytes) {
                const base = (file.name || 'photo').replace(/\.[^.]+$/, '') || 'photo';
                return { file: new File([blob], `${base}.jpg`, { type: 'image/jpeg', lastModified: Date.now() }), width, height };
            }
            width = Math.round(width * 0.85); height = Math.round(height * 0.85);
        }
        throw new Error('too-large');
    };

    // Decoded once (respecting the camera's rotation); null when the browser can't open the format.
    const decode = async file => {
        try { return await createImageBitmap(file); } catch { return null; }
    };

    const useFile = async (root, file) => {
        const p = parts(root), s = root._photo;
        const maxBytes = Number(root.dataset.maxBytes) || 5242880;
        setError(root, '');
        if (!file) return;
        if (file.type && !file.type.startsWith('image/')) {
            setError(root, 'That file isn’t an image. Choose a JPEG, PNG, WebP or GIF photo.');
            return;
        }
        busy(root, true);
        let bitmap = null;
        try {
            bitmap = await decode(file);
            // Every format the server accepts opens in the browser, so a file that can't be opened would fail on save too.
            if (!bitmap) throw new Error('unreadable');
            const size = { width: bitmap.width, height: bitmap.height };
            const accepted = ACCEPTED.includes(file.type);
            // Animated GIFs are kept as they are whenever the server accepts them; anything else is shrunk if it's oversized.
            const oversized = file.size > maxBytes || (file.type !== 'image/gif' && Math.max(size.width, size.height) > MAX_EDGE);
            let chosen = file, width = size.width, height = size.height, note = '';
            if (!accepted || oversized) {
                const result = await shrink(bitmap, file, maxBytes);
                chosen = result.file; width = result.width; height = result.height;
                note = ` Resized from ${sizeText(file.size)}${accepted ? '' : ' and converted to JPEG'}.`;
            }
            // Put the (possibly smaller) file back into the input so the form uploads it.
            const transfer = new DataTransfer();
            transfer.items.add(chosen);
            p.input.files = transfer.files;
            if (s.blob) URL.revokeObjectURL(s.blob);
            s.file = chosen;
            s.blob = URL.createObjectURL(chosen);
            s.removed = false;
            const low = width && Math.max(width, height) < LOW_EDGE ? ' Low resolution: it may look blurry in the catalog.' : '';
            s.info = `${width && height ? `${width} × ${height} · ` : ''}${sizeText(chosen.size)}.${note}${low}`;
            render(root);
        } catch (error) {
            p.input.value = '';
            setError(root, error.message === 'too-large'
                ? 'This photo is still over 5 MB after resizing. Try a smaller photo.'
                : 'This photo couldn’t be opened in your browser. Save it as a JPEG or PNG and try again.');
        } finally {
            bitmap?.close?.();
            busy(root, false);
        }
    };

    const rootOf = target => target?.closest?.('[data-photo]');

    document.addEventListener('click', event => {
        const root = rootOf(event.target);
        if (!root || root.classList.contains('is-busy')) return;
        const p = parts(root), s = root._photo;
        if (event.target.closest('[data-photo-choose]') || event.target.closest('[data-photo-frame]')) {
            p.input.click();
        } else if (event.target.closest('[data-photo-remove]')) {
            setError(root, '');
            if (s.file) { s.file = null; p.input.value = ''; }      // drop the new photo; back to the saved one, if any
            else if (s.hadPhoto) s.removed = true;
            render(root);
            p.choose.focus();
        } else if (event.target.closest('[data-photo-undo]')) {
            s.removed = false;
            render(root);
            p.remove.focus();
        }
    });

    document.addEventListener('change', event => {
        const input = event.target.closest?.('[data-photo-input]');
        if (!input || !input.files?.[0]) return;
        const root = rootOf(input);
        // Ignore the change caused by putting the shrunk file back.
        if (root._photo?.file === input.files[0]) return;
        useFile(root, input.files[0]);
    });

    // Drag and drop onto the frame (or anywhere in the photo section).
    const hasFiles = event => [...(event.dataTransfer?.types || [])].includes('Files');
    ['dragenter', 'dragover'].forEach(type => document.addEventListener(type, event => {
        const root = rootOf(event.target);
        if (!root || !hasFiles(event)) return;
        event.preventDefault();
        event.dataTransfer.dropEffect = 'copy';
        parts(root).frame.classList.add('is-dragging');
    }));
    document.addEventListener('dragleave', event => {
        const root = rootOf(event.target);
        if (root && !root.contains(event.relatedTarget)) parts(root).frame.classList.remove('is-dragging');
    });
    document.addEventListener('drop', event => {
        const root = rootOf(event.target);
        if (!root || !hasFiles(event)) return;
        event.preventDefault();
        parts(root).frame.classList.remove('is-dragging');
        if (!root.classList.contains('is-busy')) useFile(root, event.dataTransfer.files[0]);
    });

    // Paste an image (e.g. copied from another app) while the dialog is open. Pasting text into a field still works.
    document.addEventListener('paste', event => {
        const root = document.querySelector('dialog[open] [data-photo]');
        const image = [...(event.clipboardData?.files || [])].find(file => file.type.startsWith('image/'));
        if (!root || !image || root.classList.contains('is-busy')) return;
        const typing = event.target.closest?.('input:not([type="file"]), textarea, [contenteditable]');
        if (typing && event.clipboardData.getData('text/plain')) return;
        event.preventDefault();
        useFile(root, image);
    });

    /* ---------- Live catalog preview and description counter ---------- */
    // Fields marked data-preview-source="x" fill [data-preview="x"] on the preview card as the admin types.
    const previewValue = field => {
        if (field.type === 'checkbox') return field.checked ? 'Active' : 'Inactive';
        if (field.tagName === 'SELECT') return field.selectedOptions[0]?.textContent.trim() || '';
        return field.value.trim();
    };
    const syncField = field => {
        const form = field.form;
        const key = field.dataset.previewSource;
        if (form && key) {
            form.querySelectorAll(`[data-preview="${key}"]`).forEach(target => {
                const value = previewValue(field);
                target.textContent = value || target.dataset.empty || '';
                target.classList.toggle('is-placeholder', !value);
                if (key === 'active') target.classList.toggle('is-inactive', !field.checked);
            });
        }
        if (field.id) {
            const counter = form?.querySelector(`[data-counter-for="${field.id}"]`);
            if (counter) {
                const max = Number(field.maxLength) || 0;
                counter.textContent = `${field.value.length} / ${max}`;
                counter.classList.toggle('is-near', max > 0 && field.value.length >= max * 0.9);
            }
        }
    };
    const syncForm = form => form.querySelectorAll('[data-preview-source], [id]').forEach(field => {
        if (field.matches('input, select, textarea')) syncField(field);
    });
    ['input', 'change'].forEach(type => document.addEventListener(type, event => {
        const field = event.target;
        if (field.closest?.('form[data-equipment-editor]') && field.matches('input, select, textarea')) syncField(field);
    }));

    // form.reset() (the dialog closing) clears the file input; put the photo section and preview back to how it opened.
    document.addEventListener('reset', event => {
        const form = event.target;
        if (form.matches?.('form[data-equipment-editor]')) setTimeout(() => syncForm(form));
        const root = form.querySelector?.('[data-photo]');
        if (!root?._photo) return;
        Object.assign(root._photo, { file: null, removed: false, info: '' });
        setError(root, '');
        render(root);
    });
})();
