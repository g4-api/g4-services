/**
 * G4 Studio — Dashboard Page.
 * High-level operational overview: platform stats, quick actions, recent activity.
 */

import { getAllTemplates }    from '../../services/templates-service.js';
import { getAllBots }         from '../../services/bots-service.js';
import { getAllEnvironments } from '../../services/environments-service.js';
import { getPluginCache }    from '../../services/cache-service.js';
import { navigate }          from '../../router/router.js';
import { showToast }         from '../../stores/app-store.js';
import { formatDate }        from '../../utils/formatters.js';
import { escapeHtml }        from '../../utils/dom.js';

/**
 * Render the dashboard page.
 * @param {HTMLElement} container
 */
export async function render(container) {
    container.innerHTML = `
        <div class="page-wrapper">
            <div class="page-header">
                <div>
                    <div class="page-title">Dashboard</div>
                    <div class="page-subtitle">G4 Studio platform overview</div>
                </div>
                <div class="page-actions">
                    <button class="btn btn-primary" id="dash-new-workflow">New Workflow</button>
                </div>
            </div>

            <div class="grid-4 mb-6" id="stats-grid">
                ${_statSkeleton(4)}
            </div>

            <div class="grid-2" style="gap:var(--space-8);">
                <div class="card" id="quick-actions-card">
                    <div class="card-header">
                        <span class="card-title">Quick Actions</span>
                    </div>
                    <div class="card-body" style="display:flex;flex-direction:column;gap:var(--space-5);">
                        ${_quickActions()}
                    </div>
                </div>

                <div class="card" id="status-card">
                    <div class="card-header">
                        <span class="card-title">Platform Status</span>
                        <button class="btn btn-ghost btn-sm" id="refresh-status">Refresh</button>
                    </div>
                    <div class="card-body" id="status-body">
                        <div class="page-loader" style="min-height:120px;">
                            <div class="spinner"></div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `;

    // Wire quick action navigation
    container.querySelector('#dash-new-workflow')?.addEventListener('click', () => navigate('/workflow'));
    container.addEventListener('click', (e) => {
        const btn = e.target.closest('[data-nav]');
        if (btn) navigate(btn.dataset.nav);
    });

    // Load stats
    _loadStats(container);

    // Load status
    const refreshBtn = container.querySelector('#refresh-status');
    const loadStatus = () => _loadStatus(container);
    refreshBtn?.addEventListener('click', loadStatus);
    loadStatus();
}

function _statSkeleton(n) {
    return Array.from({ length: n }, () => `
        <div class="stat-card">
            <div class="stat-label" style="background:var(--bg-elevated);height:10px;border-radius:var(--radius-sm);width:60%;"></div>
            <div class="stat-value" style="background:var(--bg-elevated);height:28px;border-radius:var(--radius-sm);width:40%;margin-top:8px;"></div>
        </div>
    `).join('');
}

async function _loadStats(container) {
    const grid = container.querySelector('#stats-grid');
    if (!grid) return;

    // Fire all requests in parallel; individual failures render as N/A
    const [templates, bots, envs, cache] = await Promise.allSettled([
        getAllTemplates(),
        getAllBots(),
        getAllEnvironments(),
        getPluginCache(),
    ]);

    const templateCount = templates.status === 'fulfilled' ? (Array.isArray(templates.value) ? templates.value.length : Object.keys(templates.value || {}).length) : '—';
    const botCount      = bots.status      === 'fulfilled' ? (Array.isArray(bots.value) ? bots.value.length : '—') : '—';
    const envCount      = envs.status      === 'fulfilled' ? Object.keys(envs.value || {}).length : '—';
    const pluginCount   = cache.status     === 'fulfilled' ? _countPlugins(cache.value) : '—';

    grid.innerHTML = `
        ${_statCard('Templates', templateCount, '⊞', '/templates')}
        ${_statCard('Bots', botCount, '⊕', '/bots')}
        ${_statCard('Environments', envCount, '⊗', '/environments')}
        ${_statCard('Plugins', pluginCount, '◉', '/cache')}
    `;
}

function _statCard(label, value, icon, href) {
    return `
        <div class="stat-card" style="cursor:pointer;" data-nav="${escapeHtml(href)}">
            <div class="stat-label">${escapeHtml(icon)}&nbsp;&nbsp;${escapeHtml(label)}</div>
            <div class="stat-value">${escapeHtml(String(value))}</div>
            <div class="stat-meta text-accent" style="font-size:var(--text-xs);">View →</div>
        </div>
    `;
}

function _countPlugins(cache) {
    if (!cache || typeof cache !== 'object') return '—';
    let total = 0;
    for (const type of Object.values(cache)) {
        if (type && typeof type === 'object') total += Object.keys(type).length;
    }
    return total;
}

function _quickActions() {
    const actions = [
        { icon: '◈', label: 'Open Workflow Canvas', nav: '/workflow' },
        { icon: '⊞', label: 'Browse Templates',     nav: '/templates' },
        { icon: '▶', label: 'View Automation Runs',  nav: '/automation' },
        { icon: '⊕', label: 'Manage Bots',           nav: '/bots' },
        { icon: '⊗', label: 'Environments',          nav: '/environments' },
        { icon: '⚙', label: 'Settings',              nav: '/settings' },
    ];
    return actions.map(a => `
        <button class="btn btn-ghost" data-nav="${escapeHtml(a.nav)}"
                style="justify-content:flex-start;gap:var(--space-6);height:36px;">
            <span style="font-size:14px;">${escapeHtml(a.icon)}</span>
            <span>${escapeHtml(a.label)}</span>
        </button>
    `).join('');
}

async function _loadStatus(container) {
    const statusBody = container.querySelector('#status-body');
    if (!statusBody) return;
    statusBody.innerHTML = `<div class="page-loader" style="min-height:120px;"><div class="spinner"></div></div>`;

    const checks = [
        { name: 'Templates API', fn: getAllTemplates },
        { name: 'Bots API',      fn: getAllBots },
        { name: 'Environments',  fn: getAllEnvironments },
        { name: 'Plugin Cache',  fn: getPluginCache },
    ];

    const results = await Promise.allSettled(checks.map(c => c.fn()));

    statusBody.innerHTML = `
        <div class="detail-panel">
            ${checks.map((c, i) => {
                const ok = results[i].status === 'fulfilled';
                return `
                    <div class="detail-row">
                        <span class="detail-label">${escapeHtml(c.name)}</span>
                        <span class="${ok ? 'text-success' : 'text-danger'}">${ok ? '✓ Online' : '✕ Unavailable'}</span>
                    </div>
                `;
            }).join('')}
        </div>
        <div style="font-size:var(--text-xs);color:var(--text-muted);margin-top:12px;">
            Last checked: ${formatDate(new Date())}
        </div>
    `;
}
