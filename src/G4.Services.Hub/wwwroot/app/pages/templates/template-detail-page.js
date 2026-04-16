/**
 * G4 Studio — Template Detail Page.
 * Shows the full manifest of a single template.
 */

import { getTemplate }  from '../../services/templates-service.js';
import { navigate }     from '../../router/router.js';
import { showLoader, showError } from '../../components/app-loader.js';
import { escapeHtml }   from '../../utils/dom.js';

export async function render(container, params) {
    const key = params?.key ? decodeURIComponent(params.key) : null;

    container.innerHTML = `
        <div class="page-wrapper">
            <div class="page-header">
                <div>
                    <button class="btn btn-ghost btn-sm" id="back-btn" style="margin-bottom:8px;">‹ Templates</button>
                    <div class="page-title" id="detail-title">${escapeHtml(key || 'Template Detail')}</div>
                </div>
                <div class="page-actions">
                    <button class="btn btn-primary" id="open-wf-btn">Open in Workflow</button>
                </div>
            </div>
            <div id="detail-body"></div>
        </div>
    `;

    container.querySelector('#back-btn')?.addEventListener('click', () => navigate('/templates'));
    container.querySelector('#open-wf-btn')?.addEventListener('click', () => navigate('/workflow'));

    if (!key) {
        container.querySelector('#detail-body').innerHTML = `<div class="empty-state"><div class="empty-state-title">No template key provided.</div></div>`;
        return;
    }

    const body = container.querySelector('#detail-body');
    showLoader(body, 'Loading template…');

    try {
        const template = await getTemplate(key);
        _renderDetail(body, template);
    } catch (err) {
        showError(body, err.message, () => render(container, params));
    }
}

function _renderDetail(container, t) {
    const keys = Object.keys(t || {});
    container.innerHTML = `
        <div class="grid-2" style="gap:var(--space-8);align-items:start;">
            <div class="card">
                <div class="card-header"><span class="card-title">Properties</span></div>
                <div class="card-body">
                    <div class="detail-panel">
                        ${keys.filter(k => typeof t[k] !== 'object').map(k => `
                            <div class="detail-row">
                                <span class="detail-label">${escapeHtml(k)}</span>
                                <span class="detail-value">${escapeHtml(String(t[k] ?? '—'))}</span>
                            </div>
                        `).join('')}
                    </div>
                </div>
            </div>
            <div class="card">
                <div class="card-header"><span class="card-title">Raw Manifest</span></div>
                <div class="card-body">
                    <div class="code-block">${escapeHtml(JSON.stringify(t, null, 2))}</div>
                </div>
            </div>
        </div>
    `;
}
