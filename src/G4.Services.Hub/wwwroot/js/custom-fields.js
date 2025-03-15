/**
 * Creates a new field container with a labeled input and a hint icon.
 *
 * @param {string} id               - The unique identifier for the input field. This should match the `id` of the corresponding input element.
 * @param {string} labelDisplayName - The display text for the label associated with the input field.
 * @param {string} hintText         - The hint text to display when the hint icon is clicked.
 * 
 * @returns {HTMLDivElement} The created `div` element containing the labeled input and hint icon.
 */
const newFieldContainer = (id, labelDisplayName, hintText) => {
    /**
     * Toggles a hint text element inside a specified container.
     * If the hint text element already exists, it is removed. Otherwise, a new one is created and added.
     *
     * @param {HTMLElement} hintContainer - The parent container where the hint text will be toggled.
     * @param {string}      hintText      - The text to display inside the hint element.
     */
    const switchHint = (hintContainer, hintText) => {
        // Escape the ID of the container to ensure it can be safely used in a CSS selector.
        const escapedId = CSS.escape(hintContainer.id);

        // Query for an existing hint text element within the hintContainer.
        let hintElement = document.querySelector(`#${escapedId} > div.sqd-help-text`);

        // If the hint text element already exists, remove it and exit the function.
        if (hintElement) {
            hintElement.remove();
            return;
        }

        // Create a new hint text element if it doesn't already exist.
        hintElement = document.createElement('div');
        hintElement.classList.add('sqd-help-text'); // Add the 'sqd-help-text' class for styling.
        hintElement.innerHTML = hintText;       // Set the provided hint text as the content.

        // Append the newly created hint text element to the hintContainer.
        hintContainer.appendChild(hintElement);
    };

    // Create a new `div` element to serve as the container for the label and hint icon
    const titleContainer = document.createElement('div');
    const labelContainer = document.createElement('div');
    const hintContainer = document.createElement('div');
    const controllerContainer = document.createElement('div');

    // Set the data-g4-role attribute to 'field' for the field container
    titleContainer.setAttribute('data-g4-role', 'field');
    titleContainer.id = `${id}-field`;

    // Set the data-g4-role attribute to 'label' for the label container
    labelContainer.setAttribute('data-g4-role', 'label');
    labelContainer.id = `${id}-label`;

    // Set the data-g4-role and id attributes to for the hint container
    hintContainer.setAttribute('data-g4-role', 'hint');
    hintContainer.id = `${id}-hint`;

    // Set data-g4-role attribute to 'controller' for the controller container
    controllerContainer.setAttribute('data-g4-role', 'controller');
    controllerContainer.id = `${id}-controller`;

    // Create a new `label` element
    const labelElement = document.createElement('span');
    labelElement.classList.add('sqd-label--with-help-icon');

    // Create a new `span` element to serve as the hint icon
    const iconElement = document.createElement('span');
    iconElement.id = `${id}-icon`;
    iconElement.classList.add('sqd-help-icon-container');
    iconElement.tabIndex = 0;
    iconElement.title = 'More Information';
    iconElement.setAttribute('role', 'img');
    iconElement.setAttribute('aria-label', 'More Information');

    /**
     * Replace the initially created empty `label` element with a more complex structure
     * using `outerHTML`. This includes the label text and a hint icon with an SVG.
     *
     * Note:
     * - `for` attribute links the label to the input element with the corresponding `id`.
     * - The `sqd-help-icon-container` is interactive, indicated by `tabindex="0"`, allowing keyboard navigation.
     * - `role="img"` and `title` provide accessibility enhancements.
     */
    const labelText = `<span class="label-text">${labelDisplayName}</span></label>`;
    const svg = `
        <svg viewBox="0 -960 960 960" class="sqd-help-icon" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
            <path d="M419-334q1-87 20.5-129t65.5-76q39-31 57.5-61.109T581-666q0-39-25.5-64.5T486-756q-46 0-75 26t-43 67l-120-52q27-74 87-120.5T485.756-882q109.228 0 168.236 62.148Q713-757.703 713-669q0 60-21 105.5T625-478q-46 40-57 65.5T557-334H419Zm66.788 282Q447-52 420-79t-27-65.496q0-38.495 26.92-65.5Q446.841-237 485.92-237 525-237 552-209.996q27 27.005 27 65.5Q579-106 551.788-79q-27.213 27-66 27Z"></path>
        </svg>`;

    labelElement.insertAdjacentHTML('beforeend', labelText);
    iconElement.insertAdjacentHTML('beforeend', svg);

    // Add event listener to the icon element to toggle the hint text
    iconElement.addEventListener('click', () => switchHint(hintContainer, hintText));

    // Append the icon, label, and hint elements to the field container
    labelElement.appendChild(iconElement);
    labelContainer.appendChild(labelElement);
    titleContainer.appendChild(labelContainer);
    titleContainer.appendChild(hintContainer);
    titleContainer.appendChild(controllerContainer);

    // Return the fully constructed `titleContainer` div
    return titleContainer;
};

/**
 * Creates a new unlabeled field container with a controller for the G4 Automation Sequence.
 *
 * This function generates a structured HTML `div` element that serves as a container for
 * automation fields. It includes a controller sub-container, which is designated by the
 * `data-g4-role="controller"` attribute. The main container is marked with `data-g4-role="field"`.
 *
 * @param {string} id   - A unique identifier used to assign IDs to the field and controller containers.
 * @param {string} role - The role of the container
 * 
 * @returns {HTMLDivElement} - The constructed field container element containing the controller.
 */
const newUnlabeledFieldContainer = (id, role) => {
    // Create the main field container div element.
    const fieldContainer = document.createElement('div');

    // Create the controller sub-container div element.
    const controllerContainer = document.createElement('div');

    // Set the custom attribute 'data-g4-role' to 'field' for the main container.
    fieldContainer.setAttribute('data-g4-role', 'field');

    // Assign a unique ID to the main field container using the provided 'id'.
    fieldContainer.id = `${id}-field`;

    // Set the custom attribute 'data-g4-role' to 'controller' for the controller container.
    controllerContainer.setAttribute('data-g4-role', role || 'controller');

    // Assign a unique ID to the controller container using the provided 'id'.
    controllerContainer.id = `${id}-${role || 'controller'}`;

    // Append the controller container as a child of the main field container.
    fieldContainer.appendChild(controllerContainer);

    // Return the fully constructed field container element.
    return fieldContainer;
};

/**
 * Creates a new multiple fields container with a collapsible section for the G4 Automation Sequence.
 *
 * This function generates a structured HTML `details` element that includes a `summary` for the label
 * and a content area containing an unlabeled field container. The resulting structure allows users
 * to expand or collapse the section to view or hide multiple input fields.
 *
 * @param {string} id      - A unique identifier used to assign IDs to the field container and its controller.
 * @param {Object} options - Configuration options for the container.
 * @param {string} options.labelDisplayName - The text to display in the summary section, serving as the label for the container.
 * @param {string} options.hintText         - The tooltip text that appears when hovering over the summary label.
 * @param {string} options.role             - The role of the container.
 * 
 * @returns {HTMLDetailsElement} - The constructed `details` container element containing the summary and field container.
 */
const newMultipleFieldsContainer = (id, options) => {
    // Create the main <details> container element.
    const detailsContainer = document.createElement('details');

    // Check if the container should be open by default and set the 'open' attribute if necessary.
    if (options.isOpen) {
        detailsContainer.setAttribute("open", null)
    }

    // Create the <summary> element that serves as the clickable label.
    const summaryContainer = document.createElement('summary');

    // Create an unlabeled field container using the provided 'id' and 'role'.
    const fieldContainer = newUnlabeledFieldContainer(id, options.role);

    // Set the display text of the summary label.
    summaryContainer.textContent = options.labelDisplayName;

    // Set the tooltip text for the summary label.
    summaryContainer.title = options.hintText;

    // Append the summary and content containers to the main <details> container.
    detailsContainer.appendChild(summaryContainer);
    detailsContainer.appendChild(fieldContainer);

    // Return the fully constructed <details> container.
    return detailsContainer;
};

/**
 * Creates and appends a new Object Array Fields container to the specified parent container.
 *
 * This container includes functionality to add new array items dynamically and initializes
 * existing array items based on provided data objects. Each array item can be removed,
 * and changes are communicated via the provided callback function.
 *
 * @param {string}        id                        - The unique identifier for the container.
 * @param {Object}        options                   - Configuration options for the container.
 * @param {string}        options.labelDisplayName  - The display name for the container label.
 * @param {string}        options.title             - The title attribute for the container, typically used for tooltips.
 * @param {string}        options.role              - The role attribute for the container, used for identifying elements.
 * @param {string}        options.addButtonLabel    - The label for the "Add" button.
 * @param {string}        options.removeButtonLabel - The label for the "Remove" button.
 * @param {Array<Object>} options.dataObjects       - An array of data objects to initialize the container with.
 * @param {string}        [options.groupName]       - The group name used for property normalization.
 * @param {Function}      setCallback - A callback function invoked whenever the container is modified.
 * 
 * @returns {HTMLElement} - The DOM element representing the Object Array Fields container.
 */
const newObjectArrayFieldsContainer = (id, options, setCallback) => {
    /**
     * Creates a new array object container with dynamic properties and a remove button.
     *
     * @param {Object}   dataObject  - The data object containing properties for the array item.
     * @param {number}   index       - The index of the array item.
     * @param {string}   mode        - The mode of the form, e.g., 'NEW' or 'EDIT'.
     * @param {Function} setCallback - A callback function invoked when the array item is modified or removed.
     * 
     * @returns {HTMLElement} - The DOM element representing the array item container.
     */
    const newArrayObject = (dataObject, index, mode, setCallback) => {
        // Generate a unique key based on the index.
        const indexKey = `${index}`;

        // Create a container for the array item with a unique identifier and label.
        const arrayContainer = newMultipleFieldsContainer(`${id}-${index}`, {
            labelDisplayName: `${options.itemLabel} ${index}`,
            role: "array-item-container"
        });

        // Select the sub-container designated for array item properties.
        const itemContainer = arrayContainer.querySelector(`[data-g4-role="array-item-container"]`);

        // Select the summary element to position the remove button.
        const summaryContainer = arrayContainer.querySelector('summary');

        // Retrieve all property keys from the data object.
        const keys = Object.keys(dataObject);

        // Create and configure the remove button.
        const buttonController = document.createElement('button');
        buttonController.type = 'button';
        buttonController.textContent = `${options.removeButtonLabel}`;
        buttonController.style = "margin-top: 1em;";

        // Add an event listener to handle the removal of the array item.
        buttonController.addEventListener('click', () => {
            controllerContainer.removeChild(arrayContainer);
            setCallback({
                [indexKey]: null
            });
        });

        // Create a container for the remove button and append the button to it.
        const buttonContainer = document.createElement('div');
        buttonContainer.classList.add('sqd-button--with-text');
        buttonContainer.appendChild(buttonController);

        // Insert the remove button container after the summary element.
        summaryContainer.after(buttonContainer);

        // Iterate over each key in the data object to create corresponding property fields.
        for (const key of keys) {
            const property = dataObject[key];
            const propertyOptions = {
                groupName: options?.groupName,
                fieldName: key,
                property: property,
                mode: mode
            };
            newArrayProperty(itemContainer, propertyOptions, index, setCallback);
        }

        // Return the fully constructed array item container.
        return arrayContainer;
    };

    /**
     * Creates and appends a new property field within an array item container based on the property type.
     *
     * @param {HTMLElement} container   - The container element where the property field will be appended.
     * @param {Object}      options     - Configuration options for the property field.
     * @param {number}      index       - The index of the array item.
     * @param {Function}    setCallback - A callback function invoked when the property value changes.
     */
    const newArrayProperty = (container, options, index, setCallback) => {
        /**
         * Handles the callback for property value changes, updating the corresponding data structure.
         *
         * @param {*} value - The new value of the property.
         * @param {Function} setCallback - The callback function to update the state.
         */
        const propertyCallback = (value, setCallback) => {
            const data = {};
            const normalizedGroupName = Utilities.convertToCamelCase(options.groupName);
            const indexKey = `${index}`;

            // Initialize the group object if it doesn't exist.
            data[normalizedGroupName] = data[normalizedGroupName] || {};

            // Update the specific field within the group.
            data[normalizedGroupName][options.fieldName] = value;

            // Invoke the callback with the updated data.
            setCallback({
                [indexKey]: data
            });
        };

        // Determine the type of the property and set default values if necessary.
        const type = options.property.type?.toLocaleUpperCase() || 'STRING';
        const label = options.property?.label || 'NameNotAvailable';
        const title = options.property?.title || 'Help text not available';
        const mode = options?.mode || 'NEW';

        // Create the appropriate input field based on the property type.
        switch (type) {
            case 'STRING':
                CustomFields.newStringField(
                    {
                        container: container,
                        initialValue: mode === 'NEW' ? null : options.property.value,
                        isReadonly: false,
                        label: label,
                        title: title
                    },
                    (value) => propertyCallback(value, setCallback)
                );
                break;
            case 'TEXT':
                CustomFields.newTextField(
                    {
                        container: container,
                        initialValue: mode === 'NEW' ? null : options.property.value,
                        isReadonly: false,
                        label: label,
                        title: title
                    },
                    (value) => propertyCallback(value, setCallback)
                );
                break;
            case 'NUMBER':
                CustomFields.newNumberField(
                    {
                        container: container,
                        initialValue: mode === 'NEW' ? null : options.property.value,
                        isReadonly: false,
                        label: label,
                        step: 1,
                        title: title
                    },
                    (value) => propertyCallback(value, setCallback)
                );
                break;
            case 'KEYVALUE':
                CustomFields.newKeyValueField(
                    {
                        container: container,
                        label: label,
                        title: title,
                        initialValue: mode === 'NEW' ? null : options.property.value
                    },
                    (value) => propertyCallback(value, setCallback)
                );
                break;
            case 'ARRAY':
                CustomFields.newArrayField(
                    {
                        container: container,
                        label: label,
                        title: title,
                        initialValue: mode === 'NEW' ? null : options.property.value
                    },
                    (value) => propertyCallback(value, setCallback)
                );
                break;
            case 'DATALIST':
                CustomFields.newDataListField(
                    {
                        container: container,
                        label: label,
                        title: title,
                        initialValue: mode === 'NEW' ? null : options.property.value,
                        itemSource: options.property?.itemSource || []
                    },
                    (value) => propertyCallback(value, setCallback)
                );
                break;
            default:
                console.warn(`Unsupported property type: ${type}`);
                break;
        }
    };

    // Escape the unique identifier to ensure it's safe for use in CSS selectors.
    const escapedId = CSS.escape(id);

    // Set
    const role = options.role || 'container';

    // Create a container with multiple fields using the provided ID, label, title, and role.
    const fieldContainer = newMultipleFieldsContainer(id, {
        labelDisplayName: options.labelDisplayName,
        role: role,
        hintText: options.hintText
    });

    // Select the controller sub-container within the field container using the escaped unique ID.
    const controllerContainer = fieldContainer.querySelector(`#${escapedId}-${role}`);

    // Select the summary element to position the add button appropriately.
    const summaryContainer = fieldContainer.querySelector('summary');

    // Create and configure the "Add" button.
    const buttonController = document.createElement('button');
    buttonController.type = 'button';
    buttonController.textContent = `${options.addButtonLabel}`;
    buttonController.style = "margin-top: 1em;";

    // Add an event listener to handle the addition of new array items.
    buttonController.addEventListener('click', () => {
        // Retrieve the schema for a new array item from the first data object.
        const schema = options?.dataObjects[0];

        // Determine the index for the new array item based on the current number of children.
        const index = controllerContainer.children.length;

        // Create a new array object container in 'NEW' mode.
        const arrayContainer = newArrayObject(schema, index, 'NEW', setCallback);

        // Append the newly created array container to the controller container.
        controllerContainer.appendChild(arrayContainer);
    });

    // Create a container for the "Add" button and append the button to it.
    const buttonContainer = document.createElement('div');
    buttonContainer.classList.add('sqd-button--with-text');
    buttonContainer.appendChild(buttonController);

    // Insert the "Add" button container after the summary element.
    summaryContainer.after(buttonContainer);

    // Initialize existing array items based on the provided data objects.
    for (let index = 0; index < options.dataObjects.length; index++) {
        const dataObject = options.dataObjects[index];

        // Create a new array object container in 'ADD' mode for each existing data object.
        const arrayContainer = newArrayObject(dataObject, index, 'ADD', setCallback);

        // Append the array container to the controller container.
        controllerContainer.appendChild(arrayContainer);
    }

    // Return the fully constructed Object Array Fields container for further manipulation if needed.
    return fieldContainer;
}

class CustomG4Fields {
    /**
     * Creates a new authentication field that provides input fields for a G4™ username and password.
     *
     * This method does the following:
     * 1. Generates a unique identifier for the authentication fields.
     * 2. Creates a container with multiple fields (username and password).
     * 3. Inserts string fields for "Username" and "Password," each with an optional initial value.
     * 4. Invokes a callback function whenever the username or password changes.
     *
     * @param {Object}       options                           - Configuration options for the authentication field.
     * @param {HTMLElement} [options.container]                - The DOM element to which this field will be appended.
     * @param {string}       options.label                     - A label identifier for this field container.
     * @param {string}      [options.title]                    - An optional title (tooltip) for the field container.
     * @param {Object}      [options.initialValue]             - An object containing initial settings (e.g., username, password).
     * @param {string}      [options.initialValue.username=''] - Initial username value.
     * @param {string}      [options.initialValue.password=''] - Initial password value.
     * @param {Function}     setCallback                       - A callback function invoked whenever one of the inputs changes.
     *
     * @returns {HTMLElement} The container element that includes the newly created authentication field.
     */
    static newAuthenticationField(options, setCallback) {
        // Generate a unique identifier for the authentication fields.
        const inputId = Utilities.newUid();

        // Escape the unique identifier to ensure it's safe for use in CSS selectors.
        const id = CSS.escape(inputId);

        // Create a container with multiple fields (e.g., username and password) using the provided ID, label, and title.
        const fieldContainer = newMultipleFieldsContainer(inputId, {
            labelDisplayName: options.label,
            role: 'container',
            hintText: "Authentication credentials used to verify your identity for accessing G4™ services."
        });

        // Select the controller sub-container within the field container using the unique ID.
        const controller = fieldContainer.querySelector(`#${id}-container`);

        // Create a new string input field for the "Username" with an empty initial value or the provided initial username.
        CustomFields.newStringField(
            {
                container: controller,
                initialValue: options.initialValue?.username || '',
                isReadonly: false,
                label: 'Username',
                title: 'A valid G4™ username required for authentication.'
            },
            (value) => {
                // Build an authentication object containing the updated username
                const authentication = {
                    username: value
                };
                // Invoke the main callback function with the updated username
                setCallback(authentication);
            }
        );

        // Create a new string input field for the "Password" with an empty initial value or the provided initial password.
        CustomFields.newStringField(
            {
                container: controller,
                initialValue: options.initialValue?.password || '',
                isReadonly: false,
                label: 'Password',
                title: 'A valid G4™ password required for authentication.'
            },
            (value) => {
                // Build an authentication object containing the updated password
                const authentication = {
                    password: value
                };
                // Invoke the main callback function with the updated password
                setCallback(authentication);
            }
        );

        // Create a new string input field for the "Password" with an empty initial value or the provided initial password.
        CustomFields.newStringField(
            {
                container: controller,
                initialValue: options.initialValue?.token || '',
                isReadonly: false,
                label: 'Token',
                title: 'A valid G4™ license token. This optional token replaces username/password authentication for streamlined access.'
            },
            (value) => {
                // Build an authentication object containing the updated password
                const authentication = {
                    token: value
                };
                // Invoke the main callback function with the updated password
                setCallback(authentication);
            }
        );

        // Append the controller container to the main field container.
        fieldContainer.appendChild(controller);

        // Append the fully constructed field container to the provided parent container in the DOM.
        if (options.container) {
            options.container.appendChild(fieldContainer);
        }

        // Return the field container for further manipulation if needed.
        return options.container ? options.container : fieldContainer;
    }

    /**
     * Creates and configures a new Automation Settings field containing multiple inputs,
     * such as timeouts, parallel execution limits, and response format toggles.
     *
     * @param {Object}       options                  - Configuration options for the Automation Settings field.
     * @param {HTMLElement} [options.container]       - The DOM element to which this field will be appended.
     * @param {string}       options.label            - A label identifier for this field container.
     * @param {string}      [options.title]           - An optional title (tooltip) for the field container.
     * @param {Object}      [options.initialValue={}] - An object containing initial settings values.
     * @param {number}      [options.initialValue.loadTimeout=60000]              - The max time in ms to wait for page loads.
     * @param {number}      [options.initialValue.maxParallel=1]                  - The number of parallel rows to execute.
     * @param {boolean}     [options.initialValue.returnFlatResponse=false]       - If `true`, includes a flattened format in `responseData`.
     * @param {boolean}     [options.initialValue.returnStructuredResponse=false] - If `true`, includes a structured format in `responseTree`.
     * @param {number}      [options.initialValue.searchTimeout=15000]            - The max time in ms to wait for element searches.
     * @param {Function}     setCallback - Callback function invoked whenever field data changes.
     *
     * @returns {HTMLElement} The container element that includes the newly created Automation Settings field.
     */
    static newAutomationSettingsField(options, setCallback) {
        // Generate a unique identifier for the automation settings fields.
        const inputId = Utilities.newUid();

        // Escape the unique identifier to ensure it's safe for use in CSS selectors.
        const id = CSS.escape(inputId);

        // Create a container with multiple fields using the provided ID, label, and title.
        const fieldContainer = newMultipleFieldsContainer(inputId, {
            labelDisplayName: options.label,
            role: 'container',
            hintText: "Configure automation settings for test execution, including timeouts, parallel execution limits, and response formatting options."
        });

        // Select the controller sub-container within the field container using the unique ID.
        const controller = fieldContainer.querySelector(`#${id}-container`);

        // Create a new number input field for the "LoadTimeout".
        // - Default value is 60000 ms if not provided in initialValue.
        // - step is set to 1 to allow integer increments.
        CustomFields.newNumberField(
            {
                container: controller,
                initialValue: options.initialValue?.loadTimeout || 60000,
                isReadonly: false,
                label: 'Load Timeout',
                step: 1,
                title: 'The maximum time (in milliseconds) the driver should wait for a page to load when setting the `IWebDriver.Url` property.',
            },
            (value) => {
                const automationSettings = {
                    loadTimeout: Utilities.convertToInt(value)
                };
                setCallback(automationSettings);
            }
        );

        // Create a new number input field for the "MaxParallel".
        // - Default value is 1 if not provided in initialValue.
        // - Used to limit the number of parallel rows to execute.
        CustomFields.newNumberField(
            {
                container: controller,
                initialValue: options.initialValue?.maxParallel || 1,
                isReadonly: false,
                label: "Max Parallel",
                step: 1,
                title: "The number of parallel rows to execute actions based on `G4DataProvider`."
            },
            (value) => {
                const automationSettings = {
                    maxParallel: Utilities.convertToInt(value)
                };
                setCallback(automationSettings);
            }
        );

        // Create a new switch field for the "ReturnFlatResponse".
        // - Indicates whether a flattened format should be included in `responseData`.
        CustomFields.newSwitchField(
            {
                container: controller,
                initialValue: options.initialValue?.returnFlatResponse || false,
                label: "Return Flat Response",
                title: "Indicates whether to includes a flattened format within the `responseData` field of the response."
            },
            (value) => {
                const automationSettings = {
                    returnFlatResponse: Utilities.convertStringToBool(value)
                };
                setCallback(automationSettings);
            }
        );

        // Create a new switch field for the "ReturnStructuredResponse".
        // - Indicates whether a structured format should be included in `responseTree`.
        CustomFields.newSwitchField(
            {
                container: controller,
                initialValue: options.initialValue?.returnStructuredResponse || false,
                label: "Return Structured Response",
                title: "Indicates whether to includes a structured format within the `responseTree` field of the response."
            },
            (value) => {
                const automationSettings = {
                    returnStructuredResponse: Utilities.convertStringToBool(value)
                };
                setCallback(automationSettings);
            }
        );

        // Create a new number input field for the "SearchTimeout".
        // - Default value is 15000 ms if not provided in initialValue.
        // - step is set to 1 to allow integer increments.
        CustomFields.newNumberField(
            {
                container: controller,
                initialValue: options.initialValue?.searchTimeout || 15000,
                isReadonly: false,
                label: "Search Timeout",
                title: "The maximum time (in milliseconds) to wait for an element to be found during searches.",
                step: 1
            },
            (value) => {
                const automationSettings = {
                    searchTimeout: Utilities.convertToInt(value)
                };
                setCallback(automationSettings);
            }
        );

        // Append the fully constructed field container to the provided parent container in the DOM.
        if (options.container) {
            options.container.appendChild(fieldContainer);
        }

        // Return the field container for further manipulation if needed.
        return options.container ? options.container : fieldContainer;
    }

    /**
     * Creates a new control bar button with specified properties and appends it to the container.
     *
     * This static method generates a control bar button with an SVG icon, sets up necessary
     * attributes and event listeners, and appends it to the designated container in the DOM.
     *
     * @param {Object}      options               - Configuration options for the control bar button.
     * @param {string[]}    options.classList     - Array of CSS classes to add to the button container.
     * @param {HTMLElement} options.container     - The parent container to which the button will be appended.
     * @param {string}      options.icon          - SVG path data for the button icon.
     * @param {string[]}    options.pathClassList - Array of CSS classes to add to the SVG path element.
     * @param {string[]}    options.svgClassList  - Array of CSS classes to add to the SVG element.
     * @param {string}      options.title         - The tooltip text for the button.
     * @param {string}      options.viewBox       - The viewBox attribute for the SVG element.
     * @param {Function}    options.onClick       - The click event handler function for the button.
     * @param {Object}      [options.svgAttributes]- Optional additional attributes for the SVG element.
     * @param {Object}      [options.pathAttributes]- Optional additional attributes for the SVG path element.
     *
     * @returns {HTMLElement} The parent container with the newly added button appended.
     */
    static newControlBarButton(options) {
        // Generate a unique identifier for the automation settings fields.
        const inputId = Utilities.newUid();

        // Escape the unique identifier to ensure it's safe for use in CSS selectors.
        const id = CSS.escape(inputId);

        // Ensure class lists are initialized to avoid undefined errors.
        options.classList = options.classList || [];
        options.pathClassList = options.pathClassList || [];
        options.svgClassList = options.svgClassList || [];

        // Create a container div for the button.
        const buttonContainer = document.createElement("div");
        buttonContainer.id = inputId;
        buttonContainer.classList.add(...options.classList);
        buttonContainer.title = options.title;

        // Create the SVG element for the button icon.
        const svgElement = document.createElement("svg");
        svgElement.classList.add(...options.svgClassList);
        svgElement.setAttribute('viewBox', options.viewBox);

        // Apply any additional attributes to the SVG element if provided.
        options.svgAttributes = options.svgAttributes || {};
        for (const attribute of Object.keys(options.svgAttributes)) {
            svgElement.setAttribute(attribute, options.svgAttributes[attribute]);
        }

        // Create the path element within the SVG.
        const pathElement = document.createElement("path");
        pathElement.classList.add(...options.pathClassList);
        pathElement.setAttribute('d', options.icon);

        // Apply any additional attributes to the path element if provided.
        options.pathAttributes = options.pathAttributes || {};
        for (const attribute of Object.keys(options.pathAttributes)) {
            pathElement.setAttribute(attribute, options.pathAttributes[attribute]);
        }

        // Append the path element to the SVG.
        svgElement.appendChild(pathElement);

        // Optional: Add an onclick handler to the SVG for debugging purposes.
        // This can be removed or modified as needed.
        svgElement.onclick = () => {
            console.log("clicked");
        };

        // Append the SVG to the button container.
        buttonContainer.appendChild(svgElement);

        // Append the button container to the specified parent container in the DOM.
        if (options.container) {
            // Insert the button at the beginning of the parent container.
            options.container.insertAdjacentHTML('afterbegin', buttonContainer.outerHTML);
        }

        /**
         * Wait for the newly created button element to be present in the DOM,
         * then attach the specified click event handler.
         */
        Utilities.waitForElement(`#${id}`, 5000)
            .then((element) => {
                // Attach the provided onClick handler to the button element.
                element.addEventListener('click', options.onClick);
            })
            .catch((error) => {
                // Log any errors that occur while waiting for the element.
                console.error(`Error waiting for element: ${error.message}`);
            });

        // Return the parent container if provided, otherwise return the button container.
        return options.container ? options.container : buttonContainer;
    }

    /**
     * Creates a new Data Source field container with multiple subfields for filtering,
     * repository selection, source input, data source type, and capabilities.
     * 
     * This field is designed to allow users to configure how data should be retrieved
     * and filtered in automation or data-driven scenarios.
     *
     * @param {Object}       options            - Configuration options for the Data Source field.
     * @param {HTMLElement} [options.container] - The DOM element to which this field will be appended.
     * @param {string}       options.label      - A label identifier for this field container.
     * @param {string}      [options.title]     - An optional title (tooltip) for the field container.
     * @param {Object}      [options.initialValue={}]              - An object containing initial field values.
     * @param {string}      [options.initialValue.filter='']       - The row filter criteria (DataView RowFilter syntax).
     * @param {string}      [options.initialValue.repository='']   - The data container (e.g. DataTable, DataView, List, etc.).
     * @param {string}      [options.initialValue.source='']       - The data source path or connection string.
     * @param {string}      [options.initialValue.type='']         - The data source type (e.g., CSV, XML, JSON).
     * @param {Object}      [options.initialValue.capabilities={}] - Key-value capabilities related to the data source.
     * @param {Function}     setCallback - A callback function invoked whenever one of the subfield values changes.
     *
     * @returns {HTMLElement} The container element that includes the newly created Data Source field.
     */
    static newDataSourceField(options, setCallback) {
        // Generate a unique identifier for the Data Source field.
        const inputId = Utilities.newUid();

        // Escape the unique identifier to ensure it's safe for use in CSS selectors.
        const id = CSS.escape(inputId);

        // Create a main container for the Data Source field using a helper function.
        const fieldContainer = newMultipleFieldsContainer(inputId, {
            labelDisplayName: options.label,
            role: 'container',
            hintText: "Define data input parameters for data-driven scenarios, including filter criteria, repository, source details, data type, and additional capabilities."
        });

        // Select the controller sub-container within the field container using the unique ID.
        const controller = fieldContainer.querySelector(`#${id}-container`);

        /**
         * Create a string input field for the "Filter" property.
         * - Defaults to an empty string if not provided in initialValue.
         * - Displays syntax help for DataView RowFilter usage.
         */
        CustomFields.newStringField(
            {
                container: controller,
                initialValue: options.initialValue?.filter || '',
                isReadonly: false,
                label: 'Filter',
                title: 'Specifies the row-filtering criteria to select data. For examples on how to filter rows, see ' +
                    '<a href="https://www.csharp-examples.net/dataview-rowfilter" target="_blank">DataView RowFilter Syntax [C#]</a>.'
            },
            (value) => {
                // Build an object containing the updated filter value
                const dataSource = {
                    filter: value
                };
                // Invoke the main callback function with the updated data source
                setCallback(dataSource);
            }
        );

        /**
         * Create a string input field for the "Repository" property.
         * - Defaults to an empty string if not provided in initialValue.
         * - Represents the data container (DataTable, DataView, List, etc.).
         */
        CustomFields.newStringField(
            {
                container: controller,
                initialValue: options.initialValue?.repository || '',
                isReadonly: false,
                label: 'Repository',
                title: 'Specifies the data container (e.g., DataTable, DataView, or List) used as the data source.'
            },
            (value) => {
                // Build an object containing the updated repository value
                const dataSource = {
                    repository: value
                };

                // Invoke the main callback function with the updated data source
                setCallback(dataSource);
            }
        );

        /**
         * Create a string input field for the "source" property.
         * - Defaults to an empty string if not provided in initialValue.
         * - May represent a connection string, file path, or URL to locate the repository.
         */
        CustomFields.newStringField(
            {
                container: controller,
                initialValue: options.initialValue?.source || '',
                isReadonly: false,
                label: 'Source',
                title: 'Specifies the connection string, file path, or URL needed to locate the repository.'
            },
            (value) => {
                // Build an object containing the updated source value
                const dataSource = {
                    source: value
                };

                // Invoke the main callback function with the updated data source
                setCallback(dataSource);
            }
        );

        /**
         * Create a data list field for selecting the "Type" of data source.
         * - Defaults to an empty string if not provided in initialValue.
         * - itemSource array is provided with an example for JSON.
         */
        CustomFields.newDataListField(
            {
                container: controller,
                initialValue: options.initialValue?.type || '',
                itemSource: [
                    {
                        name: 'JSON',
                        description: [
                            'Data rows provided is JSON array, with each row represented as JSON object.'
                        ]
                    }
                ],
                label: 'Type',
                title: 'Specifies the format or type of the data source (e.g., CSV, XML, or JSON).'
            },
            (value) => {
                // Build an object containing the updated data source type
                const dataSource = {
                    type: value
                };

                // Invoke the main callback function with the updated data source
                setCallback(dataSource);
            }
        );

        /**
         * Create a key-value field for "Capabilities".
         * - Defaults to an empty object if not provided in initialValue.
         * - Allows specifying additional capabilities or properties for the data source.
         */
        CustomFields.newKeyValueField(
            {
                container: controller,
                initialValue: options.initialValue?.capabilities || {},
                label: 'Capabilities',
                title: 'Specifies additional capabilities or properties for the data source in a key-value format (e.g., enabling caching, custom parameters, etc.).'
            },
            (value) => {
                // Build an object containing the updated capabilities
                const dataSource = {
                    capabilities: value
                };

                // Invoke the main callback function with the updated data source
                setCallback(dataSource);
            }
        );

        // If an external container is provided, append the field container to it
        if (options.container) {
            options.container.appendChild(fieldContainer);
        }

        // Return the parent container holding the Data Source field
        return options.container ? options.container : fieldContainer;
    }

    /**
     * Creates and configures a new Driver Parameters field with various capability groups.
     *
     * This static method generates a comprehensive UI component for managing driver parameters,
     * including "Always Match", "First Match", and "Vendor Capabilities" groups. It leverages
     * helper functions to create and append fields, handling user interactions and updating
     * the driver parameters accordingly.
     *
     * @param {Object}   options     - Configuration options for the Driver Parameters field.
     * @param {Function} setCallback - A callback function to handle updates to the driver parameters.
     * @returns {HTMLElement} The container element holding the Driver Parameters field.
     */
    static newDriverParametersField(options, setCallback) {
        /**
         * Adds a "First Match" capabilities group to the specified container.
         *
         * This function creates and appends a UI component for managing "First Match" capabilities
         * within the provided container. It constructs data objects based on existing `firstMatch`
         * configurations and initializes an object array fields container to allow users to add,
         * remove, and modify capability groups.
         */
        const newFirstMatchCapabilities = (id, firstMatch) => {
            /**
             * Generates a data object for a "Capabilities" group.
             */
            const newDataObject = (firstMatchCapabilities) => {
                // Create a new data object with the "Capabilities" group properties.
                const dataObject = {};
                const value = firstMatchCapabilities === undefined || firstMatchCapabilities === null
                    ? "{}"
                    : Utilities.convertToJson(firstMatchCapabilities);

                // Define the "capabilities" field with its properties.
                dataObject['capabilities'] = {
                    label: 'Capabilities',
                    title: 'A collection of capabilities with additional custom information for the invocation.',
                    type: 'STRING',
                    value: value
                };

                // Return the fully constructed data object for the "Capabilities" group.
                return dataObject;
            };

            // Initialize an array to hold all data objects for existing "First Match" groups.
            const dataObjects = [];

            // Iterate over each group key to create corresponding data objects.
            for (const item of Object.values(firstMatch)) {
                const schema = newDataObject(item);
                dataObjects.push(schema);
            }

            // If there are no existing groups, initialize with a default empty "Capabilities" group.
            if (dataObjects.length === 0) {
                dataObjects.push(newDataObject(undefined));
            }

            // Configuration options for the object array fields container.
            const arrayFieldOptions = {
                addButtonLabel: 'Add Capabilities Group',
                dataObjects: dataObjects,
                groupName: 'FirstMatch',
                itemLabel: 'Group',
                labelDisplayName: "First Match Capabilities",
                removeButtonLabel: 'Remove',
                role: 'container',
                title: "First Match"
            };

            /* Callback function to handle updates to the "First Match" capabilities.
             * This function processes the updated values from the UI, restructures them into the
             * appropriate format, and invokes the provided `setCallback` to apply the changes.*/
            const objectArrayCallback = (value) => {
                // Initialize an object to hold the updated "First Match" capabilities.
                const firstMatchCapabilities = {};

                // Retrieve all keys from the updated value object.
                const keys = Object.keys(value);

                // Iterate over each key to extract and structure the capabilities data.
                for (const key of keys) {
                    const capabilities = value[key]?.firstMatch?.capabilities;

                    // Skip any entries without capabilities data.
                    if (!capabilities) {
                        continue;
                    }

                    firstMatchCapabilities[key] = Utilities.convertFromJson(capabilities);
                }

                // Construct the driver parameters object with the updated "First Match" capabilities.
                const driverParameters = {
                    firstMatch: firstMatchCapabilities
                };

                // Invoke the callback to apply the updated driver parameters.
                setCallback(driverParameters);
            };

            // Create an object array fields container for managing "First Match" capabilities.
            const firstMatchContainer = newObjectArrayFieldsContainer(`${id}-first-match`, arrayFieldOptions, objectArrayCallback);

            // Return the fully constructed "First Match" capabilities container.
            return firstMatchContainer;
        }

        // Generate a unique identifier for the plugins settings fields.
        const inputId = Utilities.newUid();

        // Escape the unique identifier to ensure it's safe for use in CSS selectors.
        const id = CSS.escape(inputId);

        // Create a main container for the Data Source field using a helper function.
        const fieldContainer = newMultipleFieldsContainer(inputId, {
            labelDisplayName: options.label,
            role: 'container',
            hintText: "Configure driver parameters to optimize automation performance by selecting a web driver, specifying driver binaries, and defining advanced capability groups for precise driver behavior.",
            isOpen: options.isOpen
        });

        // Select the controller sub-container within the field container using the unique ID.
        const controller = fieldContainer.querySelector(`#${id}-container`);

        // Create a new Data List Field for selecting the Web Driver.
        CustomFields.newDataListField(
            {
                container: controller,
                initialValue: options.initialValue?.driver || '',
                itemSource: 'Driver',
                label: 'Web Driver',
                title: 'Select the web driver to use.'
            },
            (value) => {
                const driverParameters = {
                    driver: value
                };
                setCallback(driverParameters);
            }
        );

        // Create a new String Field for specifying the Driver Binaries location.
        CustomFields.newStringField(
            {
                container: controller,
                initialValue: options.initialValue?.driverBinaries || '',
                isReadonly: false,
                label: 'Driver Binaries',
                title: 'The driver binaries location on local machine or grid endpoint.'
            },
            (value) => {
                const driverParameters = {
                    driverBinaries: value
                };
                setCallback(driverParameters);
            }
        );

        // Create a container for the "Always Match" capabilities group.
        const alwaysMatchField = newMultipleFieldsContainer(`${inputId}-always-match`, {
            labelDisplayName: 'Always Match Capabilities',
            role: 'always-match-capabilities'
        });

        // Create a new Key-Value Field for "Always Match" capabilities.
        const alwaysMatch = Utilities.convertToJson(options.initialValue?.capabilities?.alwaysMatch);
        CustomFields.newStringField(
            {
                container: alwaysMatchField.querySelector('[data-g4-role="always-match-capabilities"]'),
                label: 'Capabilities',
                title: 'A collection of capabilities with additional custom information for the invocation.',
                initialValue: alwaysMatch
            },
            (value) => {
                const alwaysMatch = Utilities.convertFromJson(value);
                const driverParameters = {
                    capabilities: {
                        alwaysMatch: alwaysMatch
                    }
                };
                setCallback(driverParameters);
            }
        );

        // Append the "Always Match" capabilities container to the main controller.
        controller.appendChild(alwaysMatchField);

        // Create and append the "First Match" capabilities group to the controller.
        const firstMatchField = newFirstMatchCapabilities(
            inputId,
            options.initialValue?.firstMatch || [{}]
        );
        controller.appendChild(firstMatchField);

        // If an external container is provided, append the field container to it
        if (options.container) {
            options.container.appendChild(fieldContainer);
        }

        // Return the parent container holding the Data Source field
        return options.container ? options.container : fieldContainer;
    }

    /**
     * Creates and configures a new Environment Settings field that allows users to:
     * 1. Specify a default environment for automation requests.
     * 2. Toggle whether the environment should be returned in the response.
     * 3. Define key-value pairs for environment variables.
     *
     * @param {Object}       options               - Configuration options for the Environment Settings field.
     * @param {HTMLElement} [options.container]    - The DOM element to which the Environment Settings field will be appended.
     * @param {string}       options.label         - The label identifier for the Environment Settings field.
     * @param {string}      [options.title]        - The title attribute for the field container, often used for tooltips.
     * @param {Object}      [options.initialValue] - The initial settings values for environment configuration.
     * @param {string}      [options.initialValue.defaultEnvironment='SystemParameters'] - Specifies the default environment.
     * @param {boolean}     [options.initialValue.returnEnvironment=false]               - Indicates whether the environment should be returned in the response.
     * @param {Object}      [options.initialValue.environmentVariables={}]               - A collection of static environment variables.
     * @param {Function}     setCallback - A callback function invoked whenever the Environment Settings field data changes.
     *
     * @returns {HTMLElement} The container element that includes the newly created Environment Settings field.
     */
    static newEnvironmentSettingsField(options, setCallback) {
        // Generate a unique identifier for the environment settings fields.
        const inputId = Utilities.newUid();

        // Escape the unique identifier to ensure it's safe for use in CSS selectors.
        const escapedId = CSS.escape(inputId);

        // Create a container with multiple environment settings fields using the provided ID, label, and title.
        const fieldContainer = newMultipleFieldsContainer(inputId, {
            labelDisplayName: options.label,
            role: 'container',
            hintText: "Configure environment settings to specify the default environment for automation, control whether the environment is returned in responses, and define static environment variables for consistent execution."
        });

        // Select the controller sub-container within the field container using the escaped unique ID.
        const controller = fieldContainer.querySelector(`#${escapedId}-container`);

        // Create a new string field for Default Environment.
        CustomFields.newStringField(
            {
                container: controller,
                initialValue: options.initialValue?.defaultEnvironment || 'SystemParameters',
                isReadonly: false,
                label: 'Default Environment',
                title: 'The default environment to use for automation requests.'
            },
            (value) => {
                // Build an object containing the updated default environment value
                const environmentSettings = {
                    defaultEnvironment: value
                };
                // Invoke the main callback function with updated settings
                setCallback(environmentSettings);
            }
        );

        // Create a new switch field for Return Environment.
        CustomFields.newSwitchField(
            {
                container: controller,
                initialValue: options.initialValue?.returnEnvironment || false,
                label: 'Return Environment',
                title: 'Indicates whether the environment should be returned in the response.'
            },
            (value) => {
                // Convert the string value ('true'/'false') to a boolean
                const environmentSettings = {
                    returnEnvironment: Utilities.convertStringToBool(value)
                };
                // Invoke the main callback function with updated settings
                setCallback(environmentSettings);
            }
        );

        // Create a new key-value field for Environment Variables.
        CustomFields.newKeyValueField(
            {
                container: controller,
                initialValue: options.initialValue?.environmentVariables || {},
                label: 'Environment Variables',
                title: 'A list of static environment variables to use for automation requests.'
            },
            (value) => {
                // Build an object containing the updated environment variables
                const environmentSettings = {
                    environmentVariables: value
                };
                // Invoke the main callback function with updated settings
                setCallback(environmentSettings);
            }
        );

        // Append the fully constructed environment settings field container to the provided parent container in the DOM.
        if (options.container) {
            options.container.appendChild(fieldContainer);
        }

        // Return the parent container with the appended environment settings field for further manipulation if needed.
        return options.container ? options.container : fieldContainer;
    }

    /**
     * Creates and configures a new Exceptions Settings field, allowing users to toggle
     * whether exceptions should be returned in the response.
     *
     * @param {Object}       options               - Configuration options for the Exceptions Settings field.
     * @param {HTMLElement} [options.container]    - The DOM element to which the Exceptions Settings field will be appended.
     * @param {string}       options.label         - The label identifier for the Exceptions Settings field.
     * @param {string}      [options.title]        - The title attribute for the field container, often used for tooltips.
     * @param {Object}      [options.initialValue] - The initial settings values for exceptions.
     * @param {boolean}     [options.initialValue.returnExceptions=false] - Indicates whether exceptions should be returned in the response.
     * @param {Function}     setCallback - A callback function invoked whenever the Exceptions Settings field data changes.
     *
     * @returns {HTMLElement} The container element that includes the newly created Exceptions Settings field.
     */
    static newExceptionsSettingsField(options, setCallback) {
        // Generate a unique identifier for the exceptions settings fields
        const inputId = Utilities.newUid();

        // Escape the unique identifier to ensure it's safe for use in CSS selectors
        const escapedId = CSS.escape(inputId);

        /**
         * Create a container for the Exceptions Settings field using a helper function.
         * - Accepts the generated ID, label, title, and a role of 'container' for proper structure.
         */
        const fieldContainer = newMultipleFieldsContainer(inputId, {
            labelDisplayName: options.label,
            role: 'container',
            hintText: "Toggle whether detailed exception information should be included in responses to assist with debugging and error analysis."
        });

        /**
         * Select the controller sub-container within the field container using the escaped unique ID.
         * Additional input fields or switches will be placed here.
         */
        const controller = fieldContainer.querySelector(`#${escapedId}-container`);

        /**
         * Create a new switch field for 'ReturnExceptions'.
         * - `initialValue` defaults to false if not specified in `options.initialValue`.
         * - The callback updates the `exceptionsSettings` object with the boolean value of `returnExceptions`.
         */
        CustomFields.newSwitchField(
            {
                container: controller,
                initialValue: options.initialValue?.returnExceptions || false,
                label: 'Return Exceptions',
                title: 'Indicates whether the exceptions should be returned in the response.'
            },
            (value) => {
                // Convert the string value ('true'/'false') to a boolean and update exceptionsSettings
                const exceptionsSettings = {
                    returnExceptions: Utilities.convertStringToBool(value)
                };
                // Invoke the main callback with the updated settings
                setCallback(exceptionsSettings);
            }
        );

        // If a container element is provided, append the field container to it
        if (options.container) {
            options.container.appendChild(fieldContainer);
        }

        // Return the container holding the new Exceptions Settings field
        return options.container ? options.container : fieldContainer;
    }

    /**
     * Creates and configures a new Queue Manager Settings field, allowing users to specify
     * a queue manager type and additional properties through dynamic input fields.
     *
     * @param {Object}       options                             - Configuration options for the Queue Manager Settings field.
     * @param {HTMLElement} [options.container]                  - The DOM element to which the Queue Manager Settings field will be appended.
     * @param {string}       options.label                       - The label identifier for the Queue Manager Settings field.
     * @param {string}      [options.title]                      - The title attribute for the field container, often used for tooltips.
     * @param {Object}      [options.initialValue]               - The initial settings values for the queue manager.
     * @param {string}      [options.initialValue.type='']       - Specifies the type of the queue manager.
     * @param {Object}      [options.initialValue.properties={}] - A key-value map of additional properties.
     * @param {Function}     setCallback                         - A callback function invoked whenever the Queue Manager Settings field data changes.
     *
     * @returns {HTMLElement} The container element that includes the newly created Queue Manager Settings field.
     */
    static newQueueManagerSettingsField(options, setCallback) {
        // Generate a unique identifier for the queue manager settings fields.
        const inputId = Utilities.newUid();

        // Escape the unique identifier to ensure it's safe for use in CSS selectors.
        const escapedId = CSS.escape(inputId);

        /**
         * Create a container with multiple queue manager settings fields, using:
         * - A helper function `newMultipleFieldsContainer` to build the main container.
         * - The generated `inputId`, label, title, and role 'container'.
         */
        const fieldContainer = newMultipleFieldsContainer(inputId, {
            labelDisplayName: options.label,
            role: 'container',
            hintText: "Define the queue manager type and configure its additional properties to control queue management behavior in automation tasks."
        });

        /**
         * Select the controller sub-container within the field container using
         * the escaped unique ID. This is where the input fields will be placed.
         */
        const controller = fieldContainer.querySelector(`#${escapedId}-container`);

        /**
         * Create a new string input field for the 'Type' property of the queue manager.
         * - Uses `newStringField` to render a text input.
         * - The initial value defaults to an empty string if not provided.
         * - The callback function captures changes to the 'Type' and invokes `setCallback`.
         */
        CustomFields.newStringField(
            {
                container: controller,
                initialValue: options.initialValue?.type || '',
                isReadonly: false,
                label: 'Type',
                title: 'Specifies the type of the queue manager.'
            },
            (value) => {
                const queueManagerSettings = {
                    type: value
                };
                setCallback(queueManagerSettings);
            }
        );

        /**
         * Create a new key-value field for 'Properties'.
         * - Uses `newKeyValueField` to render a section where multiple key-value pairs can be defined.
         * - The initial values default to an empty object if not provided.
         * - The callback function captures changes to 'Properties' and invokes `setCallback`.
         */
        CustomFields.newKeyValueField(
            {
                container: controller,
                label: 'Properties',
                title: 'Additional properties for the queue manager.',
                initialValue: options.initialValue?.properties || {}
            },
            options.initialValue?.properties || {},
            (value) => {
                const queueManagerSettings = {
                    properties: value
                };
                setCallback(queueManagerSettings);
            }
        );

        // If a container element is provided, append the fully constructed field container to it.
        if (options.container) {
            options.container.appendChild(fieldContainer);
        }

        // Return the container that now includes the new Queue Manager Settings field.
        return options.container ? options.container : fieldContainer;
    }

    /**
     * Creates and configures a new Performance Points Settings field that allows users
     * to toggle whether performance points should be returned in the response.
     *
     * @param {Object}       options               - Configuration options for the Performance Points Settings field.
     * @param {HTMLElement} [options.container]    - The DOM element to which the Performance Points Settings field will be appended.
     * @param {string}       options.label         - The label identifier for the Performance Points Settings field.
     * @param {string}      [options.title]        - The title attribute for the field container, often used for tooltips.
     * @param {Object}      [options.initialValue] - The initial settings values.
     * @param {boolean}     [options.initialValue.returnPerformancePoints=false] - Indicates whether performance points should be returned in the response.
     * @param {Function}    setCallback - A callback function invoked whenever the Performance Points Settings field data changes.
     *
     * @returns {HTMLElement} The container element that includes the newly created Performance Points Settings field.
     */
    static newPerformancePointsSettingsField(options, setCallback) {
        // Generate a unique identifier for the performance points settings fields.
        const inputId = Utilities.newUid();

        // Escape the unique identifier to ensure it's safe for use in CSS selectors.
        const escapedId = CSS.escape(inputId);

        /**
         * Create a container with multiple performance points settings fields.
         * - Uses a helper function `newMultipleFieldsContainer` to build the main container.
         * - Passes the ID, label, title, and 'container' role for proper structure and identification.
         */
        const fieldContainer = newMultipleFieldsContainer(inputId, {
            labelDisplayName: options.label,
            role: 'container',
            hintText: "Toggle inclusion of performance points in responses to enable performance diagnostics and analysis."
        });

        /**
         * Select the controller sub-container within the field container using the escaped unique ID.
         * This is where additional input fields or switches will be placed.
         */
        const controller = fieldContainer.querySelector(`#${escapedId}-container`);

        /**
         * Create a new switch field for ReturnPerformancePoints.
         * - `initialValue` is derived from options.initialValue, defaulting to false if not specified.
         * - `label` and `title` describe the field purpose.
         * - The callback converts the string value ('true'/'false') to a boolean and invokes `setCallback`.
         */
        CustomFields.newSwitchField(
            {
                container: controller,
                initialValue: options.initialValue?.returnPerformancePoints || false,
                label: 'Return Performance Points',
                title: 'Indicates whether the performance points should be returned in the response.'
            },
            (value) => {
                const performancePointsSettings = {
                    returnPerformancePoints: Utilities.convertStringToBool(value)
                };
                setCallback(performancePointsSettings);
            }
        );

        // If the user has provided a container in options, append the fully constructed field container to it.
        if (options.container) {
            options.container.appendChild(fieldContainer);
        }

        /**
         * Return the parent container with the appended Performance Points Settings field,
         * allowing further manipulation or inspection outside this function if needed.
         */
        return options.container ? options.container : fieldContainer;
    }

    /**
     * Creates and configures a new Plugins Settings field consisting of:
     * 1. An array of external repository definitions, each represented as a data object schema.
     * 2. A switch field to toggle the 'forceRuleReference' property.
     * 
     * This field is built by transforming existing external repository data into
     * standardized schemas, then rendering them in an object array container, followed
     * by a switch toggle for the 'forceRuleReference' setting.
     *
     * @param {Object}       options            - Configuration options for the Plugins Settings field.
     * @param {HTMLElement} [options.container] - The DOM element to which the Plugins Settings field will be appended.
     * @param {string}       options.label      - Identifier for the Plugins Settings field, used for display formatting.
     * @param {string}      [options.title]     - The title attribute for the field container, often used for tooltips.
     * @param {Object}      [options.initialValue={ externalRepositories: [{}] }] - Initial plugin settings data.
     *   - `externalRepositories`: An array of objects representing external repositories, each of which is transformed into a data object schema.
     *   - `forceRuleReference`: A boolean indicating whether rule references should be forced (defaults to `true` if not provided).
     * @param {Function} setCallback            - A callback function invoked whenever the Plugins Settings field data changes.
     *
     * @returns {HTMLElement} The container element that includes the newly created Plugins Settings field.
     */
    static newPluginsSettingsField(options, setCallback) {
        /**
         * Generates a data object schema for an external repository.
         */
        const newDataObject = (externalRepository) => {
            // Initialize an empty object to hold the data object schema.
            const dataObject = {};

            // Define the 'url' field for the external repository.
            dataObject['url'] = {
                label: 'Url',
                title: 'The URL of the external repository.',
                type: 'STRING',
                value: externalRepository?.url || ''
            };

            // Define the 'version' field for the external repository.
            dataObject['version'] = {
                label: 'Version',
                title: 'The API version of the external repository.',
                type: 'NUMBER',
                value: externalRepository?.version || ''
            };

            // Define the 'name' field for the external repository.
            dataObject['name'] = {
                label: 'Name',
                title: 'The name of the external repository.',
                type: 'STRING',
                value: externalRepository?.name || ''
            };

            // Define the 'username' field for authenticating with the external repository.
            dataObject['username'] = {
                label: 'Username',
                title: 'The username to authenticate with the external repository.',
                type: 'STRING',
                value: externalRepository?.username || ''
            };

            // Define the 'password' field for authenticating with the external repository.
            dataObject['password'] = {
                label: 'Password',
                title: 'The password to authenticate with the external repository.',
                type: 'STRING',
                value: externalRepository?.password || ''
            };

            // Define the 'timeout' field for request timeout settings.
            dataObject['timeout'] = {
                label: 'Timeout',
                title: 'The time in seconds to wait before the request times out (default 300 seconds).',
                type: 'NUMBER',
                value: externalRepository?.timeout || '300'
            };

            // Define the 'headers' field for request headers.
            dataObject['headers'] = {
                label: 'Headers',
                title: 'A collection of headers to be included in the request.',
                type: 'KEYVALUE',
                value: externalRepository?.headers || {}
            };

            // Define the 'capabilities' field for additional custom information.
            dataObject['capabilities'] = {
                label: 'Capabilities',
                title: 'A collection of capabilities with additional custom information for the invocation.',
                type: 'KEYVALUE',
                value: externalRepository?.capabilities || {}
            };

            // Return the fully constructed data object schema for the external repository.
            return dataObject;
        };

        // Generate a unique identifier for the plugins settings fields.
        const inputId = Utilities.newUid();

        // Initialize external repositories with existing data or a default empty object.
        const externalRepositories = options.initialValue?.externalRepositories || [{}];

        // Prepare an array to hold data object schemas for each external repository.
        const dataObjects = [];

        // Retrieve all keys (indices) from the externalRepositories object.
        const indexes = Object.keys(externalRepositories);

        // Iterate over each external repository to create its data object schema.
        for (const index of indexes) {
            const schema = newDataObject(externalRepositories[index]);
            dataObjects.push(schema);
        }

        // Add a new data object schema if no external repositories are provided.
        if (dataObjects.length === 0) {
            dataObjects.push(newDataObject(undefined));
        }

        // Configuration options for the object array fields container.
        const arrayFieldOptions = {
            addButtonLabel: 'Add External Repository',
            dataObjects: dataObjects,
            groupName: 'ExternalRepositories',
            hintText: "Configure external repository definitions—including authentication, headers, and capabilities—and toggle rule reference enforcement for consistent plugin behavior.",
            itemLabel: 'External Repository',
            labelDisplayName: options.label,
            removeButtonLabel: 'Remove',
            role: 'container',
            title: options.title
        };

        // Create the object array fields container with the provided options and callback.
        const fieldContainer = newObjectArrayFieldsContainer(inputId, arrayFieldOptions, setCallback);

        // Set the initial forceRuleReference value to true if not provided.
        const initialValue = options.initialValue;
        const isNull = initialValue?.forceRuleReference === null;
        const isUndefined = initialValue?.forceRuleReference === undefined;
        const forceRuleReference = isNull || isUndefined ? true : initialValue?.forceRuleReference;

        // Append new switch field for ForceRuleReference.
        CustomFields.newSwitchField(
            {
                container: fieldContainer,
                initialValue: forceRuleReference,
                label: 'Force Rule Reference',
                title: 'Indicates whether rule references should be forced when reused across different jobs. When set to `true` (the default), a new copy of the rule with a new reference will be created.'
            },
            (value) => {
                const pluginsSettings = {
                    forceRuleReference: Utilities.convertStringToBool(value)
                };
                setCallback(pluginsSettings);
            }
        );

        // Append the fully constructed plugins settings field container to the provided parent container in the DOM.
        if (options.container) {
            options.container.appendChild(fieldContainer);
        }

        // Return the parent container with the appended plugins settings field for further manipulation if needed.
        return options.container ? options.container : fieldContainer;
    }

    /**
     * Creates and configures a new Screenshots Settings field that allows users to:
     * 1. Specify an output folder for saving screenshots.
     * 2. Toggle whether screenshots should be converted to Base64.
     * 3. Toggle whether screenshots should be captured only on exceptions.
     * 4. Toggle whether screenshots should be included in the response.
     *
     * @param {Object}       options               - Configuration options for the Screenshots Settings field.
     * @param {HTMLElement} [options.container]    - The DOM element to which the Screenshots Settings field will be appended.
     * @param {string}       options.label         - The label identifier for this field container.
     * @param {string}      [options.title]        - An optional title (tooltip) for the field container.
     * @param {Object}      [options.initialValue] - An object containing initial settings values.
     * @param {string}      [options.initialValue.outputFolder='.']        - The default folder path for saving screenshots.
     * @param {boolean}     [options.initialValue.convertToBase64=false]   - Indicates whether screenshots should be converted to Base64 strings.
     * @param {boolean}     [options.initialValue.onExceptionOnly=false]   - Indicates whether screenshots should be captured only for exceptions.
     * @param {boolean}     [options.initialValue.returnScreenshots=false] - Indicates whether screenshots should be returned in the response.
     * @param {Function}     setCallback - A callback function invoked whenever a setting in the Screenshots field changes.
     *
     * @returns {HTMLElement} The container element that includes the newly created Screenshots Settings field.
     */
    static newScreenshotsSettingsField(options, setCallback) {
        // Generate a unique identifier for the screenshots settings fields
        const inputId = Utilities.newUid();

        // Escape the unique identifier to ensure it's safe for use in CSS selectors
        const escapedId = CSS.escape(inputId);

        /**
         * Create a container for the Screenshots Settings field using a helper function.
         * - Accepts the generated ID, label, title, and a role of 'container' for proper structure.
         */
        const fieldContainer = newMultipleFieldsContainer(inputId, {
            labelDisplayName: options.label,
            role: 'container',
            hintText: "Configure screenshot output settings, including the save folder, Base64 conversion, exception-only capture, and inclusion in automation responses."
        });

        /**
         * Select the controller sub-container within the field container using the escaped unique ID.
         * Additional input fields or switches will be placed here.
         */
        const controller = fieldContainer.querySelector(`#${escapedId}-container`);

        /**
         * Create a new string field for the "OutputFolder".
         * - Defaults to '.' if not provided in initialValue.
         * - Used to specify the folder path for saving screenshots.
         */
        CustomFields.newStringField(
            {
                container: controller,
                initialValue: options.initialValue?.outputFolder || '.',
                isReadonly: false,
                label: 'Output Folder',
                title: 'Specifies the default folder path for saving screenshots.'
            },
            (value) => {
                // Build an object containing the updated output folder value
                const environmentSettings = {
                    outputFolder: value
                };
                // Invoke the main callback function with updated settings
                setCallback(environmentSettings);
            }
        );

        /**
         * Create a new switch field for "ConvertToBase64".
         * - Indicates whether screenshots should be converted to Base64 strings.
         * - If true, base64 data is included in the response.
         */
        CustomFields.newSwitchField(
            {
                container: controller,
                initialValue: options.initialValue?.convertToBase64 || false,
                label: 'Convert To Base64',
                title: 'Indicates whether screenshots should be converted to Base64 strings. The Base64 data will be included in the response.'
            },
            (value) => {
                const screenshotsSettings = {
                    convertToBase64: Utilities.convertStringToBool(value)
                };
                setCallback(screenshotsSettings);
            }
        );

        /**
         * Create a new switch field for "ExceptionsOnly".
         * - Indicates whether screenshots should be captured only on exceptions.
         */
        CustomFields.newSwitchField(
            {
                container: controller,
                initialValue: options.initialValue?.onExceptionOnly || false,
                label: 'Exceptions Only',
                title: 'Indicates whether screenshots should be captured only on exceptions.'
            },
            (value) => {
                const screenshotsSettings = {
                    onExceptionOnly: Utilities.convertStringToBool(value)
                };
                setCallback(screenshotsSettings);
            }
        );

        /**
         * Create a new switch field for "ReturnScreenshots".
         * - Indicates whether screenshots should be included in the response.
         */
        CustomFields.newSwitchField(
            {
                container: controller,
                initialValue: options.initialValue?.returnScreenshots || false,
                label: 'Return Screenshots',
                title: 'Indicates whether screenshots should be returned in the response.'
            },
            (value) => {
                const screenshotsSettings = {
                    returnScreenshots: Utilities.convertStringToBool(value)
                };
                setCallback(screenshotsSettings);
            }
        );

        // If the user has provided a container in options, append the field container to it
        if (options.container) {
            options.container.appendChild(fieldContainer);
        }

        // Return the container holding the new Screenshots Settings field
        return options.container ? options.container : fieldContainer;
    }
}

class CustomFields {
    // TODO: Add support for text fields with data list suggestions.
    /**
     * Creates a new array field that allows users to dynamically add multiple input values.
     * The first input field is initialized with the first value from `options.initialValue` (if any),
     * and subsequent values are added to a separate container below.
     * Whenever the field content changes, a callback is invoked with the updated list of values.
     *
     * @param {Object}         options                  - Configuration options for the array field.
     * @param {HTMLElement}   [options.container]       - The DOM element to which the array field will be appended.
     * @param {string}         options.label            - The label identifier for the array field, converted from PascalCase to a space-separated format for display.
     * @param {string}        [options.title]           - The title attribute for the field container, often used for tooltips.
     * @param {Array<string>} [options.initialValue=[]] - An array of initial values to populate the field. The first value is used in the main input row, and the rest are added to a separate container.
     * @param {Function}       setCallback              - Callback function to handle changes to the array field's values.
     *
     * @returns {HTMLElement} The container element that includes the newly created array field.
     */
    static newArrayField(options, setCallback) {
        /**
         * Creates and appends a new input row within the container that matches the specified ID.
         *
         * This function locates the container element using the provided `id`, and then invokes
         * the `newInput` function to create a new row containing an input field and a remove button.
         * If the container is not found, the function returns without taking any action.
         */
        const newInputCallback = (id, setCallback) => {
            // Select the container element based on the provided ID, targeting
            // the controller and the corresponding input container.
            const container = document.querySelector(`#${id}-controller > #${id}-input-container`);

            // If the container does not exist, return early without creating a new input row.
            if (!container) {
                return;
            }

            // Create and return a new input row within the located container by invoking `newInput`.
            return newInput({ container: container }, setCallback);
        }

        /**
         * Creates a new row containing an input field and a remove button,
         * then appends it to the specified container. The input field allows
         * users to enter a value associated with `data-g4-role="valueitem"`,
         * and the remove button provides the option to remove the row from
         * the container.
         */
        const newInput = (options, setCallback) => {
            // Create a div element to serve as the row container for the input and remove button
            const row = document.createElement('div');
            row.setAttribute('data-g4-role', 'input-row');

            // Create the text input field
            const input = document.createElement('input');
            input.type = 'text';
            input.value = options.value || '';
            input.setAttribute('data-g4-role', 'valueitem');
            input.setAttribute('title', options.value);

            // Create the remove button
            const removeButton = document.createElement('button');
            removeButton.type = 'button';
            removeButton.textContent = '-';

            // Add a click event listener to the remove button
            removeButton.addEventListener('click', () => {
                // Remove the row from the container
                options.container.removeChild(row);

                // Locate the controller section within the closest field container
                const titleContainer = options.container
                    .closest('[data-g4-role="field"]')
                    .querySelector('[data-g4-role="controller"]');

                // Invoke the callback with the updated container
                callback(titleContainer, setCallback);
            });

            // Append the remove button and the input to the row
            row.appendChild(removeButton);
            row.appendChild(input);

            // Finally, append the row to the specified container
            options.container.appendChild(row);

            // Return the input element in case further actions or manipulations are needed
            return input;
        }

        /**
         * Processes a list of input elements within the specified container
         * and invokes the provided callback function with the extracted values.
         *
         * Steps:
         * 1. Locates all input elements that have the attribute data-g4-role="valueitem".
         * 2. Updates the title attribute of each input to mirror its current value.
         * 3. Collects all non-empty input values into an array.
         * 4. Passes this array to the setCallback function for further handling.
         */
        const callback = (container, setCallback) => {
            // Retrieve all input elements with data-g4-role="valueitem" within the container
            const inputs = container.querySelectorAll('input[data-g4-role="valueitem"]');
            const inputArray = Array.from(inputs);

            // Map over the inputs to update their title attributes and collect their values
            const values = inputArray.map(input => {
                // Update the title attribute to reflect the current value
                input.title = input.value;

                // Return the trimmed value for possible processing
                return input.value;
            }).filter(item => item != null && item.trim() !== ''); // Filter out any null or empty string values

            // Invoke the callback function with the filtered array of values
            setCallback(values);
        }

        // Generate a unique identifier for this array field to maintain distinctness in the DOM
        const inputId = Utilities.newUid();

        // Escape the identifier for safe usage in CSS selectors
        const escapedId = CSS.escape(inputId);

        // Extract the initial values or default to an empty array if not provided
        const values = options.initialValue || [];

        // If there are any initial values, use the first one for the main input; otherwise, use an empty string
        const mainInputValue = values.length > 0 ? values[0] : '';

        // Convert the label from PascalCase to a space-separated format for better readability
        const labelDisplayName = options.label;

        // Create the primary field container using a helper function
        const fieldContainer = newFieldContainer(inputId, labelDisplayName, options.title);

        // Select the sub-container within the field container where elements will be inserted
        const controllerContainer = fieldContainer.querySelector('[data-g4-role="controller"]');

        /**
         * Construct the main HTML structure:
         * - A button to add new input rows.
         * - A text input field initialized with `mainInputValue`.
         * - An empty container (identified by `id="${inputId}-input-container"`) where subsequent inputs will be appended.
         */
        const html = `
        <div data-g4-role="input-row">
            <button type="button">+</button>
            <input type="text" data-g4-role="valueitem" title="${mainInputValue}" value="${mainInputValue}" />
        </div>
        <div id="${inputId}-input-container"></div>`;

        // Insert the constructed HTML block into the controller container
        controllerContainer.insertAdjacentHTML('beforeend', html);

        // Select the "+" button and attach a click event to create new input rows
        const botton = controllerContainer.querySelector('button');
        botton.addEventListener('click', () => newInputCallback(escapedId));

        // Select the container where additional input rows will be placed
        const inputContainer = fieldContainer.querySelector(`#${escapedId}-input-container`);

        // For each remaining value (skipping the first one), create an additional input row in the container
        for (const value of values.slice(1)) {
            newInput({ container: inputContainer, value: value }, setCallback);
        }

        // Whenever an input event occurs within the field container, update the array of values via callback
        fieldContainer.addEventListener('input', () => callback(fieldContainer, setCallback));

        // If an external container is provided, append the entire field container to it
        if (options.container) {
            options.container.appendChild(fieldContainer);
        }

        // Return the container that includes the newly created array field
        return options.container ? options.container : fieldContainer;
    }

    /**
     * Creates a new data list field (input with datalist) that allows users to type and select an option from a predefined list.
     * The list of items is derived from the provided `itemSource`, which can be fetched and processed by `getItems`.
     *
     * @param {Object}       options                  - Configuration options for the data list field.
     * @param {HTMLElement} [options.container]       - The DOM element to which the data list field will be appended.
     * @param {string}       options.label            - The label identifier for the data list field (converted to a space-separated string for display).
     * @param {string}      [options.title]           - The title attribute for the field container, often used for tooltips.
     * @param {string}      [options.itemSource]      - The source from which the items are fetched and processed by `getItems`.
     * @param {string}      [options.initialValue=''] - The initial value shown in the data list input. Defaults to an empty string if not provided or `'undefined'`.
     * @param {Function}     setCallback              - A callback function invoked whenever the input value changes.
     *
     * @returns {HTMLElement} The container element that includes the newly created data list field.
     */
    static newDataListField(options, setCallback) {
        /**
         * Retrieves and processes items from a given source, returning a sorted object of items with their manifest details.
         *
         * Depending on the type of `itemSource`, this function performs one of two processes:
         * 1. **String**: Treats `itemSource` as a key for the `_cache` object, then transforms the retrieved items into a standardized format.
         * 2. **Array**: Assumes `itemSource` is an array of objects with `name` and `description` properties, and transforms them accordingly.
         *
         * The items are finally sorted alphabetically by their keys before being returned.
         */
        const getItems = (itemSource) => {
            // Define a cache object to store items retrieved by key.
            let items;

            // Check if itemSource is a string (used as a key to retrieve items from _cache)
            if (typeof itemSource === 'string') {
                // Retrieve the cached items if the key exists; otherwise default to an empty object
                items = (itemSource in _cache) ? _cache[itemSource] : {};

                /**
                 * Transform each item into a standardized format.
                 * - manifestKey: Extracted from item?.manifest?.key or defaults to 'No key available'.
                 * - summary    : Extracted from item?.manifest?.summary or defaults to ['No summary available'].
                 */
                items = Object.keys(items).reduce((obj, key) => {
                    const item = items[key];
                    const manifestKey = item?.manifest?.key || 'No key available';
                    obj[manifestKey] = { manifest: { summary: item?.manifest?.summary || ['No summary available'] } };
                    return obj;
                }, {});
            }
            // Check if itemSource is an array
            else if (Array.isArray(itemSource)) {
                /**
                 * Transform the array of items into a standardized format.
                 * - key    : Derived from item.name or defaults to 'No name available'.
                 * - summary: Extracted from item.description (or can be undefined if not present).
                 */
                items = itemSource.reduce((obj, item) => {
                    const key = item.name || 'No name available';
                    obj[key] = { manifest: { summary: item.description } };
                    return obj;
                }, {});
            }
            // If itemSource is neither a string nor an array, throw an error
            else {
                throw new Error('Invalid itemSource type. Must be a string or an array.');
            }

            /**
             * Sort the items object by its keys in alphabetical order.
             * The sorted keys are then used to reconstruct the object in sorted order.
             */
            items = Object.keys(items)
                .sort((i, j) => i.localeCompare(j))
                .reduce((obj, key) => {
                    obj[key] = items[key];
                    return obj;
                }, {});

            // Return the sorted items object
            return items;
        };

        // Generate a unique ID for the datalist input field to ensure uniqueness in the DOM
        const inputId = Utilities.newUid();

        // Convert the label from PascalCase to a space-separated format for display
        const labelDisplayName = options.label;

        // Retrieve and process items from the provided item source
        const items = getItems(options.itemSource);

        // Validate and sanitize the initial value; default to an empty string if invalid or 'undefined'
        options.initialValue = !options.initialValue || options.initialValue === 'undefined'
            ? ''
            : options.initialValue;

        /**
         * Construct the HTML for the input and its associated datalist:
         * 1. An <input> element with its 'list' attribute referencing the datalist ID.
         * 2. A <datalist> element containing <option> elements for each item.
         *    - The first option is a disabled, selected placeholder prompting user selection.
         *    - Each subsequent option has a value, label, and display text derived from the item data.
         */
        let html = `
        <input id="${inputId}-input" list="${inputId}-datalist" title="${options.initialValue === '' ? 'Please select an option' : options.initialValue}" />
        <datalist id="${inputId}-datalist">
            <option value="" disabled selected>-- Please select an option --</option>`;

        // Iterate over each item key to create the datalist options
        Object.keys(items).forEach(key => {
            // Retrieve the summary for each item or default to 'No summary available'
            const summary = items[key].manifest?.summary || ['No summary available'];

            // Join the summary array to create a multi-line tooltip
            const hint = summary.join("\n");

            // Create an option element: use 'key' as the value, 'hint' as the label, and display text as a space-separated key
            html += `  <option value="${key}" label="${hint}">${Utilities.convertPascalToSpaceCase(key)}</option>\n`;
        });

        // Close the datalist element
        html += '</datalist>';

        // Create the main field container, specifying the unique ID, display label, and optional title
        const fieldContainer = newFieldContainer(inputId, labelDisplayName, options.title);

        // Select the sub-container within the field container where the input/datalist HTML will be appended
        const controllerContainer = fieldContainer.querySelector('[data-g4-role="controller"]');

        // Insert the constructed HTML for the input and datalist into the controller container
        controllerContainer.insertAdjacentHTML('beforeend', html);

        // Select the newly inserted <input> element for further setup
        const input = controllerContainer.querySelector('input ');
        // Apply the validated initial value to the input
        input.value = options.initialValue;

        // If a valid callback function is provided, attach an event listener to handle input changes
        if (typeof setCallback === 'function') {
            fieldContainer.addEventListener('input', () => {
                // Update the 'title' attribute to reflect the current value
                input.title = input.value;

                // Invoke the callback with the new value
                setCallback(input.value);
            });
        }

        // If an external container is provided, append the entire field container to it
        if (options.container) {
            options.container.appendChild(fieldContainer);
        }

        /**
         * Return the container that now includes the new data list field.
         * - If an external container was specified, return that container.
         * - Otherwise, return the newly created field container.
         */
        return options.container ? options.container : fieldContainer;
    }

    /**
     * Creates and appends a new key-value field to the specified container based on the provided options.
     * This field allows users to dynamically add multiple key-value pairs, with each pair consisting of:
     * - A key input
     * - A value input
     * - A remove button (available after additional pairs are added)
     *
     * The field invokes a callback function whenever its content changes, passing the current set of key-value pairs.
     *
     * @param {Object}       options                  - Configuration options for the key-value field.
     * @param {HTMLElement} [options.container]       - The DOM element to which the key-value field will be appended.
     * @param {string}       options.label            - The label identifier for the key-value field (converted from PascalCase to a space-separated format).
     * @param {string}      [options.title]           - The title attribute for the field container, often used for tooltips.
     * @param {Object}      [options.initialValue={}] - The initial set of key-value pairs. Defaults to an empty object.
     * @param {Function}     setCallback              - Callback function to handle updates to the key-value pairs.
     *
     * @returns {HTMLElement} The container element that includes the newly created key-value field.
     */
    static newKeyValueField(options, setCallback) {
        /**
         * Creates and appends a new key-value input row within the specified container.
         *
         * This function locates the input container using the provided `id` and,
         * if found, invokes the `newInput` function to add a new input row with default values.
         */
        const newInputCallback = (id, setCallback) => {
            // Select the input container within the controller section using the provided id
            const container = document.querySelector(`#${id}-controller > #${id}-input-container`);

            // If the container does not exist, exit the function without performing any action
            if (!container) {
                return;
            }

            // Create and return a new input row within the container by invoking the `newInput` function
            return newInput({ container }, setCallback);
        }

        /**
         * Creates a new key-value input row and appends it to the specified container.
         * Each row consists of a remove button, a key input field, and a value input field.
         * The remove button allows the user to delete the specific key-value pair.
         */
        const newInput = (options, setCallback) => {
            // Create a div element to serve as the row container for the key-value pair
            const row = document.createElement('div');
            const inputId = Utilities.newUid();
            row.setAttribute('data-g4-role', 'keyvalue');

            // Create the key input field
            const newKeyInput = document.createElement('input');
            newKeyInput.type = 'text';
            newKeyInput.value = options.key || '';
            newKeyInput.setAttribute('data-g4-role', 'key');
            newKeyInput.setAttribute('title', `Key: ${options.key || ''}`);
            newKeyInput.setAttribute('placeholder', 'Enter key');
            newKeyInput.setAttribute('name', `${inputId}-key`)

            // Create the value input field
            const newValueInput = document.createElement('input');
            newValueInput.type = 'text';
            newValueInput.value = options.value || '';
            newValueInput.setAttribute('data-g4-role', 'value');
            newValueInput.setAttribute('title', `Value: ${options.value || ''}`);
            newValueInput.setAttribute('placeholder', 'Enter value');
            newKeyInput.setAttribute('name', `${inputId}-value`)

            // Create the remove button to delete the key-value pair
            const removeButton = document.createElement('button');
            removeButton.type = 'button';
            removeButton.textContent = '-';
            removeButton.title = "Remove Key/Value Pair";
            removeButton.className = 'remove-button';

            // Define the click event handler for the remove button
            removeButton.addEventListener('click', () => {
                // Remove the current row from the container
                options.container.removeChild(row);

                // Find the closest parent container with the role "field"
                const fieldContainer = options.container.closest('[data-g4-role="field"]');

                // If a field container is found, locate the controller section
                if (fieldContainer) {
                    // Within the field container, find the controller section
                    const titleContainer = fieldContainer.querySelector('[data-g4-role="controller"]');

                    // Invoke the callback to handle the updated key-value pairs
                    callback(titleContainer, setCallback);
                }
            });

            // Append the remove button, key input, and value input to the row container
            row.appendChild(removeButton);
            row.appendChild(newKeyInput);
            row.appendChild(newValueInput);

            // Append the completed row to the specified container
            options.container.appendChild(row);

            // Return the newly created row element for potential further manipulation
            return row;
        }

        /**
         * Processes the key-value input rows within the specified container and invokes the callback with the current key-value pairs.
         *
         * This function performs the following steps:
         * 1. Selects all key-value input rows within the container.
         * 2. Iterates over each input row to extract and sanitize the key and value.
         * 3. Builds a dictionary of key-value pairs, excluding entries with empty keys.
         * 4. Updates the title attributes of the input fields to reflect their current values.
         * 5. Invokes the provided `setCallback` function with the resulting dictionary.
         */
        const callback = (container, setCallback) => {
            // Select all div elements with data-g4-role="keyvalue" within the container
            const inputs = container.querySelectorAll('div[data-g4-role="keyvalue"]');
            const inputArray = Array.from(inputs);

            // Reduce the array of input rows into a dictionary of key-value pairs
            const values = inputArray.reduce((dictionary, input) => {
                // Select the key input field within the current input row
                const keyInput = input.querySelector('input[data-g4-role="key"]');

                // Select the value input field within the current input row
                const valueInput = input.querySelector('input[data-g4-role="value"]');

                // Retrieve and trim the value from the key input field; default to an empty string if not found
                const key = keyInput ? keyInput.value.trim() : '';

                // Retrieve and trim the value from the value input field; default to an empty string if not found
                const value = valueInput ? valueInput.value.trim() : '';

                // If the key is not empty, add it to the dictionary with its corresponding value
                if (key !== '') {
                    dictionary[key] = value;
                }

                // Update the title attribute of the key input field to reflect the current key
                if (keyInput) keyInput.title = key;

                // Update the title attribute of the value input field to reflect the current value
                if (valueInput) valueInput.title = value;

                // Return the updated dictionary of key-value pairs
                return dictionary;
            }, {});

            // Invoke the provided callback function with the dictionary of key-value pairs
            if (typeof setCallback === 'function') {
                setCallback(values);
            }
        }

        // Generate a unique ID for the field and escape it for safe CSS usage
        const inputId = Utilities.newUid();
        const escapedId = CSS.escape(inputId);

        // Extract the initial key-value pairs from the options or default to an empty object
        const values = options.initialValue || {};
        const keys = Object.keys(values);

        // Determine if there is at least one key to populate the main key-value row
        const mainInputKey = keys.length > 0 ? keys.shift() : '';
        const mainKey = mainInputKey || '';
        const mainValue = mainInputKey ? values[mainInputKey] : '';

        // Convert the provided label from PascalCase to a more readable space-separated format
        const labelDisplayName = Utilities.convertPascalToSpaceCase(options.label);

        // Create the primary field container using a helper function
        const fieldContainer = newFieldContainer(inputId, labelDisplayName, options.title);

        // Select the controller container within the field container where elements will be added
        const controllerContainer = fieldContainer.querySelector('[data-g4-role="controller"]');

        // Prepare the initial HTML structure with:
        // 1. A "+" button to add new key-value pairs.
        // 2. A single key-value row if a main key-value pair is available.
        // 3. An empty container for additional rows.
        const html = `
        <div data-g4-role="keyvalue">
            <button type="button" title="Add Key/Value Pair">+</button>
            <input name="${inputId}-key" type="text" data-g4-role="key" title="Key: ${mainKey}" value="${mainKey}" />
            <input name="${inputId}-value" type="text" data-g4-role="value" title="Value: ${mainValue}" value="${mainValue}" />
        </div>
        <div id="${inputId}-input-container"></div>`;

        // Insert the constructed HTML into the controller container
        controllerContainer.insertAdjacentHTML('beforeend', html);

        // Select the "+" button from the newly inserted HTML
        const addButton = controllerContainer.querySelector('button');

        // Attach an onclick handler that creates a new key-value row when triggered
        addButton.addEventListener('click', () => newInputCallback(escapedId, setCallback));

        // Find the input container where subsequent key-value rows will be appended
        const inputContainer = fieldContainer.querySelector(`#${escapedId}-input-container`);

        // For each remaining key in the initial values, create additional key-value rows
        for (let i = 0; i < keys.length; i++) {
            const key = keys[i];
            const value = values[key];
            newInput({ container: inputContainer, key, value, index: i }, setCallback);
        }

        // Whenever an input event occurs (typing, etc.), process the updated field content
        fieldContainer.addEventListener('input', () => {
            callback(fieldContainer, setCallback);
        });

        // If a container is provided in options, append the field container to it
        if (options.container) {
            options.container.appendChild(fieldContainer);
        }

        // Return the container that holds the new key-value field
        return options.container ? options.container : fieldContainer;
    }

    /**
     * Creates and appends a new list (select) field to the specified container based on provided options.
     * The select field is populated with sorted items, each accompanied by a summary tooltip, and invokes a callback with the selected value upon input.
     *
     * @param {Object}                 options                    - Configuration options for the list field.
     * @param {HTMLElement}            [options.container]        - The DOM element to which the list field will be appended.
     * @param {string}                 options.label              - The identifier for the list field, used for data attributes and labeling.
     * @param {string}                 [options.title]            - The title attribute for the field container, often used for tooltips.
     * @param {string}                 [options.initialValue='']  - The initial selected value of the list field. Defaults to an empty string if not provided or invalid.
     * @param {string | Array<Object>} options.itemsSource - The source of items to populate the list.
     *   - If a string, it is used as a key to retrieve items from the `_cache` object.
     *   - If an array, it should contain objects with `name` and `description` properties.
     * @param {boolean}                [options.isReadonly=false] - Determines if the select field is read-only.
     * @param {Function}               setCallback                - Callback function to handle changes to the select field's value.
     *
     * @returns {HTMLElement} The container element that includes the newly created list field.
     *
     * @throws {Error} Throws an error if `itemsSource` is neither a string nor an array.
     */
    static newListField(options, setCallback) {
        /**
         * Processes the provided items source and returns a sorted object of items with their manifests.
         *
         * The function handles two types of `itemsSource`:
         * 1. **String**: Assumes `itemsSource` is a key to retrieve items from a cache (`_cache`).
         *    - Extracts the manifest key and summary for each item.
         *    - Defaults to 'No key available' and ['No summary available'] if properties are missing.
         * 2. **Array**: Assumes `itemsSource` is an array of item objects.
         *    - Extracts the name and description for each item.
         *    - Defaults to 'No name available' if the name property is missing.
         *
         * After processing, the function sorts the items alphabetically by their keys.
         */
        const getItems = (itemsSource) => {
            // Define a variable to hold the items after processing
            let items;

            // Check if itemsSource is a string
            if (typeof itemsSource === 'string') {
                // Retrieve items from the cache using the provided key
                // If the key does not exist in the cache, default to an empty object
                items = (itemsSource in _cache) ? _cache[itemsSource] : {};

                /**
                 * Transform the cached items into a structured object.
                 * - Each key is derived from the item's manifest key or defaults to 'No key available'.
                 * - Each value contains the item's manifest with a summary.
                 */
                items = Object.keys(items).reduce((obj, key) => {
                    const item = items[key];

                    // Safely access the manifest key; default if unavailable
                    const manifestKey = item?.manifest?.key || 'No key available';

                    // Safely access the manifest summary; default if unavailable
                    obj[manifestKey] = {
                        manifest: {
                            summary: item?.manifest?.summary || ['No summary available']
                        }
                    };

                    return obj;
                }, {});
            }
            // Check if itemsSource is an array
            else if (Array.isArray(itemsSource)) {
                /**
                 * Transform the array of items into a structured object.
                 * - Each key is derived from the item's name or defaults to 'No name available'.
                 * - Each value contains the item's manifest with a summary derived from the description.
                 */
                items = itemsSource.reduce((obj, item) => {
                    // Safely access the item's name; default if unavailable
                    const key = item.name || 'No name available';

                    // Safely access the item's description; default if unavailable
                    obj[key] = {
                        manifest: {
                            summary: item.description || 'No description available'
                        }
                    };

                    // Return the updated object
                    return obj;
                }, {});
            }
            // If itemsSource is neither string nor array, throw an error
            else {
                throw new Error('Invalid itemsSource type. Must be a string or an array.');
            }

            /**
             * Sort the items alphabetically by their keys.
             * - Extracts the keys, sorts them, and reconstructs the items object in sorted order.
             */
            items = Object.keys(items)
                .sort((i, j) => i.localeCompare(j))
                .reduce((obj, key) => {
                    obj[key] = items[key];
                    return obj;
                }, {});

            // Return the sorted items object
            return items;
        };

        // Generate a unique identifier for the list field to ensure uniqueness in the DOM
        const inputId = Utilities.newUid();

        // Convert the label from PascalCase to a space-separated format for display purposes
        const labelDisplayName = options.label;

        // Process the items source to get a sorted items object
        const items = getItems(options.itemsSource);

        /**
         * Validate and sanitize the initial value.
         * If the initial value is not provided or is the string 'undefined', default it to an empty string.
         */
        options.initialValue = (!options.initialValue || options.initialValue === 'undefined')
            ? ''
            : options.initialValue;

        /**
         * Construct the HTML string for the select element with the necessary attributes.
         * - `title`: Tooltip text showing the current selected value or a prompt if none is selected.
         * - `type`: Specifies the input type as select.
         */
        let html = `
        <select name="${inputId}-select" title="${options.initialValue === '' ? 'Please select an option' : options.initialValue}"> 
            <option value="" disabled selected>-- Please select an option --</option>`;

        /**
         * Iterate over each key in the sorted items object to create corresponding option elements.
         * - Each option's value is the key.
         * - Each option's title attribute contains the summary for tooltip display.
         * - The displayed text is the key converted from PascalCase to space-separated format.
         */
        Object.keys(items).forEach(key => {
            const summary = items[key].manifest?.summary || ['No summary available'];
            html += `  <option value="${key}" title="${summary.join("\n")}">${convertPascalToSpaceCase(key)}</option>\n`;
        });

        // Close the select element
        html += '</select>';

        // Create a container for the field using a helper function, passing the unique ID, display label, and title
        const fieldContainer = newFieldContainer(inputId, labelDisplayName, options.title);

        // Select the specific sub-container within the field container where the select element will reside
        const controllerContainer = fieldContainer.querySelector('[data-g4-role="controller"]');

        // Insert the constructed HTML into the controller container at the end of its current content
        controllerContainer.insertAdjacentHTML('beforeend', html);

        // Retrieve the newly inserted select element for further manipulation
        const select = controllerContainer.querySelector('select');
        select.value = options.initialValue;

        /**
         * If a callback function is provided, add an event listener to handle input events.
         * - Updates the `title` attribute of the input to reflect its current value.
         * - Invokes the `setCallback` function with the new value whenever the input changes.
         */
        if (typeof setCallback === 'function') {
            fieldContainer.addEventListener('input', () => {
                select.title = select.value;
                setCallback(select.value);
            });
        }

        /**
         * If a container element is provided in the options, append the entire field container to it.
         * This allows for flexible placement of the new list field within the DOM.
         */
        if (options.container) {
            options.container.appendChild(fieldContainer);
        }

        /**
         * Return the container that now includes the new list field.
         * - If an external container was provided, return that container.
         * - Otherwise, return the newly created field container.
         */
        return options.container ? options.container : fieldContainer;
    }

    /**
     * Creates and appends a new name input field with autocomplete functionality to the specified container based on provided options.
     * The input field utilizes a datalist for alias suggestions and invokes a callback with the current value upon input.
     *
     * @param {Object}        options                    - Configuration options for the name field.
     * @param {HTMLElement}   [options.container]        - The DOM element to which the name field will be appended.
     * @param {string}        options.label              - The identifier for the name field, used for data attributes and labeling.
     * @param {string}        [options.title]            - The title attribute for the field container, often used for tooltips.
     * @param {string|number} [options.initialValue='']  - The initial value of the input field. Defaults to an empty string if not provided or invalid.
     * @param {Object}        options.step               - An object containing step-related configurations.
     * @param {string}        options.step.pluginName    - The plugin name to be used as a default alias.
     * @param {Array<string>} [options.step.aliases]     - An array of alias strings to be suggested in the datalist.
     * @param {boolean}       [options.isReadonly=false] - Determines if the input field is read-only.
     * @param {Function}      setCallback                - Callback function to handle changes to the input field's value.
     *
     * @returns {HTMLElement} The container element that includes the newly created name field.
     */
    static newNameField(options, setCallback) {
        // Generate a unique identifier for the input field to ensure uniqueness in the DOM.
        const inputId = Utilities.newUid();

        // Extract aliases from the step configuration if available.
        const aliases = options.step.aliases;

        // Convert the plugin name from PascalCase to a space-separated format for display purposes.
        const pluginName = Utilities.convertPascalToSpaceCase(options.step.pluginName);

        // Convert the label from PascalCase to a space-separated format for display purposes.
        const labelDisplayName = options.label;

        /**
         * Validate and sanitize the initial value.
         * If the initial value is not provided, is NaN, or is the string 'undefined', default it to an empty string.
         */
        options.initialValue = (!options.initialValue || options.initialValue === 'undefined')
            ? ''
            : options.initialValue;

        /**
         * Construct the HTML string for the input element with the necessary attributes.
         * - `list`             : Associates the input with a datalist for autocomplete suggestions.
         * - `data-g4-attribute`: Custom data attribute for identifying the field.
         * - `title`            : Tooltip text showing the current value or a prompt if empty.
         * - `type`             : Specifies the input type as text.
         * - `spellcheck`       : Disables spell checking for the input field.
         * - `value`            : Sets the initial value of the input field.
         */
        let html = `
        <input 
            list="${inputId}-aliases" 
            data-g4-attribute="${options.label}" 
            title="${options.initialValue === '' ? 'Please select a different alias' : options.initialValue}" 
            type="text" 
            spellcheck='false'
            value='${options.initialValue}' />`;

        /**
         * If aliases are provided, construct a datalist with options for autocomplete suggestions.
         * The datalist includes the plugin name as a default option and any additional aliases.
         */
        if (aliases && aliases.length > 0) {
            html += `
            <datalist id="${inputId}-aliases">
                <option value="${pluginName}" label="${options.step.pluginName}">${pluginName}</option>\n`;

            // Iterate over each alias to create corresponding datalist options.
            aliases.forEach(alias => {
                const formattedAlias = Utilities.convertPascalToSpaceCase(alias);
                html += `  <option value="${formattedAlias}" label="${alias}">${formattedAlias}</option>\n`;
            });

            html += '</datalist>';
        }

        // Create a container for the field using a helper function, passing the unique ID, display label, and title.
        const fieldContainer = newFieldContainer(inputId, labelDisplayName, options.title);

        // Select the specific sub-container within the field container where the input element will reside.
        const controllerContainer = fieldContainer.querySelector('[data-g4-role="controller"]');

        // Insert the constructed HTML into the controller container at the end of its current content.
        controllerContainer.insertAdjacentHTML('beforeend', html);

        // Retrieve the newly inserted input element for further manipulation.
        const input = controllerContainer.querySelector('input');

        /**
         * If the `isReadonly` option is true, set the `readonly` attribute on the input element
         * to prevent user modification.
         */
        if (options.isReadonly) {
            input.setAttribute('readonly', 'readonly');
        }

        /**
         * If a callback function is provided, add an event listener to handle input events.
         * - Updates the `title` attribute of the input to reflect its current value.
         * - Invokes the `setCallback` function with the new value whenever the input changes.
         */
        if (typeof setCallback === 'function') {
            fieldContainer.addEventListener('input', () => {
                input.title = input.value;
                setCallback(input.value);
            });
        }

        /**
         * If a container element is provided in the options, append the entire field container to it.
         * This allows for flexible placement of the new name field within the DOM.
         */
        if (options.container) {
            options.container.appendChild(fieldContainer);
        }

        /**
         * Return the container that now includes the new name field.
         * - If an external container was provided, return that container.
         * - Otherwise, return the newly created field container.
         */
        return options.container ? options.container : fieldContainer;
    }

    /**
     * Creates and appends a new number input field to the specified container based on provided options.
     * The input field handles numeric values with specified steps and invokes a callback with the current value upon input.
     *
     * @param {Object}        options                    - Configuration options for the number field.
     * @param {HTMLElement}   [options.container]        - The DOM element to which the number field will be appended.
     * @param {string}        options.label              - The identifier for the number field, used for data attributes and labeling.
     * @param {string}        [options.title]            - The title attribute for the field container, often used for tooltips.
     * @param {number|string} [options.initialValue='']  - The initial value of the number input. Defaults to an empty string if not provided or invalid.
     * @param {number}        [options.step=1]           - The increment step for the number input.
     * @param {boolean}       [options.isReadonly=false] - Determines if the number input is read-only.
     * @param {Function}      setCallback                - Callback function to handle changes to the number field's value.
     *
     * @returns {HTMLElement} The container element that includes the newly created number field.
     */
    static newNumberField(options, setCallback) {
        // Generate a unique identifier for the number input to ensure uniqueness in the DOM.
        const inputId = Utilities.newUid();

        // Convert the label from PascalCase to a space-separated format for display purposes.
        const labelDisplayName = options.label;

        /**
         * Validate and sanitize the initial value.
         * If the initial value is not provided, is NaN, or is the string 'undefined', default it to an empty string.
         */
        options.initialValue = (!options.initialValue || isNaN(options.initialValue) || options.initialValue === 'undefined')
            ? ''
            : options.initialValue;

        /**
         * Construct the HTML string for the input element with the necessary attributes.
         * - `id`               : Unique identifier for the input.
         * - `data-g4-attribute`: Custom data attribute for identifying the field.
         * - `title`            : Tooltip text showing the current value.
         * - `type`             : Specifies the input type as number.
         * - `step`             : Defines the legal number intervals for the input.
         * - `value`            : Sets the initial value of the input field.
         */
        const html = `
        <input 
            id="${inputId}"
            data-g4-attribute="${options.label}" 
            title="${options.initialValue}" 
            type="number" 
            step="${options.step || 1}"
            value="${options.initialValue}" />`;

        // Create a container for the field using a helper function, passing the unique ID, display label, and title.
        const fieldContainer = newFieldContainer(inputId, labelDisplayName, options.title);

        // Select the specific sub-container within the field container where the input element will reside.
        const controllerContainer = fieldContainer.querySelector('[data-g4-role="controller"]');

        // Insert the input HTML into the controller container at the end of its current content.
        controllerContainer.insertAdjacentHTML('beforeend', html);

        // Retrieve the newly inserted input element for further manipulation.
        const input = controllerContainer.querySelector('input');

        /**
         * If the `isReadonly` option is true, set the `readonly` attribute on the input element
         * to prevent user modification.
         */
        if (options.isReadonly) {
            input.setAttribute('readonly', 'readonly');
        }

        /**
         * If a callback function is provided, add an event listener to handle input events.
         * - Updates the `title` attribute of the input to reflect its current value.
         * - Invokes the `setCallback` function with the new value whenever the input changes.
         */
        if (typeof setCallback === 'function') {
            fieldContainer.addEventListener('input', () => {
                input.title = input.value;
                setCallback(input.value);
            });
        }

        /**
         * If a container element is provided in the options, append the entire field container to it.
         * This allows for flexible placement of the new number field within the DOM.
         */
        if (options.container) {
            options.container.appendChild(fieldContainer);
        }

        /**
         * Return the container that now includes the new number field.
         * - If an external container was provided, return that container.
         * - Otherwise, return the newly created field container.
         */
        return options.container ? options.container : fieldContainer;
    }

    /**
     * Creates and appends a new string input field (textarea) to the specified container based on provided options.
     * The textarea dynamically adjusts its height based on content and invokes a callback with the current value upon input.
     *
     * @param {Object}        options                    - Configuration options for the string field.
     * @param {HTMLElement}   [options.container]        - The DOM element to which the string field will be appended.
     * @param {string}        options.label              - The identifier for the string field, used for data attributes and labeling.
     * @param {string}        [options.title]            - The title attribute for the field container, often used for tooltips.
     * @param {string|number} [options.initialValue='']  - The initial value of the textarea. Defaults to an empty string if not provided or invalid.
     * @param {boolean}       [options.isReadonly=false] - Determines if the textarea is read-only.
     * @param {Function}      setCallback                - Callback function to handle changes to the textarea's value.
     *
     * @returns {HTMLElement} The container element that includes the newly created string field.
     */
    static newStringField(options, setCallback) {
        /**
         * Adjusts the size of the textarea dynamically based on its content
         * and invokes the callback with the current value.
         *
         * @param {HTMLTextAreaElement} textarea - The textarea element to resize and process.
         */
        const callback = (textarea) => {
            // Reset the height to 'auto' to recalculate the required height based on content.
            textarea.style.height = 'auto';

            // Calculate the scroll height, which represents the full height of the content.
            const contentHeight = textarea.scrollHeight;

            // Get the computed styles of the textarea to determine line height and minimum height.
            const computedStyle = window.getComputedStyle(textarea);
            const lineHeight = parseFloat(computedStyle.lineHeight);
            const minHeight = parseFloat(computedStyle.minHeight);

            // Define the maximum number of lines the textarea can expand to before enabling scroll.
            const maxLines = 8;
            const maxHeight = lineHeight * maxLines;

            if (contentHeight <= maxHeight) {
                // If the content height is within the maximum allowed height:

                // Set the textarea height to the content height or minimum height, whichever is greater.
                textarea.style.height = contentHeight > minHeight ? `${contentHeight}px` : `${minHeight}px`;

                // Hide the vertical scrollbar since content fits within the textarea.
                textarea.style.overflowY = 'hidden';
                // If the content exceeds the maximum allowed height:
            } else {
                // Set the textarea height to the maximum height.
                textarea.style.height = `${maxHeight}px`;

                // Enable the vertical scrollbar to allow scrolling through content.
                textarea.style.overflowY = contentHeight === 0 ? 'hidden' : 'scroll';
            }

            // Invoke the callback function with the current value of the textarea.
            setCallback(textarea.value);
        };

        /**
         * Creates and appends a new modal containing a textarea and a close button
         * for a given input field. If a modal with the same ID already exists,
         * it is first removed. The contents of the textarea are mirrored into
         * the original input field. Clicking the close button removes the modal
         * and re-enables the original input.
         *
         * @param {string} inputId - The unique identifier of the target input.
         * @param {HTMLElement} fieldContainer - The container element to which the modal will be appended.
         * @returns {HTMLElement} The newly created modal element.
         */
        const newModal = (inputId, fieldContainer) => {
            // Escape the inputId to ensure valid and safe usage in CSS selectors
            const escapedId = CSS.escape(inputId);

            // Find and remove any existing modal with the same ID in the container
            const existingModal = fieldContainer?.querySelector(`#${escapedId}-modal`);
            if (existingModal) {
                fieldContainer.removeChild(existingModal);
            }

            // Create a container <div> to serve as the modal
            const modalElement = document.createElement('div');
            modalElement.setAttribute('id', `${inputId}-modal`);
            modalElement.setAttribute('class', 'sqd-modal');
            modalElement.setAttribute('data-g4-role', 'input-modal');

            // Create a container for the textarea and close button
            const textareaContainerElement = document.createElement('div');

            // Create the textarea element
            const textareaElement = document.createElement('textarea');
            textareaElement.setAttribute('id', `${inputId}-textarea`);
            textareaElement.setAttribute('wrap', 'off');
            textareaElement.setAttribute('spellcheck', 'false');

            // Listen for changes in the modal's textarea and update the original field's value
            textareaElement.addEventListener('input', () => {
                const textarea = fieldContainer.querySelector(`#${escapedId}`);
                textarea.value = textareaElement.value;
            });

            // Create a button to close the modal
            const closeButtonElement = document.createElement('button');
            closeButtonElement.setAttribute('id', `${inputId}-closeButton`);
            closeButtonElement.setAttribute('type', 'button');
            closeButtonElement.innerText = 'Close';

            // On click, re-enable the original textarea and remove the modal
            closeButtonElement.addEventListener('click', () => {
                const textarea = fieldContainer.querySelector(`#${escapedId}`);
                textarea.disabled = false;
                fieldContainer.removeChild(modalElement);
            });

            // Add the textarea and close button to the container
            textareaContainerElement.appendChild(textareaElement);
            textareaContainerElement.appendChild(closeButtonElement);

            // Add the container to the modal
            modalElement.appendChild(textareaContainerElement);

            // Append the newly created modal to the field container
            fieldContainer.appendChild(modalElement);

            // Return the modal element so it can be referenced if needed
            return modalElement;
        };

        // Generate a unique identifier for the textarea to ensure uniqueness in the DOM.
        const inputId = Utilities.newUid();

        // Convert the label from PascalCase to a space-separated format for display purposes.
        const labelDisplayName = options.label;

        /**
         * Validate and sanitize the initial value.
         * If the initial value is not provided, is NaN, or is the string 'undefined', default it to an empty string.
         */
        options.initialValue = (!options.initialValue || options.initialValue === 'undefined')
            ? ''
            : options.initialValue;

        // Create a textarea element dynamically.
        const textareaElement = document.createElement('textarea');

        // Set necessary attributes for the textarea.
        textareaElement.setAttribute('id', inputId);
        textareaElement.setAttribute('rows', '1');                        // Start with a single row; height will adjust dynamically.
        textareaElement.setAttribute('wrap', 'off');                      // Disable text wrapping to allow horizontal scrolling if needed.
        textareaElement.setAttribute('data-g4-attribute', options.label); // Custom data attribute for identification.
        textareaElement.setAttribute('spellcheck', 'false');              // Disable spell checking.
        textareaElement.setAttribute('title', options.initialValue);      // Tooltip displaying the current value.
        textareaElement.value = options.initialValue;                     // Set the initial value of the textarea.

        // Listen for double-click events to open a modal for editing the textarea content.
        textareaElement.addEventListener("dblclick", () => {
            // Create a modal for the textarea using a helper function.
            const modalElement = newModal(inputId, fieldContainer);

            // Disable the original textarea to prevent editing while the modal is open
            textareaElement.disabled = true;

            // Mirror the textarea content into the modal textarea
            modalElement.querySelector("textarea").value = textareaElement.value;
        });

        // Create a container for the field using a helper function, passing the unique ID, display label, and title.
        const fieldContainer = newFieldContainer(inputId, labelDisplayName, options.title);

        // Escape the inputId to safely use it in a CSS selector.
        const escapedId = CSS.escape(inputId);

        // Select the specific sub-container within the field container where the textarea will reside.
        const controllerContainer = fieldContainer.querySelector(`#${escapedId}-controller`);

        // Append the textarea element to the controller container.
        controllerContainer.appendChild(textareaElement);

        // If the textarea should be read-only, set the 'readonly' attribute.
        if (options.isReadonly) {
            textareaElement.setAttribute('readonly', 'readonly');
        }

        /**
         * If a callback function is provided, add an event listener to handle changes to the select field.
         * - Updates the `title` attribute of the select to reflect its current value.
         * - Invokes the `setCallback` function with the new value whenever the selection changes.
         */
        if (typeof setCallback === 'function') {
            fieldContainer.addEventListener('input', () => {
                textareaElement.title = textareaElement.value;
                callback(textareaElement);
            });
        }

        // If a container element is provided in the options, append the entire field container to it.
        if (options.container) {
            options.container.appendChild(fieldContainer);
        }

        /**
         * Return the container that now includes the new string field.
         * - If an external container was provided, return that container.
         * - Otherwise, return the newly created field container.
         */
        return options.container ? options.container : fieldContainer;
    }

    /**
     * Creates and appends a new switch (select) field to the specified container based on provided options.
     *
     * @param {Object}         options                      - Configuration options for the switch field.
     * @param {HTMLElement}    [options.container]          - The DOM element to which the switch field will be appended.
     * @param {string}         options.label                - The identifier for the switch field, used for data attributes and labeling.
     * @param {string}         [options.title]              - The title attribute for the field container, often used for tooltips.
     * @param {boolean|string} [options.initialValue=false] - The initial value of the switch field. Can be `true`, `false`, or a falsy value.
     * @param {Function}       setCallback                  - Callback function to handle changes to the switch field's value.
     *
     * @returns {HTMLElement} The container element that includes the newly created switch field.
     */
    static newSwitchField(options, setCallback) {
        // Generate a unique identifier for the switch field to ensure uniqueness in the DOM
        const inputId = Utilities.newUid();

        // Convert the label from PascalCase to a space-separated format for display purposes
        const labelDisplayName = options.label;

        /**
         * Validate and sanitize the initial value.
         * If the initial value is not provided, is NaN, or is undefined, default it to `false`.
         */
        options.initialValue = (!options.initialValue || options.initialValue === null || Number.isNaN(options.initialValue) || options.initialValue === undefined)
            ? false
            : options.initialValue;

        /**
         * Construct the HTML string for the select element with the necessary attributes.
         * - `data-g4-attribute`: Custom data attribute for identifying the field.
         * - `title`            : Tooltip text showing the current value.
         * - `select`           : Creates a dropdown with options to activate or deactivate the switch.
         */
        const html = `
        <select name="${inputId}-switch" data-g4-attribute="${options.label}" title="${options.initialValue}">
            <option value="" disabled selected>-- Please select an option --</option>
            <option value="true" title="Activate switch">True</option>
            <option value="false" title="Deactivate switch">False</option>
        </select>`;

        // Create a container for the field using a helper function, passing the unique ID, display label, and title
        const fieldContainer = newFieldContainer(inputId, labelDisplayName, options.title);

        // Select the specific sub-container within the field container where the select element will reside
        const controllerContainer = fieldContainer.querySelector('[data-g4-role="controller"]');

        // Insert the select HTML into the controller container at the end of its current content
        controllerContainer.insertAdjacentHTML('beforeend', html);

        // Retrieve the newly inserted select element for further manipulation
        const select = controllerContainer.querySelector('select');
        select.value = options.initialValue;

        /**
         * If a callback function is provided, add an event listener to handle changes to the select field.
         * - Updates the `title` attribute of the select to reflect its current value.
         * - Invokes the `setCallback` function with the new value whenever the selection changes.
         */
        if (typeof setCallback === 'function') {
            fieldContainer.addEventListener('input', () => {
                select.title = select.value;
                setCallback(select.value);
            });
        }

        /**
         * If a container element is provided in the options, append the entire field container to it.
         * This allows for flexible placement of the new switch field within the DOM.
         */
        if (options.container) {
            options.container.appendChild(fieldContainer);
        }

        /**
         * Return the container that now includes the new switch field.
         * - If an external container was provided, return that container.
         * - Otherwise, return the newly created field container.
         */
        return options.container ? options.container : fieldContainer;
    }

    /**
     * Creates and returns a new text input field based on the provided options.
     *
     * @param {Object}      options                    - Configuration options for the text field.
     * @param {string}      options.label              - The label identifier for the text field.
     * @param {string}      [options.initialValue]     - The initial value to populate the text field.
     * @param {string}      [options.title]            - The title attribute for the field container, often used for tooltips.
     * @param {boolean}     [options.isReadonly=false] - Determines if the text field is read-only.
     * @param {HTMLElement} [options.container]        - The DOM element to which the text field will be appended.
     * @param {Function}    setCallback                - Callback function to handle changes to the text field's value.
     *
     * @returns {HTMLElement} The container element that includes the newly created text field.
     */
    static newTextField(options, setCallback) {

        // Generate a unique identifier for the input field to ensure uniqueness in the DOM
        const inputId = Utilities.newUid();

        // Convert the label from PascalCase to a space-separated format for display purposes
        const labelDisplayName = options.label;

        /**
         * Validate and sanitize the initial value.
         * If the initial value is not provided, not a number, or undefined, default it to an empty string.
         */
        options.initialValue = (!options.initialValue || options.initialValue === 'undefined')
            ? ''
            : options.initialValue;

        /**
         * Construct the HTML string for the input element with the necessary attributes.
         * - `data-g4-attribute`: Custom data attribute for identifying the field.
         * - `title`            : Tooltip text showing the current value.
         * - `type`             : Specifies the input type as text.
         * - `spellcheck`       : Disables spell checking for the input field.
         * - `value`            : Sets the initial value of the input field.
         */
        const html = `
        <input 
            data-g4-attribute="${options.label}" 
            title="${options.initialValue}" 
            type="text" 
            spellcheck="false"
            value="${options.initialValue}" />`;

        // Create a container for the field using a helper function, passing the unique ID, display label, and title
        const fieldContainer = newFieldContainer(inputId, labelDisplayName, options.title);

        // Select the specific sub-container within the field container where the input element will reside
        const controllerContainer = fieldContainer.querySelector('[data-g4-role="controller"]');

        // Insert the input HTML into the controller container at the end of its current content
        controllerContainer.insertAdjacentHTML('beforeend', html);

        // Retrieve the newly inserted input element for further manipulation
        const input = controllerContainer.querySelector('input');

        /**
         * If the `isReadonly` option is true, set the `readonly` attribute on the input element
         * to prevent user modification.
         */
        if (options.isReadonly) {
            input.setAttribute('readonly', 'readonly');
        }

        /**
         * Add an event listener to the field container that listens for input events.
         * - Updates the `title` attribute of the input to reflect its current value.
         * - Invokes the `setCallback` function with the new value whenever the input changes.
         */
        if (typeof setCallback === 'function') {
            fieldContainer.addEventListener('input', () => {
                input.title = input.value;
                setCallback(input.value);
            });
        }

        /**
         * If a container element is provided in the options, append the entire field container to it.
         * This allows for flexible placement of the new text field within the DOM.
         */
        if (options.container) {
            options.container.appendChild(fieldContainer);
        }

        /**
         * Return the container that now includes the new text field.
         * - If an external container was provided, return that container.
         * - Otherwise, return the newly created field container.
         */
        return options.container ? options.container : fieldContainer;
    }

    /**
     * Creates and appends a new title section to the specified container.
     *
     * This section includes a main title, a subtitle, and a hint icon that toggles additional help text.
     *
     * @param {Object}      options              - Configuration options for the title section.
     * @param {HTMLElement} options.container    - The DOM element to which the new title section will be appended.
     * @param {string}      options.titleText    - The main title text to be displayed as an `<h2>` element.
     * @param {string}      options.subTitleText - The subtitle text to be displayed within a `<span>` element with the class "subtitle".
     * @param {string}      options.helpText     - The help text to be displayed when the hint icon is clicked.
     * @returns {HTMLElement} - The parent container with the appended title section.
     */
    static newTitle(options) {
        /**
         * Toggles a hint text element inside a specified container.
         * If the hint text element already exists, it is removed. Otherwise, a new one is created and added.
         */
        const switchHint = (hintContainer, hintText) => {
            // Query for an existing hint text element within the hintContainer.
            let hintElement = hintContainer.querySelector('div.sqd-help-text');

            // If the hint text element already exists, remove it and exit the function.
            if (hintElement) {
                hintElement.remove();
                return;
            }

            // Create a new hint text element if it doesn't already exist.
            hintElement = document.createElement('div');
            hintElement.classList.add('sqd-help-text'); // Add the 'sqd-help-text' class for styling.
            hintElement.innerHTML = hintText;       // Set the provided hint text as the content.

            // Append the newly created hint text element to the hintContainer.
            hintContainer.appendChild(hintElement);
        };

        // Set default value for helpText if not provided.
        options.helpText = options.helpText || 'Help text not provided.';

        // Create a new div element to contain the title and subtitle.
        const titleContainer = document.createElement('div');
        titleContainer.setAttribute('data-g4-role', 'title');

        // Define the HTML structure for the title, subtitle, and hint icon.
        const html = `
        <div class="sqd-title sqd-label--with-help-icon">
            ${options.titleText}
            <span class="sqd-help-icon-container" tabindex="0" title="More Information" role="img" aria-label="More Information">
                <svg viewBox="0 -960 960 960" class="sqd-help-icon" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
                    <path d="M419-334q1-87 20.5-129t65.5-76q39-31 57.5-61.109T581-666q0-39-25.5-64.5T486-756q-46 0-75 26t-43 67l-120-52q27-74 87-120.5T485.756-882q109.228 0 168.236 62.148Q713-757.703 713-669q0 60-21 105.5T625-478q-46 40-57 65.5T557-334H419Zm66.788 282Q447-52 420-79t-27-65.496q0-38.495 26.92-65.5Q446.841-237 485.92-237 525-237 552-209.996q27 27.005 27 65.5Q579-106 551.788-79q-27.213 27-66 27Z"></path>
                </svg>
            </span>
        </div>
        <span class="sqd-subtitle">${options.subTitleText}</span>
        <div data-g4-role="summary"></div>`;

        // Insert the HTML structure into the title container.
        titleContainer.insertAdjacentHTML('beforeend', html);

        // Select the hint icon element to attach the click event listener.
        const iconElement = titleContainer.querySelector('[role="img"]');

        // Select the hint container where the hint text will be toggled.
        const hintContainer = titleContainer.querySelector('[data-g4-role="summary"]');

        // Add event listener to the hint icon to toggle the hint text on click.
        iconElement.addEventListener('click', () => switchHint(hintContainer, options.helpText));

        // Append the populated title container to the provided parent container if specified.
        if (options.container) {
            options.container.appendChild(titleContainer);
        }

        // Return the updated container for potential further use by the calling code.
        return options.container ? options.container : titleContainer;
    }
}
