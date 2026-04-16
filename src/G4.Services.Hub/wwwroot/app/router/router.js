/**
 * G4 Studio — Router.
 * Hash-based SPA router (#/path/:param).
 * Handles route matching, param extraction, auth guards, and layout switching.
 */

import { routes, DEFAULT_ROUTE, LOGIN_ROUTE } from './routes.js';
import { getAuthState } from '../stores/auth-store.js';
import { emit, EVENTS } from '../utils/events.js';
import { setPageMeta } from '../stores/app-store.js';

/** Currently matched route and params */
let _current = { route: null, params: {}, path: '' };

/** Registered layout renderer keyed by layout name */
const _layoutRenderers = new Map();

/**
 * Register a layout renderer function.
 * @param {string} name
 * @param {function(container, pageModule, params): void} renderer
 */
export function registerLayout(name, renderer) {
    _layoutRenderers.set(name, renderer);
}

/**
 * Get current route info.
 */
export function getCurrentRoute() {
    return { ..._current };
}

/**
 * Convert a route path pattern to a RegExp and extract named param keys.
 * @param {string} pattern  e.g. '/bots/:id'
 * @returns {{ regex: RegExp, keys: string[] }}
 */
function compilePattern(pattern) {
    const keys = [];
    const regexStr = pattern
        .replace(/:[a-zA-Z_][a-zA-Z0-9_]*/g, (match) => {
            keys.push(match.slice(1));
            return '([^/]+)';
        })
        .replace(/\//g, '\\/');
    return { regex: new RegExp(`^${regexStr}$`), keys };
}

const _compiled = routes.map(route => ({
    ...route,
    ...compilePattern(route.path),
}));

/**
 * Match a URL path against compiled routes.
 * @param {string} path
 * @returns {{ route: object, params: object }|null}
 */
function matchRoute(path) {
    for (const route of _compiled) {
        const match = path.match(route.regex);
        if (match) {
            const params = {};
            route.keys.forEach((key, i) => {
                params[key] = decodeURIComponent(match[i + 1]);
            });
            return { route, params };
        }
    }
    return null;
}

/**
 * Get the current path from the URL hash.
 * @returns {string}
 */
function getHashPath() {
    const hash = location.hash;
    if (!hash || hash === '#' || hash === '#/') return DEFAULT_ROUTE;
    return hash.startsWith('#') ? hash.slice(1) : hash;
}

/**
 * Navigate to a path.
 * @param {string} path
 * @param {boolean} [replace=false]  Use replaceState instead of push
 */
export function navigate(path, replace = false) {
    if (replace) {
        location.replace(`#${path}`);
    } else {
        location.hash = path;
    }
}

/**
 * Navigate back in history.
 */
export function navigateBack() {
    history.back();
}

/**
 * Handle a route change — match, guard, render.
 */
async function handleRouteChange() {
    const path = getHashPath();
    const matched = matchRoute(path);

    if (!matched) {
        // Unmatched path → redirect to dashboard or login
        const { authenticated } = getAuthState();
        navigate(authenticated ? DEFAULT_ROUTE : LOGIN_ROUTE, true);
        return;
    }

    const { route, params } = matched;

    // Auth guard
    if (!route.public) {
        const { authenticated } = getAuthState();
        if (!authenticated) {
            navigate(LOGIN_ROUTE, true);
            return;
        }
    }

    // If already on login but authenticated, redirect to dashboard
    if (route.path === LOGIN_ROUTE) {
        const { authenticated } = getAuthState();
        if (authenticated) {
            navigate(DEFAULT_ROUTE, true);
            return;
        }
    }

    _current = { route, params, path };

    // Update page meta
    setPageMeta(route.title || '', buildBreadcrumbs(route));

    // Emit route changed event
    emit(EVENTS.ROUTE_CHANGED, { route, params, path });

    // Load page module and render via layout
    try {
        const pageModule = await route.loader();
        const layoutRenderer = _layoutRenderers.get(route.layout || 'main');
        if (layoutRenderer) {
            layoutRenderer(pageModule, params);
        } else {
            console.warn(`[router] No layout renderer for: "${route.layout}"`);
        }
    } catch (err) {
        console.error('[router] Failed to load page:', err);
    }
}

/**
 * Build simple breadcrumb data from a route.
 * @param {object} route
 * @returns {Array<{label: string, href?: string}>}
 */
function buildBreadcrumbs(route) {
    return [
        { label: 'G4 Studio' },
        { label: route.title || route.path, href: `#${route.path}` },
    ];
}

/**
 * Start the router — listen for hash changes and process the current hash.
 */
export function startRouter() {
    window.addEventListener('hashchange', handleRouteChange);
    handleRouteChange();
}
