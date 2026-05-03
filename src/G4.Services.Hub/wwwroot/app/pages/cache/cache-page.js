/**
 * G4 Studio — Cache Page.
 * View the plugin cache, browse by type, trigger sync.
 */

import { getPluginCache, syncInternalCache, findTools } from '../../services/cache-service.js';
import { showToast }      from '../../stores/app-store.js';
import { showLoader, showError } from '../../components/app-loader.js';
import { escapeHtml }     from '../../utils/dom.js';

export async function render(container) {
    container.innerHTML = `
        <div class="page-wrapper">
            <div class="page-header">
                <div>
                    <div class="page-title">Plugin Cache</div>
                    <div class="page-subtitle">Cached G4 plugins by type</div>
                </div>
                <div class="page-actions">
                    <button class="btn btn-ghost btn-sm" id="sync-btn">Sync Cache</button>
                    <button class="btn btn-ghost btn-sm" id="refresh-btn">Refresh</button>
                </div>
            </div>

            <div class="toolbar" style="margin-bottom:var(--space-8);">
                <div class="search-input-wrap">
                    <span class="search-icon">⌕</span>
                    <input type="search" id="cache-search" placeholder="Find tool by intent…" />
                </div>
                <button class="btn btn-secondary btn-sm" id="find-btn">Find</button>
            </div>

            <div id="find-results" class="hidden" style="margin-bottom:var(--space-8);"></div>

            <div id="cache-body"></div>
        </div>
    `;

    const load = () => _load(container);

    container.querySelector('#refresh-btn')?.addEventListener('click', load);

    container.querySelector('#sync-btn')?.addEventListener('click', async () => {
        const btn = container.querySelector('#sync-btn');
        btn.disabled = true;
        btn.textContent = 'Syncing…';
        try {
            await syncInternalCache();
            showToast('success', 'Cache synced');
            await load();
        } catch (err) {
            showToast('error', 'Sync failed', err.message);
        } finally {
            btn.disabled = false;
            btn.textContent = 'Sync Cache';
        }
    });

    const searchInput = container.querySelector('#cache-search');
    const findBtn     = container.querySelector('#find-btn');
    const doFind = async () => {
        const intent = searchInput?.value.trim();
        if (!intent) return;
        const results = container.querySelector('#find-results');
        results?.classList.remove('hidden');
        results.innerHTML = '<div class="page-loader" style="min-height:60px;"><div class="spinner spinner-sm"></div></div>';
        try {
            const found = await findTools(intent);
            results.innerHTML = `
                <div class="card">
                    <div class="card-header"><span class="card-title">Find Results — "${escapeHtml(intent)}"</span></div>
                    <div class="card-body">
                        <div class="code-block">${escapeHtml(JSON.stringify(found, null, 2))}</div>
                    </div>
                </div>`;
        } catch (err) {
            results.innerHTML = `<div style="color:var(--color-danger);font-size:var(--text-sm);">Find failed: ${escapeHtml(err.message)}</div>`;
        }
    };

    findBtn?.addEventListener('click', doFind);
    searchInput?.addEventListener('keydown', e => { if (e.key === 'Enter') doFind(); });

    await load();
}

async function _load(container) {
    const body = container.querySelector('#cache-body');
    showLoader(body, 'Loading plugin cache…');

    let cache;
    try {
        cache = await getPluginCache();
        if (!cache || typeof cache !== 'object') cache = {};
    } catch (err) {
        showError(body, err.message, () => _load(container));
        return;
    }

    const types = Object.keys(cache);

    if (!types.length) {
        body.innerHTML = `
            <div class="empty-state">
                <div class="empty-state-icon">◉</div>
                <div class="empty-state-title">Cache is empty</div>
                <div class="empty-state-message">Use "Sync Cache" to populate the plugin cache from the backend.</div>
            </div>`;
        return;
    }

    let totalPlugins = 0;
    for (const type of types) {
        totalPlugins += Object.keys(cache[type] || {}).length;
    }

    body.innerHTML = `
        <div style="margin-bottom:12px;font-size:var(--text-sm);color:var(--text-secondary);">
            ${types.length} plugin type${types.length !== 1 ? 's' : ''} · ${totalPlugins} total plugins
        </div>

        <div class="tabs" id="cache-tabs" style="margin-bottom:16px;">
            ${types.map((t, i) => `
                <div class="tab${i === 0 ? ' active' : ''}" data-tab="${escapeHtml(t)}">
                    ${escapeHtml(t)}
                    <span class="badge" style="margin-left:4px;">${Object.keys(cache[t] || {}).length}</span>
                </div>
            `).join('')}
        </div>

        <div id="cache-tab-content"></div>
    `;

    const showType = (typeName) => {
        body.querySelectorAll('.tab').forEach(t => t.classList.toggle('active', t.dataset.tab === typeName));
        const plugins = cache[typeName] || {};
        const pluginList = Object.entries(plugins);
        const content = body.querySelector('#cache-tab-content');
        if (!content) return;

        if (!pluginList.length) {
            content.innerHTML = `<div style="color:var(--text-muted);padding:20px;">No plugins in this category.</div>`;
            return;
        }

        content.innerHTML = `
            <div class="data-table-wrap">
                <table class="data-table">
                    <thead><tr><th>Plugin Key</th><th>Name</th><th>Category</th></tr></thead>
                    <tbody>
                        ${pluginList.map(([key, plugin]) => `
                            <tr>
                                <td style="font-family:var(--font-mono);font-size:var(--text-xs);">${escapeHtml(key)}</td>
                                <td>${escapeHtml(plugin.name || plugin.pluginName || key)}</td>
                                <td><span class="badge">${escapeHtml(plugin.category || plugin.pluginType || typeName)}</span></td>
                            </tr>
                        `).join('')}
                    </tbody>
                </table>
            </div>
        `;
    };

    // Show first type
    if (types.length) showType(types[0]);

    body.querySelector('#cache-tabs')?.addEventListener('click', (e) => {
        const tab = e.target.closest('[data-tab]');
        if (tab) showType(tab.dataset.tab);
    });
}
