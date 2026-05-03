/**
 * G4 Studio — OpenAI Service.
 * Wraps the G4 OpenAI-compatible proxy endpoints.
 *
 * Endpoints:
 *   GET  /api/v4/g4/openai         — Health/status check
 *   GET  /api/v4/g4/openai/models  — List available models
 *   POST /api/v4/g4/openai/chat/completions — Chat completions
 */

import { get, post, request } from './api-client.js';

const BASE = '/api/v4/g4/openai';

/**
 * Check whether the OpenAI proxy is healthy.
 * @returns {Promise<*>}
 */
export async function getProxyStatus() {
    return get(BASE);
}

/**
 * List available G4-proxied models.
 * @returns {Promise<OpenAiModelListResponse>}
 */
export async function listModels() {
    return get(`${BASE}/models`);
}

/**
 * Generate a chat completion (non-streaming).
 * @param {OpenAiChatCompletionRequest} req
 * @returns {Promise<*>}
 */
export async function chatCompletion(req) {
    return post(`${BASE}/chat/completions`, { ...req, stream: false });
}

/**
 * Generate a streaming chat completion.
 * Returns the raw Response so the caller can consume the stream.
 * @param {OpenAiChatCompletionRequest} req
 * @returns {Promise<Response>}
 */
export async function chatCompletionStream(req) {
    const url = `${BASE}/chat/completions`;
    return fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ...req, stream: true }),
    });
}
