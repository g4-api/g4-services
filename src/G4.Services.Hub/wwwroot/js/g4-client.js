// Client for sending requests to the G4 API.
const _exportDataManifest = {
	"author": {
		"link": "https://www.linkedin.com/in/roei-sabag-247aa18/",
		"name": "Roei Sabag"
	},
	"categories": [
		"DataExtraction"
	],
	"context": {
		"integration": {
			"sequentialWorkflow": {
				"$type": "Extraction",
				"componentType": "container",
				"iconProvider": {
					"name": "export"
				},
				"model": "ExtractionRuleModel"
			}
		}
	},
	"description": [
		"### Purpose",
		"",
		"The ExportData plugin gathers data by running one or more ContentRule steps and writes the combined results to your chosen destination via a G4DataProviderModel.",
		"It makes it easy to organize data extraction tasks by breaking them into modular steps.",
		"The plugin handles errors consistently, letting you choose to continue or stop the workflow on failures.",
		"It ensures that extracted data flows smoothly into files, variables, or databases.",
		"",
		"### Key Features and Functionality",
		"",
		"| Feature           | Description                                                                                      |",
		"|-------------------|--------------------------------------------------------------------------------------------------|",
		"| Meta-Invocation   | Calls multiple ContentRule plugins by their static key in sequence.                              |",
		"| Aggregation       | Combines outputs of each invocation into a single dataset.                                       |",
		"| Flexible Delivery | Sends the combined payload through any G4DataProviderModel (file, memory, database, etc.).       |",
		"| Error Handling    | Clears extraction state and logs exceptions on error, with options to continue or halt workflow. |",
		"",
		"### Usages in RPA",
		"",
		"| Use Case                   | Description                                                                                              |",
		"|----------------------------|----------------------------------------------------------------------------------------------------------|",
		"| Export to CSV              | Extracts data from web pages or applications and writes it to CSV files for reporting or analysis.       |",
		"| Load into Database         | Runs content rules to pull data and loads results directly into a database table for further processing. |",
		"| In-Memory Variable Passing | Gathers multiple datasets and stores them in workflow variables for downstream steps to consume.         |",
		"| Combined File Generation   | Aggregates outputs and writes a consolidated file (JSON, XML) for archival or integration tasks.         |",
		"",
		"### Usages in Automation Testing",
		"",
		"| Use Case                         | Description                                                                                             |",
		"|----------------------------------|---------------------------------------------------------------------------------------------------------|",
		"| Validate Extraction Accuracy     | Runs content rules in test flows and compares output against expected datasets to ensure correctness.   |",
		"| Mock Data Provider Integration   | Simulates different G4DataProviderModel implementations to test delivery logic without real endpoints.  |",
		"| Error Scenario Testing           | Forces extraction errors to verify that the plugin logs exceptions and respects continue/halt settings. |",
		"| End-to-End Workflow Verification | Uses ExportData in test cases to confirm that data moves correctly from extraction to storage layers.   |"
	],
	"examples": [

	],
	"key": "ExportData",
	"manifestVersion": 4,
	"parameters": [
		{
			"description": [
				"Defines which portion of a web page or HTML source to extract.",
				"Choosing the correct scope ensures your automation retrieves the specific content you need."
			],
			"mandatory": true,
			"name": "Scope",
			"type": "ExtractionScope"
		}
	],
	"platforms": [
		"Any"
	],
	"pluginType": "Action",
	"properties": [
		{
			"description": [
				"Argument allows adding extra command-line information and options.",
				"It supports specifying parameters such as scope to control command behavior.",
				"Use this parameter to pass additional flags and customize execution."
			],
			"mandatory": false,
			"name": "Argument",
			"type": "String|Expression"
		},
		{
			"description": [
				"Defines configuration settings for data delivery using a provider that follows the G4DataProviderModel schema.",
				"It specifies the provider type, data source, repository details, and processing flags.",
				"Including authentication credentials and capability settings ensures secure and tailored data collection.",
				"The forEntity flag controls whether data is processed as a single entity."
			],
			"mandatory": false,
			"name": "DataCollector",
			"type": "Any"
		},
		{
			"description": [
				"Specifies a unique static identifier for a rule within the current automation context.",
				"Identifiers enable reliable referencing and management of rules.",
				"Using a consistent key prevents naming conflicts and simplifies rule tracking."
			],
			"mandatory": false,
			"name": "Key",
			"type": "String"
		},
		{
			"description": [
				"Specifies the method used to locate an element or source for extraction, such as XPath, CSS selector, or element ID.",
				"Accurate locator strategies ensure the automation targets the correct content on the page.",
				"Selecting the appropriate locator improves reliability and maintainability of extraction tasks."
			],
			"mandatory": false,
			"name": "Locator",
			"type": "String"
		},
		{
			"description": [
				"Defines the HTML element or selector to which the content rule will be applied.",
				"Accurate targeting ensures the rule only affects the intended part of the page.",
				"Choosing precise selectors reduces errors and improves the reliability of content extraction."
			],
			"mandatory": true,
			"name": "OnElement",
			"type": "String"
		},
		{
			"description": [
				"Rules defines a list of content rule objects that will be executed in sequence.",
				"Each rule extracts a specific field and maps it into the output schema.",
				"Use this list to specify all extraction steps needed for structured data."
			],
			"mandatory": false,
			"name": "Rules",
			"type": "Array"
		}
	],
	"protocol": {
		"apiDocumentation": "None",
		"w3c": "None"
	},
	"summary": [
		"The ExportData plugin applies a named content rule to extract data from a web page.",
		"It uses a G4DataProviderModel to deliver the extracted data to files, databases, or other targets.",
		"Users can narrow the extraction by setting the scope to BodyHtml, specific elements, or the full page source.",
		"Optional locator and onElement parameters let you focus on individual elements and keep extraction rules and delivery settings separate for easier maintenance."
	]
};

class G4Client {
	/**
	 * Creates an instance of G4Client.
	 * @param {string} baseUrl - The base URL for the G4 API.
	 */
	constructor(baseUrl = "/api/v4/g4") {
		// The base URL for the API.
		this.baseUrl = baseUrl;

		// The URL endpoint for the cache (if needed for future use).
		this.cacheUrl = `${this.baseUrl}/integration/cache`;

		// The URL endpoint to manage files.
		this.filesUrl = `${this.baseUrl}/integration/files`;
		
		// The URL endpoint to invoke an automation sequence.
		this.invokeUrl = `${this.baseUrl}/automation/invoke`;

		// The URL endpoint to initialize an automation sequence.
		this.initializeUri = `${this.baseUrl}/automation/init`;

		// The URL endpoint to resolve macros in an automation sequence.
		this.macrosUrl = `${this.baseUrl}/automation/resolve`;

		// The URL endpoint to fetch plugin manifests.
		this.manifestsUrl = `${this.baseUrl}/integration/manifests`;

        // The URL endpoint to stop an ongoing automation sequence.
        this.stopUrl = `${this.baseUrl}/automation/stop`;

        // The URL endpoint to fetch SVG assets.
		this.svgsUrl = `${this.baseUrl}/integration/svgs`;

		// An in-memory cache to store fetched manifests.
		this.manifests = {};
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

			// We'll reuse parameterToken for each parameter we process.
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
					|| parameterType === 'KEYVALUE'
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

		/**
		 * Returns a stable list of valid Base64 field names for rule serialization.
		 *
		 * @param {*} fieldNames - Candidate field names from the step capabilities.
		 *
		 * @returns {string[]} Unique string field names in deterministic order.
		 *
		 * @remarks This helper is compute-only so capability cleanup cannot mutate editor state.
		 */
		const getBase64EncodedFieldNames = (fieldNames) => {
			if (!Array.isArray(fieldNames)) {
				return [];
			}

			return [...new Set(
				fieldNames.filter((fieldName) => typeof fieldName === 'string')
			)].sort((leftName, rightName) => leftName.localeCompare(rightName));
		};

		/**
		 * Creates the serialized capabilities while retaining unopened legacy Base64 state.
		 *
		 * @param {Object} currentStep - Step whose capabilities are being serialized.
		 *
		 * @returns {Object} Capabilities safe to include in the generated rule.
		 *
		 * @remarks This helper is compute-only and never infers Base64 state from field values.
		 */
		const getRuleCapabilities = (currentStep) => {
			// Start with the display contract required by every serialized rule.
			const capabilities = {
				"displayName": currentStep.name
			};
			const base64EncodedFields = currentStep.capabilities?.base64EncodedFields;
			const isBase64EncodedFieldsObject = Utilities.assertObject(base64EncodedFields)
				&& !Array.isArray(base64EncodedFields);

			// Normalize a valid per-field registry so mixed Base64 states survive a rule round trip.
			if (isBase64EncodedFieldsObject) {
				const parameters = getBase64EncodedFieldNames(base64EncodedFields.parameters);
				const properties = getBase64EncodedFieldNames(base64EncodedFields.properties);
				const normalizedFields = {};

				if (parameters.length > 0) {
					normalizedFields.parameters = parameters;
				}

				if (properties.length > 0) {
					normalizedFields.properties = properties;
				}

				const isRegistryPopulated = Object.keys(normalizedFields).length > 0;

				if (isRegistryPopulated) {
					capabilities.base64EncodedFields = normalizedFields;
				}

				return capabilities;
			}

			// Preserve unopened legacy steps without inspecting values; the editor performs migration.
			if (currentStep.capabilities?.isBase64Encoded === true) {
				capabilities.isBase64Encoded = true;
			}

			return capabilities;
		};

		/**
		 * Resolves the child rule and transformer collections for the current model shape.
		 *
		 * @param {Object} currentStep - Step that may own nested rules or transformers.
		 * @param {string|undefined} model - Normalized model name used by the serializer.
		 *
		 * @returns {{rules: Array, transformers: Array}} Collections to recursively serialize.
		 *
		 * @remarks This helper is compute-only so model-specific selection stays out of rule mutation.
		 */
		const getRuleChildren = (currentStep, model) => {
			const children = {
				rules: currentStep.sequence || [],
				transformers: []
			};

			// Only content models can redirect nested collections to switch branches.
			if (model !== "CONTENTRULEMODEL") {
				return children;
			}

			const isSwitchComponent = currentStep.componentType.toUpperCase() === "SWITCH";

			if (!isSwitchComponent) {
				return children;
			}

			return {
				rules: currentStep.branches["Actions"],
				transformers: currentStep.branches["Transformers"]
			};
		};

		/**
		 * Recursively converts a homogeneous collection of nested steps.
		 *
		 * @param {Array} nestedSteps - Steps to convert using the current client instance.
		 *
		 * @returns {Array} Serialized child rules in their original order.
		 */
		const convertRuleSteps = (nestedSteps) => {
			return nestedSteps.map((nestedStep) => this.convertToRule(nestedStep));
		};

        // Get the rule type from the step's context.
        const ruleType = getRuleType(step);

		// Isolate capability normalization so the main conversion flow remains model-focused.
		const capabilities = getRuleCapabilities(step);

		// Construct the base rule object with type, pluginName, and a reference ID.
		const rule = {
			"$type": ruleType,
			"pluginName": step.pluginName,
			"reference": {
				"id": step.id
			},
			"capabilities": capabilities
		};

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
		if (parameters !== "") {
		rule.argument = parameters;
		}

		// Branch-oriented models own named child collections and complete serialization here.
		const model = step?.context?.model?.toUpperCase();
		const isConditionBranchModel = ["CONDITIONRULEMODEL", "SWITCHRULEMODEL"].includes(model);

		if (isConditionBranchModel) {
			rule.branches = convertConditionRuleModel(step);

			return rule;
		}

		// Resolve the model-specific child sources before recursively extending the rule.
		const children = getRuleChildren(step, model);

		if (children.rules.length > 0) {
			rule.rules = convertRuleSteps(children.rules);
		}

		if (children.transformers.length > 0) {
			rule.transformers = convertRuleSteps(children.transformers);
		}

		// Return the fully constructed rule after all applicable child collections are attached.
		return rule;
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
		// Input validation to ensure `g4ResponseModel` is a non-null object
		if (typeof g4ResponseModel !== 'object' || g4ResponseModel === null) {
			throw new TypeError('g4ResponseModel must be a non-null object');
		}

		// Input validation to ensure `referenceId` is a string
		if (typeof referenceId !== 'string') {
			throw new TypeError('referenceId must be a string');
		}

		// Traverse through all plugins and search for the one with the matching reference ID
		for (const plugin of getResponsePlugins(g4ResponseModel)) {

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

            // Attach the ExportData manifest to the cache data under the "Action" key.
			data["Action"]["ExportData"] = {
				document: "# ExportData",
				manifest: _exportDataManifest
			}

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
	 * Retrieves the list of static files available on the G4 integration server.
	 *
	 * This method calls the configured `this.filesUrl` endpoint, which is expected
	 * to return a JSON array of file paths relative to the base URL.
	 *
	 * Example response:
	 * [
	 *   "index.html",
	 *   "css/designer.css",
	 *   "images/icon-user.svg"
	 * ]
	 *
	 * @returns {Promise<string[]>} An array of file paths.
	 * @throws {Error} If the request fails or the server returns a non-success response.
	 */
	async getFiles() {
		try {
			// Send HTTP request to fetch the files list from the server
			const response = await fetch(this.filesUrl);

			// Validate that the HTTP response status is successful (200–299)
			if (!response.ok) {
				throw new Error(`Network response was not ok: ${response.statusText}`);
			}

			// Parse the response body as JSON
			const data = await response.json();

			// Return the parsed file list
			return data;
		} catch (error) {

			// Log the error for debugging and troubleshooting
			console.error('Failed to fetch G4 files list:', error);

			// Rethrow the error to ensure callers are aware of the failure
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
		const includeTypes = new Set(["ACTION", "CONTENT", "TRANSFORMER"]);

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
				.filter(item => includeTypes.has(item.pluginType.toUpperCase()))
				.reduce((cache, manifest) => {
					// Use the `key` field of the manifest as the dictionary key.
					cache[manifest.key] = manifest;
					return cache;
				}, {});

			// Push the ExportData manifest into the manifests array.
			this.manifests["ExportData"] = _exportDataManifest;

			// Return the cached manifests.
			return this.manifests;
		} catch (error) {
			// Log the error for debugging and rethrow it.
			console.error('Failed to fetch G4 plugins:', error);
			throw new Error(error);
		}
	}

	/**
	 * Retrieves the available G4 SVG definitions from the configured endpoint.
	 *
	 * This asynchronous function sends a GET request to the predefined SVG URL using the Fetch API.
	 * It validates the HTTP response status, parses the returned JSON payload, and handles
	 * any errors that may occur during the request lifecycle.
	 *
	 * @async
	 * @function getSvgs
	 * 
	 * @returns  {Promise<Object>} - A promise that resolves to the parsed JSON response containing the SVG definitions.
	 * 
	 * @throws   {Error} - Throws an error if the network response is not ok or if the fetch operation fails.
	 */
	async getSvgs() {
		try {
			// Send an HTTP GET request to retrieve the SVG definitions from the server.
			const response = await fetch(this.svgsUrl);

			// Check if the response status indicates a successful request (HTTP status code 200-299).
			// If the response is not ok, throw an error with the status text for debugging purposes.
			if (!response.ok) {
				throw new Error(`Network response was not ok: ${response.statusText}`);
			}

			// Parse the JSON data from the successful response.
			const data = await response.json();

			// Return the parsed SVG data for further processing by the caller.
			return data;
		} catch (error) {
			// Log the error to the console for debugging and monitoring purposes.
			console.error('Failed to fetch G4 SVGs:', error);

			// Rethrow the original error to ensure that the caller can handle it appropriately.
			// Using 'throw error' preserves the original error stack and message.
			throw error;
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
		 * Normalizes the MCP server settings collection by converting each server entry
		 * from an object that contains a name field into a dictionary keyed by that name.
		 */
		const formatMcpServers = (settings) => {
			// Return undefined immediately when no settings object was supplied.
			if (!settings) {
				return undefined;
			}

			// Ensure the plugin settings container exists before accessing nested MCP server values.
			settings.pluginsSettings = settings.pluginsSettings || {};

			// Ensure the MCP servers container exists so the normalization logic can safely iterate over it.
			settings.pluginsSettings.servers = settings.pluginsSettings.servers || {};

			// Read the plugin settings object after normalizing its existence.
			const pluginsSettings = settings.pluginsSettings || {};

			// Read the raw MCP server collection, defaulting to an empty object when missing.
			const mcpServers = pluginsSettings.servers || {};

			// Get all server entry keys from the raw MCP server collection.
			const keys = Object.keys(mcpServers);

			// Create the normalized servers dictionary keyed by each server's internal name.
			const servers = {};

			// Convert each raw server entry into a name-keyed dictionary entry.
			for (const key of keys) {
				// Read the current MCP server definition from the source collection.
				const mcpServer = mcpServers[key];

				// Read the display or logical name that will become the new dictionary key.
				const name = mcpServer.name;

				// Copy all server fields except the name property into the new value object.
				const value = Object
					.fromEntries(Object
						.entries(mcpServer)
						.filter(([key]) => key !== 'name'));

				// Store the normalized server definition under its name.
				servers[name] = value;
			}

			// Replace the original server collection with the normalized dictionary.
			settings.pluginsSettings.servers = servers;

			// Return the updated settings object.
			return settings;
		};

		/**
		 * Retrieves driver parameters if both driver and driverBinaries are provided.
		 */
		const getDriverParameters = (driverParameters) => {
			// Check if driver exists and is a non-empty string.
			const isDriver = driverParameters?.driver && driverParameters?.driver.length > 0;

			// Return the original object if both conditions are met; otherwise, return an empty object.
			return isDriver ? driverParameters : undefined;
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
		let settings = definition.properties["settings"] || undefined;
		settings = formatExternalRepositories(settings);
		settings = formatMcpServers(settings);

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

		// Include a reference object with metadata about the automation.
		// ID is aligned with the definition ID for traceability.
		const reference = {
			name: definition?.properties?.title,
			id: definition.id
		}

		// Return a newly constructed automation object containing all relevant data.
		return {
			// Include the authentication details in the automation object.
			authentication,

			// Include the data source details in the automation object.
			dataSource,

			// Include optional driver parameters (e.g., session data).
			driverParameters,

			// Include a reference object with metadata about the automation.
			reference,

			// Include any additional settings (e.g., timeouts, logging).
			settings,

			// Provide the fully constructed set of stages (each containing jobs and rules).
			stages,
		};
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
		 * Converts an array (or object of values) of strings in "key=value" format into a dictionary object.
		 */
		const convertArrayToDictionary = (input) => {
			// Normalize input into an array: if it's already an array, use it; otherwise, take the object's values.
			const array = Array.isArray(input) ? input : Object.values(input);

			// This will hold the final key/value mappings.
			const result = {};

			// Process each string item in the array
			for (const item of array) {
				// Locate the first "=" character
				const firstEqualIndex = item.indexOf('=');

				if (firstEqualIndex === -1) {
					// No "=" found: use the whole string as a key and assign an empty value
					result[item] = '';
				} else {
					// Split at the first "=" into key and value parts
					const key = item.substring(0, firstEqualIndex);
					const value = item.substring(firstEqualIndex + 1);
					result[key] = value;
				}
			}

			// Return the populated dictionary
			return result;
		};

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
		const formatParameters = (parameters) => {
			// Initialize an empty array to hold the copied parameters
			const copiedParameters = [];

			// Iterate over each parameter in the manifest's parameters array
			// If manifest or manifest.parameters is undefined, default to an empty array to prevent errors
			for (const parameter of parameters) {
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

		/**
		 * Returns the editor-ready description while preserving the manifest's supported formats.
		 *
		 * @param {Array|string|undefined} description - Manifest description to normalize.
		 *
		 * @returns {string} Trimmed description text for the step editor.
		 */
		const getParameterDescription = (description) => {
			if (Array.isArray(description)) {
				return description.join('\n').trim();
			}

			return description?.trim() || "";
		};

		/**
		 * Tests whether a parsed value should use dictionary conversion for its parameter type.
		 *
		 * @param {string} parameterType - Uppercase manifest parameter type.
		 * @param {*} value - Parsed command-line value for the parameter.
		 *
		 * @returns {boolean} Whether dictionary conversion applies.
		 *
		 * @remarks This helper preserves the legacy requirement that DICTIONARY needs a parsed value.
		 */
		const testDictionaryParameter = (parameterType, value) => {
			const isPopulatedDictionary = Boolean(value) && parameterType === 'DICTIONARY';
			const isKeyValueType = ['KEY/VALUE', 'KEYVALUE', 'OBJECT'].includes(parameterType);

			return isPopulatedDictionary || isKeyValueType;
		};

		/**
		 * Copies manifest parameters into the step while retaining editor-owned display names.
		 *
		 * @param {Object} currentStep - Step whose parameter definitions are being refreshed.
		 * @param {Array} manifestParameters - Cloned manifest parameter definitions.
		 *
		 * @remarks This helper intentionally mutates only currentStep.parameters.
		 */
		const setManifestParameters = (currentStep, manifestParameters) => {
			// Refresh definitions from the manifest without discarding display names owned by the editor.
			for (const parameter of manifestParameters) {
				const key = parameter.name;

				parameter.displayName = currentStep.parameters[key]?.displayName;
				currentStep.parameters[key] = parameter;
				currentStep.parameters[key].description = getParameterDescription(parameter.description);
			}
		};

		/**
		 * Copies supported rule fields into existing step property definitions.
		 *
		 * @param {Object} currentStep - Step whose property values are being synchronized.
		 * @param {Object} currentRule - Rule providing serialized property values.
		 * @param {string[]} includedKeys - Rule keys supported by the step editor.
		 *
		 * @remarks This helper intentionally mutates only properties already declared by the step.
		 */
		const setRuleProperties = (currentStep, currentRule, includedKeys) => {
			// Copy only the intersection of supported rule keys and declared step properties.
			for (const key in currentRule) {
				const isIncludedKey = includedKeys.includes(key);
				const isStepProperty = key in currentStep.properties;
				const isSupportedProperty = isIncludedKey && isStepProperty;

				if (!isSupportedProperty) {
					continue;
				}

				currentStep.properties[key].value = currentRule[key];
			}
		};

		/**
		 * Applies one parsed command-line value according to its manifest parameter type.
		 *
		 * @param {Object} parameter - Step parameter definition and current value.
		 * @param {string} parameterKey - Original parameter name used by the step.
		 * @param {Object} parsedParameters - Parsed parameters keyed with uppercase names.
		 *
		 * @remarks This helper mutates only the supplied parameter and preserves missing scalar values.
		 */
		const setParameterValue = (parameter, parameterKey, parsedParameters) => {
			const key = parameterKey.toUpperCase();
			const value = parsedParameters[key];
			const parameterType = parameter.type?.toUpperCase() || 'STRING';
			const isSwitch = Boolean(value) && parameterType === 'SWITCH';
			const isDictionary = testDictionaryParameter(parameterType, value);

			// A present switch token represents true regardless of the token's serialized value.
			if (isSwitch) {
				parameter.value = "true";
				return;
			}

			// Scalar parameters retain their current value when the argument does not provide one.
			if (!isDictionary) {
				parameter.value = value || parameter.value;
				return;
			}

			// Dictionary-like parameters convert only populated parsed collections.
			if (value) {
				parameter.value = convertArrayToDictionary(value);
			}
		};

		/**
		 * Parses the rule argument and applies its values to every declared step parameter.
		 *
		 * @param {Object} currentStep - Step whose parameter values are being synchronized.
		 * @param {string} argument - Serialized command-line argument from the rule.
		 *
		 * @remarks This helper owns parameter-value mutation while parsing remains delegated.
		 */
		const setRuleParameters = (currentStep, argument) => {
			// Normalize parsed keys once so manifest parameter names can be matched case-insensitively.
			const parsedArgument = formatArgumentString(argument);
			const parsedParameters = Utilities.convertToUpperCase(parsedArgument);

			// Apply each parsed value through the type-specific mutation contract.
			for (const parameterKey of Object.keys(currentStep.parameters)) {
				setParameterValue(currentStep.parameters[parameterKey], parameterKey, parsedParameters);
			}
		};

		// Retrieve the manifest for the step's plugin
		const manifest = _manifests[step.pluginName];

		// Extract parameters from the manifest or default to an empty array
		const parameters = formatParameters(manifest?.parameters || []);

		// Define the list of keys to include when updating step properties
		const includeKeys = [
			"argument",
			"dataCollector",
			"key",
			"locator",
			"locatorType",
			"onAttribute",
			"onElement",
			"regularExpression"
		];

		// Ensure the step has a parameters object
		step.parameters = step.parameters || {};

		// Refresh parameter definitions and supported property values before applying argument values.
		setManifestParameters(step, parameters);
		setRuleProperties(step, rule, includeKeys);

		// Parse arguments only when present so absent rule arguments leave parameter values untouched.
		if (rule.argument) {
			setRuleParameters(step, rule.argument);
		}

		// Return the updated step object after all synchronizations are complete
		return step;
	}

	/**
	 * Stops an active G4 Automation sequence using its identifier.
	 *
	 * This asynchronous function sends a GET request to the predefined stop URL
	 * with the provided automation definition ID. It validates the HTTP response,
	 * parses the returned JSON payload, and handles any errors that may occur
	 * during the request lifecycle.
	 *
	 * @async
	 * @function stopAutomation
	 * 
	 * @param    {Object} definition - The automation definition object containing the automation ID to stop.
	 * 
	 * @returns  {Promise<Object>} - A promise that resolves to the parsed JSON response returned by the server.
	 * 
	 * @throws   {Error} - Throws an error if the network response is not ok or if the fetch operation fails.
	 */
	async stopAutomation(definition) {
		try {
			// Send an HTTP GET request to stop the automation using its unique identifier.
			const response = await fetch(`${this.stopUrl}/${definition.id}`);

			// Check if the response status indicates a successful request (HTTP status code 200-299).
			// If the response is not ok, throw an error with the status text for debugging purposes.
			if (!response.ok) {
				throw new Error(`Network response was not ok: ${response.statusText}`);
			}

			// Parse the JSON data from the successful response.
			const data = await response.json();

			// Return the parsed response data for further processing by the caller.
			return data;
		} catch (error) {
			// Log the error to the console for debugging and monitoring purposes.
			console.error('Failed to stop G4 automation:', error);

			// Rethrow the original error to ensure that the caller can handle it appropriately.
			// Using 'throw error' preserves the original error stack and message.
			throw error;
		}
	}
}

/**
 * Enumerates the plugins declared by one job.
 *
 * @param {Object} job - Job that may expose a plugin collection.
 *
 * @returns {Generator<Object>} Plugins yielded in their original order.
 *
 * @remarks This compute-only generator keeps malformed jobs isolated from valid siblings.
 */
function* getJobPlugins(job) {
	const plugins = job.plugins;

	if (!Array.isArray(plugins)) {
		return;
	}

	// Delegate directly to the array iterator so consumers retain lazy, ordered access.
	yield* plugins;
}

/**
 * Enumerates the plugins contained in one response entry.
 *
 * @param {Object} response - Response that may contain a sessions object.
 *
 * @returns {Generator<Object>} Plugins yielded from valid sessions in source order.
 *
 * @remarks This compute-only generator validates only the response-to-session boundary it owns.
 */
function* getResponseEntryPlugins(response) {
	const sessions = response.sessions;
	const isSessionsObject = sessions !== null && typeof sessions === 'object';

	if (!isSessionsObject) {
		return;
	}

	// Delegate each session so deeper validation does not increase this layer's nesting.
	for (const session of Object.values(sessions)) {
		yield* getSessionPlugins(session);
	}
}

/**
 * Enumerates every plugin contained in a G4 response model.
 *
 * The generator preserves source order and lazy iteration so callers can stop immediately after
 * finding a matching plugin.
 *
 * @param {Object} g4ResponseModel - Validated response model containing nested plugin collections.
 *
 * @returns {Generator<Object>} Plugins yielded in their original traversal order.
 *
 * @remarks This compute-only generator delegates each structural layer to keep complexity bounded.
 */
function* getResponsePlugins(g4ResponseModel) {
	// Delegate each response independently so malformed branches do not hide valid siblings.
	for (const response of Object.values(g4ResponseModel)) {
		yield* getResponseEntryPlugins(response);
	}
}

/**
 * Enumerates the plugins reachable from one session.
 *
 * @param {Object} session - Session that may contain a staged response tree.
 *
 * @returns {Generator<Object>} Plugins yielded from valid stages in source order.
 *
 * @remarks This compute-only generator validates only the session-to-stage boundaries it owns.
 */
function* getSessionPlugins(session) {
	const responseTree = session.responseTree;
	const isResponseTreeObject = responseTree !== null && typeof responseTree === 'object';

	if (!isResponseTreeObject) {
		return;
	}

	const stages = responseTree.stages;

	if (!Array.isArray(stages)) {
		return;
	}

	// Delegate each stage so job and plugin validation remain isolated at their own layers.
	for (const stage of stages) {
		yield* getStagePlugins(stage);
	}
}

/**
 * Enumerates the plugins reachable from one stage.
 *
 * @param {Object} stage - Stage that may expose a job collection.
 *
 * @returns {Generator<Object>} Plugins yielded from valid jobs in source order.
 *
 * @remarks This compute-only generator validates only the stage-to-job boundary it owns.
 */
function* getStagePlugins(stage) {
	const jobs = stage.jobs;

	if (!Array.isArray(jobs)) {
		return;
	}

	// Delegate each job so plugin validation and yielding remain lazy and independently testable.
	for (const job of jobs) {
		yield* getJobPlugins(job);
	}
}
