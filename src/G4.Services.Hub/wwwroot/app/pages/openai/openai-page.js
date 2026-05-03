/**
 * G4 Studio — OpenAI Page.
 * Status of the G4 OpenAI proxy and available models.
 */

import { getProxyStatus, listModels, chatCompletion } from '../../services/openai-service.js';
import { showLoader, showError }  from '../../components/app-loader.js';
import { showToast }              from '../../stores/app-store.js';
import { escapeHtml }             from '../../utils/dom.js';

export async function render(container) {
    container.innerHTML = `
        <div class="page-wrapper">
            <div class="page-header">
                <div>
                    <div class="page-title">OpenAI</div>
                    <div class="page-subtitle">G4 OpenAI-compatible proxy status and model list</div>
                </div>
                <div class="page-actions">
                    <button class="btn btn-ghost btn-sm" id="refresh-btn">Refresh</button>
                </div>
            </div>

            <div class="grid-2" style="gap:var(--space-8);margin-bottom:var(--space-8);">
                <div class="card" id="status-card">
                    <div class="card-header"><span class="card-title">Proxy Status</span></div>
                    <div class="card-body" id="status-body">
                        <div class="page-loader" style="min-height:60px;"><div class="spinner spinner-sm"></div></div>
                    </div>
                </div>

                <div class="card" id="models-card">
                    <div class="card-header"><span class="card-title">Available Models</span></div>
                    <div class="card-body" id="models-body" style="padding:0;">
                        <div class="page-loader" style="min-height:60px;"><div class="spinner spinner-sm"></div></div>
                    </div>
                </div>
            </div>

            <!-- Quick test -->
            <div class="card">
                <div class="card-header"><span class="card-title">Quick Test</span></div>
                <div class="card-body">
                    <div class="form-group">
                        <label class="form-label">Model</label>
                        <input type="text" id="test-model" value="gpt-4" placeholder="Model name" style="width:280px;" />
                    </div>
                    <div class="form-group">
                        <label class="form-label">Message</label>
                        <textarea id="test-message" rows="3" placeholder="Say something to the model…" style="resize:vertical;"></textarea>
                    </div>
                    <div class="form-actions" style="margin-top:0;">
                        <button class="btn btn-primary" id="test-btn">Send</button>
                    </div>
                    <div id="test-result" class="hidden" style="margin-top:12px;">
                        <div class="section-title">Response</div>
                        <div class="code-block" id="test-result-content"></div>
                    </div>
                </div>
            </div>
        </div>
    `;

    const load = () => { _loadStatus(container); _loadModels(container); };
    container.querySelector('#refresh-btn')?.addEventListener('click', load);
    load();

    // Quick test
    container.querySelector('#test-btn')?.addEventListener('click', async () => {
        const model   = container.querySelector('#test-model')?.value.trim() || 'gpt-4';
        const message = container.querySelector('#test-message')?.value.trim();
        if (!message) { showToast('warning', 'Enter a message first'); return; }

        const btn = container.querySelector('#test-btn');
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner spinner-sm"></span>&nbsp;Sending…';

        const resultEl = container.querySelector('#test-result');
        const contentEl = container.querySelector('#test-result-content');
        resultEl?.classList.remove('hidden');
        if (contentEl) contentEl.textContent = 'Loading…';

        try {
            const res = await chatCompletion({
                model,
                messages: [{ role: 'user', content: message }],
            });
            if (contentEl) contentEl.textContent = JSON.stringify(res, null, 2);
        } catch (err) {
            if (contentEl) contentEl.textContent = `Error: ${err.message}`;
            showToast('error', 'Request failed', err.message);
        } finally {
            btn.disabled = false;
            btn.textContent = 'Send';
        }
    });
}

async function _loadStatus(container) {
    const body = container.querySelector('#status-body');
    try {
        const status = await getProxyStatus();
        body.innerHTML = `
            <div class="detail-panel">
                <div class="detail-row">
                    <span class="detail-label">State</span>
                    <span class="text-success">✓ Online</span>
                </div>
                ${typeof status === 'object' ? Object.entries(status).map(([k, v]) => `
                    <div class="detail-row">
                        <span class="detail-label">${escapeHtml(k)}</span>
                        <span class="detail-value">${escapeHtml(String(v ?? '—'))}</span>
                    </div>
                `).join('') : ''}
            </div>
        `;
    } catch (err) {
        body.innerHTML = `<div style="color:var(--color-danger);">✕ Unavailable — ${escapeHtml(err.message)}</div>`;
    }
}

async function _loadModels(container) {
    const body = container.querySelector('#models-body');
    try {
        const resp   = await listModels();
        const models = resp?.data || (Array.isArray(resp) ? resp : []);
        if (!models.length) {
            body.innerHTML = `<div style="padding:16px;color:var(--text-muted);font-size:var(--text-sm);">No models available.</div>`;
            return;
        }
        body.innerHTML = `
            <table class="data-table">
                <thead><tr><th>Model ID</th><th>Object</th></tr></thead>
                <tbody>
                    ${models.map(m => `
                        <tr>
                            <td style="font-family:var(--font-mono);font-size:var(--text-xs);">${escapeHtml(m.id || m)}</td>
                            <td><span class="badge">${escapeHtml(m.object || 'model')}</span></td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
        `;
    } catch (err) {
        body.innerHTML = `<div style="padding:16px;color:var(--color-danger);font-size:var(--text-sm);">${escapeHtml(err.message)}</div>`;
    }
}
