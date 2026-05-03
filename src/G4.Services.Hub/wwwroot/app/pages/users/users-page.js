/**
 * G4 Studio — Users Page (Mock).
 * User management is mock-only. No real backend persistence.
 */

import { getAllUsers, createUser, updateUser, toggleUserActive, deleteUser, AVAILABLE_ROLES }
    from '../../services/users-service.js';
import { showToast }      from '../../stores/app-store.js';
import { openModal, closeModal, openConfirm } from '../../components/app-modal.js';
import { showLoader }     from '../../components/app-loader.js';
import { escapeHtml }     from '../../utils/dom.js';
import { formatDate }     from '../../utils/formatters.js';

export async function render(container) {
    container.innerHTML = `
        <div class="page-wrapper">
            <div class="page-header">
                <div>
                    <div class="page-title">Users</div>
                    <div class="page-subtitle">
                        User management
                        <span class="badge badge-warning" style="margin-left:8px;vertical-align:middle;">Mock</span>
                    </div>
                </div>
                <div class="page-actions">
                    <button class="btn btn-primary" id="create-user-btn">+ New User</button>
                </div>
            </div>
            <div id="users-body"></div>
        </div>
    `;

    container.querySelector('#create-user-btn')?.addEventListener('click', () => {
        _openUserEditor(null, () => _load(container));
    });

    await _load(container);
}

async function _load(container) {
    const body = container.querySelector('#users-body');
    showLoader(body, 'Loading users…');

    const users = await getAllUsers();
    _render(container, users);
}

function _render(container, users) {
    const body = container.querySelector('#users-body');

    if (!users.length) {
        body.innerHTML = `<div class="empty-state"><div class="empty-state-title">No users</div></div>`;
        return;
    }

    body.innerHTML = `
        <div class="data-table-wrap">
            <table class="data-table">
                <thead>
                    <tr>
                        <th>Name</th>
                        <th>Username</th>
                        <th>Email</th>
                        <th>Role</th>
                        <th>Status</th>
                        <th>Created</th>
                        <th class="col-actions">Actions</th>
                    </tr>
                </thead>
                <tbody>
                    ${users.map(u => _row(u)).join('')}
                </tbody>
            </table>
        </div>
    `;

    body.addEventListener('click', async (e) => {
        const id = e.target.closest('[data-id]')?.dataset.id;
        if (!id) return;
        const user = users.find(u => u.id === id);
        if (!user) return;

        if (e.target.closest('[data-action="edit"]')) {
            _openUserEditor(user, () => _load(container));
        }
        if (e.target.closest('[data-action="toggle"]')) {
            try {
                await toggleUserActive(id);
                showToast('success', user.active ? 'User deactivated' : 'User activated');
                _load(container);
            } catch (err) {
                showToast('error', 'Failed', err.message);
            }
        }
        if (e.target.closest('[data-action="delete"]')) {
            openConfirm({
                title: 'Delete User',
                message: `Delete user "${user.name}"? This cannot be undone.`,
                confirmLabel: 'Delete',
                onConfirm: async () => {
                    try {
                        await deleteUser(id);
                        showToast('success', 'User deleted');
                        _load(container);
                    } catch (err) {
                        showToast('error', 'Failed', err.message);
                    }
                }
            });
        }
    });
}

function _row(u) {
    return `
        <tr>
            <td>${escapeHtml(u.name)}</td>
            <td style="font-family:var(--font-mono);font-size:var(--text-xs);">${escapeHtml(u.username)}</td>
            <td style="color:var(--text-secondary);">${escapeHtml(u.email || '—')}</td>
            <td><span class="badge badge-accent">${escapeHtml(u.role)}</span></td>
            <td>
                <span class="badge ${u.active ? 'badge-success' : 'badge-warning'}">
                    <span class="badge-dot"></span>${u.active ? 'Active' : 'Inactive'}
                </span>
            </td>
            <td style="color:var(--text-muted);font-size:var(--text-xs);">${formatDate(u.createdAt, false)}</td>
            <td class="col-actions">
                <div style="display:flex;gap:4px;justify-content:flex-end;">
                    <button class="btn btn-ghost btn-sm" data-id="${escapeHtml(u.id)}" data-action="edit">Edit</button>
                    <button class="btn btn-ghost btn-sm" data-id="${escapeHtml(u.id)}" data-action="toggle">
                        ${u.active ? 'Disable' : 'Enable'}
                    </button>
                    <button class="btn btn-ghost btn-sm" data-id="${escapeHtml(u.id)}" data-action="delete"
                            style="color:var(--color-danger);">Delete</button>
                </div>
            </td>
        </tr>
    `;
}

function _openUserEditor(user, onSave) {
    const isNew = !user;
    const bodyEl = document.createElement('div');
    bodyEl.innerHTML = `
        <div class="form-group">
            <label class="form-label">Name</label>
            <input type="text" id="u-name" value="${escapeHtml(user?.name || '')}" placeholder="Full name" />
        </div>
        <div class="form-group">
            <label class="form-label">Username</label>
            <input type="text" id="u-username" value="${escapeHtml(user?.username || '')}" placeholder="Username" ${!isNew ? 'readonly' : ''} />
        </div>
        <div class="form-group">
            <label class="form-label">Email</label>
            <input type="email" id="u-email" value="${escapeHtml(user?.email || '')}" placeholder="user@example.com" />
        </div>
        <div class="form-group">
            <label class="form-label">Role</label>
            <select id="u-role">
                ${AVAILABLE_ROLES.map(r => `<option value="${r}" ${user?.role === r ? 'selected' : ''}>${r}</option>`).join('')}
            </select>
        </div>
    `;

    openModal({
        title: isNew ? 'New User' : `Edit — ${user.name}`,
        body: bodyEl,
        actions: [
            { label: 'Cancel', variant: 'secondary', onClick: closeModal },
            {
                label: isNew ? 'Create' : 'Save',
                variant: 'primary',
                onClick: async () => {
                    const data = {
                        name:     bodyEl.querySelector('#u-name').value.trim(),
                        username: bodyEl.querySelector('#u-username').value.trim(),
                        email:    bodyEl.querySelector('#u-email').value.trim(),
                        role:     bodyEl.querySelector('#u-role').value,
                    };
                    if (!data.name || !data.username) {
                        showToast('warning', 'Name and username are required');
                        return;
                    }
                    try {
                        if (isNew) await createUser(data);
                        else       await updateUser(user.id, data);
                        closeModal();
                        showToast('success', isNew ? 'User created' : 'User updated');
                        onSave();
                    } catch (err) {
                        showToast('error', 'Failed', err.message);
                    }
                }
            }
        ]
    });
}
