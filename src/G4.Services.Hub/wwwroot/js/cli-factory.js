class CliFactory {
    /**
     * Gets the regular expression pattern for extracting keys from individual CLI arguments.
     */
    get argumentKeyPattern() {
        return /^[^:]*/is;
    }

    /**
     * Gets the regular expression pattern for extracting values from individual CLI arguments.
     */
    get argumentValuePattern() {
        return /(?<=:).*$/is;
    }

    /**
     * Gets the regular expression pattern for extracting individual CLI arguments from the CLI template.
     */
    get argumentPattern() {
        return /(?<=--)(.*?)(?=\s+--[\w/,.$*]|$)/gis;
    }

    /**
     * Gets the regular expression pattern for extracting the CLI template from a larger string.
     */
    get cliTemplatePattern() {
        return /(?<={{[$]).*(?=(}}))/is;
    }

    /**
     * Gets the regular expression pattern for extracting nested CLI expressions within the template.
     */
    get nestedCliExpressionPattern() {
        return /{{[$].*?(?<={{[$]).*}}/gis;
    }

    /**
     * Confirms the validity of a Command-Line Interface (CLI) against the current CLI template pattern.
     * 
     * @param {string} cli - The CLI to confirm.
     * 
     * @returns {boolean} True if the CLI is valid against the current CLI template pattern, otherwise false.
     */
    confirmCli(cli) {
        // Ensure the CLI is not null
        cli = cli || '';

        // Assert that the CLI matches the template pattern
        return this.cliTemplatePattern.test(cli);
    }

    /**
     * Converts a Command-Line Interface (CLI) string into a dictionary of key-value pairs using default patterns.
     * 
     * @param {string} cli - The CLI string to convert.
     * 
     * @returns {Object} A dictionary of parsed CLI arguments with case-insensitive keys.
     */
    convertToDictionary(cli) {
        // Check if the 'cli' string is null, undefined, or empty.
        // If 'cli' is falsy, return an empty object.
        if (!cli) {
            return {};
        }

        // Use the cliTemplatePattern to extract the relevant part of the CLI string.
        const cliMatch = this.cliTemplatePattern.exec(cli);

        // If a match is found, trim any leading/trailing whitespace; otherwise, use an empty string.
        let cleanCli = cliMatch ? cliMatch[0].trim() : '';

        // Extract nested expressions from the clean CLI string using the nestedCliExpressionPattern.
        // The nested expressions are replaced with placeholders to simplify further processing.
        const nestedExpressionMap = CliFactory._exportNestedExpressions(cleanCli, this.nestedCliExpressionPattern);

        // Iterate over each nested expression and replace its occurrence in the cleanCli with its placeholder.
        for (const [originalExpression, placeholder] of Object.entries(nestedExpressionMap)) {
            cleanCli = cleanCli.replace(originalExpression, placeholder);
        }

        // Use the argumentPattern to find all CLI arguments in the cleaned CLI string.
        const argumentMatches = cleanCli.matchAll(this.argumentPattern);

        // Convert the iterator to an array, trim each argument, and filter out any empty strings.
        const argumentsList = Array.from(argumentMatches, match => match[0].trim()).filter(arg => arg);

        // Parse the list of arguments into a dictionary mapping keys to their corresponding values.
        const argumentsDict = CliFactory._exportKeyValues(
            argumentsList,
            this.argumentKeyPattern,
            this.argumentValuePattern
        );

        // Serialize the arguments dictionary to a JSON string to facilitate placeholder replacement.
        let argumentsJson = JSON.stringify(argumentsDict);

        // Iterate over the nestedExpressionMap to replace placeholders with the original nested expressions.
        for (const [originalExpression, placeholder] of Object.entries(nestedExpressionMap)) {
            // Replace the placeholder in the JSON string with the JSON-stringified original expression.
            argumentsJson = argumentsJson.replace(placeholder, originalExpression);
        }

        // Deserialize the JSON string back into a JavaScript object.
        const collection = JSON.parse(argumentsJson);

        // Return the final collection of parsed CLI arguments.
        return collection;
    }

    // Extracts key-value pairs from a collection of arguments based on specified key and value patterns.
    static _exportKeyValues(argumentsList, keyPattern, valuePattern) {
        // Local function to extract a value from an argument using a provided regex pattern
        const extractValue = (argument, pattern) => {
            // Execute the regex pattern on the argument to find a match
            const match = pattern.exec(argument);

            // If a match is found, return the first matched group; otherwise, return an empty string
            return match ? match[0] : '';
        };

        // Initialize an empty object to group arguments by their keys
        const groups = {};

        // Iterate over each argument in the provided list
        for (const argument of argumentsList) {
            // Execute the keyPattern regex on the current argument to find the key
            const keyMatch = keyPattern.exec(argument);

            // If a key is found, use it; otherwise, default to an empty string
            const key = keyMatch ? keyMatch[0] : '';

            // Check if the key already exists in the groups object
            // If not, initialize an empty array for this key to store corresponding values
            if (!groups[key]) {
                groups[key] = [];
            }

            // Extract the value from the argument using the valuePattern regex
            const value = extractValue(argument, valuePattern);

            // Add the extracted value to the array corresponding to the current key
            groups[key].push(value);
        }

        // Initialize an empty object to store the final results
        const results = {};

        // Iterate over each key and its associated array of arguments in the groups object
        for (const [groupKey, groupArgs] of Object.entries(groups)) {
            // Convert the group key to PascalCase using a utility function from CliFactory
            const key = Utilities.convertToPascalCase(groupKey);

            // Assign the array of arguments to the results object under the PascalCase key
            // If the groupArgs array is empty, assign an empty array; otherwise, assign the groupArgs array
            results[key] = groupArgs.length === 0 ? [] : groupArgs;
        }

        // Return the final results object containing all key-value pairs
        return results;
    }

    // Exports nested expressions by matching them against a pattern and encoding them in Base64.
    static _exportNestedExpressions(cli, expressionPattern) {
        // Use `matchAll` to find all matches of the expressionPattern within the cli string.
        // Convert the iterator returned by `matchAll` into an array of matched strings.
        const nestedExpressions = Array.from(cli.matchAll(expressionPattern), match => match[0]);

        // Initialize an empty object to store the mapping of expressions to their Base64-encoded values.
        const expressionMap = {};

        // Iterate over each matched expression in the nestedExpressions array.
        nestedExpressions.forEach(expression => {
            // Convert the current expression to Base64 and store it in the expressionMap.
            // The key is the original expression, and the value is its Base64-encoded version.
            expressionMap[expression] = Utilities.convertToBase64(expression);
        });

        // Return the final mapping of expressions to their Base64-encoded values.
        return expressionMap;
    }
}
