/**
 * Dependency-free `node --test` suite for `Validators.confirmInputActionPlacement`.
 *
 * The Hub serves `wwwroot/js/validators.js` and `wwwroot/js/utilities.js` as plain browser scripts that declare the
 * global `Validators` and `Utilities` classes. Instead of changing their module format, this suite runs both files
 * in one `node:vm` context, the way the canvas page loads them, and reads the classes back from that context.
 *
 * Run it from the repository root with Node.js 18 or later; no package install is needed:
 *
 * ```bash
 * node --test src/G4.Services.Hub/test/validators.test.js
 * ```
 */
'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { describe, it } = require('node:test');
const vm = require('node:vm');

// The scripts under test, in the order the canvas page loads them.
const SCRIPT_DIRECTORY = path.join(__dirname, '..', 'wwwroot', 'js');
const SCRIPT_NAMES = ['utilities.js', 'validators.js'];

// The plugin key of an Input Actions Group and the plugin type of an input action, as the plugin manifests name them.
const GROUP_PLUGIN_NAME = 'InvokeInputActions';
const INPUT_ACTION_TYPE = 'InputAction';

// The error-bucket keys the validator records its findings under.
const EMPTY_KEY = 'inputActionsGroupEmpty';
const FOREIGN_KEY = 'inputActionsGroupStep';
const MISPLACED_KEY = 'inputActionPlacement';

// The class under test, read once, because the scripts hold no state between calls.
const Validators = importValidators();

// The suite and test callbacks stay inline, because `node:test` reads each test's name and body as one unit.
describe('Validators.confirmInputActionPlacement', () => {
    it('accepts an input action directly inside an Input Actions Group', () => {
        // Arrange: place the input action as the only step of a group.
        const inputAction = newInputAction('Add Key Down');
        const definition = newDefinition([newGroup([inputAction])]);

        // Act: validate the input action's placement.
        const isValid = Validators.confirmInputActionPlacement(inputAction, definition);

        // Assert: the placement passes and records no message.
        assert.equal(isValid, true);
        assert.deepEqual(Object.keys(inputAction.context.errors), []);
    });

    it('rejects an input action outside an Input Actions Group and names the step', () => {
        // Arrange: place the input action directly inside the Job.
        const inputAction = newInputAction('Add Pointer Move');
        const definition = newDefinition([inputAction]);

        // Act: validate the placement and read the recorded message.
        const isValid = Validators.confirmInputActionPlacement(inputAction, definition);
        const message = inputAction.context.errors[MISPLACED_KEY];

        // Assert: the placement fails with an error that names the step.
        assert.equal(isValid, false);
        assert.equal(message.level, 'ERR');
        assert.equal(message.title, 'Invalid Input Action Placement');
        assert.match(message.description, /"Add Pointer Move" step/);
    });

    it('names an unnamed misplaced input action generically instead of "undefined"', () => {
        // Arrange: a misplaced input action without a display name.
        const inputAction = newInputAction('');
        const definition = newDefinition([inputAction]);

        // Act: validate the placement and read the message description.
        Validators.confirmInputActionPlacement(inputAction, definition);
        const { description } = inputAction.context.errors[MISPLACED_KEY];

        // Assert: the message falls back to a generic name.
        assert.match(description, /"input action" step/);
        assert.doesNotMatch(description, /undefined/);
    });

    it('matches the input action plugin type case-insensitively', () => {
        // Arrange: a misplaced input action whose plugin type is upper case.
        const inputAction = newStep({ name: 'Add Pause', pluginName: 'AddPause', pluginType: 'INPUTACTION' });
        const definition = newDefinition([inputAction]);

        // Act: validate the placement.
        const isValid = Validators.confirmInputActionPlacement(inputAction, definition);

        // Assert: the step still counts as an input action and fails.
        assert.equal(isValid, false);
        assert.ok(inputAction.context.errors[MISPLACED_KEY]);
    });

    it('rejects an input action nested deeper than directly inside a group', () => {
        // Arrange: wrap the input action in another container inside the group.
        const inputAction = newInputAction('Add Key Down');
        const wrapper = newStep({ name: 'Wrapper', pluginName: 'Wrap', pluginType: 'Action', sequence: [inputAction] });
        const definition = newDefinition([newGroup([wrapper])]);

        // Act: validate the placement.
        const isValid = Validators.confirmInputActionPlacement(inputAction, definition);

        // Assert: only a direct group parent counts.
        assert.equal(isValid, false);
        assert.ok(inputAction.context.errors[MISPLACED_KEY]);
    });

    it('rejects a step that is no input action inside an Input Actions Group', () => {
        // Arrange: place a click inside a group.
        const click = newStep({ name: 'Click', pluginName: 'InvokeClick', pluginType: 'Action' });
        const definition = newDefinition([newGroup([click])]);

        // Act: validate the placement and read the recorded message.
        const isValid = Validators.confirmInputActionPlacement(click, definition);
        const message = click.context.errors[FOREIGN_KEY];

        // Assert: the foreign step fails with an error.
        assert.equal(isValid, false);
        assert.equal(message.level, 'ERR');
        assert.equal(message.title, 'Invalid Step in Input Actions Group');
    });

    it('warns about an empty Input Actions Group but keeps it valid', () => {
        // Arrange: a group without steps.
        const group = newGroup([]);
        const definition = newDefinition([group]);

        // Act: validate the group and read the recorded message.
        const isValid = Validators.confirmInputActionPlacement(group, definition);
        const message = group.context.errors[EMPTY_KEY];

        // Assert: the group stays valid and carries a warning only.
        assert.equal(isValid, true);
        assert.equal(message.level, 'WRN');
        assert.equal(message.title, 'Empty Input Actions Group');
    });

    it('treats a group without a sequence array as empty', () => {
        // Arrange: a group step that has no sequence at all.
        const group = newStep({ name: 'Input Actions Group', pluginName: GROUP_PLUGIN_NAME, pluginType: 'Action' });
        const definition = newDefinition([group]);

        // Act: validate the group.
        const isValid = Validators.confirmInputActionPlacement(group, definition);

        // Assert: the missing sequence is warned about like an empty one.
        assert.equal(isValid, true);
        assert.equal(group.context.errors[EMPTY_KEY].level, 'WRN');
    });

    it('accepts a filled Input Actions Group without any message', () => {
        // Arrange: a group that holds one input action.
        const group = newGroup([newInputAction('Add Key Down')]);
        const definition = newDefinition([group]);

        // Act: validate the group.
        const isValid = Validators.confirmInputActionPlacement(group, definition);

        // Assert: the group passes and records no message.
        assert.equal(isValid, true);
        assert.deepEqual(Object.keys(group.context.errors), []);
    });

    it('ignores a step that is neither an input action, a group, nor inside a group', () => {
        // Arrange: a click directly inside the Job.
        const click = newStep({ name: 'Click', pluginName: 'InvokeClick', pluginType: 'Action' });
        const definition = newDefinition([click]);

        // Act: validate the click.
        const isValid = Validators.confirmInputActionPlacement(click, definition);

        // Assert: the validator leaves unrelated steps alone.
        assert.equal(isValid, true);
        assert.deepEqual(Object.keys(click.context.errors), []);
    });

    it('clears an earlier placement error once the input action is moved into a group', () => {
        // Arrange: record the misplacement error first.
        const inputAction = newInputAction('Add Key Down');
        const group = newGroup([]);
        const definition = newDefinition([inputAction, group]);
        Validators.confirmInputActionPlacement(inputAction, definition);

        // Arrange: move the step into the group, as a designer drag does.
        definition.sequence[0].sequence[0].sequence = [group];
        group.sequence.push(inputAction);

        // Act: validate the new placement.
        const isValid = Validators.confirmInputActionPlacement(inputAction, definition);

        // Assert: the old error is gone.
        assert.equal(isValid, true);
        assert.equal(inputAction.context.errors[MISPLACED_KEY], undefined);
    });

    it('keeps errors recorded by other validators on the step', () => {
        // Arrange: a valid input action that already carries another validator's error.
        const inputAction = newInputAction('Add Key Down');
        inputAction.context = { errors: { rulePlacement: { level: 'ERR' } } };
        const definition = newDefinition([newGroup([inputAction])]);

        // Act: validate the placement.
        Validators.confirmInputActionPlacement(inputAction, definition);

        // Assert: only the validator's own keys are touched.
        assert.deepEqual(Object.keys(inputAction.context.errors), ['rulePlacement']);
    });
});

/**
 * Runs the browser scripts in a fresh `vm` context and returns their `Validators` class.
 *
 * @remarks
 * Owns an explicit context: a class declared at the top level of a script is a lexical binding, not a property of
 * the global object, so the class is read back by evaluating its name inside the same context. The sandbox is an
 * empty object, because the validator reaches only `Utilities`, which the context defines itself.
 *
 * @returns {Function} The `Validators` class defined by `validators.js`.
 */
function importValidators() {
    const context = vm.createContext({});

    // Run each script as the browser does, so later scripts see the classes the earlier ones declared.
    for (const scriptName of SCRIPT_NAMES) {
        const scriptPath = path.join(SCRIPT_DIRECTORY, scriptName);
        const source = fs.readFileSync(scriptPath, 'utf8');
        vm.runInContext(source, context, { filename: scriptPath });
    }

    // Read the lexical class binding back from the context the scripts ran in.
    return vm.runInContext('Validators', context);
}

/**
 * Wraps steps in a designer definition, placing them inside one Job of one Stage.
 *
 * @remarks
 * Compute-only: the definition is built from the given steps alone.
 *
 * @param {Object[]} steps - The steps to place inside the Job.
 * @returns {Object} The designer definition.
 */
function newDefinition(steps) {
    // Nest the steps the way the designer does, so the parent lookup walks a real Stage and Job.
    const job = newStep({ name: 'Job', pluginName: 'G4™ Job', pluginType: 'Container', sequence: steps });
    const stage = newStep({ name: 'Stage', pluginName: 'G4™ Stage', pluginType: 'Container', sequence: [job] });

    return { sequence: [stage] };
}

/**
 * Creates an Input Actions Group that holds the given steps.
 *
 * @remarks
 * Compute-only: the group is built from the given steps alone.
 *
 * @param {Object[]} sequence - The group's child steps.
 * @returns {Object} The group step.
 */
function newGroup(sequence) {
    return newStep({ name: 'Input Actions Group', pluginName: GROUP_PLUGIN_NAME, pluginType: 'Action', sequence });
}

/**
 * Creates an input action step, such as Add Key Down.
 *
 * @remarks
 * Compute-only: the step is built from the given name alone.
 *
 * @param {string} name - The step's display name; an empty name exercises the validator's fallback.
 * @returns {Object} The input action step.
 */
function newInputAction(name) {
    return newStep({ name, pluginName: 'AddKeyDown', pluginType: INPUT_ACTION_TYPE });
}

/**
 * Creates a designer step with the fields the validator reads.
 *
 * @remarks
 * Compute-only: the step is built from the given options alone.
 *
 * @param {Object}   options            - The step fields.
 * @param {string}   options.name       - The step's display name.
 * @param {string}   options.pluginName - The step's plugin key.
 * @param {string}   options.pluginType - The step's plugin type.
 * @param {Object[]} [options.sequence] - The child steps of a container step; omitted for a plain step.
 * @returns {Object} The step.
 */
function newStep(options) {
    const { name, pluginName, pluginType, sequence } = options;
    const step = { name, pluginName, pluginType };

    // Only container steps carry a sequence, as in the designer definition.
    if (sequence) {
        step.sequence = sequence;
    }

    return step;
}
