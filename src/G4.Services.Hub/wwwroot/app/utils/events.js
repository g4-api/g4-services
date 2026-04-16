/**
 * G4 Studio — Lightweight event bus.
 * Provides a simple publish/subscribe system for cross-module communication
 * without creating direct dependencies between pages or services.
 *
 * Usage:
 *   import { on, off, emit } from './events.js';
 *   const unsub = on('bot:status-changed', handler);
 *   emit('bot:status-changed', { id: '...', status: 'Online' });
 *   unsub(); // remove listener
 */

/** @type {Map<string, Set<Function>>} */
const _listeners = new Map();

/**
 * Subscribe to an event.
 * @param {string} event
 * @param {Function} handler
 * @returns {Function} Unsubscribe function
 */
export function on(event, handler) {
    if (!_listeners.has(event)) {
        _listeners.set(event, new Set());
    }
    _listeners.get(event).add(handler);

    return () => off(event, handler);
}

/**
 * Unsubscribe a handler from an event.
 * @param {string} event
 * @param {Function} handler
 */
export function off(event, handler) {
    _listeners.get(event)?.delete(handler);
}

/**
 * Emit an event, calling all registered handlers synchronously.
 * @param {string} event
 * @param {*} payload
 */
export function emit(event, payload) {
    _listeners.get(event)?.forEach(handler => {
        try {
            handler(payload);
        } catch (err) {
            console.error(`[events] Error in handler for "${event}":`, err);
        }
    });
}

/**
 * Subscribe to an event exactly once.
 * @param {string} event
 * @param {Function} handler
 * @returns {Function} Unsubscribe function
 */
export function once(event, handler) {
    const wrapper = (payload) => {
        handler(payload);
        off(event, wrapper);
    };
    return on(event, wrapper);
}

/** Well-known event names — use these to avoid typos across the codebase */
export const EVENTS = {
    /* Theme */
    THEME_CHANGED:          'theme:changed',
    /* Auth */
    AUTH_LOGIN:             'auth:login',
    AUTH_LOGOUT:            'auth:logout',
    /* Route */
    ROUTE_CHANGED:          'route:changed',
    /* Notifications */
    TOAST_SHOW:             'toast:show',
    /* Workflow */
    WORKFLOW_RUN_STARTED:   'workflow:run-started',
    WORKFLOW_RUN_STOPPED:   'workflow:run-stopped',
    WORKFLOW_RUN_COMPLETED: 'workflow:run-completed',
    WORKFLOW_LOG_ENTRY:     'workflow:log-entry',
    /* Bots */
    BOT_STATUS_CHANGED:     'bot:status-changed',
    /* Cache */
    CACHE_SYNCED:           'cache:synced',
};
