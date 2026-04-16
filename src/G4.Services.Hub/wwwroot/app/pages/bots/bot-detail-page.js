/**
 * G4 Studio — Bot Detail Page.
 */

import { getBot, testBot, unregisterBot } from '../../services/bots-service.js';
import { navigate }        from '../../router/router.js';
import { showToast }       from '../../stores/app-store.js';
import { openConfirm }     from '../../components/app-modal.js';
import { showLoader, showError } from '../../components/app-loader.js';
import { escapeHtml }      from '../../utils/dom.js';
import { BOT_STATUS }      from '../../utils/constants.js';

export async function render(container, params) {
    const id = params?.id ? decodeURIComponent(params.id) : null;

    container.innerHTML = `
        <div class="page-wrapper">
            <div class="page-header">
                <div>
                    <button class="btn btn-ghost btn-sm" id="back-btn" style="margin-bottom:8px;">‹ Bots</button>
                    <div class="page-title" id="bot-name">Bot Detail</div>
                </div>
                <div class="page-actions">
                    <button class="btn btn-ghost btn-sm" id="test-btn">Test Connectivity</button>
                    <button class="btn btn-danger btn-sm" id="unregister-btn">Unregister</button>
                </div>
            </div>
            <div id="bot-body"></div>
        </div>
    `;

    container.querySelector('#back-btn')?.addEventListener('click', () => navigate('/bots'));

    if (!id) {
        container.querySelector('#bot-body').innerHTML = `<div class="empty-state"><div class="empty-state-title">No bot ID provided.</div></div>`;
        return;
    }

    container.querySelector('#test-btn')?.addEventListener('click', async () => {
        const btn = container.querySelector('#test-btn');
        btn.disabled = true;
        try {
            const result = await testBot(id);
            const status = result?.status || 'Unknown';
            showToast(status === BOT_STATUS.ONLINE ? 'success' : 'warning', `Bot ${status}`, id);
            render(container, params);
        } catch (err) {
            showToast('error', 'Test failed', err.message);
        } finally {
            btn.disabled = false;
        }
    });

    container.querySelector('#unregister-btn')?.addEventListener('click', () => {
        openConfirm({
            title: 'Unregister Bot',
            message: `Unregister bot "${id}"? It will need to re-connect to appear again.`,
            confirmLabel: 'Unregister',
            onConfirm: async () => {
                try {
                    await unregisterBot(id);
                    showToast('success', 'Bot unregistered', id);
                    navigate('/bots');
                } catch (err) {
                    showToast('error', 'Failed', err.message);
                }
            }
        });
    });

    const body = container.querySelector('#bot-body');
    showLoader(body, 'Loading bot details…');

    try {
        const bot = await getBot(id);
        container.querySelector('#bot-name').textContent = bot.name || bot.machineName || id;
        _renderBot(body, bot);
    } catch (err) {
        showError(body, err.message, () => render(container, params));
    }
}

function _statusKey(status) {
    const s = (status || '').toLowerCase();
    if (s === 'online'  || s === 'ready')              return 'ready';
    if (s === 'working' || s === 'busy')               return 'busy';
    if (s === 'offline' || s === 'removed')            return 'offline';
    if (s === 'idle')                                  return 'idle';
    return 'unknown';
}

function _fmtDateTime(val) {
    if (!val) return '—';
    try { return new Date(val).toLocaleString(); } catch { return val; }
}

function _fmtUpTime(createdOn, lastModifiedOn) {
    const start = createdOn ? new Date(createdOn) : null;
    const end   = lastModifiedOn ? new Date(lastModifiedOn) : new Date();
    if (!start || isNaN(start)) return '—';
    const ms = end - start;
    if (ms < 0) return '—';
    const h = Math.floor(ms / 3600000);
    const m = Math.floor((ms % 3600000) / 60000);
    const s = Math.floor((ms % 60000) / 1000);
    return `${h}h ${m}m ${s}s`;
}

function _detailItem(label, value) {
    const v = value == null || value === '' ? '—' : value;
    return `<div class="bot-detail">
        <span class="bot-detail__label">${escapeHtml(label)}</span>
        <span class="bot-detail__value" title="${escapeHtml(String(v))}">${escapeHtml(String(v))}</span>
    </div>`;
}

function _renderBot(container, bot) {
    const status = bot.status || 'Unknown';
    const sk     = _statusKey(status);
    const hasConn = !!bot.connectionId;

    container.innerHTML = `
        <div class="grid-2" style="gap:var(--space-8);align-items:start;">
            <div class="bot-card${sk === 'ready' ? ' bot-card--active' : sk === 'offline' ? ' bot-card--error' : ''}">
                <div class="bot-card__header">
                    <span class="bot-card__indicator bot-card__indicator--${sk}" aria-hidden="true"></span>
                    <span class="bot-card__name" title="${escapeHtml(bot.id || '')}">${escapeHtml(bot.name || bot.id || 'Unknown')}</span>
                    <span class="bot-status-badge bot-status-badge--${sk}">${escapeHtml(status)}</span>
                    <span class="bot-card__meta">
                        ${escapeHtml(bot.machine || '—')}<span class="meta-sep">·</span>${escapeHtml(bot.type || '—')}
                    </span>
                    <span class="bot-card__time" title="${escapeHtml(bot.lastModifiedOn || bot.createdOn || '')}">
                        ${escapeHtml(_fmtDateTime(bot.lastModifiedOn || bot.createdOn))}
                    </span>
                </div>
                <div class="bot-card__body-inner">
                    <div class="bot-details">
                        ${_detailItem('ID',          bot.id)}
                        ${_detailItem('Type',        bot.type)}
                        ${_detailItem('Machine',     bot.machine)}
                        ${_detailItem('OS',          bot.osVersion)}
                        ${_detailItem('Connection',  bot.connectionId || (hasConn ? '—' : 'Not connected'))}
                        ${_detailItem('Callback',    bot.callbackUri  || '—')}
                        ${_detailItem('Created',     _fmtDateTime(bot.createdOn))}
                        ${_detailItem('Last update', _fmtDateTime(bot.lastModifiedOn))}
                        ${_detailItem('Up time',     _fmtUpTime(bot.createdOn, bot.lastModifiedOn))}
                    </div>
                </div>
            </div>
            <div class="card">
                <div class="card-header"><span class="card-title">Raw Data</span></div>
                <div class="card-body">
                    <div class="code-block">${escapeHtml(JSON.stringify(bot, null, 4))}</div>
                </div>
            </div>
        </div>
    `;
}
