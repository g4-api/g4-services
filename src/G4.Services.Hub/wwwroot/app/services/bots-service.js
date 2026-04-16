/**
 * G4 Studio — Bots Service.
 * Wraps the Bots Swagger domain: register, status, connectivity testing.
 *
 * All paths: /api/v4/g4/bots/...
 */

import { get, post, put, del } from './api-client.js';

const BASE = '/api/v4/g4/bots';

/** Get all connected bots. @returns {Promise<ConnectedBotModel[]>} */
export async function getAllBots() {
    return get(`${BASE}/status`);
}

/**
 * Get status for specific bots by ID array.
 * @param {string[]} ids
 * @returns {Promise<ConnectedBotModel[]>}
 */
export async function getBotsByIds(ids) {
    return post(`${BASE}/status`, ids);
}

/**
 * Get a single bot by ID.
 * @param {string} id
 * @returns {Promise<ConnectedBotModel>}
 */
export async function getBot(id) {
    return get(`${BASE}/status/${encodeURIComponent(id)}`);
}

/**
 * Register a new bot.
 * @param {ConnectedBotModel} bot
 * @returns {Promise<ConnectedBotModel>}
 */
export async function registerBot(bot) {
    return post(`${BASE}/register`, bot);
}

/**
 * Update bot metadata and status.
 * @param {string} id
 * @param {ConnectedBotModel} bot
 * @returns {Promise<ConnectedBotModel>}
 */
export async function updateBot(id, bot) {
    return put(`${BASE}/register/${encodeURIComponent(id)}`, bot);
}

/**
 * Unregister a single bot by ID.
 * @param {string} id
 * @returns {Promise<ConnectedBotModel>}
 */
export async function unregisterBot(id) {
    return del(`${BASE}/register/${encodeURIComponent(id)}`);
}

/**
 * Unregister multiple bots by ID array.
 * @param {string[]} ids
 * @returns {Promise<ConnectedBotModel[]>}
 */
export async function unregisterBots(ids) {
    return del(`${BASE}/register`, ids);
}

/** Unregister all disconnected bots. @returns {Promise<ConnectedBotModel[]>} */
export async function unregisterAllDisconnected() {
    return del(`${BASE}/register/all`);
}

/**
 * Disconnect all bots (stop all monitors).
 * @returns {Promise<void>}
 */
export async function disconnectAll() {
    return del(`${BASE}/disconnect/all`);
}

/**
 * Disconnect specific bots by connection ID array.
 * @param {string[]} connectionIds
 * @returns {Promise<void>}
 */
export async function disconnectBots(connectionIds) {
    return del(`${BASE}/disconnect`, connectionIds);
}

/**
 * Disconnect a single bot by connection ID.
 * @param {string} connectionId
 * @returns {Promise<void>}
 */
export async function disconnectBot(connectionId) {
    return del(`${BASE}/disconnect/${encodeURIComponent(connectionId)}`);
}

/** Test connectivity for all bots. @returns {Promise<ConnectedBotModel[]>} */
export async function testAllBots() {
    return get(`${BASE}/test/all`);
}

/**
 * Test connectivity for a single bot.
 * @param {string} id
 * @returns {Promise<ConnectedBotModel>}
 */
export async function testBot(id) {
    return get(`${BASE}/test/${encodeURIComponent(id)}`);
}

/**
 * Test connectivity for multiple bots.
 * @param {string[]} ids
 * @returns {Promise<ConnectedBotModel[]>}
 */
export async function testBots(ids) {
    return post(`${BASE}/test`, ids);
}
