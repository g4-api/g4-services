/**
 * G4 Studio — Cache Service.
 * Wraps the Cache Swagger domain: plugin cache, credentials, sync, find tools.
 *
 * All paths: /api/v4/g4/integration/cache/...
 */

import { get, post } from './api-client.js';

const BASE = '/api/v4/g4/integration/cache';

/**
 * Get the full internal plugin cache.
 * Returns a nested dictionary: { [pluginType]: { [pluginName]: G4PluginCacheModel } }
 * @returns {Promise<object>}
 */
export async function getPluginCache() {
    return get(BASE);
}

/**
 * Get the plugin cache from specific external repositories.
 * @param {G4ExternalRepositoryModel[]} repositories
 * @returns {Promise<object>}
 */
export async function getExternalCache(repositories) {
    return post(BASE, repositories);
}

/**
 * Get all saved credential records (from in-memory cache).
 * @returns {Promise<*>}
 */
export async function getAllCredentials() {
    return get(`${BASE}/credentials`);
}

/**
 * Get a credential by id or name.
 * @param {string} idOrName
 * @returns {Promise<*>}
 */
export async function getCredential(idOrName) {
    return get(`${BASE}/credentials/${encodeURIComponent(idOrName)}`);
}

/**
 * Download the full plugin cache as a ZIP archive (Blob).
 * @returns {Promise<Blob>}
 */
export async function downloadCacheDataset() {
    return get(`${BASE}/dataset`);
}

/**
 * Trigger synchronization of the internal plugin cache.
 * @returns {Promise<void>}
 */
export async function syncInternalCache() {
    return get(`${BASE}/sync`);
}

/**
 * Sync the plugin cache with external repositories and MCP servers.
 * @param {CacheSyncModel} syncModel  { repositories: [...], servers: {...} }
 * @returns {Promise<void>}
 */
export async function syncExternalCache(syncModel) {
    return post(`${BASE}/sync`, syncModel);
}

/**
 * Find MCP tools by intent using vector search.
 * @param {string} intent
 * @param {number} [maxResults=10]
 * @param {number} [threshold=0]
 * @returns {Promise<*>}
 */
export async function findTools(intent, maxResults = 10, threshold = 0) {
    return post(`${BASE}/find`, { intent, maxResults, threshold });
}
