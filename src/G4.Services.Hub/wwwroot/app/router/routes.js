/**
 * G4 Studio — Route Definitions.
 * Each route maps a path pattern to a page module loader and metadata.
 * Layouts are referenced by name and resolved by the router.
 */

export const routes = [
    {
        path:    '/login',
        layout:  'auth',
        title:   'Sign In',
        loader:  () => import('../pages/auth/login-page.js'),
        public:  true,
    },
    {
        path:    '/dashboard',
        layout:  'main',
        title:   'Dashboard',
        loader:  () => import('../pages/dashboard/dashboard-page.js'),
        nav:     { section: 'main', icon: '⬛', label: 'Dashboard' },
    },
    {
        path:    '/workflow',
        layout:  'workflow',
        title:   'Workflow',
        loader:  () => import('../pages/workflow/workflow-page.js'),
        nav:     { section: 'main', icon: '◈', label: 'Workflow' },
    },
    {
        path:    '/templates',
        layout:  'main',
        title:   'Templates',
        loader:  () => import('../pages/templates/templates-list-page.js'),
        nav:     { section: 'main', icon: '⊞', label: 'Templates' },
    },
    {
        path:    '/templates/:key',
        layout:  'main',
        title:   'Template Detail',
        loader:  () => import('../pages/templates/template-detail-page.js'),
    },
    {
        path:    '/automation',
        layout:  'main',
        title:   'Automation',
        loader:  () => import('../pages/automation/automation-page.js'),
        nav:     { section: 'main', icon: '▶', label: 'Automation' },
    },
    {
        path:    '/bots',
        layout:  'main',
        title:   'Bots',
        loader:  () => import('../pages/bots/bots-list-page.js'),
        nav:     { section: 'platform', icon: '⊕', label: 'Bots' },
    },
    {
        path:    '/bots/:id',
        layout:  'main',
        title:   'Bot Detail',
        loader:  () => import('../pages/bots/bot-detail-page.js'),
    },
    {
        path:    '/environments',
        layout:  'main',
        title:   'Environments',
        loader:  () => import('../pages/environments/environments-page.js'),
        nav:     { section: 'platform', icon: '⊗', label: 'Environments' },
    },
    {
        path:    '/cache',
        layout:  'main',
        title:   'Cache',
        loader:  () => import('../pages/cache/cache-page.js'),
        nav:     { section: 'platform', icon: '◉', label: 'Cache' },
    },
    {
        path:    '/documents',
        layout:  'main',
        title:   'Documents',
        loader:  () => import('../pages/documents/documents-page.js'),
        nav:     { section: 'platform', icon: '☰', label: 'Documents' },
    },
    {
        path:    '/files',
        layout:  'main',
        title:   'Files',
        loader:  () => import('../pages/files/files-page.js'),
        nav:     { section: 'platform', icon: '⊟', label: 'Files' },
    },
    {
        path:    '/manifests',
        layout:  'main',
        title:   'Manifests',
        loader:  () => import('../pages/manifests/manifests-page.js'),
        nav:     { section: 'platform', icon: '⊞', label: 'Manifests' },
    },
    {
        path:    '/openai',
        layout:  'main',
        title:   'OpenAI',
        loader:  () => import('../pages/openai/openai-page.js'),
        nav:     { section: 'ai', icon: '✦', label: 'OpenAI' },
    },
    {
        path:    '/openai-tools',
        layout:  'main',
        title:   'OpenAI Tools',
        loader:  () => import('../pages/openai-tools/openai-tools-page.js'),
        nav:     { section: 'ai', icon: '⚙', label: 'AI Tools' },
    },
    {
        path:    '/users',
        layout:  'main',
        title:   'Users',
        loader:  () => import('../pages/users/users-page.js'),
        nav:     { section: 'admin', icon: '⊙', label: 'Users' },
    },
    {
        path:    '/settings',
        layout:  'main',
        title:   'Settings',
        loader:  () => import('../pages/settings/settings-page.js'),
        nav:     { section: 'admin', icon: '◎', label: 'Settings' },
    },
];

/** Navigation sections in the order they appear in the sidebar */
export const NAV_SECTIONS = [
    { id: 'main',     label: 'Workspace' },
    { id: 'platform', label: 'Platform' },
    { id: 'ai',       label: 'AI' },
    { id: 'admin',    label: 'Admin' },
];

/** Default redirect after login */
export const DEFAULT_ROUTE = '/dashboard';

/** Login route */
export const LOGIN_ROUTE = '/login';
