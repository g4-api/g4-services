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

        // A Job is also valid as a lane: directly inside a Parallel container that sits directly inside a Stage.
        const isUnderParallel = StateMachineSteps.testG4Parallel(parent);
        const parallelParent = isUnderParallel
            ? Utilities.findParentContainer(parent, definition.sequence)
            : null;
        const isLane = isUnderParallel && parallelParent?.pluginName === 'G4™ Stage';

        // If the parent is a Stage, or the Job is a lane, this is valid placement.
        const isPlaced = isUnderStage || isLane;

        if (isPlaced) {
            // Remove any previous placement error.
            delete step.context.errors.jobPlacement;

            // Validation succeeded.
            return true;
        }

        // Invalid placement: set a user-friendly error.
        step.context.errors.jobPlacement = {
            title: "Invalid Job Placement",
            description:
                "A Job step must be placed directly under a Stage (G4™ Stage) container, or directly under a " +
                "Parallel container that sits inside a Stage. To fix this, move the Job step into a Stage, " +
                "or into a Parallel container inside a Stage.",
            level: "ERR"
        };

        // Validation failed.
        return false;
    }

    /**
     * Confirms that a Parallel container sits where the engine accepts one.
     *
     * A Parallel container is valid at the top level, where its lanes are stages, and directly inside a Stage,
     * where its lanes are jobs. Directly inside another Parallel container it is rejected by the engine, and
     * anywhere else it has no meaning. The result is recorded on the step as the `parallelPlacement` error,
     * which the step editor shows; it is cleared when the placement is valid.
     *
     * @param {Object} step       - The step to check; any step other than a Parallel container passes.
     * @param {Object} definition - The designer definition that holds the step.
     * @returns {boolean} True when the step is not a Parallel container, or is placed correctly.
     */
    static confirmParallelPlacement(step, definition) {
        // If the step is not a Parallel container, skip validation.
        if (!StateMachineSteps.testG4Parallel(step)) {
            return true;
        }

        // Ensure the step has a context object and an errors bucket for validation.
        step.context = step.context || {};
        step.context.errors = step.context.errors || {};

        // Locate the real parent container of this step by walking the workflow tree.
        const parent = Utilities.findParentContainer(step, definition.sequence);

        // The top level holds stages, and a Stage holds jobs; both accept a Parallel container.
        const isUnderStage = !!parent && parent.pluginName === 'G4™ Stage';
        const isPlaced = !parent || isUnderStage;

        if (isPlaced) {
            // Remove any previous placement error.
            delete step.context.errors.parallelPlacement;

            // Validation succeeded.
            return true;
        }

        // Name the engine's own rule when the parent is another Parallel container.
        const nestedDescription =
            "A Parallel container cannot be placed directly inside another Parallel container. " +
            "To fix this, move it to the top level, or into a Stage.";
        const misplacedDescription =
            "A Parallel container must be at the top level, where it runs stages, or directly under a " +
            "Stage (G4™ Stage) container, where it runs jobs. To fix this, move it to one of these places.";
        const description = StateMachineSteps.testG4Parallel(parent)
            ? nestedDescription
            : misplacedDescription;

        // Invalid placement: set a user-friendly error.
        step.context.errors.parallelPlacement = {
            title: "Invalid Parallel Placement",
            description,
            level: "ERR"
        };

        // Validation failed.
        return false;
    }

    static confirmRulePlacement(step, definition) {
        // Define types that should be skipped for validation.
        const skipTypes = ['G4™ Stage', 'G4™ Job', 'G4™ Parallel'];

        // 1. Skip validation for Stage, Job, or Parallel steps themselves.
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

        // A Stage is also valid as a lane: directly inside a Parallel container that sits at the top level.
        const isUnderParallel = StateMachineSteps.testG4Parallel(parent);
        const parallelParent = isUnderParallel
            ? Utilities.findParentContainer(parent, definition.sequence)
            : null;
        const isLane = isUnderParallel && !parallelParent;

        // If no parent container was found, or the Stage is a lane, this is a valid placement.
        const isPlaced = !parent || isLane;

        if (isPlaced) {
            // Remove any previous placement error key.
            delete step.context.errors.stagePlacement;

            // Validation succeeded.
            return true;
        }

        // Any other parent → invalid placement. Record a user-friendly error.
        step.context.errors.stagePlacement = {
            title: "Invalid Stage Placement",
            description:
                "A Stage step must be at the top level, or directly under a Parallel container that is at the " +
                "top level. To fix this, move the Stage step to the root sequence, or into a top-level " +
                "Parallel container.",
            level: "ERR"
        };

        // Validation failed.
        return false;
    }

    static confirmStepProperties(step) {
        /**
         * Validates that a required property exists and has a value, logging a descriptive error if not.
         */
        const newPropertyError = (properties, key, propertyType) => {
            const propertyData = properties[key];
            const isRequired = Boolean(propertyData.required);
            const hasValue = propertyData.value != null && propertyData.value !== "";

            // Step is valid — clear any existing error and return success
            if (!isRequired || hasValue) {
                delete step.context.errors[key];
                return null;
            }

            // Step is invalid — set a descriptive, user-friendly error
            const displayName = propertyData?.displayName || propertyData.name;

            // Return an error object with a title and description
            return {
                key: key,
                value: {
                    title: `Missing Required ${propertyType}: ${Utilities.convertPascalToSpaceCase(displayName)}`,
                    description: `The "${displayName}" ${propertyType.toLowerCase()} is required but is currently missing. To fix this, ` +
                        `add the "${displayName}" ${propertyType.toLowerCase() } to your step configuration with a valid value.`,
                    level: "ERR"
                }
            };
        };

        // Define types that should be skipped for validation.
        const skipTypes = ['G4™ Stage', 'G4™ Job', 'G4™ Parallel'];

        // Skip validation for Stage, Job, or Parallel steps.
        if (skipTypes.includes(step.pluginName)) {
            return true;
        }

        // Ensure the step has a context object and an errors bucket for validation.
        step.context = step.context || {};
        step.context.errors = step.context.errors || {};

        // Initialize an object to hold any property errors found during validation.
        const propertyErrors = {};
        const stepProperties = step.properties || {};
        const stepParameters = step.parameters || {};

        // Create an object to hold any errors found during validation.
        for (const key in stepProperties) {
            // Skip validation for certain keys that should not be validated.
            const isArgument = key.toUpperCase() === "ARGUMENT";
            const isParameters = Object.keys(stepParameters).length > 0;
            const isRules = key.toUpperCase() === "RULES";

            if ((isArgument && isParameters) || isRules) {
                continue;
            }

            // Validate each property using the newPropertyError function.
            const propertyError = newPropertyError(stepProperties, key, "Property");

            // If no error was found for this property, continue to the next one.
            if (!propertyError) {
                continue;
            }

            // Add the error to the propertyErrors object and the step context errors.
            propertyErrors[propertyError.key] = propertyError.value;
            step.context.errors[propertyError.key] = propertyError.value;
        }

        // Create an object to hold any errors found during validation.
        for (const key in stepParameters) {
            const propertyError = newPropertyError(stepParameters, key, "Parameter");

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
