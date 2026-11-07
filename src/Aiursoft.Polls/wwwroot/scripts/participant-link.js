(() => {
    const labels = document.getElementById('participant-link-text');
    const toast = document.getElementById('participant-link-toast');
    if (!labels || !toast) return;
    const text = (key, fallback) => labels.querySelector(`[data-key="${key}"]`)?.textContent.trim() || fallback;
    const focusTarget = button => button.closest('.dropdown')?.querySelector('[data-bs-toggle="dropdown"]') || button;
    let fallbackTimer;

    function showCopied(button) {
        if (window.bootstrap?.Toast) {
            window.bootstrap.Toast.getOrCreateInstance(toast, { delay: 1800 }).show();
        } else {
            window.clearTimeout(fallbackTimer);
            toast.classList.add('show');
            fallbackTimer = window.setTimeout(() => toast.classList.remove('show'), 1800);
        }
        focusTarget(button).focus();
    }

    function showManualCopy(button, link) {
        const message = text('manual', 'Automatic copying is unavailable. Copy this link manually.');
        if (!window.Swal) {
            window.prompt(message, link);
            focusTarget(button).focus();
            return;
        }
        window.Swal.fire({
            title: text('title', 'Participant link'),
            input: 'text',
            inputLabel: message,
            inputValue: link,
            inputAttributes: { readonly: true },
            confirmButtonText: text('close', 'Close'),
            returnFocus: false,
            didOpen: () => {
                const input = window.Swal.getInput();
                input.focus();
                input.select();
            },
            didClose: () => focusTarget(button).focus()
        });
    }

    document.querySelectorAll('[data-copy-participant-link]').forEach(button => {
        button.addEventListener('click', async () => {
            const link = button.dataset.copyParticipantLink;
            button.disabled = true;
            let copied = false;
            try {
                if (!navigator.clipboard?.writeText) throw new Error('Clipboard unavailable');
                await navigator.clipboard.writeText(link);
                copied = true;
            } catch {
                // The manual route below also works without clipboard permission.
            } finally {
                button.disabled = false;
            }
            if (copied) showCopied(button);
            else showManualCopy(button, link);
        });
    });
})();
