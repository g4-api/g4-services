class Validators {
    /**
     * Validates that a Content rule is correctly placed inside an ExportData container.
     *
     * If the rule is misplaced, adds a structured error message to `step.context.errors.isContainer`.
     *
     * @param {Object} step - The step to validate.
     * @param {Object} definition - The root definition containing the full step sequence.
     */
    static assertIsContainer(step, definition) {
        // Ensure step has a context and error bucket initialized
        step.context = step.context || {};
        step.context.errors = step.context.errors || {};

        // Try to find the direct parent container of this step
        const parent = Utilities.findParentContainer(step, definition.sequence);

        // Cache the step element for potential UI updates
        const stepElement = document.querySelector(`[data-step-id="${step.id}"]>rect`);

        // Check if the parent is a valid container (must be of type 'ExportData')
        const isContainer = parent?.pluginName === 'ExportData';

        // Step is valid — remove any existing error related to placement
        if (isContainer) {
            // Clear any previous error messages related to container placement
            delete step.context.errors.isContainer;

            // If the step element exists, remove any title attribute used for error display
            stepElement.removeAttribute("title");

            // Return true to indicate validation success
            return true;
        }

        // Step is invalid — set a descriptive, user-friendly error
        step.context.errors.isContainer = {
            title: "Invalid Rule Placement",
            description: "The Content rule must be placed inside an Export (Extraction) rule. To fix this, " +
                "move the Content rule under a parent step that uses the ExportData plugin. This ensures it functions correctly within the extraction flow.",
            level: "ERR"
        };

        // Update the step element with the error message for user feedback
        stepElement.setAttribute("title", step.context.errors.isContainer.description);

        // Return false to indicate validation failure
        return false;
    }
}