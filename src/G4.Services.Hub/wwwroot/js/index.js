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
	let sortedGroups = groups.sort((a, b) => a.name.localeCompare(b.name));

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
		if (!_includeTypes.includes(message.type.toUpperCase())) {
			return;
		}

		// Select the step in the designer by its ID.
		_designer.selectStepById(message.id);

		// Adjust the viewport so the selected step is brought into view.
		_designer.moveViewportToStep(message.id);
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
	_connection.on("ReceiveAutomationInvokedEvent", (_) => {
		// Indicate that the automation is no longer running
		_stateMachine.isRunning = false;

		// Release the designer from read-only mode
		_designer.setIsReadonly(false);

		// Reset the designer or UI state after all steps have been processed
		_stateMachine.handler.resetDesigner();
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
				// Define the list of supported icon types
				const supportedIcons = ['if', 'loop', 'text', 'job', 'stage'];

				// Determine the filename based on the type; default to 'task' if type is unsupported
				const fileName = supportedIcons.includes(type) ? type : 'task';

				// Return the relative path to the SVG icon
				return `./images/icon-${fileName}.svg`;
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
				// Check that every property key in step.properties has a truthy value
				return Object.keys(step.properties).every(n => !!step.properties[n]);
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

function newImportModal() {
	const getManifest = (cache, manifestName) => {
		for (const group of Object.keys(cache)) {
			if (manifestName in cache[group]) {
				return cache[group][manifestName].manifest;
			}
		}
	}

	const newButtonsContainerElement = (inputId, modalElement, setCallback) => {
		const buttonsContainerElement = document.createElement("div");
		buttonsContainerElement.setAttribute("style", "display: inline-flex; gap: 0.2em; margin-bottom: 0.2em;");

		const closeButtonElement = document.createElement('button');
		closeButtonElement.setAttribute('id', `${inputId}-closeButton`);
		closeButtonElement.setAttribute('type', 'button');
		closeButtonElement.innerText = 'Close';

		closeButtonElement.addEventListener('click', () => {
			fieldContainer.removeChild(modalElement);
		});

		const importButtonElement = document.createElement('button');
		importButtonElement.setAttribute('id', `${inputId}-importButton`);
		importButtonElement.setAttribute('type', 'button');
		importButtonElement.innerText = 'Import';

		importButtonElement.addEventListener('click', () => {
			inputFileElement.click();
		});

		const applyButtonElement = document.createElement('button');
		applyButtonElement.setAttribute('id', `${inputId}-applyButton`);
		applyButtonElement.setAttribute('type', 'button');
		applyButtonElement.innerText = 'Apply';

		applyButtonElement.addEventListener('click', () => {
            try {
				const textareaElement = modalElement.querySelector('textarea');
				const value = !textareaElement.value || textareaElement.value === ""
					? {}
					: JSON.parse(textareaElement.value);
				fieldContainer.removeChild(modalElement);
				setCallback(value);
			}
			catch (error) {
                console.error(error);
			}
		});

		buttonsContainerElement.appendChild(importButtonElement);
		buttonsContainerElement.appendChild(applyButtonElement);
		buttonsContainerElement.appendChild(closeButtonElement);

		return buttonsContainerElement;
	}

	const newInputFileElement = (inputId, textareaElement, setCallback) => {
		const inputFileElement = document.createElement("input");

		inputFileElement.setAttribute("type", "file");
		inputFileElement.setAttribute("id", `${inputId}-input-file`);
		inputFileElement.setAttribute("accept", `.txt;.json`);
		inputFileElement.setAttribute("style", `display: none;`);

		inputFileElement.addEventListener('change', (event) => {
			const file = event.target.files[0];
			if (file) {
				const reader = new FileReader();
				reader.onload = (e) => {
					textareaElement.value = e.target.result;
				};
				// Read the file as text
				reader.readAsText(file);
			}
		});

		return inputFileElement;
	};

	const newModalElement = (inputId) => {
		const modalElement = document.createElement('div');

		modalElement.setAttribute('id', `${inputId}-import-modal`);
		modalElement.setAttribute('data-g4-role', 'import-modal');
		modalElement.setAttribute(
			'style',
			'display: block; gap: 0.2em; position: fixed; top: 50%; left: 50%; transform: translate(-50%, -50%); margin-left: 0; z-index: 9999;');

		return modalElement;
	};

	const newTextareaElement = (inputId) => {
		const textareaElement = document.createElement("textarea");

		textareaElement.setAttribute("id", `${inputId}-textarea`);
		textareaElement.setAttribute("style", "width: 60vw; height: 25vh;");
		textareaElement.setAttribute("wrap", "off");
		textareaElement.setAttribute("spellcheck", "false");
		textareaElement.setAttribute("placeholder", "Type, paste or import definition here...");

		return textareaElement;
	}

	const setDefinition = (definition) => {
		const newStep = (rule) => {
			const manifest = getManifest(_cache, rule.pluginName);
			const step = StateMachineSteps.newG4Step(manifest);

			rule.rules = rule.rules || [];
			rule.branches = rule.branches || {};

            const branchesKeys = Object.keys(rule.branches);
			const isRules = rule.rules.length > 0;
			const isBranches = branchesKeys.length > 0;

			_client.syncStep(step, rule);

			if (!isRules && !isBranches) {
				return step;
			}

			if (isRules) {
				step.sequence = [];

				for (const subRule of rule.rules) {
					const subStep = newStep(subRule);
					_client.syncStep(subStep, subRule);
					step.sequence.push(subStep);
				}
			}

			if (isBranches) {
				for (const branchKey of branchesKeys) {
					for (const subRule of rule.branches[branchKey]) {
						const subStep = newStep(subRule);
						step.branches[branchKey] = step.branches[branchKey] || [];
						_client.syncStep(subStep, subRule);
                        step.branches[branchKey].push(subStep);
					}
				}
			}

			return step;
		}

		const setVendorCapabilities = (driverParameters) => {
			if (!driverParameters?.capabilities?.alwaysMatch) {
				return undefined;
			}

			const pattern = /^\w+:(\w+)?(options|inspection)$/si;
			const keys = Object.keys(driverParameters.capabilities.alwaysMatch);
			const vendorOptions = keys.filter(key => pattern.test(key.trim())) || [];

			driverParameters.capabilities.vendorCapabilities = {};

			for (let i = 0; i < vendorOptions.length; i++) {
				const vendor = vendorOptions[i];
				const jsonValue = JSON.stringify(driverParameters.capabilities.alwaysMatch[key]);
				const value = JSON.parse(jsonValue);

				if (!vendor) {
					continue;
				}

				driverParameters.capabilities.vendorCapabilities[i] = {
					vendor: vendor,
					capabilities: value
				}

				delete driverParameters.capabilities.alwaysMatch[key]
			}

			return driverParameters;
		}

		const newDefinition = (definition, sequence) => {

            const id = definition?.reference?.id && definition.reference.id !== ""
				? definition?.reference?.id
				: Utilities.newUid();

			const driverParameters = setVendorCapabilities(definition.driverParameters);

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
		}

		const sequence = [];

		definition.stages = definition.stages || [];

		for (const stage of definition.stages) {
			const stageStep = StateMachineSteps.newG4Stage('Stage', {}, {}, []);

			stageStep.name = stage?.reference?.name || stageStep.name;
			stageStep.description = stage?.reference?.description?.trim() || stageStep.description?.trim();
			stageStep.id = stage?.reference?.id || stageStep.id;
			stageStep.properties.driverParameters = stage.driverParameters || {};

			stage.jobs = stage.jobs || [];


			for (const job of stage.jobs) {
				const jobStep = StateMachineSteps.newG4Job('Job', {}, {}, []);

				jobStep.name = job?.reference?.name || jobStep.name;
				jobStep.description = job?.reference?.description || jobStep.description;
				jobStep.id = job?.reference?.id || jobStep.id;
				jobStep.properties.driverParameters = job.driverParameters || {};

				job.rules = job.rules || [];

                for (const rule of job.rules) {
					const step = newStep(rule);
					jobStep.sequence.push(step);
				}

                stageStep.sequence.push(jobStep);
			}

            sequence.push(stageStep);
		}

		const newDefinitionState = newDefinition(definition, sequence);

		// Create the workflow designer using the configuration and start definition.
		_designer.state.setDefinition(newDefinitionState);
	};

	const inputId = Utilities.newUid();
	const fieldContainer = document.querySelector("body");
	const existingModals = fieldContainer?.querySelectorAll("[id*=import-modal]");

	for (const existingModal of existingModals) {
		fieldContainer.removeChild(existingModal);
	}

	const modalElement = newModalElement(inputId);

	const textareaElement = newTextareaElement(inputId);
	const inputFileElement = newInputFileElement(inputId, textareaElement);
	const buttonsContainerElement = newButtonsContainerElement(inputId, modalElement, (definition) => {
		setDefinition(definition);
	});
	const textareaContainerElement = document.createElement('div');

	textareaContainerElement.setAttribute("style", "margin-top:0.2em;")
	textareaContainerElement.appendChild(textareaElement);
	textareaContainerElement.appendChild(buttonsContainerElement);

	modalElement.appendChild(inputFileElement);
	modalElement.appendChild(textareaContainerElement);

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
	return {
		id: Utilities.newUid(),
		// Default properties for the start definition.
		properties: {
			authentication: {
				username: "pyhBifB6z1YxJv53xLip",
				password: ""
			},
			driverParameters: {
				driver: "MicrosoftEdgeDriver",
				driverBinaries: ".",
				capabilities: {
					alwaysMatch: {},
					firstMatch: [
						{}
					]
				}
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
	container.setAttribute("g4-role", "root-editor");

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
			definition.properties['driverParameters']['capabilities']['firstMatch'] = definition.properties['driverParameters']['capabilities']['firstMatch'] || [{}];

			// Iterate over each key in the provided `value` object.
			for (const key of Object.keys(value)) {
				// Determine if the current key pertains to 'capabilities' with 'firstMatch'.
				const isFirstMatch = key.toLocaleUpperCase() === 'CAPABILITIES' && 'firstMatch' in value[key];

				// Determine if the current key pertains to 'capabilities' with 'alwaysMatch'.
				const isAlwaysMatch = key.toLocaleUpperCase() === 'CAPABILITIES' && 'alwaysMatch' in value[key];

				// Reference to the existing 'capabilities' object for easy access.
				const capabilities = definition.properties['driverParameters'].capabilities;

				if (isFirstMatch) {
					// Extract the 'firstMatch' object from the input value.
					const firstMatch = value[key].firstMatch;

					// Iterate over each group in 'firstMatch' and merge it into the existing capabilities.
					for (const group of Object.keys(firstMatch)) {
						capabilities['firstMatch'][group] = firstMatch[group];
					}

					// Continue to the next key as this one has been processed.
					continue;
				}

				if (isAlwaysMatch) {
					// Assign the 'alwaysMatch' object directly to the capabilities.
					capabilities['alwaysMatch'] = value[key]?.alwaysMatch;

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
			initialValue: definition.properties['automationSettings']
		},
		(value) => {
			// Ensure the "automationSettings" property exists in the definition.
			definition.properties['automationSettings'] = definition.properties['automationSettings'] || {};

			// Update the "automationSettings" property with the new values from the input.
			for (const key of Object.keys(value)) {
				definition.properties['automationSettings'][key] = value[key];
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
			initialValue: definition.properties['environmentSettings']
		},
		(value) => {
			// Ensure the "environmentSettings" property exists in the definition.
			definition.properties['environmentSettings'] = definition.properties['environmentSettings'] || {};

			// Update the "authentication" property with the new values from the input.
			for (const key of Object.keys(value)) {
				definition.properties['environmentSettings'][key] = value[key];
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
			initialValue: definition.properties['exceptionsSettings']
		},
		(value) => {
			// Ensure the "exceptionsSettings" property exists in the definition.
			definition.properties['exceptionsSettings'] = definition.properties['exceptionsSettings'] || {};

			// Update the "exceptionsSettings" property with the new values from the input.
			for (const key of Object.keys(value)) {
				definition.properties['exceptionsSettings'][key] = value[key];
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
			initialValue: definition.properties['queueManagerSettings']
		},
		(value) => {
			// Ensure the "queueManagerSettings" property exists in the definition.
			definition.properties['queueManagerSettings'] = definition.properties['queueManagerSettings'] || {};

			// Update the "queueManagerSettings" property with the new values from the input.
			for (const key of Object.keys(value)) {
				definition.properties['queueManagerSettings'][key] = value[key];
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
			initialValue: definition.properties['performancePointsSettings']
		},
		(value) => {
			// Ensure the "performancePointsSettings" property exists in the definition.
			definition.properties['performancePointsSettings'] = definition.properties['performancePointsSettings'] || {};

			// Update the "performancePointsSettings" property with the new values from the input.
			for (const key of Object.keys(value)) {
				definition.properties['performancePointsSettings'][key] = value[key];
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
			initialValue: definition.properties['pluginsSettings']
		},
		(value) => {
			// Initialize pluginsSettings if it doesn't exist
			definition.properties['pluginsSettings'] = definition.properties['pluginsSettings'] || {
				externalRepositories: {},
				forceRuleReference: false
			};

			// Reference to the current plugins settings
			const pluginsSettings = definition.properties['pluginsSettings'];

			// Get all keys from the incoming value
			const indexes = Object.keys(value) || [];

			// Iterate over each index in the incoming value
			for (const index of indexes) {
				const property = value[index];

				// If the property is not an object, set the pluginsSettings to the property
				if (!assertObject(property)) {
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
			definition.properties['pluginsSettings'] = pluginsSettings;

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
			initialValue: definition.properties['screenshotsSettings']
		},
		(value) => {
			// Ensure the "screenshotsSettings" property exists in the definition.
			definition.properties['screenshotsSettings'] = definition.properties['screenshotsSettings'] || {};

			// Update the "screenshotsSettings" property with the new values from the input.
			for (const key of Object.keys(value)) {
				definition.properties['screenshotsSettings'][key] = value[key];
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
	 * Converts an array of strings in the format "key=value" into an object (dictionary).
	 * If a string does not contain an "=", the entire string is treated as the key with an empty string as its value.
	 * If the value part after "=" is missing, it defaults to an empty string.
	 */
	const convertToDictionary = (values) => {
		return values.reduce((accumulator, currentString) => {
			// Use a regular expression to split the string at the first occurrence of "="
			// ^([^=]+)=(.*)$
			// ^        : Start of the string
			// ([^=]+)  : Capture one or more characters that are not "=" as the key
			// =        : The literal "=" character
			// (.*)     : Capture the rest of the string as the value
			// $        : End of the string
			const match = /^([^=]+)=(.*)$/.exec(currentString);

			if (match) {
				// Extract the key from the first capturing group
				const key = match[1];

				/**
				 * Extract the value from the second capturing group.
				 * If the value is undefined or an empty string, default it to "".
				 * This handles cases like "key=" where the value is missing.
				 */
				const value = match[2] !== undefined ? match[2] : "";

				// Assign the key-value pair to the accumulator object
				accumulator[key] = value;
			} else {
				/**
				 * If the string does not contain an "=", treat the entire string as the key
				 * and assign an empty string as its value.
				 * This handles cases like "key" without any associated value.
				 */
				accumulator[currentString] = "";
			}

			// Return the updated accumulator for the next iteration
			return accumulator;
		}, {});
	};

	/**
	 * Initializes and appends the appropriate input field to the container based on the parameter type.
	 *
	 * This function dynamically creates and configures input fields within a given container
	 * for either properties or parameters of a plugin step. It determines the type of the parameter
	 * and utilizes the corresponding `CustomFields` method to generate the appropriate input field.
	 * After creation, it sets up event listeners to handle value changes and notify the editor context.
	 *
	 * @param {HTMLElement} container - The DOM element that will contain the input field.
	 * @param {string}      key       - The key/name of the property or parameter.
	 * @param {Object}      step      - The plugin step object containing properties and parameters.
	 * @param {string}      type      - Specifies whether the field is a 'properties' or 'parameters' type.
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
			CustomFields.newKeyValueField(
				{
					container: container,
					initialValue: convertToDictionary(parameter.value),
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

	const initializeSystemContainerEditorProvider = (container, step) => {
		// Add a driver parameters field for configuring the G4 driver parameters.
		CustomG4Fields.newDriverParametersField(
			{
				container: container,
				label: "Driver Parameters",
				title: "Provide G4™ driver parameters to configure the automation.",
				initialValue: step.properties['driverParameters']
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
				step.properties['driverParameters']['capabilities']['firstMatch'] = step.properties['driverParameters']['capabilities']['firstMatch'] || [{}];

				// Iterate over each key in the provided `value` object.
				for (const key of Object.keys(value)) {
					// Determine if the current key pertains to 'capabilities' with 'firstMatch'.
					const isFirstMatch = key.toUpperCase() === 'CAPABILITIES' && 'firstMatch' in value[key];

					// Determine if the current key pertains to 'capabilities' with 'alwaysMatch'.
					const isAlwaysMatch = key.toUpperCase() === 'CAPABILITIES' && 'alwaysMatch' in value[key];

					// Reference to the existing 'capabilities' object for easy access.
					const capabilities = step.properties['driverParameters'].capabilities;

					if (isFirstMatch) {
						// Extract the 'firstMatch' object from the input value.
						const firstMatch = value[key].firstMatch;

						// Iterate over each group in 'firstMatch' and merge it into the existing capabilities.
						for (const group of Object.keys(firstMatch)) {
							capabilities['firstMatch'][group] = firstMatch[group];
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
	stepEditorContainer.setAttribute("g4-role", "step-editor");

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
			label: 'Plugin Name',
			title: 'The name of the plugin',
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
		hintText: 'Attributes that define the structural and operational behavior of the plugin.'
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
		hintText: "Configurable inputs that customize and control the plugin's functionality."
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
