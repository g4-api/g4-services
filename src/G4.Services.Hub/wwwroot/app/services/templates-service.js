/**
 * G4 Studio — Templates Service.
 * Wraps the Templates Swagger domain.
 *
 * Endpoints:
 *   GET    /api/v4/g4/templates        — Get all template manifests
 *   PUT    /api/v4/g4/templates        — Add or overwrite a template
 *   DELETE /api/v4/g4/templates        — Clear all templates
 *   GET    /api/v4/g4/templates/{key}  — Get template by key
 *   DELETE /api/v4/g4/templates/{key}  — Remove template by key
 */

import { get, put, del } from './api-client.js';

const BASE = '/api/v4/g4/templates';

/**
 * Get all template manifests.
 * @returns {Promise<G4PluginAttribute[]>}
 */
export async function getAllTemplates() {
    return get(BASE);
}

/**
 * Get a single template manifest by key.
 * @param {string} key
 * @returns {Promise<G4PluginAttribute>}
 */
export async function getTemplate(key) {
    return get(`${BASE}/${encodeURIComponent(key)}`);
}

/**
 * Add or overwrite a template.
 * @param {G4PluginAttribute} manifest
 * @returns {Promise<void>}
 */
export async function upsertTemplate(manifest) {
    return put(BASE, manifest);
}

/**
 * Remove a template by key.
 * @param {string} key
 * @returns {Promise<void>}
 */
export async function deleteTemplate(key) {
    return del(`${BASE}/${encodeURIComponent(key)}`);
}

/**
 * Clear all templates.
 * @returns {Promise<void>}
 */
export async function clearAllTemplates() {
    return del(BASE);
}
