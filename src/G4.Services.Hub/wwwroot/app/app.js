/**
 * G4 Studio — Application Entry Point.
 * Bootstraps the application: restores persisted state, registers layouts,
 * wires global event handlers, and starts the router.
 */

import { restoreTheme }         from './utils/theme.js';
import { restoreSession }       from './stores/auth-store.js';
import { restoreSidebarState }  from './stores/app-store.js';
import { on, EVENTS }           from './utils/events.js';
import { registerLayout, startRouter, navigate } from './router/router.js';
import { DEFAULT_ROUTE, LOGIN_ROUTE } from './router/routes.js';
import { mountToasts }          from './components/app-toast.js';
import { renderAuthLayout }     from './layouts/auth-layout.js';
import { renderMainLayout }     from './layouts/main-layout.js';
import { renderWorkflowLayout } from './layouts/workflow-layout.js';

// ---- 1. Restore persisted state ----

restoreTheme();
restoreSession();
restoreSidebarState();

// ---- 2. Mount global components ----

mountToasts();

// ---- 3. Register layout renderers ----

registerLayout('auth',     renderAuthLayout);
registerLayout('main',     renderMainLayout);
registerLayout('workflow', renderWorkflowLayout);

// ---- 4. Wire global auth events ----

on(EVENTS.AUTH_LOGIN, () => {
    // After login, redirect to dashboard
    navigate(DEFAULT_ROUTE, true);
});

on(EVENTS.AUTH_LOGOUT, () => {
    // Clear the app shell and redirect to login
    const app = document.getElementById('app');
    if (app) app.innerHTML = '';
    navigate(LOGIN_ROUTE, true);
});

// ---- 5. Start the router ----

startRouter();
