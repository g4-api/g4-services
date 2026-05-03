/**
 * G4 Studio — Environments Service.
 * Wraps the Environments Swagger domain: CRUD for environments and their parameters.
 *
 * All paths: /api/v4/g4/environments/...
 */

import { get, put, del, putText } from './api-client.js';

const BASE = '/api/v4/g4/environments';

/**
 * Get all environments with their parameters.
 * Returns: { [environmentName]: { [paramName]: value } }
 * @returns {Promise<object>}
 */
export async function getAllEnvironments() {
    return get(BASE);
}

/**
 * Get details of a specific environment.
 * @param {string} name
 * @param {boolean} [decode=false]  Base64-decode parameter values
 * @returns {Promise<object>}
 */
export async function getEnvironment(name, decode = false) {
    return get(`${BASE}/${encodeURIComponent(name)}`, { decode });
}

/**
 * Create or update an environment with a set of parameters.
 * @param {string} name
 * @param {Record<string, string>} params
 * @param {boolean} [encode=true]  Base64-encode values before storing
 * @returns {Promise<object>}
 */
export async function upsertEnvironment(name, params, encode = true) {
    return put(`${BASE}/${encodeURIComponent(name)}`, params, { encode });
}

/**
 * Delete a specific environment.
 * @param {string} name
 * @returns {Promise<void>}
 */
export async function deleteEnvironment(name) {
    return del(`${BASE}/${encodeURIComponent(name)}`);
}

/** Delete all environments. @returns {Promise<void>} */
export async function clearAllEnvironments() {
    return del(BASE);
}

/**
 * Get a single parameter from an environment.
 * @param {string} environment
 * @param {string} name
 * @param {boolean} [decode=false]
 * @returns {Promise<ParameterResponseModel>}
 */
export async function getParameter(environment, name, decode = false) {
    return get(
        `${BASE}/${encodeURIComponent(environment)}/parameter/${encodeURIComponent(name)}`,
        { decode }
    );
}

/**
 * Create or update a parameter in an environment.
 * @param {string} environment
 * @param {string} name
 * @param {string} value
 * @param {boolean} [encode=true]
 * @returns {Promise<ParameterResponseModel>}
 */
export async function upsertParameter(environment, name, value, encode = true) {
    return putText(
        `${BASE}/${encodeURIComponent(environment)}/parameter/${encodeURIComponent(name)}`,
        value,
        { encode }
    );
}

/**
 * Delete a parameter from an environment.
 * @param {string} environment
 * @param {string} name
 * @returns {Promise<void>}
 */
export async function deleteParameter(environment, name) {
    return del(
        `${BASE}/${encodeURIComponent(environment)}/parameter/${encodeURIComponent(name)}`
    );
}

/**
 * Create or update a system parameter (under the 'SystemParameters' environment).
 * @param {string} name
 * @param {string} value
 * @param {boolean} [encode=true]
 * @returns {Promise<ParameterResponseModel>}
 */
export async function upsertSystemParameter(name, value, encode = true) {
    return putText(
        `${BASE}/parameter/${encodeURIComponent(name)}`,
        value,
        { encode }
    );
}
