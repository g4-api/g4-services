/* ============================================================
   G4 Cache Entries — one entry per cached plugin
   Shared by the designer (canvas.html) and the Capabilities page (capabilities.html).
   ============================================================ */

'use strict';

// Separates the namespace from the key in a qualified plugin name, as the engine writes cache keys.
const CACHE_NAMESPACE_SEPARATOR = ':';

// Names the namespace the engine assigns to a plugin whose manifest declares none.
const CACHE_SYSTEM_NAMESPACE = 'G4.System';

/**
 * Gets one entry per plugin in one plugin-type bucket of the integration cache.
 *
 * @remarks
 * The engine stores each plugin under several lookup names: its qualified `Namespace:Key` name, its
 * bare key when no other namespace claims that key, and its aliases. A list that enumerates the
 * bucket directly therefore shows one plugin once per name. This helper groups the lookup names by
 * plugin identity (namespace and key) and returns each plugin once.
 *
 * The entry name is the shortest name that still resolves to the plugin: the bare key when the bucket
 * maps that key to this plugin, otherwise the qualified name, because several namespaces share the
 * key. Either name works as a rule's plugin name and as a lookup key in the same bucket.
 *
 * The helper is compute-only: it reads the bucket and never changes it.
 *
 * @param {Object<string, object> | null | undefined} bucket - One plugin-type bucket: lookup name to
 *     cache model, as `GET /api/v4/g4/integration/cache` returns it.
 * @returns {Array<{ name: string, model: object }>} One entry per plugin, in first-seen order.
 */
function getCacheEntries(bucket) {
    // Treat a missing bucket as empty so a plugin type the Hub rejected cannot break the calling page.
    const lookupEntries = Object.entries(bucket ?? {});
    const plugins = new Map();

    // Group the lookup names by plugin identity so qualified names and aliases collapse into one entry.
    for (const [lookupName, model] of lookupEntries) {
        const identity = getCachePluginIdentity(model, lookupName);

        if (!plugins.has(identity.identityKey)) {
            plugins.set(identity.identityKey, { identity, model });
        }
    }

    // Name each plugin by its bare key when the bucket maps that key to it, otherwise by its qualified name.
    return [...plugins.values()].map(({ identity, model }) => {
        const bareModel = bucket[identity.key];
        const isBareKeyPresent = bareModel !== null && bareModel !== undefined;
        const isBareKeyOwned = isBareKeyPresent
            && getCachePluginIdentity(bareModel, identity.key).identityKey === identity.identityKey;

        const name = isBareKeyOwned
            ? identity.key
            : identity.qualifiedName;

        return { name, model };
    });
}

/**
 * Gets the identity of one cached plugin: its key, its qualified name, and a case-insensitive
 * grouping key.
 *
 * @remarks
 * The identity comes from the manifest, because every lookup name of a plugin carries the same
 * manifest. A model without a manifest key falls back to its lookup name, so a malformed entry is
 * still listed once instead of being dropped. The engine compares names case-insensitively, so the
 * grouping key is lower-case. Compute-only.
 *
 * @param {object} model - The cache model stored under the lookup name.
 * @param {string} lookupName - The bucket key the model was read from.
 * @returns {{ identityKey: string, key: string, qualifiedName: string }} The plugin identity.
 */
function getCachePluginIdentity(model, lookupName) {
    // Read the identity from the manifest; fall back to the lookup name when the manifest has no key.
    const manifest = model?.manifest ?? {};
    const isKeyAuthored = typeof manifest.key === 'string' && manifest.key !== '';

    if (!isKeyAuthored) {
        return { identityKey: lookupName.toLowerCase(), key: lookupName, qualifiedName: lookupName };
    }

    // Apply the engine's namespace rule: an empty namespace means the system namespace.
    const isNamespaceAuthored = typeof manifest.namespace === 'string' && manifest.namespace.trim() !== '';
    const namespace = isNamespaceAuthored
        ? manifest.namespace.trim()
        : CACHE_SYSTEM_NAMESPACE;
    const qualifiedName = `${namespace}${CACHE_NAMESPACE_SEPARATOR}${manifest.key}`;

    return { identityKey: qualifiedName.toLowerCase(), key: manifest.key, qualifiedName };
}
