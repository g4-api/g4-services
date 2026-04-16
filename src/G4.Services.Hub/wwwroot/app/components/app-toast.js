/**
 * G4 Studio — Toast Component.
 * Listens for TOAST_SHOW events and renders dismissible toast notifications
 * into the #toast-container element defined in index.html.
 */

import { on, EVENTS } from '../utils/events.js';
import { escapeHtml } from '../utils/dom.js';

const ICONS = {
    success: '✓',
    error:   '✕',
    warning: '⚠',
    info:    'ℹ',
};

/** Mount the toast listener. Call once from app.js. */
export function mountToasts() {
    const container = document.getElementById('toast-container');
    if (!container) {
        console.warn('[app-toast] #toast-container not found');
        return;
    }

    on(EVENTS.TOAST_SHOW, ({ type = 'info', title = '', message = '', duration = 4000 }) => {
        const toast = _createToast(type, title, message);
        container.appendChild(toast);

        // Auto-dismiss
        const timer = setTimeout(() => _dismiss(toast), duration);

        // Manual dismiss on click
        const btn = toast.querySelector('.toast-dismiss');
        if (btn) {
            btn.addEventListener('click', () => {
                clearTimeout(timer);
                _dismiss(toast);
            });
        }
    });
}

/**
 * Build a toast element.
 * @param {string} type
 * @param {string} title
 * @param {string} message
 * @returns {HTMLElement}
 */
function _createToast(type, title, message) {
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.setAttribute('role', 'alert');
    toast.innerHTML = `
        <span class="toast-icon">${ICONS[type] || ICONS.info}</span>
        <div class="toast-content">
            <div class="toast-title">${escapeHtml(title)}</div>
            ${message ? `<div class="toast-message">${escapeHtml(message)}</div>` : ''}
        </div>
        <button class="toast-dismiss" aria-label="Dismiss">✕</button>
    `;
    return toast;
}

/**
 * Animate and remove a toast element.
 * @param {HTMLElement} toast
 */
function _dismiss(toast) {
    if (!toast.isConnected) return;
    toast.style.transition = 'opacity 200ms ease, transform 200ms ease';
    toast.style.opacity = '0';
    toast.style.transform = 'translateX(16px)';
    setTimeout(() => toast.remove(), 220);
}
