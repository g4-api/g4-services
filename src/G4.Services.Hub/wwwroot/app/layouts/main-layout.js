/**
 * G4 Studio — Main Layout.
 * Persistent shell: sidebar + content column (topbar + main content area).
 * The sidebar and topbar are mounted once and kept alive across navigations.
 * Only the main-content area is replaced per page navigation.
 */

import { mountSidebar } from '../components/app-sidebar.js';
import { getAppState, onAppState } from '../stores/app-store.js';
import { toggleTheme, getActiveTheme } from '../stores/theme-store.js';
import { on, EVENTS } from '../utils/events.js';
import { escapeHtml } from '../utils/dom.js';

/** Whether the shell has been mounted into #app */
let _mounted   = false;
/** Cleanup for the current page */
let _prevDestroy = null;
/** Cleanup for sidebar */
let _sidebarDestroy = null;
/** Cleanup for app-state subscription */
let _unsubApp = null;

/**
 * Render the main layout with a given page module.
 * @param {object} pageModule
 * @param {object} params
 */
export function renderMainLayout(pageModule, params) {
    _prevDestroy?.();
    _prevDestroy = null;

    if (!_mounted) {
        _mountShell();
    }

    // Render page into content area
    const content = document.getElementById('main-content');
    if (!content) return;

    content.scrollTop = 0;

    if (typeof pageModule.render === 'function') {
        content.innerHTML = '';
        const result = pageModule.render(content, params);
        if (result?.destroy) _prevDestroy = result.destroy;
    }
}

/** Build and mount the full shell once. */
function _mountShell() {
    const app = document.getElementById('app');
    if (!app) return;

    app.innerHTML = `
        <div class="main-layout">
            <div id="sidebar-container"></div>
            <div class="content-column">
                <header class="app-topbar" id="app-topbar">
                    <div class="topbar-breadcrumb" id="topbar-breadcrumb">
                        <span class="breadcrumb-segment">G4 Studio</span>
                    </div>
                    <div class="topbar-actions" id="topbar-actions">
                        <button class="btn btn-ghost btn-icon btn-sm" id="theme-toggle-btn" title="Toggle theme">☀</button>
                        <div style="width:1px;height:16px;background:var(--border-color);"></div>
                        <div style="font-size:var(--text-xs);color:var(--text-muted);padding:0 var(--space-4);" id="topbar-user"></div>
                    </div>
                </header>
                <main class="main-content" id="main-content" role="main"></main>
            </div>
        </div>
    `;

    // Mount sidebar
    const sidebarContainer = document.getElementById('sidebar-container');
    if (sidebarContainer) {
        const sidebar = mountSidebar(sidebarContainer);
        _sidebarDestroy = sidebar?.destroy;
    }

    // Theme toggle
    document.getElementById('theme-toggle-btn')?.addEventListener('click', () => {
        toggleTheme();
    });

    // Update topbar user badge
    _updateTopbarUser();

    // Subscribe to app state for breadcrumb updates
    _unsubApp = onAppState(state => {
        _updateBreadcrumbs(state.breadcrumbs);
    });

    // Update breadcrumbs on route change
    on(EVENTS.ROUTE_CHANGED, () => {
        const state = getAppState();
        _updateBreadcrumbs(state.breadcrumbs);
        _updateThemeIcon();
    });

    // Auth events — tear down shell on logout
    on(EVENTS.AUTH_LOGOUT, () => {
        _mounted = false;
        _prevDestroy = null;
        _sidebarDestroy?.();
        _sidebarDestroy = null;
        _unsubApp?.();
        _unsubApp = null;
    });

    _mounted = true;
    _updateThemeIcon();
}

function _updateBreadcrumbs(breadcrumbs = []) {
    const el = document.getElementById('topbar-breadcrumb');
    if (!el) return;
    if (!breadcrumbs.length) { el.innerHTML = '<span class="breadcrumb-segment">G4 Studio</span>'; return; }
    el.innerHTML = breadcrumbs.map((seg, i) => {
        const isLast = i === breadcrumbs.length - 1;
        const label  = escapeHtml(seg.label);
        const seg_el = isLast
            ? `<span class="breadcrumb-segment current">${label}</span>`
            : `<span class="breadcrumb-segment">${label}</span>`;
        const sep    = !isLast ? `<span class="breadcrumb-sep">›</span>` : '';
        return seg_el + sep;
    }).join('');
}

function _updateTopbarUser() {
    // Lazily pull from auth-store to avoid circular imports
    import('../stores/auth-store.js').then(({ getAuthState }) => {
        const { user } = getAuthState();
        const el = document.getElementById('topbar-user');
        if (el && user) el.textContent = user.name || user.username || '';
    });
}

function _updateThemeIcon() {
    const btn = document.getElementById('theme-toggle-btn');
    if (btn) btn.textContent = getActiveTheme() === 'dark' ? '☀' : '☾';
}
