class Validators {
    static confirmContentRule(step, definition) {
        // Not a Content rule, skip validation
        if (step.pluginType.toUpperCase() !== 'CONTENT') {
            return true;
        }

        // Ensure step has a context and error bucket initialized
        step.context = step.context || {};
        step.context.errors = step.context.errors || {};

        // Try to find the direct parent container of this step
        const parent = Utilities.findParentContainer(step, definition.sequence);

        // Check if the parent is a valid container (must be of type 'ExportData')
        const isContainer = parent?.pluginName === 'ExportData';

        // Step is valid — remove any existing error related to placement
        if (isContainer) {
            // Clear any previous error messages related to container placement
            delete step.context.errors.contentPlacement;

            // Return true to indicate validation success
            return true;
        }

        // Step is invalid — set a descriptive, user-friendly error
        step.context.errors.contentPlacement = {
            title: "Invalid Content Rule Placement",
            description: "The Content rule must be placed inside an Export (Extraction) rule. To fix this, " +
                "move the Content rule under a parent step that uses the ExportData plugin. This ensures it functions correctly within the extraction flow.",
            level: "ERR"
        };

        // Return false to indicate validation failure
        return false;
    }

    static confirmJobPlacement(step, definition) {
        // If the step is not a Job, skip validation.
        if (step.pluginName !== 'G4™ Job') {
            return true;
        }

        // Ensure the step has a context object and an errors bucket for validation.
        step.context = step.context || {};
        step.context.errors = step.context.errors || {};

        // Locate the real parent container of this step by walking the workflow tree.
        const parent = Utilities.findParentContainer(step, definition.sequence);

        // Determine whether the parent is a valid Stage container.
        const isUnderStage = !!parent && parent.pluginName === 'G4™ Stage';

        // If the parent is a Stage, this is valid placement.
        if (isUnderStage) {
            // Remove any previous placement error.
            delete step.context.errors.jobPlacement;

            // Validation succeeded.
            return true;
        }

        // Invalid placement: set a user-friendly error.
        step.context.errors.jobPlacement = {
            title: "Invalid Job Placement",
            description:
                "A Job step must be placed directly under a Stage (G4™ Stage) container. " +
                "To fix this, move the Job step into a parent step whose pluginName is “G4™ Stage.”",
            level: "ERR"
        };

        // Validation failed.
        return false;
    }

    static confirmRulePlacement(step, definition) {
        // Define types that should be skipped for validation.
        const skipTypes = ['G4™ Stage', 'G4™ Job'];

        // 1. Skip validation for Stage or Job steps themselves.
        if (skipTypes.includes(step.pluginName)) {
            return true;
        }

        // 2. Initialize context and error storage if missing.
        step.context = step.context || {};
        step.context.errors = step.context.errors || {};

        // 3. Find the immediate parent container.
        let container = Utilities.findParentContainer(step, definition.sequence);

        // 4. Traverse up the tree to find any Job ancestor.
        while (container) {
            if (container.pluginName === 'G4™ Job') {
                // 5a. Valid placement: remove previous error and succeed.
                delete step.context.errors.rulePlacement;

                // Validation succeeded.
                return true;
            }
            container = Utilities.findParentContainer(container, definition.sequence);
        }

        // 6. Invalid placement: record a user-friendly error.
        step.context.errors.rulePlacement = {
            title: "Invalid Rule Placement",
            description:
                "A Rule step must be nested under a Job container (pluginName “G4™ Job”) " +
                "somewhere in the workflow. To fix this, move the Rule step under a parent " +
                "whose pluginName is “G4™ Job.”",
            level: "ERR"
        };

        // Validation failed.
        return false;
    }

    static confirmStagePlacement(step, definition) {
        // If the step is not a Stage, skip validation.
        if (step.pluginName !== 'G4™ Stage') {
            return true;
        }

        // Ensure the step has a context object and an errors bucket for validation.
        step.context = step.context || {};
        step.context.errors = step.context.errors || {};

        // Locate the real parent container of this step by walking the workflow tree.
        const parent = Utilities.findParentContainer(step, definition.sequence);

        // If no parent container was found, this is a valid top-level Stage.
        if (!parent) {
            // Remove any previous placement error key.
            delete step.context.errors.stagePlacement;

            // Validation succeeded.
            return true;
        }

        // A parent was found → invalid placement. Record a user-friendly error.
        step.context.errors.stagePlacement = {
            title: "Invalid Stage Placement",
            description:
                "A Stage step must be at the top level and cannot be nested inside another container. " +
                "To fix this, move the Stage step out of any parent and place it directly under the root sequence.",
            level: "ERR"
        };

        // Validation failed.
        return false;
    }

    static confirmStepProperties(step) {
        /**
         * Validates that a required property exists and has a value, logging a descriptive error if not.
         */
        const newPropertyError = (step, propertyName) => {
            const propertyData = step.properties[propertyName];
            const isRequired = Boolean(propertyData.required);
            const hasValue = propertyData.value != null && propertyData.value !== "";

            // Step is valid — clear any existing error and return success
            if (!isRequired || hasValue) {
                delete step.context.errors[propertyName];
                return null;
            }

            // Step is invalid — set a descriptive, user-friendly error
            const friendlyName = Utilities.convertToPascalCase(propertyName);

            // Return an error object with a title and description
            return {
                key: propertyName,
                value: {
                    title: `Missing Required Property: ${Utilities.convertPascalToSpaceCase(friendlyName)}`,
                    description: `The "${friendlyName}" property is required but is currently missing. To fix this, ` +
                        `add the "${friendlyName}" property to your step configuration with a valid value.`,
                    level: "ERR"
                }
            };
        };

        // Define types that should be skipped for validation.
        const skipTypes = ['G4™ Stage', 'G4™ Job'];

        // Skip validation for Stage or Job steps.
        if (skipTypes.includes(step.pluginName)) {
            return true;
        }

        // Ensure the step has a context object and an errors bucket for validation.
        step.context = step.context || {};
        step.context.errors = step.context.errors || {};

        // Initialize an object to hold any property errors found during validation.
        const propertyErrors = {};

        // Create an object to hold any errors found during validation.
        for (const property in step.properties) {
            const propertyError = newPropertyError(step, property);

            // If no error was found for this property, continue to the next one.
            if (!propertyError) {
                continue;
            }

            // Add the error to the propertyErrors object and the step context errors.
            propertyErrors[propertyError.key] = propertyError.value;
            step.context.errors[propertyError.key] = propertyError.value;
        }

        // Assess whether the step is valid based on the errors found.
        const isValid = Object.keys(propertyErrors).length === 0;

        // If no errors were found, return true to indicate validation success.
        return isValid;
    }
}
