/**
 * G4 Studio — Login Page.
 * Mock authentication login screen. No real token flows.
 * Credentials: admin / admin (see constants.js MOCK_ADMIN_USER)
 */

import { login }      from '../../services/auth-service.js';
import { escapeHtml } from '../../utils/dom.js';

/**
 * Render the login page into the given container.
 * @param {HTMLElement} container
 */
export function render(container) {
    container.innerHTML = `
        <div style="
            background: var(--bg-surface);
            border: 1px solid var(--border-color);
            border-radius: var(--radius-lg);
            padding: 40px;
            width: 100%;
            max-width: 360px;
            box-shadow: var(--shadow-xl);
        ">
            <div style="text-align:center;margin-bottom:32px;">
                <div style="
                    width:40px;height:40px;border-radius:var(--radius-md);
                    background:var(--accent);display:inline-flex;align-items:center;
                    justify-content:center;font-size:18px;font-weight:700;color:#fff;
                    margin-bottom:16px;
                ">G4</div>
                <h1 style="font-size:var(--text-xl);margin-bottom:6px;">Sign in to G4 Studio</h1>
                <p style="font-size:var(--text-sm);color:var(--text-muted);">Workflow automation platform</p>
            </div>

            <form id="login-form" novalidate>
                <div class="form-group">
                    <label class="form-label" for="login-username">Username</label>
                    <input id="login-username" type="text" placeholder="Enter username" autocomplete="username" />
                    <span class="form-error hidden" id="username-error"></span>
                </div>

                <div class="form-group">
                    <label class="form-label" for="login-password">Password</label>
                    <input id="login-password" type="password" placeholder="Enter password" autocomplete="current-password" />
                    <span class="form-error hidden" id="password-error"></span>
                </div>

                <div class="form-error hidden" id="form-error" style="margin-bottom:12px;"></div>

                <button type="submit" class="btn btn-primary btn-lg" id="login-submit" style="width:100%;margin-top:8px;">
                    Sign In
                </button>
            </form>

            <div style="margin-top:20px;padding-top:16px;border-top:1px solid var(--border-color);text-align:center;">
                <span style="font-size:var(--text-xs);color:var(--text-muted);">
                    Demo: <code style="font-family:var(--font-mono);">admin / admin</code>
                </span>
            </div>
        </div>
    `;

    _bindForm(container);
}

function _bindForm(container) {
    const form       = container.querySelector('#login-form');
    const submitBtn  = container.querySelector('#login-submit');
    const formError  = container.querySelector('#form-error');
    const usernameEl = container.querySelector('#login-username');
    const passwordEl = container.querySelector('#login-password');

    // Auto-focus username
    usernameEl?.focus();

    form?.addEventListener('submit', async (e) => {
        e.preventDefault();

        const username = usernameEl?.value.trim() || '';
        const password = passwordEl?.value || '';

        // Clear errors
        formError.classList.add('hidden');
        formError.textContent = '';

        if (!username) {
            container.querySelector('#username-error').textContent = 'Username is required.';
            container.querySelector('#username-error').classList.remove('hidden');
            usernameEl.focus();
            return;
        }
        container.querySelector('#username-error').classList.add('hidden');

        // Disable form
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<span class="spinner spinner-sm"></span>&nbsp;Signing in…';

        const result = await login(username, password);

        if (!result.success) {
            formError.textContent = result.error || 'Login failed.';
            formError.classList.remove('hidden');
            submitBtn.disabled = false;
            submitBtn.textContent = 'Sign In';
            passwordEl.value = '';
            passwordEl.focus();
        }
        // On success, the AUTH_LOGIN event in app.js will redirect
    });
}
