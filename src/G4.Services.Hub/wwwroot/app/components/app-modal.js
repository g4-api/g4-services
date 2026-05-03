/**
 * G4 Studio — Modal Component.
 * Provides a simple imperative API for showing and closing modals.
 * Renders into the #modal-layer element defined in index.html.
 *
 * Usage:
 *   import { openModal, closeModal } from '../components/app-modal.js';
 *
 *   openModal({
 *     title: 'Confirm',
 *     size:  'md',          // 'md' | 'lg' | 'xl'
 *     body:  '<p>Sure?</p>',
 *     actions: [
 *       { label: 'Cancel', variant: 'secondary', onClick: closeModal },
 *       { label: 'Delete', variant: 'danger',    onClick: () => { doIt(); closeModal(); } },
 *     ],
 *   });
 */

import { escapeHtml } from '../utils/dom.js';

/** @type {HTMLElement|null} */
let _layer = null;
/** @type {HTMLElement|null} */
let _current = null;

function getLayer() {
    if (!_layer) _layer = document.getElementById('modal-layer');
    return _layer;
}

/**
 * @typedef {object} ModalOptions
 * @property {string} title
 * @property {string|HTMLElement} body     HTML string or DOM node
 * @property {'md'|'lg'|'xl'} [size]
 * @property {Array<{label:string,variant:string,onClick:function}>} [actions]
 * @property {boolean} [dismissible]       Whether clicking backdrop or pressing Esc closes it (default true)
 */

/**
 * Open a modal.
 * @param {ModalOptions} opts
 */
export function openModal(opts = {}) {
    const {
        title       = '',
        body        = '',
        size        = 'md',
        actions     = [],
        dismissible = true,
    } = opts;

    closeModal(); // close any existing modal first

    const sizeClass = size === 'lg' ? 'modal-lg' : size === 'xl' ? 'modal-xl' : '';

    const backdrop = document.createElement('div');
    backdrop.className = 'modal-backdrop';

    const modal = document.createElement('div');
    modal.className = ['modal', sizeClass].filter(Boolean).join(' ');
    modal.setAttribute('role', 'dialog');
    modal.setAttribute('aria-modal', 'true');

    // Header
    const header = document.createElement('div');
    header.className = 'modal-header';
    header.innerHTML = `
        <span class="modal-title">${escapeHtml(title)}</span>
        ${dismissible ? `<button class="modal-close" aria-label="Close">✕</button>` : ''}
    `;

    // Body
    const bodyEl = document.createElement('div');
    bodyEl.className = 'modal-body';
    if (typeof body === 'string') {
        bodyEl.innerHTML = body;
    } else {
        bodyEl.appendChild(body);
    }

    modal.appendChild(header);
    modal.appendChild(bodyEl);

    // Footer (actions)
    if (actions.length) {
        const footer = document.createElement('div');
        footer.className = 'modal-footer';
        for (const action of actions) {
            const btn = document.createElement('button');
            btn.className = `btn btn-${action.variant || 'secondary'}`;
            btn.textContent = action.label || 'OK';
            btn.addEventListener('click', () => action.onClick?.());
            footer.appendChild(btn);
        }
        modal.appendChild(footer);
    }

    backdrop.appendChild(modal);
    _current = backdrop;
    getLayer().appendChild(backdrop);

    // Dismiss handlers
    if (dismissible) {
        backdrop.addEventListener('click', (e) => {
            if (e.target === backdrop) closeModal();
        });
        header.querySelector('.modal-close')?.addEventListener('click', closeModal);
        _escHandler = (e) => { if (e.key === 'Escape') closeModal(); };
        document.addEventListener('keydown', _escHandler);
    }

    // Focus trap: focus the modal
    modal.setAttribute('tabindex', '-1');
    requestAnimationFrame(() => modal.focus());
}

let _escHandler = null;

/** Close the current modal. */
export function closeModal() {
    if (!_current) return;
    if (_escHandler) {
        document.removeEventListener('keydown', _escHandler);
        _escHandler = null;
    }
    _current.remove();
    _current = null;
}

/**
 * Open a simple confirmation dialog.
 * @param {object} opts
 * @param {string} opts.title
 * @param {string} opts.message
 * @param {string} [opts.confirmLabel]
 * @param {string} [opts.confirmVariant]
 * @param {function} opts.onConfirm
 */
export function openConfirm({ title = 'Confirm', message = '', confirmLabel = 'Confirm', confirmVariant = 'danger', onConfirm }) {
    openModal({
        title,
        body: `<p style="color:var(--text-secondary);line-height:1.6">${escapeHtml(message)}</p>`,
        actions: [
            { label: 'Cancel',       variant: 'secondary', onClick: closeModal },
            { label: confirmLabel,   variant: confirmVariant, onClick: () => { closeModal(); onConfirm?.(); } },
        ],
    });
}
