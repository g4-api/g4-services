/**
 * G4 Studio — Bots List Page.
 * Lists registered bots with status, actions, and connectivity testing.
 */

import { getAllBots, testAllBots, unregisterAllDisconnected } from '../../services/bots-service.js';
import { navigate }         from '../../router/router.js';
import { showToast }        from '../../stores/app-store.js';
import { openConfirm }      from '../../components/app-modal.js';
import { showLoader, showError } from '../../components/app-loader.js';
import { escapeHtml }       from '../../utils/dom.js';
import { BOT_STATUS }       from '../../utils/constants.js';

export async function render(container) {
    container.innerHTML = `
        <div class="page-wrapper">
            <div class="page-header">
                <div>
                    <div class="page-title">Bots</div>
                    <div class="page-subtitle">Connected automation bots</div>
                </div>
                <div class="page-actions">
                    <button class="btn btn-ghost btn-sm" id="test-all-btn">Test All</button>
                    <button class="btn btn-ghost btn-sm" id="cleanup-btn">Remove Disconnected</button>
                    <button class="btn btn-ghost btn-sm" id="refresh-btn">Refresh</button>
                </div>
            </div>

            <div class="toolbar">
                <div class="search-input-wrap">
                    <span class="search-icon">⌕</span>
                    <input type="search" id="bot-search" placeholder="Search bots…" />
                </div>
                <span style="font-size:var(--text-sm);color:var(--text-muted);" id="bot-count"></span>
            </div>

            <div id="bots-body"></div>
        </div>
    `;

    const load = () => _load(container);

    container.querySelector('#refresh-btn')?.addEventListener('click', load);

    container.querySelector('#test-all-btn')?.addEventListener('click', async () => {
        const btn = container.querySelector('#test-all-btn');
        btn.disabled = true;
        btn.textContent = 'Testing…';
        try {
            await testAllBots();
            showToast('success', 'Connectivity test complete');
            await load();
        } catch (err) {
            showToast('error', 'Test failed', err.message);
        } finally {
            btn.disabled = false;
            btn.textContent = 'Test All';
        }
    });

    container.querySelector('#cleanup-btn')?.addEventListener('click', () => {
        openConfirm({
            title: 'Remove Disconnected Bots',
            message: 'This will unregister all bots with a Disconnected status.',
            confirmLabel: 'Remove',
            onConfirm: async () => {
                try {
                    await unregisterAllDisconnected();
                    showToast('success', 'Disconnected bots removed');
                    await load();
                } catch (err) {
                    showToast('error', 'Failed', err.message);
                }
            }
        });
    });

    const searchInput = container.querySelector('#bot-search');
    searchInput?.addEventListener('input', () => _filter(container, searchInput.value));

    await load();
}

async function _load(container) {
    const body = container.querySelector('#bots-body');
    showLoader(body, 'Loading bots…');

    let bots;
    try {
        bots = await getAllBots();
        if (!Array.isArray(bots)) bots = [];
    } catch (err) {
        showError(body, err.message, () => _load(container));
        return;
    }

    container._bots = bots;
    _render(container, bots);
}

function _filter(container, query) {
    const q = query.toLowerCase();
    const filtered = (container._bots || []).filter(b =>
        !q ||
        String(b.id || b.botId || '').toLowerCase().includes(q) ||
        String(b.name || '').toLowerCase().includes(q) ||
        String(b.status || '').toLowerCase().includes(q)
    );
    _render(container, filtered);
    container.querySelector('#bot-search').focus();
}

function _render(container, bots) {
    const body  = container.querySelector('#bots-body');
    const count = container.querySelector('#bot-count');
    if (count) count.textContent = `${bots.length} bot${bots.length !== 1 ? 's' : ''}`;

    if (!bots.length) {
        body.innerHTML = `
            <div class="empty-state">
                <div class="empty-state-icon">⊕</div>
                <div class="empty-state-title">No bots registered</div>
                <div class="empty-state-message">Bots that connect to the G4 backend will appear here.</div>
            </div>`;
        return;
    }

    body.innerHTML = `
        <div class="data-table-wrap">
            <table class="data-table">
                <thead>
                    <tr>
                        <th>Bot ID</th>
                        <th>Name</th>
                        <th>Status</th>
                        <th>Session</th>
                        <th class="col-actions">Actions</th>
                    </tr>
                </thead>
                <tbody>
                    ${bots.map(b => _row(b)).join('')}
                </tbody>
            </table>
        </div>
    `;

    body.addEventListener('click', (e) => {
        const id = e.target.closest('[data-id]')?.dataset.id;
        if (id && e.target.closest('[data-action="view"]')) {
            navigate(`/bots/${encodeURIComponent(id)}`);
        }
    });
}

function _row(b) {
    const id      = b.id || b.botId || '—';
    const name    = b.name || b.machineName || '—';
    const status  = b.status || 'Unknown';
    const session = b.sessionId || b.connectionId || '—';

    const badgeCls = status === BOT_STATUS.ONLINE  ? 'badge-success'
                   : status === BOT_STATUS.OFFLINE ? 'badge-warning'
                   : status === BOT_STATUS.REMOVED ? 'badge-danger'
                   : '';

    return `
        <tr>
            <td style="font-family:var(--font-mono);font-size:var(--text-xs);">${escapeHtml(id)}</td>
            <td>${escapeHtml(name)}</td>
            <td><span class="badge ${badgeCls}"><span class="badge-dot"></span>${escapeHtml(status)}</span></td>
            <td style="font-family:var(--font-mono);font-size:var(--text-xs);color:var(--text-muted);">${escapeHtml(session)}</td>
            <td class="col-actions">
                <button class="btn btn-ghost btn-sm" data-id="${escapeHtml(id)}" data-action="view">Details</button>
            </td>
        </tr>
    `;
}
