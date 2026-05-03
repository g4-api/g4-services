/* ============================================================
   G4™ Cache Dashboard — Page Script
   API contract: /swagger/cache/docs.json
   ============================================================ */

'use strict';

// ── API layer ──────────────────────────────────────────────────

const API = '/api/v4/g4/integration/cache';

const G4CacheApi = {
    async getCache() {
        const r = await fetch(API);
        if (!r.ok) throw new Error(`HTTP ${r.status} — ${r.statusText}`);
        return r.json();
    },

    async getCredentials() {
        const r = await fetch(`${API}/credentials`);
        if (!r.ok) throw new Error(`HTTP ${r.status}`);
        return r.json();
    },

    /** GET /sync — synchronize internal cache only, returns 204 */
    async syncInternal() {
        const r = await fetch(`${API}/sync`);
        if (r.status !== 204) throw new Error(`Unexpected status ${r.status}`);
    },

    /**
     * POST /sync — synchronize with external repositories and/or MCP servers.
     * @param {CacheSyncModel} model — { repositories?: G4ExternalRepositoryModel[], servers?: Record<string,McpServerModel> }
     */
    async syncExternal(model) {
        const r = await fetch(`${API}/sync`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(model)
        });
        if (r.status !== 204) throw new Error(`Unexpected status ${r.status}`);
    }
};

// ── Utilities ──────────────────────────────────────────────────

function esc(s) {
    if (s == null) return '';
    return String(s)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

function debounce(fn, ms) {
    let t;
    return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); };
}

function pluginSource(model) {
    if (model.modelContextServer) return 'mcp';
    if (model.externalRepository)  return 'external';
    if ((model.manifest?.source || '').toLowerCase() === 'template') return 'template';
    return 'internal';
}

function sourceLabel(src) {
    if (src === 'mcp')      return 'MCP';
    if (src === 'external') return 'External';
    if (src === 'template') return 'Template';
    return 'Internal';
}

function joinArr(val) {
    if (Array.isArray(val)) return val.join(' ');
    return val || '';
}

// ── Page controller ────────────────────────────────────────────

class CachePage {
    constructor() {
        /** @type {Record<string,Record<string,object>>|null} */
        this._cache      = null;
        this._creds      = null;
        this._lastSync   = null;
        this._selected   = null;   // { typeName, name, model }
        this._filter     = '';     // 'internal' | 'mcp' | 'external' | ''
        this._search     = '';
        this._tab        = 'overview';
    }

    // ── Boot ──────────────────────────────────────────────────

    init() {
        this._bindTopbarScroll();
        this._bindActions();
        this._bindSearch();
        this._load();
    }

    // ── Data loading ──────────────────────────────────────────

    async _load() {
        this._setReadiness('loading');
        this._showState('Loading');

        const [cacheResult, credsResult] = await Promise.allSettled([
            G4CacheApi.getCache(),
            G4CacheApi.getCredentials()
        ]);

        if (cacheResult.status === 'rejected') {
            this._setReadiness('error');
            this._showState('Error', cacheResult.reason?.message);
            return;
        }

        this._cache    = cacheResult.value;
        this._creds    = credsResult.status === 'fulfilled' ? credsResult.value : null;
        this._lastSync = new Date();

        this._setReadiness('ready');
        this._updateChips();
        this._updateCards();
        this._renderDomains();
        this._hideState();
    }

    // ── Readiness + chips ─────────────────────────────────────

    _setReadiness(state) {
        const el    = document.getElementById('readinessIndicator');
        const label = document.getElementById('readinessLabel');
        el.className = `cache-conn cache-conn--${state}`;
        label.textContent = state === 'ready' ? 'Ready' : state === 'error' ? 'Error' : 'Loading\u2026';
    }

    _stats() {
        let total = 0, mcp = 0, ext = 0, internal = 0, template = 0;
        if (this._cache) {
            for (const plugins of Object.values(this._cache)) {
                const aliasNames = new Set(
                    Object.values(plugins).flatMap(m => (m.manifest?.aliases || []).map(a => a.toLowerCase()))
                );
                for (const [name, model] of Object.entries(plugins)) {
                    if (aliasNames.has(name.toLowerCase())) continue;
                    total++;
                    const src = pluginSource(model);
                    if (src === 'mcp')           mcp++;
                    else if (src === 'external') ext++;
                    else if (src === 'template') template++;
                    else                         internal++;
                }
            }
        }
        const types = this._cache ? Object.keys(this._cache).length : 0;
        const creds = !this._creds ? 0 :
            Array.isArray(this._creds) ? this._creds.length :
            (typeof this._creds === 'object' ? Object.keys(this._creds).length : 0);
        return { total, types, mcp, ext, internal, template, creds };
    }

    _updateChips() {
        const s = this._stats();
        this._setText('chipTotal',    s.total);
        this._setText('chipTypes',    s.types);
        this._setText('chipTemplate', s.template);
        this._setText('chipMcp',      s.mcp);
        this._setText('chipExt',      s.ext);
        this._setText('chipCreds',    s.creds);
    }

    _updateCards() {
        const s = this._stats();
        this._setText('cardTotal',    s.total);
        this._setText('cardTypes',    s.types);
        this._setText('cardInternal', s.internal);
        this._setText('cardTemplate', s.template);
        this._setText('cardMcp',      s.mcp);
        this._setText('cardExt',      s.ext);
        this._setText('cardCreds',    s.creds);
        this._setText('cardSync',     this._lastSync ? this._lastSync.toLocaleTimeString() : '—');
    }

    _setText(id, val) {
        const el = document.getElementById(id);
        if (el) el.textContent = val ?? '—';
    }

    // ── State panels ─────────────────────────────────────────

    _showState(name, message) {
        document.querySelectorAll('.cache-state').forEach(el => el.classList.add('hidden'));
        const target = document.getElementById(`state${name}`);
        if (!target) return;
        target.classList.remove('hidden');
        if (message) {
            const msg = target.querySelector('.state-message');
            if (msg) msg.textContent = message;
        }
    }

    _hideState() {
        document.querySelectorAll('.cache-state').forEach(el => el.classList.add('hidden'));
    }

    // ── Domain rendering ──────────────────────────────────────

    _renderDomains() {
        const container = document.getElementById('cacheDomains');
        if (!this._cache) { container.innerHTML = ''; return; }

        const term   = this._search.toLowerCase();
        const filter = this._filter;
        const types  = Object.keys(this._cache).sort();

        const fragment = document.createDocumentFragment();
        let rendered = 0;

        types.forEach((typeName, idx) => {
            const plugins  = this._cache[typeName];
            // Build a set of all alias names in this type group so aliases are never
            // rendered as first-class entries (they exist for backward compatibility only).
            const aliasNames = new Set(
                Object.values(plugins).flatMap(m => (m.manifest?.aliases || []).map(a => a.toLowerCase()))
            );

            const entries  = Object.entries(plugins).filter(([name, model]) => {
                if (aliasNames.has(name.toLowerCase())) return false;
                if (filter && pluginSource(model) !== filter) return false;
                if (!term) return true;
                const manifest = model.manifest || {};
                return (
                    name.toLowerCase().includes(term) ||
                    joinArr(manifest.summary).toLowerCase().includes(term) ||
                    joinArr(manifest.description).toLowerCase().includes(term) ||
                    (manifest.namespace || '').toLowerCase().includes(term)
                );
            });

            if (!entries.length) return;

            fragment.appendChild(this._buildDomainSection(typeName, entries, idx));
            rendered++;
        });

        container.innerHTML = '';
        if (rendered === 0) {
            container.innerHTML = '<div class="cache-empty">No capabilities match the current filter.</div>';
        } else {
            container.appendChild(fragment);
        }
    }

    _buildDomainSection(typeName, entries, animIdx) {
        const counts = { internal: 0, mcp: 0, external: 0 };
        entries.forEach(([, m]) => counts[pluginSource(m)]++);

        const mixHtml = [
            counts.internal ? `<span class="badge badge--internal">${counts.internal} internal</span>` : '',
            counts.mcp      ? `<span class="badge badge--mcp">${counts.mcp} mcp</span>` : '',
            counts.external ? `<span class="badge badge--external">${counts.external} ext</span>` : ''
        ].join('');

        const section = document.createElement('div');
        section.className = 'cache-domain';
        section.dataset.domainType = typeName;
        section.style.animationDelay = `${animIdx * 0.03}s`;

        section.innerHTML = `
            <div class="cache-domain__header" role="button" tabindex="0" aria-expanded="true">
                <svg class="cache-domain__chevron" viewBox="0 0 640 640" fill="currentColor" aria-hidden="true">
                    <path d="M342.6 441.4C336.4 447.5 328.2 451 320 451C311.8 451 303.6 447.5 297.4 441.4L105.4 249.4C93.1 237.2 93.1 217.3 105.4 205.1C117.6 192.8 137.5 192.8 149.7 205.1L320 375.3L490.3 205.1C502.5 192.8 522.4 192.8 534.6 205.1C546.9 217.3 546.9 237.2 534.6 249.4L342.6 441.4z"/>
                </svg>
                <span class="cache-domain__name">${esc(typeName)}</span>
                <span class="cache-domain__count">${entries.length}</span>
                <div class="cache-domain__mix">${mixHtml}</div>
            </div>
            <div class="cache-domain__body">
                <div class="cache-plugin-list" role="list">
                    ${entries.map(([name, model]) => this._buildPluginRow(typeName, name, model)).join('')}
                </div>
            </div>`;

        // Expand / collapse toggle
        const header  = section.querySelector('.cache-domain__header');
        const body    = section.querySelector('.cache-domain__body');
        const chevron = section.querySelector('.cache-domain__chevron');

        const toggle = () => {
            const expanded = header.getAttribute('aria-expanded') === 'true';
            header.setAttribute('aria-expanded', String(!expanded));
            body.classList.toggle('cache-domain__body--collapsed', expanded);
            chevron.classList.toggle('cache-domain__chevron--collapsed', expanded);
        };

        header.addEventListener('click', toggle);
        header.addEventListener('keydown', e => {
            if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggle(); }
        });

        // Plugin row clicks
        section.querySelectorAll('.cache-plugin-row').forEach(row => {
            row.addEventListener('click', () => {
                const model = this._cache[row.dataset.domainType]?.[row.dataset.plugin];
                if (model) this._selectPlugin(row.dataset.domainType, row.dataset.plugin, model, row);
            });
            row.addEventListener('keydown', e => {
                if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); row.click(); }
            });
        });

        return section;
    }

    _buildPluginRow(typeName, name, model) {
        const src     = pluginSource(model);
        const manifest = model.manifest || {};
        const summary  = joinArr(manifest.summary).slice(0, 120) || '';
        const version  = manifest.version || '';

        return `
            <div class="cache-plugin-row"
                 role="listitem button"
                 tabindex="0"
                 data-plugin="${esc(name)}"
                 data-domain-type="${esc(typeName)}"
                 title="${esc(name)}">
                <span class="cache-plugin-row__name">${esc(name)}</span>
                <span class="cache-plugin-row__summary">${esc(summary)}</span>
                <div class="cache-plugin-row__meta">
                    <span class="badge badge--${src}">${sourceLabel(src)}</span>
                </div>
            </div>`;
    }

    // ── Inspector ─────────────────────────────────────────────

    _selectPlugin(typeName, name, model, rowEl) {
        document.querySelectorAll('.cache-plugin-row--active')
            .forEach(r => r.classList.remove('cache-plugin-row--active'));
        rowEl?.classList.add('cache-plugin-row--active');

        this._selected = { typeName, name, model };

        // Ensure split shows inspector column
        const split = document.getElementById('cacheSplit');
        split.classList.add('cache-split--with-inspector');

        const inspector = document.getElementById('cacheInspector');
        inspector.classList.remove('hidden');

        // Populate header
        document.getElementById('inspectorTitle').textContent = name;
        document.getElementById('inspectorType').textContent  = typeName;
        const src = pluginSource(model);
        document.getElementById('inspectorSource').innerHTML =
            `<span class="badge badge--${src}">${sourceLabel(src)}</span>`;

        // Reset to overview tab
        this._renderInspectorTab('overview');
    }

    _closeInspector() {
        document.getElementById('cacheInspector').classList.add('hidden');
        document.getElementById('cacheSplit').classList.remove('cache-split--with-inspector');
        document.querySelectorAll('.cache-plugin-row--active')
            .forEach(r => r.classList.remove('cache-plugin-row--active'));
        this._selected = null;
    }

    _renderInspectorTab(tab) {
        this._tab = tab;
        document.querySelectorAll('.inspector-tab').forEach(t => {
            t.classList.toggle('inspector-tab--active', t.dataset.tab === tab);
            t.setAttribute('aria-selected', String(t.dataset.tab === tab));
        });

        if (!this._selected) return;
        const { model } = this._selected;
        const body = document.getElementById('inspectorBody');

        switch (tab) {
            case 'overview':   body.innerHTML = this._tabOverview(model);    break;
            case 'parameters': body.innerHTML = this._tabParameters(model);  break;
            case 'examples':   body.innerHTML = this._tabExamples(model);    break;
            case 'raw':        body.innerHTML = this._tabRaw(model);         break;
        }
    }

    // ── Inspector tab content ─────────────────────────────────

    _tabOverview(model) {
        const m    = model.manifest || {};
        const sum  = joinArr(m.summary);
        const desc = Array.isArray(m.description)
            ? m.description.join('\n')
            : (m.description || '');

        const sections = [
            this._infoSection('Identity', [
                ['Version',     m.version],
                ['Namespace',   m.namespace],
                ['Capability Type', m.pluginType],
                ['Rule Type',   m.ruleType],
                ['Source',      m.source],
                ['Author',      this._authorLink(m.author)],
                ['Project',     m.projectUrl ? `<a href="${esc(m.projectUrl)}" target="_blank" rel="noopener">${esc(m.projectUrl)}</a>` : null]
            ]),
            this._infoSection('Classification', [
                ['Categories', (m.categories||[]).join(', ')],
                ['Aliases',    (m.aliases||[]).join(', ')],
                ['Platforms',  (m.platforms||[]).join(', ')]
            ]),
        ];

        if (model.externalRepository) {
            const r = model.externalRepository;
            sections.push(this._infoSection('External Repository', [
                ['Name',    r.name],
                ['URL',     r.url ? `<a href="${esc(r.url)}" target="_blank" rel="noopener">${esc(r.url)}</a>` : null],
                ['Version', r.version],
                ['Timeout', r.timeout ? `${r.timeout}s` : null]
            ]));
        }

        if (model.modelContextServer) {
            const s = model.modelContextServer;
            sections.push(this._infoSection('MCP Server', [
                ['Command', s.command],
                ['URL',     s.url],
                ['Type',    s.type],
                ['Args',    (s.args||[]).join(' ')]
            ]));
        }

        return `
            ${sum  ? `<p class="inspector-summary">${esc(sum)}</p>` : ''}
            ${desc ? `<div class="inspector-desc inspector-desc--md">${marked.parse(desc)}</div>` : ''}
            ${sections.join('')}`;
    }

    _authorLink(author) {
        if (!author) return null;
        if (author.link) return `<a href="${esc(author.link)}" target="_blank" rel="noopener">${esc(author.name)}</a>`;
        return esc(author.name) || null;
    }

    _infoSection(title, pairs) {
        const rows = pairs
            .filter(([, v]) => v != null && v !== '')
            .map(([label, val]) => `
                <div class="inspector-kv">
                    <span class="inspector-kv__label">${esc(label)}</span>
                    <span class="inspector-kv__value">${val}</span>
                </div>`).join('');

        if (!rows) return '';
        return `
            <div class="inspector-section">
                <div class="inspector-section__title">${esc(title)}</div>
                ${rows}
            </div>`;
    }

    _tabParameters(model) {
        const params = (model.manifest?.parameters || []);
        if (!params.length) return '<p class="inspector-empty">No parameters defined.</p>';

        const rows = params.map(p => {
            const req = p.mandatory
                ? '<span class="badge badge--required">required</span>'
                : '<span class="badge">optional</span>';
            const desc = joinArr(p.description);
            return `
                <tr>
                    <td class="inspector-table__name">${esc(p.displayName || p.name || '')}</td>
                    <td><code>${esc(p.type || '—')}</code></td>
                    <td>${req}</td>
                    <td>${esc(p.default || '—')}</td>
                    <td class="inspector-table__desc">${esc(desc)}</td>
                </tr>`;
        }).join('');

        return `
            <table class="inspector-table">
                <thead>
                    <tr>
                        <th>Name</th><th>Type</th><th>Required</th>
                        <th>Default</th><th>Description</th>
                    </tr>
                </thead>
                <tbody>${rows}</tbody>
            </table>`;
    }

    _tabExamples(model) {
        const examples = (model.manifest?.examples || []);
        if (!examples.length) return '<p class="inspector-empty">No examples defined.</p>';

        return examples.map((ex, i) => {
            const desc = joinArr(ex.description);
            const rule = ex.rule ? JSON.stringify(ex.rule, null, 2) : '';
            return `
                <div class="inspector-example">
                    <div class="inspector-example__num">Example ${i + 1}</div>
                    ${desc ? `<p class="inspector-example__desc">${esc(desc)}</p>` : ''}
                    ${rule ? `<pre class="inspector-raw inspector-raw--sm">${esc(rule)}</pre>` : ''}
                </div>`;
        }).join('');
    }

    _tabRaw(model) {
        return `<pre class="inspector-raw">${esc(JSON.stringify(model, null, 4))}</pre>`;
    }

    // ── Actions ───────────────────────────────────────────────

    async _handleRefresh() {
        const btn = document.getElementById('btnRefresh');
        btn.disabled = true;
        await this._load();
        btn.disabled = false;
    }

    async _handleSyncInternal() {
        const btn = document.getElementById('btnSyncInternal');
        this._btnBusy(btn, 'Syncing\u2026');
        try {
            await G4CacheApi.syncInternal();
            this._toast('Internal cache synchronized.', 'success');
            await this._load();
        } catch (e) {
            this._toast(`Sync failed: ${e.message}`, 'error');
        } finally {
            this._btnReset(btn, 'Sync Internal');
        }
    }

    async _submitSyncExternal() {
        const name    = document.getElementById('extName').value.trim();
        const url     = document.getElementById('extUrl').value.trim();
        const version = parseInt(document.getElementById('extVersion').value, 10) || 1;
        const timeout = parseFloat(document.getElementById('extTimeout').value) || undefined;

        if (!name || !url) { this._toast('Name and URL are required.', 'error'); return; }

        const repo = { name, url, version };
        if (timeout) repo.timeout = timeout;

        const username = document.getElementById('extUsername').value.trim();
        const password = document.getElementById('extPassword').value.trim();
        const token    = document.getElementById('extToken').value.trim();
        if (username || password || token) {
            repo.credentials = {};
            if (username) repo.credentials.username = username;
            if (password) repo.credentials.password = password;
            if (token)    repo.credentials.token    = token;
        }

        const headers  = this._collectKv('extHeaders');
        const caps     = this._collectKv('extCapabilities');
        if (Object.keys(headers).length) repo.headers      = headers;
        if (Object.keys(caps).length)    repo.capabilities = caps;

        const btn = document.getElementById('btnSubmitSyncExt');
        this._btnBusy(btn, 'Syncing\u2026');
        try {
            await G4CacheApi.syncExternal({ repositories: [repo], servers: {} });
            this._toast('External repository synchronized.', 'success');
            this._hideDialog('dlgSyncExternal');
            await this._load();
        } catch (e) {
            this._toast(`Failed: ${e.message}`, 'error');
        } finally {
            this._btnReset(btn, 'Sync');
        }
    }

    async _submitAddMcp() {
        const serverName = document.getElementById('mcpServerName').value.trim();
        if (!serverName) { this._toast('Server name is required.', 'error'); return; }

        const command = document.getElementById('mcpCommand').value.trim();
        const url     = document.getElementById('mcpUrl').value.trim();
        const type    = document.getElementById('mcpType').value;
        const argsRaw = document.getElementById('mcpArgs').value.trim();

        const server = {};
        if (command) server.command = command;
        if (url)     server.url     = url;
        if (type)    server.type    = type;
        if (argsRaw) server.args    = argsRaw.split(/\s+/).filter(Boolean);

        const env     = this._collectKv('mcpEnv');
        const headers = this._collectKv('mcpHeaders');
        if (Object.keys(env).length)     server.env     = env;
        if (Object.keys(headers).length) server.headers = headers;

        const btn = document.getElementById('btnSubmitAddMcp');
        this._btnBusy(btn, 'Adding\u2026');
        try {
            await G4CacheApi.syncExternal({ repositories: [], servers: { [serverName]: server } });
            this._toast('MCP server registered and synchronized.', 'success');
            this._hideDialog('dlgAddMcp');
            await this._load();
        } catch (e) {
            this._toast(`Failed: ${e.message}`, 'error');
        } finally {
            this._btnReset(btn, 'Add MCP');
        }
    }

    _showDevView() {
        const pre = document.getElementById('devViewRaw');
        pre.textContent = this._cache
            ? JSON.stringify(this._cache, null, 2)
            : 'No cache data loaded.';
        this._showDialog('dlgDevView');
    }

    _copyRaw() {
        const text = document.getElementById('devViewRaw').textContent;
        navigator.clipboard.writeText(text)
            .then(() => this._toast('Copied to clipboard.', 'success'))
            .catch(() => this._toast('Copy failed.', 'error'));
    }

    _copyDiagnostics() {
        if (!this._cache) { this._toast('No cache data to copy.', 'error'); return; }
        const s = this._stats();
        const diag = {
            timestamp:      new Date().toISOString(),
            lastSynced:     this._lastSync?.toISOString() ?? null,
            pluginTypes:    s.types,
            totalPlugins:   s.total,
            internalPlugins: s.internal,
            mcpPlugins:     s.mcp,
            externalPlugins: s.ext,
            credentials:    s.creds
        };
        navigator.clipboard.writeText(JSON.stringify(diag, null, 2))
            .then(() => this._toast('Diagnostics copied.', 'success'))
            .catch(() => this._toast('Copy failed.', 'error'));
    }

    // ── Expand / collapse ─────────────────────────────────────

    _expandAll(expand) {
        document.querySelectorAll('.cache-domain__header').forEach(header => {
            const body    = header.nextElementSibling;
            const chevron = header.querySelector('.cache-domain__chevron');
            header.setAttribute('aria-expanded', String(expand));
            body.classList.toggle('cache-domain__body--collapsed', !expand);
            chevron.classList.toggle('cache-domain__chevron--collapsed', !expand);
        });
    }

    // ── Dialog helpers ────────────────────────────────────────

    _showDialog(id) { document.getElementById(id)?.classList.remove('hidden'); }
    _hideDialog(id) { document.getElementById(id)?.classList.add('hidden'); }

    // ── KV list helpers ───────────────────────────────────────

    _addKvRow(listId) {
        const list = document.getElementById(listId);
        if (!list) return;
        const row = document.createElement('div');
        row.className = 'kv-row';
        row.innerHTML = `
            <input type="text" class="form-input kv-key"   placeholder="Key" />
            <input type="text" class="form-input kv-value" placeholder="Value" />
            <button type="button" class="kv-remove" aria-label="Remove">
                <svg viewBox="0 0 640 640" width="12" height="12">
                    <path d="M491.3 171.7C504.8 158.2 504.8 136.1 491.3 122.7C477.8 109.2 455.7 109.2 442.3 122.7L320 244.9L197.7 122.7C184.3 109.2 162.2 109.2 148.7 122.7C135.2 136.1 135.2 158.2 148.7 171.7L270.9 293.9L148.7 416.2C135.2 429.6 135.2 451.7 148.7 465.2C162.2 478.6 184.3 478.6 197.7 465.2L320 342.9L442.3 465.2C455.7 478.6 477.8 478.6 491.3 465.2C504.8 451.7 504.8 429.6 491.3 416.2L369.1 293.9L491.3 171.7z"/>
                </svg>
            </button>`;
        row.querySelector('.kv-remove').addEventListener('click', () => row.remove());
        list.appendChild(row);
    }

    _collectKv(listId) {
        const list = document.getElementById(listId);
        if (!list) return {};
        const out = {};
        list.querySelectorAll('.kv-row').forEach(row => {
            const k = row.querySelector('.kv-key')?.value.trim();
            const v = row.querySelector('.kv-value')?.value.trim() ?? '';
            if (k) out[k] = v;
        });
        return out;
    }

    // ── Button state helpers ──────────────────────────────────

    _btnBusy(btn, label) {
        if (!btn) return;
        btn.disabled = true;
        const span = btn.querySelector('span');
        if (span) span.textContent = label;
        else btn.textContent = label;
    }

    _btnReset(btn, label) {
        if (!btn) return;
        btn.disabled = false;
        const span = btn.querySelector('span');
        if (span) span.textContent = label;
        else btn.textContent = label;
    }

    // ── Toast ─────────────────────────────────────────────────

    _toast(message, type = 'info') {
        const container = document.getElementById('toastContainer');
        const el = document.createElement('div');
        el.className = `cache-toast cache-toast--${type}`;
        el.textContent = message;
        container.appendChild(el);
        requestAnimationFrame(() => el.classList.add('cache-toast--visible'));
        setTimeout(() => {
            el.classList.remove('cache-toast--visible');
            setTimeout(() => el.remove(), 300);
        }, 3500);
    }

    // ── Event binding ─────────────────────────────────────────

    _bindTopbarScroll() {
        const topbar = document.getElementById('cacheStickyTop');
        window.addEventListener('scroll', () => {
            topbar.classList.toggle('cache-sticky-top--scrolled', window.scrollY > 8);
        }, { passive: true });
    }

    _bindActions() {
        const on = (id, fn) => document.getElementById(id)?.addEventListener('click', fn.bind(this));

        on('btnRefresh',       this._handleRefresh);
        on('btnSyncInternal',  this._handleSyncInternal);
        on('btnSyncExternal',  () => this._showDialog('dlgSyncExternal'));
        on('btnAddMcp',        () => this._showDialog('dlgAddMcp'));
        on('btnExpandAll',     () => this._expandAll(true));
        on('btnCollapseAll',   () => this._expandAll(false));
        on('btnDevView',       this._showDevView);
        on('btnCopyDiag',      this._copyDiagnostics);
        on('btnCloseInspector', this._closeInspector);
        on('btnRetry',         this._load);

        // Dialogs
        on('btnCancelSyncExt', () => this._hideDialog('dlgSyncExternal'));
        on('btnSubmitSyncExt', this._submitSyncExternal);
        on('btnCancelAddMcp',  () => this._hideDialog('dlgAddMcp'));
        on('btnSubmitAddMcp',  this._submitAddMcp);
        on('btnCancelDevView', () => this._hideDialog('dlgDevView'));
        on('btnCopyRaw',       this._copyRaw);

        // KV add buttons
        on('btnAddExtHeader',  () => this._addKvRow('extHeaders'));
        on('btnAddExtCap',     () => this._addKvRow('extCapabilities'));
        on('btnAddMcpEnv',     () => this._addKvRow('mcpEnv'));
        on('btnAddMcpHeader',  () => this._addKvRow('mcpHeaders'));

        // Inspector tabs
        document.querySelectorAll('.inspector-tab').forEach(tab => {
            tab.addEventListener('click', () => this._renderInspectorTab(tab.dataset.tab));
        });

        // Close dialogs on overlay click or Escape
        ['dlgSyncExternal', 'dlgAddMcp', 'dlgDevView'].forEach(id => {
            const overlay = document.getElementById(id);
            overlay?.addEventListener('click', e => {
                if (e.target === overlay) this._hideDialog(id);
            });
        });

        document.addEventListener('keydown', e => {
            if (e.key !== 'Escape') return;
            ['dlgSyncExternal', 'dlgAddMcp', 'dlgDevView'].forEach(id => this._hideDialog(id));
        });
    }

    _bindSearch() {
        const input = document.getElementById('cacheSearchInput');
        input?.addEventListener('input', debounce(() => {
            this._search = input.value.trim();
            this._renderDomains();
        }, 180));

        document.querySelectorAll('.filter-chip').forEach(chip => {
            chip.addEventListener('click', () => {
                const alreadyActive = chip.classList.contains('filter-chip--active');
                document.querySelectorAll('.filter-chip').forEach(c => c.classList.remove('filter-chip--active'));
                this._filter = alreadyActive ? '' : chip.dataset.filter;
                if (!alreadyActive) chip.classList.add('filter-chip--active');
                this._renderDomains();
            });
        });
    }
}

// ── Boot ───────────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', () => {
    const page = new CachePage();
    page.init();
});
