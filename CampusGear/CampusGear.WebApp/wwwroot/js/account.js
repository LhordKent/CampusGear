(() => {
    document.querySelectorAll('[data-reveal]').forEach(button => {
        const input = document.getElementById(button.dataset.reveal);
        if (!input) return;
        button.addEventListener('click', () => {
            const show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            button.setAttribute('aria-pressed', String(show));
            button.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
        });
    });
})();
