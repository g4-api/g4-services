/**
 * G4 Studio — Automation Service.
 * Wraps the Automation Swagger domain.
 *
 * Endpoints used:
 *   POST /api/v4/g4/automation/invoke        — Synchronous invocation
 *   POST /api/v4/g4/automation/async/start   — Async enqueue
 *   GET  /api/v4/g4/automation/async/completed — Completed async responses
 *   GET  /api/v4/g4/automation/stop/{id}     — Stop a running automation
 */

import { get, post } from './api-client.js';

const BASE = '/api/v4/g4/automation';

/**
 * Invoke an automation synchronously.
 * Returns the full response containing stage/job results.
 * @param {object} automationModel  G4AutomationModel
 * @returns {Promise<object>}
 */
export async function invokeAutomation(automationModel) {
    return post(`${BASE}/invoke`, automationModel);
}

/**
 * Enqueue an automation for async processing.
 * Returns 202 Accepted — poll completed() to check results.
 * @param {object} automationModel  G4AutomationModel
 * @returns {Promise<void>}
 */
export async function startAsyncAutomation(automationModel) {
    return post(`${BASE}/async/start`, automationModel);
}

/**
 * Retrieve all completed async automation responses.
 * @returns {Promise<object[]>}
 */
export async function getCompletedAutomations() {
    return get(`${BASE}/async/completed`);
}

/**
 * Stop a running automation session by ID.
 * @param {string} automationId
 * @returns {Promise<*>}
 */
export async function stopAutomation(automationId) {
    return get(`${BASE}/stop/${encodeURIComponent(automationId)}`);
}
