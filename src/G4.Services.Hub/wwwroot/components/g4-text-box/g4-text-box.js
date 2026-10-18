/*
 * G4(TM) text box component: <g4-text-box>.
 *
 * Dresses a canvas text field like the G4 Engine Client text boxes: a line-number gutter on the
 * left and an Expand button in the top-right corner, shown while the box is hovered or focused.
 * The owner keeps creating and wiring its own <textarea>; this element only arranges it, so the
 * field keeps working even before (or without) the component's template.
 *
 * Contract:
 * - Children: one <textarea>, created by the owner (its id, value, and listeners stay as they are).
 * - Attributes: test-id (prefix of every part's data-test-id; default 'text-box').
 * - Properties: textarea (the owner's text area).
 * - Methods: refresh() renumbers after the owner sets the text area value in code.
 * - Events: g4-expand when the Expand button is clicked; the owner opens its editor.
 */
(() => {
    'use strict';

    // Component name: the custom element tag and the template id prefix.
    const COMPONENT_NAME = 'g4-text-box';

    // Default test id prefix.
    const DEFAULT_TEST_ID = 'text-box';

    /**
     * Text box element.
     */
    class G4TextBox extends HTMLElement {
        // Whether the template has been rendered into this element.
        #isRendered = false;

        /**
         * Asks the owner to open its editor.
         */
        #onExpandClick = () => {
            this.dispatchEvent(new CustomEvent('g4-expand'));
        };

        /**
         * The owner's text area.
         *
         * @returns {HTMLTextAreaElement | null} The text area.
         */
        get textarea() {
            return this.querySelector('textarea');
        }

        /**
         * Arranges the text area once, when the element is first attached.
         *
         * @remarks
         * The page loads the templates before the designer draws, so the template is normally there.
         * If it is not yet, the box renders as soon as the templates arrive; until then the plain
         * text area keeps working.
         */
        connectedCallback() {
            if (this.#isRendered) {
                return;
            }

            // Render now when the template is there; otherwise once the template files are loaded.
            if (document.getElementById(`${COMPONENT_NAME}-template`)) {
                this.#render();
                return;
            }

            window.G4Templates?.ready.then(() => {
                if (this.isConnected) {
                    this.#render();
                }
            });
        }

        /**
         * Renumbers the gutter after the owner set the text area value in code.
         */
        refresh() {
            this.querySelector('g4-line-numbers')?.refresh();
        }

        /**
         * Clones the template and prefixes every part's test id with this element's test-id.
         *
         * @returns {DocumentFragment} The box markup.
         */
        #newContent() {
            const template = document.getElementById(`${COMPONENT_NAME}-template`);
            const content = template.content.cloneNode(true);
            const prefix = this.getAttribute('test-id') || DEFAULT_TEST_ID;

            // Every part gets a unique, descriptive test id under this element's prefix.
            content.querySelectorAll('[data-test-id]').forEach((element) => {
                element.dataset.testId = `${prefix}-${element.dataset.testId}`;
            });

            return content;
        }

        /**
         * Puts the owner's text area between the gutter and the corner tools.
         */
        #render() {
            const textarea = this.querySelector(':scope > textarea');
            const template = document.getElementById(`${COMPONENT_NAME}-template`);

            // Render once, and only around a text area: without one there is nothing to dress.
            if (this.#isRendered || !textarea || !template) {
                return;
            }

            this.#isRendered = true;

            // The text area takes the slot's place, keeping its own id, value, and listeners.
            const content = this.#newContent();
            const prefix = this.getAttribute('test-id') || DEFAULT_TEST_ID;

            textarea.classList.add('g4-text-box__input');
            textarea.dataset.testId ??= `${prefix}-textarea`;
            content.querySelector('[data-part="textarea-slot"]').replaceWith(textarea);

            // The corner button asks the owner to open its editor.
            content.querySelector('[data-part="expand"]').addEventListener('click', this.#onExpandClick);
            this.replaceChildren(content);
        }
    }

    if (!customElements.get(COMPONENT_NAME)) {
        customElements.define(COMPONENT_NAME, G4TextBox);
    }
})();
