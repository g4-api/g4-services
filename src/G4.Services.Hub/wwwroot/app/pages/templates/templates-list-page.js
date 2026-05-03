/**
 * G4 Studio — Templates List Page.
 * Lists all registered template manifests, supports search, delete, and open in workflow.
 */

import { getAllTemplates, deleteTemplate, clearAllTemplates } from '../../services/templates-service.js';
import { navigate }         from '../../router/router.js';
import { showToast }        from '../../stores/app-store.js';
import { openConfirm }      from '../../components/app-modal.js';
import { showLoader, showError } from '../../components/app-loader.js';
import { escapeHtml }       from '../../utils/dom.js';

export async function render(container) {
    container.innerHTML = `
        <div class="page-wrapper">
            <div class="page-header">
                <div>
                    <div class="page-title">Templates</div>
                    <div class="page-subtitle">Registered workflow template manifests</div>
                </div>
                <div class="page-actions">
                    <button class="btn btn-danger btn-sm" id="clear-all-btn">Clear All</button>
                    <button class="btn btn-primary" id="open-workflow-btn">Open in Workflow</button>
                </div>
            </div>

            <div class="toolbar">
                <div class="search-input-wrap">
                    <span class="search-icon">⌕</span>
                    <input type="search" id="template-search" placeholder="Search templates…" />
                </div>
                <span style="font-size:var(--text-sm);color:var(--text-muted);" id="template-count"></span>
            </div>

            <div id="template-table-wrap"></div>
        </div>
    `;

    container.querySelector('#open-workflow-btn')?.addEventListener('click', () => navigate('/workflow'));
    container.querySelector('#clear-all-btn')?.addEventListener('click', () => {
        openConfirm({
            title:          'Clear All Templates',
            message:        'This will permanently remove all templates. This action cannot be undone.',
            confirmLabel:   'Clear All',
            confirmVariant: 'danger',
            onConfirm: async () => {
                try {
                    await clearAllTemplates();
                    showToast('success', 'Templates cleared');
                    _load(container);
                } catch (e) {
                    showToast('error', 'Failed to clear templates', e.message);
                }
            }
        });
    });

    const searchInput = container.querySelector('#template-search');
    searchInput?.addEventListener('input', () => _filter(container, searchInput.value));

    await _load(container);
}

async function _load(container) {
    const wrap = container.querySelector('#template-table-wrap');
    showLoader(wrap, 'Loading templates…');

    let templates;
    try {
        const raw = await getAllTemplates();
        templates = Array.isArray(raw) ? raw : Object.values(raw || {});
    } catch (err) {
        showError(wrap, err.message, () => _load(container));
        return;
    }

    // Store on container for filtering
    container._templates = templates;
    _render(container, templates);
}

function _filter(container, query) {
    const q = query.toLowerCase();
    const filtered = (container._templates || []).filter(t =>
        !q ||
        String(t.key || '').toLowerCase().includes(q) ||
        String(t.pluginType || '').toLowerCase().includes(q) ||
        String(t.summary || '').toLowerCase().includes(q)
    );
    _render(container, filtered);
    container.querySelector('#template-search').focus();
}

function _render(container, templates) {
    const wrap  = container.querySelector('#template-table-wrap');
    const count = container.querySelector('#template-count');
    if (count) count.textContent = `${templates.length} template${templates.length !== 1 ? 's' : ''}`;

    if (!templates.length) {
        wrap.innerHTML = `
            <div class="empty-state">
                <div class="empty-state-icon">⊞</div>
                <div class="empty-state-title">No templates found</div>
                <div class="empty-state-message">Templates registered in the backend will appear here.</div>
            </div>`;
        return;
    }

    wrap.innerHTML = `
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
                    ${templates.map(t => _row(t)).join('')}
                </tbody>
            </table>
        </div>
    `;

    // Wire row actions
    wrap.addEventListener('click', async (e) => {
        const key = e.target.closest('[data-key]')?.dataset.key;
        if (!key) return;

        if (e.target.closest('[data-action="view"]')) {
            navigate(`/templates/${encodeURIComponent(key)}`);
        }
        if (e.target.closest('[data-action="delete"]')) {
            openConfirm({
                title: 'Delete Template',
                message: `Remove template "${key}"? This cannot be undone.`,
                confirmLabel: 'Delete',
                onConfirm: async () => {
                    try {
                        await deleteTemplate(key);
                        showToast('success', 'Template deleted', key);
                        _load(container);
                    } catch (err) {
                        showToast('error', 'Delete failed', err.message);
                    }
                }
            });
        }
    });
}

function _row(t) {
    const key  = escapeHtml(t.key || '—');
    const type = escapeHtml(t.pluginType || '—');
    const sum  = escapeHtml(String(t.summary || '').slice(0, 100));
    return `
        <tr>
            <td style="font-family:var(--font-mono);font-size:var(--text-xs);">${key}</td>
            <td><span class="badge badge-accent">${type}</span></td>
            <td style="color:var(--text-secondary);">${sum}</td>
            <td class="col-actions">
                <div style="display:flex;gap:4px;justify-content:flex-end;">
                    <button class="btn btn-ghost btn-sm" data-key="${key}" data-action="view">View</button>
                    <button class="btn btn-ghost btn-sm" data-key="${key}" data-action="delete" style="color:var(--color-danger);">Delete</button>
                </div>
            </td>
        </tr>
    `;
}
