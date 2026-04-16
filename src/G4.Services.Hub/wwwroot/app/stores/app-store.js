/**
 * G4 Studio — App Store.
 * Global UI state: loading, error, sidebar, notifications.
 * Uses a simple observable pattern — subscribe to state changes via listeners.
 */

import { emit, EVENTS } from '../utils/events.js';

const _state = {
    /** Whether the sidebar is collapsed */
    sidebarCollapsed: false,
    /** Global loading state (boolean or string message) */
    loading: false,
    /** Global error message or null */
    error: null,
    /** Current page title shown in topbar */
    pageTitle: 'Dashboard',
    /** Current breadcrumb segments [{label, href?}] */
    breadcrumbs: [],
    /** Whether dev mode is enabled */
    devMode: false,
};

/** @type {Set<Function>} */
const _listeners = new Set();

function _notify() {
    _listeners.forEach(fn => fn({ ..._state }));
}

/**
 * Get a copy of the current state.
 * @returns {typeof _state}
 */
export function getAppState() {
    return { ..._state };
}

/**
 * Subscribe to app state changes.
 * @param {function(state): void} listener
 * @returns {function} Unsubscribe
 */
export function onAppState(listener) {
    _listeners.add(listener);
    return () => _listeners.delete(listener);
}

/** Set the sidebar collapsed state and persist it. */
export function setSidebarCollapsed(collapsed) {
    _state.sidebarCollapsed = collapsed;
    try {
        localStorage.setItem('g4.sidebar.collapsed', collapsed ? '1' : '0');
    } catch { /* ignore */ }
    _notify();
}

/** Restore sidebar collapsed state from storage. */
export function restoreSidebarState() {
    try {
        const stored = localStorage.getItem('g4.sidebar.collapsed');
        if (stored === '1') _state.sidebarCollapsed = true;
    } catch { /* ignore */ }
}

/**
 * Set the global loading state.
 * @param {boolean|string} value  true | false | "Loading message..."
 */
export function setLoading(value) {
    _state.loading = value;
    _notify();
}

/**
 * Set a global error message.
 * @param {string|null} message
 */
export function setError(message) {
    _state.error = message;
    _notify();
}

/**
 * Set the page title and breadcrumbs.
 * @param {string} title
 * @param {Array<{label: string, href?: string}>} [breadcrumbs]
 */
export function setPageMeta(title, breadcrumbs = []) {
    _state.pageTitle = title;
    _state.breadcrumbs = breadcrumbs;
    document.title = title ? `${title} — G4 Studio` : 'G4 Studio';
    _notify();
}

/** Toggle developer mode. */
export function toggleDevMode() {
    _state.devMode = !_state.devMode;
    _notify();
}

/**
 * Show a toast notification.
 * Delegates to the TOAST_SHOW event handled by the toast component.
 * @param {'success'|'error'|'warning'|'info'} type
 * @param {string} title
 * @param {string} [message]
 * @param {number} [duration=4000]
 */
export function showToast(type, title, message = '', duration = 4000) {
    emit(EVENTS.TOAST_SHOW, { type, title, message, duration });
}
