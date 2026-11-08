/*
 * G4(TM) toolboxes filter component: <g4-toolboxes-filter>.
 *
 * A filter button next to the designer toolbox search box. It opens a panel that lists the toolboxes
 * with their tools, each with the designer's slider switch; every change saves the browser cookie and
 * the toolbox redraws at once, the same way it reacts to the search text. A header switch shows or
 * hides everything and reports a mixed state. Entries the project manifest sets (VS Code) are shown
 * locked, because the manifest wins over the cookie for each entry it lists.
 *
 * The markup is built in code rather than from a template file, because every row comes from the
 * toolbox data; this keeps the component independent of the template loader. The switches reuse the
 * designer's Base64 switch markup (sqd-base64-toggle), so every theme already styles them. The list
 * scrolls natively behind the designer's shared scroll bar (G4Scrollbars), which mirrors and drives it.
 *
 * Contract:
 * - Attributes: test-id (prefix of every part's data-test-id; default 'toolboxes-filter').
 * - Properties: toolboxes ([{ name, tools: [{ label, manifest }] }]; the Containers group is left out).
 * - Depends on: js/toolboxes-filter.js (globalThis.G4ToolboxesFilter) and components/g4-scrollbar
 *   (globalThis.G4Scrollbars).
 */
(() => {
    'use strict';

    // Component name: the custom element tag and the CSS block name.
    const COMPONENT_NAME = 'g4-toolboxes-filter';

    // Default test id prefix.
    const DEFAULT_TEST_ID = 'toolboxes-filter';

    // Message shown when the cookie would exceed the browser's size limit.
    const COOKIE_TOO_LARGE_MESSAGE = 'The filter is too large to save in the browser; turn fewer tools on or off.';

    // Tooltip of an entry the project manifest decides.
    const MANIFEST_LOCK_TITLE = 'Set by the project manifest';

    // SVG path of the filter (funnel) icon.
    const FILTER_ICON_PATH = 'M96 128C83.1 128 71.4 135.8 66.4 147.8C61.4 159.8 64.2 173.5 73.4 182.6L256 365.3L256 '
        + '480C256 488.5 259.4 496.6 265.4 502.6L329.4 566.6C338.6 575.8 352.3 578.5 364.3 573.5C376.3 568.5 384 '
        + '556.9 384 544L384 365.3L566.6 182.7C575.8 173.5 578.5 159.8 573.5 147.8C568.5 135.8 556.9 128 544 128L96 '
        + '128z';

    // SVG path of the expand arrow, pointing right; the CSS turns it down while the toolbox is expanded.
    const ARROW_ICON_PATH = 'M441.3 299.8C451.5 312.4 450.8 330.9 439.1 342.6L311.1 470.6C301.9 479.8 288.2 482.5 '
        + '276.2 477.5C264.2 472.5 256.5 460.9 256.5 448L256.5 192C256.5 179.1 264.3 167.4 276.3 162.4C288.3 157.4 '
        + '302 160.2 311.2 169.3L439.2 297.3L441.4 299.7z';

    // Markup of the expand arrow; it takes the text color of the toolbox name.
    const ARROW_ICON_MARKUP = `<svg class="${COMPONENT_NAME}__arrow" xmlns="http://www.w3.org/2000/svg" `
        + `viewBox="0 0 640 640" aria-hidden="true"><path fill="currentColor" d="${ARROW_ICON_PATH}"/></svg>`;

    // Group switch state (All, or one toolbox) when some of its tools are shown and some are hidden.
    const SWITCH_STATE_MIXED = 'mixed';

    // Group switch state when none of its tools is shown.
    const SWITCH_STATE_OFF = 'off';

    // Group switch state when every one of its tools is shown.
    const SWITCH_STATE_ON = 'on';

    /**
     * Toolboxes filter element.
     *
     * @remarks
     * Owns its button, its panel, the expanded-toolbox set, and the document listeners it installs while
     * connected. It reads and writes the filter only through G4ToolboxesFilter, so the toolbox patch and
     * the panel always agree.
     */
    class G4ToolboxesFilter extends HTMLElement {
        // Toolboxes and their tools, as the designer builds them.
        #toolboxes = [];

        // Toolbox names the user expanded; kept across redraws so the list does not jump.
        #expandedToolboxes = new Set();

        // Panel parts, created once when the element is first attached.
        #panel = null;

        // Header master switch input, which shows or hides everything.
        #masterInput = null;

        // Header line that counts the shown toolboxes and the manifest entries.
        #summary = null;

        // Header line for save failures.
        #status = null;

        // List that holds the toolbox rows.
        #list = null;

        // Drawn rows: each toolbox switch input and count, with its drawn tool switch inputs. A filter change
        // updates these in place, so the switches slide like the All switch instead of being redrawn.
        #rows = [];

        // Whether the button and panel are built.
        #isBuilt = false;

        // Controller of the shared scroll bar over the list.
        #scrollbars = null;

        /**
         * Updates the open panel in place after the filter changes, here or elsewhere (for example a manifest
         * update), so its switches animate.
         */
        #onFilterChanged = () => {
            if (this.#panel && !this.#panel.hidden) {
                this.#updateRows();
            }
        };

        /**
         * Closes the panel when a click lands outside the component.
         *
         * @remarks
         * Uses the event path captured when the click was dispatched: a switch click redraws the panel and
         * detaches the clicked switch, so testing the target's current ancestors would close the panel.
         *
         * @param {MouseEvent} event - The document click.
         */
        #onDocumentClick = (event) => {
            if (!event.composedPath().includes(this)) {
                this.#setPanelOpen(false);
            }
        };

        /**
         * Closes the panel on Escape.
         *
         * @param {KeyboardEvent} event - The document key press.
         */
        #onDocumentKeyDown = (event) => {
            if (event.key === 'Escape') {
                this.#setPanelOpen(false);
            }
        };

        /**
         * Shows everything from a mixed or off state, or hides every toolbox from the on state.
         *
         * @remarks
         * Only the user's cookie entries change; manifest entries keep deciding what they list.
         */
        #onMasterChange = () => {
            // Read the state the panel showed before the click, because the input has already flipped.
            const filterApi = globalThis.G4ToolboxesFilter;
            const { state } = this.#getMasterState(filterApi.getToolboxesFilter());

            // Hide every toolbox from the on state; otherwise clear the user's entries so everything shows.
            const hiddenToolboxes = this.#toolboxes.map((toolbox) => ({ name: toolbox.name, enabled: false }));
            const filter = state === SWITCH_STATE_ON
                ? { toolboxes: hiddenToolboxes, tools: [] }
                : { toolboxes: [], tools: [] };

            this.#saveCookie(filter);
        };

        /**
         * Gets the toolboxes the panel lists.
         *
         * @returns {Array<{ name: string, tools: Array<{ label: string, manifest: object }> }>} The toolboxes.
         */
        get toolboxes() {
            return this.#toolboxes;
        }

        /**
         * Sets the toolboxes the panel lists, and redraws an open panel.
         *
         * @param {Array<{ name: string, tools: Array<{ label: string, manifest: object }> }>} value - The toolboxes.
         */
        set toolboxes(value) {
            this.#toolboxes = Array.isArray(value) ? value : [];

            // New toolboxes change the rows themselves, so an open panel is rebuilt rather than updated.
            if (this.#panel && !this.#panel.hidden) {
                this.#showRows();
            }
        }

        /**
         * Builds the button and panel once, and installs the document listeners while attached.
         */
        connectedCallback() {
            // Build the markup once; a reattached element keeps it.
            if (!this.#isBuilt) {
                this.#showContent();
            }

            // Listen for filter changes and for clicks and keys that close the panel, while attached.
            document.addEventListener(globalThis.G4ToolboxesFilter.changedEventName, this.#onFilterChanged);
            document.addEventListener('click', this.#onDocumentClick);
            document.addEventListener('keydown', this.#onDocumentKeyDown);
        }

        /**
         * Removes the document listeners when the element is detached.
         */
        disconnectedCallback() {
            document.removeEventListener(globalThis.G4ToolboxesFilter.changedEventName, this.#onFilterChanged);
            document.removeEventListener('click', this.#onDocumentClick);
            document.removeEventListener('keydown', this.#onDocumentKeyDown);
        }

        /**
         * Gets the master switch state and the header counts for a filter.
         *
         * @remarks
         * Compute-only over the listed toolboxes. A toolbox counts as shown when at least one of its tools
         * shows, which is exactly when the designer toolbox lists it.
         *
         * @param {object} filter - The merged filter.
         * @returns {{ state: string, shownCount: number, toolboxCount: number }} The state and the counts.
         */
        #getMasterState(filter) {
            // Count the shown tools of every toolbox once.
            let shownToolCount = 0;
            let toolCount = 0;
            let shownCount = 0;

            for (const toolbox of this.#toolboxes) {
                const shownTools = this.#getToolboxState(filter, toolbox).shownToolCount;

                shownToolCount += shownTools;
                toolCount += toolbox.tools.length;
                shownCount += shownTools > 0 ? 1 : 0;
            }

            // Everything shown is on, nothing shown is off, and anything between is mixed.
            const state = this.#getSwitchState(shownToolCount, toolCount);

            return { shownCount, state, toolboxCount: this.#toolboxes.length };
        }

        /**
         * Gets the state of a group switch from how many of its tools are shown.
         *
         * @remarks
         * Compute-only; the All switch and every toolbox switch use the same rule.
         *
         * @param {number} shownToolCount - The tools of the group that are shown.
         * @param {number} toolCount - All tools of the group.
         * @returns {string} SWITCH_STATE_ON, SWITCH_STATE_OFF, or SWITCH_STATE_MIXED.
         */
        #getSwitchState(shownToolCount, toolCount) {
            if (shownToolCount === toolCount) {
                return SWITCH_STATE_ON;
            }

            return shownToolCount === 0
                ? SWITCH_STATE_OFF
                : SWITCH_STATE_MIXED;
        }

        /**
         * Gets a test id under this element's prefix, made of lowercase words and dashes.
         *
         * @param {string} name - The part name, which may hold any characters.
         * @returns {string} The prefixed test id.
         */
        #getTestId(name) {
            const prefix = this.getAttribute('test-id') || DEFAULT_TEST_ID;
            const part = name.toLowerCase().replaceAll(/[^a-z0-9]+/g, '-').replaceAll(/(^-)|(-$)/g, '');

            return `${prefix}-${part}`;
        }

        /**
         * Gets how many tools of one toolbox are shown, and the toolbox switch state that follows.
         *
         * @remarks
         * Compute-only. A tool counts as shown when the designer toolbox lists it in this toolbox.
         *
         * @param {object} filter - The merged filter.
         * @param {{ name: string, tools: Array<{ manifest: object }> }} toolbox - The toolbox.
         * @returns {{ shownToolCount: number, state: string }} The shown tool count and the switch state.
         */
        #getToolboxState(filter, toolbox) {
            const filterApi = globalThis.G4ToolboxesFilter;
            const shownToolCount = toolbox.tools.filter((tool) => filterApi.testToolboxesFilterTool({
                filter,
                groupName: toolbox.name,
                manifest: tool.manifest
            })).length;

            return { shownToolCount, state: this.#getSwitchState(shownToolCount, toolbox.tools.length) };
        }

        /**
         * Creates a designer slider switch (the Base64 switch markup), optionally with a text label.
         *
         * @param {{ isChecked: boolean, isLocked: boolean, label: string, isTextShown: boolean, testId: string,
         *     onChange: Function }} options - The state, the accessible name, whether the name shows next to
         *     the switch, the test id, and the change callback.
         * @returns {HTMLLabelElement} The switch.
         */
        #newSwitch(options) {
            const { isChecked, isLocked, isTextShown, label, onChange, testId } = options;

            // Report the new state, so the owner saves it and the toolbox redraws.
            const onInputChange = () => {
                onChange(input.checked);
            };

            // Build the label, the hidden native checkbox, and the drawn track the theme styles.
            const switchElement = document.createElement('label');
            const input = document.createElement('input');
            const track = document.createElement('span');

            switchElement.className = `sqd-base64-toggle ${COMPONENT_NAME}__switch`;
            switchElement.title = isLocked ? MANIFEST_LOCK_TITLE : '';
            input.className = 'sqd-base64-toggle__input';
            input.type = 'checkbox';
            input.checked = isChecked;
            input.disabled = isLocked;
            input.setAttribute('aria-label', label);
            input.dataset.testId = `${testId}-switch`;
            track.className = 'sqd-base64-toggle__switch';
            track.setAttribute('aria-hidden', 'true');
            switchElement.append(input, track);

            // Show the name inside the label when asked, so clicking it also flips the switch.
            if (isTextShown) {
                const text = document.createElement('span');

                text.className = `${COMPONENT_NAME}__name`;
                text.textContent = label;
                switchElement.append(text);
            }

            // Listen for the user's flip of the switch.
            input.addEventListener('change', onInputChange);

            return switchElement;
        }

        /**
         * Creates one toolbox row with its switch, its expand button, its count, and, when expanded, its tools.
         *
         * @param {{ filter: object, toolbox: { name: string, tools: Array<object> } }} options - The merged filter
         *     and the toolbox.
         * @returns {HTMLDivElement} The toolbox group.
         */
        #newToolboxGroup(options) {
            const { filter, toolbox } = options;
            const filterApi = globalThis.G4ToolboxesFilter;
            const testId = this.#getTestId(`toolbox-${toolbox.name}`);
            const isExpanded = this.#expandedToolboxes.has(toolbox.name);

            // Expand or collapse the tools; the set keeps the choice across redraws.
            const onExpandClick = () => {
                if (isExpanded) {
                    this.#expandedToolboxes.delete(toolbox.name);
                } else {
                    this.#expandedToolboxes.add(toolbox.name);
                }

                this.#showRows();
            };

            // Count the tools the designer toolbox shows for this toolbox, and derive its switch state.
            const { shownToolCount, state } = this.#getToolboxState(filter, toolbox);

            // Turn every tool of the toolbox off from the on state, otherwise on, as the All switch does; the
            // state is read again, because the row is updated in place after it is drawn.
            const onToolboxChange = () => {
                const currentFilter = globalThis.G4ToolboxesFilter.getToolboxesFilter();
                const currentState = this.#getToolboxState(currentFilter, toolbox).state;

                this.#saveToolbox({ isEnabled: currentState !== SWITCH_STATE_ON, toolbox });
            };

            // Build the row: switch, expand button with the arrow and the name, and the shown/total count.
            const group = document.createElement('div');
            const row = document.createElement('div');
            const expandButton = document.createElement('button');
            const name = document.createElement('span');
            const count = document.createElement('span');

            group.className = `${COMPONENT_NAME}__group`;
            group.dataset.testId = testId;
            row.className = `${COMPONENT_NAME}__toolbox`;
            expandButton.type = 'button';
            expandButton.className = `${COMPONENT_NAME}__expand`;
            expandButton.setAttribute('aria-expanded', `${isExpanded}`);
            expandButton.dataset.testId = `${testId}-expand-button`;
            expandButton.innerHTML = ARROW_ICON_MARKUP;
            name.textContent = toolbox.name;
            expandButton.append(name);
            count.className = `${COMPONENT_NAME}__count`;
            count.textContent = `${shownToolCount} / ${toolbox.tools.length}`;
            count.dataset.testId = `${testId}-count`;

            const toolboxSwitch = this.#newSwitch({
                isChecked: state === SWITCH_STATE_ON,
                isLocked: Boolean(filterApi.getToolboxesFilterManifestEntry('toolboxes', toolbox.name)),
                isTextShown: false,
                label: toolbox.name,
                onChange: onToolboxChange,
                testId
            });

            // Record the row, so a filter change can update its switch and count in place.
            const record = { count, input: toolboxSwitch.querySelector('input'), toolbox, tools: [] };

            this.#rows.push(record);
            this.#setSwitchState(record.input, state);

            // Listen for expand and collapse clicks on the name.
            expandButton.addEventListener('click', onExpandClick);

            row.append(toolboxSwitch, expandButton, count);
            group.append(row);

            // Add the tool rows only while expanded, so a collapsed list stays short and quick to draw.
            if (isExpanded) {
                group.append(this.#newToolRows({ filter, record }));
            }

            return group;
        }

        /**
         * Creates the tool rows of one expanded toolbox, and records their switches on the toolbox row.
         *
         * @param {{ filter: object, record: { toolbox: object, tools: Array<object> } }} options - The merged
         *     filter and the toolbox row record.
         * @returns {HTMLDivElement} The tools list.
         */
        #newToolRows(options) {
            const { filter, record } = options;
            const { toolbox } = record;
            const filterApi = globalThis.G4ToolboxesFilter;
            const tools = document.createElement('div');

            tools.className = `${COMPONENT_NAME}__tools`;

            for (const tool of toolbox.tools) {
                const qualifiedName = filterApi.getToolboxesFilterQualifiedName(tool.manifest);

                // Show each tool as the toolbox shows it; a tool entry wins over its toolbox entry.
                const isToolShown = filterApi.testToolboxesFilterTool({
                    filter,
                    groupName: toolbox.name,
                    manifest: tool.manifest
                });

                // Save the tool entry by its qualified name, so it names this plugin only.
                const onToolChange = (isChecked) => {
                    this.#saveEntry({ isEnabled: isChecked, listName: 'tools', name: qualifiedName });
                };

                const toolSwitch = this.#newSwitch({
                    isChecked: isToolShown,
                    isLocked: this.#testToolLocked(tool),
                    isTextShown: true,
                    label: tool.label,
                    onChange: onToolChange,
                    testId: this.#getTestId(`tool-${toolbox.name}-${qualifiedName}`)
                });

                // Record the tool switch, so a filter change can update it in place.
                record.tools.push({ input: toolSwitch.querySelector('input'), tool });
                tools.append(toolSwitch);
            }

            return tools;
        }

        /**
         * Saves a filter in the cookie, and shows why when it cannot be saved.
         *
         * @param {object} filter - The user's filter to save.
         */
        #saveCookie(filter) {
            // Save and redraw; a refused save keeps the old filter and redraws the switches back to it.
            const isSaved = globalThis.G4ToolboxesFilter.saveToolboxesFilterCookie(filter);

            this.#status.textContent = isSaved ? '' : COOKIE_TOO_LARGE_MESSAGE;

            if (!isSaved) {
                this.#updateRows();
            }
        }

        /**
         * Saves one toolbox or tool entry in the cookie, replacing the earlier entry for the same name.
         *
         * @param {{ listName: 'toolboxes' | 'tools', name: string, isEnabled: boolean }} options - The list, the
         *     entry name, and its new state.
         */
        #saveEntry(options) {
            const { isEnabled, listName, name } = options;

            // Replace the earlier entry for the name, so the cookie keeps one entry per toolbox or tool.
            const filter = globalThis.G4ToolboxesFilter.readToolboxesFilterCookie();
            const lowerCaseName = name.toLowerCase();
            const entries = filter[listName].filter((entry) => entry.name.toLowerCase() !== lowerCaseName);

            filter[listName] = [...entries, { name, enabled: isEnabled }];

            this.#saveCookie(filter);
        }

        /**
         * Saves one toolbox as fully shown or fully hidden, the way the All switch does for every toolbox.
         *
         * @remarks
         * The toolbox entry is replaced, and the user's tool entries for the toolbox's tools are removed, so
         * every tool under it follows the toolbox switch. Manifest entries are not touched.
         *
         * @param {{ isEnabled: boolean, toolbox: { name: string, tools: Array<object> } }} options - The new
         *     state and the toolbox.
         */
        #saveToolbox(options) {
            const { isEnabled, toolbox } = options;
            const filterApi = globalThis.G4ToolboxesFilter;

            // Collect the qualified names of the toolbox's tools, whose user entries the toolbox now decides.
            const toolNames = new Set(toolbox.tools.map((tool) =>
                filterApi.getToolboxesFilterQualifiedName(tool.manifest).toLowerCase()));
            const toolboxName = toolbox.name.toLowerCase();

            // Replace the toolbox entry and drop the tool entries, then save; the toolbox redraws at once.
            const filter = filterApi.readToolboxesFilterCookie();
            const toolboxes = filter.toolboxes.filter((entry) => entry.name.toLowerCase() !== toolboxName);
            const tools = filter.tools.filter((entry) => !toolNames.has(entry.name.toLowerCase()));

            this.#saveCookie({ toolboxes: [...toolboxes, { name: toolbox.name, enabled: isEnabled }], tools });
        }

        /**
         * Opens or closes the panel, filling it when it opens.
         *
         * @param {boolean} isOpen - Whether the panel should be open.
         */
        #setPanelOpen(isOpen) {
            // Fill the rows before showing them, so the panel opens with the current state.
            if (isOpen) {
                this.#status.textContent = '';
                this.#showRows();
            }

            this.#panel.hidden = !isOpen;
            this.querySelector(`.${COMPONENT_NAME}__button`).setAttribute('aria-expanded', `${isOpen}`);

            // Size the scroll bar once the open panel has its layout.
            if (isOpen) {
                this.#scrollbars.refresh();
            }
        }

        /**
         * Locks or unlocks a switch the project manifest decides, with the tooltip that says why.
         *
         * @param {HTMLInputElement} input - The switch input.
         * @param {boolean} isLocked - Whether the manifest decides the switch.
         */
        #setSwitchLocked(input, isLocked) {
            input.disabled = isLocked;
            input.closest('label').title = isLocked ? MANIFEST_LOCK_TITLE : '';
        }

        /**
         * Shows a group switch state: on, off, or mixed (thumb halfway, indeterminate for assistive technology).
         *
         * @param {HTMLInputElement} input - The switch input.
         * @param {string} state - SWITCH_STATE_ON, SWITCH_STATE_OFF, or SWITCH_STATE_MIXED.
         */
        #setSwitchState(input, state) {
            const isMixed = state === SWITCH_STATE_MIXED;

            input.checked = state === SWITCH_STATE_ON;
            input.indeterminate = isMixed;
            input.closest('label').classList.toggle('is-mixed', isMixed);
        }

        /**
         * Builds the button and the panel (header with the master switch, then the list), and mounts them.
         */
        #showContent() {
            this.#isBuilt = true;

            // Open a closed panel and close an open one.
            const onButtonClick = () => {
                this.#setPanelOpen(this.#panel.hidden);
            };

            // Build the toggle button with the funnel icon; it looks and reacts like the search box next to it.
            const button = document.createElement('button');

            button.type = 'button';
            button.className = `${COMPONENT_NAME}__button`;
            button.title = 'Filter toolboxes and tools';
            button.setAttribute('aria-label', 'Filter toolboxes and tools');
            button.setAttribute('aria-expanded', 'false');
            button.dataset.testId = this.#getTestId('button');
            button.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 640 640" aria-hidden="true">'
                + `<path fill="currentColor" d="${FILTER_ICON_PATH}"/></svg>`;

            // Build the fixed header: title and master switch, the shown count, and the status line.
            const header = document.createElement('div');
            const titleRow = document.createElement('div');
            const title = document.createElement('span');
            const masterSwitch = this.#newSwitch({
                isChecked: true,
                isLocked: false,
                isTextShown: true,
                label: 'All',
                onChange: this.#onMasterChange,
                testId: this.#getTestId('all')
            });

            header.className = `${COMPONENT_NAME}__header`;
            titleRow.className = `${COMPONENT_NAME}__title-row`;
            title.className = `${COMPONENT_NAME}__title`;
            title.textContent = 'Toolboxes';
            masterSwitch.classList.add(`${COMPONENT_NAME}__master`);
            this.#masterInput = masterSwitch.querySelector('input');

            this.#summary = document.createElement('div');
            this.#summary.className = `${COMPONENT_NAME}__summary`;
            this.#summary.dataset.testId = this.#getTestId('summary');

            this.#status = document.createElement('div');
            this.#status.className = `${COMPONENT_NAME}__status`;
            this.#status.dataset.testId = this.#getTestId('status');

            titleRow.append(title, masterSwitch);
            header.append(titleRow, this.#summary, this.#status);

            // Build the body: the natively scrolling list under the designer's shared scroll bar.
            const body = document.createElement('div');

            body.className = `${COMPONENT_NAME}__body`;

            this.#list = document.createElement('div');
            this.#list.className = `${COMPONENT_NAME}__list`;
            this.#list.dataset.testId = this.#getTestId('list');
            body.append(this.#list);

            this.#scrollbars = globalThis.G4Scrollbars.attach({
                host: body,
                scrollElement: this.#list,
                testId: this.#getTestId('list')
            });

            // Build the hidden panel around the header and the body.
            this.#panel = document.createElement('div');
            this.#panel.className = `${COMPONENT_NAME}__panel`;
            this.#panel.hidden = true;
            this.#panel.dataset.testId = this.#getTestId('panel');
            this.#panel.append(header, body);

            // Listen for the button.
            button.addEventListener('click', onButtonClick);

            // Mount the button and the panel.
            this.replaceChildren(button, this.#panel);
        }

        /**
         * Shows the header state for a filter: the All switch and the summary line.
         *
         * @param {object} filter - The merged filter.
         */
        #showHeader(filter) {
            // Show the All state: on, off, or mixed.
            const { shownCount, state, toolboxCount } = this.#getMasterState(filter);

            this.#setSwitchState(this.#masterInput, state);

            // Count the shown toolboxes, and say how many entries the manifest decides.
            const manifestFilter = globalThis.G4ToolboxesFilter.getToolboxesFilterManifest();
            const manifestEntryCount = manifestFilter.toolboxes.length + manifestFilter.tools.length;
            const manifestText = manifestEntryCount > 0
                ? ` · ${manifestEntryCount} set by the project manifest`
                : '';

            this.#summary.textContent = `${shownCount} of ${toolboxCount} shown${manifestText}`;
        }

        /**
         * Rebuilds the header state and the toolbox list from the current filter.
         *
         * @remarks
         * Used when the rows themselves change: the panel opens, a toolbox expands or collapses, or the
         * toolboxes are replaced. A filter change only updates the rows (#updateRows), so switches animate.
         */
        #showRows() {
            const filter = globalThis.G4ToolboxesFilter.getToolboxesFilter();

            this.#showHeader(filter);

            // Rebuild the rows and their records in one step, so the list never shows a half-built state.
            this.#rows = [];

            const groups = this.#toolboxes.map((toolbox) => this.#newToolboxGroup({ filter, toolbox }));

            this.#list.replaceChildren(...groups);

            // Resize the thumb, because expanding or collapsing a toolbox changes the list height.
            this.#scrollbars.refresh();
        }

        /**
         * Determines whether the project manifest decides a tool, which locks its switch.
         *
         * @param {{ manifest: { key: string } }} tool - The tool.
         * @returns {boolean} `true` when a manifest entry names the tool by its qualified name or its bare key.
         */
        #testToolLocked(tool) {
            const filterApi = globalThis.G4ToolboxesFilter;
            const qualifiedName = filterApi.getToolboxesFilterQualifiedName(tool.manifest);
            const isQualifiedLocked = Boolean(filterApi.getToolboxesFilterManifestEntry('tools', qualifiedName));
            const isBareLocked = Boolean(filterApi.getToolboxesFilterManifestEntry('tools', tool.manifest.key));

            return isQualifiedLocked || isBareLocked;
        }

        /**
         * Updates the header and the drawn rows in place from the current filter, so every switch slides.
         *
         * @remarks
         * Sets the existing inputs and counts only; rows are never replaced here, which is what lets the
         * switch transition run, as it does on the All switch.
         */
        #updateRows() {
            const filterApi = globalThis.G4ToolboxesFilter;
            const filter = filterApi.getToolboxesFilter();

            this.#showHeader(filter);

            for (const record of this.#rows) {
                // Update the toolbox switch, its lock, and its count.
                const { shownToolCount, state } = this.#getToolboxState(filter, record.toolbox);
                const toolboxEntry = filterApi.getToolboxesFilterManifestEntry('toolboxes', record.toolbox.name);

                this.#setSwitchState(record.input, state);
                this.#setSwitchLocked(record.input, Boolean(toolboxEntry));
                record.count.textContent = `${shownToolCount} / ${record.toolbox.tools.length}`;

                // Update each drawn tool switch and its lock.
                for (const toolRecord of record.tools) {
                    toolRecord.input.checked = filterApi.testToolboxesFilterTool({
                        filter,
                        groupName: record.toolbox.name,
                        manifest: toolRecord.tool.manifest
                    });
                    this.#setSwitchLocked(toolRecord.input, this.#testToolLocked(toolRecord.tool));
                }
            }
        }

    }

    if (!customElements.get(COMPONENT_NAME)) {
        customElements.define(COMPONENT_NAME, G4ToolboxesFilter);
    }
})();
