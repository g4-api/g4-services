/*
 * G4(TM) template loader for the canvas components.
 *
 * Every component keeps its markup in its own <component>/<component>.html file of <template>
 * elements. This loader fetches those files once and adds their templates to the page, so the
 * components can clone them.
 *
 * Contract:
 * - Load it with a <script> tag before the component scripts.
 * - window.G4Templates.ready: a promise that settles when every template file was added or failed.
 *   A file that fails is logged; the other components still load.
 *
 * The file addresses are resolved from this script's own address, not from the page: inside the
 * VS Code Workflow Editor the page is served from a different origin than its resources, and only
 * the <script> and <link> addresses are rewritten for it.
 */
(() => {
    'use strict';

    // Components whose template files are loaded, in the order they are added to the page.
    const COMPONENT_NAMES = ['g4-modal', 'g4-line-numbers', 'g4-expand-editor', 'g4-text-box'];

    // This script's address; every template file sits in a folder next to it.
    const BASE_URL = document.currentScript.src;

    /**
     * Reads one component's template file.
     *
     * @param {string} componentName - Component name; the file is <name>/<name>.html.
     * @returns {Promise<string>} The file's HTML.
     */
    const readTemplateFile = async (componentName) => {
        const url = new URL(`${componentName}/${componentName}.html`, BASE_URL);
        const response = await fetch(url);

        // A missing file is an error the page must report, not empty markup.
        if (!response.ok) {
            throw new Error(`${url} answered ${response.status} ${response.statusText}`);
        }

        return response.text();
    };

    /**
     * Fetches every template file and adds the templates to the end of the page.
     *
     * @returns {Promise<void>} Settles when every file was added or failed.
     */
    const addTemplates = async () => {
        // Fetch in parallel; one failed file must not keep the others out.
        const results = await Promise.allSettled(COMPONENT_NAMES.map(readTemplateFile));

        // Add each loaded file in order, and log each failure with its component.
        results.forEach((result, index) => {
            if (result.status === 'rejected') {
                console.error(`G4 templates: ${COMPONENT_NAMES[index]} could not be loaded.`, result.reason);
                return;
            }

            document.body.insertAdjacentHTML('beforeend', result.value);
        });
    };

    window.G4Templates = { ready: addTemplates() };
})();
