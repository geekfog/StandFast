// Reports the width a chart has to draw in. A chart lays itself out in pixels and only the browser knows how much room the window
// leaves it, so the measurement comes from here and the component recomputes its geometry from the number.
window.standFastChart = (() => {
    const observers = new Map();

    const report = (element, reference) => {
        try {
            reference.invokeMethodAsync('OnWidthChangedAsync', Math.round(element.clientWidth));
        } catch {
            // The circuit has gone; the observer is cleaned up when the component disposes.
        }
    };

    return {
        // Reports the element's width immediately and again whenever it changes, so the chart follows the window rather than only the first paint.
        observe: (elementId, reference) => {
            const element = document.getElementById(elementId);
            if (!element) {
                return;
            }

            observers.get(elementId)?.disconnect();

            const observer = new ResizeObserver(() => report(element, reference));
            observer.observe(element);
            observers.set(elementId, observer);

            report(element, reference);
        },

        disconnect: (elementId) => {
            observers.get(elementId)?.disconnect();
            observers.delete(elementId);
        },
    };
})();
