// Browser-side behavior for report charts, kept here so hover feedback does not wait on a round trip to the server.
window.standFastChart = (() => {
    const observers = new Map();

    // Attribute names shared with the chart markup.
    const seriesAttribute = 'data-series';
    const nameAttribute = 'data-name';
    const detailAttribute = 'data-detail';
    const focusAttribute = 'data-focus';
    const focusedClass = 'is-focused';
    const tooltipClass = 'standfast-chart-tooltip';

    // Distance between the pointer and the tooltip's top-left corner, so the tooltip never sits under the pointer.
    const tooltipOffset = 14;

    let tooltip;

    const tooltipElement = () => {
        if (!tooltip) {
            tooltip = document.createElement('div');
            tooltip.className = tooltipClass;
            tooltip.hidden = true;
            document.body.appendChild(tooltip);
        }

        return tooltip;
    };

    const hideTooltip = () => {
        if (tooltip) {
            tooltip.hidden = true;
        }
    };

    const showTooltip = (event, name, detail) => {
        const element = tooltipElement();
        element.replaceChildren(Object.assign(document.createElement('strong'), { textContent: name }));
        if (detail) {
            element.append(document.createElement('br'), detail);
        }

        element.hidden = false;

        // Flipped to the pointer's other side near the window's right or bottom edge, so it stays fully on screen.
        const { offsetWidth: width, offsetHeight: height } = element;
        const left = event.clientX + tooltipOffset + width > window.innerWidth ? event.clientX - tooltipOffset - width : event.clientX + tooltipOffset;
        const top = event.clientY + tooltipOffset + height > window.innerHeight ? event.clientY - tooltipOffset - height : event.clientY + tooltipOffset;
        element.style.left = `${Math.max(left, 0)}px`;
        element.style.top = `${Math.max(top, 0)}px`;
    };

    const report = (element, reference) => {
        try {
            reference.invokeMethodAsync('OnWidthChangedAsync', Math.round(element.clientWidth));
        } catch {
            // The circuit has gone; the observer is cleaned up when the component disposes.
        }
    };

    // Highlights one series across the plot and the legend, dimming the rest, or clears the highlight when the series is null.
    const applyFocus = (root, series) => {
        if (series === null) {
            root.removeAttribute(focusAttribute);
        } else {
            root.setAttribute(focusAttribute, series);
        }

        root.querySelectorAll(`[${seriesAttribute}]`).forEach(element =>
            element.classList.toggle(focusedClass, element.getAttribute(seriesAttribute) === series));
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

        // Hovering a person's line, marker, or legend entry highlights that person and names them in a tooltip; clicking pins the highlight
        // until the same person or an empty part of the chart is clicked. Listeners live on the root, which re-renders keep in place.
        enableSeriesFocus: (rootId) => {
            const root = document.getElementById(rootId);
            if (!root || root.dataset.focusEnabled) {
                return;
            }

            root.dataset.focusEnabled = 'true';
            let pinned = null;

            const seriesOf = target => target?.closest?.(`[${seriesAttribute}]`) ?? null;

            root.addEventListener('pointermove', event => {
                const element = seriesOf(event.target);
                if (!element) {
                    hideTooltip();
                    applyFocus(root, pinned);
                    return;
                }

                applyFocus(root, element.getAttribute(seriesAttribute));

                // A legend entry already shows the name beside its swatch, so only the plot gets a tooltip.
                if (!(element instanceof SVGElement)) {
                    hideTooltip();
                    return;
                }

                showTooltip(event, element.closest(`[${nameAttribute}]`)?.getAttribute(nameAttribute) ?? '',
                    event.target.closest(`[${detailAttribute}]`)?.getAttribute(detailAttribute));
            });

            root.addEventListener('pointerleave', () => {
                hideTooltip();
                applyFocus(root, pinned);
            });

            root.addEventListener('click', event => {
                const series = seriesOf(event.target)?.getAttribute(seriesAttribute) ?? null;
                pinned = series === pinned ? null : series;
                applyFocus(root, pinned ?? series);
            });

            // Keyboard users reach the legend entries by tab, and get the same highlight a pointer would.
            root.addEventListener('focusin', event => applyFocus(root, seriesOf(event.target)?.getAttribute(seriesAttribute) ?? pinned));
            root.addEventListener('focusout', () => applyFocus(root, pinned));
        },

        disconnect: (elementId) => {
            observers.get(elementId)?.disconnect();
            observers.delete(elementId);
            hideTooltip();
        },
    };
})();
