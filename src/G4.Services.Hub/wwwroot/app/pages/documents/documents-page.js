/**
 * G4 Studio — Documents Page.
 * Fetch and display plugin Markdown documentation by key.
 */

import { getDocumentByKey }  from '../../services/documents-service.js';
import { getAllManifests }    from '../../services/manifests-service.js';
import { showLoader, showError } from '../../components/app-loader.js';
import { showToast }          from '../../stores/app-store.js';
import { escapeHtml }         from '../../utils/dom.js';

export async function render(container) {
    container.innerHTML = `
        <div class="page-wrapper">
            <div class="page-header">
                <div>
                    <div class="page-title">Documents</div>
                    <div class="page-subtitle">Plugin Markdown documentation viewer</div>
                </div>
            </div>

            <div class="grid-2" style="gap:var(--space-8);height:calc(100vh - 160px);min-height:400px;">
                <!-- Plugin list -->
                <div class="card" style="display:flex;flex-direction:column;overflow:hidden;">
                    <div class="card-header" style="flex-shrink:0;">
                        <span class="card-title">Plugins</span>
                        <button class="btn btn-ghost btn-sm" id="refresh-btn">Refresh</button>
                    </div>
                    <div style="padding:var(--space-6);flex-shrink:0;">
                        <input type="search" id="plugin-search" placeholder="Search plugins…" />
                    </div>
                    <div id="plugin-list" style="flex:1;overflow-y:auto;"></div>
                </div>

                <!-- Document viewer -->
                <div class="card" style="display:flex;flex-direction:column;overflow:hidden;">
                    <div class="card-header" style="flex-shrink:0;">
                        <span class="card-title" id="doc-title">Select a plugin</span>
                    </div>
                    <div id="doc-content" style="flex:1;overflow-y:auto;padding:var(--space-10);">
                        <div class="empty-state">
                            <div class="empty-state-icon">☰</div>
                            <div class="empty-state-title">Select a plugin</div>
                            <div class="empty-state-message">Choose a plugin from the list to view its documentation.</div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `;

    const searchInput = container.querySelector('#plugin-search');
    const load = () => _loadPluginList(container);
    container.querySelector('#refresh-btn')?.addEventListener('click', load);
    searchInput?.addEventListener('input', () => _filterList(container, searchInput.value));

    await load();
}

async function _loadPluginList(container) {
    const listEl = container.querySelector('#plugin-list');
    listEl.innerHTML = '<div class="page-loader" style="min-height:100px;"><div class="spinner spinner-sm"></div></div>';

    let manifests;
    try {
        manifests = await getAllManifests();
        if (!Array.isArray(manifests)) manifests = [];
    } catch (err) {
        listEl.innerHTML = `<div style="padding:16px;color:var(--color-danger);font-size:var(--text-sm);">${escapeHtml(err.message)}</div>`;
        return;
    }

    container._manifests = manifests;
    _renderPluginList(container, manifests);
}

function _filterList(container, query) {
    const q = query.toLowerCase();
    const filtered = (container._manifests || []).filter(m =>
        !q ||
        String(m.key || m.name || '').toLowerCase().includes(q)
    );
    _renderPluginList(container, filtered);
}

function _renderPluginList(container, manifests) {
    const listEl = container.querySelector('#plugin-list');
    if (!manifests.length) {
        listEl.innerHTML = `<div style="padding:16px;color:var(--text-muted);font-size:var(--text-sm);">No plugins found.</div>`;
        return;
    }

    listEl.innerHTML = manifests.map(m => {
        const key = m.key || m.name || '?';
        return `
            <div class="nav-item" data-key="${escapeHtml(key)}"
                 style="height:auto;padding:8px 12px;border-bottom:1px solid var(--border-color);flex-direction:column;align-items:flex-start;">
                <span style="font-family:var(--font-mono);font-size:var(--text-xs);">${escapeHtml(key)}</span>
                ${m.pluginType ? `<span style="font-size:var(--text-xs);color:var(--text-muted);">${escapeHtml(m.pluginType)}</span>` : ''}
            </div>
        `;
    }).join('');

    listEl.addEventListener('click', async (e) => {
        const item = e.target.closest('[data-key]');
        if (!item) return;

        const key = item.dataset.key;
        listEl.querySelectorAll('[data-key]').forEach(i => i.classList.remove('active'));
        item.classList.add('active');

        await _loadDoc(container, key);
    });
}

async function _loadDoc(container, key) {
    const docContent = container.querySelector('#doc-content');
    const docTitle   = container.querySelector('#doc-title');
    if (docTitle) docTitle.textContent = key;

    showLoader(docContent, 'Loading documentation…');

    try {
        const md = await getDocumentByKey(key);
        docContent.innerHTML = `<div class="code-block" style="max-height:none;white-space:pre-wrap;">${escapeHtml(md || '(empty document)')}</div>`;
    } catch (err) {
        showError(docContent, err.message, () => _loadDoc(container, key));
    }
}
