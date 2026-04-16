/**
 * G4 Studio — Theme utility.
 * Reads, sets, and persists the active theme.
 * The theme is applied by setting data-theme="dark"|"light" on <html>.
 * Theme preference is persisted in localStorage.
 */

import { STORAGE_KEYS, THEMES } from './constants.js';
import { emit, EVENTS } from './events.js';

/**
 * Get the currently active theme.
 * @returns {'dark'|'light'}
 */
export function getTheme() {
    return document.documentElement.getAttribute('data-theme') || THEMES.DARK;
}

/**
 * Set the active theme, persist it, and emit a theme change event.
 * @param {'dark'|'light'} theme
 */
export function setTheme(theme) {
    const resolved = theme === THEMES.LIGHT ? THEMES.LIGHT : THEMES.DARK;
    document.documentElement.setAttribute('data-theme', resolved);
    try {
        localStorage.setItem(STORAGE_KEYS.THEME, resolved);
    } catch {
        // storage may be unavailable
    }
    emit(EVENTS.THEME_CHANGED, resolved);
}

/**
 * Toggle between dark and light themes.
 * @returns {'dark'|'light'} The new theme
 */
export function toggleTheme() {
    const next = getTheme() === THEMES.DARK ? THEMES.LIGHT : THEMES.DARK;
    setTheme(next);
    return next;
}

/**
 * Restore the previously persisted theme on page load.
 * Falls back to the OS preference if no stored value exists.
 */
export function restoreTheme() {
    let stored;
    try {
        stored = localStorage.getItem(STORAGE_KEYS.THEME);
    } catch { /* ignore */ }

    if (stored === THEMES.LIGHT || stored === THEMES.DARK) {
        setTheme(stored);
        return;
    }

    // Use OS preference as fallback
    const prefersDark = window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? true;
    setTheme(prefersDark ? THEMES.DARK : THEMES.LIGHT);
}
