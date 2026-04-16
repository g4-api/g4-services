/**
 * G4 Studio — Users Service (Mock).
 * User management is mock-only in this phase.
 * Stores users in memory — no real backend persistence.
 */

import { MOCK_ADMIN_USER } from '../utils/constants.js';

/** @type {Map<string, object>} */
const _users = new Map([
    [MOCK_ADMIN_USER.id, {
        id:       MOCK_ADMIN_USER.id,
        username: MOCK_ADMIN_USER.username,
        name:     MOCK_ADMIN_USER.name,
        email:    MOCK_ADMIN_USER.email,
        role:     'Admin',
        active:   true,
        createdAt: new Date().toISOString(),
    }],
    ['mock-user-002', {
        id:       'mock-user-002',
        username: 'operator',
        name:     'Operator User',
        email:    'operator@g4.local',
        role:     'Operator',
        active:   true,
        createdAt: new Date().toISOString(),
    }],
    ['mock-user-003', {
        id:       'mock-user-003',
        username: 'viewer',
        name:     'Viewer User',
        email:    'viewer@g4.local',
        role:     'Viewer',
        active:   false,
        createdAt: new Date().toISOString(),
    }],
]);

let _idCounter = 100;

/**
 * Get all users.
 * @returns {Promise<object[]>}
 */
export async function getAllUsers() {
    return [..._users.values()];
}

/**
 * Get a user by ID.
 * @param {string} id
 * @returns {Promise<object|null>}
 */
export async function getUser(id) {
    return _users.get(id) || null;
}

/**
 * Create a new mock user.
 * @param {object} data  { username, name, email, role }
 * @returns {Promise<object>}
 */
export async function createUser(data) {
    const id = `mock-user-${++_idCounter}`;
    const user = {
        id,
        username: data.username,
        name:     data.name,
        email:    data.email,
        role:     data.role || 'Viewer',
        active:   true,
        createdAt: new Date().toISOString(),
    };
    _users.set(id, user);
    return user;
}

/**
 * Update an existing user.
 * @param {string} id
 * @param {Partial<object>} updates
 * @returns {Promise<object>}
 */
export async function updateUser(id, updates) {
    const user = _users.get(id);
    if (!user) throw new Error(`User not found: ${id}`);
    const updated = { ...user, ...updates, id }; // id is immutable
    _users.set(id, updated);
    return updated;
}

/**
 * Toggle a user's active state.
 * @param {string} id
 * @returns {Promise<object>}
 */
export async function toggleUserActive(id) {
    const user = _users.get(id);
    if (!user) throw new Error(`User not found: ${id}`);
    return updateUser(id, { active: !user.active });
}

/**
 * Delete a mock user. Cannot delete the mock admin.
 * @param {string} id
 * @returns {Promise<void>}
 */
export async function deleteUser(id) {
    if (id === MOCK_ADMIN_USER.id) throw new Error('Cannot delete the admin user.');
    _users.delete(id);
}

/** Available roles for the UI role picker */
export const AVAILABLE_ROLES = ['Admin', 'Operator', 'Viewer'];
