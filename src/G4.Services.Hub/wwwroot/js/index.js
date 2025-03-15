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
	containersGroup.steps.push(...[stage, job]);

	// If "Containers" group doesn't exist, add it to the groups array.
	if (!containers) {
		groups.push(containersGroup);
	}

	// Sort groups alphabetically by name.
	let sortedGroups = groups.toSorted((a, b) => a.name.localeCompare(b.name));

	// Sort steps within each group alphabetically by name.
	for (const group of sortedGroups) {
		let sortedSteps = group.steps.sort((a, b) => a.name.localeCompare(b.name));
		group.steps = sortedSteps;
	}

	// Update the configuration toolbox with the sorted groups.
	configuration.toolbox.groups = sortedGroups;

	// Create the workflow designer using the configuration and start definition.
	_designer = sequentialWorkflowDesigner.Designer.create(designerHtmlElement, startDefinition, configuration);

	// Listen for the "ReceiveAutomationEvent" message from the server
	_connection.on("ReceiveAutomationStartEvent", (message) => {
		// Convert the message type to uppercase for consistent comparison.
		const messageType = message.type.toUpperCase();

		// Exit early if the message type is not one of the included types.
		if (!_flowableTypes.includes(messageType)) {
			return;
		}

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

	// Listen for the "ReceiveAutomationRequestInitializedEvent" message from the server
	_connection.on("ReceiveAutomationRequestInitializedEvent", (message) => {
		console.log(message);
	});

	// TODO: write to log
	// Listen for the "ReceiveAutomationRequestInitializedEvent" message from the server
	_connection.on("ReceiveLogCreatedEvent", (message) => {
		console.log(message);
	});

	// Listen for the "ReceiveAutomationInvokedEvent" message from the server
	_connection.on("ReceiveAutomationInvokedEvent", (_) => {
		// Indicate that the automation is no longer running
		_stateMachine.isRunning = false;

		// Release the designer from read-only mode
		_designer.setIsReadonly(false);

		// Reset the designer or UI state after all steps have been processed
		_stateMachine.handler.resetDesigner();

        // Add another action to the average counter
		_averageCounter.addOne();

        // Stop the timer after the automation has completed
		_timer.stop();
	});
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
			groups: [],

			/**
			 * Creates a toolbox item DOM element based on the provided step.
			 *
			 * @param {Object} step               - The step object containing details to create the toolbox item.
			 * @param {string} step.description   - A description of the step, used as a tooltip.
			 * @param {string} step.componentType - The component type of the step, used to determine the icon.
			 * @param {string} step.type          - The specific type of the step, used to select the appropriate icon.
			 * @param {string} step.name          - The display name of the step.
			 *
			 * @returns {HTMLElement} The constructed toolbox item element.
			 *
			 * @example
			 * const step = {
			 *     description: 'Loop Step',
			 *     componentType: 'workflow',
			 *     type: 'loop',
			 *     name: 'Loop'
			 * };
			 * const toolboxItem = toolbox.itemProvider(step);
			 * document.body.appendChild(toolboxItem);
			 */
			itemProvider: (step) => {
				// Create the main container div for the toolbox item
				const item = document.createElement('div');
				item.className = 'sqd-toolbox-item';

				// If a description is provided, set it as the tooltip (title attribute)
				if (step.description) {
					item.title = step.description;
				}

				// Create the image element for the step icon
				const icon = document.createElement('img');

                // Set the alt text for the icon element
				icon.setAttribute("alt", "g4-icon");

				// Set the class name for the icon element
				icon.className = 'sqd-toolbox-item-icon';

				// Set the source of the icon using the iconUrlProvider function
				icon.src = newConfiguration.steps.iconUrlProvider(step.componentType, step.type);

				// Create the div element for the step name
				const name = document.createElement('div');
				name.className = 'sqd-toolbox-item-name';
				name.textContent = step.name; // Set the text content to the step's name

				// Append the icon and name to the main item container
				item.appendChild(icon);
				item.appendChild(name);

				// Return the fully constructed toolbox item
				return item;
			}
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
							manifest?.context?.integration?.sequentialWorkflow?.iconProvider || 'task'
						)
					)
				);

				// Determine the iconType based on the input 'type'.
				// If the provided type is not in the list of supportedIcons, default to 'task'.
				let iconType = supportedIcons.includes(type) ? type : 'task';

				// Convert the input type to uppercase for case-insensitive comparisons.
				const upperType = type.toUpperCase();

				// If the type is 'STAGE', force iconType to 'stage'.
				if (upperType === 'STAGE') {
					iconType = 'stage';
				}
				// Else if the type is 'JOB', force iconType to 'job'.
				else if (upperType === 'JOB') {
					iconType = 'job';
				}

				// Return the relative path to the SVG icon based on the determined iconType.
				return `./images/icon-${iconType}.svg`;
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
			step: step => {
				return !step?.categories?.toUpperCase().includes("G-ERROR");
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
 * Creates and displays a new import modal on the page.
 *
 * This function generates a unique identifier for the modal and its related elements,
 * removes any existing modals, and then creates a new modal that contains a hidden file input,
 * a textarea for editing/importing definitions, and a set of action buttons (Import, Apply, Close).
 * When the Apply button is clicked, the definition is processed and set.
 */
function newImportModal() {
	/**
	 * Retrieves a manifest from the cache based on the manifest name.
	 *
	 * This function iterates over the groups in the provided cache object and checks if the specified
	 * manifest name exists in any group. If found, it returns the associated `manifest` property.
	 */
	const getManifest = (cache, manifestName) => {
		// Iterate over each group in the cache by retrieving its keys.
		for (const group of Object.keys(cache)) {
			// Check if the manifest name exists in the current group.
			if (manifestName in cache[group]) {
				// Return the 'manifest' property from the matched manifest entry.
				return cache[group][manifestName].manifest;
			}
		}
	};

	/**
	 * Creates a container element with Import, Apply, and Close buttons for modal interactions.
	 *
	 * This function creates a div element that contains three buttons:
	 * - **Import**: Triggers a click on an assumed existing file input element (`inputFileElement`) to import data.
	 * - **Apply** : Retrieves JSON from a textarea within the modal, parses it, removes the modal from the DOM, and invokes a callback with the parsed value.
	 * - **Close** : Simply removes the modal element from the DOM.
	 *
	 * **Note:** The code assumes that `fieldContainer` and `inputFileElement` are available in the enclosing scope.
	 */
	const newButtonsContainerElement = (inputId, modalElement, setCallback) => {
		// Create a container for the buttons with inline-flex layout for horizontal alignment.
		const buttonsContainerElement = document.createElement("div");
		buttonsContainerElement.setAttribute("style", "display: inline-flex; gap: 0.2em; margin-bottom: 0.2em;");

		// Create the Close button.
		const closeButtonElement = document.createElement('button');
		closeButtonElement.setAttribute('id', `${inputId}-closeButton`);
		closeButtonElement.setAttribute('type', 'button');
		closeButtonElement.innerText = 'Close';

		// Add event listener to remove the modal when the Close button is clicked.
		// Remove the modal element from the field container.
		closeButtonElement.addEventListener('click', () => {
			fieldContainer.removeChild(modalElement);
		});

		// Create the Import button.
		const importButtonElement = document.createElement('button');
		importButtonElement.setAttribute('id', `${inputId}-importButton`);
		importButtonElement.setAttribute('type', 'button');
		importButtonElement.innerText = 'Import';

		// Add event listener to simulate a click on the file input element when the Import button is clicked.
		// Trigger the file input element's click event (assumed to exist in the outer scope).
		importButtonElement.addEventListener('click', () => {
			inputFileElement.click();
		});

		// Create the Apply button.
		const applyButtonElement = document.createElement('button');
		applyButtonElement.setAttribute('id', `${inputId}-applyButton`);
		applyButtonElement.setAttribute('type', 'button');
		applyButtonElement.innerText = 'Apply';

		// Add event listener to handle JSON parsing and applying the callback.
		applyButtonElement.addEventListener('click', () => {
			try {
				// Retrieve the textarea element within the modal.
				const textareaElement = modalElement.querySelector('textarea');

				// Parse the JSON from the textarea's value.
				// If the textarea is empty, default to an empty object.
				const value = !textareaElement.value || textareaElement.value === ""
					? {}
					: JSON.parse(textareaElement.value);

				// Remove the modal element from the field container.
				fieldContainer.removeChild(modalElement);

				// Invoke the callback function with the parsed JSON value.
				setCallback(value);
			}
			catch (error) {
				// Log any error encountered during parsing.
				console.error(error);
			}
		});

		// Append the buttons to the container in the order: Import, Apply, Close.
		buttonsContainerElement.appendChild(importButtonElement);
		buttonsContainerElement.appendChild(applyButtonElement);
		buttonsContainerElement.appendChild(closeButtonElement);

		// Return the container element with the buttons.
		return buttonsContainerElement;
	}

	/**
	 * Creates a hidden file input element that, when a file is selected, reads its content and
	 * sets the content as the value of the specified textarea element.
	 *
	 * This function is used to allow users to import file content (either .txt or .json)
	 * into a textarea via a hidden file input.
	 */
	const newInputFileElement = (inputId, textareaElement) => {
		// Create a new input element.
		const inputFileElement = document.createElement("input");

		// Set the input element type to "file" so that it opens a file picker.
		inputFileElement.setAttribute("type", "file");

		// Assign a unique ID to the input element.
		inputFileElement.setAttribute("id", `${inputId}-input-file`);

		// Restrict the accepted file types to .txt and .json.
		inputFileElement.setAttribute("accept", `.txt,.json`);

		// Hide the input element from the UI.
		inputFileElement.setAttribute("style", `display: none;`);

		// Add an event listener to handle file selection.
		inputFileElement.addEventListener('change', (event) => {
			// Get the first file from the file input.
			const file = event.target.files[0];

			if (file) {
				// Create a FileReader to read the file content.
				const reader = new FileReader();

				// When the file has been read, update the textarea with its content.
				reader.onload = (e) => {
					textareaElement.value = e.target.result;
				};

				// Read the file as text.
				reader.readAsText(file);
			}
		});

		// Return the configured file input element.
		return inputFileElement;
	};

	/**
	 * Creates a modal element used for import operations.
	 *
	 * This function creates and returns a div element configured as a modal.
	 * The modal is centered on the screen with a fixed position and high z-index to ensure it appears on top.
	 */
	const newModalElement = (inputId) => {
		// Create a new div element to act as the modal.
		const modalElement = document.createElement('div');

		// Set a unique ID for the modal element using the provided inputId.
		modalElement.setAttribute('id', `${inputId}-import-modal`);

		// Set a custom data attribute to indicate the role of this element.
		modalElement.setAttribute('data-g4-role', 'import-modal');

		// Apply inline styles to center the modal and set display properties.
		modalElement.setAttribute('class', 'sqd-modal');

		// Return the configured modal element.
		return modalElement;
	};

	/**
	 * Creates a textarea element configured for importing or editing definitions.
	 *
	 * The textarea is styled with a specific width, height, and other attributes for optimal use.
	 */
	const newTextareaElement = (inputId) => {
		// Create a new textarea element.
		const textareaElement = document.createElement("textarea");

		// Set a unique ID for the textarea using the provided inputId.
		textareaElement.setAttribute("id", `${inputId}-textarea`);

		// Apply inline styles to define the size of the textarea.
		textareaElement.setAttribute("style", "width: 60vw; height: 25vh;");

		// Disable text wrapping in the textarea.
		textareaElement.setAttribute("wrap", "off");

		// Disable spellchecking to prevent browser spell-check from interfering.
		textareaElement.setAttribute("spellcheck", "false");

		// Set a placeholder text to guide the user on what to do.
		textareaElement.setAttribute("placeholder", "Type, paste or import definition here...");

		// Return the configured textarea element.
		return textareaElement;
	};

	/**
	 * Observes the canvas for DOM changes and triggers a reset view action when new nodes are added.
	 *
	 * This function sets up a MutationObserver on the canvas (via _canvasObserver) to monitor
	 * changes in its DOM subtree. If new nodes are added, it selects the "Reset view" button in
	 * the control bar and triggers a click event to adjust the viewport.
	 */
	const resetView = (observer) => {
		// Configuration object for the MutationObserver.
		const config = {
			attributes: false,
			childList: true,
			subtree: true
		};

		// Start observing DOM mutations on the target node using observer.
		observer.observeDOMChanges(config, (mutationsList, observer) => {
			// Extract added nodes from each mutation record, converting NodeLists to arrays,
			// and flatten all arrays into a single array of nodes.
			const addedNodes = mutationsList.flatMap(mutation => Array.from(mutation.addedNodes));

			// If no new nodes were added, exit early.
			if (addedNodes.length === 0) {
				return;
			}

			// Select the reset view button from the control bar using its title attribute.
			const resetViewButton = document.querySelector(".sqd-control-bar div[title='Reset view']");

			// If the button exists, trigger a click event to reset the view.
			resetViewButton?.click();

            // Disconnect the observer to prevent further mutations.
			observer.disconnect();
		});
	};

	/**
	 * Sets the definition for the workflow and initializes the designer state.
	 *
	 * This function processes a given workflow definition by constructing a sequence of
	 * steps from its stages, jobs, and rules. It recursively builds each step using the
	 * `newStep` helper, synchronizes them with the client, and finally creates a new
	 * definition state that is passed to the workflow designer.
	 */
	const setDefinition = (definition) => {
		/**
		 * Retrieves driver parameters if both driver and driverBinaries are provided.
		 */
		const getDriverParameters = (driverParameters) => {
			if (!driverParameters) {
				return {};
			}

			// Check if driverBinaries exists and contains at least one element.
			const isBinaries = driverParameters?.driverBinaries && driverParameters?.driverBinaries.length > 0;

			// Check if driver exists and is a non-empty string.
			const isDriver = driverParameters?.driver && driverParameters?.driver.length > 0;
			const isFirstMatch = driverParameters?.firstMatch?.length > 0;
			const firstMatch = {};

			if (isFirstMatch) {
				
				for (let i = 0; i < driverParameters.firstMatch.length; i++) {
					const key = `${i}`;
					const value = driverParameters.firstMatch[i];
					firstMatch[key] = value
				}
			}

			driverParameters.firstMatch = firstMatch || driverParameters.firstMatch;

			// Return the original object if both conditions are met; otherwise, return an empty object.
			return isBinaries && isDriver ? driverParameters : {};
		};

		/**
		 * Recursively creates a new step from a rule.
		 *
		 * This helper function retrieves the manifest for the rule's plugin, creates a new step
		 * using the state machine factory, synchronizes it with the rule, and processes any nested
		 * rules or branches.
		 */
		const newStep = (rule) => {
			// Retrieve the manifest for the rule's plugin from the cache.
			const manifest = getManifest(_cache, rule.pluginName);

			// Create a new step using the state machine factory and the retrieved manifest.
			const step = StateMachineSteps.newG4Step(manifest);

			// Assign the name of the rule's capabilities to the step if available.
			step.name = step?.pluginName?.toUpperCase() === 'MISSINGPLUGIN'
				? `Missing Plugin (${rule?.capabilities?.displayName || step.name})`
				: rule?.capabilities?.displayName || step.name;

			// Ensure that the rule has 'rules' and 'branches' properties.
			rule.rules = rule.rules || [];
			rule.branches = rule.branches || {};

			// Determine if the rule contains nested rules or branch entries.
			const branchesKeys = Object.keys(rule.branches);
			const isRules = rule.rules.length > 0;
			const isBranches = branchesKeys.length > 0;

			// Synchronize the current step with the rule configuration.
			_client.syncStep(step, rule);

			// If no nested rules or branches exist, return the current step.
			if (!isRules && !isBranches) {
				return step;
			}

			// Process nested rules recursively if they exist.
			if (isRules) {
				step.sequence = [];
				for (const subRule of rule.rules) {
					const subStep = newStep(subRule);
					_client.syncStep(subStep, subRule);
					step.sequence.push(subStep);
				}
			}

			// Process branch rules if they exist.
			if (isBranches) {
				for (const branchKey of branchesKeys) {
					// Initialize the branch array if not already present.
					for (const subRule of rule.branches[branchKey]) {
						const subStep = newStep(subRule);
						step.branches[branchKey] = step.branches[branchKey] || [];
						_client.syncStep(subStep, subRule);
						step.branches[branchKey].push(subStep);
					}
				}
			}

			// Return the constructed step with nested rules or branches.
			return step;
		};

		/**
		 * Creates a new definition state object.
		 *
		 * This helper function generates a unique ID for the definition and assembles its properties,
		 * including authentication, data source, driver parameters, settings, and a default speed.
		 * It also incorporates the constructed sequence of steps.
		 */
		const newDefinition = (definition, sequence) => {
			// Generate a unique identifier for the new definition.
			const id = Utilities.newUid();

			// Extract the driver parameters from the definition.
			const driverParameters = definition.driverParameters;

			// Return the new definition state object with the constructed properties.
			return {
				id,
				properties: {
					authentication: definition.authentication,
					dataSource: definition.dataSource,
					driverParameters: driverParameters,
					settings: definition.settings,
					speed: 300
				},
				sequence
			};
		};

		// Initialize an empty sequence that will hold all stage steps.
		const sequence = [];

		// Ensure that the definition has a stages property.
		definition.stages = definition.stages || [];

		// Process each stage in the definition.
		for (const stage of definition.stages) {
			// Create a new stage step using the state machine factory.
			const stageStep = StateMachineSteps.newG4Stage('Stage', {}, {}, []);

			// Assign name and description from the stage reference if available.
			stageStep.name = stage?.reference?.name || stageStep.name;
			stageStep.description = stage?.reference?.description?.trim() || stageStep.description?.trim();
			stageStep.id = Utilities.newUid();
			stageStep.properties.driverParameters = getDriverParameters(stage.driverParameters);

			// Ensure that the stage has a jobs array.
			stage.jobs = stage.jobs || [];

			// Process each job within the stage.
			for (const job of stage.jobs) {
				// Create a new job step using the state machine factory.
				const jobStep = StateMachineSteps.newG4Job('Job', {}, {}, []);

				// Assign name and description from the job reference if available.
				jobStep.name = job?.reference?.name || jobStep.name;
				jobStep.description = job?.reference?.description || jobStep.description;
				jobStep.id = Utilities.newUid();
				jobStep.properties.driverParameters = getDriverParameters(job.driverParameters);

				// Ensure that the job has a rules array.
				job.rules = job.rules || [];

				// Process each rule in the job by creating and appending a new step.
				for (const rule of job.rules) {
					const step = newStep(rule);
					jobStep.sequence.push(step);
				}

				// Append the job step to the stage's sequence.
				stageStep.sequence.push(jobStep);
			}

			// Append the stage step to the overall sequence.
			sequence.push(stageStep);
		}

		// Create the new definition state object using the constructed sequence.
		const newDefinitionState = newDefinition(definition, sequence);

		// Initialize the workflow designer with the new definition state.
		_designer.state.setDefinition(newDefinitionState);
	};

	// Generate a unique identifier for this modal instance.
	const inputId = Utilities.newUid();

	// Select the target node that we want to observe for DOM changes.
	const workspaceElement = document.querySelector('.sqd-workspace');

    // Create a new MutationObserver instance to observe the canvas for DOM changes.
	const workspaceObserver = new Observer(workspaceElement);

	// Select the field container where the modal will be appended. In this case, it's the <body> element.
	const fieldContainer = document.querySelector("#designer > div");

	// Find and remove any existing modals to ensure only one is visible at a time.
	const existingModals = fieldContainer?.querySelectorAll("[id*=import-modal]");
	for (const existingModal of existingModals) {
		fieldContainer.removeChild(existingModal);
	}

	// Create a new modal element using the unique identifier.
	const modalElement = newModalElement(inputId);

	// Create a new textarea element for inputting or editing the definition.
	const textareaElement = newTextareaElement(inputId);

	// Create a hidden file input element that updates the textarea when a file is selected.
	const inputFileElement = newInputFileElement(inputId, textareaElement);

	// Create a container with Import, Apply, and Close buttons.
	const buttonsContainerElement = newButtonsContainerElement(inputId, modalElement, (definition) => {
        // Set the definition using the parsed JSON value.
		setDefinition(definition);

		// Reset the view to adjust the viewport after setting the new definition.
		resetView(workspaceObserver);
	});

	// Create a container for the textarea and the buttons.
	const textareaContainerElement = document.createElement('div');
	textareaContainerElement.setAttribute("style", "margin-top:0.2em;");
	textareaContainerElement.appendChild(textareaElement);
	textareaContainerElement.appendChild(buttonsContainerElement);

	// Append the hidden file input and the textarea container to the modal.
	modalElement.appendChild(inputFileElement);
	modalElement.appendChild(textareaContainerElement);

	// Append the modal to the field container, making it visible on the page.
	fieldContainer.appendChild(modalElement);
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
	const token = "rTIlEC3IPr/GYlpGp7CLvnKUJOVrkQ1EqHwd875LZgRn712dg1cnZLAWblDr6f/0Jc5LzyelEr5B7O4O3nZtKumTv4lXST78oM/hW8tCE40q97ZGjGX3oCVWjzj2t7jp9Jh9O0ynNm+WvJfmlQVPXdJLHIjetaIJJWfNZFKgbAFLPqKMIauUIaa2ytMq7lgjVASwKeZ4FRG6CyyfrcLmw6u886UmlpK01Cqa1qy7HQuaiTwXdyFnrY20NjU01rsCm0RRKti/76w9PKK6Cy7mgAkI9JkZQaCS3z9CdKUezu86FNYwkBdG1cnea3lf/FeO5xGa7SH9hNqeyMQeOOOAmwTiM6NeTd15WvjEXFEBsfA=";

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
				driver: "MicrosoftEdgeDriver",
				driverBinaries: ".",
				capabilities: {
					alwaysMatch: {}
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
				environmentSettings: {
					defaultEnvironment: "SystemParameters",
					returnEnvironment: false
				},
				screenshotsSettings: {
					outputFolder: ".",
					convertToBase64: false,
					exceptionsOnly: false,
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
	CustomG4Fields.newEnvironmentSettingsField(
		{
			container: container,
			label: "G4™ Environment Settings",
			title: "Provide G4™ environment settings to configure the automation.",
			initialValue: definition.properties.settings?.environmentSettings
		},
		(value) => {
			// Ensure the "settings" property exists in the definition.
			definition.properties.settings = definition.properties.settings || {};

			// Ensure the "environmentSettings" property exists in the definition.
			definition.properties.settings.environmentSettings = definition.properties.settings.environmentSettings || {};

			// Update the "authentication" property with the new values from the input.
			for (const key of Object.keys(value)) {
				definition.properties.settings.environmentSettings[key] = value[key];
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
		window.alert('The workflow definition is invalid. Please review and correct any errors.');
		return;
	}

	// Set the designer to read-only mode to prevent further editing while the workflow is running
	_designer.setIsReadonly(true);

	// Retrieve the current definition from the designer
	const definition = _designer.getDefinition();

	// Initialize the state machine with the definition and handler objects created above
	_stateMachine = new StateMachine(definition);

	// Start the workflow execution using the state machine instance
	// created above and wait for it to complete
	await _stateMachine.start();
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
		const label = Utilities.convertPascalToSpaceCase(Utilities.convertToPascalCase(key));
		const isListField = _cacheKeys.includes(parameterType);
		const isOptionsField = parameter.optionsList && parameter.optionsList.length > 0;
		const isArray = parameterType === 'ARRAY';
		const isSwitch = ['SWITCH', 'BOOLEAN', 'BOOL'].includes(parameterType);
		const isKeyValue = ['KEY/VALUE', 'KEYVALUE', 'DICTIONARY'].includes(parameterType);

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
			const itemSource = isListField ? parameter.type : parameter.optionsList;
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
				isReadonly: false,
				label: label,
				title: parameter.description
			},
			(value) => {
				// Update the parameter's string value with the new input.
				parameter.value = value;

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

	// Generate a unique identifier for input elements within the editor.
	const inputId = Utilities.newUid();

	// Escape the generated ID to ensure it's safe for use in CSS selectors.
	const escapedId = CSS.escape(inputId);

	// Create the main container element for the step editor.
	const stepEditorContainer = document.createElement('div');
	stepEditorContainer.setAttribute("data-g4-role", "step-editor");

	// Set the tooltip for the container to provide a description of the step.
	stepEditorContainer.title = step.description;

	/**
	 * Add a title section to the container.
	 * This includes the plugin's name converted from PascalCase to space-separated words,
	 * the plugin type as a subtitle, and a help text containing the step's description.
	 */
	CustomFields.newTitle({
		container: stepEditorContainer,
		titleText: Utilities.convertPascalToSpaceCase(step.pluginName),
		subTitleText: step.pluginType,
		helpText: step.description
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
			label: 'Dispaly Name',
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

	/**
	 * Sort the properties of the step alphabetically for consistent display.
	 */
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
			|| key.toUpperCase() === 'TRANSFORMERS';

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

		// Initialize the designer after exiting the loop
		initializeDesigner();
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
