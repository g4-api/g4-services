/**
 * G4 Studio — Application constants.
 * Central location for all non-computed static values.
 */

/** Base URL for the G4 backend. Reads from the page if embedded, falls back to localhost. */
export const API_BASE = (
    window.__G4_API_BASE__ ||
    (location.hostname === 'localhost' || location.hostname === '127.0.0.1'
        ? `${location.protocol}//${location.hostname}:9944`
        : '')
);

/** All backend Swagger document URLs */
export const SWAGGER_DOCS = {
    automation:  `${API_BASE}/swagger/automation/docs.json`,
    bots:        `${API_BASE}/swagger/bots/docs.json`,
    cache:       `${API_BASE}/swagger/cache/docs.json`,
    documents:   `${API_BASE}/swagger/documents/docs.json`,
    environments:`${API_BASE}/swagger/environments/docs.json`,
    files:       `${API_BASE}/swagger/files/docs.json`,
    manifests:   `${API_BASE}/swagger/manifests/docs.json`,
    openai:      `${API_BASE}/swagger/openai/docs.json`,
    openaiTools: `${API_BASE}/swagger/openai-tools/docs.json`,
    templates:   `${API_BASE}/swagger/templates/docs.json`,
};

/** LocalStorage keys */
export const STORAGE_KEYS = {
    THEME:             'g4.theme',
    SESSION:           'g4.session',
    SIDEBAR_COLLAPSED: 'g4.sidebar.collapsed',
    LAST_ROUTE:        'g4.last_route',
};

/** Theme names */
export const THEMES = {
    DARK:  'dark',
    LIGHT: 'light',
};

/** Route paths (kept in sync with routes.js) */
export const ROUTES = {
    LOGIN:        '/login',
    DASHBOARD:    '/dashboard',
    WORKFLOW:     '/workflow',
    AUTOMATION:   '/automation',
    BOTS:         '/bots',
    BOT_DETAIL:   '/bots/:id',
    CACHE:        '/cache',
    DOCUMENTS:    '/documents',
    ENVIRONMENTS: '/environments',
    FILES:        '/files',
    MANIFESTS:    '/manifests',
    OPENAI:       '/openai',
    OPENAI_TOOLS: '/openai-tools',
    TEMPLATES:    '/templates',
    TEMPLATE_DETAIL: '/templates/:key',
    USERS:        '/users',
    SETTINGS:     '/settings',
};

/** Bot status values returned by the API */
export const BOT_STATUS = {
    ONLINE:      'Online',
    OFFLINE:     'Offline',
    REMOVED:     'Removed',
    UNKNOWN:     'Unknown',
};

/** Mock admin user used for the mock login flow */
export const MOCK_ADMIN_USER = {
    id:       'mock-admin-001',
    username: 'admin',
    password: 'admin',   // intentionally visible — this is mock-only
    name:     'Admin User',
    role:     'Admin',
    email:    'admin@g4.local',
};

/** Pagination default */
export const DEFAULT_PAGE_SIZE = 25;
