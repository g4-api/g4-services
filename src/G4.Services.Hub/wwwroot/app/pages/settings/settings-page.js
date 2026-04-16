/**
 * G4 Studio — Settings Page.
 * Theme switching, API endpoint info, developer mode, session overview.
 * All changes are local-only — no backend persistence for preferences.
 */

import { getActiveTheme, setTheme }   from '../../stores/theme-store.js';
import { getAuthState }               from '../../stores/auth-store.js';
import { getAppState, toggleDevMode } from '../../stores/app-store.js';
import { API_BASE, SWAGGER_DOCS }     from '../../utils/constants.js';
import { escapeHtml }                 from '../../utils/dom.js';
import { on, EVENTS }                 from '../../utils/events.js';

/**
 * Render the settings page.
 * @param {HTMLElement} container
 * @returns {{ destroy: function }}
 */
export function render(container) {
    container.innerHTML = `
        <div class="page-wrapper">
            <div class="page-header">
                <div>
                    <div class="page-title">Settings</div>
                    <div class="page-subtitle">Application preferences and system configuration</div>
                </div>
            </div>

            <div style="display:flex;flex-direction:column;gap:var(--space-8);max-width:720px;">

                <!-- Theme -->
                <div class="card">
                    <div class="card-header">
                        <span class="card-title">Appearance</span>
                    </div>
                    <div class="card-body">
                        <div class="detail-panel">
                            <div class="detail-row" style="align-items:center;">
                                <span class="detail-label">Theme</span>
                                <div style="display:flex;gap:var(--space-4);">
                                    <button class="btn btn-secondary btn-sm theme-pick ${getActiveTheme() === 'dark' ? 'btn-primary' : ''}"
                                            data-theme="dark">Dark</button>
                                    <button class="btn btn-secondary btn-sm theme-pick ${getActiveTheme() === 'light' ? 'btn-primary' : ''}"
                                            data-theme="light">Light</button>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Session -->
                <div class="card">
                    <div class="card-header">
                        <span class="card-title">Session</span>
                        <span class="badge badge-warning">Mock</span>
                    </div>
                    <div class="card-body">
                        <div class="detail-panel" id="session-panel">
                            ${_renderSession()}
                        </div>
                    </div>
                </div>

                <!-- API Endpoints -->
                <div class="card">
                    <div class="card-header">
                        <span class="card-title">Backend Endpoints</span>
                        <span class="badge badge-info" style="font-family:var(--font-mono);font-size:10px;">
                            ${escapeHtml(API_BASE || window.location.origin)}
                        </span>
                    </div>
                    <div class="card-body" style="padding:0;">
                        <table class="data-table">
                            <thead>
                                <tr>
                                    <th>Domain</th>
                                    <th>Swagger URL</th>
                                </tr>
                            </thead>
                            <tbody>
                                ${Object.entries(SWAGGER_DOCS).map(([domain, url]) => `
                                    <tr>
                                        <td style="font-weight:var(--font-medium);">${escapeHtml(domain)}</td>
                                        <td style="font-family:var(--font-mono);font-size:var(--text-xs);color:var(--text-secondary);">
                                            ${escapeHtml(url)}
                                        </td>
                                    </tr>
                                `).join('')}
                            </tbody>
                        </table>
                    </div>
                </div>

                <!-- Developer Mode -->
                <div class="card">
                    <div class="card-header">
                        <span class="card-title">Developer</span>
                    </div>
                    <div class="card-body">
                        <div class="detail-panel">
                            <div class="detail-row" style="align-items:center;">
                                <span class="detail-label">Dev Mode</span>
                                <div style="display:flex;align-items:center;gap:var(--space-6);">
                                    <button class="btn btn-secondary btn-sm" id="devmode-btn">
                                        ${getAppState().devMode ? 'Enabled — Disable' : 'Disabled — Enable'}
                                    </button>
                                    <span style="font-size:var(--text-xs);color:var(--text-muted);">
                                        Enables additional diagnostics and placeholders
                                    </span>
                                </div>
                            </div>
                            <div class="detail-row" style="align-items:center;">
                                <span class="detail-label">App Version</span>
                                <span class="detail-value" style="font-family:var(--font-mono);">G4 Studio v0.1.0</span>
                            </div>
                        </div>
                    </div>
                </div>

            </div>
        </div>
    `;

    // Theme picker
    container.querySelectorAll('.theme-pick').forEach(btn => {
        btn.addEventListener('click', () => {
            setTheme(btn.dataset.theme);
            // Update active state on buttons
            container.querySelectorAll('.theme-pick').forEach(b => {
                b.classList.toggle('btn-primary', b.dataset.theme === btn.dataset.theme);
                b.classList.toggle('btn-secondary', b.dataset.theme !== btn.dataset.theme);
            });
        });
    });

    // Dev mode toggle
    container.querySelector('#devmode-btn')?.addEventListener('click', () => {
        toggleDevMode();
        const devBtn = container.querySelector('#devmode-btn');
        if (devBtn) {
            devBtn.textContent = getAppState().devMode ? 'Enabled — Disable' : 'Disabled — Enable';
        }
    });

    // Keep theme buttons in sync if theme changes externally
    const unsubTheme = on(EVENTS.THEME_CHANGED, (theme) => {
        container.querySelectorAll('.theme-pick').forEach(b => {
            b.classList.toggle('btn-primary', b.dataset.theme === theme);
            b.classList.toggle('btn-secondary', b.dataset.theme !== theme);
        });
    });

    return {
        destroy() {
            unsubTheme();
        }
    };
}

function _renderSession() {
    const { user, authenticated } = getAuthState();
    if (!authenticated || !user) {
        return `
            <div class="detail-row">
                <span class="detail-label">Status</span>
                <span class="badge badge-warning">Not authenticated</span>
            </div>
        `;
    }
    return `
        <div class="detail-row">
            <span class="detail-label">Status</span>
            <span class="badge badge-success"><span class="badge-dot"></span>Active (mock)</span>
        </div>
        <div class="detail-row">
            <span class="detail-label">Name</span>
            <span class="detail-value">${escapeHtml(user.name || '—')}</span>
        </div>
        <div class="detail-row">
            <span class="detail-label">Username</span>
            <span class="detail-value" style="font-family:var(--font-mono);">${escapeHtml(user.username || '—')}</span>
        </div>
        <div class="detail-row">
            <span class="detail-label">Role</span>
            <span class="badge badge-accent">${escapeHtml(user.role || '—')}</span>
        </div>
        <div class="detail-row">
            <span class="detail-label">Email</span>
            <span class="detail-value">${escapeHtml(user.email || '—')}</span>
        </div>
    `;
}
