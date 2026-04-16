/**
 * G4 Studio — Auth Store (Mock).
 * Provides a mock session layer. No real tokens, no real auth flows.
 * Replace with a real auth implementation in a future phase.
 */

import { STORAGE_KEYS, MOCK_ADMIN_USER } from '../utils/constants.js';
import { emit, EVENTS } from '../utils/events.js';

const _state = {
    /** Current authenticated user or null */
    user: null,
    /** Whether a session is active */
    authenticated: false,
};

/** @type {Set<Function>} */
const _listeners = new Set();

function _notify() {
    _listeners.forEach(fn => fn({ ..._state }));
}

/**
 * Get current auth state.
 * @returns {{ user: object|null, authenticated: boolean }}
 */
export function getAuthState() {
    return { ..._state };
}

/**
 * Subscribe to auth state changes.
 * @param {function} listener
 * @returns {function} Unsubscribe
 */
export function onAuthState(listener) {
    _listeners.add(listener);
    return () => _listeners.delete(listener);
}

/**
 * Restore session from localStorage on page load.
 */
export function restoreSession() {
    try {
        const raw = localStorage.getItem(STORAGE_KEYS.SESSION);
        if (raw) {
            const user = JSON.parse(raw);
            _state.user = user;
            _state.authenticated = true;
        }
    } catch { /* malformed storage */ }
}

/**
 * Mock login — validates against the single hard-coded admin credential.
 * @param {string} username
 * @param {string} password
 * @returns {Promise<{success: boolean, error?: string}>}
 */
export async function login(username, password) {
    // Simulate network delay
    await new Promise(r => setTimeout(r, 400));

    if (
        username === MOCK_ADMIN_USER.username &&
        password === MOCK_ADMIN_USER.password
    ) {
        const user = { ...MOCK_ADMIN_USER };
        delete user.password; // never persist the password
        _state.user = user;
        _state.authenticated = true;
        try {
            localStorage.setItem(STORAGE_KEYS.SESSION, JSON.stringify(user));
        } catch { /* ignore */ }
        _notify();
        emit(EVENTS.AUTH_LOGIN, user);
        return { success: true };
    }

    return { success: false, error: 'Invalid username or password.' };
}

/**
 * Log out and clear the session.
 */
export function logout() {
    _state.user = null;
    _state.authenticated = false;
    try {
        localStorage.removeItem(STORAGE_KEYS.SESSION);
    } catch { /* ignore */ }
    _notify();
    emit(EVENTS.AUTH_LOGOUT, null);
}

/**
 * Check whether the current user has a given role.
 * @param {string} role
 * @returns {boolean}
 */
export function hasRole(role) {
    return _state.user?.role === role;
}

/**
 * Get the display name of the current user.
 * @returns {string}
 */
export function getUserDisplayName() {
    return _state.user?.name || _state.user?.username || 'User';
}
