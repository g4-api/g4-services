/**
 * G4 Studio — Auth Layout.
 * Simple centered layout for login and other unauthenticated pages.
 * Registered with the router as the "auth" layout renderer.
 */

/** @type {HTMLElement|null} */
let _shell = null;
/** @type {function|null} Cleanup for the previous page */
let _prevDestroy = null;

/**
 * Render the auth layout with the given page module.
 * @param {object} pageModule
 * @param {object} params
 */
export function renderAuthLayout(pageModule, params) {
    _prevDestroy?.();
    _prevDestroy = null;

    const app = document.getElementById('app');
    if (!app) return;

    // Build shell if not already present
    if (!_shell || !app.contains(_shell)) {
        app.innerHTML = '';
        _shell = document.createElement('div');
        _shell.className = 'auth-layout';
        app.appendChild(_shell);
    }

    // Render the page into the shell
    if (typeof pageModule.render === 'function') {
        const result = pageModule.render(_shell, params);
        if (result?.destroy) _prevDestroy = result.destroy;
    }
}
