/**
 * G4 Studio — Automation Page.
 * View completed async automation runs. Link back to workflow to start new runs.
 */

import { getCompletedAutomations } from '../../services/automation-service.js';
import { navigate }         from '../../router/router.js';
import { showLoader, showError } from '../../components/app-loader.js';
import { escapeHtml }       from '../../utils/dom.js';
import { formatDate }       from '../../utils/formatters.js';

export async function render(container) {
    container.innerHTML = `
        <div class="page-wrapper">
            <div class="page-header">
                <div>
                    <div class="page-title">Automation</div>
                    <div class="page-subtitle">Completed async automation run results</div>
                </div>
                <div class="page-actions">
                    <button class="btn btn-ghost btn-sm" id="refresh-btn">Refresh</button>
                    <button class="btn btn-primary" id="new-run-btn">New Run</button>
                </div>
            </div>
            <div id="auto-body"></div>
        </div>
    `;

    container.querySelector('#new-run-btn')?.addEventListener('click', () => navigate('/workflow'));
    const load = () => _load(container);
    container.querySelector('#refresh-btn')?.addEventListener('click', load);
    await load();
}

async function _load(container) {
    const body = container.querySelector('#auto-body');
    showLoader(body, 'Loading automation results…');

    let runs;
    try {
        runs = await getCompletedAutomations();
    } catch (err) {
        showError(body, err.message, () => _load(container));
        return;
    }

    if (!Array.isArray(runs) || !runs.length) {
        body.innerHTML = `
            <div class="empty-state">
                <div class="empty-state-icon">▷</div>
                <div class="empty-state-title">No completed runs</div>
                <div class="empty-state-message">
                    Async automation results will appear here after runs complete.
                    Start a new run from the Workflow page.
                </div>
                <button class="btn btn-primary mt-4" id="go-workflow">Open Workflow</button>
            </div>`;
        body.querySelector('#go-workflow')?.addEventListener('click', () => navigate('/workflow'));
        return;
    }

    body.innerHTML = `
        <div class="data-table-wrap">
            <table class="data-table">
                <thead>
                    <tr>
                        <th>Run ID</th>
                        <th>Status</th>
                        <th>Completed</th>
                        <th>Stages</th>
                    </tr>
                </thead>
                <tbody>
                    ${runs.map(r => _row(r)).join('')}
                </tbody>
            </table>
        </div>
    `;
}

function _row(r) {
    const id       = escapeHtml(String(r.id || r.automationId || '—'));
    const status   = r.status || 'completed';
    const date     = formatDate(r.completedAt || r.endTime || null);
    const stages   = Array.isArray(r.stages) ? r.stages.length : '—';

    const badgeCls = status.toLowerCase().includes('error') ? 'badge-danger'
                   : status.toLowerCase().includes('stop')  ? 'badge-warning'
                   : 'badge-success';

    return `
        <tr>
            <td style="font-family:var(--font-mono);font-size:var(--text-xs);">${id}</td>
            <td><span class="badge ${badgeCls}">${escapeHtml(status)}</span></td>
            <td style="color:var(--text-secondary);">${date}</td>
            <td>${escapeHtml(String(stages))}</td>
        </tr>
    `;
}
