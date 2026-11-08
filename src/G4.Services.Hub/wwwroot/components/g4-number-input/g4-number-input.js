/*
 * G4(TM) number input component: <g4-number-input>.
 *
 * Dresses a canvas number box like the G4 Engine Client number boxes: themed up/down steppers
 * inside its right edge in place of the native spinners (which cannot follow the canvas theme).
 * The owner keeps creating and wiring its own <input type="number">; this element only arranges
 * it, so the field keeps working even before (or without) the component's template.
 *
 * Contract:
 * - Children: one <input type="number">, created by the owner (its id, value, step, min, max,
 *   readonly, and listeners stay as they are).
 * - Attributes: test-id (prefix of every part's data-test-id; default 'number-input').
 * - Properties: input (the owner's number box).
 * - Events: a stepper click fires a bubbling 'input' event on the owner's box, the same event as
 *   typing, so the owner's listeners need no change.
 */
(() => {
    'use strict';

    // Component name: the custom element tag and the template id prefix.
    const COMPONENT_NAME = 'g4-number-input';

    // Default test id prefix.
    const DEFAULT_TEST_ID = 'number-input';

    /**
     * Number box element.
     */
    class G4NumberInput extends HTMLElement {
        // Whether the template has been rendered into this element.
        #isRendered = false;

        // Steps the box down by one step.
        #onDecreaseClick = () => {
            this.#step(-1);
        };

        // Steps the box up by one step.
        #onIncreaseClick = () => {
            this.#step(1);
        };

        // Keeps the caret in the box while a stepper is pressed, so stepping never moves the focus.
        #onStepperPointerDown = (event) => {
            event.preventDefault();
        };

        /**
         * The owner's number box.
         *
         * @returns {HTMLInputElement | null} The number box.
         */
        get input() {
            return this.querySelector('input[type="number"]');
        }

        /**
         * Arranges the number box once, when the element is first attached.
         *
         * @remarks
         * The page loads the templates before the designer draws, so the template is normally there.
         * If it is not yet, the box renders as soon as the templates arrive; until then the plain
         * number box keeps working.
         */
        connectedCallback() {
            // A box moved in the page keeps its steppers and listeners.
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
         * Puts the owner's number box in front of the steppers.
         */
        #render() {
            const input = this.querySelector(':scope > input[type="number"]');
            const template = document.getElementById(`${COMPONENT_NAME}-template`);

            // Render once, and only around a number box: without one there is nothing to dress.
            if (this.#isRendered || !input || !template) {
                return;
            }

            this.#isRendered = true;

            // The number box takes the slot's place, keeping its own id, value, and listeners.
            const content = this.#newContent();
            const prefix = this.getAttribute('test-id') || DEFAULT_TEST_ID;
            const increase = content.querySelector('[data-part="increase"]');
            const decrease = content.querySelector('[data-part="decrease"]');

            input.classList.add('g4-number-input__input');
            input.dataset.testId ??= `${prefix}-input`;
            content.querySelector('[data-part="input-slot"]').replaceWith(input);

            // A read-only box shows its steppers disabled, so they never suggest the value can change.
            increase.disabled = input.readOnly || input.disabled;
            decrease.disabled = increase.disabled;

            // Both steppers step the box; pressing them leaves the caret where it is.
            increase.addEventListener('click', this.#onIncreaseClick);
            increase.addEventListener('pointerdown', this.#onStepperPointerDown);
            decrease.addEventListener('click', this.#onDecreaseClick);
            decrease.addEventListener('pointerdown', this.#onStepperPointerDown);
            this.replaceChildren(content);
        }

        /**
         * Steps the box by its own step, within its min and max, and reports it like typing.
         *
         * @param {number} direction - 1 to increase, -1 to decrease.
         *
         * @remarks
         * Uses the box's native stepUp/stepDown, so step, min, and max behave exactly as the
         * browser's own spinner did (an empty box steps from 0, or from min when 0 is out of range).
         */
        #step(direction) {
            const input = this.input;

            // A missing or read-only box cannot change, so a click does nothing.
            if (!input || input.readOnly || input.disabled) {
                return;
            }

            // Step with the browser's own rules; a box whose step is 'any' cannot step and stays as it is.
            try {
                if (direction > 0) {
                    input.stepUp();
                } else {
                    input.stepDown();
                }
            } catch {
                return;
            }

            // Report the stepped value through the same event as typing, so the owner saves it.
            input.dispatchEvent(new Event('input', { bubbles: true }));
        }
    }

    if (!customElements.get(COMPONENT_NAME)) {
        customElements.define(COMPONENT_NAME, G4NumberInput);
    }
})();
