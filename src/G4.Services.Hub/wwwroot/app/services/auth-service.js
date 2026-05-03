/**
 * G4 Studio — Auth Service (Mock).
 * Delegates to the auth store. Provides the service-layer interface
 * so that pages do not import the store directly.
 */

export { login, logout, restoreSession, getAuthState, onAuthState, hasRole, getUserDisplayName }
    from '../stores/auth-store.js';
