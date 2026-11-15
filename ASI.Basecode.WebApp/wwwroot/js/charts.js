// Hover and focus tooltips for the server-rendered dashboard charts. Any element with data-tip shows its
// text (one line per "\n") beside the pointer, or beside the element when focused from the keyboard.
(() => {
    const tip = document.createElement('div');
    tip.className = 'chart-tip';
    tip.setAttribute('role', 'tooltip');
    tip.hidden = true;
    document.body.appendChild(tip);

    const place = (x, y) => {
        const gap = 14;
        const { width, height } = tip.getBoundingClientRect();
        const left = Math.min(Math.max(8, x + gap), window.innerWidth - width - 8);
        const top = y - height - gap < 8 ? y + gap : y - height - gap;
        tip.style.transform = `translate(${Math.round(left)}px, ${Math.round(top)}px)`;
    };
    const show = (target, x, y) => {
        const [title, ...lines] = target.dataset.tip.split('\n');
        tip.replaceChildren(Object.assign(document.createElement('strong'), { textContent: title }),
            ...lines.map(line => Object.assign(document.createElement('span'), { textContent: line })));
        tip.hidden = false;
        place(x, y);
    };
    const hide = () => { tip.hidden = true; };

    document.addEventListener('pointermove', event => {
        const target = event.target.closest?.('[data-tip]');
        if (!target || event.pointerType === 'touch') { if (!target) hide(); return; }
        show(target, event.clientX, event.clientY);
    });
    document.addEventListener('focusin', event => {
        const target = event.target.closest?.('[data-tip]');
        if (!target) return hide();
        const box = target.getBoundingClientRect();
        show(target, box.left + box.width / 2, box.top);
    });
    document.addEventListener('focusout', hide);
    document.addEventListener('scroll', hide, { passive: true });
    document.addEventListener('keydown', event => { if (event.key === 'Escape') hide(); });
})();
