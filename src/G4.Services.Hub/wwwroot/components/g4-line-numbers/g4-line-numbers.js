/*
 * G4(TM) line numbers component: <g4-line-numbers>.
 *
 * The gutter on the left of a text box: one number per line, kept in step with the box's text,
 * size, and vertical scroll. Text boxes never wrap, so every line is exactly one row. Ported from
 * the G4 Engine Client extension.
 *
 * Contract:
 * - Placement: directly before its <textarea>, inside a flex row (the gutter binds to its next
 *   element sibling).
 * - Attributes: variant ('editor' for an expanded editor; none for a field), test-id.
 * - Methods: refresh() renumbers after the owner sets the text box value in code.
 *
 * Typing, resizing, and scrolling are followed automatically. A box that is hidden (a closed
 * section) has no size yet; it is numbered when it becomes visible.
 */
(() => {
    'use strict';

    // Component name: the custom element tag, the template id prefix, and the default test id prefix.
    const COMPONENT_NAME = 'g4-line-numbers';

    // Line breaks in text box content (Windows or Unix). Linear: optional character plus one literal.
    const LINE_BREAK_PATTERN = /\r?\n/;

    // Id of the hidden one-row text area that measures row heights (one per page).
    const PROBE_ID = 'g4-line-numbers-probe';

    /**
     * Returns the page's row probe, creating it on first use.
     *
     * @remarks
     * A text area's row is taller than a block's when line-height is 'normal', so the row height is
     * read from a real one-row text area with the box's font.
     *
     * @returns {HTMLTextAreaElement} The probe.
     */
    const getRowProbe = () => {
        let probe = document.getElementById(PROBE_ID);

        if (!probe) {
            // First use on this page: create the hidden probe.
            probe = document.createElement('textarea');
            probe.id = PROBE_ID;
            probe.className = 'g4-line-numbers__probe';
            probe.rows = 1;
            probe.setAttribute('aria-hidden', 'true');
            probe.tabIndex = -1;
            document.body.append(probe);
        }

        return probe;
    };

    /**
     * Line-number gutter element.
     */
    class G4LineNumbers extends HTMLElement {
        // Whether the template has been rendered into this element.
        #isRendered = false;

        /**
         * Renumbers while the user types.
         */
        #onTextareaInput = () => {
            this.refresh();
        };

        /**
         * Keeps the numbers aligned while the text box scrolls.
         */
        #onTextareaScroll = () => {
            const rows = this.querySelector('[data-part="rows"]');

            if (rows && this.#textarea) {
                rows.scrollTop = this.#textarea.scrollTop;
            }
        };

        // Watches the text box size, so a box that becomes visible gets its numbers.
        #resizeObserver = null;

        // The numbered text box.
        #textarea = null;

        /**
         * Renders the gutter once and starts following its text box.
         */
        connectedCallback() {
            if (!this.#isRendered) {
                this.#isRendered = true;
                this.replaceChildren(this.#newContent());
            }

            this.#bind();
        }

        /**
         * Stops following the text box while the gutter is detached.
         */
        disconnectedCallback() {
            this.#resizeObserver?.disconnect();
            this.#textarea?.removeEventListener('input', this.#onTextareaInput);
            this.#textarea?.removeEventListener('scroll', this.#onTextareaScroll);
            this.#textarea = null;
        }

        /**
         * Writes one number per line, each one row tall, and aligns the gutter with the text.
         */
        refresh() {
            const textarea = this.#textarea;
            const rows = this.querySelector('[data-part="rows"]');

            // A detached or hidden box has nothing to align with yet.
            if (!textarea || !rows || textarea.clientWidth === 0) {
                return;
            }

            // Measure one row with the box's own font, so the numbers line up with the text.
            const style = getComputedStyle(textarea);
            const probe = getRowProbe();

            probe.style.font = style.font;
            probe.style.lineHeight = style.lineHeight;
            probe.value = 'x';

            const rowHeight = probe.scrollHeight;
            const lines = textarea.value.split(LINE_BREAK_PATTERN);

            rows.replaceChildren(...lines.map((_, index) => {
                const number = document.createElement('div');
                number.textContent = String(index + 1);
                number.style.height = `${rowHeight}px`;
                return number;
            }));

            // The column is as wide as the largest number, in the box's font (1ch is one digit).
            rows.style.font = style.font;
            rows.style.lineHeight = `${rowHeight}px`;
            rows.style.paddingTop = style.paddingTop;
            this.style.font = style.font;
            this.style.width = `calc(${String(lines.length).length}ch + 18px)`;
            rows.scrollTop = textarea.scrollTop;
        }

        /**
         * Starts following the next sibling text box: typing, size, and scroll.
         */
        #bind() {
            const textarea = this.nextElementSibling;

            if (!(textarea instanceof HTMLTextAreaElement)) {
                return;
            }

            // Follow typing, size changes (a closed section opening), and scrolling.
            this.#textarea = textarea;
            this.#resizeObserver ??= new ResizeObserver(() => this.refresh());
            this.#resizeObserver.observe(textarea);
            textarea.addEventListener('input', this.#onTextareaInput);
            textarea.addEventListener('scroll', this.#onTextareaScroll);
            this.refresh();
        }

        /**
         * Clones the template and prefixes every part's test id with this element's test-id.
         *
         * @returns {DocumentFragment} The gutter markup.
         */
        #newContent() {
            const template = document.getElementById(`${COMPONENT_NAME}-template`);
            const content = template.content.cloneNode(true);
            const prefix = this.getAttribute('test-id') || COMPONENT_NAME;

            // Every part gets a unique, descriptive test id under this element's prefix.
            content.querySelectorAll('[data-test-id]').forEach((element) => {
                element.dataset.testId = `${prefix}-${element.dataset.testId}`;
            });

            return content;
        }
    }

    if (!customElements.get(COMPONENT_NAME)) {
        customElements.define(COMPONENT_NAME, G4LineNumbers);
    }
})();
