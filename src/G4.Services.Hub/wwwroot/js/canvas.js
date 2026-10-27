/* global window, document, _designer, _manifests, _cache, _cacheKeys, _stateMachine */

/**
 * Exports the current definition from the designer as a G4 JSON file.
 *
 * 1. Retrieves the definition object from the designer.
 * 2. Generates an automation object from the definition.
 * 3. Logs the pretty-printed JSON in the console with decorative headers.
 * 4. Creates a Blob URL from the JSON and triggers a download.
 * 5. Cleans up the Blob URL reference.
 */
function exportDefinition() {
	// Retrieve the definition from the designer
	const definition = _designer.getDefinition();

	// Create or retrieve the object you want to print
	const automationObject = _client.newAutomation(definition);

	// Convert the object into a pretty-printed JSON string (4 spaces)
	const automationJson = JSON.stringify(automationObject, null, 4);

	// Print a decorative header
	console.log("====================================");
	console.log("          Automation Details        ");
	console.log("====================================");

	// Print a quick description
	console.log("Below is the automation object as JSON:\n");

	// Print the JSON itself
	console.log(automationJson);

	// Print a closing line
	console.log("====================================");
	console.log("  End of Automation Object Details  ");
	console.log("====================================\n\n");

	// Create a Blob from the string (specify the MIME type as JSON)
	const blob = new Blob([automationJson], { type: 'application/json' });

	// Generate a temporary URL for that blob
	const url = URL.createObjectURL(blob);

	// Create a hidden <a> element and set its download attribute
	const downloadLink = document.createElement('a');
	downloadLink.href = url;
	downloadLink.download = `${Utilities.newUid()}.json`;

	// Programmatically click the link to trigger the download
	downloadLink.click();

	// Revoke the Blob URL to release memory
	URL.revokeObjectURL(url);
}

/**
 * Initializes the workflow designer with manifests, groups, and configurations.
 *
 * @async
 * 
 * @returns {Promise<void>} A promise that resolves once the designer is initialized.
 */
async function initializeDesigner() {
	// Get the HTML element where the designer will be rendered.
	const designerHtmlElement = document.getElementById('designer');

	// Initialize the workflow's starting definition with the "WriteLog" manifest.
	const initalState = initializeStartDefinition(_manifests["WriteLog"]);

	// Create a new start definition object.
	const startDefinition = newStartDefinition(initalState);

	// Initialize a new configuration for the workflow designer.
	const configuration = newConfiguration();

	// Initialize an array to hold all groups.
	const groups = [];

	// Process each manifest group to create the groups for the designer.
	for (const [groupName, manifestsGroup] of Object.entries(_manifestsGroups)) {
		// Retrieve the manifests from the group, or default to an empty array.
		const manifests = manifestsGroup.manifests ? manifestsGroup.manifests : [];

		// Create a group object with its name and steps.
		const group = {
			name: groupName,
			steps: []
		};

		// Convert each manifest into a step and add it to the group.
		for (const manifest of manifests) {
			const step = StateMachineSteps.newG4Step(manifest);
			group.steps.push(step);
		}

		// Add the group to the array of groups.
		groups.push(group);
	}

	// Retrieve or create the "Containers" group in the configuration toolbox.
	let containers = groups.find(group => group.name === 'Containers');
	let containersGroup = containers !== undefined && containers !== null
		? containers
		: { name: 'Containers', steps: [] };

	// Create default container types for "Stage" and "Job".
	const stage = StateMachineSteps.newG4Stage('Stage', {}, {}, []);
	const job = StateMachineSteps.newG4Job('Job', {}, {}, []);

	// Add the containers to the "Containers" group.
	containersGroup.steps.push(stage, job);

	// If "Containers" group doesn't exist, add it to the groups array.
	if (!containers) {
		groups.push(containersGroup);
	}

	// Sort groups alphabetically by name.
	let sortedGroups = groups.toSorted((a, b) => a.name.localeCompare(b.name));

	// Sort steps within each group alphabetically by name.
	for (const group of sortedGroups) {
		let sortedSteps = group.steps.toSorted((a, b) => a.name.localeCompare(b.name));
		group.steps = sortedSteps;
	}

	// Update the configuration toolbox with the sorted groups.
	configuration.toolbox.groups = sortedGroups;

	// Create the workflow designer using the configuration and start definition.
	_designer = sequentialWorkflowDesigner.Designer.create(designerHtmlElement, startDefinition, configuration);

	// Subscribe to definition changes in the designer.
	// This event fires whenever a step is added, removed, or updated.
	_designer.onDefinitionChanged.subscribe(async () => {

		// CSS selector: finds <g> elements that contain a rect.sqd-input
		// immediately followed by an <image> element.
		// Example target:
		// <g>
		//   <rect class="sqd-input" ... />
		//   <image ... />
		// </g>
		const iconsSelector = "g:has(> rect.sqd-input+image)";

		// CSS selector: finds <g> groups whose class contains 'sqd-step-'
		// AND that directly contain an <image> or <img> element.
		// This is used for task icons inside workflow steps.
		const tasksSelector =
			"g[class*='sqd-step-']:has(>image), g[class*='sqd-step-']:has(>img)";

		// CSS class applied to replaced SVG <path> elements
		// for input connector icon styling.
		const iconsClass = "sqd-input-icon";

		// CSS class applied to replaced SVG <path> elements
		// for task icons inside steps.
		const tasksClass = "sqd-step-task-icon";

		// Replace <image> tags under input connectors with inline SVG
		// and apply styling class to the <path> inside each SVG.
		Utilities.switchImages(iconsSelector, iconsClass, _svgsCache);

		// Replace <image>/<img> tags in step task icons with inline SVG
		// and apply styling class to the <path> inside each SVG.
		Utilities.switchImages(tasksSelector, tasksClass, _svgsCache);

		// Re-apply the run-state frames, since re-rendering the canvas recreates the step elements.
		applyRunStates();
	});

	// Listen for the "StartAutomation" reply from the server. A finished run answers with an empty result;
	// a failed or stopped run answers with the Hub's error model, whose reasons are shown to the user.
	_connection.on("StartAutomation", (response) => {
		// A stop the user asked for also ends with an "Automation stopped" reply, which is not a failure.
		if (_stateMachine.isStopRequested === true) {
			return;
		}

		showAutomationErrors(response?.errors);
	});

	// Listen for the "StopAutomation" message from the server
	_connection.on("StopAutomation", (_) => {
		console.log("Automation stopped");
	});

	// Listen for the "ReceiveAutomationEvent" message from the server
	_connection.on("ReceiveAutomationStartEvent", (message) => {
		// Convert the message type to uppercase for consistent comparison.
		const messageType = message.type.toUpperCase();

		// Exit early if the message type is not one of the included types.
		if (!_flowableTypes.includes(messageType)) {
			return;
		}

		// Check whether the step exists on the canvas. Rules expanded at runtime
		// (e.g., the nested rules of a template) carry server-generated IDs that
		// have no matching step, so selecting them would remove the current frame.
		const isOnCanvas = isStepOnCanvas(message.id);

		// Exit early to keep the current selection (e.g., the playing template) framed.
		// The owning canvas step (e.g., the template) is marked as running again while its nested rule runs.
		if (!isOnCanvas) {
			const ownerStepId = resolveCanvasStepId(message);
			if (ownerStepId) {
				setRunState(ownerStepId, "running");
			}
			return;
		}

		// Remember the step as the owner of any nested rule events that follow.
		_lastStartedStepId = message.id;

		// Color the step's frame with the neutral "running" color.
		setRunState(message.id, "running");

		// Select the step in the designer using the provided ID.
		_designer.selectStepById(message.id);

		// Adjust the viewport to bring the selected step into view.
		_designer.moveViewportToStep(message.id);

		// Increment the total actions counter.
		if (_auditableTypes.includes(messageType)) {
			_averageCounter.addOne();
			_counter.addOne();
		}
	});

	// Listen for the "ReceiveAutomationEndEvent" message from the server
	_connection.on("ReceiveAutomationEndEvent", (message) => {
		// Convert the message type to uppercase for consistent comparison.
		const messageType = message.type.toUpperCase();

		// Exit early if the message type is not one of the included types.
		if (!_flowableTypes.includes(messageType)) {
			return;
		}

		// Resolve the canvas step that owns this event (nested rules resolve to their template).
		const stepId = resolveCanvasStepId(message);

		// Exit early if the event cannot be tied to a step on the canvas.
		if (!stepId) {
			return;
		}

		// The step failed if the execution returned any exception.
		const exceptions = message?.value?.exceptions;
		const isFailed = Array.isArray(exceptions) && exceptions.length > 0;

		// Color the step's frame with the result.
		setRunState(stepId, isFailed ? "failed" : "passed");
	});

	// Listen for the "ReceiveAutomationRequestInitializedEvent" message from the server
	_connection.on("ReceiveAutomationRequestInitializedEvent", (message) => {
		console.log(message);
	});

	// Listen for the "ReceiveAutomationRequestInitializedEvent" message from the server
	_connection.on("ReceiveLogCreatedEvent", (message) => {
		console.log(message);
	});

	// Listen for the "ReceiveAutomationInvokedEvent" message from the server
	_connection.on("ReceiveAutomationInvokedEvent", (message) => {
		// Indicate that the automation is no longer running
		_stateMachine.isRunning = false;

		// Release the designer from read-only mode
		_designer.setIsReadonly(false);

		// Add another action to the average counter
		_averageCounter.addOne();

		// Stop the timer after the automation has completed
		_timer.stop();

		// Clear the frames of steps whose outcome is unknown (e.g., the run was stopped).
		clearRunningStates();

		// Convert the message object to a JSON string for logging or further processing.
		const automationResults = JSON.stringify(message?.value?.response || {});

        // Convert the automation results to a base64 string.
		// If the results are an empty object, use an empty string instead.
		const base64 = automationResults === '{}'
			? ""
			: Utilities.convertToBase64(automationResults);

		// Create a console message with a specific prefix and the base64-encoded automation results.
		const consoleMessage = '__G4_DOCUMENT_RESULT_BASE64__:' + base64;

		// Log the console message, which can be captured by external tools or scripts to retrieve the automation results.
		console.log(consoleMessage)
	});
}

/**
 * Applies the stored run state of a step to its frame on the canvas.
 *
 * Removes any previous run-state class from the step's SVG group and adds the class
 * matching the state stored in `_runStates`. Steps that are not rendered are ignored.
 *
 * @param {string} stepId - The ID of the step on the canvas.
 */
function applyRunState(stepId) {
	// Find the step's SVG group on the canvas; exit if it is not rendered.
	const element = document.querySelector(`#designer g[data-step-id="${CSS.escape(stepId)}"]`);
	if (!element) {
		return;
	}

	// Remove any previous run-state class from the step.
	element.classList.remove(...Object.values(_runStateClasses));

	// Add the class for the current run state, if the step has one.
	const state = _runStates.get(stepId);
	if (state) {
		element.classList.add(_runStateClasses[state]);
	}
}

/**
 * Applies all stored run states to the canvas. Used after the canvas re-renders.
 */
function applyRunStates() {
	for (const stepId of _runStates.keys()) {
		applyRunState(stepId);
	}
}

/**
 * Removes the "running" state from all steps.
 *
 * Called when the automation ends: a step still marked as running at that point
 * (e.g., the run was stopped) has an unknown outcome, so its frame is cleared.
 */
function clearRunningStates() {
	for (const [stepId, state] of _runStates) {
		// Skip steps that already have a final state.
		if (state !== "running") {
			continue;
		}

		// Drop the state and refresh the step's frame.
		_runStates.delete(stepId);
		applyRunState(stepId);
	}

	// No step is running anymore.
	_lastStartedStepId = null;
}

/**
 * Checks whether a step with the given ID exists in the canvas definition.
 *
 * @param {string} stepId - The step ID to look up.
 *
 * @returns {boolean} True if the step exists on the canvas; otherwise, false.
 */
function isStepOnCanvas(stepId) {
	// Treat empty IDs as not on the canvas.
	if (!stepId) {
		return false;
	}

	// The walker returns null (it does not throw) when the step is not found.
	return _designer.getWalker().findById(_designer.getDefinition(), stepId) !== null;
}

/**
 * Clears all run states and removes the run-state classes from the canvas.
 * Called when a new run starts.
 */
function resetRunStates() {
	// Forget all stored states and the last started step.
	_runStates.clear();
	_lastStartedStepId = null;

	// Remove every run-state class still present on the canvas.
	const classes = Object.values(_runStateClasses);
	const selector = classes.map(i => `#designer .${i}`).join(", ");
	for (const element of document.querySelectorAll(selector)) {
		element.classList.remove(...classes);
	}
}

/**
 * Resolves the canvas step that an automation event belongs to.
 *
 * Rules expanded at runtime (e.g., the nested rules of a template) are not on the canvas.
 * Their events belong to the nearest ancestor that is on the canvas, found by walking the
 * rule's parent references. If none is found, the last started canvas step is used, which
 * is correct because a job runs one action at a time.
 *
 * @param {Object} message - The automation event message received from the server.
 *
 * @returns {string|null} The ID of the canvas step, or null if it cannot be resolved.
 */
function resolveCanvasStepId(message) {
	// The event's own step is on the canvas.
	if (isStepOnCanvas(message.id)) {
		return message.id;
	}

	// Start events carry the rule as the value; end events wrap it in a "rule" property.
	const reference = message?.value?.reference || message?.value?.rule?.reference;

	// Walk up the parent references until a step on the canvas is found.
	let parent = reference?.parentReference;
	while (parent) {
		if (isStepOnCanvas(parent.id)) {
			return parent.id;
		}
		parent = parent.parentReference;
	}

	// Fall back to the last started canvas step.
	return _lastStartedStepId;
}

/**
 * Sets the run state of a step and updates its frame on the canvas.
 *
 * A failed state is final for the run: later states never replace it, so a template
 * stays red if any of its nested rules failed.
 *
 * @param {string} stepId - The ID of the step on the canvas.
 * @param {"running"|"passed"|"failed"} state - The new run state.
 */
function setRunState(stepId, state) {
	// Keep the failed state until the next run resets it.
	if (_runStates.get(stepId) === "failed") {
		return;
	}

	// Store the new state and refresh the step's frame.
	_runStates.set(stepId, state);
	applyRunState(stepId);
}

/**
 * Initializes the default start definition for a state machine workflow.
 *
 * @param {Object} manifest - The manifest object containing necessary metadata for the state machine steps.
 * 
 * @returns {Array} An array containing the top-level container (`stage`) with its nested structure.
 */
function initializeStartDefinition(manifest) {
	// Create the initial step using the manifest.
	let initialStep = StateMachineSteps.newG4Step(manifest);

	// Set a default value for the "Argument" property of the initial step.
	initialStep.properties["argument"]["value"] = "Foo Bar";

	// Create a job container with the initial step inside.
	// 'G4™ Default Job' is the name, 'job' is the type, an empty object represents additional properties, and `[initialStep]` is the list of steps.
	let job = StateMachineSteps.newG4Job('G4™ Default Job', {}, {}, [initialStep]);

	// Create a stage container with the job inside.
	// 'G4™ Default Stage' is the name, 'stage' is the type, and `[job]` represents the nested structure.
	let stage = StateMachineSteps.newG4Stage('G4™ Default Stage', {}, {}, [job]);

	// Return the stage as an array, as the function expects to return a list of containers.
	return [stage];
}

/**
 * Creates a new configuration object for the application.
 *
 * This function initializes the configuration settings, including toolbox setup,
 * step icon provisioning, validation rules, editor providers, and control bar settings.
 *
 * @property {number}  undoStackSize - The maximum number of undo operations allowed.
 * @property {Object}  toolbox       - Configuration for the toolbox UI component.
 * @property {Object}  steps         - Configuration related to step icons and types.
 * @property {Object}  validator     - Validation rules for steps and the root definition.
 * @property {Object}  editors       - Providers for root and step editors.
 * @property {boolean} controlBar    - Flag to enable or disable the control bar.
 * 
 * @returns {Object} The configuration object containing all necessary settings.
 */
function newConfiguration() {
	return {
		// Maximum number of undo operations the user can perform
		undoStackSize: 5,

		/**
		 * Configuration for the toolbox UI component.
		 *
		 * @property {Array} groups - An array to hold different groups within the toolbox.
		 * @property {Function} itemProvider - Function to create toolbox items based on a step.
		 */
		toolbox: {
			// Initialize with no groups; groups can be added dynamically
			groups: []
		},

		/**
		 * Configuration related to step icons and types.
		 *
		 * @property {Function} iconUrlProvider - Function to determine the icon URL based on component type and step type.
		 */
		steps: {
			/**
			 * Provides the URL for the step icon based on its component type and specific type.
			 *
			 * @param {string} componentType - The component type of the step (e.g., 'workflow').
			 * @param {string} type - The specific type of the step (e.g., 'loop', 'if').
			 *
			 * @returns {string} The URL to the corresponding SVG icon.
			 *
			 * @example
			 * const iconUrl = newConfiguration.steps.iconUrlProvider('workflow', 'loop');
			 * console.log(iconUrl); // Outputs: './images/icon-loop.svg'
			 */
			iconUrlProvider: (_, type) => {
				// Extract unique icon providers from the _manifests object.
				const supportedIcons = Array.from(
					new Set(
						Object.values(_manifests).map(manifest =>
							manifest?.context?.integration?.sequentialWorkflow?.iconProvider?.name || 'gears'
						)
					)
				);

				// Determine the iconType based on the input 'type'.
				// If the provided type is not in the list of supportedIcons, default to 'gears'.
				let iconType = supportedIcons.includes(type) ? type : 'gears';

				// Convert the input type to uppercase for case-insensitive comparisons.
				const upperType = type.toUpperCase();

				// If the type is 'STAGE', force iconType to 'layer-group'.
				if (upperType === 'STAGE') {
					iconType = 'layer-group';
				}
				// Else if the type is 'JOB', force iconType to 'diagram-project'.
				else if (upperType === 'JOB') {
					iconType = 'diagram-project';
				}
				// Else if the type is 'JOB', force iconType to 'upload'.
				else if (upperType === 'EXPORT') {
					iconType = 'upload';
				}

				// Return the relative path to the SVG icon based on the determined iconType.
				return `${BASE_HUB_URL}/images/icon-${iconType}.svg`;
			}
		},

		/**
		 * Validation rules for steps and the root definition.
		 *
		 * @property {Function} step - Validates individual step properties.
		 * @property {Function} root - Validates the root definition properties.
		 */
		validator: {
			/**
			 * Validates that all properties of a step are truthy (i.e., not null, undefined, false, 0, or '').
			 *
			 * @param {Object} step - The step object to validate.
			 * @param {Object} step.properties - An object containing the properties of the step.
			 *
			 * @returns {boolean} True if all properties are valid, false otherwise.
			 *
			 * @example
			 * const step = { properties: { name: 'Loop', count: 3 } };
			 * const isValid = validator.step(step);
			 * console.log(isValid); // Outputs: true
			 */
			step: (step, _, definition) => {
                // Get the ID of the currently selected step in the designer
				const id = _designer.getSelectedStepId();

				// If the step is not selected, return true (no validation needed)
				if ((!id || id !== step.id) && step.sequence && step.sequence.length > 0) {
                    step.context.errors = step.context.errors || {};
					return Object.keys(step.context.errors).length === 0
						&& !step?.categories?.toUpperCase().includes("G-ERROR");
				}

				// Array to collect each validation result (true = pass, false = fail)
				const assertions = [];

				// Check that the rule step is placed under a Job container somewhere
				assertions.push(
					Validators.confirmRulePlacement(step, definition)
				);

				// Ensure all required step properties are present and valid
				assertions.push(
					Validators.confirmStepProperties(step)
				);

				// Verify that Content rules are correctly nested under an ExportData plugin
				assertions.push(
					Validators.confirmContentRule(step, definition)
				);

				// Confirm that the step appears in a valid Stage context if needed
				assertions.push(
					Validators.confirmStagePlacement(step, definition)
				);

				// Validate that Job-specific placement rules are satisfied
				assertions.push(
					Validators.confirmJobPlacement(step, definition)
				);

				// Validate the step.
				const isValid = assertions.every(assertion => assertion)
					&& !step?.categories?.toUpperCase().includes("G-ERROR");

				// emit after validating
				document.dispatchEvent(new CustomEvent(STEP_VALIDATED, {
					detail: { step, definition, isValid }
				}));

				// Only return true if every assertion passed AND the step is not marked with a "G-ERROR" category
				return isValid;
			},

			/**
			 * Validates that the 'speed' property of the root definition is greater than 0.
			 *
			 * @param {Object} definition - The root definition object to validate.
			 * @param {number} definition.properties.speed - The speed property to validate.
			 *
			 * @returns {boolean} True if the 'speed' property is greater than 0, false otherwise.
			 *
			 * @example
			 * const definition = { properties: { speed: 10 } };
			 * const isValid = validator.root(definition);
			 * console.log(isValid); // Outputs: true
			 */
			root: definition => {
				// Ensure that the 'speed' property exists and is greater than 0
				return definition.properties['speed'] > 0;
			}
		},

		/**
		 * Providers for root and step editors.
		 *
		 * @property {Function} rootEditorProvider - Function to provide the editor for the root definition.
		 * @property {Function} stepEditorProvider - Function to provide the editor for individual steps.
		 */
		editors: {
			rootEditorProvider,
			stepEditorProvider
		},

		// Flag to enable the control bar in the UI
		controlBar: true
	};
}

/**
 * Opens the Import Definition editor: a large JSON editor in a modal, where a definition is typed,
 * pasted, or imported from a file, and then applied to the designer.
 *
 * The editor checks the JSON while typing and offers Format. Apply refuses invalid JSON and keeps
 * the error in view; an empty editor applies an empty definition, as before. Escape or the corner
 * button closes the editor without applying anything.
 */
function newImportModal() {
	// SVG namespace for the header button icons.
	const SVG_NAMESPACE = 'http://www.w3.org/2000/svg';

	// Import icon (arrow into a tray), embedded here for this editor only.
	const IMPORT_ICON_PATH = 'M352 96C352 78.3 337.7 64 320 64C302.3 64 288 78.3 288 96L288 306.7L246.6 265.3C234.1 252.8 213.8 252.8 201.3 265.3C188.8 277.8 188.8 298.1 201.3 310.6L297.3 406.6C309.8 419.1 330.1 419.1 342.6 406.6L438.6 310.6C451.1 298.1 451.1 277.8 438.6 265.3C426.1 252.8 405.8 252.8 393.3 265.3L352 306.7L352 96zM160 384C124.7 384 96 412.7 96 448L96 480C96 515.3 124.7 544 160 544L480 544C515.3 544 544 515.3 544 480L544 448C544 412.7 515.3 384 480 384L433.1 384L376.5 440.6C345.3 471.8 294.6 471.8 263.4 440.6L206.9 384L160 384zM464 440C477.3 440 488 450.7 488 464C488 477.3 477.3 488 464 488C450.7 488 440 477.3 440 464C440 450.7 450.7 440 464 440z';

	// Apply icon (document with a check mark), embedded here for this editor only.
	const APPLY_ICON_PATH = 'M128 64C92.7 64 64 92.7 64 128L64 512C64 547.3 92.7 576 128 576L308 576C285.3 544.5 272 505.8 272 464C272 363.4 349.4 280.8 448 272.7L448 234.6C448 217.6 441.3 201.3 429.3 189.3L322.7 82.7C310.7 70.7 294.5 64 277.5 64L128 64zM389.5 240L296 240C282.7 240 272 229.3 272 216L272 122.5L389.5 240zM608 464C608 384.5 543.5 320 464 320C384.5 320 320 384.5 320 464C320 543.5 384.5 608 464 608C543.5 608 608 543.5 608 464zM521.4 403.1C528.5 408.3 530.1 418.3 524.9 425.4L460.9 513.4C458.1 517.2 453.9 519.6 449.2 519.9C444.5 520.2 439.9 518.6 436.6 515.3L396.6 475.3C390.4 469.1 390.4 458.9 396.6 452.7C402.8 446.5 413 446.5 419.2 452.7L446 479.5L499 406.6C504.2 399.5 514.2 397.9 521.4 403.1z';

	/**
	 * Creates one icon-only header button of the Import Definition editor.
	 *
	 * @param {Object} buttonOptions - Button icon, tooltip, and test id.
	 * @param {string} buttonOptions.iconPath - SVG path data of the icon (640 x 640 view box).
	 * @param {string} buttonOptions.testId - Value of data-test-id.
	 * @param {string} buttonOptions.title - Tooltip and accessible name.
	 * @returns {HTMLButtonElement} The button, styled as a modal header tool.
	 */
	const newHeaderButton = (buttonOptions) => {
		const buttonElement = document.createElement('button');
		const svgElement = document.createElementNS(SVG_NAMESPACE, 'svg');
		const pathElement = document.createElementNS(SVG_NAMESPACE, 'path');

		// The icon takes the button's text colour, so it follows the theme.
		svgElement.setAttribute('viewBox', '0 0 640 640');
		svgElement.setAttribute('aria-hidden', 'true');
		pathElement.setAttribute('fill', 'currentColor');
		pathElement.setAttribute('d', buttonOptions.iconPath);
		svgElement.append(pathElement);

		// Icon only: the tooltip and the accessible name say what the button does.
		buttonElement.type = 'button';
		buttonElement.className = 'g4-modal__tool';
		buttonElement.title = buttonOptions.title;
		buttonElement.dataset.testId = buttonOptions.testId;
		buttonElement.setAttribute('aria-label', buttonOptions.title);
		buttonElement.append(svgElement);

		return buttonElement;
	};

	// Only one Import Definition editor is open at a time; a second click keeps the open one.
	if (document.querySelector('g4-modal[test-id="import-definition"]')) {
		return;
	}

	// Watch the workspace, so the view can be reset once the new definition is drawn.
	const workspaceObserver = new Observer(document.querySelector('.sqd-workspace'));

	// The editor and its header tools: Import reads a file, Apply sends the definition to the designer.
	const editor = document.createElement('g4-expand-editor');
	const fileInputElement = document.createElement('input');
	const importButtonElement = newHeaderButton({
		iconPath: IMPORT_ICON_PATH,
		testId: 'import-definition-import-file-button',
		title: 'Import a definition file (.json or .txt)'
	});
	const applyButtonElement = newHeaderButton({
		iconPath: APPLY_ICON_PATH,
		testId: 'import-definition-apply-button',
		title: 'Apply the definition to the designer'
	});

	// A file picker that is never shown: the Import button opens it.
	fileInputElement.type = 'file';
	fileInputElement.accept = '.txt,.json';

	/**
	 * Shows the picked file's text in the editor, checked like typed text.
	 */
	const onFileInputChange = () => {
		const file = fileInputElement.files[0];

		if (!file) {
			return;
		}

		// Read the file as text, then show it with its line numbers and JSON check.
		const reader = new FileReader();

		reader.addEventListener('load', () => {
			editor.setText(typeof reader.result === 'string' ? reader.result : '');
		});

		reader.readAsText(file);
	};

	/**
	 * Applies the definition and closes the editor; invalid JSON keeps the editor open.
	 */
	const onApplyClick = () => {
		// The error line already says what is wrong; nothing half-parsed may reach the designer.
		if (editor.error !== '') {
			return;
		}

		// An empty editor applies an empty definition, as the import did before.
		const text = editor.value;
		const definition = text.trim() === '' ? {} : JSON.parse(text);

		// Close first, then draw the new definition and fit the view to it.
		editor.close();
		setDefinition(definition);
		resetView(workspaceObserver);
	};

	fileInputElement.addEventListener('change', onFileInputChange);

	importButtonElement.addEventListener('click', () => {
		fileInputElement.click();
	});

	applyButtonElement.addEventListener('click', onApplyClick);

	// Open the editor in JSON mode, and give focus back to the control bar button when it closes.
	editor.setAttribute('editor-title', 'Import Definition');
	editor.setAttribute('test-id', 'import-definition');
	editor.open({
		focusReturnElement: document.activeElement,
		isJson: true,
		placeholder: 'Type, paste or import a definition here...',
		text: '',
		tools: [importButtonElement, applyButtonElement]
	});
}

/**
 * Creates a new start definition object for a workflow with default properties.
 *
 * @param {Array} sequence - The sequence of steps or containers to include in the start definition.
 * 
 * @returns {Object} An object representing the start definition, containing default properties and the provided sequence.
 */
function newStartDefinition(sequence) {
	// Authentication token for G4™ requests
	const token = "";

	// Generate a unique identifier for the start definition.
	const id = Utilities.newUid();

	// Return the start definition object with default properties and the provided sequence.
	return {
		id,
		properties: {
			authentication: {
				token: token,
				password: '',
				username: '',
			},
			driverParameters: {
				driver: "ChromeDriver",
				driverBinaries: ".",
				capabilities: {
					"alwaysMatch": {
						"browserName": "chrome",
						"goog:chromeOptions": {
							"args": [
								"--disable-gpu"
							]
						}
					}
				},
				firstMatch: [
					{}
				]
			},
			settings: {
				automationSettings: {
					loadTimeout: 60000,
					maxParallel: 1,
					returnStructuredResponse: false,
					searchTimeout: 15000,
				},
				environmentsSettings: {
					defaultEnvironment: "SystemParameters",
					returnEnvironment: false
				},
				screenshotsSettings: {
					autoScreenshots: false,
					outputFolder: ".",
					convertToBase64: false,
					onExceptionOnly: false,
					returnScreenshots: false
				}
			},
			speed: 300
		},
		// The provided sequence of steps or containers.
		sequence
	};
}

/**
 * Provides an editor interface for the root configuration of the workflow.
 *
 * @param {Object}  definition    - The definition object containing the properties and settings for the workflow.
 * @param {Object}  editorContext - Context object for notifying changes to the editor.
 * @param {boolean} isReadonly    - Flag indicating if the editor should be in read-only mode.
 * 
 * @returns {HTMLElement} A container element housing the root editor fields.
 */
function rootEditorProvider(definition, editorContext, isReadonly) {
	// Create the main container div element for the root editor.
	const container = document.createElement('div');
	container.setAttribute("data-g4-role", "root-editor");

	// Add a title to the container to indicate the configuration section.
	CustomFields.newTitle({
		container: container,
		helpText: 'Configure the automation settings for the flow.',
		subTitleText: 'Flow Configuration',
		titleText: 'Automation Settings'
	});

	// Add a string input field for configuring the "Invocation Interval".
	CustomFields.newStringField(
		{
			container: container,
			initialValue: definition.properties['speed'],
			isReadonly: isReadonly,
			label: 'Invocation Interval (ms)',
			title: 'Time between each action invocation'
		},
		(value) => {
			// Update the "speed" property with the new value from the input.
			definition.properties['speed'] = parseInt(value, 10); // Ensure the value is an integer.
			editorContext.notifyPropertiesChanged();
		}
	);

	// Add a string input field for configuring the "Invocation Interval".
	CustomFields.newStringField(
		{
			container: container,
			initialValue: definition.properties['title'],
			isReadonly: isReadonly,
			label: 'Title',
			title: 'Title of the workflow'
		},
		(value) => {
			// Update the "title" property with the new value from the input.
			definition.properties['title'] = value;
			editorContext.notifyPropertiesChanged();
		}
	);

	// Add an authentication field for providing G4 credentials to allow automation requests.
	CustomG4Fields.newAuthenticationField(
		{
			container: container,
			label: "Authentication",
			title: "Provide G4™ credentials to allow automation requests.",
			initialValue: definition.properties['authentication']
		},
		(value) => {
			// Ensure the "authentication" property exists in the definition.
			definition.properties['authentication'] = definition.properties['authentication'] || {};

			// Update the "authentication" property with the new values from the input.
			for (const key of Object.keys(value)) {
				definition.properties['authentication'][key] = value[key];
			}

			// Notify the editor of the updated properties.
			editorContext.notifyPropertiesChanged();
		}
	);

	// Add a data source field for configuring the G4 data source settings.
	CustomG4Fields.newDataSourceField(
		{
			container: container,
			label: "Data Source",
			title: "Provide G4™ data source to configure the automation.",
			initialValue: definition.properties['dataSource']
		},
		(value) => {
			// Ensure the "dataSource" property exists in the definition.
			definition.properties['dataSource'] = definition.properties['dataSource'] || {};

			// Update the "dataSource" property with the new values from the input.
			for (const key of Object.keys(value)) {
				definition.properties['dataSource'][key] = value[key];
			}

			// Notify the editor of the updated properties.
			editorContext.notifyPropertiesChanged();
		}
	);

	// Add a driver parameters field for configuring the G4 driver parameters.
	CustomG4Fields.newDriverParametersField(
		{
			container: container,
			label: "Driver Parameters",
			title: "Provide G4™ driver parameters to configure the automation.",
			initialValue: definition.properties['driverParameters']
		},
		/**
		 * Callback function to handle updates to the Driver Parameters field.
		 *
		 * This function processes the input `value` and updates the `definition.properties`
		 * accordingly. It ensures that the `capabilities` structure is correctly maintained
		 * and merges new values into existing configurations. After processing, it notifies
		 * the editor that properties have changed.
		 *
		 * @param {Object} value - The updated Driver Parameters provided by the user.
		 */
		(value) => {
			// Ensure the 'driverParameters' property exists in the definition.
			definition.properties['driverParameters'] = definition.properties['driverParameters'] || {};

			// Ensure the 'capabilities' object exists within 'driverParameters'.
			definition.properties['driverParameters']['capabilities'] = definition.properties['driverParameters']['capabilities'] || {};

			// Ensure the 'firstMatch' object exists within 'capabilities'.
			definition.properties['driverParameters']['firstMatch'] = definition.properties['driverParameters']['firstMatch'] || {};

			// Iterate over each key in the provided `value` object.
			for (const key of Object.keys(value)) {
				// Determine if the current key pertains to 'capabilities' with 'firstMatch'.
				const isFirstMatch = key.toUpperCase() === 'FIRSTMATCH';

				// Determine if the current key pertains to 'capabilities' with 'alwaysMatch'.
				const isAlwaysMatch = key.toUpperCase() === 'CAPABILITIES' && 'alwaysMatch' in value[key];

				// Reference to the existing 'capabilities' object for easy access.
				const capabilities = definition.properties['driverParameters'].capabilities;

				if (isFirstMatch) {
					// Extract the 'firstMatch' object from the input value.
					const firstMatch = value.firstMatch;

					// Iterate over each group in 'firstMatch' and merge it into the existing capabilities.
					for (const group of Object.keys(firstMatch)) {
						definition.properties['driverParameters']['firstMatch'][group] = firstMatch[group];
					}

					// Continue to the next key as this one has been processed.
					continue;
				}

				if (isAlwaysMatch) {
					// Assign the 'alwaysMatch' object directly to the capabilities.
					capabilities['alwaysMatch'] = value[key].alwaysMatch;

					// Continue to the next key as this one has been processed.
					continue;
				}

				// For all other keys, assign the value directly to 'driverParameters'.
				definition.properties['driverParameters'][key] = value[key];
			}

			// Notify the editor that the properties have been updated.
			editorContext.notifyPropertiesChanged();
		}
	);

	// Add an automation settings field for configuring the automation settings.
	CustomG4Fields.newAutomationSettingsField(
		{
			container: container,
			label: "G4™ Automation Settings",
			title: "Provide G4™ automation settings to configure the automation.",
			initialValue: definition.properties.settings?.automationSettings || {}
		},
		(value) => {
			// Ensure the "settings" property exists in the definition.
			definition.properties.settings = definition.properties.settings || {};

			// Ensure the "automationSettings" property exists in the definition.
			definition.properties.settings.automationSettings = definition.properties.settings.automationSettings || {};

			// Update the "automationSettings" property with the new values from the input.
			for (const key of Object.keys(value)) {
				definition.properties.settings.automationSettings[key] = value[key];
			}

			// Notify the editor of the updated properties.
			editorContext.notifyPropertiesChanged();
		}
	);

	// Add an environment settings field for configuring the G4 environment settings.
	CustomG4Fields.newEnvironmentsSettingsField(
		{
			container: container,
			label: "G4™ Environments Settings",
			title: "Provide G4™ environment settings to configure the automation.",
			initialValue: definition.properties.settings?.environmentsSettings
		},
		(value) => {
			// Ensure the "settings" property exists in the definition.
			definition.properties.settings = definition.properties.settings || {};

			// Ensure the "environmentsSettings" property exists in the definition.
			definition.properties.settings.environmentsSettings = definition.properties.settings.environmentsSettings || {};

			// Update the "authentication" property with the new values from the input.
			for (const key of Object.keys(value)) {
				definition.properties.settings.environmentsSettings[key] = value[key];
			}

			// Notify the editor of the updated properties.
			editorContext.notifyPropertiesChanged();
		}
	);

	// Add an exceptions settings field for configuring the G4 exceptions settings.
	CustomG4Fields.newExceptionsSettingsField(
		{
			container: container,
			label: "G4™ Exceptions Settings",
			title: "Provide G4™ exceptions settings to configure the automation.",
			initialValue: definition.properties.settings?.exceptionsSettings
		},
		(value) => {
			// Ensure the "settings" property exists in the definition.
			definition.properties.settings = definition.properties.settings || {};

			// Ensure the "exceptionsSettings" property exists in the definition.
			definition.properties.settings.exceptionsSettings = definition.properties.settings.exceptionsSettings || {};

			// Update the "exceptionsSettings" property with the new values from the input.
			for (const key of Object.keys(value)) {
				definition.properties.settings.exceptionsSettings[key] = value[key];
			}

			// Notify the editor of the updated properties.
			editorContext.notifyPropertiesChanged();
		}
	);

	// Add a queue manager settings field for configuring the G4 queue manager settings.
	CustomG4Fields.newQueueManagerSettingsField(
		{
			container: container,
			label: "G4™ Queue Manager Settings",
			title: "Provide G4™ queue manager settings to configure the automation.",
			initialValue: definition.properties.settings?.queueManagerSettings
		},
		(value) => {
			// Ensure the "settings" property exists in the definition.
			definition.properties.settings = definition.properties.settings || {};

			// Ensure the "queueManagerSettings" property exists in the definition.
			definition.properties.settings.queueManagerSettings = definition.properties.settings.queueManagerSettings || {};

			// Update the "queueManagerSettings" property with the new values from the input.
			for (const key of Object.keys(value)) {
				definition.properties.settings.queueManagerSettings[key] = value[key];
			}

			// Notify the editor of the updated properties.
			editorContext.notifyPropertiesChanged();
		}
	);

	// Add a performance points settings field for configuring the G4 performance points settings.
	CustomG4Fields.newPerformancePointsSettingsField(
		{
			container: container,
			label: "G4™ Performance Points Settings",
			title: "Provide G4™ performance points settings to configure the automation.",
			initialValue: definition.properties.settings?.performancePointsSettings
		},
		(value) => {
			// Ensure the "settings" property exists in the definition.
			definition.properties.settings = definition.properties.settings || {};

			// Ensure the "performancePointsSettings" property exists in the definition.
			definition.properties.settings.performancePointsSettings = definition.properties.settings.performancePointsSettings || {};

			// Update the "performancePointsSettings" property with the new values from the input.
			for (const key of Object.keys(value)) {
				definition.properties.settings.performancePointsSettings[key] = value[key];
			}

			// Notify the editor of the updated properties.
			editorContext.notifyPropertiesChanged();
		}
	);

	// Add a plugins settings field for configuring the G4 plugins settings.
	CustomG4Fields.newPluginsSettingsField(
		{
			container: container,
			label: "G4™ Plugins Settings",
			title: "Provide G4™ plugins settings to configure the automation.",
			initialValue: definition.properties.settings?.pluginsSettings
		},
		(value) => {
			// Ensure the "settings" property exists in the definition.
			definition.properties.settings = definition.properties.settings || {};

			// Initialize pluginsSettings if it doesn't exist
			definition.properties.settings.pluginsSettings = definition.properties.settings.pluginsSettings || {
				externalRepositories: {},
				forceRuleReference: false
			};

			// Reference to the current plugins settings
			const pluginsSettings = definition.properties.settings.pluginsSettings;

			// Get all keys from the incoming value
			const indexes = Object.keys(value) || [];

			// Iterate over each index in the incoming value
			for (const index of indexes) {
				const property = value[index];

				// If the property is not an object, set the pluginsSettings to the property
				if (!Utilities.assertObject(property)) {
					pluginsSettings[index] = property;
					continue;
				}

				// If the property is null or undefined, delete the property from the definition
				// This is done to ensure that the property is not set to null or undefined
				// as it would be set to null or undefined in the definition
				if (!property) {
					delete value[index];
					continue;
				}

				// Iterate over each key within the current property
				for (const key of Object.keys(property)) {
					const propertyValue = property[key];
					const propertyKeys = Object.keys(propertyValue) || [];

					// Iterate over each property key to set the corresponding pluginsSettings
					for (const propertyKey of propertyKeys) {
						pluginsSettings[key] = pluginsSettings[key] || {};
						pluginsSettings[key][index] = pluginsSettings[key][index] || {};
						pluginsSettings[key][index][propertyKey] = property[key][propertyKey];
					}
				}
			}

			// Update the definition with the new plugins settings
			definition.properties.settings.pluginsSettings = pluginsSettings;

			// Notify the editor that the properties have changed
			editorContext.notifyPropertiesChanged();
		}
	);

	// Add a screenshots settings field for configuring the G4 screenshots settings.
	CustomG4Fields.newScreenshotsSettingsField(
		{
			container: container,
			label: "G4™ Screenshots Settings",
			title: "Provide G4™ screenshots settings to configure the automation.",
			initialValue: definition.properties.settings?.screenshotsSettings
		},
		(value) => {
			// Ensure the "settings" property exists in the definition.
			definition.properties.settings = definition.properties.settings || {};

			// Ensure the "screenshotsSettings" property exists in the definition.
			definition.properties.settings.screenshotsSettings = definition.properties.settings.screenshotsSettings || {};

			// Update the "screenshotsSettings" property with the new values from the input.
			for (const key of Object.keys(value)) {
				definition.properties.settings.screenshotsSettings[key] = value[key];
			}

			// Notify the editor of the updated properties.
			editorContext.notifyPropertiesChanged();
		}
	);

	// Return the fully constructed container with all added elements.
	return container;
}

/**
 * Initializes and starts the workflow definition. This function checks whether the
 * designer is in a valid and editable state, then sets it to read-only mode before
 * creating and invoking the state machine that processes the workflow.
 *
 * @async
 * @function startDefinition
 * 
 * @returns {Promise<void>} Resolves when the workflow has been fully executed.
 */
async function startDefinition() {
	// Check if the designer is in read-only mode - indicating the workflow is running
	// If it is, exit the function early
	if (_designer.isReadonly()) {
		return;
	}

	// Check if the designer is valid before proceeding
	// If it is not, display an alert and exit the function
	if (!_designer.isValid()) {
		console.error(
			"⚠️ Workflow validation failed! " +
			"Your definition contains errors that must be addressed. " +
			"The sequence might still run, but leaving these issues uncorrected " +
			"risks outright failures or unpredictable behavior. " +
			"Please review and fix the highlighted steps now."
		);
	}

	// Set the designer to read-only mode to prevent further editing while the workflow is running
	_designer.setIsReadonly(true);

	// Reset the run-state frames left from the previous run
	resetRunStates();

	// Retrieve the current definition from the designer
	const definition = _designer.getDefinition();

	// Initialize the state machine with the definition and handler objects created above
	_stateMachine = new StateMachine(definition);

	// Start the workflow execution using the state machine instance
	// created above and wait for it to complete
	await _stateMachine.start();
}

/**
 * Asynchronously stops the current state machine execution.
 *
 * Sends an interrupt signal to the state machine, causing it to
 * halt its current workflow gracefully.
 *
 * @async
 * @function stopDefinition
 * @returns {Promise<void>} Resolves once the interrupt has been issued.
 */
async function stopDefinition() {
    // Check if the state machine instance exists before attempting to stop it
	if (!_stateMachine.interrupt) {
		return;
	}

	// Send an interrupt to the state machine to stop its execution flow
	_stateMachine.interrupt();
}

/**
 * Shows the reasons of a failed automation run in a dialog over the designer.
 *
 * The Hub reports a failed run on the "StartAutomation" reply with an error model whose errors map an
 * area, for example "Automation", to its messages; the state machine uses the same shape for a run that
 * never reached the Hub. A reply without messages (a finished run) shows nothing. Only one dialog is open
 * at a time, so a burst of failures does not stack dialogs; closing it returns focus to the control that
 * was active when it opened.
 *
 * @function showAutomationErrors
 * @param {Object<string, string[]>} [errors] - The error messages keyed by area, as the Hub sends them.
 */
function showAutomationErrors(errors) {
	// Collect the non-empty messages of every area, so the dialog lists every reason the Hub gave.
	const messages = Object
		.values(errors ?? {})
		.flat()
		.filter((message) => typeof message === 'string' && message.trim() !== '');

	// A reply without messages is a finished run, so there is nothing to report.
	if (messages.length === 0) {
		return;
	}

	// Keep the reasons in the console as well, next to the designer's other run logs.
	console.error('Automation failed:', messages.join(' '));

	// Keep the dialog that is already open, so repeated failures do not stack dialogs.
	const modalTestId = 'automation-error';
	const isDialogOpen = document.querySelector(`g4-modal[test-id="${modalTestId}"]`) !== null;

	if (isDialogOpen) {
		return;
	}

	// List each reason as its own item, so long engine messages stay readable.
	const listElement = document.createElement('ul');
	listElement.dataset.testId = 'automation-error-message-list';

	for (const message of messages) {
		const itemElement = document.createElement('li');
		itemElement.dataset.testId = 'automation-error-message';
		itemElement.textContent = message;
		listElement.append(itemElement);
	}

	// Open the dialog over the designer and give focus back to the active control when it closes.
	const modalElement = document.createElement('g4-modal');
	modalElement.setAttribute('modal-title', 'Automation Error');
	modalElement.setAttribute('test-id', modalTestId);
	modalElement.append(listElement);
	modalElement.show(document.activeElement);
}

/**
 * Provides a step editor UI component for a given plugin step.
 *
 * This function creates and configures the HTML structure necessary for editing a plugin step,
 * including sections for the plugin's name, properties, and parameters. It utilizes helper functions
 * from the `CustomFields` and other modules to generate form fields dynamically based on the
 * provided step's configuration.
 *
 * @param {Object} step          - The plugin step configuration object.
 * @param {Object} editorContext - The context object for the editor, used for notifying changes.
 * 
 * @returns {HTMLElement} The fully populated step editor container element.
 */
function stepEditorProvider(step, editorContext) {
	// Normalize capabilities for older in-memory steps so per-field Base64 state always has a writable owner.
	step.capabilities = step.capabilities || {};

	/**
	 * Tests whether a bridge field is rendered through newStringField.
	 *
	 * @param {Object} field - The property or parameter bridge object to inspect.
	 * @returns {boolean} True when no specialized field renderer owns the bridge object.
	 */
	const testStringField = (field) => {
		const fieldType = field?.type?.toUpperCase() || 'STRING';
		const isArrayField = fieldType === 'ARRAY';
		const isDataListField = _cacheKeys.includes(fieldType);
		const isKeyValueField = ['KEY/VALUE', 'KEYVALUE', 'DICTIONARY', 'OBJECT'].includes(fieldType);
		const isOptionsField = Array.isArray(field?.values) && field.values.length > 0;
		const isSwitchField = ['SWITCH', 'BOOLEAN', 'BOOL'].includes(fieldType);
		const isSpecializedField = isArrayField
			|| isDataListField
			|| isKeyValueField
			|| isOptionsField
			|| isSwitchField;

		return !isSpecializedField;
	};

	/**
	 * Tests whether a named bridge field is both visible and rendered as a string field.
	 *
	 * @param {'parameters'|'properties'} groupName - The bridge collection that owns the field.
	 * @param {string} fieldName - The stable field name within the bridge collection.
	 * @returns {boolean} True when the field receives an independent Base64 toggle.
	 */
	const testVisibleStringField = (groupName, fieldName) => {
		const group = step[groupName] || {};
		const normalizedFieldName = fieldName.toUpperCase();
		const isParameterCollectionPopulated = Object.keys(step.parameters || {}).length > 0;
		const isArgumentHidden = groupName === 'properties'
			&& isParameterCollectionPopulated
			&& normalizedFieldName === 'ARGUMENT';
		const isStructuralProperty = groupName === 'properties'
			&& ['RULES', 'TRANSFORMERS', 'DATACOLLECTOR'].includes(normalizedFieldName);

		if (isArgumentHidden || isStructuralProperty) {
			return false;
		}

		return testStringField(group[fieldName]);
	};

	/**
	 * Initializes the authoritative per-field Base64 registry.
	 *
	 * Existing registries are normalized without inspecting their values. A legacy
	 * rule-level true flag deterministically migrates every visible string field to
	 * the encoded registry because the former contract could only mean "all".
	 */
	const initializeBase64EncodedFields = () => {
		const currentRegistry = step.capabilities.base64EncodedFields;
		const isCurrentRegistryObject = Utilities.assertObject(currentRegistry)
			&& !Array.isArray(currentRegistry);

		const getNormalizedFieldNames = (groupName) => {
			const fieldNames = currentRegistry?.[groupName];

			if (!Array.isArray(fieldNames)) {
				return [];
			}

			return [...new Set(fieldNames.filter(fieldName => typeof fieldName === 'string'))]
				.sort((leftName, rightName) => leftName.localeCompare(rightName));
		};

		if (isCurrentRegistryObject) {
			step.capabilities.base64EncodedFields = {
				parameters: getNormalizedFieldNames('parameters'),
				properties: getNormalizedFieldNames('properties')
			};
			delete step.capabilities.isBase64Encoded;
			return;
		}

		// Expand the legacy all-fields flag from bridge metadata only; field contents are never inspected.
		const isLegacyBase64Encoded = step.capabilities.isBase64Encoded === true;
		const newRegistry = {
			parameters: [],
			properties: []
		};

		if (isLegacyBase64Encoded) {
			newRegistry.parameters = Object.keys(step.parameters || {})
				.filter(fieldName => testVisibleStringField('parameters', fieldName));
			newRegistry.properties = Object.keys(step.properties || {})
				.filter(fieldName => testVisibleStringField('properties', fieldName));
		}

		step.capabilities.base64EncodedFields = newRegistry;
		delete step.capabilities.isBase64Encoded;
	};

	/**
	 * Tests the persisted representation state for one field identity.
	 *
	 * @param {'parameters'|'properties'} groupName - The bridge collection that owns the field.
	 * @param {string} fieldName - The stable field name within the bridge collection.
	 * @returns {boolean} True when that specific field is Base64 encoded.
	 */
	const testBase64EncodedField = (groupName, fieldName) => {
		return step.capabilities.base64EncodedFields[groupName].includes(fieldName);
	};

	/**
	 * Updates only the selected field in the Base64 registry.
	 *
	 * @param {'parameters'|'properties'} groupName - The bridge collection that owns the field.
	 * @param {string} fieldName - The stable field name within the bridge collection.
	 * @param {boolean} isBase64Encoded - The representation selected for that field.
	 */
	const setBase64EncodedField = (groupName, fieldName, isBase64Encoded) => {
		const encodedFieldNames = new Set(step.capabilities.base64EncodedFields[groupName]);

		if (isBase64Encoded) {
			encodedFieldNames.add(fieldName);
		} else {
			encodedFieldNames.delete(fieldName);
		}

		step.capabilities.base64EncodedFields[groupName] = [...encodedFieldNames]
			.sort((leftName, rightName) => leftName.localeCompare(rightName));
	};

	/**
	 * Initializes and appends the appropriate input field to the container based on the parameter type.
	 *
	 * This function dynamically creates and configures input fields within a given container
	 * for either properties or parameters of a plugin step. It determines the type of the parameter
	 * and utilizes the corresponding `CustomFields` method to generate the appropriate input field.
	 * After creation, it sets up event listeners to handle value changes and notify the editor context.
	 */
	const initializeField = (container, key, step, type) => {
		// Initialize an empty parameter object to store the current parameter's properties.
		let parameter = {};

		// Retrieve the parameter object based on the type ('properties' or 'parameters').
		if (type === 'properties') {
			parameter = step.properties[key];
		} else if (type === 'parameters') {
			parameter = step.parameters[key];
		}

		// Determine the nature of the parameter to decide which input field to create.
		const parameterType = parameter.type?.toUpperCase();
		const label = parameter?.displayName || parameter.name;
		const isListField = _cacheKeys.includes(parameterType);
		const isOptionsField = parameter.values && parameter.values.length > 0;
		const isArray = parameterType === 'ARRAY';
		const isSwitch = ['SWITCH', 'BOOLEAN', 'BOOL'].includes(parameterType);
		const isKeyValue = ['KEY/VALUE', 'KEYVALUE', 'DICTIONARY', 'OBJECT'].includes(parameterType);

		/**
		 * Handles the creation and configuration of a Key-Value input field.
		 * Updates the parameter value and notifies the editor context upon changes.
		 */
		if (isKeyValue) {
			parameter.value = Utilities.assertObject(parameter.value) ? parameter.value : {};
			CustomFields.newKeyValueField(
				{
					container: container,
					initialValue: parameter.value || {},
					label: label,
					title: parameter.description
				},
				(value) => {
					// Update the parameter's value with the new input.
					parameter.value = value;

					// Notify the editor context that properties have changed.
					editorContext.notifyPropertiesChanged();
				}
			);

			// Exit the function after creating the Key-Value field.
			return;
		}

		/**
		 * Handles the creation and configuration of a Switch (Boolean) input field.
		 * Updates the parameter value and notifies the editor context upon changes.
		 */
		if (isSwitch) {
			CustomFields.newSwitchField(
				{
					container: container,
					initialValue: parameter.value,
					label: label,
					title: parameter.description
				},
				(value) => {
					// Update the parameter's value based on the switch toggle.
					parameter.value = value;

					// Notify the editor context that properties have changed.
					editorContext.notifyPropertiesChanged();
				}
			);

			// Exit the function after creating the Switch field.
			return;
		}

		/**
		 * Handles the creation and configuration of an Array input field.
		 * Updates the parameter value and notifies the editor context upon changes.
		 */
		if (isArray) {
			CustomFields.newArrayField(
				{
					container: container,
					initialValue: parameter.value,
					label: label,
					title: parameter.description
				},
				(value) => {
					// Update the parameter's array value with the new input.
					parameter.value = value;

					// Notify the editor context that properties have changed.
					editorContext.notifyPropertiesChanged();
				}
			);

			// Exit the function after creating the Array field.
			return;
		}

		/**
		 * Handles the creation and configuration of a Data List (Dropdown) input field.
		 * Chooses between a list field or options field based on the parameter's properties.
		 * Updates the parameter value and notifies the editor context upon changes.
		 */
		if (isListField || isOptionsField) {
			const itemSource = isListField ? parameter.type : parameter.values;
			CustomFields.newDataListField(
				{
					container: container,
					initialValue: parameter.value,
					itemSource: itemSource,
					label: label,
					title: parameter.description
				},
				(value) => {
					// Update the parameter's value based on the selected option.
					parameter.value = value;

					// Notify the editor context that properties have changed.
					editorContext.notifyPropertiesChanged();
				}
			);

			// Exit the function after creating the Data List field.
			return;
		}

		/**
		 * Handles the creation and configuration of a String input field.
		 * Defaults to this type if none of the above conditions are met.
		 * Updates the parameter value and notifies the editor context upon changes.
		 */
		CustomFields.newStringField(
			{
				container: container,
				initialValue: parameter.value,
				isBase64Encoded: testBase64EncodedField(type, key),
				isReadonly: false,
				label: label,
				title: parameter.description
			},
			(value, fieldState) => {
				// Update the parameter's string value with the new input.
				parameter.value = value;

				// Persist the representation only for this property or parameter so sibling toggles remain independent.
				setBase64EncodedField(type, key, fieldState.isBase64Encoded);

				// Notify the editor context that properties have changed.
				editorContext.notifyPropertiesChanged();
			}
		);
	};

	/**
	 * Initializes the system container editor provider by adding a driver parameters field.
	 * This field allows users to configure G4™ driver parameters in the specified container.
	 */
	const initializeSystemContainerEditorProvider = (container, step) => {
		// Add a driver parameters field for configuring the G4 driver parameters.
		CustomG4Fields.newDriverParametersField(
			{
				container: container,
				label: "Driver Parameters",
				title: "Provide G4™ driver parameters to configure the automation.",
				initialValue: step.properties['driverParameters'],
				isOpen: true
			},
			/**
			 * Callback function to handle updates to the Driver Parameters field.
			 *
			 * This function processes the input `value` and updates the `definition.properties`
			 * accordingly. It ensures that the `capabilities` structure is correctly maintained
			 * and merges new values into existing configurations. After processing, it notifies
			 * the editor that properties have changed.
			 */
			(value) => {
				// Ensure the 'driverParameters' property exists in the definition.
				step.properties['driverParameters'] = step.properties['driverParameters'] || {};

				// Ensure the 'capabilities' object exists within 'driverParameters'.
				step.properties['driverParameters']['capabilities'] = step.properties['driverParameters']['capabilities'] || {};

				// Ensure the 'firstMatch' object exists within 'capabilities'.
				step.properties['driverParameters']['firstMatch'] = step.properties['driverParameters']['firstMatch'] || {};

				// Iterate over each key in the provided `value` object.
				for (const key of Object.keys(value)) {
					// Determine if the current key pertains to 'capabilities' with 'firstMatch'.
					const isFirstMatch = key.toUpperCase() === 'FIRSTMATCH';

					// Determine if the current key pertains to 'capabilities' with 'alwaysMatch'.
					const isAlwaysMatch = key.toUpperCase() === 'CAPABILITIES' && 'alwaysMatch' in value[key];

					// Reference to the existing 'capabilities' object for easy access.
					const capabilities = step.properties['driverParameters'].capabilities;

					if (isFirstMatch) {
						// Extract the 'firstMatch' object from the input value.
						const firstMatch = value.firstMatch;

						// Iterate over each group in 'firstMatch' and merge it into the existing capabilities.
						for (const group of Object.keys(firstMatch)) {
							step.properties['driverParameters']['firstMatch'][group] = firstMatch[group];
						}

						// Continue to the next key as this one has been processed.
						continue;
					}

					if (isAlwaysMatch) {
						// Assign the 'alwaysMatch' object directly to the capabilities.
						capabilities['alwaysMatch'] = value[key].alwaysMatch;

						// Continue to the next key as this one has been processed.
						continue;
					}

					// For all other keys, assign the value directly to 'driverParameters'.
					step.properties['driverParameters'][key] = value[key];
				}

				// Notify the editor that the properties have been updated.
				editorContext.notifyPropertiesChanged();
			}
		);
	}

	/**
	 * Initializes the export data editor by rendering a custom G4 data collector field.
	 */
	const initializeExportDataEditorProvider = (container, step) => {
		// Create a new custom data collector field inside the given container
		CustomG4Fields.newDataCollectorField(
			{
				container: container,
				itemSource: _dataCollectors.itemSource,
				label: "Data Collector",
				title: "Provide G4™ data collector to configure the automation.",
				initialValue: step.properties['dataCollector'].value || {}
			},
			(value) => {
				// Ensure the 'dataCollector' property exists in the step properties.
				step.properties['dataCollector'].value = step.properties['dataCollector'].value || {};

                // Update the 'dataCollector' property with the new values from the input.
				for (const key of Object.keys(value)) {
					step.properties['dataCollector'].value[key] = value[key];
				}

				// Notify the editor of the updated properties.
				editorContext.notifyPropertiesChanged();
			}
		);
	};

	/**
	 * Initializes the “containerable” toggle on a step editor.
	 * This adds a switch that lets you convert a step between a task and a container.
	 */
	const initializeContainerableEditorProvider = (container, step) => {
		// Ensure we have a context object to store our “containerable” flag
		step.context = step.context || {};

        // Create a switch field for toggling container mode
		CustomFields.newSwitchField(
			{
				container: container,
				initialValue: step.context.convertToContainer || false,
				label: 'Convert to Container',
				// Tooltip explaining container mode behavior and revert conditions
				title: 'Toggle Container Mode:\n' +
					'- When enabled, you can add child rules to this step.\n' +
					'- To revert back to Task Mode, first remove all child rules.',
				step: step,
				sequence: step.sequence || []
			},
			(value) => {
				// Normalize the switch value to a boolean
				step.context.convertToContainer = String(value).toUpperCase() === 'TRUE';

                // If the switch is turned off, we need to check the current state of the step
				const isSequenceEmpty = Array.isArray(step.sequence) && step.sequence.length === 0;

				// Prevent disabling container mode if there are existing child steps
				if (!isSequenceEmpty) {
					step.context.convertToContainer = true;
					editorContext.notifyPropertiesChanged();
					return;
				}

				// If there are no child steps and container mode is turned off,
				// convert this step into a standalone task
				if (isSequenceEmpty && !step.context.convertToContainer) {
					step.componentType = 'task';
					delete step.sequence;
					editorContext.notifyNameChanged();
					return;
				}

				// Otherwise, ensure we are in container mode:
				// clear any existing sequence and set the component type
				step.componentType = 'container';
				step.sequence = [];

				editorContext.notifyNameChanged();
			}
		);
	};

	/**
	 * Initializes a switchable editor provider on a given container for a workflow step.
	 * This adds a toggle that lets the step switch between “Task Mode” (single action)
	 * and “Branching Mode” (multiple Actions & Transformers).
	 */
	const initializeSwitchableEditorProvider = (container, step) => {
		// Ensure we have a `context` object on the step to store the toggle flag
		step.context = step.context || {};

		// Create a switch field UI for toggling between Task and Branching modes
		CustomFields.newSwitchField(
			{
				// Host element for the switch
				container: container,
				// Load saved state or default off
				initialValue: step.context.convertToSwitch || false,
				// Visible label next to the toggle
				label: 'Convert to Switch',
				// Tooltip guiding the user on what enabling/disabling will do
				title:
					'Activate Branching Mode:\n' +
					'- Allows adding Actions and Transformers to this step.\n' +
					'- To revert to Task Mode, remove all existing branches first.',
				step: step,
				// Pass existing branch names, if any
				branches: step.branches || []
			},
			(value) => {
				// Normalize the incoming value (“true”/“false” or boolean) into a boolean flag
				step.context.convertToSwitch = String(value).toUpperCase() === 'TRUE';

				// Check whether any branch holds child steps (non-null, non-empty arrays)
				const isSequenceEmpty = Object
					.values(step.branches || {})
					.every(i => i == null || (Array.isArray(i) && i.length === 0));

				// If disabling branching but branches still exist, revert the toggle back on
				if (!isSequenceEmpty) {
					step.context.convertToSwitch = true;

					// Refresh the switch UI
					editorContext.notifyPropertiesChanged();
					return;
				}

				// If toggle is off (Task Mode) and there are no branches, convert to a standalone task
				if (!step.context.convertToSwitch && isSequenceEmpty) {
					step.componentType = 'task';

					// Remove any leftover branches data
					delete step.branches;

					// Update step’s displayed name/icon
					editorContext.notifyNameChanged();
					return;
				}

				// Otherwise (enabling branching), set up Branching Mode:
				step.componentType = 'switch';
				step.branches = {};

				// For each branch name stored in context, create an empty child-step list
				(step.context.branches || []).forEach(branchName => {
					step.branches[branchName] = [];
				});

				// Refresh the step display
				editorContext.notifyNameChanged();
			}
		);
	};

	/**
	 * Initializes the Branchable Editor UI for a step.
	 */
	const initializeBranchableEditorProvider = (container, step) => {

		// Get the branch names that already exist on the step.
		const branches = Object.keys(step.branches);

		// Create a unique ID so the input field and internal elements don't collide with others.
		const inputId = Utilities.newUid();
		const escapedId = CSS.escape(inputId);

		// Create the visual box that will hold all branch-related controls.
		// This is the "Branches" group the user sees in the editor.
		const fieldContainer = newMultipleFieldsContainer(`${inputId}`, {
			labelDisplayName: 'Branches',
			role: 'branches-container',
			hintText: 'Case branches that determine which action path runs at runtime.',
			isOpen: true
		});

		// Make sure the step has a context object so the editor can track updates.
		step.context = step.context || {};

		// Create a small UI element with a text input and a button.
		// The user types a branch name and clicks "Add Branch".
		CustomFields.newButtonField(
			{
				button: {
					label: 'Add Branch',
					title: 'Add a branch that activates when the value you entered is matched.'
				},
				container: container,
				input: {
					label: 'Branch Value',
					title: 'Enter the value that will trigger this branch at runtime.'
				}
			},
			(value) => {
				// This gives us what the user typed and the ID of the input element.
				const branchValue = value.branchValue;
				const id = value.id;

				// If the user typed nothing or typed a branch that already exists,
				// we simply stop and do nothing.
				if (!branchValue || branches.includes(branchValue)) {
					return;
				}

				// Add a new empty branch to the step’s branches collection.
				step.branches[branchValue] = [];

				// Clear the input box after adding the branch.
				document.getElementById(id).value = '';

				// Tell the editor that the step has changed so the UI can refresh.
				editorContext.notifyPropertiesChanged();
				editorContext.notifyNameChanged();
			}
		);
	};

	// Generate a unique identifier for input elements within the editor.
	const inputId = Utilities.newUid();

	// Escape the generated ID to ensure it's safe for use in CSS selectors.
	const escapedId = CSS.escape(inputId);

	// Create the main container element for the step editor.
	const stepEditorContainer = document.createElement('div');

    // Assign a data attribute to identify the step editor role.
	stepEditorContainer.setAttribute("data-g4-role", "step-editor");

	// Set the tooltip for the container to provide a description of the step.
	stepEditorContainer.title = step.description;

    // Add event listener for the STEP_VALIDATED event. This event is triggered when the step is validated.
	document.addEventListener(STEP_VALIDATED, e => {
		// Check if the event's step ID matches the current step's ID.
        // If it doesn't match, we ignore the event.
		if (e.detail.step.id !== step.id) {
			return;
		}

        // Select the error elements within the step editor container.
		const errorElements = document.querySelectorAll(`div[data-g4-role="error"]`);

		// Clean up any existing error elements in the container.
		if (errorElements) {
			errorElements.forEach(i => i.remove());
		}

		/**
		 * Add an error field to the container.
		 * This field is used to display any errors related to the step.
		 * It is initialized with the step's error state.
		 * This allows users to see if there are any issues with the step configuration.
		 */
		CustomFields.newError({
			container: stepEditorContainer,
			editorContext: editorContext,
			step: e.detail.step
		});
	});

	/**
	 * Add a title section to the container.
	 * This includes the plugin's name converted from PascalCase to space-separated words,
	 * the plugin type as a subtitle, and a help text containing the step's description.
	 */
	CustomFields.newTitle({
		container: stepEditorContainer,
		titleText: step.name,// Utilities.convertPascalToSpaceCase(step.pluginName),
		subTitleText: step.pluginType,
		helpText: step.description
	});

	/**
	 * Add an error field to the container.
	 * This field is used to display any errors related to the step.
	 * It is initialized with the step's error state.
	 * This allows users to see if there are any issues with the step configuration.
	 */
	CustomFields.newError({
		container: stepEditorContainer,
        editorContext: editorContext,
        step: step
	});

	/**
	 * Add a name input field for the plugin.
	 * This field allows users to view and edit the name of the plugin.
	 * It is not read-only, enabling dynamic changes to the plugin's name.
	 */
	CustomFields.newNameField(
		{
			container: stepEditorContainer,
			initialValue: step.name,
			isReadonly: false,
			label: 'Display Name',
			title: 'The name displayed for this step. This is for visual purposes only and is not used for identification.',
			step: step
		},
		(value) => {
			// Update the step's name with the new value entered by the user.
			step.name = value;

			// Notify the editor context that the plugin name has changed.
			editorContext.notifyNameChanged();
		}
	);

	// Check if the step is a stage or job and initialize the system container editor provider
	if (step.type?.toUpperCase() === 'STAGE' || step.type?.toUpperCase() === 'JOB') {
		initializeSystemContainerEditorProvider(stepEditorContainer, step);
		return stepEditorContainer;
	}

    // If the step is of type 'EXPORT', initialize the export data editor provider.
	if (step.type?.toUpperCase() === 'EXPORT') {
		initializeExportDataEditorProvider(stepEditorContainer, step);
	}

	// Initialize rule-field representation state only after container-specific editors have exited.
	initializeBase64EncodedFields();

	// Sort the properties of the step alphabetically for consistent display.
	let sortedProperties = Object.keys(step.properties).sort((a, b) => a.localeCompare(b));

	// Determine if the step has any parameters defined.
	const hasParameters = Object.keys(step.parameters).length > 0;

	/**
	 * Create a container for the Properties section.
	 * This container includes a label, role attribute, and a hint text explaining the purpose of properties.
	 */
	const propertiesFieldContainer = newMultipleFieldsContainer(`${inputId}`, {
		labelDisplayName: 'Properties',
		role: 'properties-container',
		hintText: 'Attributes that define the structural and operational behavior of the plugin.',
		isOpen: true
	});

	// Select the specific container within the Properties section where individual property fields will be added.
	const propertiesControllerContainer = propertiesFieldContainer.querySelector(`#${escapedId}-properties-container`);

	/**
	 * Iterate through each sorted property key to initialize corresponding input fields.
	 * Certain properties like 'Argument' and 'Rules' are conditionally skipped based on the presence of parameters.
	 */
	const validProperties = [];
	for (const key of sortedProperties) {
		// Determine if the current property should be skipped.
		const skip = (hasParameters && key.toUpperCase() === 'ARGUMENT')
			|| key.toUpperCase() === 'RULES'
			|| key.toUpperCase() === 'TRANSFORMERS'
			|| key.toUpperCase() === 'DATACOLLECTOR';

		// Update the sorted properties list to exclude the skipped property.
		//sortedProperties = skip ? sortedProperties.filter((property) => property !== key) : sortedProperties;

		// Skip the property if it meets the conditions above.
		if (!skip) {
			validProperties.push(key);
			initializeField(propertiesControllerContainer, key, step, "properties");
		}
	}

	/**
	 * Append the Properties section to the main container if there are any properties to display.
	 */
	if (validProperties.length > 0) {
		stepEditorContainer.appendChild(propertiesFieldContainer);
	}

	/**
	 * Sort the parameters of the step alphabetically for consistent display.
	 */
	const sortedParameters = Object.keys(step.parameters).sort((a, b) => a.localeCompare(b));

	/**
	 * Create a container for the Parameters section.
	 * This container includes a label, role attribute, and a hint text explaining the purpose of parameters.
	 */
	const parametersFieldContainer = newMultipleFieldsContainer(`${inputId}`, {
		labelDisplayName: 'Parameters',
		role: 'parameters-container',
		hintText: "Configurable inputs that customize and control the plugin's functionality.",
		isOpen: true
	});

	// Select the specific container within the Parameters section where individual parameter fields will be added.
	const parametersControllerContainer = parametersFieldContainer.querySelector(`#${escapedId}-parameters-container`);

	/**
	 * Iterate through each sorted parameter key to initialize corresponding input fields.
	 */
	for (const key of sortedParameters) {
		initializeField(parametersControllerContainer, key, step, "parameters");
	}

	/**
	 * Append the Parameters section to the main container if there are any parameters to display.
	 */
	if (sortedParameters.length > 0) {
		stepEditorContainer.appendChild(parametersFieldContainer);
	}

    // TODO: When available, change to assert only if step context allows "Convert to Container" capability (context.isContainerable).
	if (step.pluginType === "Content" || step.context.switchable) {
		step.context.branches = step.context.branches || ["Actions", "Transformers"]
		initializeSwitchableEditorProvider(stepEditorContainer, step);
	}

	// TODO: When available, change to assert only if step context allows "Convert to Container" capability (context.isContainerable).
	if (step.context.containerable) {
		initializeContainerableEditorProvider(stepEditorContainer, step);
	}

	// A switch step gets the branch editor unless its manifest sets canEditBranches to false (e.g. SetCondition,
	// whose True/False branches are fixed).
	const isSwitch = (step.componentType || (step.context && step.context.componentType) || '').toLowerCase() === "switch";
	const canEditBranches = step.context?.canEditBranches !== false;

	if (isSwitch && canEditBranches) {
		initializeBranchableEditorProvider(stepEditorContainer, step);
	}

	/**
	 * Return the fully populated step editor container element.
	 * This element includes sections for the plugin's name, properties, and parameters.
	 */
	return stepEditorContainer;
}

/**
 * Initializes the designer application once the browser window has fully loaded.
 */
window.addEventListener('load', async () => {
	// The loader element
	const loadingIndicator = document.getElementById('loading-indicator');

	// Maximum time to wait for _cache to be populated (in milliseconds)
	const MAX_WAIT_TIME = 15000;

	// Interval between each check of _cache (in milliseconds)
	const CHECK_INTERVAL = 1000;

	/**
	 * Returns a promise that resolves after a specified delay.
	 */
	const wait = (ms) => new Promise(resolve => setTimeout(resolve, ms));

	/**
	 * Checks if the global _cache object is empty.
	 */
	const assertCacheEmpty = () => Object.keys(_cache).length === 0;

	/**
	 * Attempts to initialize the designer by ensuring that _cache contains data.
	 */
	const attemptInitializeDesigner = async () => {
		// Record the current time to track elapsed time
		const startTime = Date.now();

		// Continuously check if _cache is empty until data is available or timeout is reached
		while (assertCacheEmpty()) {
			// Calculate elapsed time
			const elapsedTime = Date.now() - startTime;

			// If the maximum wait time is exceeded, log a warning
			if (elapsedTime >= MAX_WAIT_TIME) {
				console.warn('Timeout reached. The designer could not be loaded.');
				break;
			}
			// If still within the wait time, wait for the specified interval before rechecking
			else {
				await wait(CHECK_INTERVAL);
			}
		}

		// Wait for the component templates, so the first editors the designer draws get their text boxes
		await window.G4Templates?.ready;

		// Initialize the designer after exiting the loop
		await initializeDesigner();
	};

	try {
		// Attempt to initialize the designer, awaiting the completion of the asynchronous operation
		await attemptInitializeDesigner();
	} catch (error) {
		// Handle any errors that occur during the initialization process
		console.error('An error occurred during designer initialization:', error);
	} finally {
		// After initialization (successful or not), hide the loading indicator if it exists
		if (loadingIndicator) {
			loadingIndicator.style.display = 'none';
		}
	}
});
