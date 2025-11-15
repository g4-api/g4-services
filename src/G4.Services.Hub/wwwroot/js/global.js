const STEP_VALIDATED = 'stepValidated';
const BASE_NOTIFICATION_PATH = "/hub/v4/g4/notifications";
const BASE_CLIENT_PATH = "/api/v4/g4";
const BASE_HUB_URL = "";

let _averageCounter = 0;
let _counter;
let _cache = {};
let _cacheKeys = [];
let _client = {};
let _cliFactory = {};
let _dataCollectors = {};
let _designer;
let _editorObserver;
let _extractionScopes = {};
let _manifests = {};
let _manifestsGroups = [];
let _stateMachine = {};
let _timer;

const _flowableTypes = ["ACTION", "CONTENT", "EXPORT", "JOB", "STAGE", "TRANSFORMER"];
const _auditableTypes = ["ACTION", "CONTENT", "TRANSFORMER"];

const _connection = new signalR
	.HubConnectionBuilder()
	.withUrl(BASE_NOTIFICATION_PATH)
	.withAutomaticReconnect()
	.build();

// Start the SiognalR connection
_connection
	.start()
	.catch(err => console.error("Connection failed:", err.message));

(async () => {
    // Create a new G4Client instance.
    _client = new G4Client(BASE_CLIENT_PATH);

    // Create a new CliFactory instance.
    _cliFactory = new CliFactory();

    // Fetch manifests and manifest groups from the G4Client.
    _manifests = await _client.getManifests();

    // Collect all manifests groups from the G4Client.
    _manifestsGroups = await _client.getGroups();

    // Store the cache in a global variable for later use.
    _cache = await _client.getCache();

    // Store the cache keys in a global variable for later use.
    _cacheKeys = Object.keys(_cache).map(key => key.toUpperCase());

    // Store extraction scopes in a global variable for later use.
    _extractionScopes = {
        providers: _cache["ExtractionScope"],
        itemSource: Object.values(_cache["ExtractionScope"]).map(i => ({
            name: i.manifest.key,
            description: i.manifest.summary,
        })),
    }

    // Store data collectors in a global variable for later use.
    _dataCollectors = {
        providers: _cache["DataCollector"],
        itemSource: Object.values(_cache["DataCollector"]).map(i => ({
            name: i.manifest.key,
            description: i.manifest.summary,
        })),
    }

    /**
     * Wait for the timer element to be available in the DOM.
     * Once the element is found or after 5000ms, execute the callback.
     */
    Utilities.waitForElement('#designer--timer', 5000).then(() => {
        // Get the timer element from the DOM.
        const timerElement = document.querySelector('#designer--timer');

        // Create a new Timer instance with the timer element.
        _timer = new Timer(timerElement);
    });

    /**
     * Wait for the counter element to be available in the DOM.
     * Once the element is found or after 5000ms, execute the callback.
     */
    Utilities.waitForElement('#designer--total-actions', 5000).then(() => {
        // Get the counter element from the DOM.
        const counterElement = document.querySelector('#designer--total-actions');

        // Create a new Counter instance with the counter element.
        _counter = new Counter(counterElement);
    });

    /**
     * Wait for the average counter element to be available in the DOM.
     * Once the element is found or after 5000ms, execute the callback.
     */
    Utilities.waitForElement('#designer--average-action-time', 5000).then(() => {
        // Get the average counter element from the DOM.
        const averageElement = document.querySelector('#designer--average-action-time');

        // Create a new AverageCounter instance with the average counter element.
        _averageCounter = new AverageCounter(averageElement);
    });

    /**
     * Wait for the smart editor element to be available in the DOM.
     * Once the element is found or after 5000ms, execute the callback.
     */
    Utilities.waitForElement('#designer .sqd-smart-editor', 5000).then(() => {
        // Select the target node that we want to observe for DOM changes.
        const targetNode = document.querySelector('#designer .sqd-smart-editor');

        // Query all elements in the document that match the provided selector.
        const elements = document.querySelectorAll('#designer .sqd-smart-editor textarea') || [];

        // Loop through each element and dispatch the event.
        for (const element of elements) {
            Utilities.setTextareaSize(element, 8);
        }

        // Define the configuration for the MutationObserver.
        // This configuration listens for changes to the child nodes and the entire subtree.
        const config = {
            attributes: false,
            childList: true,
            subtree: true
        };

        // Create an Observer instance for the target node.
        _editorObserver = new Observer(targetNode);

        // Start observing the target node for DOM mutations.
        _editorObserver.observeDOMChanges(config, (mutationsList) => {
            // Convert each mutation's addedNodes (a NodeList) into an array and flatten them into a single array.
            const addedNodes = mutationsList.flatMap(mutation => Array.from(mutation.addedNodes));

            // If no nodes were added during the mutations, exit early.
            if (addedNodes.length === 0) {
                return;
            }

            // Query all elements in the document that match the provided selector.
            const elements = document.querySelectorAll('#designer .sqd-smart-editor textarea') || [];

            // Loop through each element and dispatch the event.
            for (const element of elements) {
                Utilities.setTextareaSize(element, 8);
            }
        });
    });

    /**
     * Wait for the canvas element to be available in the DOM.
     * Once the element is found or after 5000ms, execute the callback.
     */
    Utilities.waitForElement('.sqd-workspace', 5000);

	/**
	 * Module to enable drag-and-drop import of model definitions into the designer canvas.
	 * @module dragAndDropImport
	 */

	/**
	 * Enable drag-and-drop import of model definitions into the designer canvas.
	 * Listens for file drops on the '#designer' element, reads the file,
	 * parses JSON or raw text, and applies the definition.
	 */

	/**
	 * Waits for the '#designer' element and sets up dragover, dragleave, and drop handlers.
	 */
	Utilities.waitForElement('#designer', 5000).then(async (designer) => {
		// Highlight the designer when a draggable item is over it.
		designer.addEventListener('dragover', e => {
			// Allow drop by preventing default
			e.preventDefault();
			e.dataTransfer.dropEffect = 'copy';

			// Add visual indicator
			designer.classList.add('sqd-content-dragover');
		});

		// Remove highlight when drag leaves the designer
		designer.addEventListener('dragleave', e => {
			// Allow drop by preventing default
			e.preventDefault();
			e.dataTransfer.dropEffect = 'copy';

			// Remove visual highlight when drag leaves the designer.
			designer.classList.remove('sqd-content-dragover');
		});

        // Check if running in an Electron environment
		const isElectron = /Electron\/\d+\.\d+\.\d+/.test(navigator.userAgent);

		// If running in Electron, skip drag-and-drop import setup.
		if (isElectron) {
			console.info("Running in Electron environment: enhanced desktop features enabled.");
			return;
		}

		// Handle file drop event
		// Process dropped file and import its content.
		designer.addEventListener('drop', async (e) => {
			// Prevent browser from opening the file
			e.preventDefault();

            // Remove drag highlight
			designer.classList.remove('dragover');

			// If no file, exit
			const file = e.dataTransfer.files[0];
			const fileUri = e.dataTransfer.getData('text/uri-list');
			if (!file && !fileUri) {
				return;
			}

			// Read file content
			if (file) {
				const reader = new FileReader();
				reader.onload = e => {
					// Raw file data in e.target.result
					const content = e.target.result;

					// Set up a variable to hold the definition
					let definition;

					// Try JSON.parse, fallback to raw text
					try {
						definition = JSON.parse(content);
					} catch (error) {
						console.warn('Failed to parse dropped file as JSON:', error);
						return;
					}

					// Select workspace element to observe DOM changes
					const workspaceElement = document.querySelector('.sqd-workspace');

					// Observe workspace to reset view after import
					const workspaceObserver = new Observer(workspaceElement);

					// Apply imported model definition
					setDefinition(definition);

					// Reset view based on workspace observer
					resetView(workspaceObserver);
				};

				// Read file as text; change to readAsArrayBuffer for binary
				reader.readAsText(file);
			}
			else if (fileUri) {
				const response = await fetch(fileUri);
				const definition = await response.text();

				// Select workspace element to observe DOM changes
				const workspaceElement = document.querySelector('.sqd-workspace');

				// Observe workspace to reset view after import
				const workspaceObserver = new Observer(workspaceElement);

				// Apply imported model definition
				setDefinition(definition);

				// Reset view based on workspace observer
				resetView(workspaceObserver);
			}
		});
	});
})();

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
	 * Recursively creates a new step from a rule.
	 *
	 * This helper function retrieves the manifest for the rule's plugin, creates a new step
	 * using the state machine factory, synchronizes it with the rule, and processes any nested
	 * rules or branches.
	 */
	const newStep = (rule) => {
		// Normalize extraction rules to use 'ExportData' as the plugin name if not specified.
		if (rule["$type"].toUpperCase() === "EXTRACTION" && !rule.pluginName) {
			rule.pluginName = 'ExportData'
		}

		// Retrieve the manifest for the rule's plugin from the cache.
		const manifest = getManifest(_cache, rule.pluginName);

		// Create a new step using the state machine factory and the retrieved manifest.
		const step = StateMachineSteps.newG4Step(manifest, rule.pluginName);

		// Assign the name of the rule's capabilities to the step if available.
		step.name = step?.pluginName?.toUpperCase() === 'MISSINGPLUGIN'
			? `Missing Plugin (${rule?.capabilities?.displayName || step.name})`
			: rule?.capabilities?.displayName || step.name;

		// Ensure that the rule has 'rules' and 'branches' properties.
		rule.rules = rule.rules || [];
		rule.branches = rule.branches || {};
		rule.transformers = rule.transformers || [];

		// Determine if the rule contains nested rules or branch entries.
		const branchesKeys = Object.keys(rule.branches);
		const isRules = rule.rules.length > 0;
		const isTransformers = rule.transformers.length > 0;
		const isBranches = branchesKeys.length > 0;

		// Synchronize the current step with the rule configuration.
		_client.syncStep(step, rule);

		// If no nested rules or branches exist, return the current step.
		if (!isRules && !isBranches && !isTransformers) {
			return step;
		}

		// If the rule contains nested child rules, process them recursively
		if (isRules) {
			// Initialize an empty array to hold the sequence of sub-steps
			step.sequence = [];

			// Iterate over each child rule in the "rules" array
			for (const subRule of rule.rules) {
				// Create a new step object based on the sub-rule definition
				const subStep = newStep(subRule);

				// Synchronize the new sub-step with its original rule data
				_client.syncStep(subStep, subRule);

				// Add the synced sub-step to the sequence array
				step.sequence.push(subStep);
			}
		}

		// If there are transformer rules defined, process them recursively
		if (isTransformers) {
			// Initialize an empty array to hold the transformer sub-steps
			step.transformers = [];

			// Iterate over each transformer definition on the rule
			for (const transformer of rule.transformers) {
				// Create a new step object from this transformer definition
				const subStep = newStep(transformer);

				// Sync the new sub-step with its source transformer data
				_client.syncStep(subStep, transformer);

				// Add the synced sub-step into the step's transformers array
				step.transformers.push(subStep);
			}
		}

		// Process branch rules if any branches are defined on this rule
		if (isBranches) {
			// Iterate over each branch key (e.g., "Actions", "Transformers")
			for (const branchKey of branchesKeys) {
				// Safely retrieve the array of sub-rules for this branch
				const subRules = rule.branches[branchKey];

				// For each sub-rule in the current branch
				for (const subRule of subRules) {
					// Create a new step object from the sub-rule definition
					const subStep = newStep(subRule);

					// Ensure the step.branches array exists for this branch
					step.branches[branchKey] = step.branches[branchKey] || [];

					// Synchronize the newly created step with its source rule data
					_client.syncStep(subStep, subRule);

					// Add the synchronized sub-step into the branch array
					step.branches[branchKey].push(subStep);
				}
			}
		}

		// Check if the current step is a ContentRule (actions or transformers)
		const isContentRule = step.pluginType.toUpperCase() === "CONTENT";

		if (isContentRule && (isRules || isTransformers)) {
			// Change the component type to a switcher when rules/transformers apply
			step.componentType = 'switch';

			// Initialize an empty branches object to hold sub-flows
			step.branches = {};

			// Assign the existing sequence of actions into an "Actions" branch
			step.branches["Actions"] = step.sequence || [];

			// Assign any transformers into a "Transformers" branch
			step.branches["Transformers"] = step.transformers || [];

			// Remove the old flat sequence and transformers properties
			delete step.sequence;
			delete step.transformers;
		}

		// Return the modified step object, now with nested branches if applicable
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
