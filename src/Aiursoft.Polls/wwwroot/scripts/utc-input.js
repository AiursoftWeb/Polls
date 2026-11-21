// The server renders UTC values. Only marked instant fields use browser-local editing.
(() => {
    const fields = document.querySelectorAll('input[data-utc-input]');
    document.addEventListener('DOMContentLoaded', () => {
        const $ = window.jQuery;
        if (!$) return;

        $(() => fields.forEach(input => {
            // jQuery Validate cannot apply its numeric step rule to datetime-local.
            if ($(input.form).data('validator')) $(input).rules('add', { step: false });
        }));
    });

    if (!('FormDataEvent' in window)) return;
    const pad = value => String(value).padStart(2, '0');
    const format = date => `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
    const notes = document.querySelectorAll('[data-utc-input-note]');
    const invalidTime = document.querySelector('[data-key="invalid-local-time"]')?.textContent
        ?? 'This local time does not exist because the clocks change. Choose another time.';

    fields.forEach(input => {
        const utc = input.value ? new Date(input.value + 'Z') : null;
        if (utc && !isNaN(utc.getTime())) {
            input.value = format(utc);
        }
        const initialValue = input.value;
        const initialUtc = utc && !isNaN(utc.getTime()) ? utc.toISOString() : null;
        const validate = () => {
            const date = new Date(input.value);
            // Reject spring-forward gaps instead of silently moving the time forward.
            const expected = input.value.length === 16 ? input.value + ':00' : input.value;
            input.setCustomValidity(input.value && (isNaN(date.getTime()) || format(date) !== expected) ? invalidTime : '');
        };
        input.addEventListener('input', validate);
        input.addEventListener('change', validate);
        input.form?.addEventListener('submit', event => {
            // The form's JavaScript validator disables automatic native validation.
            validate();
            if (!input.reportValidity()) event.preventDefault();
        });
        input.form?.addEventListener('formdata', event => {
            if (!input.name || input.disabled) return;
            if (!input.value) {
                event.formData.set(input.name, '');
                return;
            }
            // Preserve an unchanged instant, including the second occurrence of a
            // repeated autumn hour. Do not mutate the visible input during submission.
            const value = input.value === initialValue && initialUtc
                ? initialUtc : new Date(input.value).toISOString();
            event.formData.set(input.name, value);
        });
    });
    notes.forEach(note => {
        note.querySelector('[data-key="utc-input-time"]').hidden = true;
        note.querySelector('[data-key="local-input-time"]').hidden = false;
    });
})();
