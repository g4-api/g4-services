/**
 * G4 Studio — Environments Page.
 * View and manage G4 environments and their parameters.
 */

import { getAllEnvironments, upsertParameter, deleteEnvironment, deleteParameter }
    from '../../services/environments-service.js';
import { showToast }      from '../../stores/app-store.js';
import { openModal, closeModal, openConfirm } from '../../components/app-modal.js';
import { showLoader, showError } from '../../components/app-loader.js';
import { escapeHtml }     from '../../utils/dom.js';

export async function render(container) {
    container.innerHTML = `
        <div class="page-wrapper">
            <div class="page-header">
                <div>
                    <div class="page-title">Environments</div>
                    <div class="page-subtitle">Manage environment parameter stores</div>
                </div>
                <div class="page-actions">
                    <button class="btn btn-ghost btn-sm" id="refresh-btn">Refresh</button>
                </div>
            </div>
            <div id="env-body"></div>
        </div>
    `;

    const load = () => _load(container);
    container.querySelector('#refresh-btn')?.addEventListener('click', load);
    await load();
}

async function _load(container) {
    const body = container.querySelector('#env-body');
    showLoader(body, 'Loading environments…');

    let envs;
    try {
        envs = await getAllEnvironments();
        if (typeof envs !== 'object') envs = {};
    } catch (err) {
        showError(body, err.message, () => _load(container));
        return;
    }

    const names = Object.keys(envs);

    if (!names.length) {
        body.innerHTML = `
            <div class="empty-state">
                <div class="empty-state-icon">⊗</div>
                <div class="empty-state-title">No environments found</div>
                <div class="empty-state-message">Environments will appear here once configured.</div>
            </div>`;
        return;
    }

    body.innerHTML = `
        <div style="display:flex;flex-direction:column;gap:var(--space-8);">
            ${names.map(name => _envCard(name, envs[name])).join('')}
        </div>
    `;

    // Wire edit/delete actions
    body.addEventListener('click', (e) => {
        const envName   = e.target.closest('[data-env]')?.dataset.env;
        const paramName = e.target.closest('[data-param]')?.dataset.param;

        if (!envName) return;

        if (e.target.closest('[data-action="edit-param"]') && paramName) {
            _openParamEditor(envName, paramName, envs[envName]?.[paramName], () => _load(container));
        }
        if (e.target.closest('[data-action="add-param"]')) {
            _openParamEditor(envName, '', '', () => _load(container));
        }
        if (e.target.closest('[data-action="delete-param"]') && paramName) {
            openConfirm({
                title: 'Delete Parameter',
                message: `Remove "${paramName}" from "${envName}"?`,
                confirmLabel: 'Delete',
                onConfirm: async () => {
                    try {
                        await deleteParameter(envName, paramName);
                        showToast('success', 'Parameter deleted');
                        _load(container);
                    } catch (err) {
                        showToast('error', 'Failed', err.message);
                    }
                }
            });
        }
        if (e.target.closest('[data-action="delete-env"]')) {
            openConfirm({
                title: 'Delete Environment',
                message: `Delete environment "${envName}" and all its parameters?`,
                confirmLabel: 'Delete',
                onConfirm: async () => {
                    try {
                        await deleteEnvironment(envName);
                        showToast('success', 'Environment deleted');
                        _load(container);
                    } catch (err) {
                        showToast('error', 'Failed', err.message);
                    }
                }
            });
        }
    });
}

function _envCard(name, params) {
    const entries = Object.entries(params || {});
    return `
        <div class="card" data-env="${escapeHtml(name)}">
            <div class="card-header">
                <span class="card-title">${escapeHtml(name)}</span>
                <div style="display:flex;gap:var(--space-4);">
                    <button class="btn btn-ghost btn-sm" data-env="${escapeHtml(name)}" data-action="add-param">+ Parameter</button>
                    <button class="btn btn-ghost btn-sm" data-env="${escapeHtml(name)}" data-action="delete-env" style="color:var(--color-danger);">Delete</button>
                </div>
            </div>
            <div class="card-body" style="padding:0;">
                ${entries.length === 0
                    ? `<div style="padding:20px;color:var(--text-muted);font-size:var(--text-sm);">No parameters configured.</div>`
                    : `<table class="data-table">
                        <thead>
                            <tr>
                                <th>Parameter</th>
                                <th>Value</th>
                                <th class="col-actions">Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${entries.map(([k, v]) => `
                                <tr>
                                    <td style="font-family:var(--font-mono);font-size:var(--text-xs);">${escapeHtml(k)}</td>
                                    <td style="color:var(--text-secondary);font-family:var(--font-mono);font-size:var(--text-xs);">${escapeHtml(String(v ?? ''))}</td>
                                    <td class="col-actions">
                                        <div style="display:flex;gap:4px;justify-content:flex-end;">
                                            <button class="btn btn-ghost btn-sm" data-env="${escapeHtml(name)}" data-param="${escapeHtml(k)}" data-action="edit-param">Edit</button>
                                            <button class="btn btn-ghost btn-sm" data-env="${escapeHtml(name)}" data-param="${escapeHtml(k)}" data-action="delete-param" style="color:var(--color-danger);">Delete</button>
                                        </div>
                                    </td>
                                </tr>
                            `).join('')}
                        </tbody>
                    </table>`
                }
            </div>
        </div>
    `;
}

function _openParamEditor(envName, paramName, currentValue, onSave) {
    const isNew = !paramName;
    const body = `
        <div class="form-group">
            <label class="form-label">Parameter Name</label>
            <input id="param-name-input" type="text" value="${escapeHtml(paramName)}"
                   placeholder="e.g. ApiKey" ${!isNew ? 'readonly' : ''} />
        </div>
        <div class="form-group">
            <label class="form-label">Value</label>
            <input id="param-value-input" type="text" value="${escapeHtml(String(currentValue || ''))}"
                   placeholder="Parameter value" />
            <span class="form-hint">Values are base64-encoded on the backend.</span>
        </div>
    `;

    const bodyEl = document.createElement('div');
    bodyEl.innerHTML = body;

    openModal({
        title: isNew ? `Add Parameter — ${envName}` : `Edit Parameter — ${paramName}`,
        body: bodyEl,
        actions: [
            { label: 'Cancel', variant: 'secondary', onClick: closeModal },
            {
                label: 'Save', variant: 'primary',
                onClick: async () => {
                    const name  = bodyEl.querySelector('#param-name-input').value.trim();
                    const value = bodyEl.querySelector('#param-value-input').value;
                    if (!name) { showToast('warning', 'Parameter name is required'); return; }
                    try {
                        await upsertParameter(envName, name, value);
                        closeModal();
                        showToast('success', 'Parameter saved');
                        onSave();
                    } catch (err) {
                        showToast('error', 'Save failed', err.message);
                    }
                }
            },
        ]
    });
}
