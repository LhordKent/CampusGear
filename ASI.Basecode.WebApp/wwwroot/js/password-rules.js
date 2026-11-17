// Live password checklist for <ul data-password-rules="PasswordId" data-password-confirm="ConfirmId">.
// Each <li data-rule> ticks as the user types. The checks mirror ASP.NET Identity's: ASCII A–Z, a–z, 0–9, and any
// other character counts as a symbol. A rule left unmet shows as missing once the user leaves the field, not while
// they are still typing. Listeners are on the document, so checklists inside dialogs loaded later work too.
(() => {
    const checks = {
        length: (value, min) => value.length >= min,
        upper: value => /[A-Z]/.test(value),
        lower: value => /[a-z]/.test(value),
        digit: value => /[0-9]/.test(value),
        symbol: value => /[^A-Za-z0-9]/.test(value),
    };

    const update = (list, { touched = false } = {}) => {
        const form = list.closest('form') ?? document;
        const password = form.querySelector(`#${CSS.escape(list.dataset.passwordRules)}`);
        if (!password) return;
        const confirm = list.dataset.passwordConfirm ? form.querySelector(`#${CSS.escape(list.dataset.passwordConfirm)}`) : null;
        const value = password.value;
        const min = Number(list.dataset.minLength) || 8;
        if (touched) list.dataset.touched = '';

        let allMet = true;
        list.querySelectorAll('li[data-rule]').forEach(item => {
            const rule = item.dataset.rule;
            const met = rule === 'match'
                ? value !== '' && confirm?.value === value
                : checks[rule]?.(value, min) ?? true;
            // "match" only shows as missing once the confirm field has something in it.
            const missing = !met && list.hasAttribute('data-touched') && (rule !== 'match' || confirm?.value);
            if (rule !== 'match') allMet &&= met;
            item.classList.toggle('is-met', met);
            item.classList.toggle('is-missing', Boolean(missing));
            const state = item.querySelector('[data-rule-state]');
            if (state) state.textContent = met ? ' — done' : ' — not yet';
        });
        list.classList.toggle('is-complete', allMet);
    };

    const listsFor = field => {
        const form = field.closest('form');
        if (!form || !field.id) return [];
        return [...form.querySelectorAll('ul[data-password-rules]')].filter(list =>
            list.dataset.passwordRules === field.id || list.dataset.passwordConfirm === field.id);
    };

    document.addEventListener('input', event => {
        if (!(event.target instanceof HTMLInputElement)) return;
        listsFor(event.target).forEach(list => update(list));
    });
    document.addEventListener('focusout', event => {
        if (!(event.target instanceof HTMLInputElement) || event.target.value === '') return;
        listsFor(event.target).forEach(list => update(list, { touched: true }));
    });

    // A form re-rendered by the server after an error may already hold a value (or the browser may have filled one).
    document.querySelectorAll('ul[data-password-rules]').forEach(list => update(list));
})();
