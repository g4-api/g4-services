/**
 * G4 Studio — Workflow Layout.
 * Renders the outer workflow shell: workflow topbar, canvas host, log sidebar, status bar.
 * The workflow page module is responsible for its own inner state (canvas adapter, etc.).
 *
 * This layout does NOT include the main sidebar — the workflow view occupies full width
 * so the canvas has maximum real estate. A back-to-dashboard link is shown in the topbar.
 */

import { on, EVENTS } from '../utils/events.js';

/** @type {boolean} */
let _mounted   = false;
/** @type {function|null} */
let _prevDestroy = null;

// Reset on logout so the shell is rebuilt after the next login
on(EVENTS.AUTH_LOGOUT, () => {
    _prevDestroy?.();
    _prevDestroy = null;
    _mounted = false;
});

/**
 * Render the workflow layout with the given page module.
 * @param {object} pageModule
 * @param {object} params
 */
export function renderWorkflowLayout(pageModule, params) {
    _prevDestroy?.();
    _prevDestroy = null;

    if (!_mounted) {
        _mountShell();
    }

    const host = document.getElementById('workflow-page-host');
    if (!host) return;

    host.innerHTML = '';

    if (typeof pageModule.render === 'function') {
        const result = pageModule.render(host, params);
        if (result?.destroy) _prevDestroy = result.destroy;
    }
}

function _mountShell() {
    const app = document.getElementById('app');
    if (!app) return;

    app.innerHTML = `
        <div class="workflow-layout" style="height:100vh;">
            <div id="workflow-page-host" style="flex:1;overflow:hidden;display:flex;flex-direction:column;"></div>
        </div>
    `;

    _mounted = true;
}
