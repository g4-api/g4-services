/**
 * G4 Studio — Canvas Page Adapter.
 * Responsible for hosting the existing canvas page inside the workflow shell.
 *
 * Strategy:
 *   - The canvas page is loaded inside an <iframe> within the canvas-host element.
 *   - If the canvas page URL resolves (200), the iframe is shown.
 *   - If the canvas page is missing (404 or network error), a placeholder is shown instead.
 *   - The adapter does NOT reach into the iframe DOM — the canvas page owns its internals.
 *   - Communication between the outer shell and the canvas page (if needed) goes via postMessage.
 *
 * The canvas page path is configurable. Default: /views/canvas-placeholder.html
 * When the real canvas page is available, point CANVAS_PAGE_PATH to it.
 */

/** Path to the real canvas page. Update when the actual page is deployed. */
const CANVAS_PAGE_PATH = '/canvas';

/** Fallback placeholder path (always available) */
const PLACEHOLDER_PATH = '/views/canvas-placeholder.html';

/** @type {HTMLIFrameElement|null} */
let _iframe = null;

/** @type {boolean} Whether the real canvas page was confirmed available */
let _canvasAvailable = null;

/**
 * Check whether the canvas page exists by issuing a HEAD request.
 * Result is cached for the session.
 * @returns {Promise<boolean>}
 */
async function probeCanvasPage() {
    if (_canvasAvailable !== null) return _canvasAvailable;
    try {
        const res = await fetch(CANVAS_PAGE_PATH, { method: 'HEAD', cache: 'no-store' });
        _canvasAvailable = res.ok;
    } catch {
        _canvasAvailable = false;
    }
    return _canvasAvailable;
}

/**
 * Mount the canvas page (or placeholder) into the given host element.
 * Clears any existing content in the host first.
 * @param {HTMLElement} hostElement  The .canvas-host container
 * @returns {Promise<{ isPlaceholder: boolean }>}
 */
export async function mountCanvas(hostElement) {
    // Clear previous content
    hostElement.innerHTML = '';

    const available = await probeCanvasPage();
    const src = available ? CANVAS_PAGE_PATH : PLACEHOLDER_PATH;

    _iframe = document.createElement('iframe');
    _iframe.src = src;
    _iframe.className = 'canvas-embed';
    _iframe.setAttribute('title', available ? 'Workflow Canvas' : 'Canvas Placeholder');
    _iframe.setAttribute('allowfullscreen', '');
    _iframe.setAttribute('sandbox', [
        'allow-scripts',
        'allow-same-origin',
        'allow-forms',
        'allow-popups',
        'allow-modals',
    ].join(' '));

    hostElement.appendChild(_iframe);

    return { isPlaceholder: !available };
}

/**
 * Unmount and clean up the canvas iframe.
 * @param {HTMLElement} hostElement
 */
export function unmountCanvas(hostElement) {
    if (_iframe) {
        _iframe.src = 'about:blank';
        _iframe.remove();
        _iframe = null;
    }
    hostElement.innerHTML = '';
}

/**
 * Send a message to the canvas page via postMessage.
 * Only works when the iframe origin matches (same-origin canvas pages).
 * @param {*} message
 */
export function sendToCanvas(message) {
    if (!_iframe?.contentWindow) return;
    try {
        _iframe.contentWindow.postMessage(message, location.origin);
    } catch (err) {
        console.warn('[canvas-adapter] postMessage failed:', err);
    }
}

/**
 * Listen for messages from the canvas page.
 * Returns an unsubscribe function.
 * @param {function(MessageEvent): void} handler
 * @returns {function}
 */
export function onCanvasMessage(handler) {
    const listener = (event) => {
        if (event.source !== _iframe?.contentWindow) return;
        handler(event);
    };
    window.addEventListener('message', listener);
    return () => window.removeEventListener('message', listener);
}

/**
 * Check whether the canvas iframe is currently loaded (not placeholder).
 * @returns {boolean}
 */
export function isCanvasLoaded() {
    return _iframe !== null && _canvasAvailable === true;
}
