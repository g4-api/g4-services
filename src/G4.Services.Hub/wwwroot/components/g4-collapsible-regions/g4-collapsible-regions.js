/*
 * G4(TM) collapsible regions: a G4 designer plugin (see components/g4-designer-plugins).
 *
 * Lets the user collapse and expand every container (Stage, Job, loops) and switch (conditions) on the
 * canvas by clicking the arrow in the region's top-left corner. A collapsed region draws one compact box
 * in place of its children: the step's icon and name, and a count of what it holds ("7 steps",
 * "2 branches · 5 steps"). Its connectors still join the flow, a hidden step's validation error still
 * shows on the box, and the name still selects the step.
 *
 * Contract:
 * - Uses only what the designer bundle exports: the regionComponentView designer extension (which
 *   draws every container and switch frame), the preferenceStorage setting, and the designer's
 *   public methods and events. It never edits js/canvas.umd.js or js/canvas.js.
 * - Collapsed is a designer preference, kept outside the automation: a collapse never changes the
 *   bot document and is not part of Undo.
 * - Saved state survives a reload, although the designer gives every step a new id on load: it names
 *   each collapsed region by its position (sequence indexes and branch names) and checks its component
 *   type and type when it is read back, so a region that moved or changed outside the designer opens
 *   expanded.
 * - A step that gets selected or focused by code (a running step) first expands the collapsed regions
 *   around it, so it can be shown.
 * - Only one regionComponentView extension can be active; this plugin is that extension.
 */
(() => {
    'use strict';

    // Component name: the CSS block name of the arrow and the collapsed box.
    const COMPONENT_NAME = 'g4-collapsible-regions';

    // Plugin name: the key of the plugin's saved state.
    const PLUGIN_NAME = 'collapsible-regions';

    // Preference key the designer keeps per step (it prefixes the step id) for a collapsed region.
    const COLLAPSED_PREFERENCE_KEY = 'collapsed';

    // Ending of a full preference key ("<step id>:collapsed") the preference storage receives.
    const COLLAPSED_KEY_SUFFIX = `:${COLLAPSED_PREFERENCE_KEY}`;

    // Designer component class names of the regions that collapse; other regions draw as they always do.
    const COLLAPSIBLE_CLASS_NAMES = new Set(['container', 'switch']);

    // Version of the saved state shape; state of another version is ignored.
    const STATE_VERSION = 1;

    // The arrow's top-left corner sits on the region's top-left corner, so its square covers the frame's
    // corner and stays clear of the step's label below.
    const TOGGLE_OFFSET_PIXELS = 0;

    // Size of the arrow's square click area.
    const TOGGLE_SIZE_PIXELS = 16;

    // Size of the arrow icon inside its click area.
    const TOGGLE_ICON_SIZE_PIXELS = 10;

    // Geometry of the collapsed box: the designer's container values for the input and the labels, so a
    // collapsed region lines up with an expanded one.
    const COLLAPSED_VIEW = {
        inputIconSize: 14,
        inputRadius: 4,
        inputSize: 18,
        label: {
            height: 22,
            minWidth: 50,
            paddingX: 10,
            radius: 10
        },
        labelGap: 8,
        paddingBottom: 12,
        paddingTop: 20,
        paddingX: 20
    };

    // SVG path of the right arrow, shown on a collapsed region.
    const ARROW_RIGHT_ICON_PATH = 'M441.3 299.8C451.5 312.4 450.8 330.9 439.1 342.6L311.1 470.6C301.9 479.8 288.2 482.5 '
        + '276.2 477.5C264.2 472.5 256.5 460.9 256.5 448L256.5 192C256.5 179.1 264.3 167.4 276.3 162.4C288.3 157.4 '
        + '302 160.2 311.2 169.3L439.2 297.3L441.4 299.7z';

    // SVG path of the down arrow, shown on an expanded region.
    const ARROW_DOWN_ICON_PATH = 'M300.3 440.8C312.9 451 331.4 450.3 343.1 438.6L471.1 310.6C480.3 301.4 483 287.7 '
        + '478 275.7C473 263.7 461.4 256 448.5 256L192.5 256C179.6 256 167.9 263.8 162.9 275.8C157.9 287.8 160.7 '
        + '301.5 169.9 310.6L297.9 438.6L300.3 440.8z';

    /**
     * Keeps the collapsed regions of one designer and saves them for the open automation.
     *
     * @remarks
     * The designer reads and writes collapsed through this object, its preference storage. The object
     * also follows the designer's events to save the state after each change, restores it when the
     * designer gets a new definition (an import, Undo), and expands the regions around a step that code
     * selects. Owns explicit per-designer state rather than module state, since a page can create more
     * than one designer.
     */
    class G4CollapsibleRegionsSession {
        // Ids of the collapsed regions in the current definition.
        #collapsedStepIds = new Set();

        // The designer, once it was created; preference reads before then see no saved state.
        #designer = null;

        // Whether the designer drew its first canvas, so a redraw is possible.
        #isReady = false;

        // The definition the collapsed ids belong to; another one means the state must be read again.
        #knownDefinition = null;

        // Preferences other extensions keep, served from the storage the configuration had before.
        #otherPreferences;

        // Title and state text last saved or read, so an unchanged state is not saved again.
        #savedStateText = null;

        // Access to the plugin's saved state, from the plugin host.
        #stateContext = null;

        /**
         * Creates the session of one designer.
         *
         * @param {{ getItem: Function, setItem: Function } | undefined} previousStorage - The preference
         *     storage the configuration had, which keeps serving every other preference; undefined keeps
         *     them in memory.
         */
        constructor(previousStorage) {
            this.#otherPreferences = previousStorage ?? newMemoryPreferenceStorage();
        }

        /**
         * Connects the session to its designer once the designer is created.
         *
         * @remarks
         * Reads the saved state for the start definition before the first canvas is drawn, follows the
         * designer's events, and wraps two of this designer instance's methods (not the bundle's
         * class), so a step selected or focused by code first expands the regions that hide it.
         *
         * @param {Object} designer - The created designer.
         * @param {{ readState: Function, saveState: Function }} stateContext - The plugin's saved state.
         */
        attach(designer, stateContext) {
            // Keep the designer and the state access; every later read and save goes through them.
            this.#designer = designer;
            this.#stateContext = stateContext;

            // Read the saved state for the start definition; the first canvas is drawn after this.
            this.#syncDefinition();

            // Follow the designer: redraws become possible once it is ready, and every change is saved.
            const onReady = () => {
                this.#isReady = true;
            };
            const onStateChanged = () => {
                this.#saveState();
            };

            designer.onReady.subscribe(onReady);
            designer.onDefinitionChanged.subscribe(onStateChanged);
            designer.onPreferencesChanged.subscribe(onStateChanged);

            // Expand the regions around a step that code selects or focuses, before the designer looks
            // for its view; the designer's own events arrive too late for that.
            const selectStepById = designer.selectStepById.bind(designer);
            const moveViewportToStep = designer.moveViewportToStep.bind(designer);

            designer.selectStepById = (stepId) => {
                this.#expandParents(stepId);
                selectStepById(stepId);
            };
            designer.moveViewportToStep = (stepId) => {
                this.#expandParents(stepId);
                moveViewportToStep(stepId);
            };
        }

        /**
         * Returns a preference the designer reads while it draws a step.
         *
         * @param {string} key - The full preference key ("<step id>:<key>").
         * @returns {string | null} The preference value; null when it is not set.
         */
        getItem(key) {
            // A collapsed preference answers from the collapsed ids; every other key from the storage before.
            if (!key.endsWith(COLLAPSED_KEY_SUFFIX)) {
                return this.#otherPreferences.getItem(key);
            }

            // A new definition (an import) brings new ids, so the saved state is read for it first.
            this.#syncDefinition();

            // Answer for the step the key names; an expanded region has no value, as an unset preference.
            const stepId = key.slice(0, -COLLAPSED_KEY_SUFFIX.length);

            return this.#collapsedStepIds.has(stepId) ? 'true' : null;
        }

        /**
         * Applies saved state that arrived after the designer was created (the VS Code designer).
         *
         * @remarks
         * The plugin host's state storage already holds the state; this reads it for the current
         * definition and redraws the canvas.
         */
        receiveState() {
            // Forget the definition the ids belong to, so the arrived state is read for it again, then redraw.
            this.#knownDefinition = null;
            this.#syncDefinition();
            this.#render();
        }

        /**
         * Keeps a preference the designer sets, for example when the user clicks a region's arrow.
         *
         * @param {string} key - The full preference key ("<step id>:<key>").
         * @param {string} value - The preference value.
         */
        setItem(key, value) {
            // Every preference other than collapsed belongs to the storage the configuration had.
            if (!key.endsWith(COLLAPSED_KEY_SUFFIX)) {
                this.#otherPreferences.setItem(key, value);

                return;
            }

            // Record the region as collapsed or expanded; the designer redraws and reports the change.
            const stepId = key.slice(0, -COLLAPSED_KEY_SUFFIX.length);

            if (value === 'true') {
                this.#collapsedStepIds.add(stepId);

                return;
            }

            this.#collapsedStepIds.delete(stepId);
        }

        /**
         * Expands the collapsed regions that hide a step, so the designer can select and show it.
         *
         * @param {string} stepId - The step code is about to select or focus.
         */
        #expandParents(stepId) {
            // Nothing can hide the step while no region is collapsed, or before the canvas is drawn.
            const designer = this.#designer;
            const isNothingHidden = !this.#isReady || this.#collapsedStepIds.size === 0;

            if (isNothingHidden) {
                return;
            }

            // A step that is not in the definition has no parents; the designer reports it as it always does.
            const definition = designer.getDefinition();

            if (designer.getWalker().findById(definition, stepId) === null) {
                return;
            }

            // Expand every collapsed region around the step; the step itself may stay collapsed.
            let isExpanded = false;

            for (const parent of designer.getStepParents(stepId)) {
                // The parents list holds branch names too; only a collapsed step around the step hides it.
                const isParentStep = typeof parent === 'object' && parent.id !== stepId;
                const isHidingRegion = isParentStep && this.#collapsedStepIds.has(parent.id);

                if (isHidingRegion) {
                    this.#collapsedStepIds.delete(parent.id);
                    isExpanded = true;
                }
            }

            // Redraw now, so the step's view exists when the designer looks for it, and save the change.
            if (isExpanded) {
                this.#render();
                this.#saveState();
            }
        }

        /**
         * Redraws the canvas once the designer drew it the first time.
         */
        #render() {
            if (!this.#isReady) {
                return;
            }

            this.#designer.updateRootComponent();
        }

        /**
         * Saves the collapsed regions of the current definition, when they changed.
         *
         * @remarks
         * Runs after every definition and preference change, since moving or deleting a step changes
         * the positions the state is saved by.
         */
        #saveState() {
            // A new definition is read before it is saved, so its saved state is never overwritten unread.
            this.#syncDefinition();

            // Name each collapsed region by its position; regions that were deleted drop out.
            const definition = this.#designer.getDefinition();
            const state = newState(definition, this.#collapsedStepIds);
            const documentTitle = getDocumentTitle(definition);
            const stateText = JSON.stringify({ documentTitle, state });

            // Save only a real change, so opening an automation never writes its state back.
            if (stateText === this.#savedStateText) {
                return;
            }

            this.#savedStateText = stateText;
            this.#stateContext.saveState(documentTitle, state);
        }

        /**
         * Reads the saved state again when the designer holds a definition the collapsed ids do not
         * belong to.
         *
         * @remarks
         * The designer replaces the definition object on an import or an Undo, and draws it right away,
         * reading collapsed for each region while it draws; reading the state here, on the first of those
         * reads, collapses the right regions in that same drawing.
         */
        #syncDefinition() {
            // Before the designer exists, and while its definition stays the same, the ids still apply.
            const definition = this.#designer?.getDefinition();
            const isKnown = !definition || definition === this.#knownDefinition;

            if (isKnown) {
                return;
            }

            // Map the saved positions to the new definition's region ids.
            const documentTitle = getDocumentTitle(definition);
            const savedState = this.#stateContext.readState(documentTitle);

            this.#knownDefinition = definition;
            this.#collapsedStepIds = getCollapsedStepIds(definition, savedState);

            // Remember what was read, in the shape a save writes, so an unchanged state is not saved back.
            const state = newState(definition, this.#collapsedStepIds);

            this.#savedStateText = JSON.stringify({ documentTitle, state });
        }
    }

    /**
     * Converts a count to text with the right singular or plural noun.
     *
     * @param {number} count - The count.
     * @param {string} singular - The noun for one.
     * @param {string} plural - The noun for any other count.
     * @returns {string} The text, for example "1 step" or "4 steps".
     */
    function convertToCountText(count, singular, plural) {
        const noun = count === 1 ? singular : plural;

        return `${count} ${noun}`;
    }

    /**
     * Returns the text under a collapsed region's name: how many branches and steps it holds.
     *
     * @remarks
     * Compute-only. Counts every step inside the region, at any depth.
     *
     * @param {Object} step - The collapsed container or switch step.
     * @returns {string} The summary, for example "7 steps" or "2 branches · 5 steps".
     */
    function getChildrenSummary(step) {
        // Count every step under the region, at any depth.
        const countDescendants = (parentStep) => {
            let count = 0;

            for (const sequence of getChildSequences(parentStep)) {
                for (const childStep of sequence) {
                    count += 1 + countDescendants(childStep);
                }
            }

            return count;
        };

        const stepsText = convertToCountText(countDescendants(step), 'step', 'steps');

        // A switch also says how many branches it holds.
        if (!step.branches) {
            return stepsText;
        }

        const branchesText = convertToCountText(Object.keys(step.branches).length, 'branch', 'branches');

        return `${branchesText} · ${stepsText}`;
    }

    /**
     * Returns the child sequences of a step: a container's sequence, or a switch's branches.
     *
     * @remarks
     * Compute-only.
     *
     * @param {Object} step - A designer step.
     * @returns {Array<Array<Object>>} The child sequences; empty for a task.
     */
    function getChildSequences(step) {
        if (Array.isArray(step?.sequence)) {
            return [step.sequence];
        }

        const isSwitch = step?.branches !== null && typeof step?.branches === 'object';

        if (isSwitch) {
            return Object.values(step.branches).filter((branch) => Array.isArray(branch));
        }

        return [];
    }

    /**
     * Returns the collapsed region ids of a definition, read from its saved state.
     *
     * @remarks
     * Compute-only. A saved region applies only where the definition still has a region at that
     * position with the same component type and type; anything else is dropped, and state of another
     * shape or version counts as none.
     *
     * @param {Object} definition - The designer definition.
     * @param {unknown} savedState - The saved state, as the storage returned it.
     * @returns {Set<string>} The ids of the regions to show collapsed.
     */
    function getCollapsedStepIds(definition, savedState) {
        const collapsedStepIds = new Set();
        const isState = savedState?.version === STATE_VERSION && Array.isArray(savedState?.regions);

        if (!isState) {
            return collapsedStepIds;
        }

        // Index the definition's regions by position, then keep each saved region that still matches.
        const regionsByPath = new Map();

        for (const region of getRegions(definition)) {
            regionsByPath.set(JSON.stringify(region.path), region.step);
        }

        for (const savedRegion of savedState.regions) {
            const step = regionsByPath.get(JSON.stringify(savedRegion?.path));
            const isMatch = step !== undefined
                && step.componentType === savedRegion.componentType
                && step.type === savedRegion.type;

            if (isMatch) {
                collapsedStepIds.add(step.id);
            }
        }

        return collapsedStepIds;
    }

    /**
     * Returns the automation title the saved state is kept under.
     *
     * @remarks
     * Compute-only. The Hub keeps state per automation title; the VS Code designer keeps it per bot
     * file and ignores the title.
     *
     * @param {Object} definition - The designer definition.
     * @returns {string} The title; empty when the automation has none.
     */
    function getDocumentTitle(definition) {
        const title = definition?.properties?.title;

        return typeof title === 'string' ? title.trim() : '';
    }

    /**
     * Returns every region of a definition with its position.
     *
     * @remarks
     * Compute-only. A position lists the index in each sequence on the way down, and the branch name
     * before an index inside a switch branch, so it is stable across loads, where step ids are not.
     *
     * @param {Object} definition - The designer definition.
     * @returns {Array<{ path: Array<number|string>, step: Object }>} The regions, outermost first.
     */
    function getRegions(definition) {
        const regions = [];

        // Walk one sequence; a step with child sequences is a region, and its children are walked under it.
        const addRegions = (sequence, parentPath) => {
            for (const [index, step] of sequence.entries()) {
                const path = [...parentPath, index];

                if (Array.isArray(step.sequence)) {
                    regions.push({ path, step });
                    addRegions(step.sequence, path);

                    continue;
                }

                const isSwitch = step.branches !== null && typeof step.branches === 'object';

                if (isSwitch) {
                    regions.push({ path, step });

                    for (const [branchName, branch] of Object.entries(step.branches)) {
                        addRegions(branch, [...path, branchName]);
                    }
                }
            }
        };

        addRegions(definition?.sequence ?? [], []);

        return regions;
    }

    /**
     * Draws a collapsed region: a box with the step's icon, name, and children summary in place of its
     * children.
     *
     * @remarks
     * Returns the designer's step view contract. The box has no child components and no placeholders,
     * so nothing can be dropped into it; it marks its children as collapsed, so the designer validates
     * them and shows their errors on the box.
     *
     * @param {{ stepContext: Object, stepGroup: SVGGElement, viewContext: Object }} options - The step's group, and
     *     the designer's step and view contexts.
     * @returns {Object} The step view.
     */
    function newCollapsedView(options) {
        const { stepContext, stepGroup, viewContext } = options;
        const { DefaultRegionView, Dom, InputView, JoinView, LabelView, Vector } = globalThis.sequentialWorkflowDesigner;
        const { label, paddingTop } = COLLAPSED_VIEW;

        // Draw the name and, under it, the summary of what the region holds.
        const nameLabel = LabelView.create(
            stepGroup, paddingTop, label, viewContext.getStepName(), 'primary', viewContext.textWidthMeasurer);
        const summaryTop = paddingTop + label.height + COLLAPSED_VIEW.labelGap;
        const summaryLabel = LabelView.create(
            stepGroup, summaryTop, label, getChildrenSummary(stepContext.step), 'secondary', viewContext.textWidthMeasurer);

        // Size the box around the wider label and center both on the flow line.
        const width = Math.max(nameLabel.width, summaryLabel.width) + COLLAPSED_VIEW.paddingX * 2;
        const height = summaryTop + label.height + COLLAPSED_VIEW.paddingBottom;
        const joinX = width / 2;

        Dom.translate(nameLabel.g, joinX, 0);
        Dom.translate(summaryLabel.g, joinX, 0);

        // Run the flow line through the box, behind the labels, and draw the input with the step's icon.
        JoinView.createStraightJoin(stepGroup, new Vector(joinX, 0), height);

        const inputView = InputView.createRectInput(stepGroup, joinX, 0, COLLAPSED_VIEW.inputSize,
            COLLAPSED_VIEW.inputRadius, COLLAPSED_VIEW.inputIconSize, viewContext.getStepIconUrl());

        // Frame the box like any region, and add the arrow that expands it (not on a drag preview).
        const regionView = DefaultRegionView.create(stepGroup, [width], height);
        const toggle = stepContext.isPreview ? null : newToggle(stepGroup, true);

        stepGroup.classList.add(`${COMPONENT_NAME}--collapsed`);

        return {
            components: null,
            g: stepGroup,
            hasOutput: true,
            haveCollapsedChildren: true,
            height,
            joinX,
            placeholders: null,
            width,
            getClientPosition() {
                return regionView.getClientPosition();
            },
            resolveClick(click) {
                // The arrow expands the region; anywhere else on the box selects the step.
                if (toggle?.contains(click.element)) {
                    return newToggleCommand(stepContext, viewContext, false);
                }

                return regionView.resolveClick(click);
            },
            setIsDisabled(isDisabled) {
                Dom.toggleClass(stepGroup, isDisabled, 'sqd-disabled');
            },
            setIsDragging(isDragging) {
                inputView.setIsHidden(isDragging);
            },
            setIsSelected(isSelected) {
                regionView.setIsSelected(isSelected);
            }
        };
    }

    /**
     * Creates an in-memory preference storage for a configuration that had none.
     *
     * @remarks
     * Compute-only factory; matches the designer's own default storage.
     *
     * @returns {{ getItem: Function, setItem: Function }} The storage.
     */
    function newMemoryPreferenceStorage() {
        const preferences = new Map();

        return {
            getItem: (key) => preferences.get(key) ?? null,
            setItem: (key, value) => {
                preferences.set(key, value);
            }
        };
    }

    /**
     * Creates the designer extension that draws every container and switch frame.
     *
     * @remarks
     * Compute-only factory. The designer calls create with five arguments (its extension contract). A
     * collapsible region draws collapsed, or expanded with the designer's own content plus the arrow;
     * any other region draws exactly as the designer's default extension draws it.
     *
     * @returns {{ create: Function }} The regionComponentView extension.
     */
    function newRegionComponentViewExtension() {
        const { ComponentDom, DefaultRegionView } = globalThis.sequentialWorkflowDesigner;

        return {
            create(parentElement, componentClassName, stepContext, viewContext, contentFactory) {
                // Every region view starts from the step's group, as the default extension's does.
                const step = stepContext.step;
                const stepGroup = ComponentDom.stepG(componentClassName, step.type, step.id);

                parentElement.appendChild(stepGroup);

                // A region that does not collapse draws exactly as it always does.
                if (!COLLAPSIBLE_CLASS_NAMES.has(componentClassName)) {
                    return contentFactory(stepGroup, DefaultRegionView.create);
                }

                // A collapsed region draws its box; an expanded one its content, with the arrow on its frame.
                if (viewContext.getPreference(COLLAPSED_PREFERENCE_KEY) === 'true') {
                    return newCollapsedView({ stepContext, stepGroup, viewContext });
                }

                return contentFactory(stepGroup, newRegionViewFactory(stepContext, viewContext));
            }
        };
    }

    /**
     * Creates the region frame factory of an expanded region: the designer's frame plus the arrow.
     *
     * @remarks
     * The designer's container and switch views call the factory last, after drawing their content, and
     * pass on any command the frame returns from a click, so the arrow's collapse command reaches the
     * designer while every other click behaves as it always does.
     *
     * @param {Object} stepContext - The designer's step context.
     * @param {Object} viewContext - The designer's view context.
     * @returns {Function} The factory: (parent, widths, height) → region view.
     */
    function newRegionViewFactory(stepContext, viewContext) {
        const { DefaultRegionView } = globalThis.sequentialWorkflowDesigner;

        return (parent, widths, height) => {
            // Draw the frame, then the arrow over its top-left corner (not on a drag preview).
            const regionView = DefaultRegionView.create(parent, widths, height);
            const toggle = stepContext.isPreview ? null : newToggle(parent, false);

            return {
                getClientPosition() {
                    return regionView.getClientPosition();
                },
                resolveClick(click) {
                    // The arrow collapses the region; any other click is the frame's.
                    if (toggle?.contains(click.element)) {
                        return newToggleCommand(stepContext, viewContext, true);
                    }

                    return regionView.resolveClick(click);
                },
                setIsSelected(isSelected) {
                    regionView.setIsSelected(isSelected);
                }
            };
        };
    }

    /**
     * Returns the saved state of a definition's collapsed regions.
     *
     * @remarks
     * Compute-only. Each collapsed region is named by its position, component type, and type; null
     * means nothing is collapsed, so the storage drops the entry.
     *
     * @param {Object} definition - The designer definition.
     * @param {Set<string>} collapsedStepIds - The ids of the collapsed regions.
     * @returns {{ regions: Array<Object>, version: number } | null} The state; null when none is collapsed.
     */
    function newState(definition, collapsedStepIds) {
        const regions = getRegions(definition)
            .filter((region) => collapsedStepIds.has(region.step.id))
            .map((region) => ({
                componentType: region.step.componentType,
                path: region.path,
                type: region.step.type
            }));

        if (regions.length === 0) {
            return null;
        }

        return { regions, version: STATE_VERSION };
    }

    /**
     * Draws the arrow in a region's top-left corner: right while collapsed, down while expanded.
     *
     * @param {SVGGElement} parent - The region's group.
     * @param {boolean} isCollapsed - Whether the region is collapsed.
     * @returns {SVGGElement} The arrow's group, which a click is tested against.
     */
    function newToggle(parent, isCollapsed) {
        const { Dom } = globalThis.sequentialWorkflowDesigner;
        const iconOffset = TOGGLE_OFFSET_PIXELS + (TOGGLE_SIZE_PIXELS - TOGGLE_ICON_SIZE_PIXELS) / 2;
        const arrowPath = isCollapsed ? ARROW_RIGHT_ICON_PATH : ARROW_DOWN_ICON_PATH;
        const tooltipText = isCollapsed ? 'Expand' : 'Collapse';

        // Group the click area, the icon, and the tooltip, so one test covers a click on any of them.
        const toggle = Dom.svg('g', {
            class: `${COMPONENT_NAME}__toggle`,
            'data-test-id': 'collapsible-region-toggle-button'
        });
        const area = Dom.svg('rect', {
            class: `${COMPONENT_NAME}__toggle-area`,
            height: TOGGLE_SIZE_PIXELS,
            width: TOGGLE_SIZE_PIXELS,
            x: TOGGLE_OFFSET_PIXELS,
            y: TOGGLE_OFFSET_PIXELS
        });
        const icon = Dom.svg('svg', {
            class: `${COMPONENT_NAME}__toggle-icon`,
            height: TOGGLE_ICON_SIZE_PIXELS,
            viewBox: '0 0 640 640',
            width: TOGGLE_ICON_SIZE_PIXELS,
            x: iconOffset,
            y: iconOffset
        });
        const arrow = Dom.svg('path', { d: arrowPath });
        const tooltip = Dom.svg('title');

        // Name what a click does.
        tooltip.textContent = tooltipText;

        // Assemble the arrow on top of the region's content.
        icon.appendChild(arrow);
        toggle.append(tooltip, area, icon);
        parent.appendChild(toggle);

        return toggle;
    }

    /**
     * Creates the click command that collapses or expands a region.
     *
     * @remarks
     * Compute-only. The designer runs it as a preference change: it keeps the preference through the
     * session, redraws the canvas, and reports the change, which the session saves.
     *
     * @param {Object} stepContext - The designer's step context.
     * @param {Object} viewContext - The designer's view context.
     * @param {boolean} isCollapsed - Whether the click collapses the region.
     * @returns {Object} The designer's change-preferences click command.
     */
    function newToggleCommand(stepContext, viewContext, isCollapsed) {
        const { ClickCommandType } = globalThis.sequentialWorkflowDesigner;
        const value = isCollapsed ? 'true' : 'false';

        return {
            changes: [viewContext.createPreferenceChange(COLLAPSED_PREFERENCE_KEY, value)],
            step: stepContext.step,
            type: ClickCommandType.changePreferences
        };
    }

    // Register the plugin; it installs into every designer the page creates.
    globalThis.G4DesignerPlugins?.register({
        name: PLUGIN_NAME,

        /**
         * Installs the plugin into a designer's configuration.
         *
         * @param {Object} configuration - The designer configuration, before the designer is created.
         * @returns {G4CollapsibleRegionsSession} The session of that designer.
         */
        install(configuration) {
            const session = new G4CollapsibleRegionsSession(configuration.preferenceStorage);
            const extensions = configuration.extensions ?? [];

            configuration.extensions = [...extensions, { regionComponentView: newRegionComponentViewExtension() }];
            configuration.preferenceStorage = session;

            return session;
        }
    });
})();
