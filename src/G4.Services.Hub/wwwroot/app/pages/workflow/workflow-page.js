/**
 * G4 Studio — Workflow Page.
 * Outer workflow shell that wraps the existing canvas page.
 *
 * Responsibility boundary:
 *   - This page owns: top bar, log/chat sidebar, workflow name, status badge, outer actions
 *   - The canvas page owns: inner designer surface, node graph, automation start/stop internals
 *
 * The canvas is loaded via canvas-page-adapter.js — an iframe approach that preserves
 * the canvas page as an independent, unmodified document.
 */

import { mountCanvas, unmountCanvas, sendToCanvas } from '../../adapters/canvas-page-adapter.js';
import { getWorkflowState, onWorkflowState, setWorkflowName,
         toggleLogSidebar, addLogEntry, clearLogs } from '../../stores/workflow-store.js';
import { getAllTemplates } from '../../services/templates-service.js';
import { navigate }        from '../../router/router.js';
import { showToast }       from '../../stores/app-store.js';
import { escapeHtml }      from '../../utils/dom.js';
import { formatDate }      from '../../utils/formatters.js';

/** @type {function|null} Unsubscribe from workflow store */
let _unsubWorkflow = null;

/**
 * Render the workflow page into the workflow layout host.
 * @param {HTMLElement} container
 * @param {object} params
 * @returns {{ destroy: function }}
 */
export function render(container, params) {
    const state = getWorkflowState();

    container.innerHTML = `
        <div class="workflow-layout" style="height:100%;">
            <!-- Workflow top bar -->
            <div class="workflow-topbar">
                <div class="workflow-topbar-left">
                    <button class="btn btn-ghost btn-sm btn-icon" id="wf-back-btn" title="Back to Dashboard">‹</button>
                    <input class="workflow-name-input" id="wf-name-input"
                           type="text" value="${escapeHtml(state.workflowName)}"
                           placeholder="Workflow name" />
                    <span class="badge workflow-status-badge badge-info" id="wf-status-badge">Ready</span>
                </div>

                <div class="workflow-topbar-center">
                    <button class="btn btn-ghost btn-sm" id="wf-templates-btn" title="Load template">
                        ⊞&nbsp;Templates
                    </button>
                </div>

                <div class="workflow-topbar-right">
                    <button class="btn btn-ghost btn-icon btn-sm" id="wf-log-toggle" title="Toggle logs">
                        ☰
                    </button>
                </div>
            </div>

            <!-- Body: canvas host + log sidebar -->
            <div class="workflow-body" style="flex:1;overflow:hidden;">
                <!-- Canvas host (canvas page loads here) -->
                <div class="canvas-host" id="canvas-host"></div>

                <!-- Log sidebar -->
                <aside class="workflow-log-sidebar${state.logSidebarOpen ? '' : ' collapsed'}" id="log-sidebar">
                    <div class="log-sidebar-header">
                        <span class="log-sidebar-title">Execution Log</span>
                        <div class="log-sidebar-actions">
                            <button class="btn btn-ghost btn-icon btn-sm" id="wf-clear-logs" title="Clear logs">✕</button>
                        </div>
                    </div>
                    <div class="log-entries" id="log-entries">
                        ${state.logEntries.length === 0 ? '<div style="color:var(--text-muted);font-size:var(--text-xs);padding:12px;">No log entries yet.</div>' : ''}
                    </div>
                </aside>
            </div>

            <!-- Status bar -->
            <div class="workflow-statusbar">
                <div class="statusbar-item">
                    <span id="sb-canvas-status">Canvas: loading…</span>
                </div>
                <div class="statusbar-sep"></div>
                <div class="statusbar-item">
                    <span id="sb-run-state">Idle</span>
                </div>
                <div class="statusbar-right">
                    <span style="font-size:var(--text-xs);color:var(--text-muted);">G4 Studio</span>
                </div>
            </div>
        </div>

        <!-- Template panel (slide-in) -->
        <div class="template-panel" id="template-panel">
            <div style="display:flex;align-items:center;justify-content:space-between;padding:12px;border-bottom:1px solid var(--border-color);">
                <span style="font-size:var(--text-sm);font-weight:var(--font-semibold);">Load Template</span>
                <button class="btn btn-ghost btn-icon btn-sm" id="close-template-panel">✕</button>
            </div>
            <div style="flex:1;overflow-y:auto;padding:8px;" id="template-list-container">
                <div class="page-loader"><div class="spinner spinner-sm"></div></div>
            </div>
        </div>
    `;

    // ---- Mount canvas page ----
    const canvasHost = container.querySelector('#canvas-host');
    const sbCanvas   = container.querySelector('#sb-canvas-status');

    mountCanvas(canvasHost).then(({ isPlaceholder }) => {
        if (sbCanvas) {
            sbCanvas.textContent = isPlaceholder
                ? 'Canvas: placeholder'
                : 'Canvas: loaded';
        }
        addLogEntry('info', isPlaceholder ? 'Canvas placeholder loaded.' : 'Canvas page loaded.');
    });

    // ---- Wire controls ----

    container.querySelector('#wf-back-btn')?.addEventListener('click', () => navigate('/dashboard'));

    container.querySelector('#wf-name-input')?.addEventListener('change', (e) => {
        setWorkflowName(e.target.value.trim() || 'Untitled Workflow');
    });

    container.querySelector('#wf-log-toggle')?.addEventListener('click', toggleLogSidebar);

    container.querySelector('#wf-clear-logs')?.addEventListener('click', () => {
        clearLogs();
    });

    container.querySelector('#wf-templates-btn')?.addEventListener('click', () => {
        const panel = container.querySelector('#template-panel');
        panel?.classList.toggle('open');
        if (panel?.classList.contains('open')) _loadTemplateList(container);
    });

    container.querySelector('#close-template-panel')?.addEventListener('click', () => {
        container.querySelector('#template-panel')?.classList.remove('open');
    });

    // ---- Subscribe to workflow store ----
    _unsubWorkflow = onWorkflowState(state => {
        _syncState(container, state);
    });

    // Initial render of log entries
    _renderLogEntries(container, state.logEntries);

    return {
        destroy() {
            _unsubWorkflow?.();
            _unsubWorkflow = null;
            unmountCanvas(container.querySelector('#canvas-host'));
        }
    };
}

/** Sync UI with workflow state changes */
function _syncState(container, state) {
    const logSidebar  = container.querySelector('#log-sidebar');
    const statusBadge = container.querySelector('#wf-status-badge');
    const sbRunState  = container.querySelector('#sb-run-state');
    const nameInput   = container.querySelector('#wf-name-input');

    if (logSidebar) logSidebar.classList.toggle('collapsed', !state.logSidebarOpen);

    if (nameInput && document.activeElement !== nameInput) {
        nameInput.value = state.workflowName;
    }

    const stateLabel = { idle: 'Ready', running: 'Running', stopped: 'Stopped', error: 'Error' };
    const badgeClass = { idle: 'badge-info', running: 'badge-success', stopped: 'badge-warning', error: 'badge-danger' };

    if (statusBadge) {
        statusBadge.textContent = stateLabel[state.runState] || state.runState;
        statusBadge.className   = `badge workflow-status-badge ${badgeClass[state.runState] || 'badge-info'}`;
    }
    if (sbRunState) sbRunState.textContent = stateLabel[state.runState] || state.runState;

    _renderLogEntries(container, state.logEntries);
}

/** Render log entries into the log sidebar */
function _renderLogEntries(container, entries) {
    const el = container.querySelector('#log-entries');
    if (!el) return;
    if (!entries.length) {
        el.innerHTML = '<div style="color:var(--text-muted);font-size:var(--text-xs);padding:12px;">No log entries yet.</div>';
        return;
    }
    el.innerHTML = entries.map(e => `
        <div class="log-entry log-${e.level}">
            <span class="log-entry-time">${escapeHtml(e.time)}</span>
            <span class="log-entry-text">${escapeHtml(e.text)}</span>
        </div>
    `).join('');
    // Auto-scroll to bottom
    el.scrollTop = el.scrollHeight;
}

/** Load templates into the slide-in panel */
async function _loadTemplateList(container) {
    const listEl = container.querySelector('#template-list-container');
    if (!listEl) return;
    listEl.innerHTML = '<div class="page-loader" style="min-height:80px;"><div class="spinner spinner-sm"></div></div>';

    try {
        const templates = await getAllTemplates();
        const list = Array.isArray(templates) ? templates : Object.values(templates || {});
        if (!list.length) {
            listEl.innerHTML = '<div style="color:var(--text-muted);font-size:var(--text-xs);padding:12px;">No templates found.</div>';
            return;
        }
        listEl.innerHTML = list.map(t => `
            <div class="nav-item" data-key="${escapeHtml(t.key || t.name || '')}"
                 style="height:auto;min-height:36px;flex-direction:column;align-items:flex-start;padding:8px 12px;cursor:pointer;border-bottom:1px solid var(--border-color);">
                <span style="font-size:var(--text-sm);font-weight:var(--font-medium);">${escapeHtml(t.key || t.name || 'Unknown')}</span>
                ${t.summary ? `<span style="font-size:var(--text-xs);color:var(--text-muted);">${escapeHtml(String(t.summary).slice(0, 80))}</span>` : ''}
            </div>
        `).join('');

        // Clicking a template sends its key to the canvas via postMessage
        listEl.addEventListener('click', (e) => {
            const item = e.target.closest('[data-key]');
            if (item) {
                sendToCanvas({ type: 'load-template', key: item.dataset.key });
                container.querySelector('#template-panel')?.classList.remove('open');
                showToast('info', 'Template selected', `Sent "${item.dataset.key}" to canvas.`);
            }
        });
    } catch (err) {
        listEl.innerHTML = `<div style="color:var(--color-danger);font-size:var(--text-xs);padding:12px;">Failed to load templates.</div>`;
    }
}
