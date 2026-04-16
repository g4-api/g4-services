/**
 * G4 Studio — Manifests Page.
 * Browse all G4 plugin manifests with search and detail view.
 */

import { getAllManifests, getManifestByKey } from '../../services/manifests-service.js';
import { showLoader, showError }  from '../../components/app-loader.js';
import { openModal, closeModal }  from '../../components/app-modal.js';
import { escapeHtml }             from '../../utils/dom.js';

export async function render(container) {
    container.innerHTML = `
        <div class="page-wrapper">
            <div class="page-header">
                <div>
                    <div class="page-title">Manifests</div>
                    <div class="page-subtitle">G4 plugin manifests from the integration cache</div>
                </div>
                <div class="page-actions">
                    <button class="btn btn-ghost btn-sm" id="refresh-btn">Refresh</button>
                </div>
            </div>

            <div class="toolbar">
                <div class="search-input-wrap">
                    <span class="search-icon">⌕</span>
                    <input type="search" id="manifest-search" placeholder="Search manifests…" />
                </div>
                <span style="font-size:var(--text-sm);color:var(--text-muted);" id="manifest-count"></span>
            </div>

            <div id="manifests-body"></div>
        </div>
    `;

    const searchInput = container.querySelector('#manifest-search');
    const load = () => _load(container);
    container.querySelector('#refresh-btn')?.addEventListener('click', load);
    searchInput?.addEventListener('input', () => _filter(container, searchInput.value));

    await load();
}

async function _load(container) {
    const body = container.querySelector('#manifests-body');
    showLoader(body, 'Loading manifests…');

    let manifests;
    try {
        manifests = await getAllManifests();
        if (!Array.isArray(manifests)) manifests = [];
    } catch (err) {
        showError(body, err.message, () => _load(container));
        return;
    }

    container._manifests = manifests;
    _render(container, manifests);
}

function _filter(container, query) {
    const q = query.toLowerCase();
    const filtered = (container._manifests || []).filter(m =>
        !q ||
        String(m.key || '').toLowerCase().includes(q) ||
        String(m.pluginType || '').toLowerCase().includes(q) ||
        String(m.summary || '').toLowerCase().includes(q)
    );
    _render(container, filtered);
    container.querySelector('#manifest-search').focus();
}

function _render(container, manifests) {
    const body  = container.querySelector('#manifests-body');
    const count = container.querySelector('#manifest-count');
    if (count) count.textContent = `${manifests.length} manifest${manifests.length !== 1 ? 's' : ''}`;

    if (!manifests.length) {
        body.innerHTML = `
            <div class="empty-state">
                <div class="empty-state-icon">⊞</div>
                <div class="empty-state-title">No manifests found</div>
                <div class="empty-state-message">Plugin manifests are loaded from the backend cache. Sync the cache to populate.</div>
            </div>`;
        return;
    }

    body.innerHTML = `
        <div class="data-table-wrap">
            <table class="data-table">
                <thead>
                    <tr>
                        <th>Key</th>
                        <th>Plugin Type</th>
                        <th>Summary</th>
                        <th class="col-actions">Actions</th>
                    </tr>
                </thead>
                <tbody>
                    ${manifests.map(m => _row(m)).join('')}
                </tbody>
            </table>
        </div>
    `;

    body.addEventListener('click', async (e) => {
        const key = e.target.closest('[data-key]')?.dataset.key;
        if (!key || !e.target.closest('[data-action="view"]')) return;

        // Show full manifest detail in a modal
        const bodyEl = document.createElement('div');
        bodyEl.innerHTML = '<div class="page-loader" style="min-height:80px;"><div class="spinner spinner-sm"></div></div>';
        openModal({ title: key, body: bodyEl, size: 'xl', actions: [{ label: 'Close', variant: 'secondary', onClick: closeModal }] });

        try {
            const manifest = await getManifestByKey(key);
            bodyEl.innerHTML = `<div class="code-block" style="max-height:500px;">${escapeHtml(JSON.stringify(manifest, null, 2))}</div>`;
        } catch (err) {
            bodyEl.innerHTML = `<div style="color:var(--color-danger);">${escapeHtml(err.message)}</div>`;
        }
    });
}

function _row(m) {
    const key  = escapeHtml(m.key || '—');
    const type = escapeHtml(m.pluginType || '—');
    const sum  = escapeHtml(String(m.summary || '').slice(0, 90));
    return `
        <tr>
            <td style="font-family:var(--font-mono);font-size:var(--text-xs);">${key}</td>
            <td><span class="badge badge-accent">${type}</span></td>
            <td style="color:var(--text-secondary);">${sum}</td>
            <td class="col-actions">
                <button class="btn btn-ghost btn-sm" data-key="${key}" data-action="view">View</button>
            </td>
        </tr>
    `;
}
