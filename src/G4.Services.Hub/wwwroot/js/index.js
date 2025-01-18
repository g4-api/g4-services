/* global window, document */

// Build a connection to the hub endpoint.
// The URL must match app.MapHub<G4AutomationNotificationsHub>("/hub/v4/g4/notifications")
const _connection = new signalR
	.HubConnectionBuilder()
	.withUrl("/hub/v4/g4/notifications")
	.withAutomaticReconnect()
	.build();

// Once the connection starts, enable the button
_connection
	.start()
	.catch(err => console.error("Connection failed:", err.message));

let _designer;
let _cache = {};
let _cacheKeys = [];
let _manifests = {};
let _definition = {};
let _stateMachine = {};

/**
 * Initializes and starts the workflow definition. This function checks whether the
 * designer is in a valid and editable state, then sets it to read-only mode before
 * creating and invoking the state machine that processes the workflow.
 *
 * @async
 * @function startDefinition
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

	// Initialize workflow handler with the current definition and start the workflow
	const handler = {
		/**
		 * Determines if the loop can continue based on the current step's pluginName.
		 * Depending on the loop type (FOR, FOREACH, WHILE), the corresponding handler is called.
		 *
		 * @async
		 * @function
		 * @name assertCanLoopContinue
		 * @param {Object} options - The configuration object for the current step/automation.
		 * @param {Object} options.step - The step object containing properties like `pluginName`.
		 * @param {Object} options.session - The current session context, if any.
		 * @param {Object} options.automation - The current automation instance.
		 * @returns {Promise<boolean>} - Resolves to `true` if the loop should continue, otherwise `false`.
		 */
		assertCanLoopContinue: async (options) => {
			// Check if the pluginName is "INVOKEFORLOOP" (case-insensitive),
			// then delegate to forLoopHandler to decide continuation.
			if (options.step.pluginName?.toLocaleUpperCase() === "INVOKEFORLOOP") {
				return handler.forLoopHandler.assertCanContinue(options);
			}

			// If it's "INVOKEFOREACHLOOP", first call the 'set' method to update locators
			// or other iteration-specific details, then check if it can continue.
			if (options.step.pluginName?.toLocaleUpperCase() === "INVOKEFOREACHLOOP") {
				handler.foreachLoopHandler.set(options);
				return handler.foreachLoopHandler.assertCanContinue(options);
			}

			// If it's "INVOKEWHILELOOP", simply call whileLoopHandler to verify continuation.
			if (options.step.pluginName?.toLocaleUpperCase() === "INVOKEWHILELOOP") {
				return handler.whileLoopHandler.assertCanContinue(options);
			}
		},

		/**
		 * Determines if a loop can start based on the current step's pluginName.
		 * Depending on the loop type (FOR, FOREACH, WHILE), the corresponding handler is called.
		 *
		 * @async
		 * @function
		 * @name assertCanLoopStart
		 * @param {Object} options            - The configuration object for the current step/automation.
		 * @param {Object} options.step       - The step object containing properties like `pluginName`.
		 * @param {Object} options.session    - The current session context, if any.
		 * @param {Object} options.automation - The current automation instance.
		 * @returns {Promise<boolean>} - Resolves to `true` if the loop should start, otherwise `false`.
		 */
		assertCanLoopStart: async (options) => {
			// If the pluginName is "INVOKEFORLOOP", delegate loop-start logic to forLoopHandler.
			if (options.step.pluginName?.toLocaleUpperCase() === "INVOKEFORLOOP") {
				return handler.forLoopHandler.assertCanStart(options);
			}

			// If the pluginName is "INVOKEFOREACHLOOP", use the foreachLoopHandler's start logic.
			if (options.step.pluginName?.toLocaleUpperCase() === "INVOKEFOREACHLOOP") {
				return handler.foreachLoopHandler.assertCanStart(options);
			}

			// If the pluginName is "INVOKEWHILELOOP", use the whileLoopHandler's start logic.
			if (options.step.pluginName?.toLocaleUpperCase() === "INVOKEWHILELOOP") {
				return handler.whileLoopHandler.assertCanStart(options);
			}
		},

		/**
		 * Asserts the provided step by converting it to a rule, forcing the plugin name to "Assert",
		 * and then evaluating the rule to determine which branch to follow in a sequential automation workflow.
		 *
		 * @async
		 * @function assert
		 * @param {Object} step        - The step object containing the rule definition.
		 * @param {string} [step.type] - The type of the step (e.g., "IF").
		 * @returns {Promise<string>} A promise that resolves with the branch name or indication of how the automation logic should proceed.
		 */
		assertPlugin: async (options) => {
			// Store the original step type to restore it after the assertion is complete.
			const originalStepType = options.step.pluginType;

			// Remove the step type to prevent it from being blocked and allow it to run as a rule (Assert).
			options.step.type = undefined;

			// Convert the step object into a rule object that can be processed.
			const rule = options.client.convertToRule(options.step);

			// Override the plugin name to "Assert" specifically for the "If" step.
			// This ensures the correct plugin is invoked in a sequential workflow scenario.
			rule.pluginName = "Assert";

			// Prepare the options object for the step invocation.
			// Note that automation and session are assumed to be accessible in this scope.
			options.rule = rule;

			// Invoke the step asynchronously through the StateMachine and wait for the result.
			const response = await StateMachine.invokeStep(options.client, options);

			// Identify the plugin used based on the step ID and the automation result returned.
			const plugin = options.client.findPlugin(options.step.id, response.automationResult);

			// Assert the plugin to determine the outcome of the assertion.
			const assertion = options.client.assertPlugin(plugin);

			// Update the session from the response for potential use in subsequent steps.
			options.session = response.session;

			// Reset the step type to "originalStepType" so the automation engine knows to continue.
			options.step.type = originalStepType;

			// Store the assertion result for reference in future steps.
			options.step.context['assertion'] = assertion;

			// Evaluate the plugin to determine the assertion outcome (e.g., which branch to follow).
			// This typically returns a string representing the branch name (like "trueBranch" or "falseBranch").
			return {
				assertion,
				plugin
			};
		},

		/**
		 * Retrieves the sequence associated with a given step.
		 *
		 * - If the step's `type` is not "IF" (case-insensitive), it returns `step.sequence` or an empty array.
		 * - If the step's `type` is "IF", it evaluates which branch to follow using `handler.assert(step)`
		 *   and returns that branch from the step object.
		 *
		 * @async
		 * @function getSequence
		 * @param {Object} step            - The step object containing the type and potentially multiple branches
		 * @param {string} [step.type]     - A string representing the type of the step (e.g., "IF")
		 * @param {Array}  [step.sequence] - Default sequence to return if the step is not an "IF" type
		 * @returns {Promise<Array>} A promise that resolves to the appropriate sequence based on the step type
		 */
		getSequence: async (options) => {
			// Convert the type to upper case and check if it is NOT equal to "IF".
			// If it isn't "IF", just return `step.sequence` or an empty array.
			if (options.step.type?.toLocaleUpperCase() !== "IF") {
				return options.step.sequence || [];
			}

			// If the step is an "IF" type, use the handler to figure out which branch to follow.
			// `handler.assert(step)` should return the branch key as a string, e.g., "trueBranch" or "falseBranch".
			const branch = await handler.assertPlugin(options.step).assertion;

			// Return the sequence stored under the key that matches the evaluated branch.
			// This allows dynamic branching based on the result of `handler.assert(step)`.
			return options.step.branches[`${branch}`];
		},

		/**
		 * Selects the given step in the designer interface and moves the viewport so that the step is visible.
		 *
		 * @function initializeStep
		 * @param {Object} step    - An object representing the step to be initialized.
		 * @param {string} step.id - The unique identifier for the step.
		 */
		initializeStep: async (options) => {
			const pluginName = options.step.pluginName.toLocaleUpperCase();

			if (pluginName === "INVOKEFORLOOP") {
				await handler.forLoopHandler.initialize(options);
			}

			if (pluginName === "INVOKEFOREACHLOOP") {
				await handler.foreachLoopHandler.initialize(options);
			}

			// Select the step in the designer by its ID.
			_designer.selectStepById(options.step.id);

			// Adjust the viewport so the selected step is brought into view.
			_designer.moveViewportToStep(options.step.id);
		},

		/**
		 * Resets the designer by disabling read-only mode, allowing full editing capabilities.
		 *
		 * @function resetDesigner
		 */
		resetDesigner: () => {
			// Re-enable editing by setting the designer to non-read-only mode.
			_designer.setIsReadonly(false);
		},

		/**
		 * Pauses execution for the specified number of milliseconds.
		 *
		 * @function waitFlow
		 * @param {number} ms - The number of milliseconds to wait before resolving the Promise.
		 * @returns {Promise<void>} A Promise that resolves after the specified delay.
		 */
		waitFlow: (ms) => {
			// Returns a Promise that resolves after 'ms' milliseconds, effectively pausing execution.
			return new Promise((resolve) => setTimeout(resolve, ms));
		},

		/**
		 * Resolves macros and updates step properties using a specified or derived rule.
		 * This function can convert a step to a rule if none is provided, invoke macro resolution,
		 * and update the step properties accordingly. It also supports specifying a session to
		 * set driver parameters dynamically in the automation config.
		 *
		 * @async
		 * @function
		 * @name resolveMacros
		 * @param {Object} options            - The configuration object for the macro resolution process.
		 * @param {Object} options.step       - The current step configuration object.
		 * @param {Object} [options.rule]     - An optional rule object, bypassing the need to convert the step into a rule.
		 * @param {Object} options.client     - Reference to a client object providing automation utilities (e.g., `convertToRule`, `resolveMacros`).
		 * @param {Object} options.automation - The automation configuration, which includes stages, jobs, and driver parameters.
		 * @param {string} [options.session]  - The session identifier, which can be used to update driver parameters.
		 * @returns {Promise<void>} Resolves when the macro resolution has completed and the step properties have been updated.
		 * @throws Will throw an error if macro resolution fails.
		 */
		resolveMacros: async (options) => {
			// Determine whether to use the provided rule or convert the step into a rule using the client's method.
			const rule = options.rule ? options.rule : options.client.convertToRule(options.step);

			// Create an array of rules, currently containing only the converted or provided rule.
			const rules = [rule];

			// Assign the generated rules to the first job of the first stage in the automation configuration.
			// This assumes that the automation configuration has at least one stage and one job.
			options.automation.stages[0].jobs[0].rules = rules;

			// Update the driver parameters based on the provided session.
			// If `session` is undefined, retain the existing driver parameters.
			// Otherwise, set the driver to reference the new session ID.
			options.automation.driverParameters = options.session === undefined
				? options.automation.driverParameters
				: { driver: `Id(${options.session})` };

			try {
				// Invoke the macro resolution process asynchronously and wait for the result.
				// This method should return the resolved rules after successful invocation.
				const resolvedRule = (await options.client.resolveMacros(options.automation))[0];

				// excludeKeys contains the properties that should not be considered for loop initialization
				const excludeKeys = ["rules"];

				// All property keys except those in excludeKeys
				const properties = Object.keys(options.step.properties).filter(
					(key) => !excludeKeys.includes(key)
				);

				// Update the step properties with the resolved values
				for (const property of properties) {
					options.step.properties[property].value = resolvedRule[property];
				}
			} catch (error) {
				// Handle any errors that occur during the macro resolution invocation.
				console.error('Resolution invocation failed:', error);

				// Rethrow the error after logging to allow further handling upstream.
				throw error;
			}
		},

		forLoopHandler: {
			/**
			 * Determines whether the loop should continue by decrementing the index and checking its value.
			 *
			 * This method decrements the `index` stored in `options.data` and returns `true` if the new value
			 * is greater than or equal to zero, indicating that the loop should continue. If the `index` becomes
			 * negative, it returns `false`, signaling the end of the loop.
			 *
			 * @param {Object} options      - The options object containing loop data and step information.
			 * @param {Object} options.data - An object to store loop-related data, including the `index`.
			 *
			 * @returns {boolean} `true` if the loop should continue; otherwise, `false`.
			 *
			 * @example
			 * // Assuming options.data.index is initially 3
			 * const shouldContinue = forLoopHandler.assert(options); // Decrements index to 2, returns true
			 * console.log(options.data.index); // Outputs: 2
			 */
			assertCanContinue: (options) => {
				// Decrement the 'index' in options.data and check if it's still non-negative
				return --options.step.context['index'] >= 0;
			},

			/**
			 * Checks whether the while loop can start. In this particular handler,
			 * it always returns true, indicating the loop can begin.
			 *
			 * @function
			 * @name assertCanStart
			 * @returns {boolean} - Always returns true for a while loop.
			 */
			assertCanStart: () => {
				return true;
			},

			/**
			 * Initializes the loop index based on the provided step argument.
			 *
			 * This method retrieves the 'Argument' property from the step's properties. If the argument is a valid
			 * number, it sets the `index` in `options.data` to that number. Otherwise, it defaults the `index` to 0.
			 *
			 * @param {Object} options                                - The options object containing step information and data storage.
			 * @param {Object} options.step                           - The step object containing properties used for initialization.
			 * @param {Object} options.step.properties                - An object holding various properties of the step.
			 * @param {Object} options.step.properties.Argument       - The 'Argument' property used to determine the loop count.
			 * @param {any}    options.step.properties.Argument.value - The value of the 'Argument' property, expected to be a number or numeric string.
			 * @param {Object} options.data                           - An object to store loop-related data, including the `index`.
			 *
			 * @returns {void}
			 *
			 * @example
			 * const options = {
			 *     step: {
			 *         properties: {
			 *             'Argument': { value: '5' }
			 *         }
			 *     },
			 *     data: {}
			 * };
			 *
			 * forLoopHandler.initialize(options);
			 * console.log(options.data.index); // Outputs: 5
			 */
			initialize: async (options) => {
				// Resolve any macros in the step properties before proceeding
				await handler.resolveMacros(options);

				// Retrieve the 'Argument' value from the step properties
				const argument = options.step.properties['argument'].value;

				// Check if the argument is a valid integer using a regular expression
				const isNumber = `${argument}`.match(/^\d+$/);

				// Parse the argument to an integer if it's a number; otherwise, default to 0
				const index = isNumber ? parseInt(argument, 10) : 0;

				// Initialize the loop index in options.data with the parsed value
				options.step.context = options.step.context || {};

				// Store the initialized index in options.data for loop tracking
				options.step.context['index'] = index
			}
		},

		foreachLoopHandler: {
			/**
			 * Determines whether the loop can continue by decrementing `index` in `options.step.context`.
			 * If `index` is no longer >= 0, the loop ends and child steps' `onElement` properties
			 * are restored to their original values.
			 *
			 * @param {Object} options      - The options object for the current step and session context.
			 * @param {Object} options.step - The step configuration, which includes a `context` object.
			 * @returns {boolean} True if the loop can continue; false otherwise.
			 */
			assertCanContinue: (options) => {
				// Decrement the 'index' in options.step.context and check if it's still non-negative
				const canContinue = --options.step.context['index'] >= 0;

				// If the index is non-negative, the loop should continue
				if (canContinue) {
					return true;
				}

				// If the loop should not continue, reset the 'onElement' property for each step in the sequence
				for (const index in options.step.sequence) {
					// Retrieve the current step in the sequence
					const step = options.step.sequence[index];

					// Check if the step has an 'onElement' property
					if (step.properties["onElement"]) {
						// Restore the original 'onElement' value
						step.properties["onElement"].value = step.context["originalOnElement"];
					}
				}

				// Return false to indicate the loop should not continue
				return false;
			},

			/**
			 * Checks whether the while loop can start. In this particular handler,
			 * it always returns true, indicating the loop can begin.
			 *
			 * @function
			 * @name assertCanStart
			 * @returns {boolean} - Always returns true for a while loop.
			 */
			assertCanStart: () => {
				return true;
			},

			/**
			 * Initializes the loop by storing the original locator and `onElement` values for each step.
			 * Converts the step to a rule for "Assert" logic to get the element count, which is used
			 * as the loop range (`index` and `total`).
			 *
			 * @async
			 * @param {Object} options            - The options object containing references to the step and related automation objects.
			 * @param {Object} options.step       - The current step configuration object.
			 * @param {Object} options.client     - A reference to the client, which provides automation/rule conversion utilities.
			 * @param {Object} options.automation - The automation instance being executed.
			 * @param {Object} options.session    - The current session state passed between steps.
			 * @returns {Promise<void>} Resolves when initialization is complete.
			 */
			initialize: async (options) => {
				// Resolve any macros in the step properties before proceeding
				await handler.resolveMacros(options);

				// Store the original locator (`onElement`) values for each step in the sequence
				for (const index in options.step.sequence) {
					const step = options.step.sequence[index];
					step.context["originalOnElement"] = step.properties?.onElement?.value || "";
				}

				// Remove the step type to prevent the step from being blocked and allow it to run as a rule (Assert)
				options.step.type = undefined;

				// Convert the step to a rule so it can be invoked in the automation engine
				const rule = options.client.convertToRule(options.step);

				// Override the plugin name to "Assert" for the "If" step
				rule.pluginName = "Assert";
				rule.argument = "{{$ --Condition:ElementsCount --Expected:0 --Operator:Greater}}";

				// Prepare the options object for invoking the step
				const invokeOptions = {
					automation: options.automation,
					rule: rule,
					session: options.session,
					step: options.step
				};

				// Invoke the step asynchronously and wait for the result
				const response = await StateMachine.invokeStep(options.client, invokeOptions);

				// Locate the plugin result in the automation response
				const plugin = options.client.findPlugin(options.step.id, response.automationResult);

				// Extract the 'Actual' value (the element count) and convert it to a number
				const index = parseInt(plugin?.extractions[0]?.entities[0]?.content['Actual'] || 0);

				// Update the session with any new data from the response
				options.session = response.session;

				// Reset the step type to "loop" for the loop to continue
				options.step.type = "loop";

				// Store the initialized index in options.step.context for loop tracking
				options.step.context['total'] = index;
				options.step.context['index'] = index;
			},

			/**
			 * Dynamically sets or updates the `onElement` locator for each step in the sequence
			 * based on the current loop index. This function is only applied to steps where
			 * `pluginName` is "INVOKEFOREACHLOOP".
			 *
			 * @param {Object} options - The options object containing references to the step and related automation objects.
			 * @param {Object} options.step - The current step configuration object.
			 */
			set: (options) => {
				// Only proceed if the current plugin is "INVOKEFOREACHLOOP"
				if (options.step.pluginName.toLocaleUpperCase() !== "INVOKEFOREACHLOOP") {
					return;
				}

				// Calculate the index for the element locator.
				// index = (total steps) - (current step index in the loop)
				const index = options.step.context['total'] - options.step.context['index'];

				// The base locator is the element defined on the main step (if any)
				const baseLocator = options.step.properties?.onElement?.value;

				// Loop through each child step in the sequence and adjust the locator
				for (const step of options.step.sequence) {
					// Retrieve the original `onElement` value, falling back to an empty string if not found
					let onElement = step.context?.originalOnElement || "";

					// Determine the locator type (e.g., "XPATH" or "CSSSELECTOR"); default to "XPATH"
					const locator = step.properties?.locator?.value?.toLocaleUpperCase() || "XPATH";

					// Check if `onElement` starts with '.', indicating a relative reference
					const isSelf = onElement?.startsWith(".");

					// Determine if we're dealing with XPATH or CSS
					const isXpath = locator === "XPATH";
					const isCss = locator === "CSSSELECTOR";

					// If the locator is relative and we are using XPATH, construct a new XPATH
					if (isSelf && isXpath) {
						// Example: (//div[contains(@class, "item")])[2]
						onElement = `(${baseLocator})[${index + 1}]`;
					}

					// If the locator is relative and we are using CSS, construct a new CSS selector
					if (isSelf && isCss) {
						// Example: .some-class:nth-of-type(2)
						onElement = `${baseLocator}:nth-of-type(${index + 1})`;
					}

					// Assign the updated locator back to the step
					if (onElement) {
						step.properties.onElement.value = onElement;
					}
				}
			}
		},

		whileLoopHandler: {
			/**
			 * Evaluates if the while loop can continue on the current iteration.
			 * This method temporarily converts the step into an "Assert" rule,
			 * invokes it, and bases the continuation on the assertion result.
			 *
			 * @async
			 * @function
			 * @name assertCanContinue
			 * @param {Object} options            - The configuration object passed to the method.
			 * @param {Object} options.step       - The current step configuration, including `type`, `pluginName`, `context`, etc.
			 * @param {Object} options.client     - Reference to the automation client (contains methods like `convertToRule` and `assertPlugin`).
			 * @param {Object} options.session    - Current session data passed through the automation flow.
			 * @param {Object} options.automation - The overall automation instance handling the flow.
			 * @returns {Promise<boolean>} - Resolves to `true` if the loop should continue; `false` otherwise.
			 */
			assertCanContinue: async (options) => {
				// Assert if the while loop should continue based on the current options
				return await handler.assertPlugin(options).assertion;
			},

			/**
			 * Checks whether the while loop can start. In this particular handler,
			 * it always returns true, indicating the loop can begin.
			 *
			 * @function
			 * @name assertCanStart
			 * @returns {boolean} - Always returns true for a while loop.
			 */
			assertCanStart: () => {
				return true;
			},

			/**
			 * Performs any necessary initialization for the while loop before
			 * iterations begin. This may include macro resolution, updating
			 * contextual data, etc.
			 *
			 * @async
			 * @function
			 * @name initialize
			 * @param {Object} options            - The configuration object passed to the method.
			 * @param {Object} options.step       - The current step configuration.
			 * @param {Object} options.client     - Reference to the automation client.
			 * @param {Object} options.session    - The current session data.
			 * @param {Object} options.automation - The overall automation instance.
			 * @returns {Promise<void>} - Resolves when the initialization is complete.
			 */
			initialize: async (options) => {
				// Call a method to resolve macros or other dynamic placeholders in the step.
				await handler.resolveMacros(options);
			}
		}
	};

	// Retrieve the current definition from the designer
	const definition = _designer.getDefinition();

	// Initialize the state machine with the definition and handler objects created above
	_stateMachine = new StateMachine(definition, handler);

	// Start the workflow execution using the state machine instance
	// created above and wait for it to complete
	await _stateMachine.start();
}

/**
 * Initializes the workflow designer with manifests, groups, and configurations.
 *
 * @async
 * @returns {Promise<void>} A promise that resolves once the designer is initialized.
 */
async function initializeDesigner() {
	// Create a new G4Client instance.
	const g4Cliet = new G4Client();

	// Fetch manifests and manifest groups from the G4Client.
	const manifests = await g4Cliet.getManifests();
	const manifestsGroups = await g4Cliet.getGroups();

	// Store the manifests and groups in global variables for later use.
	_manifests = manifests;

	// Store the cache in a global variable for later use.
	_cache = await g4Cliet.getCache();

	// Store the cache keys in a global variable for later use.
	_cacheKeys = Object.keys(_cache).map(key => key.toUpperCase());

	// Get the HTML element where the designer will be rendered.
	const designerHtmlElement = document.getElementById('designer');

	// Initialize the workflow's starting definition with the "WriteLog" manifest.
	const initalState = initializeStartDefinition(manifests["WriteLog"]);

	// Create a new start definition object.
	const startDefinition = newStartDefinition(initalState);

	// Initialize a new configuration for the workflow designer.
	const configuration = newConfiguration();

	// Initialize an array to hold all groups.
	const groups = [];

	// Process each manifest group to create the groups for the designer.
	for (const [groupName, manifestsGroup] of Object.entries(manifestsGroups)) {
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
	let containersGroup = containers ? containers : { name: 'Containers', steps: [] };

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
	const excludedTypes = ["AUTOMATION", "ASSERTION", "OPERATOR"];
	_connection.on("ReceiveAutomationStartEvent", (message) => {
		if (excludedTypes.includes(message.type.toUpperCase())) {
			return;
		}

		//try {
		//	// Select the step in the designer by its ID.
		//	_designer.selectStepById(message.id);

		//	// Adjust the viewport so the selected step is brought into view.
		//	_designer.moveViewportToStep(message.id);
		//} catch {
		//	// Do nothing on error, silent handling (no logging or actions)
		//}

		// Log the message to the console.
		console.log(message);
	});

	// Listen for the "ReceiveDefinitionCompleteEvent" message from the server
	_connection.on("ReceiveDefinitionCompleteEvent", (_) => {
		// Indicate that the automation is no longer running
		_stateMachine.isRunning = false;

		// Reset the designer or UI state after all steps have been processed
		_stateMachine.handler.resetDesigner();
	});
}

/**
 * Initializes the default start definition for a state machine workflow.
 *
 * @param {Object} manifest - The manifest object containing necessary metadata for the state machine steps.
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
 * @returns {Object} The configuration object containing all necessary settings.
 *
 * @property {number}  undoStackSize - The maximum number of undo operations allowed.
 * @property {Object}  toolbox       - Configuration for the toolbox UI component.
 * @property {Object}  steps         - Configuration related to step icons and types.
 * @property {Object}  validator     - Validation rules for steps and the root definition.
 * @property {Object}  editors       - Providers for root and step editors.
 * @property {boolean} controlBar    - Flag to enable or disable the control bar.
 *
 * @example
 * const config = newConfiguration();
 * initializeApplication(config);
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

/**
 * Creates a new start definition object for a workflow with default properties.
 *
 * @param {Array} sequence - The sequence of steps or containers to include in the start definition.
 * @returns {Object} An object representing the start definition, containing default properties and the provided sequence.
 */
function newStartDefinition(sequence) {
	return {
		id: uid(),
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
			definition.properties['driverParameters']['capabilities']['firstMatch'] = definition.properties['driverParameters']['capabilities']['firstMatch'] || {};

			// Ensure the 'vendorCapabilities' object exists within 'capabilities'.
			definition.properties['driverParameters']['capabilities']['vendorCapabilities'] = definition.properties['driverParameters']['capabilities']['vendorCapabilities'] || {};

			// Iterate over each key in the provided `value` object.
			for (const key of Object.keys(value)) {
				// Determine if the current key pertains to 'capabilities' with 'firstMatch'.
				const isFirstMatch = key.toLocaleUpperCase() === 'CAPABILITIES' && 'firstMatch' in value[key];

				// Determine if the current key pertains to 'capabilities' with 'alwaysMatch'.
				const isAlwaysMatch = key.toLocaleUpperCase() === 'CAPABILITIES' && 'alwaysMatch' in value[key];

				// Determine if the current key pertains to 'capabilities' with 'vendorCapabilities'.
				const isVendors = key.toLocaleUpperCase() === 'CAPABILITIES' && 'vendorCapabilities' in value[key];

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

				if (isVendors) {
					// Extract the 'vendorCapabilities' object from the input value.
					const vendors = value[key].vendorCapabilities;

					// Iterate over each vendor in 'vendorCapabilities'.
					for (const vendor of Object.keys(vendors)) {
						// Iterate over each property for the current vendor.
						for (const property of Object.keys(vendors[vendor])) {
							// Ensure the vendor object exists within 'vendorCapabilities'.
							capabilities['vendorCapabilities'][vendor] = capabilities['vendorCapabilities'][vendor] || {};

							// Assign the property value to the corresponding vendor and property.
							capabilities['vendorCapabilities'][vendor][property] = vendors[vendor][property];
						}
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
 * Provides a step editor UI component for a given plugin step.
 *
 * This function creates and configures the HTML structure necessary for editing a plugin step,
 * including sections for the plugin's name, properties, and parameters. It utilizes helper functions
 * from the `CustomFields` and other modules to generate form fields dynamically based on the
 * provided step's configuration.
 *
 * @param {Object} step          - The plugin step configuration object.
 * @param {Object} editorContext - The context object for the editor, used for notifying changes.
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

		// normalize the key to ensure it is in the correct format


		// Determine the nature of the parameter to decide which input field to create.
		const label = convertPascalToSpaceCase(convertToPascalCase(key));
		const isListField = _cacheKeys.includes(parameter.type?.toUpperCase());
		const isOptionsField = parameter.optionsList && parameter.optionsList.length > 0;
		const isArray = parameter.type?.toUpperCase() === 'ARRAY';
		const isSwitch = ['SWITCH', 'BOOLEAN', 'BOOL'].includes(parameter.type.toUpperCase());
		const isKeyValue = ['KEY/VALUE', 'KEYVALUE', 'DICTIONARY'].includes(parameter.type.toUpperCase());

		/**
		 * Handles the creation and configuration of a Key-Value input field.
		 * Updates the parameter value and notifies the editor context upon changes.
		 */
		if (isKeyValue) {
			CustomFields.newKeyValueField(
				{
					container: container,
					initialValue: parameter.value,
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

	const initializeSystemContainerEditorProvider = (container, step, type) => {
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
			 *
			 * @param {Object} value - The updated Driver Parameters provided by the user.
			 */
			(value) => {
				// Ensure the 'driverParameters' property exists in the definition.
				step.properties['driverParameters'] = step.properties['driverParameters'] || {};

				// Ensure the 'capabilities' object exists within 'driverParameters'.
				step.properties['driverParameters']['capabilities'] = step.properties['driverParameters']['capabilities'] || {};

				// Ensure the 'firstMatch' object exists within 'capabilities'.
				step.properties['driverParameters']['capabilities']['firstMatch'] = step.properties['driverParameters']['capabilities']['firstMatch'] || {};

				// Ensure the 'vendorCapabilities' object exists within 'capabilities'.
				step.properties['driverParameters']['capabilities']['vendorCapabilities'] = step.properties['driverParameters']['capabilities']['vendorCapabilities'] || {};

				// Iterate over each key in the provided `value` object.
				for (const key of Object.keys(value)) {
					// Determine if the current key pertains to 'capabilities' with 'firstMatch'.
					const isFirstMatch = key.toLocaleUpperCase() === 'CAPABILITIES' && 'firstMatch' in value[key];

					// Determine if the current key pertains to 'capabilities' with 'alwaysMatch'.
					const isAlwaysMatch = key.toLocaleUpperCase() === 'CAPABILITIES' && 'alwaysMatch' in value[key];

					// Determine if the current key pertains to 'capabilities' with 'vendorCapabilities'.
					const isVendors = key.toLocaleUpperCase() === 'CAPABILITIES' && 'vendorCapabilities' in value[key];

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

					if (isVendors) {
						// Extract the 'vendorCapabilities' object from the input value.
						const vendors = value[key].vendorCapabilities;

						// Iterate over each vendor in 'vendorCapabilities'.
						for (const vendor of Object.keys(vendors)) {
							// Iterate over each property for the current vendor.
							for (const property of Object.keys(vendors[vendor])) {
								// Ensure the vendor object exists within 'vendorCapabilities'.
								capabilities['vendorCapabilities'][vendor] = capabilities['vendorCapabilities'][vendor] || {};

								// Assign the property value to the corresponding vendor and property.
								capabilities['vendorCapabilities'][vendor][property] = vendors[vendor][property];
							}
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
	const inputId = newUid();

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
		titleText: convertPascalToSpaceCase(step.pluginName),
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
		initializeSystemContainerEditorProvider(stepEditorContainer, step, "properties");
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
	for (let index = 0; index < sortedProperties.length; index++) {
		// Retrieve the current property key from the sorted list.
		const key = sortedProperties[index];

		// Determine if the current property should be skipped.
		const skip = (hasParameters && key.toUpperCase() === 'ARGUMENT') || key.toUpperCase() === 'RULES';

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
	for (let index = 0; index < sortedParameters.length; index++) {
		// Retrieve the current parameter key from the sorted list.
		const key = sortedParameters[index];

		// Initialize the input field for the current parameter key.
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

// Initialize the designer when the window has loaded
window.addEventListener('load', () => {
	initializeDesigner();
});
