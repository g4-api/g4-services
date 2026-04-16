/**
 * G4 Studio — Validators.
 * Pure validation functions. Return null on pass, error string on fail.
 */

/**
 * Require a value to be non-empty.
 * @param {string} value
 * @param {string} [label]
 * @returns {string|null}
 */
export function required(value, label = 'This field') {
    return (!value || String(value).trim() === '')
        ? `${label} is required.`
        : null;
}

/**
 * Validate a URL string.
 * @param {string} value
 * @param {string} [label]
 * @returns {string|null}
 */
export function validUrl(value, label = 'URL') {
    if (!value) return null;
    try {
        new URL(value);
        return null;
    } catch {
        return `${label} must be a valid URL.`;
    }
}

/**
 * Validate that a string matches a regex pattern.
 * @param {string} value
 * @param {RegExp} pattern
 * @param {string} message
 * @returns {string|null}
 */
export function matchesPattern(value, pattern, message) {
    if (!value) return null;
    return pattern.test(value) ? null : message;
}

/**
 * Validate environment name (word characters only).
 * @param {string} value
 * @returns {string|null}
 */
export function validEnvName(value) {
    return matchesPattern(value, /^\w+$/, 'Environment name must contain only letters, numbers, and underscores.');
}

/**
 * Validate bot name (lowercase alphanumeric and hyphens).
 * @param {string} value
 * @returns {string|null}
 */
export function validBotName(value) {
    return matchesPattern(value, /^[a-z0-9\-]+$/, 'Bot name must be lowercase letters, numbers, and hyphens only.');
}

/**
 * Run a set of validators and return the first error, or null.
 * @param {string} value
 * @param {Array<function>} validators
 * @returns {string|null}
 */
export function validate(value, validators) {
    for (const fn of validators) {
        const err = fn(value);
        if (err) return err;
    }
    return null;
}
