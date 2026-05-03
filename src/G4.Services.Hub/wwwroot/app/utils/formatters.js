/**
 * G4 Studio — Formatters.
 * Pure functions for formatting values for display. No side effects.
 */

/**
 * Format an ISO date string or Date object into a readable local date-time.
 * @param {string|Date|null} value
 * @param {boolean} [includeTime=true]
 * @returns {string}
 */
export function formatDate(value, includeTime = true) {
    if (!value) return '—';
    const d = value instanceof Date ? value : new Date(value);
    if (isNaN(d)) return String(value);
    const opts = includeTime
        ? { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }
        : { year: 'numeric', month: 'short', day: 'numeric' };
    return d.toLocaleString(undefined, opts);
}

/**
 * Format a date as a short relative time string (e.g. "3 min ago").
 * @param {string|Date|null} value
 * @returns {string}
 */
export function timeAgo(value) {
    if (!value) return '—';
    const d = value instanceof Date ? value : new Date(value);
    if (isNaN(d)) return String(value);
    const diff = Date.now() - d.getTime();
    const sec = Math.floor(diff / 1000);
    if (sec < 5)  return 'just now';
    if (sec < 60) return `${sec}s ago`;
    const min = Math.floor(sec / 60);
    if (min < 60) return `${min} min ago`;
    const hr = Math.floor(min / 60);
    if (hr < 24)  return `${hr}h ago`;
    const day = Math.floor(hr / 24);
    if (day < 30) return `${day}d ago`;
    return formatDate(d, false);
}

/**
 * Format a byte count to a human-readable size string.
 * @param {number} bytes
 * @param {number} [decimals=1]
 * @returns {string}
 */
export function formatBytes(bytes, decimals = 1) {
    if (bytes == null || isNaN(bytes)) return '—';
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(decimals))} ${sizes[i]}`;
}

/**
 * Truncate a string to a max length, appending ellipsis if truncated.
 * @param {string} str
 * @param {number} max
 * @returns {string}
 */
export function truncate(str, max) {
    if (!str) return '';
    return str.length > max ? `${str.slice(0, max)}…` : str;
}

/**
 * Convert a camelCase or PascalCase string to Title Case with spaces.
 * e.g. "pluginType" → "Plugin Type"
 * @param {string} str
 * @returns {string}
 */
export function toTitleCase(str) {
    if (!str) return '';
    return str
        .replace(/([A-Z])/g, ' $1')
        .replace(/^./, s => s.toUpperCase())
        .trim();
}

/**
 * Convert a kebab-case string to Title Case.
 * e.g. "openai-tools" → "Openai Tools"
 * @param {string} str
 * @returns {string}
 */
export function kebabToTitle(str) {
    if (!str) return '';
    return str
        .split('-')
        .map(w => w.charAt(0).toUpperCase() + w.slice(1))
        .join(' ');
}

/**
 * Format a number with comma separators.
 * @param {number|null} n
 * @returns {string}
 */
export function formatNumber(n) {
    if (n == null || isNaN(n)) return '—';
    return n.toLocaleString();
}

/**
 * Format a duration in milliseconds to a human-readable string.
 * @param {number} ms
 * @returns {string}
 */
export function formatDuration(ms) {
    if (ms == null || isNaN(ms)) return '—';
    if (ms < 1000) return `${ms}ms`;
    const s = (ms / 1000).toFixed(1);
    if (ms < 60000) return `${s}s`;
    const m = Math.floor(ms / 60000);
    const rem = ((ms % 60000) / 1000).toFixed(0);
    return `${m}m ${rem}s`;
}

/**
 * Convert an array of strings to a comma-separated list for display.
 * @param {string[]|null} arr
 * @param {string} [empty='—']
 * @returns {string}
 */
export function joinList(arr, empty = '—') {
    if (!arr?.length) return empty;
    return arr.join(', ');
}
