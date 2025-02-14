/* global setTimeout */

/**
 * Represents a State Machine for managing automation processes.
 */
class StateMachine {
	/**
	 * Indicates whether the state machine has been interrupted.
	 * @type {boolean}
	 */
	isInterrupted = false;

	/**
	 * Indicates whether the state machine is currently running.
	 * @type {boolean}
	 */
	isRunning = false;

	/**
	 * Creates an instance of StateMachine.
	 *
	 * @param {Object} definition - The definition of the automation sequence.
	 */
	constructor(definition) {
		/**
		 * The automation sequence definition.
		 * @type {Object}
		 */
		this.definition = definition;
	}

	/**
	 * Initiates the automation process.
	 * Sets up the G4 client, creates an automation instance, and starts the automation.
	 *
	 * @async
	 * @function start
	 * 
	 * @returns {Promise<void>} Resolves when the automation process has been successfully started.
	 *
	 * @throws {Error} Throws an error if the automation process fails to start.
	 */
	async start() {
		// Ensure the state machine is not already running
		if (this.isRunning) {
			throw new Error("StateMachine is already running.");
		}

        // Resst the average counter before starting the automation process
		_averageCounter.reset();

		// Reset the total actions counter after the automation has completed
		_counter.reset();

        // Start the timer and reset the time counters to zero before beginning the automation process
		_timer.reset();
		_timer.start();

		// Mark the state machine as running
		this.isRunning = true;

		try {
			// Retrieve the automation definition from the current instance
			const definition = this.definition;

			// Create a new G4 client instance
			const client = new G4Client();

			// Instantiate a new automation object using the client and definition
			const automation = client.newAutomation(definition);

			// Invoke the StartAutomation method on the connection to begin the process
			await _connection.invoke("StartAutomation", automation);
		} catch (error) {
			// Handle any errors that occur during the start process
			console.error("Failed to start the automation process:", error);
			throw error;
		} finally {
			// Indicate that the automation is no longer running
			_stateMachine.isRunning = false;

			// Release the designer from read-only mode
			_designer.setIsReadonly(false);

            // Stop the timer and update the display with the final time elapsed after the automation process completes
			_timer.stop();
		}
	}

	/**
	 * Interrupts the running automation process.
	 * Sets the isInterrupted flag to true and performs any necessary cleanup.
	 *
	 * @function interrupt
	 */
	interrupt() {
		// Set the interruption flag
		this.isInterrupted = true;

		// Perform any additional cleanup or state resetting as required
		// For example:
		// this.cleanupResources();
		console.log("StateMachine has been interrupted.");
	}

	/**
	 * Handles the completion of the automation process.
	 * Resets the state machine's running state and performs any post-processing.
	 *
	 * @private
	 * @function handleCompletion
	 */
	handleCompletion() {
		// Reset the running state
		this.isRunning = false;

		// Perform any post-processing steps
		console.log("Automation process has completed successfully.");
	}

	/**
	 * Cleans up resources used by the state machine.
	 * Ensures that all connections and processes are properly terminated.
	 *
	 * @private
	 * @function cleanupResources
	 */
	cleanupResources() {
		// Implement resource cleanup logic here
		// For example:
		// _connection.dispose();
		console.log("Resources have been cleaned up.");
	}
}

// eslint-disable-next-line @typescript-eslint/no-unused-vars
class StateMachineSteps {
	/**
	 * Creates a new Stage container for the G4 Automation Sequence.
	 * 
	 * A Stage is a container that holds Jobs, each comprising specific Actions, to structure and manage the sequential automation flow.
	 * Stages organize tasks into logical groups, enabling efficient execution, resource allocation, monitoring, and error handling within the automation sequence.
	 *
	 * @param {string} name       - The name of the Stage container.
	 * @param {Object} properties - The properties defining the Stage container.
	 * @param {Object} parameters - The parameters associated with the Stage.
	 * @param {Array}  steps      - The steps or actions that belong to the Stage.
	 * @returns {Object} A new Stage container object created by the newG4Container function.
	 */
	static newG4Stage(name, properties, parameters, steps) {
		// Description of the Stage container, detailing its purpose and functionalities within the G4 Automation Sequence.
		const stageDescription = `
        A container that holds jobs, each comprising specific actions, to structure and manage the sequential automation flow.
        Stages organize tasks into logical groups, enabling efficient execution, resource allocation, monitoring, and error handling within the automation sequence.`;

		// Initialize the Stage container using the newG4Container function.
		const container = StateMachineSteps.newG4Container(name, 'stage', stageDescription, properties, parameters, steps);
		container["pluginType"] = 'Container'
		container["pluginName"] = 'G4™ Stage'
		container.properties.driverParameters = {};

		// Return the Stage container.
		return container;
	}

	/**
	 * Creates a new Job container within a Stage for the G4 Automation Sequence.
	 *
	 * A Job is a container that holds Actions, organizing and executing them as part of a Job within a Stage.
	 * Job containers manage specific tasks, handle dependencies between actions, coordinate execution,
	 * and ensure efficient resource utilization and error handling within the automation sequence.
	 * By encapsulating related actions, Job containers facilitate modularity, scalability, and maintainability,
	 * allowing complex automation workflows to be broken down into manageable and reusable components.
	 *
	 * @param {string} name       - The name of the Job container.
	 * @param {Object} properties - The properties defining the Job container.
	 * @param {Object} parameters - The parameters associated with the Job.
	 * @param {Array}  steps      - The steps or actions that belong to the Job.
	 * @returns {Object} A new Job container object created by the newG4Container function.
	 */
	static newG4Job(name, properties, parameters, steps) {
		// Description of the Job container, detailing its purpose and functionalities within the G4 Automation Sequence.
		const jobDescription = `
        A container that holds actions, organizing and executing them as part of a job within a stage.
        Job containers manage specific tasks, handle dependencies between actions, coordinate execution,
        and ensure efficient resource utilization and error handling within the automation sequence.
        By encapsulating related actions, job containers facilitate modularity, scalability, and maintainability,
        allowing complex automation workflows to be broken down into manageable and reusable components.`;

		// Initialize the Job container using the newG4Container function.
		let container = StateMachineSteps.newG4Container(name, 'job', jobDescription, properties, parameters, steps);
		container["pluginType"] = 'Container'
		container["pluginName"] = 'G4™ Job'
		container.properties.driverParameters = {};

		// Return the Job container.
		return container;
	}

	/**
	 * Creates a new G4 container object for use in a workflow.
	 *
	 * @param {string} name        - The name of the container.
	 * @param {string} type        - The type of the container (e.g., "stage", "job").
	 * @param {string} description - A brief description of the container.
	 * @param {Object} properties  - An object containing properties for the container.
	 * @param {Object} parameters  - An object containing parameters for the container.
	 * @param {Array}  steps       - An array of steps or sub-containers to include in the container's sequence.
	 * @returns {Object} A new container object with a unique ID and specified properties.
	 */
	static newG4Container(name, type, description, properties, parameters, steps) {
		return {
			description: description || 'Description not provided.',
			id: Utilities.newUid(),       // Generate a unique identifier for the container.
			componentType: 'container',   // Specify the component type as "container".
			type,                         // The type of the container (e.g., stage, job).
			name,                         // The name of the container.
			parameters: parameters || {}, // Parameters specific to the container.
			properties: properties || {}, // Properties specific to the container.
			sequence: steps || [],        // The sequence of steps or sub-containers; defaults to an empty array.
			context: {}                   // The context object for storing additional information.
		};
	}

	/**
	 * Creates a new G4 step based on the provided manifest.
	 *
	 * @param {Object} manifest - The manifest object containing properties and parameters.
	 * @returns {Object} The newly created G4 step object.
	 */
	static newG4Step(manifest) {
		// Creates a new bridge object from a G4 parameter object.
		const newBridgeObject = (g4ParameterObject) => {
			let bridgeObject = {
				description: g4ParameterObject.description.join('\n'),  // Set summary
				name: Utilities.convertPascalToSpaceCase(g4ParameterObject.name), // Convert name to space case
				required: g4ParameterObject.mandatory || false,         // Set required flag
				type: g4ParameterObject.type || 'String',               // Set type or default to 'String'
				value: g4ParameterObject.default || '',                 // Set default value or empty string
				optionsList: g4ParameterObject.values || []             // Set options or default to an empty array
			};

			// TODO: Consider to remove this condition
			if (bridgeObject.type.toUpperCase() === 'STRING' || bridgeObject.type.toUpperCase() === 'ANY') {
				bridgeObject.multiLine = false;
			}

			// Return the bridge object
			return bridgeObject;
		}

		// Initialize properties and parameters objects
		const properties = {};
		const parameters = {};

        // Check if the manifest is missing and return an error step placeholder
		if (!manifest) {
			return {
				aliases: [],
				categories: "G-ERROR",
				componentType: "task",
				context: {},
				description: "Description not provided.",
				id: Utilities.newUid(),
				name: "Missing Plugin",
				parameters: {},
				pluginName: "MissingPlugin",
				pluginType: "Action",
				properties: {},
				type: "task"
			}
		}

		// Process each property in manifest.properties
		if (manifest.properties) {
			for (const property of manifest.properties) {
				const name = Utilities.convertToCamelCase(property.name);
				properties[name] = newBridgeObject(property);
			}
		}

		// Process each parameter in manifest.parameters
		if (manifest.parameters) {
			for (const parameter of manifest.parameters) {
				const name = Utilities.convertToCamelCase(parameter.name);
				parameters[name] = newBridgeObject(parameter);
			}
		}

		// Check if the manifest has categories and determine if it is a condition or loop
		const context = manifest.context?.integration?.sequentialWorkflow || {};
		const componentType = context?.componentType?.toLowerCase() || "task";
		const iconProvider = context?.iconProvider?.toLowerCase() || "task";
		const label = context?.label || Utilities.convertPascalToSpaceCase(manifest.key);
		const isSwitch = componentType === "switch";
		const isLoop = componentType === "loop";
		const isContainer = componentType === "container";

		// Initialize the new G4 step object
		const step = {
			componentType: componentType,
			type: iconProvider
		};

		// Check if the manifest is a condition and initialize the branches object
		if (isSwitch) {
			const branches = {};
			const contextBranches = manifest.context?.integration?.sequentialWorkflow?.branches || [];
			for (const branch of contextBranches) {
				branches[branch] = [];
			}
			step.branches = branches;
		}

		// Check if the manifest is a loop or container and initialize the sequence array
		if (isLoop || isContainer) {
			step.sequence = [];
		}

		// Set the remaining properties of the new G4 step object
		step.categories = manifest.categories ? manifest.categories.join("|").toUpperCase() : "";
		step.description = manifest.summary ? manifest.summary.join('\n') : 'Description not provided.';
		step.id = Utilities.newUid();
		step.name = label;
		step.parameters = parameters;
		step.pluginName = manifest.key;
		step.aliases = manifest.aliases || [];
		step.pluginType = manifest.pluginType;
		step.properties = properties;
		step.context = context;

		// Return the new G4 step object
		return step;
	}
}
