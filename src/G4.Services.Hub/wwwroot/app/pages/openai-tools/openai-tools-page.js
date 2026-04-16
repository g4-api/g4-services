/**
 * G4 Studio — OpenAI Tools Page.
 * View and invoke G4 AI tools (MCP tool registry).
 */

import { getTools, findTool, getInstructions } from '../../services/openai-tools-service.js';
import { showLoader, showError }   from '../../components/app-loader.js';
import { openModal, closeModal }   from '../../components/app-modal.js';
import { showToast }               from '../../stores/app-store.js';
import { escapeHtml }              from '../../utils/dom.js';

export async function render(container) {
    container.innerHTML = `
        <div class="page-wrapper">
            <div class="page-header">
                <div>
                    <div class="page-title">AI Tools</div>
                    <div class="page-subtitle">G4 MCP tool registry — browse and invoke AI tools</div>
                </div>
                <div class="page-actions">
                    <button class="btn btn-ghost btn-sm" id="instructions-btn">View Instructions</button>
                    <button class="btn btn-ghost btn-sm" id="refresh-btn">Refresh</button>
                </div>
            </div>

            <div class="toolbar">
                <div class="search-input-wrap">
                    <span class="search-icon">⌕</span>
                    <input type="search" id="tool-search" placeholder="Find tool by intent…" />
                </div>
                <button class="btn btn-secondary btn-sm" id="find-tool-btn">Find</button>
                <span style="font-size:var(--text-sm);color:var(--text-muted);" id="tool-count"></span>
            </div>

            <div id="find-result" class="hidden" style="margin-bottom:var(--space-8);"></div>
            <div id="tools-body"></div>
        </div>
    `;

    const load = () => _load(container);
    container.querySelector('#refresh-btn')?.addEventListener('click', load);

    container.querySelector('#instructions-btn')?.addEventListener('click', async () => {
        const bodyEl = document.createElement('div');
        bodyEl.innerHTML = '<div class="page-loader" style="min-height:80px;"><div class="spinner spinner-sm"></div></div>';
        openModal({ title: 'Tool Instructions', body: bodyEl, size: 'lg', actions: [{ label: 'Close', variant: 'secondary', onClick: closeModal }] });
        try {
            const result = await getInstructions();
            bodyEl.innerHTML = `<div class="code-block" style="max-height:400px;">${escapeHtml(JSON.stringify(result, null, 2))}</div>`;
        } catch (err) {
            bodyEl.innerHTML = `<div style="color:var(--color-danger);">${escapeHtml(err.message)}</div>`;
        }
    });

    const searchInput = container.querySelector('#tool-search');
    container.querySelector('#find-tool-btn')?.addEventListener('click', () => _doFind(container, searchInput?.value));
    searchInput?.addEventListener('keydown', e => { if (e.key === 'Enter') _doFind(container, searchInput.value); });

    await load();
}

async function _load(container) {
    const body = container.querySelector('#tools-body');
    showLoader(body, 'Loading AI tools…');

    let toolsResult;
    try {
        toolsResult = await getTools({});
    } catch (err) {
        showError(body, err.message, () => _load(container));
        return;
    }

    // Tools response is a ToolOutputSchema — extract tools array
    const tools = toolsResult?.tools || toolsResult?.result || toolsResult || [];
    const list   = Array.isArray(tools) ? tools : [];

    const count = container.querySelector('#tool-count');
    if (count) count.textContent = `${list.length} tool${list.length !== 1 ? 's' : ''}`;

    if (!list.length) {
        body.innerHTML = `
            <div class="empty-state">
                <div class="empty-state-icon">⚙</div>
                <div class="empty-state-title">No tools registered</div>
                <div class="empty-state-message">AI tools are registered via MCP server connections.</div>
            </div>`;
        return;
    }

    body.innerHTML = `
        <div class="data-table-wrap">
            <table class="data-table">
                <thead>
                    <tr>
                        <th>Tool Name</th>
                        <th>Description</th>
                        <th class="col-actions">Actions</th>
                    </tr>
                </thead>
                <tbody>
                    ${list.map(t => _row(t)).join('')}
                </tbody>
            </table>
        </div>
    `;

    body.addEventListener('click', (e) => {
        const name = e.target.closest('[data-name]')?.dataset.name;
        if (!name || !e.target.closest('[data-action="view"]')) return;
        const tool = list.find(t => (t.name || t.toolName) === name);
        if (tool) _showToolDetail(tool);
    });
}

async function _doFind(container, intent) {
    if (!intent?.trim()) return;
    const resultEl = container.querySelector('#find-result');
    resultEl?.classList.remove('hidden');
    resultEl.innerHTML = '<div class="page-loader" style="min-height:60px;"><div class="spinner spinner-sm"></div></div>';
    try {
        const result = await findTool({ intent: intent.trim() });
        resultEl.innerHTML = `
            <div class="card">
                <div class="card-header"><span class="card-title">Find Result — "${escapeHtml(intent)}"</span></div>
                <div class="card-body">
                    <div class="code-block">${escapeHtml(JSON.stringify(result, null, 2))}</div>
                </div>
            </div>`;
    } catch (err) {
        resultEl.innerHTML = `<div style="color:var(--color-danger);">${escapeHtml(err.message)}</div>`;
    }
}

function _row(t) {
    const name = escapeHtml(t.name || t.toolName || '—');
    const desc = escapeHtml(String(t.description || t.summary || '').slice(0, 100));
    return `
        <tr>
            <td style="font-family:var(--font-mono);font-size:var(--text-xs);">${name}</td>
            <td style="color:var(--text-secondary);">${desc}</td>
            <td class="col-actions">
                <button class="btn btn-ghost btn-sm" data-name="${name}" data-action="view">View</button>
            </td>
        </tr>
    `;
}

function _showToolDetail(tool) {
    const bodyEl = document.createElement('div');
    bodyEl.innerHTML = `<div class="code-block" style="max-height:400px;">${escapeHtml(JSON.stringify(tool, null, 2))}</div>`;
    openModal({
        title: tool.name || tool.toolName || 'Tool Detail',
        body:  bodyEl,
        size:  'lg',
        actions: [{ label: 'Close', variant: 'secondary', onClick: closeModal }],
    });
}
