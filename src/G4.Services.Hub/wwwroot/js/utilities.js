class Utilities {
    /**
     * Determines whether a given value is an object.
     *
     * This function checks if the provided value is an object, which includes
     * objects, arrays, and functions. It explicitly excludes `null` and all
     * primitive types such as boolean, number, string, symbol, bigint, and undefined.
     *
     * @param {*} value - The value to be checked.
     * 
     * @returns {boolean} - Returns `true` if the value is an object, `false` if it's a primitive type.
     */
    static assertObject(value) {
        // Exclude `null` since `typeof null` returns 'object', but it's a primitive.
        if (value === null) {
            return false;
        }

        // Check if the type of the value is 'object' or 'function'.
        // In JavaScript, functions are considered objects.
        return (typeof value === 'object' || typeof value === 'function');
    }

    /**
     * Converts a JSON string into a JavaScript object.
     *
     * This static method attempts to parse the provided string as JSON.
     * If the parsing fails (e.g., due to invalid JSON format), it returns null.
     *
     * @param {string} value - The JSON string to convert.
     * 
     * @returns {Object|null} The parsed JavaScript object if the string is valid JSON; otherwise, null.
     */
    static convertFromJson(value) {
        try {
            // Attempt to parse the JSON string.
            return JSON.parse(value);
        } catch {
            // If parsing fails, return null.
            return null;
        }
    }

    /**
     * Parses a given string and converts it to its corresponding boolean value.
     *
     * The function recognizes the following case-insensitive string representations:
     * - `'true'`, `'1'`, `'yes'`, `'y'`, `'on'` → `true`
     * - `'false'`, `'0'`, `'no'`, `'n'`, `'off'` → `false`
     *
     * If the input string does not match any recognized boolean representations,
     * the function returns `null` to indicate an unparseable value.
     *
     * @param {string} str - The string to parse into a boolean.
     * 
     * @returns {boolean|null} - The parsed boolean value or `null` if parsing fails.
     */
    static convertStringToBool(str) {
        if (typeof str !== 'string') {
            console.warn(`parseStringToBool: Expected a string but received type '${typeof str}'.`);
            return null;
        }

        // Trim whitespace and convert the string to lowercase for case-insensitive comparison
        const normalizedStr = str.trim().toLowerCase();

        // Define mappings of string representations to boolean values
        const trueValues = ['true', '1', 'yes', 'y', 'on'];
        const falseValues = ['false', '0', 'no', 'n', 'off'];

        if (trueValues.includes(normalizedStr)) {
            return true;
        } else if (falseValues.includes(normalizedStr)) {
            return false;
        } else {
            console.warn(`parseStringToBool: Unable to parse '${str}' to a boolean.`);
            return null;
        }
    }

    /**
     * Converts a PascalCase string to a space-separated string.
     *
     * @param {string} str - The PascalCase string to convert.
     * 
     * @returns {string} - The converted space-separated string.
     */
    static convertPascalToSpaceCase(str) {
        return str.replace(/([A-Z])/g, ' $1').trim();
    }

    /**
     * Converts a given string to its Base64-encoded representation.
     *
     * This method takes a UTF-8 string, encodes it into bytes, and then
     * converts those bytes into a Base64-encoded string. It's useful for
     * encoding data that needs to be safely transmitted or stored in environments
     * that are not binary-safe.
     *
     * @param {string} str - The input string to be encoded into Base64.
     * 
     * @returns {string} The Base64-encoded representation of the input string.
     */
    static convertToBase64(str) {
        // Create a new TextEncoder instance to encode the string into UTF-8 bytes.
        const encoder = new TextEncoder();

        // Encode the input string into a Uint8Array of UTF-8 bytes.
        const utf8Bytes = encoder.encode(str);

        // Initialize an empty string to accumulate the binary representation.
        let binary = '';

        // Iterate over each byte in the Uint8Array.
        // Convert each byte to its corresponding ASCII character and append to the binary string.
        utf8Bytes.forEach(byte => binary += String.fromCharCode(byte));

        // Use the built-in btoa function to encode the binary string to Base64.
        return btoa(binary);
    }

    /**
     * Converts a given string to camelCase.
     *
     * The function processes the input string by:
     * 1. Removing any non-alphanumeric separators (e.g., spaces, dashes, underscores).
     * 2. Capitalizing the first letter of each word except the first one.
     * 3. Ensuring the first character of the resulting string is in lowercase.
     *
     * @param {string} str - The input string to be converted to camelCase.
     * 
     * @returns {string} - The camelCase version of the input string. Returns 'N/A' if the input is falsy.
     */
    static convertToCamelCase(str) {
        // If the input string is falsy (e.g., null, undefined, empty), return 'N/A'.
        if (!str) {
            return 'N/A';
        }

        // Replace any non-alphanumeric characters followed by a character with the uppercase of that character.
        // This removes separators and capitalizes the following letter.
        const camelCased = str.replace(/[^a-zA-Z0-9]+(.)/g, (_, chr) => chr.toUpperCase());

        // Convert the first character to lowercase to adhere to camelCase conventions.
        return camelCased.charAt(0).toLowerCase() + camelCased.slice(1);
    }

    /**
     * Converts a given input to an integer.
     *
     * This function takes any input, converts it to a string, and attempts to parse it into an integer.
     * If the parsed value is not a valid number (NaN) or an error occurs during conversion,
     * the function returns 0.
     *
     * @param {any} value - The input value to convert. It will be coerced to a string.
     * 
     * @returns {number} - The resulting integer, or 0 if the conversion fails.
     */
    static convertToInt(value) {
        try {
            // Convert the input to a string using template literals and parse it as an integer with base 10.
            const num = parseInt(`${value}`, 10);

            // Check if the result is NaN. If so, return 0; otherwise, return the parsed integer.
            return isNaN(num) ? 0 : num;
        } catch (error) {
            // Although parseInt typically doesn't throw errors, this catch block is a safeguard.
            // Return 0 if any unexpected error occurs during conversion.
            return 0;
        }
    }

    /**
     * Converts a JavaScript value into a formatted JSON string.
     *
     * This static method attempts to convert the provided value into a JSON string
     * with an indentation of 4 spaces for readability.
     * If the conversion fails (e.g., if the value contains circular references), it returns null.
     *
     * @param {*} value - The JavaScript value to convert to JSON. This can be any type that is JSON serializable.
     * 
     * @returns {string|null} The formatted JSON string if conversion is successful; otherwise, null.
     */
    static convertToJson(value) {
        try {
            // Attempt to stringify the value to a JSON string with 4-space indentation.
            return JSON.stringify(value, null, 4);
        } catch {
            // If stringification fails (for example, due to circular references), return null.
            return null;
        }
    }

    /**
     * Converts a camelCase string to PascalCase.
     * 
     * @param {string} str - The camelCase string.
     * 
     * @returns {string} The PascalCase version of the string.
     */
    static convertToPascalCase(str) {
        return str ? str[0].toUpperCase() + str.slice(1) : "";
    }

    /**
     * Converts all keys in a dictionary to uppercase.
     *
     * This helper function takes an input dictionary and returns a new dictionary with all keys transformed to uppercase.
     *
     * @param {Object} dict - The original dictionary with keys to be transformed.
     * 
     * @returns {Object} A new dictionary with all keys in uppercase.
     */
    static convertToUpperCase(dict) {
        return Object.entries(dict).reduce((accumulator, [key, value]) => {
            accumulator[key.toUpperCase()] = value;
            return accumulator;
        }, {});
    }

    /**
     * Invokes an event of a specified type on all elements matching a selector.
     *
     * @param {Object}  options            - Options for the event invocation.
     * @param {string}  options.selector   - CSS selector to target elements.
     * @param {string}  options.type       - The type of event to create and dispatch (e.g., "click", "input").
     * @param {boolean} options.bubbles    - Whether the event bubbles up through the DOM.
     * @param {boolean} options.cancelable - Whether the event's default action can be prevented.
     */
    static invokeEvent(options) {
        // Query all elements in the document that match the provided selector.
        const elements = document.querySelectorAll(options.selector) || [];

        // Create a new event with the specified type and properties.
        const event = new Event(options.type, {
            bubbles: options.bubbles,
            cancelable: options.cancelable
        });

        // Loop through each element and dispatch the event.
        for (const element of elements) {
            element.dispatchEvent(event);
        }
    }

    /**
     * Generates a unique identifier (UID) as a hexadecimal string.
     *
     * @returns {string} A unique identifier generated by combining a random number and converting it to a hexadecimal string.
     */
    static newUid() {
        return Math.ceil(Math.random() * 10 ** 16).toString(16);
    }

    /**
     * Adjusts the height of a textarea element based on its content while enforcing a maximum number of lines.
     * 
     * The function resets the textarea height to 'auto' to calculate the full content height, then adjusts
     * the height while ensuring that it does not exceed the height corresponding to the maximum allowed lines.
     * If the content fits within the maximum height, it hides the vertical scrollbar; otherwise, it enables scrolling.
     *
     * @param {HTMLTextAreaElement} textarea - The textarea element to be resized.
     * @param {number} maxLines              - The maximum number of lines the textarea can expand to.
     */
    static setTextareaSize(textarea, maxLines) {
        // Reset the height to 'auto' so the scrollHeight reflects the content's full height.
        textarea.style.height = 'auto';

        // Determine the full height of the content within the textarea.
        const contentHeight = textarea.scrollHeight;

        // Retrieve computed styles to calculate line height and minimum height.
        const computedStyle = window.getComputedStyle(textarea);
        const lineHeight = parseFloat(computedStyle.lineHeight);
        const minHeight = parseFloat(computedStyle.minHeight);

        // Calculate the maximum allowed height based on the specified maximum number of lines.
        const maxHeight = lineHeight * maxLines;

        if (contentHeight <= maxHeight) {
            // If content fits within the maximum allowed height, adjust the textarea height accordingly.
            // Ensure that the height is not set below the minimum height.
            textarea.style.height = contentHeight > minHeight ? `${contentHeight}px` : `${minHeight}px`;

            // Hide the vertical scrollbar as the content does not require scrolling.
            textarea.style.overflowY = 'hidden';
        } else {
            // If content exceeds the maximum allowed height, restrict the textarea height.
            textarea.style.height = `${maxHeight}px`;

            // Enable the vertical scrollbar to allow the user to scroll through the overflowing content.
            textarea.style.overflowY = contentHeight === 0 ? 'hidden' : 'scroll';
        }
    }

    /**
     * Toggles the theme of the application between the default and dark modes.
     *
     * This method checks the current theme by inspecting the 'href' attribute of the
     * <link> element with the ID 'theme-stylesheet'. It then switches the theme
     * to the opposite mode by updating the 'href' accordingly.
     */
    static switchMode() {
        // Retrieve the <link> element that holds the current theme stylesheet using its ID
        const themeStylesheet = document.getElementById('theme-stylesheet');

        // Get the current stylesheet path and convert it to lowercase for comparison
        const theme = themeStylesheet.getAttribute('href').toLowerCase();

        // Define the default (blueprint) theme stylesheet path
        const defaultTheme = './css/designer-blueprint-parameters.css';

        // Define the dark theme stylesheet path
        const darkTheme = './css/designer-blueprint-parameters-dark.css';

        // Toggle the theme:
        // If the current theme is the default theme, switch to the dark theme;
        // otherwise, switch back to the default theme.
        if (theme === defaultTheme) {
            themeStylesheet.setAttribute('href', darkTheme);
        } else {
            themeStylesheet.setAttribute('href', defaultTheme);
        }
    }

    /**
    * Waits for a DOM element matching the specified selector to appear within a given timeout.
    *
    * This function repeatedly checks for the presence of a DOM element that matches the provided
    * CSS selector at regular intervals. If the element is found within the specified timeout period,
    * the promise resolves with the found element. If the timeout is reached without finding the element,
    * the promise rejects with an error.
    *
    * @param {string} selector - The CSS selector of the element to wait for.
    * @param {number} [timeout=5000] - The maximum time to wait for the element in milliseconds. Default is 5000ms.
    * 
    * @returns {Promise<HTMLElement>} A promise that resolves with the found HTMLElement or rejects with an error if the timeout is reached.
    */
    static waitForElement(selector, timeout = 5000) {
        return new Promise((resolve, reject) => {
            // Interval time in milliseconds between each check
            const interval = 100;

            // Tracks the total elapsed time
            let elapsedTime = 0;

            /**
             * Repeatedly checks for the presence of the element.
             *
             * - If the element is found, clears the interval and resolves the promise with the element.
             * - If the timeout is reached without finding the element, clears the interval and rejects the promise with an error.
             */
            const intervalId = setInterval(() => {
                // Attempt to find the element
                const element = document.querySelector(selector);

                if (element) {
                    clearInterval(intervalId); // Stop further checks
                    resolve(element);          // Resolve the promise with the found element
                } else {
                    // Increment the elapsed time
                    elapsedTime += interval;

                    // Stop further checks
                    // Reject the promise with an error
                    if (elapsedTime >= timeout) {
                        clearInterval(intervalId);
                        reject(new Error(`Timeout waiting for element: ${selector}`));
                    }
                }
            }, interval);
        });
    }
}
