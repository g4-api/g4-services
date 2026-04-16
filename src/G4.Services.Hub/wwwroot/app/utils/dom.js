/**
 * G4 Studio — DOM utilities.
 * Small helper functions for working with the DOM safely and consistently.
 * No state, no side effects beyond what is explicitly called.
 */

/**
 * Query a single element, throwing if it is required but missing.
 * @param {string} selector
 * @param {Element|Document} [context=document]
 * @param {boolean} [required=false]
 * @returns {Element|null}
 */
export function qs(selector, context = document, required = false) {
    const el = context.querySelector(selector);
    if (required && !el) {
        throw new Error(`[dom] Required element not found: "${selector}"`);
    }
    return el;
}

/**
 * Query all matching elements as an Array.
 * @param {string} selector
 * @param {Element|Document} [context=document]
 * @returns {Element[]}
 */
export function qsa(selector, context = document) {
    return Array.from(context.querySelectorAll(selector));
}

/**
 * Create an element with optional attributes, classes, and text content.
 * @param {string} tag
 * @param {Object} [opts]
 * @param {string[]} [opts.classes]
 * @param {Object<string,string>} [opts.attrs]
 * @param {string} [opts.text]
 * @param {string} [opts.html]
 * @returns {HTMLElement}
 */
export function el(tag, opts = {}) {
    const node = document.createElement(tag);
    if (opts.classes?.length) node.classList.add(...opts.classes);
    if (opts.attrs) {
        for (const [k, v] of Object.entries(opts.attrs)) {
            node.setAttribute(k, v);
        }
    }
    if (opts.text !== undefined) node.textContent = opts.text;
    if (opts.html !== undefined) node.innerHTML = opts.html;
    return node;
}

/**
 * Remove all children from an element.
 * @param {Element} node
 */
export function clearChildren(node) {
    while (node.firstChild) node.removeChild(node.firstChild);
}

/**
 * Delegate a DOM event to a child matching selector.
 * Returns an unsubscribe function.
 * @param {Element} parent
 * @param {string} event
 * @param {string} selector
 * @param {Function} handler
 * @returns {Function}
 */
export function delegate(parent, event, selector, handler) {
    const listener = (e) => {
        const target = e.target.closest(selector);
        if (target && parent.contains(target)) {
            handler(e, target);
        }
    };
    parent.addEventListener(event, listener);
    return () => parent.removeEventListener(event, listener);
}

/**
 * Toggle a class on an element based on a condition.
 * @param {Element} node
 * @param {string} cls
 * @param {boolean} force
 */
export function toggleClass(node, cls, force) {
    node.classList.toggle(cls, force);
}

/**
 * Set multiple inline styles at once.
 * @param {HTMLElement} node
 * @param {Object<string,string>} styles
 */
export function css(node, styles) {
    Object.assign(node.style, styles);
}

/**
 * Build an HTML string safely, escaping text content.
 * @param {string} text
 * @returns {string}
 */
export function escapeHtml(text) {
    const map = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' };
    return String(text).replace(/[&<>"']/g, m => map[m]);
}

/**
 * Render a list of items into a container element.
 * Clears existing content before rendering.
 * @param {Element} container
 * @param {Array} items
 * @param {function(item): Element|string} renderFn
 */
export function renderList(container, items, renderFn) {
    clearChildren(container);
    if (!items?.length) return;
    const fragment = document.createDocumentFragment();
    for (const item of items) {
        const node = renderFn(item);
        if (typeof node === 'string') {
            const wrapper = document.createElement('div');
            wrapper.innerHTML = node;
            fragment.append(...wrapper.childNodes);
        } else {
            fragment.appendChild(node);
        }
    }
    container.appendChild(fragment);
}

/**
 * Scroll an element into view within its scrollable parent.
 * @param {Element} node
 * @param {'start'|'center'|'end'} [block='nearest']
 */
export function scrollIntoView(node, block = 'nearest') {
    node?.scrollIntoView({ behavior: 'smooth', block });
}
