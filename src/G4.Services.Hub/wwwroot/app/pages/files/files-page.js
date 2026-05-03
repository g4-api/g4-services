/**
 * G4 Studio — Files Page.
 * Lists static files in wwwroot and SVG files from the backend.
 */

import { listAllFiles, listSvgFiles } from '../../services/files-service.js';
import { showLoader, showError }      from '../../components/app-loader.js';
import { escapeHtml }                 from '../../utils/dom.js';

export async function render(container) {
    container.innerHTML = `
        <div class="page-wrapper">
            <div class="page-header">
                <div>
                    <div class="page-title">Files</div>
                    <div class="page-subtitle">Static files and SVG assets served by the backend</div>
                </div>
                <div class="page-actions">
                    <button class="btn btn-ghost btn-sm" id="refresh-btn">Refresh</button>
                </div>
            </div>

            <div class="tabs" id="files-tabs" style="margin-bottom:16px;">
                <div class="tab active" data-tab="files">All Files</div>
                <div class="tab" data-tab="svgs">SVG Files</div>
            </div>

            <div class="toolbar">
                <div class="search-input-wrap">
                    <span class="search-icon">⌕</span>
                    <input type="search" id="file-search" placeholder="Filter files…" />
                </div>
                <span style="font-size:var(--text-sm);color:var(--text-muted);" id="file-count"></span>
            </div>

            <div id="files-body"></div>
        </div>
    `;

    let _activeTab = 'files';
    let _files = [];
    let _svgs = {};

    const load = async () => {
        const body = container.querySelector('#files-body');
        showLoader(body, 'Loading files…');
        try {
            if (_activeTab === 'files') {
                _files = await listAllFiles();
                if (!Array.isArray(_files)) _files = [];
                _renderFiles(container, _files);
            } else {
                _svgs = await listSvgFiles();
                if (!_svgs || typeof _svgs !== 'object') _svgs = {};
                _renderSvgs(container, _svgs);
            }
        } catch (err) {
            showError(body, err.message, load);
        }
    };

    container.querySelector('#refresh-btn')?.addEventListener('click', load);

    container.querySelector('#files-tabs')?.addEventListener('click', (e) => {
        const tab = e.target.closest('[data-tab]');
        if (!tab) return;
        container.querySelectorAll('.tab').forEach(t => t.classList.toggle('active', t === tab));
        _activeTab = tab.dataset.tab;
        load();
    });

    const searchInput = container.querySelector('#file-search');
    searchInput?.addEventListener('input', () => {
        const q = searchInput.value.toLowerCase();
        if (_activeTab === 'files') {
            _renderFiles(container, _files.filter(f => f.toLowerCase().includes(q)));
        } else {
            const filtered = Object.fromEntries(
                Object.entries(_svgs).filter(([k, v]) => k.toLowerCase().includes(q) || v.toLowerCase().includes(q))
            );
            _renderSvgs(container, filtered);
        }
    });

    await load();
}

function _renderFiles(container, files) {
    const body  = container.querySelector('#files-body');
    const count = container.querySelector('#file-count');
    if (count) count.textContent = `${files.length} file${files.length !== 1 ? 's' : ''}`;

    if (!files.length) {
        body.innerHTML = `<div class="empty-state"><div class="empty-state-icon">⊟</div><div class="empty-state-title">No files found</div></div>`;
        return;
    }

    body.innerHTML = `
        <div class="data-table-wrap">
            <table class="data-table">
                <thead><tr><th>Path</th><th>Extension</th></tr></thead>
                <tbody>
                    ${files.map(f => {
                        const ext = f.split('.').pop().toLowerCase();
                        return `
                            <tr>
                                <td style="font-family:var(--font-mono);font-size:var(--text-xs);">${escapeHtml(f)}</td>
                                <td><span class="badge">${escapeHtml(ext)}</span></td>
                            </tr>
                        `;
                    }).join('')}
                </tbody>
            </table>
        </div>
    `;
}

function _renderSvgs(container, svgs) {
    const body    = container.querySelector('#files-body');
    const count   = container.querySelector('#file-count');
    const entries = Object.entries(svgs);
    if (count) count.textContent = `${entries.length} SVG${entries.length !== 1 ? 's' : ''}`;

    if (!entries.length) {
        body.innerHTML = `<div class="empty-state"><div class="empty-state-icon">◻</div><div class="empty-state-title">No SVG files found</div></div>`;
        return;
    }

    body.innerHTML = `
        <div class="data-table-wrap">
            <table class="data-table">
                <thead><tr><th>Name</th><th>Path</th></tr></thead>
                <tbody>
                    ${entries.map(([name, path]) => `
                        <tr>
                            <td>${escapeHtml(name)}</td>
                            <td style="font-family:var(--font-mono);font-size:var(--text-xs);color:var(--text-muted);">${escapeHtml(path)}</td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
        </div>
    `;
}
