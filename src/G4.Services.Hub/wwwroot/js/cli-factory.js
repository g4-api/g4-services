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

        // Extract the fully balanced nested expressions from the clean CLI string. Each nested
        // expression is swapped for a placeholder so the argument splitter below cannot break apart
        // a value that legitimately contains "--" inside a nested expression.
        const nestedExpressionMap = CliFactory._exportNestedExpressions(cleanCli);

        // Swap every occurrence of each nested expression for its placeholder. split/join replaces
        // all occurrences literally, so a value that repeats the same nested expression is protected
        // consistently rather than only on its first occurrence.
        for (const [originalExpression, placeholder] of Object.entries(nestedExpressionMap)) {
            cleanCli = cleanCli.split(originalExpression).join(placeholder);
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

        // Restore every placeholder back to its original nested expression. split/join is used
        // instead of String.replace for two reasons: it restores all occurrences of a repeated
        // expression, and it inserts the expression literally so "$" sequences inside the expression
        // are never interpreted as replacement patterns (for example "$&" or "$1").
        for (const [originalExpression, placeholder] of Object.entries(nestedExpressionMap)) {
            argumentsJson = argumentsJson.split(placeholder).join(originalExpression);
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

    // Exports every top-level nested expression as a map from the original expression to a
    // Base64-encoded placeholder that is safe from the argument splitter.
    static _exportNestedExpressions(cli) {
        // Use the depth-aware scanner so a fully nested expression is captured as one complete unit
        // rather than being truncated at the first inner "}}".
        const nestedExpressions = CliFactory._getNestedExpressions(cli);

        // Map each original expression to its Base64 placeholder. Identical expressions collapse to a
        // single entry because the object key is the expression text itself.
        const expressionMap = {};

        for (const expression of nestedExpressions) {
            expressionMap[expression] = Utilities.convertToBase64(expression);
        }

        return expressionMap;
    }

    /**
     * Extracts every top-level, fully balanced `{{$...}}` expression from a CLI fragment.
     *
     * @remarks
     * A regular expression cannot match balanced, nested delimiters, so this scanner walks the
     * string and tracks brace depth to capture each outermost `{{$...}}` span in full. Expressions
     * nested inside a captured span are intentionally not returned on their own: they are protected
     * as part of their parent span and restored together with it. Scanning resumes after each
     * captured span so only sibling top-level expressions are collected. An unterminated expression
     * (an opening `{{$` with no balancing `}}`) is captured to the end of the string so malformed
     * input cannot spin the loop.
     *
     * @param {string} cli - The CLI fragment to scan for nested expressions.
     *
     * @returns {string[]} The complete top-level `{{$...}}` expressions, in the order found.
     */
    static _getNestedExpressions(cli) {
        // Collect each complete top-level expression as it is discovered.
        const expressions = [];

        // Track the scan position across the whole string so every sibling expression is found.
        let searchIndex = 0;

        while (searchIndex < cli.length) {
            // Locate the next macro opener; when none remains the scan is complete.
            const startIndex = cli.indexOf('{{$', searchIndex);

            if (startIndex === -1) {
                break;
            }

            // Walk forward from the opener counting brace depth until it returns to zero, which marks
            // the matching close of this outermost expression.
            let depth = 0;
            let scanIndex = startIndex;
            let endIndex = -1;

            while (scanIndex < cli.length) {
                const isOpeningBrace = cli.startsWith('{{', scanIndex);
                const isClosingBrace = cli.startsWith('}}', scanIndex);

                if (isOpeningBrace) {
                    depth++;
                    scanIndex += 2;
                    continue;
                }

                if (isClosingBrace) {
                    depth--;
                    scanIndex += 2;

                    // Depth back to zero means this closing pair balances the original opener.
                    if (depth === 0) {
                        endIndex = scanIndex;
                        break;
                    }

                    continue;
                }

                // Any other character is ordinary content inside the expression.
                scanIndex++;
            }

            // An unterminated expression has no balancing close; capture the remainder and stop so a
            // malformed argument cannot spin the outer loop.
            if (endIndex === -1) {
                expressions.push(cli.slice(startIndex));
                break;
            }

            // Capture the fully balanced span, then resume scanning after it for sibling expressions.
            expressions.push(cli.slice(startIndex, endIndex));
            searchIndex = endIndex;
        }

        return expressions;
    }
}
