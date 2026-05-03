/**
 * G4 Studio — Theme Store.
 * Wraps the theme utility into a subscribable store.
 */

import { getTheme, setTheme, toggleTheme, restoreTheme } from '../utils/theme.js';
import { on, EVENTS } from '../utils/events.js';

/** @type {Set<Function>} */
const _listeners = new Set();

// Forward theme change events to store listeners
on(EVENTS.THEME_CHANGED, (theme) => {
    _listeners.forEach(fn => fn(theme));
});

/**
 * Get the current theme.
 * @returns {'dark'|'light'}
 */
export function getActiveTheme() {
    return getTheme();
}

/**
 * Subscribe to theme changes.
 * @param {function(theme: string): void} listener
 * @returns {function} Unsubscribe
 */
export function onThemeChange(listener) {
    _listeners.add(listener);
    return () => _listeners.delete(listener);
}

export { setTheme, toggleTheme, restoreTheme };
