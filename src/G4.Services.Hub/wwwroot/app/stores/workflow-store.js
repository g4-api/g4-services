/**
 * G4 Studio — Workflow Store.
 * Holds UI state for the currently open workflow page:
 * active template/automation, run state, logs, sidebar visibility.
 */

import { emit, EVENTS } from '../utils/events.js';

const _state = {
    /** Key of the currently loaded template/automation */
    activeKey: null,
    /** Display name of the current workflow */
    workflowName: 'Untitled Workflow',
    /** Current run state: 'idle' | 'running' | 'stopped' | 'error' */
    runState: 'idle',
    /** ID of the active automation run (from the backend), or null */
    automationId: null,
    /** Whether the log sidebar is open */
    logSidebarOpen: true,
    /** Log entries [{time, level, text}] */
    logEntries: [],
    /** Template data currently loaded */
    template: null,
};

/** @type {Set<Function>} */
const _listeners = new Set();

function _notify() {
    _listeners.forEach(fn => fn({ ..._state, logEntries: [..._state.logEntries] }));
}

/**
 * Get current workflow state.
 */
export function getWorkflowState() {
    return { ..._state, logEntries: [..._state.logEntries] };
}

/**
 * Subscribe to workflow state changes.
 * @param {function} listener
 * @returns {function} Unsubscribe
 */
export function onWorkflowState(listener) {
    _listeners.add(listener);
    return () => _listeners.delete(listener);
}

/**
 * Load a template/workflow by key.
 * @param {string} key
 * @param {string} [name]
 * @param {object} [template]
 */
export function loadWorkflow(key, name, template = null) {
    _state.activeKey = key;
    _state.workflowName = name || key;
    _state.template = template;
    _state.runState = 'idle';
    _state.automationId = null;
    _state.logEntries = [];
    _notify();
}

/**
 * Set the workflow name (from the inline editable input).
 * @param {string} name
 */
export function setWorkflowName(name) {
    _state.workflowName = name;
    _notify();
}

/**
 * Mark a run as started.
 * @param {string} automationId
 */
export function setRunStarted(automationId) {
    _state.runState = 'running';
    _state.automationId = automationId;
    addLogEntry('info', 'Automation run started.');
    _notify();
    emit(EVENTS.WORKFLOW_RUN_STARTED, { automationId });
}

/**
 * Mark a run as stopped.
 */
export function setRunStopped() {
    _state.runState = 'stopped';
    addLogEntry('warning', 'Automation run stopped by user.');
    _notify();
    emit(EVENTS.WORKFLOW_RUN_STOPPED, { automationId: _state.automationId });
}

/**
 * Mark a run as completed.
 */
export function setRunCompleted() {
    _state.runState = 'idle';
    addLogEntry('success', 'Automation run completed successfully.');
    _notify();
    emit(EVENTS.WORKFLOW_RUN_COMPLETED, { automationId: _state.automationId });
    _state.automationId = null;
}

/**
 * Mark a run as errored.
 * @param {string} message
 */
export function setRunError(message) {
    _state.runState = 'error';
    addLogEntry('error', `Automation error: ${message}`);
    _notify();
}

/**
 * Toggle the log sidebar visibility.
 */
export function toggleLogSidebar() {
    _state.logSidebarOpen = !_state.logSidebarOpen;
    _notify();
}

/**
 * Add a log entry.
 * @param {'info'|'success'|'warning'|'error'} level
 * @param {string} text
 */
export function addLogEntry(level, text) {
    const entry = {
        time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' }),
        level,
        text,
    };
    _state.logEntries.push(entry);
    // Keep last 500 entries
    if (_state.logEntries.length > 500) {
        _state.logEntries = _state.logEntries.slice(-500);
    }
    emit(EVENTS.WORKFLOW_LOG_ENTRY, entry);
    _notify();
}

/**
 * Clear all log entries.
 */
export function clearLogs() {
    _state.logEntries = [];
    _notify();
}
