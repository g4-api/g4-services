/*
 * G4(TM) designer plugins: G4DesignerPlugins.register({ name, install }).
 *
 * Lets features attach to the workflow designer without living inside the designer bundle
 * (js/canvas.umd.js) or the canvas page script (js/canvas.js). The host wraps the bundle's public
 * entry point, sequentialWorkflowDesigner.Designer.create, once: every designer the page creates first
 * lets each registered plugin add to its configuration (designer extensions, a preference storage),
 * then hands the created designer to the plugin. Upgrading the bundle never removes a plugin; a plugin
 * only depends on what the bundle exports.
 *
 * Contract:
 * - Load after js/canvas.umd.js and before the script that creates the designer; plugins register
 *   after this file and before the designer is created.
 * - register(plugin): plugin.name names it (and its saved state); plugin.install(configuration) runs
 *   before each designer is created, may change the configuration, and returns the instance for that
 *   designer, or nothing. The instance's attach(designer, stateContext) runs right after the designer is
 *   created; its optional receiveState(state) runs when saved state arrives later (VS Code).
 * - stateContext: readState(documentTitle) and saveState(documentTitle, state) keep a plugin's state for
 *   one automation in the current state storage.
 * - setStateStorage(storage): replaces where state is kept. The default keeps it in the browser's
 *   localStorage, keyed by plugin name and automation title; the VS Code designer replaces it with one
 *   that keeps it in the extension, keyed by the bot file, and calls receiveState when it arrives.
 * - A plugin that throws is reported in the console and skipped, so it cannot stop the designer.
 */
(() => {
    'use strict';

    // Prefix of every localStorage key the default state storage writes.
    const STORAGE_KEY_PREFIX = 'g4-designer-plugins';

    // Stands for an automation without a title in a localStorage key.
    const UNTITLED_DOCUMENT_TITLE = 'untitled';

    // The plugins registered so far, in registration order.
    const plugins = [];

    // The instances of the most recently created designer, so state that arrives later reaches them.
    let installedInstances = [];

    // Where plugin state is kept; replaced by the host page when the browser storage does not persist.
    let stateStorage = newLocalStateStorage();

    // Publish the plugin host.
    globalThis.G4DesignerPlugins = {
        /**
         * Hands state that arrived after the designer was created to the plugin's instance.
         *
         * @param {string} pluginName - The name the plugin registered with.
         * @param {unknown} state - The plugin's saved state for the open automation; null when there is none.
         */
        receiveState(pluginName, state) {
            for (const installedInstance of installedInstances) {
                const isReceiver = installedInstance.name === pluginName
                    && typeof installedInstance.instance.receiveState === 'function';

                if (isReceiver) {
                    invokeSafely(pluginName, () => installedInstance.instance.receiveState(state));
                }
            }
        },

        /**
         * Registers a plugin; it applies to every designer created after this call.
         *
         * @param {{ name: string, install: Function }} plugin - The plugin's name and its install function.
         */
        register(plugin) {
            // A plugin without a name or an install function cannot be loaded, so it is refused loudly.
            const isPlugin = typeof plugin?.name === 'string' && typeof plugin?.install === 'function';

            if (!isPlugin) {
                console.error('G4 designer plugin refused: it needs a name and an install function.');

                return;
            }

            plugins.push(plugin);
        },

        /**
         * Replaces where plugin state is kept.
         *
         * @param {{ readState: Function, saveState: Function }} storage - readState(pluginName, documentTitle)
         *     returns the saved state or null; saveState(pluginName, documentTitle, state) keeps it.
         */
        setStateStorage(storage) {
            stateStorage = storage;
        }
    };

    /**
     * Wraps the designer bundle's Designer.create, so plugins install before and attach after each create.
     *
     * @remarks
     * Owns the single wrap of the bundle's public entry point. When the bundle is missing (loaded in
     * the wrong order), the host reports it and leaves the page as it is.
     */
    function initializeDesignerPlugins() {
        // The bundle must already be loaded; without it there is nothing to wrap.
        const designerClass = globalThis.sequentialWorkflowDesigner?.Designer;

        if (typeof designerClass?.create !== 'function') {
            console.error('G4 designer plugins: load canvas.umd.js before g4-designer-plugins.js.');

            return;
        }

        // Keep the bundle's own create; the wrapper calls it between the plugin steps.
        const createDesigner = designerClass.create;

        designerClass.create = function (placeholder, startDefinition, configuration) {
            // Let each plugin change the configuration and keep the instance it returns for this designer.
            const instances = [];

            for (const plugin of plugins) {
                const instance = invokeSafely(plugin.name, () => plugin.install(configuration));

                if (instance) {
                    instances.push({ instance, name: plugin.name });
                }
            }

            // Create the designer exactly as the page asked, with the configuration the plugins extended.
            const designer = createDesigner.call(this, placeholder, startDefinition, configuration);

            // Hand the designer to each instance, with access to its own saved state.
            for (const installedInstance of instances) {
                if (typeof installedInstance.instance.attach !== 'function') {
                    continue;
                }

                const stateContext = newStateContext(installedInstance.name);

                invokeSafely(installedInstance.name, () => installedInstance.instance.attach(designer, stateContext));
            }

            // Remember the instances, so state that arrives later reaches this designer's plugins.
            installedInstances = instances;

            return designer;
        };
    }

    /**
     * Runs one plugin step, reporting a failure instead of letting it stop the designer.
     *
     * @param {string} pluginName - The plugin the step belongs to, named in the report.
     * @param {Function} action - The plugin step.
     * @returns {unknown} The step's result; undefined when it failed.
     */
    function invokeSafely(pluginName, action) {
        try {
            return action();
        } catch (error) {
            console.error(`G4 designer plugin '${pluginName}' failed and was skipped.`, error);

            return undefined;
        }
    }

    /**
     * Creates the default state storage, which keeps each plugin's state in the browser's localStorage.
     *
     * @remarks
     * Compute-only factory. Storage that is blocked or full (a private window, cleared site data) is
     * treated as empty, so the plugins keep working without saved state.
     *
     * @returns {{ readState: Function, saveState: Function }} The storage.
     */
    function newLocalStateStorage() {
        // One key per plugin and automation title.
        const getStorageKey = (pluginName, documentTitle) => {
            const title = documentTitle || UNTITLED_DOCUMENT_TITLE;

            return `${STORAGE_KEY_PREFIX}:${pluginName}:${title}`;
        };

        return {
            readState(pluginName, documentTitle) {
                // Read and parse the saved state; anything unreadable counts as no state.
                try {
                    const text = localStorage.getItem(getStorageKey(pluginName, documentTitle));

                    return text === null ? null : JSON.parse(text);
                } catch {
                    return null;
                }
            },

            saveState(pluginName, documentTitle, state) {
                // Keep the state, or remove the key when there is nothing left to keep.
                try {
                    const storageKey = getStorageKey(pluginName, documentTitle);

                    if (state === null || state === undefined) {
                        localStorage.removeItem(storageKey);

                        return;
                    }

                    localStorage.setItem(storageKey, JSON.stringify(state));
                } catch (error) {
                    console.warn(`G4 designer plugin '${pluginName}' could not save its state.`, error);
                }
            }
        };
    }

    /**
     * Creates the state access one plugin instance gets.
     *
     * @remarks
     * Resolves the storage on every call rather than once, so a storage the host page sets after the
     * designer was created (VS Code) still applies.
     *
     * @param {string} pluginName - The plugin whose state the context reads and saves.
     * @returns {{ readState: Function, saveState: Function }} The plugin's state context.
     */
    function newStateContext(pluginName) {
        return {
            readState: (documentTitle) => stateStorage.readState(pluginName, documentTitle),
            saveState: (documentTitle, state) => stateStorage.saveState(pluginName, documentTitle, state)
        };
    }

    // Wrap the designer's entry point once, so every designer the page creates loads the plugins.
    initializeDesignerPlugins();
})();
