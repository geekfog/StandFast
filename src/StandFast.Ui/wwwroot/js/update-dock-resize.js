// Lets the divider above the board's update dock be dragged, or moved with the arrow keys, to share the board's height between the columns and the dock.
// The dock's height is kept as a share of the board, so it scales with the window, and is remembered in this browser. Double-clicking the divider restores the default.
(() => {
    const splitterSelector = '.standfast-board-splitter';
    const boardSelector = '.standfast-board';
    const draggingClass = 'standfast-board-splitter-dragging';
    // app.css sizes the dock from this variable while this class is on <html>, which Blazor never re-renders, clamping it between the dock's and the columns' minimum heights.
    const resizedClass = 'standfast-update-dock-resized';
    const heightVariable = '--standfast-update-dock-height';
    const storageKey = 'standfast.updateDockHeightPercent';
    const keyStepPercent = 5;
    const keySteps = { ArrowUp: keyStepPercent, ArrowDown: -keyStepPercent };

    const root = document.documentElement;
    let currentPercent = null;

    const clampPercent = percent => Math.min(100, Math.max(0, percent));

    const apply = percent => {
        currentPercent = percent;
        root.classList.toggle(resizedClass, percent !== null);
        if (percent === null) {
            root.style.removeProperty(heightVariable);
        } else {
            root.style.setProperty(heightVariable, `${percent.toFixed(1)}%`);
        }
    };

    const save = () => {
        try {
            if (currentPercent === null) {
                localStorage.removeItem(storageKey);
            } else {
                localStorage.setItem(storageKey, currentPercent.toFixed(1));
            }
        } catch {
            // Storage can be blocked; the size then lasts only for this page load.
        }
    };

    const restore = () => {
        try {
            const stored = Number.parseFloat(localStorage.getItem(storageKey));
            if (Number.isFinite(stored)) {
                apply(clampPercent(stored));
            }
        } catch {
            // Storage can be blocked; the dock then starts at its default size.
        }
    };

    // The dock is the board's last child, so its height is the distance from the pointer to the board's bottom edge.
    const percentAt = (board, clientY) => {
        const bounds = board.getBoundingClientRect();
        return clampPercent((bounds.bottom - clientY) / bounds.height * 100);
    };

    // The share the dock fills now, used as the starting point for arrow keys before the dock has been resized.
    const measuredPercent = splitter => {
        const board = splitter.closest(boardSelector);
        const dock = splitter.nextElementSibling;
        return board && dock ? clampPercent(dock.getBoundingClientRect().height / board.getBoundingClientRect().height * 100) : 0;
    };

    document.addEventListener('pointerdown', event => {
        const splitter = event.target.closest?.(splitterSelector);
        const board = splitter?.closest(boardSelector);
        if (!board || event.button !== 0) {
            return;
        }
        event.preventDefault();
        splitter.setPointerCapture(event.pointerId);
        splitter.classList.add(draggingClass);

        const move = moveEvent => apply(percentAt(board, moveEvent.clientY));
        const end = () => {
            splitter.removeEventListener('pointermove', move);
            splitter.removeEventListener('pointerup', end);
            splitter.removeEventListener('pointercancel', end);
            splitter.classList.remove(draggingClass);
            save();
        };
        splitter.addEventListener('pointermove', move);
        splitter.addEventListener('pointerup', end);
        splitter.addEventListener('pointercancel', end);
    });

    document.addEventListener('dblclick', event => {
        if (event.target.closest?.(splitterSelector)) {
            apply(null);
            save();
        }
    });

    document.addEventListener('keydown', event => {
        const splitter = event.target.closest?.(splitterSelector);
        const step = keySteps[event.key];
        if (!splitter || step === undefined) {
            return;
        }
        event.preventDefault();
        apply(clampPercent((currentPercent ?? measuredPercent(splitter)) + step));
        save();
    });

    restore();
})();
