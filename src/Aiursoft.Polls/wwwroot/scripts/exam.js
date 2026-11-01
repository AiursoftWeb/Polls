(() => {
    const form = document.getElementById('examForm');
    if (!form) return;
    const text = (key, fallback) => document.querySelector(`#exam-loc [data-key="${key}"]`)?.textContent || fallback;
    const expiresAt = new Date(form.dataset.expiresAt).getTime();
    const total = expiresAt - new Date(form.dataset.startedAt).getTime();
    const saveState = document.getElementById('saveState');
    const retry = document.getElementById('retrySave');
    const answers = new Map();
    let submitting = false;

    function renderSaveState() {
        const states = [...answers.values()];
        const failed = states.some(state => state.failed);
        const pending = states.some(state => state.savedRevision !== state.revision);
        saveState.textContent = failed ? text('failed', 'Some answers are not saved.')
            : pending ? text('saving', 'Saving…') : text('saved', 'All changes saved');
        saveState.classList.toggle('text-warning', failed);
        retry.hidden = !failed;
        retry.disabled = states.filter(state => state.failed).every(state => state.saving);
    }

    function updateAnsweredState() {
        let answered = 0;
        document.querySelectorAll('.question-card').forEach(card => {
            const hasAnswer = card.querySelectorAll('.answer-input:checked').length > 0;
            document.querySelectorAll(`[data-nav-question="${card.dataset.questionId}"]`).forEach(link => link.classList.toggle('answered', hasAnswer));
            if (hasAnswer) answered++;
        });
        document.getElementById('answeredCount').textContent = answered;
    }

    function submitExpiredAttempt() {
        submitting = true;
        form.submit();
    }

    function updateTimer() {
        const remaining = Math.max(0, expiresAt - Date.now());
        const seconds = Math.floor(remaining / 1000);
        const hours = Math.floor(seconds / 3600);
        const minutes = Math.floor((seconds % 3600) / 60);
        const secs = seconds % 60;
        document.getElementById('countdown').textContent =
            (hours ? String(hours).padStart(2, '0') + ':' : '') +
            String(minutes).padStart(2, '0') + ':' + String(secs).padStart(2, '0');
        document.getElementById('timeProgress').style.width = Math.max(0, remaining / total * 100) + '%';
        if (remaining <= 0 && !submitting) submitExpiredAttempt();
    }

    async function saveAnswer(state) {
        if (state.saving || submitting) return;
        state.saving = true;
        renderSaveState();
        try {
            // Serialize changes to one question, then send its latest selection.
            while (state.savedRevision !== state.revision && !submitting) {
                const revision = state.revision;
                const response = await fetch(form.dataset.saveUrl, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': form.querySelector('input[name="__RequestVerificationToken"]').value
                    },
                    body: JSON.stringify({
                        attemptId: Number(form.dataset.attemptId),
                        attemptQuestionId: state.questionId,
                        optionIds: state.selected
                    })
                });
                if (response.status === 409) {
                    if (!submitting) submitExpiredAttempt();
                    return;
                }
                if (!response.ok || (await response.json()).saved !== true) throw new Error('Save failed');
                state.savedRevision = revision;
                state.failed = false;
            }
        } catch {
            state.failed = true;
        } finally {
            state.saving = false;
            renderSaveState();
        }
    }

    document.querySelectorAll('.answer-input').forEach(input => input.addEventListener('change', event => {
        const card = event.target.closest('.question-card');
        const selected = [...card.querySelectorAll('.answer-input:checked')].map(x => Number(x.value));
        card.querySelector('.selection-value').value = selected.join(',');
        const questionId = Number(card.dataset.questionId);
        const state = answers.get(questionId) || { questionId, revision: 0, savedRevision: 0, failed: false, saving: false };
        state.selected = selected;
        state.revision++;
        answers.set(questionId, state);
        updateAnsweredState();
        saveAnswer(state);
    }));

    retry.addEventListener('click', () => {
        answers.forEach(state => {
            if (state.savedRevision !== state.revision) saveAnswer(state);
        });
    });
    window.addEventListener('beforeunload', event => {
        if (!submitting && [...answers.values()].some(state => state.savedRevision !== state.revision)) {
            event.preventDefault();
            event.returnValue = '';
        }
    });
    form.addEventListener('submit', () => {
        submitting = true;
        const button = document.getElementById('submitButton');
        button.disabled = true;
        button.textContent = text('submitting', 'Submitting…');
    });
    updateTimer();
    updateAnsweredState();
    setInterval(updateTimer, 1000);
})();
