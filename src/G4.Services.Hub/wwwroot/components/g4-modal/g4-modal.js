/*
 * G4(TM) modal component: <g4-modal>.
 *
 * A centered dialog over the page: a title, optional header tools, the content, and an icon-only
 * Close button on the dialog's top-right corner. Escape closes the top modal; clicking the backdrop
 * does nothing, so a stray click never closes an editor. Ported from the G4 Engine Client extension.
 *
 * Contract:
 * - Attributes: modal-title (header text), size ('editor' | 'reference' | none), test-id (prefix of
 *   every part's data-test-id).
 * - Children: elements with data-slot="tools" go to the header; every other child is the content.
 *   Header buttons with the g4-modal__tool class get the header button look.
 * - Methods: show(focusReturnElement) opens the modal over the page; close() removes it and gives
 *   focus back to focusReturnElement.
 * - Events: g4-close (bubbles) after the modal closed.
 *
 * Modals open inside #g4-modal when the page has it, otherwise at the end of <body>; a later modal
 * stacks over an earlier one. While any modal is open, the page's #app is inert, so the designer
 * behind it can neither be clicked nor reached with the keyboard.
 */
(() => {
    'use strict';

    // Component name: the custom element tag, the template id prefix, and the default test id prefix.
    const COMPONENT_NAME = 'g4-modal';

    // Open modals, oldest first. Module-level because Escape is one document listener that must
    // close only the newest modal of the page.
    const OPEN_MODALS = [];

    /**
     * Closes the newest modal on Escape.
     *
     * @param {KeyboardEvent} event - Key event.
     */
    const onDocumentKeyDown = (event) => {
        const modal = OPEN_MODALS.at(-1);

        if (event.key !== 'Escape' || !modal) {
            return;
        }

        event.preventDefault();
        modal.close();
    };

    /**
     * Makes the page behind the modals inert while at least one modal is open.
     */
    const updatePageInert = () => {
        const app = document.getElementById('app');

        if (app) {
            app.inert = OPEN_MODALS.length > 0;
        }
    };

    /**
     * Modal dialog element.
     */
    class G4Modal extends HTMLElement {
        // Element that gets focus back when the modal closes.
        #focusReturnElement = null;

        // Whether the template has been rendered into this element.
        #isRendered = false;

        /**
         * Removes the modal and returns focus to where the user was.
         */
        close() {
            // Forget this modal, so Escape goes to the one under it, and free the page when it was the last.
            const index = OPEN_MODALS.indexOf(this);

            if (index !== -1) {
                OPEN_MODALS.splice(index, 1);
            }

            updatePageInert();

            // Remove it, tell the page, and give focus back to where the user was.
            this.remove();
            this.dispatchEvent(new CustomEvent('g4-close', { bubbles: true }));

            if (this.#focusReturnElement?.isConnected) {
                this.#focusReturnElement.focus();
            }
        }

        /**
         * Renders the dialog once, when the element is first attached.
         */
        connectedCallback() {
            // An element moved in the page keeps its rendered parts and listeners.
            if (this.#isRendered) {
                return;
            }

            this.#isRendered = true;
            this.#render();
        }

        /**
         * Opens the modal over the page.
         *
         * @param {HTMLElement | null} focusReturnElement - Element that gets focus back on close.
         */
        show(focusReturnElement) {
            this.#focusReturnElement = focusReturnElement ?? null;
            (document.getElementById('g4-modal') ?? document.body).append(this);
            OPEN_MODALS.push(this);
            updatePageInert();
        }

        /**
         * Clones the template and prefixes every part's test id with this element's test-id.
         *
         * @returns {DocumentFragment} The dialog markup.
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

        /**
         * Builds the dialog around the children: tools in the header, the rest as content.
         */
        #render() {
            const children = [...this.childNodes];
            const content = this.#newContent();
            const titleId = `${this.getAttribute('test-id') || COMPONENT_NAME}-title-${OPEN_MODALS.length}`;
            const title = content.querySelector('[data-part="title"]');
            const dialog = content.querySelector('.g4-modal__dialog');
            const size = this.getAttribute('size');

            // The title names the dialog for assistive technology.
            title.id = titleId;
            title.textContent = this.getAttribute('modal-title') ?? '';
            dialog.setAttribute('aria-labelledby', titleId);

            if (size) {
                dialog.classList.add(`g4-modal__dialog--${size}`);
            }

            // Header tools go next to the title; everything else is the content.
            const tools = content.querySelector('[data-part="tools"]');
            const body = content.querySelector('[data-part="content"]');

            children.forEach((child) => {
                const isTool = child.nodeType === Node.ELEMENT_NODE && child.dataset.slot === 'tools';
                (isTool ? tools : body).append(child);
            });

            content.querySelector('[data-part="close"]').addEventListener('click', () => this.close());
            this.replaceChildren(content);
        }
    }

    document.addEventListener('keydown', onDocumentKeyDown);

    if (!customElements.get(COMPONENT_NAME)) {
        customElements.define(COMPONENT_NAME, G4Modal);
    }
})();
