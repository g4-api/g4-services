/**
 * G4 Studio — Swagger Loader.
 * Fetches and caches OpenAPI documents from the G4 backend.
 * Used for introspection and service scaffolding during development.
 * In production, the documents are still loaded to support dynamic features.
 */

import { SWAGGER_DOCS } from '../utils/constants.js';

/** @type {Map<string, object>} Cache of fetched documents */
const _cache = new Map();

/**
 * Fetch and cache a Swagger document by name.
 * @param {string} name  Key from SWAGGER_DOCS (e.g. 'automation')
 * @returns {Promise<object>} Parsed OpenAPI document
 */
export async function loadSwaggerDoc(name) {
    if (_cache.has(name)) return _cache.get(name);

    const url = SWAGGER_DOCS[name];
    if (!url) throw new Error(`[swagger-loader] Unknown Swagger doc name: "${name}"`);

    const res = await fetch(url);
    if (!res.ok) throw new Error(`[swagger-loader] Failed to load "${name}": HTTP ${res.status}`);

    const doc = await res.json();
    _cache.set(name, doc);
    return doc;
}

/**
 * Fetch all Swagger documents. Returns a record keyed by name.
 * Failed individual fetches are captured as Error objects (not thrown).
 * @returns {Promise<Record<string, object|Error>>}
 */
export async function loadAllSwaggerDocs() {
    const entries = await Promise.all(
        Object.entries(SWAGGER_DOCS).map(async ([name, url]) => {
            try {
                const doc = await loadSwaggerDoc(name);
                return [name, doc];
            } catch (err) {
                return [name, err];
            }
        })
    );
    return Object.fromEntries(entries);
}

/**
 * Get a list of all path+method pairs from a cached document.
 * Useful for diagnostic / developer mode views.
 * @param {string} name
 * @returns {Array<{method: string, path: string, summary: string}>}
 */
export async function listEndpoints(name) {
    const doc = await loadSwaggerDoc(name);
    const result = [];
    for (const [path, methods] of Object.entries(doc.paths || {})) {
        for (const [method, op] of Object.entries(methods)) {
            result.push({
                method: method.toUpperCase(),
                path,
                summary: op.summary || '',
                operationId: op.operationId || '',
                tags: op.tags || [],
            });
        }
    }
    return result;
}

/**
 * Clear the document cache (e.g. after a cache sync).
 */
export function clearSwaggerCache() {
    _cache.clear();
}
