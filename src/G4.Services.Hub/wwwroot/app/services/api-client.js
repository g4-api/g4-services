/**
 * G4 Studio — Centralized API Client.
 * All HTTP communication to the G4 backend flows through this module.
 * Handles base URL, headers, JSON encoding/decoding, and normalized error handling.
 * Service modules call these helpers — pages never call fetch() directly.
 */

import { API_BASE } from '../utils/constants.js';

/**
 * Normalized API error. Carries HTTP status and the parsed error body.
 */
export class ApiError extends Error {
    /**
     * @param {number} status
     * @param {string} message
     * @param {object|null} body
     */
    constructor(status, message, body = null) {
        super(message);
        this.name = 'ApiError';
        this.status = status;
        this.body = body;
    }
}

/**
 * Build a full URL from a path, resolving against API_BASE.
 * Falls back to the current page origin when API_BASE is not set
 * (same-origin deployments where the .NET app serves both the UI and API).
 * @param {string} path
 * @param {Record<string, string|number|boolean>} [params]
 * @returns {string}
 */
function buildUrl(path, params) {
    const base = (API_BASE || window.location.origin) + '/';
    const url = new URL(path, base);
    if (params) {
        for (const [k, v] of Object.entries(params)) {
            if (v !== undefined && v !== null) {
                url.searchParams.set(k, String(v));
            }
        }
    }
    return url.toString();
}

/**
 * Default headers for JSON requests.
 * @returns {Record<string, string>}
 */
function defaultHeaders() {
    return {
        'Content-Type': 'application/json',
        'Accept':       'application/json',
    };
}

/**
 * Parse the response based on Content-Type.
 * @param {Response} res
 * @returns {Promise<*>}
 */
async function parseResponse(res) {
    const ct = res.headers.get('Content-Type') || '';
    if (ct.includes('application/json')) return res.json();
    if (ct.includes('text/markdown'))     return res.text();
    if (ct.includes('text/plain'))        return res.text();
    if (ct.includes('application/zip'))   return res.blob();
    // Empty body (204 etc.)
    const text = await res.text();
    return text || null;
}

/**
 * Core request executor.
 * @param {'GET'|'POST'|'PUT'|'DELETE'|'PATCH'} method
 * @param {string} path
 * @param {object} [options]
 * @param {object} [options.params]   URL query params
 * @param {*}      [options.body]     Request body (will be JSON-serialized)
 * @param {Record<string,string>} [options.headers]  Extra headers
 * @param {string} [options.contentType]  Override Content-Type
 * @returns {Promise<*>}
 */
export async function request(method, path, options = {}) {
    const { params, body, headers: extraHeaders = {}, contentType } = options;

    const url = buildUrl(path, params);
    const headers = { ...defaultHeaders(), ...extraHeaders };

    if (contentType) headers['Content-Type'] = contentType;

    const init = {
        method,
        headers,
    };

    if (body !== undefined && body !== null) {
        if (typeof body === 'string') {
            init.body = body;
        } else {
            init.body = JSON.stringify(body);
        }
    }

    let res;
    try {
        res = await fetch(url, init);
    } catch (networkErr) {
        throw new ApiError(0, `Network error: ${networkErr.message}`, null);
    }

    if (!res.ok) {
        let errorBody = null;
        try { errorBody = await res.json(); } catch { /* not JSON */ }
        const message =
            errorBody?.detail ||
            errorBody?.title ||
            errorBody?.message ||
            `HTTP ${res.status} ${res.statusText}`;
        throw new ApiError(res.status, message, errorBody);
    }

    return parseResponse(res);
}

/** Convenience wrappers */
export const get    = (path, params, headers) => request('GET',    path, { params, headers });
export const post   = (path, body,   params)  => request('POST',   path, { body, params });
export const put    = (path, body,   params)  => request('PUT',    path, { body, params });
export const del    = (path, body,   params)  => request('DELETE', path, { body, params });
export const patch  = (path, body,   params)  => request('PATCH',  path, { body, params });

/**
 * POST with a plain-text body (used by environments parameter endpoints).
 * @param {string} path
 * @param {string} text
 * @param {object} [params]
 * @returns {Promise<*>}
 */
export function postText(path, text, params) {
    return request('POST', path, { body: text, params, contentType: 'text/plain' });
}

/**
 * PUT with a plain-text body.
 * @param {string} path
 * @param {string} text
 * @param {object} [params]
 * @returns {Promise<*>}
 */
export function putText(path, text, params) {
    return request('PUT', path, { body: text, params, contentType: 'text/plain' });
}
