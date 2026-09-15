// Selection-aware markdown editing helpers. Blazor owns the text; this file only computes the new text and restores the caret,
// so every toolbar button in the app behaves identically without each component re-implementing selection maths.
window.standFastMarkdown = (() => {
    const resolveTextArea = (elementId) => {
        const element = document.getElementById(elementId);
        if (!element) {
            return null;
        }
        return element.tagName === 'TEXTAREA' ? element : element.querySelector('textarea');
    };

    const commit = (textArea, value, selectionStart, selectionEnd) => {
        textArea.value = value;
        textArea.focus();
        textArea.setSelectionRange(selectionStart, selectionEnd);
        return value;
    };

    return {
        // Wraps the selection (or inserts a placeholder) with the supplied delimiters, and unwraps again when the selection is already wrapped.
        wrapSelection(elementId, before, after, placeholder) {
            const textArea = resolveTextArea(elementId);
            if (!textArea) {
                return null;
            }

            const value = textArea.value ?? '';
            const start = textArea.selectionStart;
            const end = textArea.selectionEnd;
            const selected = value.slice(start, end);
            const alreadyWrapped = value.slice(start - before.length, start) === before && value.slice(end, end + after.length) === after;

            if (alreadyWrapped) {
                const unwrapped = value.slice(0, start - before.length) + selected + value.slice(end + after.length);
                return commit(textArea, unwrapped, start - before.length, end - before.length);
            }

            const body = selected.length > 0 ? selected : placeholder;
            const next = value.slice(0, start) + before + body + after + value.slice(end);
            return commit(textArea, next, start + before.length, start + before.length + body.length);
        },

        // Applies a per-line prefix across the selected lines. An ordered prefix renumbers from 1; an existing identical prefix is removed instead.
        prefixLines(elementId, prefix, ordered) {
            const textArea = resolveTextArea(elementId);
            if (!textArea) {
                return null;
            }

            const value = textArea.value ?? '';
            const start = textArea.selectionStart;
            const end = textArea.selectionEnd;
            const lineStart = value.lastIndexOf('\n', start - 1) + 1;
            const lineEndIndex = value.indexOf('\n', end);
            const lineEnd = lineEndIndex === -1 ? value.length : lineEndIndex;

            const lines = value.slice(lineStart, lineEnd).split('\n');
            const applied = lines.map((line, index) => {
                const linePrefix = ordered ? `${index + 1}. ` : prefix;
                const orderedPattern = /^\d+\.\s/;
                if (ordered ? orderedPattern.test(line) : line.startsWith(prefix)) {
                    return ordered ? line.replace(orderedPattern, '') : line.slice(prefix.length);
                }
                return linePrefix + line;
            });

            const replacement = applied.join('\n');
            const next = value.slice(0, lineStart) + replacement + value.slice(lineEnd);
            return commit(textArea, next, lineStart, lineStart + replacement.length);
        },

        focus(elementId) {
            const textArea = resolveTextArea(elementId);
            if (textArea) {
                textArea.focus();
            }
        }
    };
})();
