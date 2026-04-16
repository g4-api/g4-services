/**
 * G4 Studio — Documents Service.
 * Wraps the Documents Swagger domain: plugin Markdown documentation.
 *
 * All paths: /api/v4/g4/integration/documents/...
 */

import { get, post } from './api-client.js';

const BASE = '/api/v4/g4/integration/documents';

/**
 * Get plugin Markdown documentation by plugin name (key).
 * @param {string} key  Plugin name
 * @returns {Promise<string>}
 */
export async function getDocumentByKey(key) {
    return get(`${BASE}/key/${encodeURIComponent(key)}`);
}

/**
 * Get plugin Markdown documentation by key from an external repository.
 * @param {string} key
 * @param {G4ExternalRepositoryModel} repository
 * @returns {Promise<string>}
 */
export async function getDocumentByKeyAndRepo(key, repository) {
    return post(`${BASE}/key/${encodeURIComponent(key)}`, repository);
}

/**
 * Get plugin Markdown documentation by plugin type and key.
 * @param {string} pluginType
 * @param {string} key
 * @returns {Promise<string>}
 */
export async function getDocumentByTypeAndKey(pluginType, key) {
    return get(`${BASE}/type/${encodeURIComponent(pluginType)}/key/${encodeURIComponent(key)}`);
}

/**
 * Get plugin Markdown documentation by type, key, and external repository.
 * @param {string} pluginType
 * @param {string} key
 * @param {G4ExternalRepositoryModel} repository
 * @returns {Promise<string>}
 */
export async function getDocumentByTypeKeyAndRepo(pluginType, key, repository) {
    return post(
        `${BASE}/type/${encodeURIComponent(pluginType)}/key/${encodeURIComponent(key)}`,
        repository
    );
}
