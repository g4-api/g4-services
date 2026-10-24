/*
 * G4(TM) expand editor component: <g4-expand-editor>.
 *
 * A large editor for the text of a small text box: opens in a <g4-modal>, numbers its lines with
 * <g4-line-numbers>, never wraps, and reports every edit so the owner can keep its own box in step.
 * Ported from the G4 Engine Client extension, with a JSON mode (Check & Format plus an error line)
 * and a read-only mode built in, so every canvas owner gets the same tools. Outside JSON mode the
 * header has a Markdown reference button, as the Engine Client's Markdown editors do.
 *
 * Contract:
 * - Attributes: editor-title (modal title), test-id (prefix of every part's data-test-id; the modal
 *   uses the same prefix; default 'expand-editor').
 * - Properties: value (the text), error (the current JSON error; '' when valid or not in JSON mode).
 * - Methods: open(options) shows the editor; setText(text) replaces the text without reporting it;
 *   setError(message) fills the error line; setJsonMode(isJson) turns the JSON tools on or off;
 *   close() closes the modal.
 * - Events: g4-input { value } on every edit, including Format.
 *
 * In JSON mode the text is checked on every edit, but an invalid text is still reported: the owner
 * decides whether it can be used.
 */
(() => {
    'use strict';

    // Component name: the custom element tag and the template id prefix.
    const COMPONENT_NAME = 'g4-expand-editor';

    // Default test id prefix, matching the editor's role on every page.
    const DEFAULT_TEST_ID = 'expand-editor';

    // Guidance appended to a JSON error while typing.
    const EDIT_GUIDANCE = 'Correct the JSON text.';

    // Guidance appended to a JSON error when Format cannot parse the text.
    const FORMAT_GUIDANCE = 'Correct the JSON text before formatting.';

    /**
     * Resolves a readable message from any caught value without default object stringification.
     *
     * @param {unknown} error - Value caught in a catch clause.
     * @returns {string} Readable message.
     */
    const resolveErrorMessage = (error) => {
        if (error instanceof Error) {
            return error.message;
        }

        return typeof error === 'string' ? error : 'Unknown error';
    };

    /**
     * Tests JSON text and describes the problem.
     *
     * @remarks
     * Compute-only. Empty text is valid: the owner decides what an empty value means.
     *
     * @param {string} text - JSON text.
     * @param {string} guidance - Sentence appended to the error.
     * @returns {string} '' when the text is valid; otherwise the error line.
     */
    const getJsonError = (text, guidance) => {
        if (text.trim() === '') {
            return '';
        }

        try {
            JSON.parse(text);
        } catch (error) {
            return `Invalid JSON - ${resolveErrorMessage(error)}. ${guidance}`;
        }

        return '';
    };

    /**
     * Expanded text editor element.
     */
    class G4ExpandEditor extends HTMLElement {
        // Current JSON error ('' when valid or not in JSON mode).
        #error = '';

        // The Format header button, created when the editor opens.
        #formatButton = null;

        // Whether the JSON tools are on.
        #isJson = false;

        // Whether the text can be edited.
        #isReadonly = false;

        // Whether the template has been rendered into this element.
        #isRendered = false;

        // The Markdown reference header button, created when the editor opens.
        #markdownButton = null;

        // The modal that holds the editor while it is open.
        #modal = null;

        /**
         * Formats the text from the header button.
         */
        #onFormatClick = () => {
            this.#formatText();
        };

        /**
         * Opens the Markdown reference from the header button.
         */
        #onMarkdownClick = () => {
            this.#showReference();
        };

        /**
         * Checks the text and reports every edit to the owner.
         *
         * @param {Event} event - Input event of the editor's text area.
         */
        #onTextareaInput = (event) => {
            this.#updateError(EDIT_GUIDANCE);
            this.#dispatchInput(event.target.value);
        };

        /**
         * The current JSON error; '' when the text is valid or the editor is not in JSON mode.
         *
         * @returns {string} Error text.
         */
        get error() {
            return this.#error;
        }

        /**
         * The editor text.
         *
         * @returns {string} Text.
         */
        get value() {
            return this.#getPart('textarea')?.value ?? '';
        }

        /**
         * Closes the editor's modal.
         */
        close() {
            this.#modal?.close();
        }

        /**
         * Renders the editor once, when the element is first attached.
         */
        connectedCallback() {
            // An element moved in the page keeps its rendered parts and listeners.
            if (this.#isRendered) {
                return;
            }

            this.#isRendered = true;

            // Build the editor and report every edit to the owner.
            this.replaceChildren(this.#newContent());
            this.#getPart('textarea').addEventListener('input', this.#onTextareaInput);
        }

        /**
         * Opens the editor over the page.
         *
         * @param {object} options - Open options.
         * @param {string} options.text - Text to edit.
         * @param {number} [options.selectionStart=0] - Caret or selection start to restore.
         * @param {number} [options.selectionEnd=0] - Selection end to restore.
         * @param {HTMLElement} [options.focusReturnElement] - Element that gets focus back on close.
         * @param {HTMLElement[]} [options.tools=[]] - Elements for the modal header, after Format and the
         *   Markdown reference.
         * @param {boolean} [options.isJson=false] - Whether the JSON tools start on.
         * @param {boolean} [options.isReadonly=false] - Whether the text is read-only.
         * @param {string} [options.placeholder=''] - Text shown while the editor is empty.
         */
        open(options) {
            const {
                focusReturnElement = null,
                isJson = false,
                isReadonly = false,
                placeholder = '',
                selectionEnd = 0,
                selectionStart = 0,
                text,
                tools = []
            } = options;
            const modal = document.createElement('g4-modal');
            const testId = this.getAttribute('test-id') || DEFAULT_TEST_ID;

            // The modal carries the title, the header tools, and this editor as its content.
            modal.setAttribute('modal-title', this.getAttribute('editor-title') ?? '');
            modal.setAttribute('size', 'editor');
            modal.setAttribute('test-id', testId);

            // Format and the Markdown reference come first in the header; the JSON mode picks which shows.
            this.#formatButton = this.#newHeaderButton('-format', this.#onFormatClick);
            this.#markdownButton = this.#newHeaderButton('-markdown', this.#onMarkdownClick);
            this.#isReadonly = isReadonly;

            [this.#formatButton, this.#markdownButton, ...tools].forEach((tool) => {
                tool.dataset.slot = 'tools';
                modal.append(tool);
            });

            modal.append(this);
            modal.show(focusReturnElement);
            this.#modal = modal;

            // The editor is rendered now: fill it, apply the modes, and open where the caret was.
            const textarea = this.#getPart('textarea');

            textarea.placeholder = placeholder;
            textarea.readOnly = isReadonly;
            textarea.value = text;
            this.setJsonMode(isJson);
            this.querySelector('g4-line-numbers').refresh();
            textarea.focus();
            textarea.setSelectionRange(selectionStart, selectionEnd);
        }

        /**
         * Shows an error under the text; '' clears it.
         *
         * @param {string} message - Error text.
         */
        setError(message) {
            this.#error = message;

            const error = this.#getPart('error');

            if (error) {
                error.textContent = message;
            }
        }

        /**
         * Turns the JSON tools on or off, for example when the owner's text changed representation.
         *
         * @param {boolean} isJson - Whether the text is JSON.
         */
        setJsonMode(isJson) {
            this.#isJson = isJson;

            // Format is offered only for JSON that can be edited, the Markdown reference only for other
            // text; the error line follows the mode.
            if (this.#formatButton) {
                this.#formatButton.hidden = !isJson || this.#isReadonly;
            }

            if (this.#markdownButton) {
                this.#markdownButton.hidden = isJson;
            }

            this.#updateError(EDIT_GUIDANCE);
        }

        /**
         * Replaces the text without reporting it, for example after the owner converted it.
         *
         * @param {string} text - New text.
         */
        setText(text) {
            const textarea = this.#getPart('textarea');

            if (!textarea) {
                return;
            }

            // Show the new text with its line numbers and its JSON check.
            textarea.value = text;
            this.querySelector('g4-line-numbers').refresh();
            this.#updateError(EDIT_GUIDANCE);
        }

        /**
         * Reports an edit to the owner.
         *
         * @param {string} value - The editor text.
         */
        #dispatchInput(value) {
            this.dispatchEvent(new CustomEvent('g4-input', { detail: { value } }));
        }

        /**
         * Pretty-prints valid JSON (4-space indent) and reports it like an edit. Invalid text is left
         * as it is and the error says to fix it first.
         */
        #formatText() {
            const text = this.value;
            const error = getJsonError(text, FORMAT_GUIDANCE);

            // Invalid text stays as typed, with an error that says to fix it first.
            if (error !== '') {
                this.setError(error);
                return;
            }

            // Valid text is pretty-printed, shown, and reported like an edit.
            const formatted = text.trim() === '' ? '' : JSON.stringify(JSON.parse(text), null, 4);

            this.setText(formatted);
            this.#getPart('textarea').focus();
            this.#dispatchInput(formatted);
        }

        /**
         * Returns one rendered part.
         *
         * @param {string} name - Part name (data-part).
         * @returns {HTMLElement | null} The part.
         */
        #getPart(name) {
            return this.querySelector(`[data-part="${name}"]`);
        }

        /**
         * Clones the template and prefixes every part's test id with this element's test-id.
         *
         * @param {string} [templateSuffix=''] - Template id suffix ('' for the main template).
         * @returns {DocumentFragment} The markup.
         */
        #newContent(templateSuffix = '') {
            const template = document.getElementById(`${COMPONENT_NAME}${templateSuffix}-template`);
            const content = template.content.cloneNode(true);
            const prefix = this.getAttribute('test-id') || DEFAULT_TEST_ID;

            // Every part gets a unique, descriptive test id under this element's prefix.
            content.querySelectorAll('[data-test-id]').forEach((element) => {
                element.dataset.testId = `${prefix}-${element.dataset.testId}`;
            });

            return content;
        }

        /**
         * Creates one of the editor's own header buttons.
         *
         * @param {string} templateSuffix - Template id suffix of the button ('-format' or '-markdown').
         * @param {Function} onClick - Click listener.
         * @returns {HTMLButtonElement} The button, hidden until the JSON mode decides whether it shows.
         */
        #newHeaderButton(templateSuffix, onClick) {
            const button = this.#newContent(templateSuffix).firstElementChild;

            button.hidden = true;
            button.addEventListener('click', onClick);

            return button;
        }

        /**
         * Opens the static Markdown reference in its own modal over the editor.
         *
         * @remarks
         * Escape or the corner button closes only the reference, and focus goes back to the
         * Markdown button, so the user returns to the editor where they were.
         */
        #showReference() {
            const modal = document.createElement('g4-modal');
            const testId = this.getAttribute('test-id') || DEFAULT_TEST_ID;

            // The reference stacks over the editor's modal.
            modal.setAttribute('modal-title', 'Markdown reference');
            modal.setAttribute('size', 'reference');
            modal.setAttribute('test-id', `${testId}-markdown-reference`);
            modal.append(this.#newContent('-reference'));
            modal.show(this.#markdownButton);

            // Focus the Close button, so Escape or Enter closes the reference at once.
            modal.querySelector('[data-part="close"]')?.focus();
        }

        /**
         * Re-checks the text in JSON mode, and clears the error line otherwise.
         *
         * @param {string} guidance - Sentence appended to an error.
         */
        #updateError(guidance) {
            this.setError(this.#isJson ? getJsonError(this.value, guidance) : '');
        }
    }

    if (!customElements.get(COMPONENT_NAME)) {
        customElements.define(COMPONENT_NAME, G4ExpandEditor);
    }
})();
