// Sizes the open navigation drawer, and the page margin beside it, to the drawer's widest line instead of MudBlazor's fixed default width.
(() => {
    const drawerSelector = '.mud-drawer';
    const openClass = 'mud-drawer--open';
    const closedClass = 'mud-drawer--closed';
    // MudBlazor reads both the open drawer's width and the main content's left margin from this variable.
    const widthVariable = '--mud-drawer-width-left';

    // Measured on a hidden copy forced open, because the collapsed rail clips its labels. Blazor never re-renders <html>, so the width set there lasts the session.
    const fitToContent = () => {
        const drawer = document.querySelector(drawerSelector);
        if (!drawer) {
            return;
        }
        const copy = drawer.cloneNode(true);
        copy.removeAttribute('id');
        copy.setAttribute('aria-hidden', 'true');
        copy.classList.remove(closedClass);
        copy.classList.add(openClass);
        Object.assign(copy.style, { visibility: 'hidden', pointerEvents: 'none', width: 'max-content', overflow: 'visible' });
        drawer.after(copy);
        const width = Math.ceil(copy.getBoundingClientRect().width);
        copy.remove();
        if (width > 0) {
            document.documentElement.style.setProperty(widthVariable, `${width}px`);
        }
    };

    document.fonts.ready.then(fitToContent);
})();
