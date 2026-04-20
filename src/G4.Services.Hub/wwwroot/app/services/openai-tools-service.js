/**
 * G4 Studio — OpenAI Tools Service.
 * Wraps the G4 AI Tools endpoints (MCP tool registry, session management).
 *
 * All paths: /api/v4/g4/tools/...
 */

import { get, post } from './api-client.js';

const BASE = '/api/v4/g4/tools';

/**
 * Get instructions for a policy.
 * @param {string} [policy]  Defaults to 'default' if omitted
 * @returns {Promise<ToolOutputSchema>}
 */
export async function getInstructions(policy) {
    const params = policy ? { policy } : undefined;
    return get(`${BASE}/get_instructions`, params);
}

/**
 * Find a tool by name or intent.
 * @param {object} input  { toolName?, intent?, id? }
 * @returns {Promise<ToolOutputSchema>}
 */
export async function findTool(input) {
    return post(`${BASE}/find_tool`, input);
}

/**
 * Get all available tools, optionally filtered.
 * @param {object} [filter]  { intent?, toolTypes? }
 * @returns {Promise<ToolOutputSchema>}
 */
export async function getTools(filter = {}) {
    return post(`${BASE}/get_tools`, filter);
}

/**
 * Get the DOM of a current driver session.
 * @param {object} input  { driverSession, token, id? }
 * @returns {Promise<ToolOutputSchema>}
 */
export async function getApplicationDom(input) {
    return post(`${BASE}/get_application_dom`, input);
}

/**
 * Resolve a locator expression in a driver session.
 * @param {object} input  { driverSession, intent, token, openai_* optional }
 * @returns {Promise<ToolOutputSchema>}
 */
export async function resolveLocator(input) {
    return post(`${BASE}/resolve_locator`, input);
}

/**
 * Start a new G4 driver session.
 * @param {object} input  { driver, driver_binaries, token, always_match?, first_match? }
 * @returns {Promise<ToolOutputSchema>}
 */
export async function startSession(input) {
    return post(`${BASE}/start_g4_session`, input);
}

/**
 * Execute a rule in an active driver session.
 * @param {object} input  { driverSession, rule, token }
 * @returns {Promise<ToolOutputSchema>}
 */
export async function startRule(input) {
    return post(`${BASE}/start_g4_rule`, input);
}
