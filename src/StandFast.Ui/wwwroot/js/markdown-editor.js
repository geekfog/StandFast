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

    // A list item line: indent, a bullet (-, *, +) or a number with its delimiter, the spacing after it, and an optional task checkbox.
    const listItemPattern = /^( *)(?:([-*+])|(\d{1,9})([.)]))( +|$)(\[[ xX]\] +)?/;
    const continuationPattern = /^ +\S/;
    const uncheckedTask = '[ ] ';
    const firstListNumber = 1;
    const editorHandlers = new Map();
    // Every kind of removal (Backspace, Delete, word and line deletes, cut, drag) reports an input type with this prefix.
    const deleteInputTypePrefix = 'delete';
    const previewSelector = '.standfast-markdown';
    const previewCheckboxSelector = `${previewSelector} input[type="checkbox"]`;

    const parseListItem = (line) => {
        const match = listItemPattern.exec(line);
        if (!match) {
            return null;
        }
        const marker = match[2] ?? `${match[3]}${match[4]}`;
        const spacing = match[5] || ' ';
        return {
            indent: match[1].length,
            bullet: match[2] ?? null,
            number: match[3] === undefined ? null : Number(match[3]),
            delimiter: match[4] ?? null,
            spacing,
            task: match[6] !== undefined,
            contentColumn: match[1].length + marker.length + spacing.length,
            prefixLength: match[0].length,
            content: line.slice(match[0].length)
        };
    };

    const isBlank = (line) => line.trim() === '';
    const isListContent = (line) => parseListItem(line) !== null || continuationPattern.test(line);
    const leadingSpaces = (line) => line.length - line.trimStart().length;
    const lineIndexAt = (value, position) => value.slice(0, position).split('\n').length - 1;
    const lineStartOffset = (lines, index) => lines.slice(0, index).reduce((offset, line) => offset + line.length + 1, 0);

    // The run of list lines around a line, including indented continuations and single blank lines between items, since markdown keeps those in one list.
    const listBlockBounds = (lines, index) => {
        const belongs = (at, step) => isListContent(lines[at]) || (isBlank(lines[at]) && at + step >= 0 && at + step < lines.length && isListContent(lines[at + step]));
        let first = index;
        while (first > 0 && belongs(first - 1, -1)) {
            first--;
        }
        let last = index;
        while (last < lines.length - 1 && belongs(last + 1, 1)) {
            last++;
        }
        return { first, last };
    };

    // Numbers each ordered sequence consecutively from 1 per nesting level, since some markdown renderers ignore any other starting number.
    const renumber = (lines, first, last) => {
        const levels = [];
        for (let index = first; index <= last; index++) {
            const item = parseListItem(lines[index]);
            if (!item) {
                continue;
            }
            while (levels.length > 0 && levels.at(-1).indent > item.indent) {
                levels.pop();
            }
            const sibling = levels.length > 0 && levels.at(-1).indent === item.indent ? levels.pop() : null;
            let number = null;
            if (item.number !== null) {
                number = sibling?.next ?? firstListNumber;
                const rest = lines[index].slice(item.indent + String(item.number).length + item.delimiter.length);
                lines[index] = `${' '.repeat(item.indent)}${number}${item.delimiter}${rest}`;
            }
            levels.push({ indent: item.indent, next: number === null ? null : number + 1 });
        }
    };

    const renumberAround = (lines, ...indexes) => {
        const bounds = indexes.map((index) => listBlockBounds(lines, index));
        renumber(lines, Math.min(...bounds.map((b) => b.first)), Math.max(...bounds.map((b) => b.last)));
    };

    // Indenting nests an item under the previous item at its level, aligned with that item's text; outdenting aligns it with its parent.
    const shiftDelta = (lines, index, item, direction) => {
        for (let previous = index - 1; previous >= 0; previous--) {
            const line = lines[previous];
            if (isBlank(line)) {
                continue;
            }
            const candidate = parseListItem(line);
            if (!candidate) {
                if (continuationPattern.test(line)) {
                    continue;
                }
                break;
            }
            if (direction > 0) {
                if (candidate.indent < item.indent) {
                    return 0;
                }
                if (candidate.indent === item.indent) {
                    return candidate.contentColumn - item.indent;
                }
            } else if (candidate.indent < item.indent) {
                return candidate.indent - item.indent;
            }
        }
        return direction > 0 ? 0 : -item.indent;
    };

    const shiftLine = (line, delta) => {
        if (isBlank(line)) {
            return line;
        }
        const indent = leadingSpaces(line);
        return ' '.repeat(Math.max(0, indent + delta)) + line.slice(indent);
    };

    // Keeps the caret the same distance from its line's end, which survives renumbering and indent changes earlier on the line.
    const caretFromLineEnd = (lines, index, offsetFromEnd) => {
        const start = lineStartOffset(lines, index);
        return Math.max(start, start + lines[index].length - offsetFromEnd);
    };

    // Enter on a list item starts the next item, carrying any text after the caret onto it; Enter on an empty item outdents it, or ends the list at the top level.
    const continueList = (textArea) => {
        const value = textArea.value ?? '';
        const start = textArea.selectionStart;
        if (start !== textArea.selectionEnd) {
            return null;
        }

        const lines = value.split('\n');
        const index = lineIndexAt(value, start);
        const line = lines[index];
        const item = parseListItem(line);
        const column = start - lineStartOffset(lines, index);
        if (!item || column < item.prefixLength) {
            return null;
        }

        let caretLine = index;
        let offsetFromEnd = line.length - column;
        if (isBlank(item.content)) {
            lines[index] = item.indent > 0 ? shiftLine(line, shiftDelta(lines, index, item, -1)) : '';
            offsetFromEnd = 0;
        } else {
            const marker = item.bullet ?? `${item.number + 1}${item.delimiter}`;
            const prefix = ' '.repeat(item.indent) + marker + item.spacing + (item.task ? uncheckedTask : '');
            lines.splice(index, 1, line.slice(0, column), prefix + line.slice(column));
            caretLine = index + 1;
        }

        renumberAround(lines, caretLine);
        const next = lines.join('\n');
        if (textArea.maxLength > 0 && next.length > textArea.maxLength) {
            return null;
        }
        const caret = caretFromLineEnd(lines, caretLine, offsetFromEnd);
        return commit(textArea, next, caret, caret);
    };

    // After a deletion, renumbers the list at the caret and the one starting on the next line, which is where the removed text joined up.
    const renumberAfterDelete = (textArea) => {
        const value = textArea.value ?? '';
        const start = textArea.selectionStart;
        if (start !== textArea.selectionEnd) {
            return null;
        }

        const lines = value.split('\n');
        const index = lineIndexAt(value, start);
        const offsetFromEnd = lineStartOffset(lines, index) + lines[index].length - start;
        const listLines = [index, index + 1].filter((at) => at < lines.length && isListContent(lines[at]));
        if (listLines.length === 0) {
            return null;
        }
        renumberAround(lines, ...listLines);

        const next = lines.join('\n');
        if (next === value) {
            return null;
        }
        const caret = caretFromLineEnd(lines, index, offsetFromEnd);
        return commit(textArea, next, caret, caret);
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
                const linePrefix = ordered ? `${firstListNumber + index}. ` : prefix;
                const orderedPattern = /^\d+\.\s/;
                if (ordered ? orderedPattern.test(line) : line.startsWith(prefix)) {
                    return ordered ? line.replace(orderedPattern, '') : line.slice(prefix.length);
                }
                return linePrefix + line;
            });

            const replacement = applied.join('\n');
            const next = value.slice(0, lineStart) + replacement + value.slice(lineEnd);
            if (start === end) {
                const caret = lineStart + replacement.length - (lineEnd - start);
                return commit(textArea, next, Math.max(lineStart, caret), Math.max(lineStart, caret));
            }
            return commit(textArea, next, lineStart, lineStart + replacement.length);
        },

        // Indents (direction 1) or outdents (direction -1) the selected lines by one list level, then renumbers the affected ordered lists.
        shiftLines(elementId, direction) {
            const textArea = resolveTextArea(elementId);
            if (!textArea) {
                return null;
            }

            const value = textArea.value ?? '';
            const start = textArea.selectionStart;
            const end = textArea.selectionEnd;
            const lines = value.split('\n');
            const firstLine = lineIndexAt(value, start);
            const lastLine = lineIndexAt(value, end);
            const offsetFromEnd = lines[firstLine].length - (start - lineStartOffset(lines, firstLine));

            let listLine = firstLine;
            while (listLine <= lastLine && !parseListItem(lines[listLine])) {
                listLine++;
            }
            if (listLine > lastLine) {
                return null;
            }

            const delta = shiftDelta(lines, listLine, parseListItem(lines[listLine]), direction);
            if (delta === 0) {
                return null;
            }

            for (let index = firstLine; index <= lastLine; index++) {
                lines[index] = shiftLine(lines[index], delta);
            }
            renumberAround(lines, firstLine, lastLine);

            const next = lines.join('\n');
            if (start === end) {
                const caret = caretFromLineEnd(lines, firstLine, offsetFromEnd);
                return commit(textArea, next, caret, caret);
            }
            return commit(textArea, next, lineStartOffset(lines, firstLine), lineStartOffset(lines, lastLine) + lines[lastLine].length);
        },

        // Continues lists on Enter, renumbers them after deletions, and toggles preview checkboxes. Listens on the editor container so it survives the preview toggle re-creating its contents.
        attach(elementId, dotNetReference, methodName, taskToggledMethodName) {
            const container = document.getElementById(elementId);
            if (!container || editorHandlers.has(elementId)) {
                return;
            }

            // Blazor owns the text, so the click is cancelled and the box shows its new state once the toggled text re-renders.
            const click = (event) => {
                const checkbox = event.target;
                if (!(checkbox instanceof HTMLInputElement) || checkbox.type !== 'checkbox' || checkbox.disabled || !checkbox.closest(previewSelector)) {
                    return;
                }
                event.preventDefault();
                const checkboxes = [...container.querySelectorAll(previewCheckboxSelector)];
                dotNetReference.invokeMethodAsync(taskToggledMethodName, checkboxes.indexOf(checkbox));
            };

            const keydown = (event) => {
                const textArea = event.target;
                if (event.key !== 'Enter' || event.shiftKey || event.ctrlKey || event.altKey || event.metaKey || event.isComposing
                    || textArea.tagName !== 'TEXTAREA' || textArea.readOnly) {
                    return;
                }
                const next = continueList(textArea);
                if (next !== null) {
                    event.preventDefault();
                    dotNetReference.invokeMethodAsync(methodName, next);
                }
            };
            const input = (event) => {
                const textArea = event.target;
                if (!event.inputType?.startsWith(deleteInputTypePrefix) || textArea.tagName !== 'TEXTAREA' || textArea.readOnly) {
                    return;
                }
                const next = renumberAfterDelete(textArea);
                if (next !== null) {
                    dotNetReference.invokeMethodAsync(methodName, next);
                }
            };

            const handlers = { click, input, keydown };
            Object.entries(handlers).forEach(([type, handler]) => container.addEventListener(type, handler));
            editorHandlers.set(elementId, { container, handlers });
        },

        detach(elementId) {
            const registration = editorHandlers.get(elementId);
            if (registration) {
                Object.entries(registration.handlers).forEach(([type, handler]) => registration.container.removeEventListener(type, handler));
                editorHandlers.delete(elementId);
            }
        },

        focus(elementId) {
            const textArea = resolveTextArea(elementId);
            if (textArea) {
                textArea.focus();
            }
        }
    };
})();
