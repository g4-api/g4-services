// Client for sending requests to the G4 API.

class G4Client {
	/**
	 * Creates an instance of G4Client.
	 * @param {string} baseUrl - The base URL for the G4 API.
	 */
	constructor(baseUrl = "/api/v4/g4") {
		// The base URL for the API.
		this.baseUrl = baseUrl;

		// The URL endpoint to invoke an automation sequence.
		this.invokeUrl = `${this.baseUrl}/automation/invoke`;

		// The URL endpoint to initialize an automation sequence.
		this.initializeUri = `${this.baseUrl}/automation/init`;

		// The URL endpoint to resolve macros in an automation sequence.
		this.macrosUrl = `${this.baseUrl}/automation/resolve`;

		// The URL endpoint to fetch plugin manifests.
		this.manifestsUrl = `${this.baseUrl}/integration/manifests`;

		// The URL endpoint for the cache (if needed for future use).
		this.cacheUrl = `${this.baseUrl}/integration/cache`;

		// An in-memory cache to store fetched manifests.
		this.manifests = [];
	}

	/**
	 * Asserts that all entities within each extraction of the plugin response have a valid 'Evaluation' status.
	 *
	 * This method checks whether every extraction in the provided `pluginResponse` contains entities
	 * where the 'Evaluation' field is either missing or explicitly set to `true`. If any entity has
	 * an 'Evaluation' field set to `false` or any other falsy value, the assertion fails.
	 *
	 * @param {Object} pluginResponse - The response object from the plugin containing extraction data.
	 * @param {Array}  pluginResponse.extractions                      - An array of extraction objects to be validated.
	 * @param {Array}  pluginResponse.extractions[].entities           - An array of entity objects within each extraction.
	 * @param {Object} pluginResponse.extractions[].entities[].content - The content object of each entity, potentially containing an 'Evaluation' field.
	 *
	 * @returns {boolean} Returns `true` if all entities pass the validation criteria, otherwise `false`.
	 *
	 * @throws {TypeError} Throws an error if `pluginResponse` is not an object or if `extractions` is not an array.
	 */
	assertPlugin(pluginResponse) {
		// Extract the 'extractions' array from the pluginResponse object
		const extractions = pluginResponse.extractions;

		// Ensure that 'extractions' is an array and perform validation
		if (!extractions || !Array.isArray(extractions) || extractions.length === 0) {
			return false;
		}

		// Ensure that 'extractions' is an array and perform validation
		return extractions.every(extraction =>
			extraction.entities.every(entity => {
				// If the 'Evaluation' field is missing in the entity's content, consider it as false
				if (!('Evaluation' in entity.content)) {
					return false;
				}
				// If the 'Evaluation' field exists, it must be explicitly set to true
				return entity.content["Evaluation"] === true;
			})
		);
	}

	/**
	 * Converts a step configuration object into a rule object.
	 * 
	 * @param {Object} step            - The step object containing all necessary details.
	 * @param {Object} step.context    - An object containing context details, such as $type or model.
	 * @param {string} step.pluginName - The name of the plugin associated with this step.
	 * @param {string} step.id         - A unique identifier for this step.
	 * @param {Object} step.properties - Key-value pairs of property definitions.
	 * @param {Object} step.parameters - Key-value pairs describing parameters and their types.
	 * @param {Array}  step.sequence   - An array of nested steps (if any).
	 * 
	 * @returns {Object} A rule object derived from the given step configuration.
	 */
	convertToRule(step) {
		/**
		 * Converts a condition rule model by processing each branch and its corresponding steps.
		 *
		 * This function iterates through each branch in the provided `step` object,
		 * converts each branch step into a rule using the `convertToRule` method,
		 * and organizes these rules under their respective branches.
		 */
		const convertConditionRuleModel = (step) => {
			// Retrieve all branch names from the step's branches
			const branches = Object.keys(step.branches);

			// Initialize an empty object to store the converted rules organized by branch
			const ruleBranches = {};

			// Iterate over each branch name
			for (const branch of branches) {
				// Get the array of steps associated with the current branch
				const branchSteps = step.branches[branch];

				// Iterate over each step within the current branch
				for (const branchStep of branchSteps) {
					// Convert the current branch step into a rule using the convertToRule method
					const childRule = this.convertToRule(branchStep);

					// If the branch doesn't exist in ruleBranches, initialize it with an empty array
					ruleBranches[branch] = ruleBranches[branch] || [];

					// Add the converted rule to the corresponding branch's array
					ruleBranches[branch].push(childRule);
				}
			}

			// Return the object containing all branches with their respective converted rules
			return ruleBranches;
		};

		/**
		 * Converts an array parameter into a formatted string of command-line arguments.
		 */
		const convertFromArray = (parameter) => {
			// If parameter.value is undefined or null, default it to an empty array.
			parameter.value = parameter.value || [];

			// If no values exist, return an empty string.
			if (parameter.value.length === 0) {
				return "";
			}

			// Extract the name from the parameter object.
			const name = parameter.name;

			// Map each value to a "--name:value" format and join them with spaces.
			return parameter.value.map(item => `--${name}:${item}`).join(" ");
		}

		/**
		 * Converts a dictionary parameter into a formatted string of command-line arguments.
		 */
		const convertFromDictionary = (parameter) => {
			// If parameter.value is undefined or null, default it to an empty object.
			parameter.value = parameter.value || {};

			// Extract all keys from the dictionary.
			const keys = Object.keys(parameter.value);

			// If no keys exist, return an empty string.
			if (keys.length === 0) {
				return "";
			}

			// Extract the name from the parameter object.
			const name = parameter.name;

			// Map each key-value pair to a "--name:key=value" format and join them with spaces.
			return keys.map(key => `--${name}:${key}=${parameter.value[key]}`).join(" ");
		}

		/**
		 * Processes the parameters from the given step object and returns a single aggregated
		 * string of command-line arguments in the format "{{$ --key:value --key2:value2 ...}}".
		 */
		const formatParameters = (step) => {
			// This will hold all parameter tokens, e.g., ["--key:value", "--key2:value2", ...].
			let parameters = [];

			// We’ll reuse parameterToken for each parameter we process.
			let parameterToken = '';

			// Iterate over each parameter within step.parameters.
			for (const key in step.parameters) {
				// Determine the parameter type and transform to uppercase for consistency.
				const parameterType = step.parameters[key].type.toUpperCase();

				// Retrieve the value.
				const value = step.parameters[key].value;

				// Check for different parameter types.
				const isArray = value && parameterType === 'ARRAY';
				const isDictionary = value && (parameterType === 'DICTIONARY'
					|| parameterType === 'KEY/VALUE'
					|| parameterType === 'OBJECT');
				const isBoolean = parameterType === 'SWITCH';
				const isValue = !isDictionary && !isArray && value && value.length > 0;

				// Construct the parameter token based on its type.
				if (isBoolean && isValue) {
					// Boolean type usually doesn't need a value, just the presence of the flag.
					parameterToken = `--${key}`;
				}
				else if (isValue) {
					// Simple string or numeric value type: "--key:value".
					parameterToken = `--${key}:${value}`;
				}
				else if (isArray) {
					// Array type: convert using convertFromArray().
					parameterToken = convertFromArray(step.parameters[key]);
				}
				else if (isDictionary) {
					// Dictionary type: convert using convertFromDictionary().
					parameterToken = convertFromDictionary(step.parameters[key]);
				}
				else if (!parameterToken || parameterToken === "") {
					// If there's no valid token, skip.
					continue;
				}
				else {
					// Otherwise, skip as well.
					continue;
				}

				// Push the resulting token to the parameters array.
				parameters.push(`${parameterToken}`);
			}

			// Join all parameter tokens with spaces and wrap them with the required format "{{$ ...}}".
			return parameters.length > 0 ? `{{$ ${parameters.join(" ")}}}` : "";
		}

		/**
		 * Determines the rule type based on the step's context object. If the context contains a "$type" key,
		 * or if a 'model' property ending with "RuleModel" is found, it returns the appropriate value. 
		 */
		const getRuleType = (step) => {
			// Extract the keys from the step's context object.
			const keys = Object.keys(step.context);

			// Determine the rule type based on the context.
			let type = keys.includes("$type") ? step.context["$type"] : "Action";

			// If step.context.model exists, strip "RuleModel" from the end of the string.
			if (step.context.model) {
				type = step.context.model.replace("RuleModel", "");
			}

			// Return the resolved rule type.
			return type;
		}

		// Construct the base rule object with type, pluginName, and a reference ID.
		const rule = {
			"$type": getRuleType(step),
			"pluginName": step.pluginName,
			"reference": {
				"id": step.id
			},
			"capabilities": {
				"displayName": step.name
			}
		}

		// Iterate over the step's properties to populate the rule object.
		for (const key in step.properties) {
			// Convert the property key to camelCase.
			const propertyKey = Utilities.convertToCamelCase(key);

			// Assign the property's value to the rule object using the camelCase key.
			rule[propertyKey] = step.properties[key].value;
		}

		// Format all parameters into a single argument string.
		const parameters = formatParameters(step);

		// If parameters exist, set them on the rule's argument property.
		if (parameters && parameters !== "") {
			rule.argument = parameters;
		}

		// If the step context model is a condition rule model, convert the branches.
		const model = step?.context?.model?.toUpperCase();
		if (model === "SWITCHRULEMODEL" || model === "CONDITIONRULEMODEL") {
			// Convert the branches to rules and negativeRules.
			const branches = convertConditionRuleModel(step);

			// Assign the branches to the rule object.
			rule.branches = branches;

			// Return the rule object with the branches.
			return rule;
		}

		// If there's no sequence in the step, we can return the rule here.
		if (!step.sequence || step.sequence.length === 0) {
			return rule;
		}

		// Otherwise, process each step in the sequence recursively (assuming 'this.convert' is defined elsewhere).
		const rules = []
		for (const nestedStep of step.sequence) {
			// Convert the nested step to a rule object.
			const childRule = this.convertToRule(nestedStep);

			// If the child rule is not null, add it to the rules array.
			rules.push(childRule);
		}

		// Assign the array of child rules to our main rule under 'rules'.
		rule.rules = rules;

		// Finally, return the fully-constructed rule object.
		return rule;
	}

	/**
	 * Synchronizes a step object with the provided rule by updating its properties and parameters.
	 *
	 * This function processes the given rule to update the corresponding properties of the step.
	 * It handles the conversion of argument strings containing templated variables into parameter dictionaries.
	 *
	 * @param {Object} step - The step object to be synchronized. It contains properties and parameters that may be updated.
	 * @param {Object} rule - The rule object containing key-value pairs that dictate how the step should be updated.
	 * 
	 * @returns {Object} The updated step object after synchronization with the rule.
	 */
	syncStep(step, rule) {
		/**
		 * Formats an argument string into a dictionary if it contains templated variables.
		 */
		const formatArgumentString = (arg) =>
			// Check if the argument contains templated variables by looking for "{{$"
			// Return an empty object if no templated variables are found
			// Convert to a dictionary if templated variables are present
			!arg.includes("{{$")
				? {}
				: _cliFactory.convertToDictionary(arg);

		/**
		 * Formats and copies parameters from the manifest to prevent unintended mutations.
		 *
		 * This function extracts parameters from the provided manifest object, creates copies
		 * of each parameter to ensure immutability, and returns an array of these copied parameters.
		 * By doing so, it safeguards the original manifest parameters from accidental modification
		 */
		const formatParameters = (manifest) => {
			// Initialize an empty array to hold the copied parameters
			const copiedParameters = [];

			// Iterate over each parameter in the manifest's parameters array
			// If manifest or manifest.parameters is undefined, default to an empty array to prevent errors
			for (const parameter of manifest?.parameters || []) {
				// Create a deep copy of the current parameter to ensure immutability
				// Replace the following line with a deep copy method if parameters contain nested objects
				const parameterJson = JSON.stringify(parameter);
				const newParameter = JSON.parse(parameterJson);

				// Add the copied parameter object to the copiedParameters array
				copiedParameters.push(newParameter);
			}

			// Return the array of copied parameter objects
			return copiedParameters;
		};

		// Retrieve the manifest for the step's plugin
		const manifest = _manifests[step.pluginName];

		// Extract parameters from the manifest or default to an empty array
		const parameters = formatParameters(manifest?.parameters || []);

		// Define the list of keys to include when updating step properties
		const includeKeys = [
			"argument",
			"locator",
			"locatorType",
			"onAttribute",
			"onElement",
			"regularExpression",
		];

		// Ensure the step has a parameters object
		step.parameters = step.parameters || {};

		// Populate step.parameters with data from the manifest
		for (const parameter of parameters) {
			const key = Utilities.convertToCamelCase(parameter.name);
			step.parameters[key] = parameter;

			// Join the description array into a single string if it exists
			step.parameters[key].description = Array.isArray(parameter.description)
				? parameter.description?.join('\n').trim()
				: parameter.description?.trim() || "";
		}

		// Iterate over each key in the rule object to update the corresponding step properties
		for (const key in rule) {
			// Skip keys that are not in the includeKeys list or do not exist in step.properties
			if (!includeKeys.includes(key) || !(key in step.properties)) {
				continue;
			}

			// Update the value of the step's property with the value from the rule
			step.properties[key].value = rule[key];
		}

		// Check if the rule contains an 'argument' field to process parameters
		if (rule.argument) {
			// Parse the argument string into a dictionary of parameters
			const parsedParameters = formatArgumentString(rule.argument);

			// Convert parameter keys to uppercase for consistency
			const parameters = Utilities.convertToUpperCase(parsedParameters);
			const parameterKeys = Object.keys(step.parameters);

			// Update step.parameters with values from the parsed argument
			for (const parameterKey of parameterKeys) {
				const key = parameterKey.toUpperCase();
				const value = parameters[key];

				// Use the value from the parsed argument if available; otherwise, retain the existing value
				step.parameters[parameterKey].value = value || step.parameters[parameterKey].value;
			}
		}

		// Return the updated step object after all synchronizations are complete
		return step;
	}

	/**
	 * Searches for a plugin within the provided G4 response model that matches the given reference ID.
	 *
	 * This function traverses the nested structure of the G4 response model, iterating through sessions,
	 * response trees, stages, jobs, and plugins to locate a plugin with a matching reference ID.
	 *
	 * @param {string} referenceId     - The reference ID to search for within the plugins.
	 * @param {Object} g4ResponseModel - The G4 response model object containing nested plugin data.
	 *
	 * @returns {Object|null} The plugin object that matches the reference ID, or null if not found.
	 *
	 * @throws {TypeError} Throws an error if `g4ResponseModel` is not a non-null object or if `referenceId` is not a string.
	 */
	findPlugin(referenceId, g4ResponseModel) {
		/**
		 * Generator function to traverse all plugins within the G4 response model.
		 *
		 * Iterates through each response, session, response tree, stage, job, and plugin,
		 * yielding each plugin encountered.
		 */
		function* traversePlugins(g4ResponseModel) {
			// Iterate over each response in the G4 response model
			for (const response of Object.values(g4ResponseModel)) {
				// Extract the sessions object from the current response
				const sessions = response.sessions;

				// Continue to the next response if sessions is not an object
				if (!sessions || typeof sessions !== 'object') continue;

				// Iterate over each session within the current response
				for (const session of Object.values(sessions)) {
					// Extract the response tree object from the current session
					const responseTree = session.responseTree;

					// Continue to the next session if responseTree is not an object
					if (!responseTree || typeof responseTree !== 'object') continue;

					const stages = responseTree.stages;
					// Continue to the next session if stages is not an array
					if (!Array.isArray(stages)) continue;

					// Iterate over each stage within the response tree
					for (const stage of stages) {
						// Extract the jobs array from the current stage
						const jobs = stage.jobs;

						// Continue to the next stage if jobs is not an array
						if (!Array.isArray(jobs)) continue;

						// Iterate over each job within the current stage
						for (const job of jobs) {
							// Extract the plugins array from the current job
							const plugins = job.plugins;

							// Continue to the next job if plugins is not an array
							if (!Array.isArray(plugins)) continue;

							// Iterate over each plugin within the current job and yield it
							for (const plugin of plugins) {
								yield plugin;
							}
						}
					}
				}
			}
		}

		// Input validation to ensure `g4ResponseModel` is a non-null object
		if (typeof g4ResponseModel !== 'object' || g4ResponseModel === null) {
			throw new TypeError('g4ResponseModel must be a non-null object');
		}

		// Input validation to ensure `referenceId` is a string
		if (typeof referenceId !== 'string') {
			throw new TypeError('referenceId must be a string');
		}

		// Traverse through all plugins and search for the one with the matching reference ID
		for (const plugin of traversePlugins(g4ResponseModel)) {

			// Check if the plugin's performancePoint.reference.id matches the referenceId
			// Return the matching plugin
			if (plugin.performancePoint?.reference?.id === referenceId) {
				return plugin;
			}
		}

		// Return null if no matching plugin is found
		return null;
	}

	/**
	 * Fetches the G4 cache from the API.
	 *
	 * @async
	 * 
	 * @returns {Promise<Object>} A promise that resolves to the cached data retrieved from the API.
	 * 
	 * @throws {Error} Throws an error if the network request fails or the response is not OK.
	 */
	async getCache() {
		try {
			// Fetch the cache data from the API using the cacheUrl endpoint.
			const response = await fetch(this.cacheUrl);

			// Check if the response status indicates success (HTTP 200-299).
			if (!response.ok) {
				throw new Error(`Network response was not ok: ${response.statusText}`);
			}

			// Parse the JSON data from the response.
			const data = await response.json();

			// Return the parsed cache data.
			return data;
		} catch (error) {
			// Log the error to the console for debugging.
			console.error('Failed to fetch G4 cache:', error);

			// Rethrow the error to ensure the caller is aware of the failure.
			throw new Error(error);
		}
	}

	/**
	 * Retrieves and organizes plugin manifests into groups based on their categories and scopes.
	 *
	 * @async
	 * 
	 * @returns {Promise<Object>} A promise that resolves to an object containing grouped manifests. Each group is keyed by category (and optionally scope) with its corresponding manifests.
	 */
	async getGroups() {
		// Fetch the manifests using the existing method.
		const manifests = await this.getManifests();

		// Initialize an empty object to store the groups.
		const groups = {};

		// Iterate through each manifest in the manifests object.
		for (const manifest of Object.values(manifests)) {
			// Ensure the manifest has a 'scopes' array.
			manifest.scopes = manifest.scopes || [];

			// Determine if the manifest has a scope that includes 'ANY' (case-insensitive).
			const isAnyScope = manifest.scopes.some(scope => scope.toUpperCase() === 'ANY') || manifest.scopes.length === 0;

			// Retrieve the categories array or use an empty array if it's undefined.
			const categories = manifest.categories || [];

			// If the manifest has 'ANY' scope, add it to each of its categories.
			if (isAnyScope) {
				for (const category of categories) {
					// Convert the category name to a space-separated string.
					const categoryName = Utilities.convertPascalToSpaceCase(category);

					// Ensure the group exists for this category.
					groups[categoryName] = groups[categoryName] || { name: categoryName, manifests: [] };

					// Add the manifest to the category's group.
					groups[categoryName].manifests.push(manifest);
				}

				// Skip processing other scopes since 'ANY' covers all.
				continue;
			}

			// If no 'ANY' scope, iterate through each category and scope.
			for (const category of categories) {
				for (const scope of manifest.scopes) {
					// Create a combined category name (e.g., "Category (Scope)").
					const categoryName = `${Utilities.convertPascalToSpaceCase(category)} (${Utilities.convertPascalToSpaceCase(scope)})`;

					// Ensure the group exists for this combined category and scope.
					groups[categoryName] = groups[categoryName] || { name: categoryName, manifests: [] };

					// Add the manifest to the combined group's manifests.
					groups[categoryName].manifests.push(manifest);
				}
			}
		}

		// Return the organized groups.
		return groups;
	}

	/**
	 * Fetches and returns G4 manifests of type 'Action'.
	 * Caches the manifests after the first fetch to avoid redundant network requests.
	 * 
	 * @returns {Promise<Object>} A promise that resolves to the manifests object.
	 * 
	 * @throws Will throw an error if the network request fails.
	 */
	async getManifests() {
		// If manifests are already cached, return them directly.
		if (this.manifests.length > 0) {
			return this.manifests;
		}

		// Define the plugin types to include in the fetch request.
		const includeTypes = ["ACTION", "CONTENT", "TRANSFORMER"];

		try {
			// Fetch the plugin manifests from the API.
			const response = await fetch(this.manifestsUrl);

			// Check if the response status is OK (HTTP 200-299).
			if (!response.ok) {
				throw new Error(`Network response was not ok: ${response.statusText}`);
			}

			// Parse the JSON response.
			const data = await response.json();

			// Filter only the plugins of type 'Action' and organize them into a dictionary by `key`.
			this.manifests = data
				.filter(item => includeTypes.includes(item.pluginType.toUpperCase()))
				.reduce((cache, manifest) => {
					// Use the `key` field of the manifest as the dictionary key.
					cache[manifest.key] = manifest;
					return cache;
				}, {});

			// Return the cached manifests.
			return this.manifests;
		} catch (error) {
			// Log the error for debugging and rethrow it.
			console.error('Failed to fetch G4 plugins:', error);
			throw new Error(error);
		}
	}

	/**
	 * Invokes the G4 Automation Sequence by sending a POST request with the provided definition.
	 *
	 * This asynchronous function sends a JSON payload to a predefined automation URL using the Fetch API.
	 * It handles the response by parsing the returned JSON data and managing errors that may occur during the request.
	 *
	 * @async
	 * @function invokeAutomation
	 * 
	 * @param    {Object} definition - The automation definition object to be sent in the POST request body.
	 * 
	 * @returns  {Promise<Object>} - A promise that resolves to the parsed JSON response data from the server.
	 * 
	 * @throws   {Error} - Throws an error if the network response is not ok or if the fetch operation fails.
	 */
	async invokeAutomation(definition) {
		try {
			// Invoke the G4 automation sequence by sending a POST request with the automation definition.
			const response = await fetch(this.invokeUrl, {
				method: 'POST',
				headers: {
					'Content-Type': 'application/json'
				},
				body: JSON.stringify(definition)
			});

			// Check if the response status indicates a successful request (HTTP status code 200-299).
			// If the response is not ok, throw an error with the status text for debugging purposes.
			if (!response.ok) {
				throw new Error(`Network response was not ok: ${response.statusText}`);
			}

			// Parse the JSON data from the successful response.
			const data = await response.json();

			// Return the parsed data for further processing by the caller.
			return data;
		} catch (error) {
			// Log the error to the console for debugging and monitoring purposes.
			console.error('Failed to invoke G4 automation:', error);

			// Rethrow the original error to ensure that the caller can handle it appropriately.
			// Using 'throw error' preserves the original error stack and message.
			throw error;
		}
	}

	/**
	 * Resolves all macros for the G4 Automation Sequence by sending a POST request with the provided definition.
	 *
	 * This asynchronous function sends a JSON payload to a predefined automation URL using the Fetch API.
	 * It handles the response by parsing the returned JSON data and managing errors that may occur during the request.
	 *
	 * @async
	 * @function invokeAutomation
	 * 
	 * @param    {Object} definition - The automation definition object to be sent in the POST request body.
	 * 
	 * @returns  {Promise<Object>} - A promise that resolves to the parsed JSON response data from the server.
	 * 
	 * @throws   {Error} - Throws an error if the network response is not ok or if the fetch operation fails.
	 */
	async resolveMacros(definition) {
		try {
			// Resolve the G4 automation sequence by sending a POST request with the automation definition.
			const response = await fetch(this.macrosUrl, {
				method: 'POST',
				headers: {
					'Content-Type': 'application/json'
				},
				body: JSON.stringify(definition)
			});

			// Check if the response status indicates a successful request (HTTP status code 200-299).
			// If the response is not ok, throw an error with the status text for debugging purposes.
			if (!response.ok) {
				throw new Error(`Network response was not ok: ${response.statusText}`);
			}

			// Parse the JSON data from the successful response.
			const data = await response.json();

			// Return the parsed data for further processing by the caller.
			return data;
		} catch (error) {
			// Log the error to the console for debugging and monitoring purposes.
			console.error('Failed to resolve G4 automation:', error);

			// Rethrow the original error to ensure that the caller can handle it appropriately.
			// Using 'throw error' preserves the original error stack and message.
			throw error;
		}
	}

	/**
	 * Constructs a new automation object using the provided definition, which includes properties for
	 * authentication, driver parameters, settings, and sequences of stages and jobs.
	 *
	 * @function newAutomation
	 * 
	 * @param {Object} definition                               - The definition object describing the automation flow.
	 * @param {Object} definition.properties                    - The properties of the automation, including authentication, driver parameters, and settings.
	 * @param {Object} [definition.properties.authentication]   - Optional authentication details (e.g., tokens or credentials).
	 * @param {Object} [definition.properties.driverParameters] - Optional driver parameters (e.g., capabilities for WebDriver).
	 * @param {Object} [definition.properties.settings]         - Optional additional settings (e.g., timeouts, logging preferences).
	 * @param {Array}  definition.sequence                      - An array of stages, each of which may contain multiple jobs.
	 * 
	 * @returns {Object} The newly constructed automation object containing authentication, driver parameters, settings, and stages.
	 */
	newAutomation(definition) {
		/**
		 * Formats and merges driver parameters, ensuring that vendor-specific capabilities
		 * are placed under the correct keys (e.g., `{vendorName}:options`). This function
		 * constructs a standardized `parameters` object that can be passed to a WebDriver
		 * or similar driver-configuration mechanism.
		 */
		const formatDriverParameters = (driverParameters) => {
			// Extract the main capabilities from the input object.
			// Use an empty object as a default if none are provided.
			const capabilities = driverParameters?.capabilities || {};

			// Extract the firstMatch capabilities array from the input.
			// If not provided, default to an empty array.
			const firstMatch = driverParameters.firstMatch || [];

			// Create a base parameters object with minimal fields:
			// - An empty alwaysMatch object (to be populated later).
			// - The driver and driverBinaries properties, if provided.
			// - A default firstMatch array with a single empty object.
			const parameters = {
				capabilities: {
					alwaysMatch: {}
				},
				driver: driverParameters?.driver,
				driverBinaries: driverParameters?.driverBinaries,
				firstMatch: []
			};

			// Loop over the keys (indexes) of the firstMatch array.
			for (const group of Object.keys(firstMatch)) {
				const value = firstMatch[group];
				parameters.firstMatch.push(value);
			}

			// Assign the provided alwaysMatch capabilities (if any) to the parameters object.
			// If capabilities.alwaysMatch is undefined, this will assign undefined.
			parameters.capabilities.alwaysMatch = capabilities.alwaysMatch;

			// Use the provided firstMatch array if it has any entries.
			// Otherwise, retain the default firstMatch placeholder defined in parameters.
			parameters.firstMatch = firstMatch.length > 0 ? firstMatch : parameters.firstMatch;

			// Return the fully constructed and merged parameters object.
			return parameters;
		};

		/**
		 * Formats the external repositories settings by filtering out any repository entries
		 * that do not have both a URL and a version.
		 */
		const formatExternalRepositories = (settings) => {
			// If settings is falsy, return undefined immediately.
			if (!settings) {
				return undefined;
			}

            // Ensure the pluginsSettings object exists and is an object.
			settings.pluginsSettings = settings.pluginsSettings || {};
            settings.pluginsSettings.externalRepositories = settings.pluginsSettings.externalRepositories || {};

			// Retrieve the plugins settings; use an empty object if not present.
			const pluginsSettings = settings.pluginsSettings || {};

			// Retrieve the externalRepositories from the plugins settings; default to an empty object.
			const externalRepositories = pluginsSettings.externalRepositories || {};

			// Initialize an array to hold the valid repository objects.
			const repositories = [];

			// Get all keys from the externalRepositories object.
			const keys = Object.keys(externalRepositories);

			// Iterate over each key in the externalRepositories object.
			for (const key of keys) {
				// Get the repository object corresponding to the current key.
				const repository = externalRepositories[key];

				// Validate that the repository has both a URL and a version.
				// If either is missing, skip this repository.
				if (!repository.url || !repository.version) {
					continue;
				}

				// Add the repository to the array if it meets the criteria.
				repositories.push(repository);
			}

			// Update the externalRepositories property with the filtered array.
			settings.pluginsSettings.externalRepositories = repositories;

			// Return the modified settings object.
			return settings;
		};

		/**
		 * Retrieves driver parameters if both driver and driverBinaries are provided.
		 */
		const getDriverParameters = (driverParameters) => {
			// Check if driverBinaries exists and contains at least one element.
			const isBinaries = driverParameters?.driverBinaries && driverParameters?.driverBinaries.length > 0;

			// Check if driver exists and is a non-empty string.
			const isDriver = driverParameters?.driver && driverParameters?.driver.length > 0;

			// Return the original object if both conditions are met; otherwise, return an empty object.
			return isBinaries && isDriver ? driverParameters : undefined;
		};

		// Extract the authentication parameters from the definition properties.
		const authentication = definition.properties?.authentication;

        // Extract the data source from the definition properties.
        const dataSource = definition.properties?.dataSource;

        // Extract the driver parameters if both driver and driverBinaries are provided.
		let driverParameters = getDriverParameters(definition.properties?.driverParameters);

		// Extract and format the driver parameters from the definition properties using the helper function.
		driverParameters = driverParameters ? formatDriverParameters(driverParameters) : undefined;

		// Extract additional settings (if any) from the definition properties.
		const settings = formatExternalRepositories(definition.properties["settings"] || undefined);

		// Prepare an array to collect stages from the definition sequence.
		const stages = [];

		// Iterate over each stage in the definition's sequence.
		for (const stage of definition.sequence) {
            // Extract the driver parameters for the current stage.
			let driverParameters = getDriverParameters(stage.properties?.driverParameters);

			// Construct a new stage object with minimal required properties.
			const newStage = {
				// Stage-level driver parameters (if provided).
				driverParameters: driverParameters ? formatDriverParameters(driverParameters) : undefined,

				// A reference object that captures key metadata about the stage.
				reference: {
					description: stage.description,
					id: stage.id,
					name: stage.name,
				},

				// Each stage contains multiple jobs.
				jobs: [],
			};

			// Iterate over each job in the current stage.
			for (const job of stage.sequence) {
                // Extract the driver parameters for the current job.
				let driverParameters = getDriverParameters(job.properties?.driverParameters);

				// Construct a new job object.
				const newJob = {
					// Job-level driver parameters (if provided).
					driverParameters: driverParameters ? formatDriverParameters(driverParameters) : undefined,

					// A reference object that captures key metadata about the job.
					reference: {
						description: job.description,
						id: job.id,
						name: job.name,
					},

					// Each job contains multiple rules derived from its steps.
					rules: [],
				};

				// Convert each step in the job's sequence into a rule, then add it to the job.
				for (const step of job.sequence) {
					const rule = this.convertToRule(step);
					newJob.rules.push(rule);
				}

				// Add the populated job to the current stage.
				newStage.jobs.push(newJob);
			}

			// After processing all jobs, add the completed stage to the main stages array.
			stages.push(newStage);
		}

		// Return a newly constructed automation object containing all relevant data.
		return {
            // Include the authentication details in the automation object.
			authentication,

            // Include the data source details in the automation object.
			dataSource,

			// Include optional driver parameters (e.g., session data).
			driverParameters,

			// Include any additional settings (e.g., timeouts, logging).
			settings,

			// Provide the fully constructed set of stages (each containing jobs and rules).
			stages,
		};
	}
}
