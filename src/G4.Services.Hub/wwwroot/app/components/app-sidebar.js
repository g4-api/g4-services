/**
 * G4 Studio — Sidebar Component.
 * Renders the main navigation sidebar and keeps it in sync with route and state changes.
 * Handles collapse/expand, active item highlighting, and user avatar.
 */

import { routes, NAV_SECTIONS } from '../router/routes.js';
import { navigate, getCurrentRoute } from '../router/router.js';
import { getAppState, setSidebarCollapsed, onAppState } from '../stores/app-store.js';
import { getAuthState, logout } from '../stores/auth-store.js';
import { on, EVENTS } from '../utils/events.js';
import { escapeHtml } from '../utils/dom.js';

/** Nav items derived from routes that have a nav property */
const NAV_ITEMS = routes.filter(r => r.nav);

/**
 * Mount the sidebar into a container element.
 * @param {HTMLElement} container
 * @returns {{ destroy: function }}
 */
export function mountSidebar(container) {
    let _collapsed = getAppState().sidebarCollapsed;
    let _currentPath = getCurrentRoute().path;

    // Build initial HTML
    container.innerHTML = _buildSidebar(_collapsed, _currentPath);

    // Wire collapse button
    const collapseBtn = container.querySelector('.sidebar-collapse-btn');
    collapseBtn?.addEventListener('click', () => {
        _collapsed = !_collapsed;
        setSidebarCollapsed(_collapsed);
        _refresh();
    });

    // Wire nav items (event delegation)
    container.addEventListener('click', (e) => {
        const item = e.target.closest('.nav-item[data-href]');
        if (item) {
            e.preventDefault();
            navigate(item.dataset.href);
        }
        const logoutBtn = e.target.closest('[data-action="logout"]');
        if (logoutBtn) {
            logout();
        }
    });

    // Listen for state changes
    const unsubApp   = onAppState(state => {
        _collapsed = state.sidebarCollapsed;
        _refresh();
    });
    const unsubRoute = on(EVENTS.ROUTE_CHANGED, ({ path }) => {
        _currentPath = path;
        _refresh();
    });

    function _refresh() {
        const sidebar = container.querySelector('.app-sidebar');
        if (!sidebar) { container.innerHTML = _buildSidebar(_collapsed, _currentPath); return; }

        // Toggle collapsed class
        sidebar.classList.toggle('collapsed', _collapsed);

        // Update active items
        container.querySelectorAll('.nav-item[data-href]').forEach(item => {
            const active = item.dataset.href === _currentPath;
            item.classList.toggle('active', active);
        });

        // Flip collapse icon
        const btn = container.querySelector('.sidebar-collapse-btn');
        if (btn) btn.textContent = _collapsed ? '›' : '‹';
    }

    return {
        destroy() {
            unsubApp();
            unsubRoute();
        }
    };
}

/**
 * Build the sidebar HTML string.
 */
function _buildSidebar(collapsed, currentPath) {
    const user = getAuthState().user;
    const colClass = collapsed ? ' collapsed' : '';

    const sectionsHtml = NAV_SECTIONS.map(section => {
        const items = NAV_ITEMS.filter(r => r.nav.section === section.id);
        if (!items.length) return '';
        return `
            <div class="nav-section">
                <div class="nav-section-label">${escapeHtml(section.label)}</div>
                ${items.map(r => _buildNavItem(r, currentPath)).join('')}
            </div>
        `;
    }).join('');

    const userInitials = _initials(user?.name || user?.username || '?');
    const userName     = escapeHtml(user?.name || user?.username || 'User');
    const userRole     = escapeHtml(user?.role || '');

    return `
        <aside class="app-sidebar${colClass}" aria-label="Main navigation">
            <div class="sidebar-header">
                <div class="sidebar-logo">
                    <div class="sidebar-logo-mark">G4</div>
                    <span class="sidebar-logo-text">G4 Studio</span>
                </div>
                <button class="sidebar-collapse-btn" title="${collapsed ? 'Expand' : 'Collapse'} sidebar">
                    ${collapsed ? '›' : '‹'}
                </button>
            </div>

            <nav class="sidebar-nav" aria-label="Navigation">
                ${sectionsHtml}
            </nav>

            <div class="sidebar-footer">
                <div class="nav-item" data-label="User" style="height:40px;gap:var(--space-5);cursor:default;">
                    <div class="sidebar-user-avatar">${escapeHtml(userInitials)}</div>
                    <div style="flex:1;min-width:0;overflow:hidden;">
                        <div class="truncate" style="font-size:var(--text-sm);font-weight:var(--font-medium);color:var(--text-primary);">${userName}</div>
                        <div class="truncate" style="font-size:var(--text-xs);color:var(--text-muted);">${userRole}</div>
                    </div>
                </div>
                <div class="nav-item" data-action="logout" data-label="Sign Out" style="color:var(--text-muted);">
                    <span class="nav-item-icon">⏻</span>
                    <span class="nav-item-label">Sign Out</span>
                </div>
            </div>
        </aside>
    `;
}

function _buildNavItem(route, currentPath) {
    const active = route.path === currentPath ? ' active' : '';
    const label  = escapeHtml(route.nav.label);
    const icon   = escapeHtml(route.nav.icon);
    return `
        <a class="nav-item${active}"
           data-href="${escapeHtml(route.path)}"
           data-label="${label}"
           href="#${escapeHtml(route.path)}"
           aria-current="${route.path === currentPath ? 'page' : 'false'}">
            <span class="nav-item-icon">${icon}</span>
            <span class="nav-item-label">${label}</span>
        </a>
    `;
}

function _initials(name) {
    return name.trim().split(/\s+/).map(w => w[0].toUpperCase()).slice(0, 2).join('');
}
