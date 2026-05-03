/**
 * G4 Studio — Files Service.
 * Wraps the Files Swagger domain: list static files, list SVGs.
 *
 * Endpoints:
 *   GET /api/v4/g4/integration/files  — List all wwwroot files
 *   GET /api/v4/g4/integration/svgs   — List all SVG files (name → path)
 */

import { get } from './api-client.js';

/**
 * List all static files in wwwroot (relative paths).
 * @returns {Promise<string[]>}
 */
export async function listAllFiles() {
    return get('/api/v4/g4/integration/files');
}

/**
 * List all SVG files as a dictionary: { [formattedName]: relativePath }
 * @returns {Promise<Record<string, string>>}
 */
export async function listSvgFiles() {
    return get('/api/v4/g4/integration/svgs');
}
