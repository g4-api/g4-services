/**
 * G4 Studio — Page Loader Component.
 * Renders a centered spinner into a container while content is loading.
 * Also provides an error-state renderer for failed page loads.
 */

import { escapeHtml } from '../utils/dom.js';

/**
 * Show a loading spinner in a container.
 * @param {HTMLElement} container
 * @param {string} [message]
 */
export function showLoader(container, message = 'Loading…') {
    container.innerHTML = `
        <div class="page-loader">
            <div class="spinner spinner-lg"></div>
            <span class="page-loader-text">${escapeHtml(message)}</span>
        </div>
    `;
}

/**
 * Show an error state in a container.
 * @param {HTMLElement} container
 * @param {string} [message]
 * @param {Function} [onRetry]
 */
export function showError(container, message = 'Failed to load content.', onRetry = null) {
    container.innerHTML = `
        <div class="empty-state">
            <div class="empty-state-icon">⚠</div>
            <div class="empty-state-title">Something went wrong</div>
            <div class="empty-state-message">${escapeHtml(message)}</div>
            ${onRetry ? `<button class="btn btn-secondary mt-4" id="retry-btn">Try Again</button>` : ''}
        </div>
    `;
    if (onRetry) {
        container.querySelector('#retry-btn')?.addEventListener('click', onRetry);
    }
}

/**
 * Show an empty state in a container.
 * @param {HTMLElement} container
 * @param {object} opts
 * @param {string} [opts.icon]
 * @param {string} [opts.title]
 * @param {string} [opts.message]
 * @param {string} [opts.actionLabel]
 * @param {Function} [opts.onAction]
 */
export function showEmpty(container, { icon = '◯', title = 'Nothing here yet', message = '', actionLabel = '', onAction = null } = {}) {
    container.innerHTML = `
        <div class="empty-state">
            <div class="empty-state-icon">${escapeHtml(icon)}</div>
            <div class="empty-state-title">${escapeHtml(title)}</div>
            ${message ? `<div class="empty-state-message">${escapeHtml(message)}</div>` : ''}
            ${actionLabel && onAction ? `<button class="btn btn-primary mt-4" id="empty-action">${escapeHtml(actionLabel)}</button>` : ''}
        </div>
    `;
    if (actionLabel && onAction) {
        container.querySelector('#empty-action')?.addEventListener('click', onAction);
    }
}
