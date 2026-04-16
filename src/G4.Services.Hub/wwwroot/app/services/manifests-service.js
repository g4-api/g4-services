/**
 * G4 Studio — Manifests Service.
 * Wraps the Manifests Swagger domain: retrieve G4 plugin manifests.
 *
 * All paths: /api/v4/g4/integration/manifests/...
 */

import { get, post } from './api-client.js';

const BASE = '/api/v4/g4/integration/manifests';

/**
 * Get all plugin manifests.
 * @param {string[]} [expandFields]  Optional field names to include
 * @returns {Promise<IG4PluginManifest[]>}
 */
export async function getAllManifests(expandFields = []) {
    const params = expandFields.length ? { expandFields: expandFields.join(',') } : undefined;
    return get(BASE, params);
}

/**
 * Get manifests from external repositories.
 * @param {G4ExternalRepositoryModel[]} repositories
 * @param {string[]} [expandFields]
 * @returns {Promise<IG4PluginManifest[]>}
 */
export async function getExternalManifests(repositories, expandFields = []) {
    const params = expandFields.length ? { expandFields: expandFields.join(',') } : undefined;
    return post(BASE, repositories, params);
}

/**
 * Get a single manifest by plugin key.
 * @param {string} key
 * @param {string[]} [expandFields]
 * @returns {Promise<IG4PluginManifest>}
 */
export async function getManifestByKey(key, expandFields = []) {
    const params = expandFields.length ? { expandFields: expandFields.join(',') } : undefined;
    return get(`${BASE}/key/${encodeURIComponent(key)}`, params);
}

/**
 * Get a manifest by key from an external repository.
 * @param {string} key
 * @param {G4ExternalRepositoryModel} repository
 * @param {string[]} [expandFields]
 * @returns {Promise<IG4PluginManifest>}
 */
export async function getManifestByKeyAndRepo(key, repository, expandFields = []) {
    const params = expandFields.length ? { expandFields: expandFields.join(',') } : undefined;
    return post(`${BASE}/key/${encodeURIComponent(key)}`, repository, params);
}

/**
 * Get a manifest by plugin type and key.
 * @param {string} pluginType
 * @param {string} key
 * @param {string[]} [expandFields]
 * @returns {Promise<IG4PluginManifest>}
 */
export async function getManifestByTypeAndKey(pluginType, key, expandFields = []) {
    const params = expandFields.length ? { expandFields: expandFields.join(',') } : undefined;
    return get(
        `${BASE}/type/${encodeURIComponent(pluginType)}/key/${encodeURIComponent(key)}`,
        params
    );
}
