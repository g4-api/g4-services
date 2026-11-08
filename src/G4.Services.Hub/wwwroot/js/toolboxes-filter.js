/* ============================================================
   G4 Toolboxes Filter — which toolboxes and tools the designer toolbox shows
   Loaded by canvas.html before the designer library; read by the library's toolbox patch.
   ============================================================ */

'use strict';

// Names the browser cookie that keeps the user's filter between designer sessions.
const TOOLBOXES_FILTER_COOKIE_NAME = 'g4-toolboxes-filter';

// Keeps the cookie for one year, so the filter survives restarts without becoming permanent.
const TOOLBOXES_FILTER_COOKIE_MAXIMUM_AGE_SECONDS = 365 * 24 * 60 * 60;

// Caps the encoded cookie below the common 4 KB browser limit, so a save never silently drops entries.
const TOOLBOXES_FILTER_COOKIE_MAXIMUM_LENGTH = 4000;

// Names the toolbox group that holds Stage and Job; it is always shown.
const TOOLBOXES_FILTER_CONTAINERS_GROUP = 'Containers';

// Names the document event the designer toolbox listens to, to redraw after a filter change.
const TOOLBOXES_FILTER_CHANGED_EVENT = 'g4:toolboxes-filter-changed';

// Names the namespace the engine assigns to a plugin whose manifest declares none.
const TOOLBOXES_FILTER_SYSTEM_NAMESPACE = 'G4.System';

/**
 * Holds the filter state shared by the designer page, its toolbox patch, and the filter dropdown.
 *
 * @remarks
 * Module-level state is intentional: the designer library, canvas.js, and the dropdown are separate
 * classic scripts that must read one filter. `manifestFilter` is the project manifest's filter that
 * VS Code sends; it stays empty in the browser. `stepManifests` maps each toolbox step to the manifest
 * it was built from, because a toolbox step carries only the plugin key and not its namespace.
 */
const toolboxesFilterState = {
    manifestFilter: { toolboxes: [], tools: [] },
    stepManifests: new WeakMap()
};

/**
 * Converts any value into a filter that holds only complete entries.
 *
 * @remarks
 * Compute-only. An entry counts only with a non-empty string name and a boolean `enabled`, so a
 * malformed cookie or manifest can never hide a tool by accident.
 *
 * @param {unknown} value - A parsed cookie, a manifest `toolboxesFilter`, or anything else.
 * @returns {{ toolboxes: Array<{ name: string, enabled: boolean }>, tools: Array<{ name: string, enabled: boolean }> }}
 *     The filter with only its complete entries.
 */
function convertToToolboxesFilter(value) {
    // Keep only complete entries of one list, so each list is checked the same way.
    const getEntries = (entries) => {
        if (!Array.isArray(entries)) {
            return [];
        }

        return entries
            .filter((entry) => {
                const isNamed = typeof entry?.name === 'string' && entry.name.trim() !== '';
                const isDecided = typeof entry?.enabled === 'boolean';

                return isNamed && isDecided;
            })
            .map((entry) => ({ name: entry.name.trim(), enabled: entry.enabled }));
    };

    return {
        toolboxes: getEntries(value?.toolboxes),
        tools: getEntries(value?.tools)
    };
}

/**
 * Gets the filter the designer applies: the cookie entries followed by the manifest entries.
 *
 * @remarks
 * The last entry for a name wins, so placing the manifest entries last lets the manifest override the
 * cookie entry by entry, while cookie-only entries still apply.
 *
 * @returns {{ toolboxes: Array<{ name: string, enabled: boolean }>, tools: Array<{ name: string, enabled: boolean }> }}
 *     The merged filter.
 */
function getToolboxesFilter() {
    // Read both sources; the cookie is the user's choice and the manifest is the project's.
    const cookieFilter = readToolboxesFilterCookie();
    const manifestFilter = toolboxesFilterState.manifestFilter;

    return {
        toolboxes: [...cookieFilter.toolboxes, ...manifestFilter.toolboxes],
        tools: [...cookieFilter.tools, ...manifestFilter.tools]
    };
}

/**
 * Gets a copy of the project manifest's filter that VS Code sent; empty in the browser.
 *
 * @remarks
 * The dropdown uses it to say how many entries the manifest decides; the copy keeps the state private.
 *
 * @returns {{ toolboxes: Array<{ name: string, enabled: boolean }>, tools: Array<{ name: string, enabled: boolean }> }}
 *     The manifest filter.
 */
function getToolboxesFilterManifest() {
    return structuredClone(toolboxesFilterState.manifestFilter);
}

/**
 * Gets the manifest entry for a toolbox or tool name, when the project manifest sets one.
 *
 * @remarks
 * The dropdown uses it to lock entries the manifest decides, because a cookie entry could not change them.
 *
 * @param {'toolboxes' | 'tools'} listName - The filter list to search.
 * @param {string} name - The toolbox name or the qualified tool name.
 * @returns {{ name: string, enabled: boolean } | undefined} The last manifest entry for the name.
 */
function getToolboxesFilterManifestEntry(listName, name) {
    // Match names the same way the decision rules do, so the lock follows the entry that decides.
    const testName = listName === 'toolboxes'
        ? (entryName) => testToolboxName(entryName, name)
        : (entryName) => testToolName(entryName, name);

    return toolboxesFilterState.manifestFilter[listName].findLast((entry) => testName(entry.name));
}

/**
 * Gets the qualified Namespace:Key name of a manifest, as the engine writes it.
 *
 * @param {{ key?: string, namespace?: string }} manifest - The plugin manifest.
 * @returns {string} The qualified name; an empty namespace becomes G4.System.
 */
function getToolboxesFilterQualifiedName(manifest) {
    // Apply the engine's namespace rule, so the name matches the qualified names users write.
    const isNamespaceAuthored = typeof manifest?.namespace === 'string' && manifest.namespace.trim() !== '';
    const namespace = isNamespaceAuthored
        ? manifest.namespace.trim()
        : TOOLBOXES_FILTER_SYSTEM_NAMESPACE;

    return `${namespace}:${manifest?.key ?? ''}`;
}

/**
 * Reads the filter the user saved in the browser cookie.
 *
 * @remarks
 * A missing, unreadable, or malformed cookie reads as an empty filter, so everything stays shown.
 *
 * @returns {{ toolboxes: Array<{ name: string, enabled: boolean }>, tools: Array<{ name: string, enabled: boolean }> }}
 *     The saved filter with only its complete entries.
 */
function readToolboxesFilterCookie() {
    // Find the filter cookie among the page cookies.
    const cookiePrefix = `${TOOLBOXES_FILTER_COOKIE_NAME}=`;
    const cookie = document.cookie
        .split(';')
        .map((part) => part.trim())
        .find((part) => part.startsWith(cookiePrefix));

    if (!cookie) {
        return convertToToolboxesFilter(undefined);
    }

    // Decode and parse it; a broken value falls back to the empty filter instead of failing the page.
    try {
        const value = JSON.parse(decodeURIComponent(cookie.slice(cookiePrefix.length)));

        return convertToToolboxesFilter(value);
    } catch {
        return convertToToolboxesFilter(undefined);
    }
}

/**
 * Registers the manifest a toolbox step was built from, so the filter can resolve its namespace.
 *
 * @param {object} step - The toolbox step object the designer receives.
 * @param {object} manifest - The plugin manifest the step was built from.
 */
function registerToolboxesFilterStep(step, manifest) {
    // Keep the link weakly, so a step the designer drops can be collected.
    toolboxesFilterState.stepManifests.set(step, manifest);
}

/**
 * Saves the user's filter in the browser cookie and redraws the toolbox.
 *
 * @remarks
 * Only complete entries are saved. A filter whose encoded value exceeds the cookie limit is not saved,
 * and the caller reports the failure, rather than the browser dropping part of it.
 *
 * @param {unknown} filter - The filter to save.
 * @returns {boolean} `true` when the cookie was saved; `false` when it was too large.
 */
function saveToolboxesFilterCookie(filter) {
    // Encode only complete entries, then refuse a value the browser would truncate.
    const value = encodeURIComponent(JSON.stringify(convertToToolboxesFilter(filter)));
    const isTooLarge = value.length > TOOLBOXES_FILTER_COOKIE_MAXIMUM_LENGTH;

    if (isTooLarge) {
        return false;
    }

    // Write the cookie for the whole Hub, then redraw the toolbox with the saved filter.
    const cookieAttributes = `path=/; max-age=${TOOLBOXES_FILTER_COOKIE_MAXIMUM_AGE_SECONDS}; SameSite=Lax`;
    document.cookie = `${TOOLBOXES_FILTER_COOKIE_NAME}=${value}; ${cookieAttributes}`;

    sendToolboxesFilterChanged();

    return true;
}

/**
 * Selects the toolbox groups and items the filter shows, before the toolbox search text applies.
 *
 * @remarks
 * Compute-only over the given groups. The Containers group is always shown. An item whose step has no
 * registered manifest is shown, and a group left without items is removed.
 *
 * @param {Array<{ name: string, items: Array<{ step: object }> }>} groups - The toolbox groups.
 * @returns {Array<{ name: string, items: Array<{ step: object }> }>} The groups the filter shows.
 */
function selectToolboxesFilterGroups(groups) {
    // Read the merged filter once for the whole toolbox.
    const filter = getToolboxesFilter();

    // Keep each item its group shows, then drop the groups that end up empty.
    return groups
        .map((group) => {
            const isContainers = group.name === TOOLBOXES_FILTER_CONTAINERS_GROUP;
            const items = isContainers
                ? group.items
                : group.items.filter((item) => testToolboxesFilterItem({
                    filter,
                    groupName: group.name,
                    step: item.step
                }));

            return { ...group, items };
        })
        .filter((group) => group.items.length > 0);
}

/**
 * Sets the project manifest's filter that VS Code sends, and redraws the toolbox.
 *
 * @param {unknown} filter - The manifest `toolboxesFilter`; anything malformed reads as empty.
 */
function setToolboxesFilterManifest(filter) {
    // Keep only complete entries, then redraw so the manifest applies live.
    toolboxesFilterState.manifestFilter = convertToToolboxesFilter(filter);

    sendToolboxesFilterChanged();
}

/**
 * Determines whether the filter shows one tool in one toolbox.
 *
 * @remarks
 * Compute-only. The first matching rule decides: the tool entry with the qualified name, the tool entry
 * with the bare key, the toolbox entry for the group, and otherwise the tool is shown. The last entry for
 * a name wins. The server's filtered endpoints apply the same rules.
 *
 * @param {{ filter: object, groupName: string, manifest: object }} options - The merged filter, the toolbox
 *     name, and the plugin manifest.
 * @returns {boolean} `true` when the tool is shown in the toolbox.
 */
function testToolboxesFilterTool(options) {
    const { filter, groupName, manifest } = options;

    // Apply the tool entries first; the qualified name names this plugin only, so it beats the bare key.
    const qualifiedName = getToolboxesFilterQualifiedName(manifest);
    const qualifiedEntry = filter.tools.findLast((entry) => testToolName(entry.name, qualifiedName));

    if (qualifiedEntry) {
        return qualifiedEntry.enabled;
    }

    const bareEntry = filter.tools.findLast((entry) => testToolName(entry.name, manifest?.key ?? ''));

    if (bareEntry) {
        return bareEntry.enabled;
    }

    // Apply the toolbox entry for the group; an unlisted toolbox is shown.
    return testToolboxesFilterToolbox(filter, groupName);
}

/**
 * Determines whether the filter shows one toolbox, by its last toolbox entry.
 *
 * @remarks
 * Compute-only. An unlisted toolbox is shown. Tool entries can still show single tools of a hidden
 * toolbox; this answers only for the toolbox entry itself.
 *
 * @param {{ toolboxes: Array<{ name: string, enabled: boolean }> }} filter - The merged filter.
 * @param {string} toolboxName - The toolbox name.
 * @returns {boolean} `true` when the toolbox entry shows the toolbox, or there is none.
 */
function testToolboxesFilterToolbox(filter, toolboxName) {
    // Use the last entry for the name, so a later source overrides an earlier one.
    const toolboxEntry = filter.toolboxes.findLast((entry) => testToolboxName(entry.name, toolboxName));

    return toolboxEntry ? toolboxEntry.enabled : true;
}

/**
 * Asks the designer toolbox to redraw with the current filter.
 */
function sendToolboxesFilterChanged() {
    // The toolbox patch listens on the document, as it reacts to typing in the search box.
    document.dispatchEvent(new CustomEvent(TOOLBOXES_FILTER_CHANGED_EVENT));
}

/**
 * Determines whether the filter shows one toolbox item.
 *
 * @param {{ filter: object, groupName: string, step: object }} options - The merged filter, the toolbox
 *     name, and the item's step.
 * @returns {boolean} `true` when the item is shown.
 */
function testToolboxesFilterItem(options) {
    const { filter, groupName, step } = options;

    // Show an item without a registered manifest, because only a plugin can be named by a filter entry.
    const manifest = toolboxesFilterState.stepManifests.get(step);

    if (!manifest) {
        return true;
    }

    return testToolboxesFilterTool({ filter, groupName, manifest });
}

/**
 * Compares toolbox names ignoring case and white space, so "UiControls" matches "Ui Controls".
 *
 * @param {string} name - The entry name.
 * @param {string} toolboxName - The toolbox or category name.
 * @returns {boolean} `true` when both name the same toolbox.
 */
function testToolboxName(name, toolboxName) {
    // Remove all white space, then compare without case.
    const normalizedName = name.replaceAll(/\s/g, '').toLowerCase();
    const normalizedToolboxName = toolboxName.replaceAll(/\s/g, '').toLowerCase();

    return normalizedName === normalizedToolboxName;
}

/**
 * Compares tool names ignoring case.
 *
 * @param {string} name - The entry name.
 * @param {string} toolName - The qualified name or bare key of a tool.
 * @returns {boolean} `true` when both name the same tool.
 */
function testToolName(name, toolName) {
    return name.trim().toLowerCase() === toolName.toLowerCase();
}

// Publish one entry point for the designer library patch and the other page scripts.
globalThis.G4ToolboxesFilter = {
    changedEventName: TOOLBOXES_FILTER_CHANGED_EVENT,
    containersGroupName: TOOLBOXES_FILTER_CONTAINERS_GROUP,
    convertToToolboxesFilter,
    getToolboxesFilter,
    getToolboxesFilterManifest,
    getToolboxesFilterManifestEntry,
    getToolboxesFilterQualifiedName,
    readToolboxesFilterCookie,
    registerToolboxesFilterStep,
    saveToolboxesFilterCookie,
    selectToolboxesFilterGroups,
    setToolboxesFilterManifest,
    testToolboxesFilterTool,
    testToolboxesFilterToolbox
};
