// Filters the availability calendar's equipment cards (Views/Shared/_EquipmentPicker.cshtml) by text and status as
// the user types or taps a chip. Choosing a card is handled by calendar.js, which loads that unit's calendar in place.
(() => {
    document.querySelectorAll('[data-equipment-picker]').forEach(picker => {
        const search = picker.querySelector('[data-picker-search]');
        const items = [...picker.querySelectorAll('[data-picker-item]')];
        const chips = [...picker.querySelectorAll('[data-picker-filter]')];
        const empty = picker.querySelector('[data-picker-empty]');
        const count = picker.querySelector('[data-picker-count]');
        let status = '';

        const apply = () => {
            const terms = (search?.value ?? '').trim().toLowerCase().split(/\s+/).filter(Boolean);
            let shown = 0;
            items.forEach(item => {
                const match = (!status || item.dataset.status === status) && terms.every(term => item.dataset.search.includes(term));
                item.hidden = !match;
                if (match) shown++;
            });
            if (empty) empty.hidden = shown > 0;
            if (count) count.textContent = shown === items.length ? String(items.length) : `${shown} of ${items.length}`;
        };

        search?.addEventListener('input', apply);
        // Escape clears the search first, so the full list is one key away.
        search?.addEventListener('keydown', event => { if (event.key === 'Escape' && search.value) { search.value = ''; apply(); } });
        chips.forEach(chip => chip.addEventListener('click', () => {
            status = chip.dataset.pickerFilter;
            chips.forEach(other => other.setAttribute('aria-pressed', String(other === chip)));
            apply();
        }));

        // Keep the chosen unit in view when the list scrolls on its own.
        picker.querySelector('.equip-option.is-selected')?.scrollIntoView({ block: 'nearest' });
        window.addEventListener('pageshow', apply);
    });
})();
