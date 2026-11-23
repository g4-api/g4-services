/*
 * G4(TM) Parallel container: a G4 designer plugin (see components/g4-designer-plugins).
 *
 * Lets the user draw a Parallel container on the canvas: a block whose stages (at the top level) or jobs
 * (inside a Stage) are drawn side by side, each as one lane, between a fork and a join. The engine runs
 * the lanes at the same time and continues the flow when they have finished. The automation writes the
 * block as an entry with "$type": "Parallel"; js/g4-client.js and js/global.js convert it, and
 * js/validators.js holds its placement rules.
 *
 * What the plugin adds to the designer:
 * - A "Parallel" item in the Containers toolbox, with its own icon.
 * - The block's drawing: the container's name and icon on top, the lanes side by side under it, and a
 *   "Background" tag on every lane the container does not wait for.
 * - Two step editor fields: "Max Parallel" on the container, and "Background Lane" on a Stage or Job that
 *   is a lane.
 * - The placement rules of the Parallel container and of the stages and jobs that can be its lanes, checked
 *   on every validation, so a placement error always matches where the step sits now.
 * - A steady view during a run: from the container's start event to its end event, while its lanes run at
 *   the same time, the designer keeps the container in view instead of jumping between the steps of
 *   different lanes.
 *
 * Contract:
 * - Uses only what the designer bundle exports and the designer's configuration: a step extension for
 *   the launchPad component, the toolbox groups, the icon provider, the step validator, and the step
 *   editor provider. It never edits js/canvas.umd.js or js/canvas.js.
 * - Draws its frame through the designer's region view as a container, so the collapsible regions plugin
 *   collapses and expands it like a Stage or a Job.
 * - Keeps no saved state: everything it shows is part of the automation.
 * - Load after components/g4-designer-plugins and components/g4-collapsible-regions, so its selection
 *   guard runs before a collapsed region is expanded for a step that is not going to be selected.
 */
(() => {
    'use strict';

    // Component name: the CSS block name of the block's own elements.
    const COMPONENT_NAME = 'g4-parallel-container';

    // Plugin name: how the plugin host names the plugin in its reports.
    const PLUGIN_NAME = 'parallel-container';

    // Name of the toolbox group that holds the Stage and Job containers.
    const CONTAINERS_GROUP_NAME = 'Containers';

    // File name of the container's icon, next to the other step icons of the Hub.
    const ICON_FILE_NAME = 'icon-parallel.svg';

    // Step type of the container, which the icon provider receives.
    const PARALLEL_STEP_TYPE = 'parallel';

    // Designer component type that draws the steps of a sequence side by side.
    const LANES_COMPONENT_TYPE = 'launchPad';

    // Region class the block is framed with: the container's, so it is styled and collapsed like one.
    const REGION_CLASS_NAME = 'container';

    // Plugin names of the steps that can be a lane.
    const LANE_PLUGIN_NAMES = new Set(['G4™ Stage', 'G4™ Job']);

    // Event type of the Hub's run events that report the start and the end of a Parallel container.
    const PARALLEL_EVENT_TYPE = 'PARALLEL';

    // Event type of the Hub's run event that reports the start of a run.
    const AUTOMATION_EVENT_TYPE = 'AUTOMATION';

    // Geometry of the block's header: the designer's container values, so the name and the icon line up
    // with those of a Stage or a Job.
    const HEADER_VIEW = {
        inputIconSize: 14,
        inputRadius: 4,
        inputSize: 18,
        label: {
            height: 22,
            minWidth: 50,
            paddingX: 10,
            radius: 10
        },
        paddingTop: 20,
        paddingX: 20
    };

    // Geometry of the lanes under the header: the designer's launchPad values, without the frame (the
    // block draws its own) and without the icon of an empty block (the header already shows it).
    const LANES_VIEW = {
        connectionHeight: 20,
        emptyIconSize: 0,
        emptyInputSize: 14,
        emptyOutputSize: 10,
        emptyPaddingX: 20,
        emptyPaddingY: 20,
        isRegionEnabled: false,
        paddingY: 10
    };

    // Geometry of any other launchPad step: the designer's own defaults, so it draws as it always does.
    const DEFAULT_LANES_VIEW = {
        connectionHeight: 20,
        emptyIconSize: 24,
        emptyInputSize: 14,
        emptyOutputSize: 10,
        emptyPaddingX: 20,
        emptyPaddingY: 20,
        isRegionEnabled: true,
        paddingY: 10
    };

    // Space between the fork and the lanes when a lane carries the Background tag: room for the tag on the
    // lane's connector, as a switch keeps room for its branch names. The same space is kept above the join.
    const TAGGED_LANES_PADDING_PIXELS = 36;

    // Geometry of the lanes when at least one of them is a background lane.
    const TAGGED_LANES_VIEW = { ...LANES_VIEW, paddingY: TAGGED_LANES_PADDING_PIXELS };

    // Geometry of the tag on a background lane: a small label on the connector above the lane.
    const LANE_TAG_VIEW = {
        height: 18,
        minWidth: 30,
        paddingX: 8,
        radius: 9
    };

    // Text of the tag on a background lane, and what it means.
    const LANE_TAG_TEXT = 'Background';
    const LANE_TAG_TOOLTIP = 'Background lane: the container does not wait for it.';

    // Label and hint of the container's lanes limit in the step editor.
    const MAX_PARALLEL_LABEL = 'Max Parallel';
    const MAX_PARALLEL_HINT = 'The most lanes that run at the same time. 0 runs every lane at once. '
        + 'With a lower value, the lanes start in the order they are drawn. Background lanes start at once '
        + 'and do not count.';

    // Label and hint of a lane's background flag in the step editor.
    const BACKGROUND_LABEL = 'Background Lane';
    const BACKGROUND_HINT = 'The Parallel container does not wait for this lane. When every other lane has '
        + 'finished, the lane is stopped before its next rule and is reported as complete. When every lane '
        + 'is a background lane, the flag is ignored.';

    /**
     * Keeps the view of one designer steady while the lanes of a Parallel container run.
     *
     * @remarks
     * The canvas selects every step that starts and moves the viewport to it, which is right while one
     * step runs at a time. Lanes run at the same time, so their steps start in turns and the view would
     * jump from lane to lane. The session follows the Hub's Parallel container events to know which
     * containers are running: the canvas shows a container when it starts, and the session then skips the
     * selection of the steps inside it until the container ends. Owns explicit per-designer state rather
     * than module state, since a page can create more than one designer.
     */
    class G4ParallelContainerSession {
        // The designer, once it was created.
        #designer = null;

        // The ids of the Parallel containers that started and have not ended yet.
        #runningContainerIds = new Set();

        /**
         * Connects the session to its designer once the designer is created.
         *
         * @remarks
         * Wraps two of this designer instance's methods (not the bundle's class) with the guard, and follows
         * the Hub's run events. The listeners are registered here, before the canvas registers its own, so
         * a container is known to be running before the canvas selects the first step of a lane.
         *
         * @param {Object} designer - The created designer.
         */
        attach(designer) {
            // Keep the designer and its own methods; the guard decides when they run.
            const selectStepById = designer.selectStepById.bind(designer);
            const moveViewportToStep = designer.moveViewportToStep.bind(designer);

            this.#designer = designer;

            // Skip a step that code selects or shows while it is inside a container whose lanes run together.
            designer.selectStepById = (stepId) => {
                if (this.#testHeldStep(stepId)) {
                    return;
                }

                selectStepById(stepId);
            };
            designer.moveViewportToStep = (stepId) => {
                if (this.#testHeldStep(stepId)) {
                    return;
                }

                moveViewportToStep(stepId);
            };

            // A page without the Hub connection (a test page) has no run to follow.
            if (typeof _connection === 'undefined') {
                return;
            }

            // Follow the run: containers start and end, and the run itself ends, is stopped, or fails to start.
            const onRunEnded = () => {
                this.#runningContainerIds.clear();
            };

            _connection.on('ReceiveAutomationStartEvent', (message) => this.#onStepStarted(message));
            _connection.on('ReceiveAutomationEndEvent', (message) => this.#onStepEnded(message));
            _connection.on('ReceiveAutomationInvokedEvent', onRunEnded);
            _connection.on('StartAutomation', onRunEnded);
        }

        /**
         * Redraws the canvas after a change the designer does not redraw for by itself.
         *
         * @remarks
         * The designer redraws for a new name or a moved step, and only refreshes the error badges for a
         * changed property. The Background tag is part of the drawing, so a lane whose flag changed is
         * redrawn here. Before the designer exists there is nothing to redraw.
         */
        updateCanvas() {
            this.#designer?.updateRootComponent();
        }

        /**
         * Forgets a Parallel container that ended, so the view follows the run again.
         *
         * @remarks
         * The container ends after every lane has finished or was stopped, a background lane included, so
         * nothing inside it is still running when the view is released.
         *
         * @param {Object} message - The Hub's event for a step that ended.
         */
        #onStepEnded(message) {
            // Only the end of a container changes what the session follows.
            if (getEventType(message) !== PARALLEL_EVENT_TYPE) {
                return;
            }

            // Release the view: the canvas follows every step that starts after this container again.
            this.#runningContainerIds.delete(message.id);
        }

        /**
         * Records a Parallel container that started; the canvas selects and shows it right after.
         *
         * @param {Object} message - The Hub's event for a step that started.
         */
        #onStepStarted(message) {
            // A new run starts with no container running, whatever the last run left behind.
            const eventType = getEventType(message);

            if (eventType === AUTOMATION_EVENT_TYPE) {
                this.#runningContainerIds.clear();

                return;
            }

            // Any other step that starts is followed by the canvas as it always is.
            const isContainerEvent = eventType === PARALLEL_EVENT_TYPE && typeof message.id === 'string';

            if (!isContainerEvent) {
                return;
            }

            // Record the container before the canvas asks to select the steps of its lanes. The container's
            // id is the id of its step, because the designer sends the step ids with the run.
            this.#runningContainerIds.add(message.id);
        }

        /**
         * Tests whether a step must not be selected or shown now, because it is inside a Parallel container
         * whose lanes are running at the same time.
         *
         * @param {string} stepId - The step code is about to select or show.
         * @returns {boolean} True when the selection is skipped.
         */
        #testHeldStep(stepId) {
            // Outside a running container every step is selected as it always is.
            if (this.#runningContainerIds.size === 0) {
                return false;
            }

            // A step that is not in the definition has no parents; the designer reports it as it always does.
            const designer = this.#designer;

            if (designer.getWalker().findById(designer.getDefinition(), stepId) === null) {
                return false;
            }

            // The parents list holds branch names too; only a running container around the step holds it.
            return designer.getStepParents(stepId).some((parent) => {
                const isParentStep = typeof parent === 'object' && parent.id !== stepId;
                const isRunningContainer = isParentStep && this.#runningContainerIds.has(parent.id);

                return isRunningContainer && testConcurrentLanes(parent);
            });
        }
    }

    /**
     * Adds the Background Lane switch to the step editor of a Stage or a Job that is a lane.
     *
     * @remarks
     * The switch writes the lane's flag on the step, which the conversion to an automation reads, and
     * reports the change to the designer, so the lane's tag is redrawn and the edit can be undone.
     *
     * @param {{ editorContext: Object, editorElement: HTMLElement, session: G4ParallelContainerSession,
     *     step: Object }} options - The step editor's element and context, the designer's session, and the
     *     lane's step.
     */
    function addBackgroundField(options) {
        const { editorContext, editorElement, session, step } = options;

        // Create the switch on its own, not yet in the editor, so it can be placed inside the editor.
        const fieldElement = CustomFields.newSwitchField(
            {
                initialValue: step.lane?.isBackground === true,
                label: BACKGROUND_LABEL,
                title: BACKGROUND_HINT
            },
            (value) => {
                // The switch reports its state as text; keep the flag as a boolean on the step.
                step.lane = { ...step.lane, isBackground: value === 'true' };

                // Report the change, so the edit reaches the automation and can be undone.
                editorContext.notifyPropertiesChanged();

                // Draw or remove the lane's tag; the designer does not redraw for a changed property.
                session.updateCanvas();
            }
        );

        // Place the switch under the display name, above the driver parameters, which open as a long section.
        // An editor without that section gets the switch at its end.
        editorElement.insertBefore(fieldElement, editorElement.querySelector(':scope > details'));
    }

    /**
     * Draws the Background tag on the connector above a lane, where a switch shows a branch name.
     *
     * @remarks
     * The tag is drawn in the group of the lanes, after the connectors, so it covers the connector it sits
     * on. The lane's place is read from the lane's own group, where the designer's launchPad view put it.
     *
     * @param {{ lanesGroup: SVGGElement, laneView: Object, viewContext: Object }} options - The group
     *     that holds the lanes and their connectors, the lane's step view, and the designer's view context
     *     of the container.
     */
    function addLaneTag(options) {
        const { lanesGroup, laneView, viewContext } = options;
        const { Dom, LabelView } = globalThis.sequentialWorkflowDesigner;

        // Read where the lane was placed; a lane that was not moved sits at the group's origin.
        const laneTransform = laneView.g.transform.baseVal.consolidate();
        const laneLeft = laneTransform?.matrix.e ?? 0;
        const laneTop = laneTransform?.matrix.f ?? 0;

        // Center the tag in the space kept above the lane, clear of the lane's icon, which rises above the
        // lane's top edge by half its size.
        const iconRise = HEADER_VIEW.inputSize / 2;
        const tagTop = laneTop - iconRise - (TAGGED_LANES_PADDING_PIXELS - iconRise + LANE_TAG_VIEW.height) / 2;
        const tag = LabelView.create(
            lanesGroup, tagTop, LANE_TAG_VIEW, LANE_TAG_TEXT, 'secondary', viewContext.textWidthMeasurer);
        const tooltip = Dom.svg('title');

        // Name the tag for the styles and the tests, and say what it means.
        tooltip.textContent = LANE_TAG_TOOLTIP;
        tag.g.classList.add(`${COMPONENT_NAME}__lane-tag`);
        tag.g.dataset.testId = 'parallel-lane-background-tag';
        tag.g.appendChild(tooltip);

        // Put the tag on the lane's connector.
        Dom.translate(tag.g, laneLeft + laneView.joinX, 0);
    }

    /**
     * Adds the Max Parallel number field to the step editor of a Parallel container.
     *
     * @param {{ editorContext: Object, editorElement: HTMLElement, isReadonly: boolean, step: Object }} options -
     *     The step editor's element and context, whether the designer is read-only, and the container's step.
     */
    function addMaxParallelField(options) {
        const { editorContext, editorElement, isReadonly, step } = options;

        // Show the limit as text, because the number field reads the number zero as "no value".
        const maxParallel = StateMachineSteps.convertToMaxParallel(step.parallel?.maxParallel);

        CustomFields.newNumberField(
            {
                container: editorElement,
                initialValue: `${maxParallel}`,
                isReadonly,
                label: MAX_PARALLEL_LABEL,
                step: 1,
                title: MAX_PARALLEL_HINT
            },
            (value) => {
                // Keep the limit as a whole number on the step; anything else means every lane at once.
                step.parallel = { ...step.parallel, maxParallel: StateMachineSteps.convertToMaxParallel(value) };

                // Notify the editor context that the step changed, so the edit is reported and can be undone.
                editorContext.notifyPropertiesChanged();
            }
        );
    }

    /**
     * Adds the Parallel container to the Containers toolbox, next to the Stage and the Job.
     *
     * @remarks
     * Mutates the designer configuration it is given, before the designer is created. A configuration
     * without the Containers group (another page) gets no item.
     *
     * @param {Object} configuration - The designer configuration.
     */
    function addToolboxStep(configuration) {
        // Find the group of the Stage and the Job; a page that has none is left as it is.
        const groups = configuration.toolbox?.groups ?? [];
        const containersGroup = groups.find((group) => group.name === CONTAINERS_GROUP_NAME);

        if (!containersGroup) {
            return;
        }

        // Add the container and keep the group in name order, as the page sorts every group.
        const parallelStep = StateMachineSteps.newG4Parallel('Parallel', {}, {}, []);

        containersGroup.steps = [...containersGroup.steps, parallelStep]
            .toSorted((left, right) => left.name.localeCompare(right.name));
    }

    /**
     * Finds the Parallel container a Stage or a Job is a lane of.
     *
     * @remarks
     * Compute-only. A lane is a Stage or a Job placed directly inside a Parallel container; only a lane
     * has the background flag.
     *
     * @param {Object} step - The step to look up.
     * @param {Object} definition - The designer definition that holds the step.
     * @returns {Object | null} The Parallel container's step; null when the step is not a lane.
     */
    function findLaneContainer(step, definition) {
        // Only a Stage or a Job can be a lane.
        if (!LANE_PLUGIN_NAMES.has(step?.pluginName)) {
            return null;
        }

        // It is one when its direct parent is a Parallel container.
        const parent = Utilities.findParentContainer(step, definition?.sequence ?? []);

        return StateMachineSteps.testG4Parallel(parent) ? parent : null;
    }

    /**
     * Returns the type of a Hub run event in upper case.
     *
     * @remarks
     * Compute-only. The Hub writes the type in mixed case ("Stage", "Job", "Parallel", "Automation"); an
     * event without a text type has none.
     *
     * @param {Object} message - The Hub's run event.
     * @returns {string} The type in upper case; empty when the event has none.
     */
    function getEventType(message) {
        const eventType = message?.type;

        return typeof eventType === 'string' ? eventType.toUpperCase() : '';
    }

    /**
     * Creates the view factory of the launchPad component.
     *
     * @remarks
     * Compute-only factory. A Parallel container draws as the block; any other launchPad step draws
     * exactly as the designer's default extension draws it.
     *
     * @returns {Function} The designer's component view factory: (parent, stepContext, viewContext) → view.
     */
    function newComponentViewFactory() {
        // Create the designer's own launchPad views once: one for each geometry the component draws with.
        const { createLaunchPadStepComponentViewFactory } = globalThis.sequentialWorkflowDesigner;
        const newDefaultView = createLaunchPadStepComponentViewFactory(false, DEFAULT_LANES_VIEW);
        const newLanesView = createLaunchPadStepComponentViewFactory(false, LANES_VIEW);
        const newTaggedLanesView = createLaunchPadStepComponentViewFactory(false, TAGGED_LANES_VIEW);

        return (parentElement, stepContext, viewContext) => {
            // Any launchPad step that is not a Parallel container keeps the designer's drawing.
            if (!StateMachineSteps.testG4Parallel(stepContext.step)) {
                return newDefaultView(parentElement, stepContext, viewContext);
            }

            // Keep room for the Background tag only in a block that shows one.
            const lanes = stepContext.step.sequence ?? [];
            const isTagged = lanes.some((lane) => lane.lane?.isBackground === true);
            const newBlockLanesView = isTagged
                ? newTaggedLanesView
                : newLanesView;

            // Frame the block through the designer's region view, which also lets it collapse.
            return viewContext.createRegionComponentView(
                parentElement,
                REGION_CLASS_NAME,
                (stepGroup, regionViewFactory) => newParallelView({
                    newLanesView: newBlockLanesView,
                    regionViewFactory,
                    stepContext,
                    stepGroup,
                    viewContext
                }));
        };
    }

    /**
     * Creates the icon provider that also knows the Parallel container's icon.
     *
     * @remarks
     * Compute-only factory. The container's icon sits in the folder of the other step icons. The folder is
     * taken from an icon the page's provider already resolves, because the VS Code designer rewrites that
     * provider to the Hub's address, which this plugin cannot know.
     *
     * @param {Function} iconUrlProvider - The provider the configuration had: (componentType, type) → URL.
     * @returns {Function} The provider with the container's icon.
     */
    function newIconUrlProvider(iconUrlProvider) {
        return (componentType, type) => {
            // Every other step keeps the icon the page's provider gives it.
            if (type !== PARALLEL_STEP_TYPE) {
                return iconUrlProvider(componentType, type);
            }

            // Swap the file name of the Stage icon for the container's, keeping the folder it was resolved to.
            const stageIconUrl = iconUrlProvider(componentType, 'stage');
            const folderLength = stageIconUrl.lastIndexOf('/') + 1;

            return `${stageIconUrl.slice(0, folderLength)}${ICON_FILE_NAME}`;
        };
    }

    /**
     * Draws a Parallel container: its name and icon on top, and its lanes side by side under them.
     *
     * @remarks
     * Returns the designer's step view contract. The lanes are drawn by the designer's own launchPad view,
     * which also gives the block its drop places between the lanes; the block adds the header, the frame,
     * and the tags of the background lanes.
     *
     * @param {{ newLanesView: Function, regionViewFactory: Function, stepContext: Object, stepGroup: SVGGElement,
     *     viewContext: Object }} options - The launchPad view factory, the frame factory, the step's group,
     *     and the designer's step and view contexts.
     * @returns {Object} The step view.
     */
    function newParallelView(options) {
        const { newLanesView, regionViewFactory, stepContext, stepGroup, viewContext } = options;
        const { Dom, InputView, JoinView, LabelView, Vector } = globalThis.sequentialWorkflowDesigner;
        const { label, paddingTop, paddingX } = HEADER_VIEW;

        // Draw the name first, then the lanes, in the order a container draws its parts.
        const nameLabel = LabelView.create(
            stepGroup, paddingTop, label, viewContext.getStepName(), 'primary', viewContext.textWidthMeasurer);

        // Draw the lanes side by side, with the fork above them and the join below.
        const lanesView = newLanesView(stepGroup, stepContext, viewContext);

        // Size the block around the wider of the name and the lanes, and keep both on the flow line.
        const halfLabelWidth = nameLabel.width / 2;
        const offsetLeft = Math.max(halfLabelWidth - lanesView.joinX, 0) + paddingX;
        const offsetRight = Math.max(halfLabelWidth - (lanesView.width - lanesView.joinX), 0) + paddingX;
        const headerHeight = paddingTop + label.height;
        const width = offsetLeft + lanesView.width + offsetRight;
        const height = headerHeight + lanesView.height;
        const joinX = lanesView.joinX + offsetLeft;

        Dom.translate(nameLabel.g, joinX, 0);
        Dom.translate(lanesView.g, offsetLeft, headerHeight);

        // Draw the input with the container's icon, and the flow line from it down to the name.
        const inputView = InputView.createRectInput(stepGroup, joinX, 0, HEADER_VIEW.inputSize,
            HEADER_VIEW.inputRadius, HEADER_VIEW.inputIconSize, viewContext.getStepIconUrl());

        JoinView.createStraightJoin(stepGroup, new Vector(joinX, 0), paddingTop);

        // Tag every lane the container does not wait for.
        for (const laneComponent of lanesView.components ?? []) {
            if (laneComponent.step?.lane?.isBackground === true) {
                addLaneTag({ lanesGroup: lanesView.g, laneView: laneComponent.view, viewContext });
            }
        }

        // Frame the block like any container, and name it for the styles and the tests.
        const regionView = regionViewFactory(stepGroup, [width], height);

        stepGroup.classList.add(COMPONENT_NAME);
        stepGroup.dataset.testId = 'parallel-container';

        return {
            components: lanesView.components,
            g: stepGroup,
            hasOutput: lanesView.hasOutput,
            height,
            joinX,
            placeholders: lanesView.placeholders,
            width,
            getClientPosition() {
                return regionView.getClientPosition();
            },
            resolveClick(click) {
                // The frame answers first: it selects the block, or returns a command such as collapse.
                const regionResult = regionView.resolveClick(click);

                if (regionResult !== null) {
                    return regionResult;
                }

                // The name and the icon select the block too, as they do on a Stage or a Job.
                const isHeaderClick = nameLabel.g.contains(click.element) || inputView.g.contains(click.element);

                return isHeaderClick ? true : null;
            },
            setIsDisabled(isDisabled) {
                Dom.toggleClass(stepGroup, isDisabled, 'sqd-disabled');
            },
            setIsDragging(isDragging) {
                inputView.setIsHidden(isDragging);
                lanesView.setIsDragging(isDragging);
            },
            setIsSelected(isSelected) {
                regionView.setIsSelected(isSelected);
            }
        };
    }

    /**
     * Creates the step editor provider that also edits the Parallel container and its lanes.
     *
     * @remarks
     * Compute-only factory. The page's provider draws the editor as it always does (the title, the errors,
     * the display name, and for a Stage or a Job the driver parameters); this adds the lanes limit to a
     * Parallel container, and the background switch to a Stage or a Job that is a lane.
     *
     * @param {Function} stepEditorProvider - The provider the configuration had.
     * @param {G4ParallelContainerSession} session - The designer's session, which redraws a changed lane.
     * @returns {Function} The provider: (step, editorContext, definition, isReadonly) → editor element.
     */
    function newStepEditorProvider(stepEditorProvider, session) {
        return (step, editorContext, definition, isReadonly) => {
            // Let the page draw the editor first, so the added fields join an editor that is already complete.
            const editorElement = stepEditorProvider(step, editorContext, definition, isReadonly);

            // A Parallel container gets its lanes limit, and nothing else.
            if (StateMachineSteps.testG4Parallel(step)) {
                addMaxParallelField({ editorContext, editorElement, isReadonly: isReadonly === true, step });

                return editorElement;
            }

            // A Stage or a Job gets the background switch only while it is a lane.
            if (findLaneContainer(step, definition) !== null) {
                addBackgroundField({ editorContext, editorElement, session, step });
            }

            return editorElement;
        };
    }

    /**
     * Creates the step validator that checks, on every validation, where a Stage, a Job, and a Parallel
     * container sit.
     *
     * @remarks
     * Compute-only factory. The page's validator checks a step fully only while the step is selected or has
     * no children; for any other step it answers from the errors already recorded on the step. A placement
     * error recorded while a step was misplaced therefore stayed on it after the step, or the container
     * around it, was moved to a valid place: a Job kept "Invalid Job Placement" after its Parallel container
     * was moved from inside a Job into a Stage. The placement checks run here first, for every step and
     * every time, and each records or clears its own error on the step, so the page's validator always
     * answers from the placement the step has now.
     *
     * @param {Function | undefined} stepValidator - The validator the configuration had.
     * @returns {Function} The validator: (step, parentSequence, definition) → whether the step is valid.
     */
    function newStepValidator(stepValidator) {
        return (step, parentSequence, definition) => {
            // Record where the step sits now; each check passes a step it does not apply to, and clears its
            // own error when the placement is valid.
            const isStagePlaced = Validators.confirmStagePlacement(step, definition);
            const isJobPlaced = Validators.confirmJobPlacement(step, definition);
            const isParallelPlaced = Validators.confirmParallelPlacement(step, definition);
            const isPlaced = isStagePlaced && isJobPlaced && isParallelPlaced;

            // Then ask the page's validator; a configuration without one accepts every step.
            const isValid = typeof stepValidator === 'function'
                ? stepValidator(step, parentSequence, definition)
                : true;

            return isValid && isPlaced;
        };
    }

    /**
     * Tests whether the lanes of a Parallel container run at the same time.
     *
     * @remarks
     * Compute-only. A container with one lane, or with a limit of one lane and no background lane, runs
     * its lanes one after another, so the view can follow each step as it does outside a container.
     *
     * @param {Object} container - The Parallel container's step.
     * @returns {boolean} True when at least two lanes can run together.
     */
    function testConcurrentLanes(container) {
        // One lane has nothing to run together with.
        const lanes = container?.sequence ?? [];

        if (lanes.length < 2) {
            return false;
        }

        // A limit of one lane runs them in turn, unless a background lane starts beside them at once.
        const isBackgroundLane = lanes.some((lane) => lane.lane?.isBackground === true);
        const isOneAtATime = StateMachineSteps.convertToMaxParallel(container.parallel?.maxParallel) === 1 && !isBackgroundLane;

        return !isOneAtATime;
    }

    // Register the plugin; it installs into every designer the page creates.
    globalThis.G4DesignerPlugins?.register({
        name: PLUGIN_NAME,

        /**
         * Installs the plugin into a designer's configuration.
         *
         * @param {Object} configuration - The designer configuration, before the designer is created.
         * @returns {G4ParallelContainerSession} The session of that designer.
         */
        install(configuration) {
            // The session of this designer; the step editor asks it to redraw a lane whose tag changed.
            const session = new G4ParallelContainerSession();

            // Offer the container in the toolbox, with its icon.
            addToolboxStep(configuration);

            configuration.steps = configuration.steps ?? {};
            configuration.steps.iconUrlProvider = newIconUrlProvider(configuration.steps.iconUrlProvider);

            // Check its placement, and edit it and its lanes in the step editor.
            configuration.validator = configuration.validator ?? {};
            configuration.validator.step = newStepValidator(configuration.validator.step);
            configuration.editors.stepEditorProvider = newStepEditorProvider(
                configuration.editors.stepEditorProvider, session);

            // Draw it: this step extension replaces the designer's default launchPad view.
            const extensions = configuration.extensions ?? [];
            const stepExtension = {
                componentType: LANES_COMPONENT_TYPE,
                createComponentView: newComponentViewFactory()
            };

            configuration.extensions = [...extensions, { steps: [stepExtension] }];

            return session;
        }
    });
})();
