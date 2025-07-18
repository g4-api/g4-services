(function (global, factory) {
	typeof exports === 'object' && typeof module !== 'undefined' ? factory(exports) :
		typeof define === 'function' && define.amd ? define(['exports'], factory) :
			(global = typeof globalThis !== 'undefined' ? globalThis : global || self, factory(global.sequentialWorkflowDesigner = {}));
})(this, (function (exports) {
	'use strict';

	class Dom {
		static svg(name, attributes) {
			const element = document.createElementNS('http://www.w3.org/2000/svg', name);
			if (attributes) {
				Dom.attrs(element, attributes);
			}
			return element;
		}
		static translate(element, x, y) {
			element.setAttribute('transform', `translate(${x}, ${y})`);
		}
		static attrs(element, attributes) {
			Object.keys(attributes).forEach(name => {
				const value = attributes[name];
				element.setAttribute(name, typeof value === 'string' ? value : value.toString());
			});
		}
		static element(name, attributes) {
			const element = document.createElement(name);
			if (attributes) {
				Dom.attrs(element, attributes);
			}
			return element;
		}
		static toggleClass(element, isEnabled, className) {
			if (isEnabled) {
				element.classList.add(className);
			}
			else {
				element.classList.remove(className);
			}
		}
	}

	// Source: https://fonts.google.com/icons or https://github.com/google/material-design-icons
	class Icons {
		static appendPath(parent, pathClassName, d, size) {
			const g = Dom.svg('g');
			const scale = size / 48;
			const path = Dom.svg('path', {
				d,
				class: pathClassName,
				transform: `scale(${scale})`
			});
			g.appendChild(path);
			parent.appendChild(g);
			return g;
		}
		static createSvg(className, d) {
			const icon = Dom.svg('svg', {
				class: className,
				viewBox: '0 0 48 48'
			});
			const path = Dom.svg('path', {
				d,
				class: 'sqd-icon-path'
			});
			icon.appendChild(path);
			return icon;
		}
	}
	Icons.folderIn = 'M42.05 42.25H11.996v-7.12h17.388L6 11.746 11.546 6.2 34.93 29.584V12.196h7.12V42.25z';
	Icons.folderOut = 'M6 6.2h30.054v7.12H18.666L42.05 36.704l-5.546 5.546L13.12 18.866v17.388H6V6.2z';
	Icons.center = 'M9 42q-1.2 0-2.1-.9Q6 40.2 6 39v-8.6h3V39h8.6v3Zm21.4 0v-3H39v-8.6h3V39q0 1.2-.9 2.1-.9.9-2.1.9ZM24 31.15q-3.15 0-5.15-2-2-2-2-5.15 0-3.15 2-5.15 2-2 5.15-2 3.15 0 5.15 2 2 2 2 5.15 0 3.15-2 5.15-2 2-5.15 2ZM6 17.6V9q0-1.2.9-2.1Q7.8 6 9 6h8.6v3H9v8.6Zm33 0V9h-8.6V6H39q1.2 0 2.1.9.9.9.9 2.1v8.6Z';
	Icons.zoomIn = 'M39.8 41.95 26.65 28.8q-1.5 1.3-3.5 2.025-2 .725-4.25.725-5.4 0-9.15-3.75T6 18.75q0-5.3 3.75-9.05 3.75-3.75 9.1-3.75 5.3 0 9.025 3.75 3.725 3.75 3.725 9.05 0 2.15-.7 4.15-.7 2-2.1 3.75L42 39.75Zm-20.95-13.4q4.05 0 6.9-2.875Q28.6 22.8 28.6 18.75t-2.85-6.925Q22.9 8.95 18.85 8.95q-4.1 0-6.975 2.875T9 18.75q0 4.05 2.875 6.925t6.975 2.875ZM17.3 24.3v-4.1h-4.1v-3h4.1v-4.05h3v4.05h4.05v3H20.3v4.1Z';
	Icons.zoomOut = 'M39.8 41.95 26.65 28.8q-1.5 1.3-3.5 2.025-2 .725-4.25.725-5.4 0-9.15-3.75T6 18.75q0-5.3 3.75-9.05 3.75-3.75 9.1-3.75 5.3 0 9.025 3.75 3.725 3.75 3.725 9.05 0 2.15-.7 4.15-.7 2-2.1 3.75L42 39.75Zm-20.95-13.4q4.05 0 6.9-2.875Q28.6 22.8 28.6 18.75t-2.85-6.925Q22.9 8.95 18.85 8.95q-4.1 0-6.975 2.875T9 18.75q0 4.05 2.875 6.925t6.975 2.875Zm-5.1-8.35v-3H23.8v3Z';
	Icons.undo = 'M14 38v-3h14.45q3.5 0 6.025-2.325Q37 30.35 37 26.9t-2.525-5.775Q31.95 18.8 28.45 18.8H13.7l5.7 5.7-2.1 2.1L8 17.3 17.3 8l2.1 2.1-5.7 5.7h14.7q4.75 0 8.175 3.2Q40 22.2 40 26.9t-3.425 7.9Q33.15 38 28.4 38Z';
	Icons.redo = 'M19.6 38q-4.75 0-8.175-3.2Q8 31.6 8 26.9t3.425-7.9q3.425-3.2 8.175-3.2h14.7l-5.7-5.7L30.7 8l9.3 9.3-9.3 9.3-2.1-2.1 5.7-5.7H19.55q-3.5 0-6.025 2.325Q11 23.45 11 26.9t2.525 5.775Q16.05 35 19.55 35H34v3Z';
	Icons.move = 'm24 44-8.15-8.15 2.2-2.2 4.45 4.45v-9.45h3v9.45l4.45-4.45 2.2 2.2ZM11.9 31.9 4 24l7.95-7.95 2.2 2.2L9.9 22.5h9.45v3H9.9l4.2 4.2Zm24.2 0-2.2-2.2 4.2-4.2h-9.4v-3h9.4l-4.2-4.2 2.2-2.2L44 24ZM22.5 19.3V9.9l-4.2 4.2-2.2-2.2L24 4l7.9 7.9-2.2 2.2-4.2-4.2v9.4Z';
	Icons.delete = 'm16.5 33.6 7.5-7.5 7.5 7.5 2.1-2.1-7.5-7.5 7.5-7.5-2.1-2.1-7.5 7.5-7.5-7.5-2.1 2.1 7.5 7.5-7.5 7.5ZM24 44q-4.1 0-7.75-1.575-3.65-1.575-6.375-4.3-2.725-2.725-4.3-6.375Q4 28.1 4 24q0-4.15 1.575-7.8 1.575-3.65 4.3-6.35 2.725-2.7 6.375-4.275Q19.9 4 24 4q4.15 0 7.8 1.575 3.65 1.575 6.35 4.275 2.7 2.7 4.275 6.35Q44 19.85 44 24q0 4.1-1.575 7.75-1.575 3.65-4.275 6.375t-6.35 4.3Q28.15 44 24 44Z';
	Icons.folderUp = 'M22.5 34h3V23.75l3.7 3.7 2.1-2.1-7.3-7.3-7.3 7.3 2.1 2.1 3.7-3.7ZM7.05 40q-1.2 0-2.1-.925-.9-.925-.9-2.075V11q0-1.15.9-2.075Q5.85 8 7.05 8h14l3 3h17q1.15 0 2.075.925.925.925.925 2.075v23q0 1.15-.925 2.075Q42.2 40 41.05 40Zm0-29v26h34V14H22.8l-3-3H7.05Zm0 0v26Z';
	Icons.close = 'm12.45 37.65-2.1-2.1L21.9 24 10.35 12.45l2.1-2.1L24 21.9l11.55-11.55 2.1 2.1L26.1 24l11.55 11.55-2.1 2.1L24 26.1Z';
	Icons.options = 'm19.4 44-1-6.3q-.95-.35-2-.95t-1.85-1.25l-5.9 2.7L4 30l5.4-3.95q-.1-.45-.125-1.025Q9.25 24.45 9.25 24q0-.45.025-1.025T9.4 21.95L4 18l4.65-8.2 5.9 2.7q.8-.65 1.85-1.25t2-.9l1-6.35h9.2l1 6.3q.95.35 2.025.925Q32.7 11.8 33.45 12.5l5.9-2.7L44 18l-5.4 3.85q.1.5.125 1.075.025.575.025 1.075t-.025 1.05q-.025.55-.125 1.05L44 30l-4.65 8.2-5.9-2.7q-.8.65-1.825 1.275-1.025.625-2.025.925l-1 6.3ZM24 30.5q2.7 0 4.6-1.9 1.9-1.9 1.9-4.6 0-2.7-1.9-4.6-1.9-1.9-4.6-1.9-2.7 0-4.6 1.9-1.9 1.9-1.9 4.6 0 2.7 1.9 4.6 1.9 1.9 4.6 1.9Z';
	Icons.expand = 'm24 30.75-12-12 2.15-2.15L24 26.5l9.85-9.85L36 18.8Z';
	Icons.alert = 'M24 42q-1.45 0-2.475-1.025Q20.5 39.95 20.5 38.5q0-1.45 1.025-2.475Q22.55 35 24 35q1.45 0 2.475 1.025Q27.5 37.05 27.5 38.5q0 1.45-1.025 2.475Q25.45 42 24 42Zm-3.5-12V6h7v24Z';
	Icons.play = 'M14.75 40.15V7.55l25.6 16.3Z';
	Icons.stop = 'M10.75 37.25V10.7H37.3v26.55Z';
	Icons.folder = 'M7.05 40q-1.2 0-2.1-.925-.9-.925-.9-2.075V11q0-1.15.9-2.075Q5.85 8 7.05 8h14l3 3h17q1.15 0 2.075.925.925.925.925 2.075v23q0 1.15-.925 2.075Q42.2 40 41.05 40Z';

	class ObjectCloner {
		static deepClone(instance) {
			if (typeof window.structuredClone !== 'undefined') {
				return window.structuredClone(instance);
			}
			return JSON.parse(JSON.stringify(instance));
		}
	}

	class Vector {
		constructor(x, y) {
			this.x = x;
			this.y = y;
		}
		add(v) {
			return new Vector(this.x + v.x, this.y + v.y);
		}
		subtract(v) {
			return new Vector(this.x - v.x, this.y - v.y);
		}
		multiplyByScalar(s) {
			return new Vector(this.x * s, this.y * s);
		}
		divideByScalar(s) {
			return new Vector(this.x / s, this.y / s);
		}
		round() {
			return new Vector(Math.round(this.x), Math.round(this.y));
		}
		distance() {
			return Math.sqrt(Math.pow(this.x, 2) + Math.pow(this.y, 2));
		}
	}

	function getAbsolutePosition(element) {
		const rect = element.getBoundingClientRect();
		return new Vector(rect.x + window.scrollX, rect.y + window.scrollY);
	}

	class Uid {
		static next() {
			const bytes = new Uint8Array(16);
			window.crypto.getRandomValues(bytes);
			return Array.from(bytes, v => v.toString(16).padStart(2, '0')).join('');
		}
	}

	class SimpleEvent {
		constructor() {
			this.listeners = [];
			this.forward = (value) => {
				if (this.listeners.length > 0) {
					this.listeners.forEach(listener => listener(value));
				}
			};
		}
		subscribe(listener) {
			this.listeners.push(listener);
		}
		unsubscribe(listener) {
			const index = this.listeners.indexOf(listener);
			if (index >= 0) {
				this.listeners.splice(index, 1);
			}
			else {
				throw new Error('Unknown listener');
			}
		}
		count() {
			return this.listeners.length;
		}
		first() {
			return new Promise(resolve => {
				const handler = (value) => {
					this.unsubscribe(handler);
					resolve(value);
				};
				this.subscribe(handler);
			});
		}
	}

	function race(timeout, a, b, c, d) {
		const value = [undefined, undefined, undefined, undefined];
		const result = new SimpleEvent();
		let scheduled = false;
		function forward() {
			if (scheduled) {
				return;
			}
			scheduled = true;
			setTimeout(() => {
				try {
					result.forward(value);
				}
				finally {
					scheduled = false;
					value.fill(undefined);
				}
			}, timeout);
		}
		function subscribe(event, index) {
			event.subscribe(v => {
				value[index] = v;
				forward();
			});
		}
		subscribe(a, 0);
		subscribe(b, 1);
		if (c) {
			subscribe(c, 2);
		}
		if (d) {
			subscribe(d, 3);
		}
		return result;
	}

	class ControlBarApi {
		static create(state, historyController, stateModifier) {
			const api = new ControlBarApi(state, historyController, stateModifier);
			race(0, state.onIsReadonlyChanged, state.onSelectedStepIdChanged, state.onIsDragDisabledChanged, api.isUndoRedoSupported() ? state.onDefinitionChanged : undefined).subscribe(api.onStateChanged.forward);
			return api;
		}
		constructor(state, historyController, stateModifier) {
			this.state = state;
			this.historyController = historyController;
			this.stateModifier = stateModifier;
			this.onStateChanged = new SimpleEvent();
		}
		isDragDisabled() {
			return this.state.isDragDisabled;
		}
		setIsDragDisabled(isDragDisabled) {
			this.state.setIsDragDisabled(isDragDisabled);
		}
		toggleIsDragDisabled() {
			this.setIsDragDisabled(!this.isDragDisabled());
		}
		isUndoRedoSupported() {
			return !!this.historyController;
		}
		tryUndo() {
			if (this.canUndo() && this.historyController) {
				this.historyController.undo();
				return true;
			}
			return false;
		}
		canUndo() {
			return !!this.historyController && this.historyController.canUndo() && !this.state.isReadonly && !this.state.isDragging;
		}
		tryRedo() {
			if (this.canRedo() && this.historyController) {
				this.historyController.redo();
				return true;
			}
			return false;
		}
		canRedo() {
			return !!this.historyController && this.historyController.canRedo() && !this.state.isReadonly && !this.state.isDragging;
		}
		tryDelete() {
			if (this.canDelete() && this.state.selectedStepId) {
				this.stateModifier.tryDelete(this.state.selectedStepId);
				return true;
			}
			return false;
		}
		canDelete() {
			return (!!this.state.selectedStepId &&
				!this.state.isReadonly &&
				!this.state.isDragging &&
				this.stateModifier.isDeletable(this.state.selectedStepId));
		}
	}

	exports.KeyboardAction = void 0;
	(function (KeyboardAction) {
		KeyboardAction["delete"] = "delete";
	})(exports.KeyboardAction || (exports.KeyboardAction = {}));
	exports.DefinitionChangeType = void 0;
	(function (DefinitionChangeType) {
		DefinitionChangeType[DefinitionChangeType["stepNameChanged"] = 1] = "stepNameChanged";
		DefinitionChangeType[DefinitionChangeType["stepPropertyChanged"] = 2] = "stepPropertyChanged";
		DefinitionChangeType[DefinitionChangeType["stepChildrenChanged"] = 3] = "stepChildrenChanged";
		DefinitionChangeType[DefinitionChangeType["stepDeleted"] = 4] = "stepDeleted";
		DefinitionChangeType[DefinitionChangeType["stepMoved"] = 5] = "stepMoved";
		DefinitionChangeType[DefinitionChangeType["stepInserted"] = 6] = "stepInserted";
		DefinitionChangeType[DefinitionChangeType["rootPropertyChanged"] = 7] = "rootPropertyChanged";
		DefinitionChangeType[DefinitionChangeType["rootReplaced"] = 8] = "rootReplaced";
	})(exports.DefinitionChangeType || (exports.DefinitionChangeType = {}));

	class EditorRenderer {
		static create(state, selectedStepIdProvider, definitionWalker, handler) {
			const raceEvent = race(0, state.onDefinitionChanged, selectedStepIdProvider.onSelectedStepIdChanged, state.onIsReadonlyChanged);
			const listener = new EditorRenderer(state, selectedStepIdProvider, definitionWalker, handler, raceEvent);
			raceEvent.subscribe(listener.raceEventHandler);
			listener.renderIfStepChanged(selectedStepIdProvider.selectedStepId);
			return listener;
		}
		constructor(state, selectedStepIdProvider, definitionWalker, handler, raceEvent) {
			this.state = state;
			this.selectedStepIdProvider = selectedStepIdProvider;
			this.definitionWalker = definitionWalker;
			this.handler = handler;
			this.raceEvent = raceEvent;
			this.currentStepId = undefined;
			this.raceEventHandler = ([definitionChanged, selectedStepId, isReadonlyChanged]) => {
				if (isReadonlyChanged !== undefined) {
					this.render(this.selectedStepIdProvider.selectedStepId);
				}
				else if (definitionChanged) {
					if (definitionChanged.changeType === exports.DefinitionChangeType.rootReplaced) {
						this.render(this.selectedStepIdProvider.selectedStepId);
					}
					else {
						this.renderIfStepChanged(this.selectedStepIdProvider.selectedStepId);
					}
				}
				else if (selectedStepId !== undefined) {
					this.renderIfStepChanged(selectedStepId);
				}
			};
		}
		destroy() {
			this.raceEvent.unsubscribe(this.raceEventHandler);
		}
		render(stepId) {
			const step = stepId ? this.definitionWalker.getById(this.state.definition, stepId) : null;
			this.currentStepId = stepId;
			this.handler(step);
		}
		renderIfStepChanged(stepId) {
			if (this.currentStepId !== stepId) {
				this.render(stepId);
			}
		}
	}

	class EditorApi {
		constructor(state, definitionWalker, stateModifier) {
			this.state = state;
			this.definitionWalker = definitionWalker;
			this.stateModifier = stateModifier;
		}
		isCollapsed() {
			return this.state.isEditorCollapsed;
		}
		isReadonly() {
			return this.state.isReadonly;
		}
		toggleIsCollapsed() {
			this.state.setIsEditorCollapsed(!this.state.isEditorCollapsed);
		}
		subscribeIsCollapsed(listener) {
			this.state.onIsEditorCollapsedChanged.subscribe(listener);
		}
		getDefinition() {
			return this.state.definition;
		}
		addDefinitionModifierDependency(dependency) {
			this.stateModifier.addDependency(dependency);
		}
		runRenderer(rendererHandler, customSelectedStepIdProvider) {
			const selectedStepIdProvider = customSelectedStepIdProvider || this.state;
			return EditorRenderer.create(this.state, selectedStepIdProvider, this.definitionWalker, rendererHandler);
		}
		createStepEditorContext(stepId) {
			if (!stepId) {
				throw new Error('Step id is empty');
			}
			return {
				notifyPropertiesChanged: () => {
					this.state.notifyDefinitionChanged(exports.DefinitionChangeType.stepPropertyChanged, stepId);
				},
				notifyNameChanged: () => {
					this.state.notifyDefinitionChanged(exports.DefinitionChangeType.stepNameChanged, stepId);
				},
				notifyChildrenChanged: () => {
					this.state.notifyDefinitionChanged(exports.DefinitionChangeType.stepChildrenChanged, stepId);
					this.stateModifier.updateDependencies();
				}
			};
		}
		createRootEditorContext() {
			return {
				notifyPropertiesChanged: () => {
					this.state.notifyDefinitionChanged(exports.DefinitionChangeType.rootPropertyChanged, null);
				}
			};
		}
	}

	class PathBarApi {
		constructor(state, definitionWalker) {
			this.state = state;
			this.definitionWalker = definitionWalker;
			this.onStateChanged = race(0, this.state.onFolderPathChanged, this.state.onDefinitionChanged);
		}
		setFolderPath(path) {
			this.state.setFolderPath(path);
		}
		getFolderPath() {
			return this.state.folderPath;
		}
		getFolderPathStepNames() {
			return this.state.folderPath.map(stepId => {
				return this.definitionWalker.getById(this.state.definition, stepId).name;
			});
		}
	}

	class DragStepView {
		static create(step, theme, componentContext) {
			var _a;
			const body = (_a = componentContext.shadowRoot) !== null && _a !== void 0 ? _a : document.body;
			const layer = Dom.element('div', {
				class: `sqd-drag sqd-theme-${theme}`
			});
			body.appendChild(layer);
			const component = componentContext.services.draggedComponent.create(layer, step, componentContext);
			return new DragStepView(component, layer, body);
		}
		constructor(component, layer, body) {
			this.component = component;
			this.layer = layer;
			this.body = body;
		}
		setPosition(position) {
			this.layer.style.top = position.y + 'px';
			this.layer.style.left = position.x + 'px';
		}
		remove() {
			this.component.destroy();
			this.body.removeChild(this.layer);
		}
	}

	class PlaceholderFinder {
		static create(placeholders, state) {
			const checker = new PlaceholderFinder(placeholders, state);
			state.onViewportChanged.subscribe(checker.clearCache);
			window.addEventListener('scroll', checker.clearCache, false);
			return checker;
		}
		constructor(placeholders, state) {
			this.placeholders = placeholders;
			this.state = state;
			this.clearCache = () => {
				this.cache = undefined;
			};
		}
		find(vLt, vWidth, vHeight) {
			var _a;
			if (!this.cache) {
				const scroll = new Vector(window.scrollX, window.scrollY);
				this.cache = this.placeholders.map(placeholder => {
					const rect = placeholder.getClientRect();
					const lt = new Vector(rect.x, rect.y).add(scroll);
					const br = new Vector(rect.x + rect.width, rect.y + rect.height).add(scroll);
					return {
						placeholder,
						lt,
						br,
						diagSq: lt.x * lt.x + lt.y * lt.y
					};
				});
				this.cache.sort((a, b) => a.diagSq - b.diagSq);
			}
			const vR = vLt.x + vWidth;
			const vB = vLt.y + vHeight;
			return (_a = this.cache.find(p => {
				return Math.max(vLt.x, p.lt.x) < Math.min(vR, p.br.x) && Math.max(vLt.y, p.lt.y) < Math.min(vB, p.br.y);
			})) === null || _a === void 0 ? void 0 : _a.placeholder;
		}
		destroy() {
			this.state.onViewportChanged.unsubscribe(this.clearCache);
			window.removeEventListener('scroll', this.clearCache, false);
		}
	}

	class DragStepBehavior {
		static create(designerContext, step, draggedStepComponent) {
			const view = DragStepView.create(step, designerContext.theme, designerContext.componentContext);
			return new DragStepBehavior(view, designerContext.workspaceController, designerContext.placeholderController, designerContext.state, step, designerContext.stateModifier, draggedStepComponent);
		}
		constructor(view, workspaceController, placeholderController, designerState, step, stateModifier, draggedStepComponent) {
			this.view = view;
			this.workspaceController = workspaceController;
			this.placeholderController = placeholderController;
			this.designerState = designerState;
			this.step = step;
			this.stateModifier = stateModifier;
			this.draggedStepComponent = draggedStepComponent;
		}
		onStart(position) {
			let offset = null;
			if (this.draggedStepComponent) {
				this.draggedStepComponent.setIsDisabled(true);
				this.draggedStepComponent.setIsDragging(true);
				const hasSameSize = this.draggedStepComponent.view.width === this.view.component.width &&
					this.draggedStepComponent.view.height === this.view.component.height;
				if (hasSameSize) {
					// Mouse cursor will be positioned on the same place as the source component.
					const pagePosition = this.draggedStepComponent.view.getClientPosition();
					offset = position.subtract(pagePosition);
				}
			}
			if (!offset) {
				// Mouse cursor will be positioned in the center of the component.
				offset = new Vector(this.view.component.width, this.view.component.height).divideByScalar(2);
			}
			this.view.setPosition(position.subtract(offset));
			this.designerState.setIsDragging(true);
			const { placeholders, components } = this.resolvePlaceholders(this.draggedStepComponent);
			this.state = {
				placeholders,
				components,
				startPosition: position,
				finder: PlaceholderFinder.create(placeholders, this.designerState),
				offset
			};
			placeholders.forEach(placeholder => placeholder.setIsVisible(true));
			components.forEach(component => component.setIsDragging(true));
		}
		onMove(delta) {
			if (this.state) {
				const newPosition = this.state.startPosition.subtract(delta).subtract(this.state.offset);
				this.view.setPosition(newPosition);
				const placeholder = this.state.finder.find(newPosition, this.view.component.width, this.view.component.height);
				if (this.currentPlaceholder !== placeholder) {
					if (this.currentPlaceholder) {
						this.currentPlaceholder.setIsHover(false);
					}
					if (placeholder) {
						placeholder.setIsHover(true);
					}
					this.currentPlaceholder = placeholder;
				}
			}
		}
		onEnd(interrupt) {
			if (!this.state) {
				throw new Error('Invalid state');
			}
			this.state.placeholders.forEach(placeholder => placeholder.setIsVisible(false));
			this.state.components.forEach(component => component.setIsDragging(false));
			this.state.finder.destroy();
			this.state = undefined;
			this.view.remove();
			this.designerState.setIsDragging(false);
			let modified = false;
			if (!interrupt && this.currentPlaceholder) {
				if (this.draggedStepComponent) {
					modified = this.stateModifier.tryMove(this.draggedStepComponent.parentSequence, this.draggedStepComponent.step, this.currentPlaceholder.parentSequence, this.currentPlaceholder.index);
				}
				else {
					modified = this.stateModifier.tryInsert(this.step, this.currentPlaceholder.parentSequence, this.currentPlaceholder.index);
				}
			}
			if (!modified) {
				if (this.draggedStepComponent) {
					this.draggedStepComponent.setIsDisabled(false);
					this.draggedStepComponent.setIsDragging(false);
				}
				if (this.currentPlaceholder) {
					this.currentPlaceholder.setIsHover(false);
				}
			}
			this.currentPlaceholder = undefined;
		}
		resolvePlaceholders(skipComponent) {
			const result = this.workspaceController.resolvePlaceholders(skipComponent);
			if (this.placeholderController.canShow) {
				const canShow = this.placeholderController.canShow;
				result.placeholders = result.placeholders.filter(placeholder => canShow(placeholder.parentSequence, placeholder.index, this.step.componentType, this.step.type));
			}
			return result;
		}
	}

	class ToolboxApi {
		constructor(state, designerContext, behaviorController, toolboxDataProvider, uidGenerator) {
			this.state = state;
			this.designerContext = designerContext;
			this.behaviorController = behaviorController;
			this.toolboxDataProvider = toolboxDataProvider;
			this.uidGenerator = uidGenerator;
		}
		isCollapsed() {
			return this.state.isToolboxCollapsed;
		}
		toggleIsCollapsed() {
			this.state.setIsToolboxCollapsed(!this.state.isToolboxCollapsed);
		}
		subscribeIsCollapsed(listener) {
			this.state.onIsToolboxCollapsedChanged.subscribe(listener);
		}
		getAllGroups() {
			return this.toolboxDataProvider.getAllGroups();
		}
		applyFilter(allGroups, filter) {
			return this.toolboxDataProvider.applyFilter(allGroups, filter);
		}
		/**
		 * @param position Mouse or touch position.
		 * @param step Step definition.
		 * @returns If started dragging returns true, otherwise returns false.
		 */
		tryDrag(position, step) {
			if (!this.state.isReadonly) {
				const newStep = this.activateStep(step);
				this.behaviorController.start(position, DragStepBehavior.create(this.designerContext, newStep));
				return true;
			}
			return false;
		}
		activateStep(step) {
			const newStep = ObjectCloner.deepClone(step);
			newStep.id = this.uidGenerator();
			return newStep;
		}
	}

	const regexp = /^[a-zA-Z][a-zA-Z0-9_-]+$/;
	class StepTypeValidator {
		static validate(type) {
			if (!regexp.test(type)) {
				throw new Error(`Step type "${type}" contains not allowed characters`);
			}
		}
	}

	class ToolboxDataProvider {
		constructor(i18n, iconProvider, configuration) {
			this.i18n = i18n;
			this.iconProvider = iconProvider;
			this.configuration = configuration;
			this.createItemData = (step) => {
				StepTypeValidator.validate(step.type);
				const iconUrl = this.iconProvider.getIconUrl(step);
				const rawLabel = this.configuration && this.configuration.labelProvider ? this.configuration.labelProvider(step) : step.name;
				const label = this.i18n(`toolbox.item.${step.type}.label`, rawLabel);
				const description = this.configuration && this.configuration.descriptionProvider ? this.configuration.descriptionProvider(step) : label;
				const lowerCaseLabel = label.toLowerCase();
				return {
					iconUrl,
					label,
					description,
					lowerCaseLabel,
					step
				};
			};
		}
		getAllGroups() {
			if (!this.configuration) {
				return [];
			}
			return this.configuration.groups.map(group => {
				return {
					name: group.name,
					items: group.steps.map(this.createItemData)
				};
			});
		}
		applyFilter(allGroups, filter) {
			if (!filter) {
				return allGroups;
			}
			const lowerCaseFilter = filter.toLowerCase();
			return allGroups
				.map(group => {
					return {
						name: group.name,
						items: group.items.filter(s => {
							return s.lowerCaseLabel.includes(lowerCaseFilter);
						})
					};
				})
				.filter(group => group.items.length > 0);
		}
	}

	function animate(interval, handler) {
		const iv = setInterval(tick, 15);
		const startTime = Date.now();
		const anim = {
			isAlive: true,
			stop: () => {
				anim.isAlive = false;
				clearInterval(iv);
			}
		};
		function tick() {
			const progress = Math.min((Date.now() - startTime) / interval, 1);
			handler(progress);
			if (progress === 1) {
				anim.stop();
			}
		}
		return anim;
	}

	class ViewportAnimator {
		constructor(state) {
			this.state = state;
		}
		execute(target) {
			if (this.animation && this.animation.isAlive) {
				this.animation.stop();
			}
			const startPosition = this.state.viewport.position;
			const startScale = this.state.viewport.scale;
			const deltaPosition = startPosition.subtract(target.position);
			const deltaScale = startScale - target.scale;
			this.animation = animate(150, progress => {
				const newScale = startScale - deltaScale * progress;
				this.state.setViewport({
					position: startPosition.subtract(deltaPosition.multiplyByScalar(progress)),
					scale: newScale
				});
			});
		}
	}

	class ZoomByWheelCalculator {
		static calculate(controller, current, canvasPosition, e) {
			if (e.deltaY === 0) {
				return null;
			}
			const nextScale = controller.getNextScale(current.scale, e.deltaY < 0);
			let scale;
			const absDeltaY = Math.abs(e.deltaY);
			if (absDeltaY < controller.smoothDeltaYLimit) {
				const fraction = absDeltaY / controller.smoothDeltaYLimit;
				const step = nextScale.next - nextScale.current;
				scale = current.scale + step * fraction;
			}
			else {
				scale = nextScale.next;
			}
			const mousePoint = new Vector(e.pageX, e.pageY).subtract(canvasPosition);
			// The real point is point on canvas with no scale.
			const mouseRealPoint = mousePoint.divideByScalar(current.scale).subtract(current.position.divideByScalar(current.scale));
			const position = mouseRealPoint.multiplyByScalar(-scale).add(mousePoint);
			return { position, scale };
		}
	}

	class ViewportApi {
		constructor(state, workspaceController, viewportController) {
			this.state = state;
			this.workspaceController = workspaceController;
			this.viewportController = viewportController;
			this.animator = new ViewportAnimator(this.state);
		}
		limitScale(scale) {
			return this.viewportController.limitScale(scale);
		}
		resetViewport() {
			const defaultViewport = this.viewportController.getDefault();
			this.state.setViewport(defaultViewport);
		}
		zoom(direction) {
			const viewport = this.viewportController.getZoomed(direction);
			if (viewport) {
				this.state.setViewport(viewport);
			}
		}
		moveViewportToStep(stepId) {
			const component = this.workspaceController.getComponentByStepId(stepId);
			const canvasPosition = this.workspaceController.getCanvasPosition();
			const clientPosition = component.view.getClientPosition();
			const componentPosition = clientPosition.subtract(canvasPosition);
			const componentSize = new Vector(component.view.width, component.view.height);
			const viewport = this.viewportController.getFocusedOnComponent(componentPosition, componentSize);
			this.animator.execute(viewport);
		}
		handleWheelEvent(e) {
			const canvasPosition = this.workspaceController.getCanvasPosition();
			const newViewport = ZoomByWheelCalculator.calculate(this.viewportController, this.state.viewport, canvasPosition, e);
			if (newViewport) {
				this.state.setViewport(newViewport);
			}
		}
	}

	const defaultResolvers = [sequentialResolver, branchedResolver];
	function branchedResolver(step) {
		const branches = step.branches;
		if (branches) {
			return { type: exports.StepChildrenType.branches, items: branches };
		}
		return null;
	}
	function sequentialResolver(step) {
		const sequence = step.sequence;
		if (sequence) {
			return { type: exports.StepChildrenType.sequence, items: sequence };
		}
		return null;
	}

	exports.StepChildrenType = void 0;
	(function (StepChildrenType) {
		StepChildrenType[StepChildrenType["sequence"] = 1] = "sequence";
		StepChildrenType[StepChildrenType["branches"] = 2] = "branches";
	})(exports.StepChildrenType || (exports.StepChildrenType = {}));
	class DefinitionWalker {
		constructor(resolvers) {
			this.resolvers = resolvers ? resolvers.concat(defaultResolvers) : defaultResolvers;
		}
		/**
		 * Returns children of the step. If the step doesn't have children, returns null.
		 * @param step The step.
		 */
		getChildren(step) {
			const count = this.resolvers.length;
			for (let i = 0; i < count; i++) {
				const result = this.resolvers[i](step);
				if (result) {
					return result;
				}
			}
			return null;
		}
		/**
		 * Returns the parents of the step or the sequence.
		 * @param definition The definition.
		 * @param needle The step, stepId or sequence to find.
		 * @returns The parents of the step or the sequence.
		 */
		getParents(definition, needle) {
			const result = [];
			let searchSequence = null;
			let searchStepId = null;
			if (Array.isArray(needle)) {
				searchSequence = needle;
			}
			else if (typeof needle === 'string') {
				searchStepId = needle;
			}
			else {
				searchStepId = needle.id;
			}
			if (this.find(definition.sequence, searchSequence, searchStepId, result)) {
				result.reverse();
				return result.map(item => {
					return typeof item === 'string' ? item : item.step;
				});
			}
			throw new Error(searchStepId ? `Cannot get parents of step: ${searchStepId}` : 'Cannot get parents of sequence');
		}
		findParentSequence(definition, stepId) {
			const result = [];
			if (this.find(definition.sequence, null, stepId, result)) {
				return result[0];
			}
			return null;
		}
		getParentSequence(definition, stepId) {
			const result = this.findParentSequence(definition, stepId);
			if (!result) {
				throw new Error(`Cannot find step by id: ${stepId}`);
			}
			return result;
		}
		findById(definition, stepId) {
			const result = this.findParentSequence(definition, stepId);
			return result ? result.step : null;
		}
		getById(definition, stepId) {
			return this.getParentSequence(definition, stepId).step;
		}
		forEach(definition, callback) {
			this.iterateSequence(definition.sequence, callback);
		}
		forEachSequence(sequence, callback) {
			this.iterateSequence(sequence, callback);
		}
		forEachChildren(step, callback) {
			this.iterateStep(step, callback);
		}
		find(sequence, needSequence, needStepId, result) {
			if (needSequence && sequence === needSequence) {
				return true;
			}
			const count = sequence.length;
			for (let index = 0; index < count; index++) {
				const step = sequence[index];
				if (needStepId && step.id === needStepId) {
					result.push({ step, index, parentSequence: sequence });
					return true;
				}
				const children = this.getChildren(step);
				if (children) {
					switch (children.type) {
						case exports.StepChildrenType.sequence:
							{
								const parentSequence = children.items;
								if (this.find(parentSequence, needSequence, needStepId, result)) {
									result.push({ step, index, parentSequence });
									return true;
								}
							}
							break;
						case exports.StepChildrenType.branches:
							{
								const branches = children.items;
								const branchNames = Object.keys(branches);
								for (const branchName of branchNames) {
									const parentSequence = branches[branchName];
									if (this.find(parentSequence, needSequence, needStepId, result)) {
										result.push(branchName);
										result.push({ step, index, parentSequence });
										return true;
									}
								}
							}
							break;
						default:
							throw new Error(`Not supported step children type: ${children.type}`);
					}
				}
			}
			return false;
		}
		iterateSequence(sequence, callback) {
			const count = sequence.length;
			for (let index = 0; index < count; index++) {
				const step = sequence[index];
				if (callback(step, index, sequence) === false) {
					return false;
				}
				if (!this.iterateStep(step, callback)) {
					return false;
				}
			}
			return true;
		}
		iterateStep(step, callback) {
			const children = this.getChildren(step);
			if (children) {
				switch (children.type) {
					case exports.StepChildrenType.sequence:
						{
							const sequence = children.items;
							if (!this.iterateSequence(sequence, callback)) {
								return false;
							}
						}
						break;
					case exports.StepChildrenType.branches:
						{
							const sequences = Object.values(children.items);
							for (const sequence of sequences) {
								if (!this.iterateSequence(sequence, callback)) {
									return false;
								}
							}
						}
						break;
					default:
						throw new Error(`Not supported step children type: ${children.type}`);
				}
			}
			return true;
		}
	}

	class WorkspaceApi {
		constructor(state, definitionWalker, workspaceController) {
			this.state = state;
			this.definitionWalker = definitionWalker;
			this.workspaceController = workspaceController;
		}
		getViewport() {
			return this.state.viewport;
		}
		setViewport(viewport) {
			this.state.setViewport(viewport);
		}
		getCanvasPosition() {
			return this.workspaceController.getCanvasPosition();
		}
		getCanvasSize() {
			return this.workspaceController.getCanvasSize();
		}
		getRootComponentSize() {
			return this.workspaceController.getRootComponentSize();
		}
		updateRootComponent() {
			this.workspaceController.updateRootComponent();
		}
		updateBadges() {
			this.workspaceController.updateBadges();
		}
		updateCanvasSize() {
			this.workspaceController.updateCanvasSize();
		}
		getRootSequence() {
			const stepId = this.state.tryGetLastStepIdFromFolderPath();
			if (stepId) {
				const parentStep = this.definitionWalker.getParentSequence(this.state.definition, stepId);
				const children = this.definitionWalker.getChildren(parentStep.step);
				if (!children || children.type !== exports.StepChildrenType.sequence) {
					throw new Error('Cannot find single sequence in folder step');
				}
				return {
					sequence: children.items,
					parentStep
				};
			}
			return {
				sequence: this.state.definition.sequence,
				parentStep: null
			};
		}
	}

	class DesignerApi {
		static create(context) {
			const workspace = new WorkspaceApi(context.state, context.definitionWalker, context.workspaceController);
			const viewportController = context.services.viewportController.create(workspace);
			const toolboxDataProvider = new ToolboxDataProvider(context.i18n, context.componentContext.iconProvider, context.configuration.toolbox);
			return new DesignerApi(context.configuration.shadowRoot, ControlBarApi.create(context.state, context.historyController, context.stateModifier), new ToolboxApi(context.state, context, context.behaviorController, toolboxDataProvider, context.uidGenerator), new EditorApi(context.state, context.definitionWalker, context.stateModifier), workspace, new ViewportApi(context.state, context.workspaceController, viewportController), new PathBarApi(context.state, context.definitionWalker), context.definitionWalker, context.i18n);
		}
		constructor(shadowRoot, controlBar, toolbox, editor, workspace, viewport, pathBar, definitionWalker, i18n) {
			this.shadowRoot = shadowRoot;
			this.controlBar = controlBar;
			this.toolbox = toolbox;
			this.editor = editor;
			this.workspace = workspace;
			this.viewport = viewport;
			this.pathBar = pathBar;
			this.definitionWalker = definitionWalker;
			this.i18n = i18n;
		}
	}

	const TYPE = 'selectStep';
	class SelectStepBehaviorEndToken {
		static is(token) {
			return Boolean(token) && token.type === TYPE;
		}
		constructor(stepId, time) {
			this.stepId = stepId;
			this.time = time;
			this.type = TYPE;
		}
	}

	const BADGE_GAP = 4;
	class DefaultBadgesDecorator {
		constructor(position, badges, g) {
			this.position = position;
			this.badges = badges;
			this.g = g;
		}
		update() {
			let offsetX = 0;
			let maxHeight = 0;
			let j = 0;
			for (let i = 0; i < this.badges.length; i++) {
				const badge = this.badges[i];
				if (badge && badge.view) {
					offsetX += j === 0 ? badge.view.width / 2 : badge.view.width;
					maxHeight = Math.max(maxHeight, badge.view.height);
					Dom.translate(badge.view.g, -offsetX, 0);
					offsetX += BADGE_GAP;
					j++;
				}
			}
			Dom.translate(this.g, this.position.x, this.position.y + -maxHeight / 2);
		}
	}

	class Badges {
		static createForStep(stepContext, view, componentContext) {
			const g = createG(view.g);
			const badges = componentContext.services.badges.map(ext => ext.createForStep(g, view, stepContext, componentContext));
			const decorator = componentContext.services.stepBadgesDecorator.create(g, view, badges);
			return new Badges(badges, decorator);
		}
		static createForRoot(parentElement, position, componentContext) {
			const g = createG(parentElement);
			const badges = componentContext.services.badges.map(ext => {
				return ext.createForRoot ? ext.createForRoot(g, componentContext) : null;
			});
			const decorator = new DefaultBadgesDecorator(position, badges, g);
			return new Badges(badges, decorator);
		}
		constructor(badges, decorator) {
			this.badges = badges;
			this.decorator = decorator;
		}
		update(result) {
			const count = this.badges.length;
			for (let i = 0; i < count; i++) {
				const badge = this.badges[i];
				if (badge) {
					result[i] = badge.update(result[i]);
				}
			}
			this.decorator.update();
		}
		resolveClick(click) {
			for (const badge of this.badges) {
				const command = badge && badge.resolveClick(click);
				if (command) {
					return command;
				}
			}
			return null;
		}
	}
	function createG(parentElement) {
		const g = Dom.svg('g', {
			class: 'sqd-badges'
		});
		parentElement.appendChild(g);
		return g;
	}

	class ValidationErrorBadgeView {
		static create(parent, cfg) {
			const g = Dom.svg('g');
			const halfOfSize = cfg.size / 2;
			const circle = Dom.svg('path', {
				class: 'sqd-validation-error',
				d: `M 0 ${-halfOfSize} l ${halfOfSize} ${cfg.size} l ${-cfg.size} 0 Z`
			});
			Dom.translate(circle, halfOfSize, halfOfSize);
			g.appendChild(circle);
			const icon = Icons.appendPath(g, 'sqd-validation-error-icon-path', Icons.alert, cfg.iconSize);
			const offsetX = (cfg.size - cfg.iconSize) / 2;
			const offsetY = offsetX * 1.5;
			Dom.translate(icon, offsetX, offsetY);
			parent.appendChild(g);
			return new ValidationErrorBadgeView(parent, g, cfg.size, cfg.size);
		}
		constructor(parent, g, width, height) {
			this.parent = parent;
			this.g = g;
			this.width = width;
			this.height = height;
		}
		destroy() {
			this.parent.removeChild(this.g);
		}
	}

	class ValidatorFactory {
		static createForStep(stepContext, view, componentContext) {
			return () => {
				if (!componentContext.validator.validateStep(stepContext.step, stepContext.parentSequence)) {
					return false;
				}
				if (view.haveCollapsedChildren) {
					let allChildrenValid = true;
					componentContext.definitionWalker.forEachChildren(stepContext.step, (step, _, parentSequence) => {
						if (!componentContext.validator.validateStep(step, parentSequence)) {
							allChildrenValid = false;
							return false;
						}
					});
					if (!allChildrenValid) {
						return false;
					}
				}
				return true;
			};
		}
		static createForRoot(componentContext) {
			return () => {
				return componentContext.validator.validateRoot();
			};
		}
	}

	class ValidationErrorBadge {
		static createForStep(parentElement, view, stepContext, componentContext, configuration) {
			const validator = ValidatorFactory.createForStep(stepContext, view, componentContext);
			return new ValidationErrorBadge(parentElement, validator, configuration);
		}
		static createForRoot(parentElement, componentContext, configuration) {
			const validator = ValidatorFactory.createForRoot(componentContext);
			return new ValidationErrorBadge(parentElement, validator, configuration);
		}
		constructor(parentElement, validator, configuration) {
			this.parentElement = parentElement;
			this.validator = validator;
			this.configuration = configuration;
			this.view = null;
		}
		update(result) {
			const isValid = this.validator();
			if (isValid) {
				if (this.view) {
					this.view.destroy();
					this.view = null;
				}
			}
			else if (!this.view) {
				this.view = ValidationErrorBadgeView.create(this.parentElement, this.configuration);
			}
			return isValid && result;
		}
		resolveClick() {
			return null;
		}
	}

	const defaultConfiguration$6 = {
		view: {
			size: 22,
			iconSize: 12
		}
	};
	class ValidationErrorBadgeExtension {
		static create(configuration) {
			return new ValidationErrorBadgeExtension(configuration !== null && configuration !== void 0 ? configuration : defaultConfiguration$6);
		}
		constructor(configuration) {
			this.configuration = configuration;
			this.id = 'validationError';
			this.createStartValue = () => true;
		}
		createForStep(parentElement, view, stepContext, componentContext) {
			return ValidationErrorBadge.createForStep(parentElement, view, stepContext, componentContext, this.configuration.view);
		}
		createForRoot(parentElement, componentContext) {
			return ValidationErrorBadge.createForRoot(parentElement, componentContext, this.configuration.view);
		}
	}

	class ComponentDom {
		static stepG(componentClassName, type, id) {
			return Dom.svg('g', {
				class: `sqd-step-${componentClassName} sqd-type-${type}`,
				'data-step-id': id
			});
		}
	}

	class InputView {
		static createRectInput(parent, x, y, size, radius, iconSize, iconUrl) {
			const g = Dom.svg('g');
			parent.appendChild(g);
			const rect = Dom.svg('rect', {
				class: 'sqd-input',
				width: size,
				height: size,
				x: x - size / 2,
				y: y + size / -2 + 0.5,
				rx: radius,
				ry: radius
			});
			g.appendChild(rect);
			if (iconUrl) {
				const icon = Dom.svg('image', {
					href: iconUrl,
					width: iconSize,
					height: iconSize,
					x: x - iconSize / 2,
					y: y + iconSize / -2
				});
				g.appendChild(icon);
			}
			return new InputView(g);
		}
		static createRoundInput(parent, x, y, size) {
			const circle = Dom.svg('circle', {
				class: 'sqd-input',
				cx: x,
				xy: y,
				r: size / 2
			});
			parent.appendChild(circle);
			return new InputView(circle);
		}
		constructor(g) {
			this.g = g;
		}
		setIsHidden(isHidden) {
			Dom.attrs(this.g, {
				visibility: isHidden ? 'hidden' : 'visible'
			});
		}
	}

	const EPS = 0.5; // Epsilon, a tiny offset to avoid rendering issues
	class JoinView {
		static createStraightJoin(parent, start, height) {
			const dy = Math.sign(height);
			const join = Dom.svg('line', {
				class: 'sqd-join',
				x1: start.x,
				y1: start.y - EPS * dy,
				x2: start.x,
				y2: start.y + height + EPS * dy
			});
			parent.insertBefore(join, parent.firstChild);
		}
		static createJoins(parent, start, targets) {
			const firstTarget = targets[0];
			const h = Math.abs(firstTarget.y - start.y) / 2; // half height
			const dy = Math.sign(firstTarget.y - start.y); // direction y
			switch (targets.length) {
				case 1:
					if (start.x === targets[0].x) {
						JoinView.createStraightJoin(parent, start, h * 2 * dy);
					}
					else {
						appendCurvedJoins(parent, start, targets, h, dy);
					}
					break;
				case 2:
					appendCurvedJoins(parent, start, targets, h, dy);
					break;
				default:
					{
						const f = targets[0]; // first
						const l = targets[targets.length - 1]; // last
						const eps = EPS * dy;
						appendJoin(parent, `M ${f.x} ${f.y + eps} l 0 ${-eps} q ${h * 0.3} ${h * -dy * 0.8} ${h} ${h * -dy} ` +
							`l ${l.x - f.x - h * 2} 0 q ${h * 0.8} ${-h * -dy * 0.3} ${h} ${-h * -dy} l 0 ${eps}`);
						for (let i = 1; i < targets.length - 1; i++) {
							JoinView.createStraightJoin(parent, targets[i], h * -dy);
						}
						JoinView.createStraightJoin(parent, start, h * dy);
					}
					break;
			}
		}
	}
	function appendCurvedJoins(parent, start, targets, h, dy) {
		const eps = EPS * dy;
		for (const target of targets) {
			const l = Math.abs(target.x - start.x) - h * 2; // straight line length
			const dx = Math.sign(target.x - start.x); // direction x
			appendJoin(parent, `M ${start.x} ${start.y - eps} l 0 ${eps} q ${dx * h * 0.3} ${dy * h * 0.8} ${dx * h} ${dy * h} ` +
				`l ${dx * l} 0 q ${dx * h * 0.7} ${dy * h * 0.2} ${dx * h} ${dy * h} l 0 ${eps}`);
		}
	}
	function appendJoin(parent, d) {
		const join = Dom.svg('path', {
			class: 'sqd-join',
			fill: 'none',
			d
		});
		parent.insertBefore(join, parent.firstChild);
	}

	class LabelView {
		static create(parent, y, cfg, text, theme) {
			const g = Dom.svg('g', {
				class: `sqd-label sqd-label-${theme}`
			});
			parent.appendChild(g);
			const nameText = Dom.svg('text', {
				class: 'sqd-label-text',
				y: y + cfg.height / 2
			});
			nameText.textContent = text;
			g.appendChild(nameText);
			const width = Math.max(nameText.getBBox().width + cfg.paddingX * 2, cfg.minWidth);
			const nameRect = Dom.svg('rect', {
				class: 'sqd-label-rect',
				width: width,
				height: cfg.height,
				x: -width / 2 + 0.5,
				y: y + 0.5,
				rx: cfg.radius,
				ry: cfg.radius
			});
			g.insertBefore(nameRect, nameText);
			return new LabelView(g, width, cfg.height);
		}
		constructor(g, width, height) {
			this.g = g;
			this.width = width;
			this.height = height;
		}
	}

	class OutputView {
		static create(parent, x, y, size) {
			const circle = Dom.svg('circle', {
				class: 'sqd-output',
				cx: x,
				cy: y,
				r: size / 2
			});
			parent.appendChild(circle);
			return new OutputView(circle);
		}
		constructor(root) {
			this.root = root;
		}
		setIsHidden(isHidden) {
			Dom.attrs(this.root, {
				visibility: isHidden ? 'hidden' : 'visible'
			});
		}
	}

	// PlaceholderExtension
	exports.PlaceholderGapOrientation = void 0;
	(function (PlaceholderGapOrientation) {
		PlaceholderGapOrientation[PlaceholderGapOrientation["along"] = 0] = "along";
		PlaceholderGapOrientation[PlaceholderGapOrientation["perpendicular"] = 1] = "perpendicular"; // Goes perpendicular to the flow
	})(exports.PlaceholderGapOrientation || (exports.PlaceholderGapOrientation = {}));

	class DefaultSequenceComponentView {
		static create(parent, sequenceContext, componentContext) {
			const phSize = componentContext.services.placeholder.getGapSize(exports.PlaceholderGapOrientation.along);
			const phWidth = phSize.x;
			const phHeight = phSize.y;
			const { sequence } = sequenceContext;
			const g = Dom.svg('g');
			parent.appendChild(g);
			const components = [];
			for (let index = 0; index < sequence.length; index++) {
				const stepContext = {
					parentSequence: sequenceContext.sequence,
					step: sequence[index],
					depth: sequenceContext.depth,
					position: index,
					isInputConnected: index === 0 ? sequenceContext.isInputConnected : components[index - 1].hasOutput,
					isOutputConnected: index === sequence.length - 1 ? sequenceContext.isOutputConnected : true,
					isPreview: sequenceContext.isPreview
				};
				components[index] = componentContext.stepComponentFactory.create(g, stepContext, componentContext);
			}
			let joinX;
			let totalWidth;
			if (components.length > 0) {
				const restWidth = Math.max(...components.map(c => c.view.width - c.view.joinX));
				joinX = Math.max(...components.map(c => c.view.joinX));
				totalWidth = joinX + restWidth;
			}
			else {
				joinX = phWidth / 2;
				totalWidth = phWidth;
			}
			let offsetY = phHeight;
			const placeholders = [];
			for (let i = 0; i < components.length; i++) {
				const component = components[i];
				const offsetX = joinX - component.view.joinX;
				if ((i === 0 && sequenceContext.isInputConnected) || (i > 0 && components[i - 1].hasOutput)) {
					JoinView.createStraightJoin(g, new Vector(joinX, offsetY - phHeight), phHeight);
				}
				if (!sequenceContext.isPreview && componentContext.placeholderController.canCreate(sequence, i)) {
					const ph = componentContext.services.placeholder.createForGap(g, sequence, i, exports.PlaceholderGapOrientation.along);
					Dom.translate(ph.view.g, joinX - phWidth / 2, offsetY - phHeight);
					placeholders.push(ph);
				}
				Dom.translate(component.view.g, offsetX, offsetY);
				offsetY += component.view.height + phHeight;
			}
			if (sequenceContext.isOutputConnected && (components.length === 0 || components[components.length - 1].hasOutput)) {
				JoinView.createStraightJoin(g, new Vector(joinX, offsetY - phHeight), phHeight);
			}
			const newIndex = components.length;
			if (!sequenceContext.isPreview && componentContext.placeholderController.canCreate(sequence, newIndex)) {
				const ph = componentContext.services.placeholder.createForGap(g, sequence, newIndex, exports.PlaceholderGapOrientation.along);
				Dom.translate(ph.view.g, joinX - phWidth / 2, offsetY - phHeight);
				placeholders.push(ph);
			}
			return new DefaultSequenceComponentView(g, totalWidth, offsetY, joinX, placeholders, components);
		}
		constructor(g, width, height, joinX, placeholders, components) {
			this.g = g;
			this.width = width;
			this.height = height;
			this.joinX = joinX;
			this.placeholders = placeholders;
			this.components = components;
		}
		hasOutput() {
			if (this.components.length > 0) {
				return this.components[this.components.length - 1].hasOutput;
			}
			return true;
		}
	}

	class DefaultSequenceComponent {
		static create(parentElement, sequenceContext, context) {
			const view = DefaultSequenceComponentView.create(parentElement, sequenceContext, context);
			return new DefaultSequenceComponent(view, view.hasOutput());
		}
		constructor(view, hasOutput) {
			this.view = view;
			this.hasOutput = hasOutput;
		}
		resolveClick(click) {
			for (const component of this.view.components) {
				const result = component.resolveClick(click);
				if (result) {
					return result;
				}
			}
			for (const placeholder of this.view.placeholders) {
				const result = placeholder.resolveClick(click);
				if (result) {
					return result;
				}
			}
			return null;
		}
		findById(stepId) {
			for (const component of this.view.components) {
				const result = component.findById(stepId);
				if (result) {
					return result;
				}
			}
			return null;
		}
		resolvePlaceholders(skipComponent, result) {
			this.view.components.forEach(component => component.resolvePlaceholders(skipComponent, result));
			this.view.placeholders.forEach(placeholder => result.placeholders.push(placeholder));
		}
		updateBadges(result) {
			this.view.components.forEach(component => component.updateBadges(result));
		}
	}

	exports.ClickCommandType = void 0;
	(function (ClickCommandType) {
		ClickCommandType[ClickCommandType["selectStep"] = 1] = "selectStep";
		ClickCommandType[ClickCommandType["rerenderStep"] = 2] = "rerenderStep";
		ClickCommandType[ClickCommandType["openFolder"] = 3] = "openFolder";
		ClickCommandType[ClickCommandType["triggerCustomAction"] = 4] = "triggerCustomAction";
	})(exports.ClickCommandType || (exports.ClickCommandType = {}));
	exports.PlaceholderDirection = void 0;
	(function (PlaceholderDirection) {
		PlaceholderDirection[PlaceholderDirection["gap"] = 0] = "gap";
		PlaceholderDirection[PlaceholderDirection["in"] = 1] = "in";
		PlaceholderDirection[PlaceholderDirection["out"] = 2] = "out";
	})(exports.PlaceholderDirection || (exports.PlaceholderDirection = {}));

	class StartStopRootComponentView {
		static create(parent, sequence, parentPlaceIndicator, context, cfg) {
			const g = Dom.svg('g', {
				class: 'sqd-root-start-stop'
			});
			parent.appendChild(g);
			const sequenceComponent = DefaultSequenceComponent.create(g, {
				sequence,
				depth: 0,
				isInputConnected: Boolean(cfg.start),
				isOutputConnected: true,
				isPreview: false
			}, context);
			const view = sequenceComponent.view;
			const x = view.joinX - cfg.size / 2;
			const endY = cfg.size + view.height;
			const iconSize = parentPlaceIndicator ? cfg.folderIconSize : cfg.defaultIconSize;
			if (cfg.start) {
				const startCircle = createCircle('start', parentPlaceIndicator ? cfg.folderIconD : cfg.start.iconD, cfg.size, iconSize);
				Dom.translate(startCircle, x, 0);
				g.appendChild(startCircle);
			}
			Dom.translate(view.g, 0, cfg.size);
			const stopCircle = createCircle('stop', parentPlaceIndicator ? cfg.folderIconD : cfg.stopIconD, cfg.size, iconSize);
			Dom.translate(stopCircle, x, endY);
			g.appendChild(stopCircle);
			let startPlaceholder = null;
			let endPlaceholder = null;
			if (parentPlaceIndicator) {
				const size = new Vector(cfg.size, cfg.size);
				startPlaceholder = context.services.placeholder.createForArea(g, size, exports.PlaceholderDirection.out, parentPlaceIndicator.sequence, parentPlaceIndicator.index);
				endPlaceholder = context.services.placeholder.createForArea(g, size, exports.PlaceholderDirection.out, parentPlaceIndicator.sequence, parentPlaceIndicator.index);
				Dom.translate(startPlaceholder.view.g, x, 0);
				Dom.translate(endPlaceholder.view.g, x, endY);
			}
			const badges = Badges.createForRoot(g, new Vector(x + cfg.size, 0), context);
			return new StartStopRootComponentView(g, view.width, view.height + cfg.size * 2, view.joinX, sequenceComponent, startPlaceholder, endPlaceholder, badges);
		}
		constructor(g, width, height, joinX, component, startPlaceholder, endPlaceholder, badges) {
			this.g = g;
			this.width = width;
			this.height = height;
			this.joinX = joinX;
			this.component = component;
			this.startPlaceholder = startPlaceholder;
			this.endPlaceholder = endPlaceholder;
			this.badges = badges;
		}
		destroy() {
			var _a;
			(_a = this.g.parentNode) === null || _a === void 0 ? void 0 : _a.removeChild(this.g);
		}
	}
	function createCircle(classSuffix, d, size, iconSize) {
		const g = Dom.svg('g', {
			class: 'sqd-root-start-stop-' + classSuffix
		});
		const r = size / 2;
		const circle = Dom.svg('circle', {
			class: 'sqd-root-start-stop-circle',
			cx: r,
			cy: r,
			r: r
		});
		g.appendChild(circle);
		const offset = (size - iconSize) / 2;
		const icon = Icons.appendPath(g, 'sqd-root-start-stop-icon', d, iconSize);
		Dom.translate(icon, offset, offset);
		return g;
	}

	class StartStopRootComponent {
		static create(parentElement, sequence, parentPlaceIndicator, context, viewConfiguration) {
			const view = StartStopRootComponentView.create(parentElement, sequence, parentPlaceIndicator, context, viewConfiguration);
			return new StartStopRootComponent(view);
		}
		constructor(view) {
			this.view = view;
		}
		resolveClick(click) {
			return this.view.component.resolveClick(click);
		}
		findById(stepId) {
			return this.view.component.findById(stepId);
		}
		resolvePlaceholders(skipComponent, result) {
			this.view.component.resolvePlaceholders(skipComponent, result);
			if (this.view.startPlaceholder && this.view.endPlaceholder) {
				result.placeholders.push(this.view.startPlaceholder);
				result.placeholders.push(this.view.endPlaceholder);
			}
		}
		updateBadges(result) {
			this.view.badges.update(result);
			this.view.component.updateBadges(result);
		}
	}

	const defaultViewConfiguration$1 = {
		size: 30,
		defaultIconSize: 22,
		folderIconSize: 18,
		start: {
			iconD: Icons.play
		},
		stopIconD: Icons.stop,
		folderIconD: Icons.folder
	};
	class StartStopRootComponentExtension {
		static create(configuration) {
			return new StartStopRootComponentExtension(configuration);
		}
		constructor(configuration) {
			this.configuration = configuration;
		}
		create(parentElement, sequence, parentPlaceIndicator, context) {
			var _a;
			const view = ((_a = this.configuration) === null || _a === void 0 ? void 0 : _a.view) ? Object.assign(Object.assign({}, defaultViewConfiguration$1), this.configuration.view) : defaultViewConfiguration$1;
			return StartStopRootComponent.create(parentElement, sequence, parentPlaceIndicator, context, view);
		}
	}

	const COMPONENT_CLASS_NAME$3 = 'container';
	const createContainerStepComponentViewFactory = (cfg) => (parentElement, stepContext, viewContext) => {
		return viewContext.createRegionComponentView(parentElement, COMPONENT_CLASS_NAME$3, (g, regionViewBuilder) => {
			const step = stepContext.step;
			const name = viewContext.getStepName();
			const labelView = LabelView.create(g, cfg.paddingTop, cfg.label, name, 'primary');
			const sequenceComponent = viewContext.createSequenceComponent(g, step.sequence);
			const halfOfWidestElement = labelView.width / 2;
			const offsetLeft = Math.max(halfOfWidestElement - sequenceComponent.view.joinX, 0) + cfg.paddingX;
			const offsetRight = Math.max(halfOfWidestElement - (sequenceComponent.view.width - sequenceComponent.view.joinX), 0) + cfg.paddingX;
			const width = offsetLeft + sequenceComponent.view.width + offsetRight;
			const height = cfg.paddingTop + cfg.label.height + sequenceComponent.view.height;
			const joinX = sequenceComponent.view.joinX + offsetLeft;
			Dom.translate(labelView.g, joinX, 0);
			Dom.translate(sequenceComponent.view.g, offsetLeft, cfg.paddingTop + cfg.label.height);
			let inputView = null;
			if (cfg.inputSize > 0) {
				const iconUrl = viewContext.getStepIconUrl();
				inputView = InputView.createRectInput(g, joinX, 0, cfg.inputSize, cfg.inputRadius, cfg.inputIconSize, iconUrl);
			}
			JoinView.createStraightJoin(g, new Vector(joinX, 0), cfg.paddingTop);
			const regionView = regionViewBuilder(g, [width], height);
			return {
				g,
				width,
				height,
				joinX,
				placeholders: null,
				components: [sequenceComponent],
				hasOutput: sequenceComponent.hasOutput,
				getClientPosition() {
					return regionView.getClientPosition();
				},
				resolveClick(click) {
					const result = regionView.resolveClick(click);
					return result === true || (result === null && g.contains(click.element)) ? true : result;
				},
				setIsDragging(isDragging) {
					if (cfg.autoHideInputOnDrag && inputView) {
						inputView.setIsHidden(isDragging);
					}
				},
				setIsSelected(isSelected) {
					regionView.setIsSelected(isSelected);
				},
				setIsDisabled(isDisabled) {
					Dom.toggleClass(g, isDisabled, 'sqd-disabled');
				}
			};
		});
	};

	const COMPONENT_CLASS_NAME$2 = 'launch-pad';
	function createView$1(parentElement, stepContext, viewContext, regionViewFactory, isInterruptedIfEmpty, cfg) {
		const step = stepContext.step;
		const sequence = stepContext.step.sequence;
		const g = ComponentDom.stepG(COMPONENT_CLASS_NAME$2, step.type, step.id);
		parentElement.appendChild(g);
		const components = [];
		let width;
		let height;
		let joinX;
		const placeholdersX = [];
		let placeholderOrientation;
		let placeholderSize;
		let hasOutput;
		let inputView = null;
		let outputView = null;
		if (sequence.length > 0) {
			let maxComponentHeight = 0;
			for (let i = 0; i < sequence.length; i++) {
				const component = viewContext.createStepComponent(g, sequence, sequence[i], i);
				components.push(component);
				maxComponentHeight = Math.max(maxComponentHeight, component.view.height);
			}
			const joinsX = [];
			const positionsX = [];
			const spacesY = [];
			placeholderOrientation = exports.PlaceholderGapOrientation.perpendicular;
			placeholderSize = viewContext.getPlaceholderGapSize(placeholderOrientation);
			placeholdersX.push(0);
			let positionX = placeholderSize.x;
			for (let i = 0; i < components.length; i++) {
				if (i > 0) {
					placeholdersX.push(positionX);
					positionX += placeholderSize.x;
				}
				const component = components[i];
				const componentY = (maxComponentHeight - component.view.height) / 2 + cfg.connectionHeight + cfg.paddingY;
				Dom.translate(component.view.g, positionX, componentY);
				joinsX.push(positionX + component.view.joinX);
				positionX += component.view.width;
				positionsX.push(positionX);
				spacesY.push(Math.max(0, (maxComponentHeight - component.view.height) / 2));
			}
			placeholdersX.push(positionX);
			positionX += placeholderSize.x;
			width = positionX;
			height = maxComponentHeight + 2 * cfg.connectionHeight + 2 * cfg.paddingY;
			const contentJoinX = components.length % 2 === 0
				? positionsX[Math.max(0, Math.floor(components.length / 2) - 1)] + placeholderSize.x / 2
				: joinsX[Math.floor(components.length / 2)];
			if (stepContext.isInputConnected) {
				const joinsTopY = joinsX.map(x => new Vector(x, cfg.connectionHeight));
				JoinView.createJoins(g, new Vector(contentJoinX, 0), joinsTopY);
				for (let i = 0; i < joinsX.length; i++) {
					JoinView.createStraightJoin(g, joinsTopY[i], cfg.paddingY + spacesY[i]);
				}
			}
			const joinsBottomY = joinsX.map(x => new Vector(x, cfg.connectionHeight + 2 * cfg.paddingY + maxComponentHeight));
			JoinView.createJoins(g, new Vector(contentJoinX, height), joinsBottomY);
			for (let i = 0; i < joinsX.length; i++) {
				JoinView.createStraightJoin(g, joinsBottomY[i], -(cfg.paddingY + spacesY[i]));
			}
			hasOutput = true;
			joinX = contentJoinX;
		}
		else {
			placeholderOrientation = exports.PlaceholderGapOrientation.along;
			placeholderSize = viewContext.getPlaceholderGapSize(placeholderOrientation);
			placeholdersX.push(cfg.emptyPaddingX);
			width = placeholderSize.x + cfg.emptyPaddingX * 2;
			height = placeholderSize.y + cfg.emptyPaddingY * 2;
			hasOutput = !isInterruptedIfEmpty;
			if (stepContext.isInputConnected) {
				inputView = InputView.createRoundInput(g, width / 2, 0, cfg.emptyInputSize);
			}
			if (stepContext.isOutputConnected && hasOutput) {
				outputView = OutputView.create(g, width / 2, height, cfg.emptyOutputSize);
			}
			if (cfg.emptyIconSize > 0) {
				const iconUrl = viewContext.getStepIconUrl();
				if (iconUrl) {
					const icon = Dom.svg('image', {
						href: iconUrl,
						x: (width - cfg.emptyIconSize) / 2,
						y: (height - cfg.emptyIconSize) / 2,
						width: cfg.emptyIconSize,
						height: cfg.emptyIconSize
					});
					g.appendChild(icon);
				}
			}
			joinX = width / 2;
		}
		let regionView = null;
		if (regionViewFactory) {
			regionView = regionViewFactory(g, [width], height);
		}
		const placeholders = [];
		const placeholderY = (height - placeholderSize.y) / 2;
		for (let i = 0; i < placeholdersX.length; i++) {
			const placeholder = viewContext.createPlaceholderForGap(g, sequence, i, placeholderOrientation);
			placeholders.push(placeholder);
			Dom.translate(placeholder.view.g, placeholdersX[i], placeholderY);
		}
		return {
			g,
			width,
			height,
			joinX,
			components,
			placeholders,
			hasOutput,
			getClientPosition() {
				return getAbsolutePosition(g);
			},
			resolveClick(click) {
				if (regionView) {
					const result = regionView.resolveClick(click);
					return result === true || (result === null && g.contains(click.element)) ? true : result;
				}
				return null;
			},
			setIsDragging(isDragging) {
				inputView === null || inputView === void 0 ? void 0 : inputView.setIsHidden(isDragging);
				outputView === null || outputView === void 0 ? void 0 : outputView.setIsHidden(isDragging);
			},
			setIsDisabled(isDisabled) {
				Dom.toggleClass(g, isDisabled, 'sqd-disabled');
			},
			setIsSelected(isSelected) {
				regionView === null || regionView === void 0 ? void 0 : regionView.setIsSelected(isSelected);
			}
		};
	}
	const createLaunchPadStepComponentViewFactory = (isInterruptedIfEmpty, cfg) => (parentElement, stepContext, viewContext) => {
		if (cfg.isRegionEnabled) {
			return viewContext.createRegionComponentView(parentElement, COMPONENT_CLASS_NAME$2, (g, regionViewBuilder) => {
				return createView$1(g, stepContext, viewContext, regionViewBuilder, isInterruptedIfEmpty, cfg);
			});
		}
		return createView$1(parentElement, stepContext, viewContext, null, isInterruptedIfEmpty, cfg);
	};

	const COMPONENT_CLASS_NAME$1 = 'switch';
	function createView(g, width, height, joinX, viewContext, sequenceComponents, regionView, cfg) {
		let inputView = null;
		if (cfg.inputSize > 0) {
			const iconUrl = viewContext.getStepIconUrl();
			inputView = InputView.createRectInput(g, joinX, cfg.paddingTop1, cfg.inputSize, cfg.inputRadius, cfg.inputIconSize, iconUrl);
		}
		return {
			g,
			width,
			height,
			joinX,
			placeholders: null,
			components: sequenceComponents,
			hasOutput: sequenceComponents ? sequenceComponents.some(c => c.hasOutput) : true,
			getClientPosition() {
				return regionView.getClientPosition();
			},
			resolveClick(click) {
				const result = regionView.resolveClick(click);
				return result === true || (result === null && g.contains(click.element)) ? true : result;
			},
			setIsDragging(isDragging) {
				if (cfg.autoHideInputOnDrag && inputView) {
					inputView.setIsHidden(isDragging);
				}
			},
			setIsSelected(isSelected) {
				regionView.setIsSelected(isSelected);
			},
			setIsDisabled(isDisabled) {
				Dom.toggleClass(g, isDisabled, 'sqd-disabled');
			}
		};
	}
	const createSwitchStepComponentViewFactory = (cfg) => (parent, stepContext, viewContext) => {
		return viewContext.createRegionComponentView(parent, COMPONENT_CLASS_NAME$1, (g, regionViewBuilder) => {
			const step = stepContext.step;
			const paddingTop = cfg.paddingTop1 + cfg.paddingTop2;
			const name = viewContext.getStepName();
			const nameLabelView = LabelView.create(g, paddingTop, cfg.nameLabel, name, 'primary');
			const branchNames = Object.keys(step.branches);
			if (branchNames.length === 0) {
				const width = Math.max(nameLabelView.width, cfg.minBranchWidth) + cfg.paddingX * 2;
				const height = nameLabelView.height + paddingTop + cfg.noBranchPaddingBottom;
				const joinX = width / 2;
				const regionView = regionViewBuilder(g, [width], height);
				Dom.translate(nameLabelView.g, joinX, 0);
				JoinView.createStraightJoin(g, new Vector(joinX, 0), height);
				return createView(g, width, height, joinX, viewContext, null, regionView, cfg);
			}
			const branchComponents = [];
			const branchLabelViews = [];
			const branchSizes = [];
			let totalBranchesWidth = 0;
			let maxBranchesHeight = 0;
			branchNames.forEach(branchName => {
				const labelY = paddingTop + cfg.nameLabel.height + cfg.connectionHeight;
				const translatedBranchName = viewContext.i18n(`stepComponent.${step.type}.branchName`, branchName);
				const labelView = LabelView.create(g, labelY, cfg.branchNameLabel, translatedBranchName, 'secondary');
				const component = viewContext.createSequenceComponent(g, step.branches[branchName]);
				const halfOfWidestBranchElement = Math.max(labelView.width, cfg.minBranchWidth) / 2;
				const branchOffsetLeft = Math.max(halfOfWidestBranchElement - component.view.joinX, 0) + cfg.paddingX;
				const branchOffsetRight = Math.max(halfOfWidestBranchElement - (component.view.width - component.view.joinX), 0) + cfg.paddingX;
				const width = component.view.width + branchOffsetLeft + branchOffsetRight;
				const joinX = component.view.joinX + branchOffsetLeft;
				const offsetX = totalBranchesWidth;
				totalBranchesWidth += width;
				maxBranchesHeight = Math.max(maxBranchesHeight, component.view.height);
				branchComponents.push(component);
				branchLabelViews.push(labelView);
				branchSizes.push({ width, branchOffsetLeft, offsetX, joinX });
			});
			const centerBranchIndex = Math.floor(branchNames.length / 2);
			const centerBranchSize = branchSizes[centerBranchIndex];
			let joinX = centerBranchSize.offsetX;
			if (branchNames.length % 2 !== 0) {
				joinX += centerBranchSize.joinX;
			}
			const halfOfWidestSwitchElement = nameLabelView.width / 2 + cfg.paddingX;
			const switchOffsetLeft = Math.max(halfOfWidestSwitchElement - joinX, 0);
			const switchOffsetRight = Math.max(halfOfWidestSwitchElement - (totalBranchesWidth - joinX), 0);
			const viewWidth = switchOffsetLeft + totalBranchesWidth + switchOffsetRight;
			const viewHeight = maxBranchesHeight + paddingTop + cfg.nameLabel.height + cfg.branchNameLabel.height + cfg.connectionHeight * 2;
			const shiftedJoinX = switchOffsetLeft + joinX;
			Dom.translate(nameLabelView.g, shiftedJoinX, 0);
			const branchOffsetTop = paddingTop + cfg.nameLabel.height + cfg.branchNameLabel.height + cfg.connectionHeight;
			branchComponents.forEach((component, i) => {
				const branchSize = branchSizes[i];
				const branchOffsetLeft = switchOffsetLeft + branchSize.offsetX + branchSize.branchOffsetLeft;
				Dom.translate(branchLabelViews[i].g, switchOffsetLeft + branchSize.offsetX + branchSize.joinX, 0);
				Dom.translate(component.view.g, branchOffsetLeft, branchOffsetTop);
				if (component.hasOutput && stepContext.isOutputConnected) {
					const endOffsetTopOfComponent = paddingTop + cfg.nameLabel.height + cfg.branchNameLabel.height + cfg.connectionHeight + component.view.height;
					const missingHeight = viewHeight - endOffsetTopOfComponent - cfg.connectionHeight;
					if (missingHeight > 0) {
						JoinView.createStraightJoin(g, new Vector(switchOffsetLeft + branchSize.offsetX + branchSize.joinX, endOffsetTopOfComponent), missingHeight);
					}
				}
			});
			JoinView.createStraightJoin(g, new Vector(shiftedJoinX, 0), paddingTop);
			JoinView.createJoins(g, new Vector(shiftedJoinX, paddingTop + cfg.nameLabel.height), branchSizes.map(s => new Vector(switchOffsetLeft + s.offsetX + s.joinX, paddingTop + cfg.nameLabel.height + cfg.connectionHeight)));
			if (stepContext.isOutputConnected) {
				const ongoingSequenceIndexes = branchComponents
					.map((component, index) => (component.hasOutput ? index : null))
					.filter(index => index !== null);
				const ongoingJoinTargets = ongoingSequenceIndexes.map((i) => {
					const branchSize = branchSizes[i];
					return new Vector(switchOffsetLeft + branchSize.offsetX + branchSize.joinX, paddingTop + cfg.connectionHeight + cfg.nameLabel.height + cfg.branchNameLabel.height + maxBranchesHeight);
				});
				if (ongoingJoinTargets.length > 0) {
					JoinView.createJoins(g, new Vector(shiftedJoinX, viewHeight), ongoingJoinTargets);
				}
			}
			const regions = branchSizes.map(s => s.width);
			regions[0] += switchOffsetLeft;
			regions[regions.length - 1] += switchOffsetRight;
			const regionView = regionViewBuilder(g, regions, viewHeight);
			return createView(g, viewWidth, viewHeight, shiftedJoinX, viewContext, branchComponents, regionView, cfg);
		});
	};

	const COMPONENT_CLASS_NAME = 'task';
	const createTaskStepComponentViewFactory = (isInterrupted, cfg) => (parentElement, stepContext, viewContext) => {
		const { step } = stepContext;
		const g = ComponentDom.stepG(COMPONENT_CLASS_NAME, step.type, step.id);
		parentElement.appendChild(g);
		const boxHeight = cfg.paddingY * 2 + cfg.iconSize;
		const text = Dom.svg('text', {
			x: cfg.paddingLeft + cfg.iconSize + cfg.textMarginLeft,
			y: boxHeight / 2,
			class: 'sqd-step-task-text'
		});
		text.textContent = viewContext.getStepName();
		g.appendChild(text);
		const textWidth = Math.max(text.getBBox().width, cfg.minTextWidth);
		const boxWidth = cfg.iconSize + cfg.paddingLeft + cfg.paddingRight + cfg.textMarginLeft + textWidth;
		const rect = Dom.svg('rect', {
			x: 0.5,
			y: 0.5,
			class: 'sqd-step-task-rect',
			width: boxWidth,
			height: boxHeight,
			rx: cfg.radius,
			ry: cfg.radius
		});
		g.insertBefore(rect, text);
		const iconUrl = viewContext.getStepIconUrl();
		const icon = iconUrl
			? Dom.svg('image', {
				href: iconUrl
			})
			: Dom.svg('rect', {
				class: 'sqd-step-task-empty-icon',
				rx: cfg.radius,
				ry: cfg.radius
			});
		Dom.attrs(icon, {
			x: cfg.paddingLeft,
			y: cfg.paddingY,
			width: cfg.iconSize,
			height: cfg.iconSize
		});
		g.appendChild(icon);
		const isInputViewHidden = !stepContext.isInputConnected; // TODO: handle inside the folder
		const isOutputViewHidden = isInterrupted;
		const inputView = isInputViewHidden ? null : InputView.createRoundInput(g, boxWidth / 2, 0, cfg.inputSize);
		const outputView = isOutputViewHidden ? null : OutputView.create(g, boxWidth / 2, boxHeight, cfg.outputSize);
		return {
			g,
			width: boxWidth,
			height: boxHeight,
			joinX: boxWidth / 2,
			components: null,
			placeholders: null,
			hasOutput: !!outputView,
			getClientPosition() {
				return getAbsolutePosition(rect);
			},
			resolveClick(click) {
				return g.contains(click.element) ? true : null;
			},
			setIsDragging(isDragging) {
				inputView === null || inputView === void 0 ? void 0 : inputView.setIsHidden(isDragging);
				outputView === null || outputView === void 0 ? void 0 : outputView.setIsHidden(isDragging);
			},
			setIsDisabled(isDisabled) {
				Dom.toggleClass(g, isDisabled, 'sqd-disabled');
			},
			setIsSelected(isSelected) {
				Dom.toggleClass(rect, isSelected, 'sqd-selected');
			}
		};
	};

	class CenteredViewportCalculator {
		static center(padding, canvasSize, rootComponentSize) {
			if (canvasSize.x === 0 || canvasSize.y === 0) {
				return {
					position: new Vector(0, 0),
					scale: 1
				};
			}
			const canvasSafeWidth = Math.max(canvasSize.x - padding * 2, 0);
			const canvasSafeHeight = Math.max(canvasSize.y - padding * 2, 0);
			const scale = Math.min(Math.min(canvasSafeWidth / rootComponentSize.x, canvasSafeHeight / rootComponentSize.y), 1);
			const width = rootComponentSize.x * scale;
			const height = rootComponentSize.y * scale;
			const x = Math.max(0, (canvasSize.x - width) / 2);
			const y = Math.max(0, (canvasSize.y - height) / 2);
			return {
				position: new Vector(x, y),
				scale
			};
		}
		static getFocusedOnComponent(canvasSize, viewport, componentPosition, componentSize) {
			const realPosition = viewport.position.divideByScalar(viewport.scale).subtract(componentPosition.divideByScalar(viewport.scale));
			const componentOffset = componentSize.divideByScalar(2);
			const position = realPosition.add(canvasSize.divideByScalar(2)).subtract(componentOffset);
			return { position, scale: 1 };
		}
	}

	class ClassicWheelController {
		static create(api) {
			return new ClassicWheelController(api);
		}
		constructor(api) {
			this.api = api;
		}
		onWheel(e) {
			this.api.handleWheelEvent(e);
		}
	}

	class ClassicWheelControllerExtension {
		constructor() {
			this.create = ClassicWheelController.create;
		}
	}

	class NextQuantifiedNumber {
		constructor(values) {
			this.values = values;
		}
		next(value, direction) {
			let bestIndex = 0;
			let bestDistance = Number.MAX_VALUE;
			for (let i = 0; i < this.values.length; i++) {
				const distance = Math.abs(this.values[i] - value);
				if (bestDistance > distance) {
					bestIndex = i;
					bestDistance = distance;
				}
			}
			let index;
			if (direction) {
				index = Math.min(bestIndex + 1, this.values.length - 1);
			}
			else {
				index = Math.max(bestIndex - 1, 0);
			}
			return {
				current: this.values[bestIndex],
				next: this.values[index]
			};
		}
		limit(scale) {
			const min = this.values[0];
			const max = this.values[this.values.length - 1];
			return Math.min(Math.max(scale, min), max);
		}
	}

	const defaultConfiguration$5 = {
		scales: [0.06, 0.08, 0.1, 0.12, 0.16, 0.2, 0.26, 0.32, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1],
		smoothDeltaYLimit: 16,
		padding: 10
	};
	class DefaultViewportController {
		static create(api, configuration) {
			const config = configuration !== null && configuration !== void 0 ? configuration : defaultConfiguration$5;
			const nqn = new NextQuantifiedNumber(config.scales);
			return new DefaultViewportController(config.smoothDeltaYLimit, nqn, api, config.padding);
		}
		constructor(smoothDeltaYLimit, nqn, api, padding) {
			this.smoothDeltaYLimit = smoothDeltaYLimit;
			this.nqn = nqn;
			this.api = api;
			this.padding = padding;
		}
		getDefault() {
			const rootComponentSize = this.api.getRootComponentSize();
			const canvasSize = this.api.getCanvasSize();
			return CenteredViewportCalculator.center(this.padding, canvasSize, rootComponentSize);
		}
		getZoomed(direction) {
			const current = this.api.getViewport();
			const nextScale = this.nqn.next(current.scale, direction);
			if (nextScale) {
				return {
					position: current.position,
					scale: nextScale.next
				};
			}
			return null;
		}
		getFocusedOnComponent(componentPosition, componentSize) {
			const viewport = this.api.getViewport();
			const canvasSize = this.api.getCanvasSize();
			return CenteredViewportCalculator.getFocusedOnComponent(canvasSize, viewport, componentPosition, componentSize);
		}
		getNextScale(scale, direction) {
			return this.nqn.next(scale, direction);
		}
		limitScale(scale) {
			return this.nqn.limit(scale);
		}
	}

	class DefaultViewportControllerExtension {
		static create(configuration) {
			return new DefaultViewportControllerExtension(configuration);
		}
		constructor(configuration) {
			this.configuration = configuration;
		}
		create(api) {
			return DefaultViewportController.create(api, this.configuration);
		}
	}

	class StepComponent {
		static create(view, stepContext, componentContext) {
			const badges = Badges.createForStep(stepContext, view, componentContext);
			return new StepComponent(view, stepContext.step, stepContext.parentSequence, view.hasOutput, badges);
		}
		constructor(view, step, parentSequence, hasOutput, badges) {
			this.view = view;
			this.step = step;
			this.parentSequence = parentSequence;
			this.hasOutput = hasOutput;
			this.badges = badges;
		}
		findById(stepId) {
			if (this.step.id === stepId) {
				return this;
			}
			if (this.view.components) {
				for (const component of this.view.components) {
					const result = component.findById(stepId);
					if (result) {
						return result;
					}
				}
			}
			return null;
		}
		resolveClick(click) {
			if (this.view.components) {
				for (const component of this.view.components) {
					const result = component.resolveClick(click);
					if (result) {
						return result;
					}
				}
			}
			if (this.view.placeholders) {
				for (const placeholder of this.view.placeholders) {
					const result = placeholder.resolveClick(click);
					if (result) {
						return result;
					}
				}
			}
			const badgeResult = this.badges.resolveClick(click);
			if (badgeResult) {
				return badgeResult;
			}
			const viewResult = this.view.resolveClick(click);
			if (viewResult) {
				return viewResult === true
					? {
						type: exports.ClickCommandType.selectStep,
						component: this
					}
					: viewResult;
			}
			return null;
		}
		resolvePlaceholders(skipComponent, result) {
			if (skipComponent !== this) {
				if (this.view.components) {
					this.view.components.forEach(component => component.resolvePlaceholders(skipComponent, result));
				}
				if (this.view.placeholders) {
					this.view.placeholders.forEach(ph => result.placeholders.push(ph));
				}
				result.components.push(this);
			}
		}
		setIsDragging(isDragging) {
			this.view.setIsDragging(isDragging);
		}
		setIsSelected(isSelected) {
			this.view.setIsSelected(isSelected);
		}
		setIsDisabled(isDisabled) {
			this.view.setIsDisabled(isDisabled);
		}
		updateBadges(result) {
			if (this.view.components) {
				this.view.components.forEach(component => component.updateBadges(result));
			}
			this.badges.update(result);
		}
	}

	class StepExtensionResolver {
		static create(services) {
			const dict = {};
			for (let i = services.steps.length - 1; i >= 0; i--) {
				const extension = services.steps[i];
				dict[extension.componentType] = extension;
			}
			return new StepExtensionResolver(dict);
		}
		constructor(dict) {
			this.dict = dict;
		}
		resolve(componentType) {
			const extension = this.dict[componentType];
			if (!extension) {
				throw new Error(`Not supported component type: ${componentType}`);
			}
			return extension;
		}
	}

	class PlaceholderController {
		static create(configuration) {
			return new PlaceholderController(configuration);
		}
		constructor(configuration) {
			var _a, _b, _c, _d;
			this.configuration = configuration;
			this.canCreate = (_b = (_a = this.configuration) === null || _a === void 0 ? void 0 : _a.canCreate) !== null && _b !== void 0 ? _b : (() => true);
			this.canShow = (_d = (_c = this.configuration) === null || _c === void 0 ? void 0 : _c.canShow) !== null && _d !== void 0 ? _d : (() => true);
		}
	}

	class RectPlaceholderView {
		static create(parent, width, height, radius, iconSize, direction) {
			const g = Dom.svg('g', {
				visibility: 'hidden',
				class: 'sqd-placeholder'
			});
			parent.appendChild(g);
			const rect = Dom.svg('rect', {
				class: 'sqd-placeholder-rect',
				width,
				height,
				rx: radius,
				ry: radius
			});
			g.appendChild(rect);
			if (direction) {
				const iconD = direction === exports.PlaceholderDirection.in ? Icons.folderIn : Icons.folderOut;
				const icon = Icons.appendPath(g, 'sqd-placeholder-icon-path', iconD, iconSize);
				Dom.translate(icon, (width - iconSize) / 2, (height - iconSize) / 2);
			}
			parent.appendChild(g);
			return new RectPlaceholderView(rect, g);
		}
		constructor(rect, g) {
			this.rect = rect;
			this.g = g;
		}
		setIsHover(isHover) {
			Dom.toggleClass(this.g, isHover, 'sqd-hover');
		}
		setIsVisible(isVisible) {
			Dom.attrs(this.g, {
				visibility: isVisible ? 'visible' : 'hidden'
			});
		}
	}

	class RectPlaceholder {
		static create(parent, size, direction, sequence, index, radius, iconSize) {
			const view = RectPlaceholderView.create(parent, size.x, size.y, radius, iconSize, direction);
			return new RectPlaceholder(view, sequence, index);
		}
		constructor(view, parentSequence, index) {
			this.view = view;
			this.parentSequence = parentSequence;
			this.index = index;
		}
		getClientRect() {
			return this.view.rect.getBoundingClientRect();
		}
		setIsHover(isHover) {
			this.view.setIsHover(isHover);
		}
		setIsVisible(isVisible) {
			this.view.setIsVisible(isVisible);
		}
		resolveClick() {
			return null;
		}
	}

	class DefaultRegionView {
		static create(parent, widths, height) {
			const totalWidth = widths.reduce((result, width) => result + width, 0);
			const lines = [
				drawLine(parent, 0, 0, totalWidth, 0),
				drawLine(parent, 0, 0, 0, height),
				drawLine(parent, 0, height, totalWidth, height),
				drawLine(parent, totalWidth, 0, totalWidth, height)
			];
			let offsetX = widths[0];
			for (let i = 1; i < widths.length; i++) {
				lines.push(drawLine(parent, offsetX, 0, offsetX, height));
				offsetX += widths[i];
			}
			return new DefaultRegionView(lines, totalWidth, height);
		}
		constructor(lines, width, height) {
			this.lines = lines;
			this.width = width;
			this.height = height;
		}
		getClientPosition() {
			return getAbsolutePosition(this.lines[0]);
		}
		resolveClick(click) {
			const regionPosition = this.getClientPosition();
			const d = click.position.subtract(regionPosition);
			if (d.x >= 0 && d.y >= 0 && d.x < this.width * click.scale && d.y < this.height * click.scale) {
				return true;
			}
			return null;
		}
		setIsSelected(isSelected) {
			this.lines.forEach(region => {
				Dom.toggleClass(region, isSelected, 'sqd-selected');
			});
		}
	}
	function drawLine(parent, x1, y1, x2, y2) {
		const line = Dom.svg('line', {
			class: 'sqd-region',
			x1,
			y1,
			x2,
			y2
		});
		parent.insertBefore(line, parent.firstChild);
		return line;
	}

	class DefaultRegionComponentViewExtension {
		create(parentElement, componentClassName, stepContext, _, contentFactory) {
			const g = ComponentDom.stepG(componentClassName, stepContext.step.type, stepContext.step.id);
			parentElement.appendChild(g);
			return contentFactory(g, DefaultRegionView.create);
		}
	}

	class DefaultViewportControllerDesignerExtension {
		static create(configuration) {
			return new DefaultViewportControllerDesignerExtension(DefaultViewportControllerExtension.create(configuration));
		}
		constructor(viewportController) {
			this.viewportController = viewportController;
		}
	}

	class LineGrid {
		static create(size) {
			const path = Dom.svg('path', {
				class: 'sqd-line-grid-path',
				fill: 'none'
			});
			return new LineGrid(size, path);
		}
		constructor(size, element) {
			this.size = size;
			this.element = element;
		}
		setScale(_, scaledSize) {
			Dom.attrs(this.element, {
				d: `M ${scaledSize.x} 0 L 0 0 0 ${scaledSize.y}`
			});
		}
	}

	const defaultConfiguration$4 = {
		gridSizeX: 48,
		gridSizeY: 48
	};
	class LineGridExtension {
		static create(configuration) {
			return new LineGridExtension(configuration !== null && configuration !== void 0 ? configuration : defaultConfiguration$4);
		}
		constructor(configuration) {
			this.configuration = configuration;
		}
		create() {
			const size = new Vector(this.configuration.gridSizeX, this.configuration.gridSizeY);
			return LineGrid.create(size);
		}
	}

	class LineGridDesignerExtension {
		static create(configuration) {
			const grid = LineGridExtension.create(configuration);
			return new LineGridDesignerExtension(grid);
		}
		constructor(grid) {
			this.grid = grid;
		}
	}

	class StartStopRootComponentDesignerExtension {
		static create(configuration) {
			return new StartStopRootComponentDesignerExtension(StartStopRootComponentExtension.create(configuration));
		}
		constructor(rootComponent) {
			this.rootComponent = rootComponent;
		}
	}

	const defaultConfiguration$3 = {
		view: {
			paddingTop: 20,
			paddingX: 20,
			inputSize: 18,
			inputRadius: 4,
			inputIconSize: 14,
			autoHideInputOnDrag: true,
			label: {
				height: 22,
				paddingX: 10,
				minWidth: 50,
				radius: 10
			}
		}
	};
	class ContainerStepExtension {
		static create(configuration) {
			return new ContainerStepExtension(configuration !== null && configuration !== void 0 ? configuration : defaultConfiguration$3);
		}
		constructor(configuration) {
			this.configuration = configuration;
			this.componentType = 'container';
			this.createComponentView = createContainerStepComponentViewFactory(this.configuration.view);
		}
	}

	const defaultConfiguration$2 = {
		view: {
			minBranchWidth: 88,
			paddingX: 20,
			paddingTop1: 0,
			paddingTop2: 22,
			connectionHeight: 20,
			noBranchPaddingBottom: 24,
			inputSize: 18,
			inputIconSize: 14,
			inputRadius: 4,
			autoHideInputOnDrag: true,
			branchNameLabel: {
				height: 22,
				paddingX: 10,
				minWidth: 50,
				radius: 10
			},
			nameLabel: {
				height: 22,
				paddingX: 10,
				minWidth: 50,
				radius: 10
			}
		}
	};
	class SwitchStepExtension {
		static create(configuration) {
			return new SwitchStepExtension(configuration !== null && configuration !== void 0 ? configuration : defaultConfiguration$2);
		}
		constructor(configuration) {
			this.configuration = configuration;
			this.componentType = 'switch';
			this.createComponentView = createSwitchStepComponentViewFactory(this.configuration.view);
		}
	}

	const defaultConfiguration$1 = {
		view: {
			paddingLeft: 12,
			paddingRight: 12,
			paddingY: 10,
			textMarginLeft: 12,
			minTextWidth: 70,
			iconSize: 22,
			radius: 5,
			inputSize: 14,
			outputSize: 10
		}
	};
	class TaskStepExtension {
		static create(configuration) {
			return new TaskStepExtension(configuration !== null && configuration !== void 0 ? configuration : defaultConfiguration$1);
		}
		constructor(configuration) {
			this.configuration = configuration;
			this.componentType = 'task';
			this.createComponentView = createTaskStepComponentViewFactory(false, this.configuration.view);
		}
	}

	const defaultViewConfiguration = {
		isRegionEnabled: true,
		paddingY: 10,
		connectionHeight: 20,
		emptyPaddingX: 20,
		emptyPaddingY: 20,
		emptyInputSize: 14,
		emptyOutputSize: 10,
		emptyIconSize: 24
	};
	class LaunchPadStepExtension {
		static create(configuration) {
			return new LaunchPadStepExtension(configuration);
		}
		constructor(configuration) {
			var _a, _b;
			this.configuration = configuration;
			this.componentType = 'launchPad';
			this.createComponentView = createLaunchPadStepComponentViewFactory(false, (_b = (_a = this.configuration) === null || _a === void 0 ? void 0 : _a.view) !== null && _b !== void 0 ? _b : defaultViewConfiguration);
		}
	}

	class StepsDesignerExtension {
		static create(configuration) {
			const steps = [];
			if (configuration.container) {
				steps.push(ContainerStepExtension.create(configuration.container));
			}
			if (configuration.switch) {
				steps.push(SwitchStepExtension.create(configuration.switch));
			}
			if (configuration.task) {
				steps.push(TaskStepExtension.create(configuration.task));
			}
			if (configuration.launchPad) {
				steps.push(LaunchPadStepExtension.create(configuration.launchPad));
			}
			return new StepsDesignerExtension(steps);
		}
		constructor(steps) {
			this.steps = steps;
		}
	}

	class DefinitionValidator {
		constructor(configuration, state) {
			this.configuration = configuration;
			this.state = state;
		}
		validateStep(step, parentSequence) {
			var _a;
			if ((_a = this.configuration) === null || _a === void 0 ? void 0 : _a.step) {
				return this.configuration.step(step, parentSequence, this.state.definition);
			}
			return true;
		}
		validateRoot() {
			var _a;
			if ((_a = this.configuration) === null || _a === void 0 ? void 0 : _a.root) {
				return this.configuration.root(this.state.definition);
			}
			return true;
		}
	}

	class IconProvider {
		constructor(configuration) {
			this.configuration = configuration;
		}
		getIconUrl(step) {
			if (this.configuration.iconUrlProvider) {
				return this.configuration.iconUrlProvider(step.componentType, step.type);
			}
			return null;
		}
	}

	class StepComponentViewContextFactory {
		static create(stepContext, componentContext) {
			const preferenceKeyPrefix = stepContext.step.id + ':';
			return {
				i18n: componentContext.i18n,
				getStepIconUrl: () => componentContext.iconProvider.getIconUrl(stepContext.step),
				getStepName: () => componentContext.i18n(`step.${stepContext.step.type}.name`, stepContext.step.name),
				createStepComponent: (parentElement, parentSequence, step, position) => {
					return componentContext.stepComponentFactory.create(parentElement, {
						parentSequence,
						step,
						depth: stepContext.depth + 1,
						position,
						isInputConnected: stepContext.isInputConnected,
						isOutputConnected: stepContext.isOutputConnected,
						isPreview: stepContext.isPreview
					}, componentContext);
				},
				createSequenceComponent: (parentElement, sequence) => {
					const sequenceContext = {
						sequence,
						depth: stepContext.depth + 1,
						isInputConnected: true,
						isOutputConnected: stepContext.isOutputConnected,
						isPreview: stepContext.isPreview
					};
					return componentContext.services.sequenceComponent.create(parentElement, sequenceContext, componentContext);
				},
				createRegionComponentView(parentElement, componentClassName, contentFactory) {
					return componentContext.services.regionComponentView.create(parentElement, componentClassName, stepContext, this, contentFactory);
				},
				getPlaceholderGapSize: orientation => componentContext.services.placeholder.getGapSize(orientation),
				createPlaceholderForGap: componentContext.services.placeholder.createForGap.bind(componentContext.services.placeholder),
				createPlaceholderForArea: componentContext.services.placeholder.createForArea.bind(componentContext.services.placeholder),
				getPreference: (key) => componentContext.preferenceStorage.getItem(preferenceKeyPrefix + key),
				setPreference: (key, value) => componentContext.preferenceStorage.setItem(preferenceKeyPrefix + key, value)
			};
		}
	}

	class StepComponentFactory {
		constructor(stepExtensionResolver) {
			this.stepExtensionResolver = stepExtensionResolver;
		}
		create(parentElement, stepContext, componentContext) {
			const viewContext = StepComponentViewContextFactory.create(stepContext, componentContext);
			const extension = this.stepExtensionResolver.resolve(stepContext.step.componentType);
			const view = extension.createComponentView(parentElement, stepContext, viewContext);
			const wrappedView = componentContext.services.stepComponentViewWrapper.wrap(view, stepContext);
			return StepComponent.create(wrappedView, stepContext, componentContext);
		}
	}

	class ComponentContext {
		static create(configuration, state, stepExtensionResolver, placeholderController, definitionWalker, preferenceStorage, i18n, services) {
			const validator = new DefinitionValidator(configuration.validator, state);
			const iconProvider = new IconProvider(configuration.steps);
			const stepComponentFactory = new StepComponentFactory(stepExtensionResolver);
			return new ComponentContext(configuration.shadowRoot, validator, iconProvider, placeholderController, stepComponentFactory, definitionWalker, services, preferenceStorage, i18n);
		}
		constructor(shadowRoot, validator, iconProvider, placeholderController, stepComponentFactory, definitionWalker, services, preferenceStorage, i18n) {
			this.shadowRoot = shadowRoot;
			this.validator = validator;
			this.iconProvider = iconProvider;
			this.placeholderController = placeholderController;
			this.stepComponentFactory = stepComponentFactory;
			this.definitionWalker = definitionWalker;
			this.services = services;
			this.preferenceStorage = preferenceStorage;
			this.i18n = i18n;
		}
	}

	class CustomActionController {
		constructor(configuration, state, stateModifier) {
			this.configuration = configuration;
			this.state = state;
			this.stateModifier = stateModifier;
		}
		trigger(action, step, sequence) {
			const handler = this.configuration.customActionHandler;
			if (!handler) {
				console.warn(`Custom action handler is not defined (action type: ${action.type})`);
				return;
			}
			const context = this.createCustomActionHandlerContext();
			handler(action, step, sequence, context);
		}
		createCustomActionHandlerContext() {
			return {
				notifyStepNameChanged: (stepId) => this.notifyStepChanged(exports.DefinitionChangeType.stepNameChanged, stepId, false),
				notifyStepPropertiesChanged: (stepId) => this.notifyStepChanged(exports.DefinitionChangeType.stepPropertyChanged, stepId, false),
				notifyStepInserted: (stepId) => this.notifyStepChanged(exports.DefinitionChangeType.stepInserted, stepId, true),
				notifyStepMoved: (stepId) => this.notifyStepChanged(exports.DefinitionChangeType.stepMoved, stepId, true),
				notifyStepDeleted: (stepId) => this.notifyStepChanged(exports.DefinitionChangeType.stepDeleted, stepId, true)
			};
		}
		notifyStepChanged(changeType, stepId, updateDependencies) {
			if (!stepId) {
				throw new Error('Step id is empty');
			}
			this.state.notifyDefinitionChanged(changeType, stepId);
			if (updateDependencies) {
				this.stateModifier.updateDependencies();
			}
		}
	}

	class EditorView {
		static create(parent) {
			return new EditorView(parent);
		}
		constructor(parent) {
			this.parent = parent;
			this.currentContainer = null;
		}
		setContent(content, className) {
			const container = Dom.element('div', {
				class: className
			});
			container.appendChild(content);
			if (this.currentContainer) {
				this.parent.removeChild(this.currentContainer);
			}
			this.parent.appendChild(container);
			this.currentContainer = container;
		}
		destroy() {
			if (this.currentContainer) {
				this.parent.removeChild(this.currentContainer);
			}
		}
	}

	class Editor {
		static create(parent, api, stepEditorClassName, stepEditorProvider, rootEditorClassName, rootEditorProvider, customSelectedStepIdProvider) {
			const view = EditorView.create(parent);
			function render(step) {
				const definition = api.getDefinition();
				let content;
				let className;
				if (step) {
					const stepContext = api.createStepEditorContext(step.id);
					content = stepEditorProvider(step, stepContext, definition, api.isReadonly());
					className = stepEditorClassName;
				}
				else {
					const rootContext = api.createRootEditorContext();
					content = rootEditorProvider(definition, rootContext, api.isReadonly());
					className = rootEditorClassName;
				}
				view.setContent(content, className);
			}
			const renderer = api.runRenderer(step => render(step), customSelectedStepIdProvider);
			return new Editor(view, renderer);
		}
		constructor(view, renderer) {
			this.view = view;
			this.renderer = renderer;
		}
		destroy() {
			this.view.destroy();
			this.renderer.destroy();
		}
	}

	function readMousePosition(e) {
		return new Vector(e.pageX, e.pageY);
	}
	function readTouchClientPosition(e) {
		if (e.touches.length > 0) {
			const touch = e.touches[0];
			return new Vector(touch.clientX, touch.clientY);
		}
		throw new Error('Unknown touch position');
	}
	function readTouchPosition(e) {
		if (e.touches.length > 0) {
			const touch = e.touches[0];
			return new Vector(touch.pageX, touch.pageY);
		}
		throw new Error('Unknown touch position');
	}
	function calculateFingerDistance(e) {
		if (e.touches.length === 2) {
			const t0 = e.touches[0];
			const t1 = e.touches[1];
			return Math.hypot(t0.clientX - t1.clientX, t0.clientY - t1.clientY);
		}
		throw new Error('Cannot calculate finger distance');
	}
	function readFingerCenterPoint(e) {
		if (e.touches.length === 2) {
			const t0 = e.touches[0];
			const t1 = e.touches[1];
			return new Vector((t0.pageX + t1.pageX) / 2, (t0.pageY + t1.pageY) / 2);
		}
		throw new Error('Cannot calculate finger center point');
	}

	const notInitializedError$1 = 'State is not initialized';
	const nonPassiveOptions$1 = {
		passive: false
	};
	class BehaviorController {
		static create(shadowRoot) {
			return new BehaviorController(shadowRoot !== null && shadowRoot !== void 0 ? shadowRoot : document, shadowRoot);
		}
		constructor(dom, shadowRoot) {
			this.dom = dom;
			this.shadowRoot = shadowRoot;
			this.previousEndToken = null;
			this.state = null;
			this.onMouseMove = (e) => {
				e.preventDefault();
				e.stopPropagation();
				this.move(readMousePosition(e));
			};
			this.onTouchMove = (e) => {
				e.preventDefault();
				e.stopPropagation();
				this.move(readTouchPosition(e));
			};
			this.onMouseUp = (e) => {
				e.preventDefault();
				e.stopPropagation();
				this.stop(false, e.target);
			};
			this.onTouchEnd = (e) => {
				var _a;
				e.preventDefault();
				e.stopPropagation();
				if (!this.state) {
					throw new Error(notInitializedError$1);
				}
				const position = (_a = this.state.lastPosition) !== null && _a !== void 0 ? _a : this.state.startPosition;
				const element = this.dom.elementFromPoint(position.x, position.y);
				this.stop(false, element);
			};
			this.onTouchStart = (e) => {
				e.preventDefault();
				e.stopPropagation();
				if (e.touches.length !== 1) {
					this.stop(true, null);
				}
			};
		}
		start(startPosition, behavior) {
			if (this.state) {
				this.stop(true, null);
				return;
			}
			this.state = {
				startPosition,
				behavior
			};
			behavior.onStart(this.state.startPosition);
			if (this.shadowRoot) {
				this.bind(this.shadowRoot);
			}
			this.bind(window);
		}
		bind(target) {
			target.addEventListener('mousemove', this.onMouseMove, false);
			target.addEventListener('touchmove', this.onTouchMove, nonPassiveOptions$1);
			target.addEventListener('mouseup', this.onMouseUp, false);
			target.addEventListener('touchend', this.onTouchEnd, nonPassiveOptions$1);
			target.addEventListener('touchstart', this.onTouchStart, nonPassiveOptions$1);
		}
		unbind(target) {
			target.removeEventListener('mousemove', this.onMouseMove, false);
			target.removeEventListener('touchmove', this.onTouchMove, nonPassiveOptions$1);
			target.removeEventListener('mouseup', this.onMouseUp, false);
			target.removeEventListener('touchend', this.onTouchEnd, nonPassiveOptions$1);
			target.removeEventListener('touchstart', this.onTouchStart, nonPassiveOptions$1);
		}
		move(position) {
			if (!this.state) {
				throw new Error(notInitializedError$1);
			}
			this.state.lastPosition = position;
			const delta = this.state.startPosition.subtract(position);
			const newBehavior = this.state.behavior.onMove(delta);
			if (newBehavior) {
				this.state.behavior.onEnd(true, null, null);
				this.state.behavior = newBehavior;
				this.state.startPosition = position;
				this.state.behavior.onStart(this.state.startPosition);
			}
		}
		stop(interrupt, element) {
			if (!this.state) {
				throw new Error(notInitializedError$1);
			}
			if (this.shadowRoot) {
				this.unbind(this.shadowRoot);
			}
			this.unbind(window);
			const endToken = this.state.behavior.onEnd(interrupt, element, this.previousEndToken);
			this.state = null;
			this.previousEndToken = endToken || null;
		}
	}

	class SequenceModifier {
		static tryMoveStep(sourceSequence, step, targetSequence, targetIndex) {
			const sourceIndex = sourceSequence.indexOf(step);
			if (sourceIndex < 0) {
				throw new Error('Step not found in source sequence');
			}
			const isSameSequence = sourceSequence === targetSequence;
			if (isSameSequence) {
				if (sourceIndex < targetIndex) {
					targetIndex--;
				}
				if (sourceIndex === targetIndex) {
					return null; // No changes
				}
			}
			return function apply() {
				sourceSequence.splice(sourceIndex, 1);
				targetSequence.splice(targetIndex, 0, step);
			};
		}
		static insertStep(step, targetSequence, targetIndex) {
			targetSequence.splice(targetIndex, 0, step);
		}
		static deleteStep(step, parentSequence) {
			const index = parentSequence.indexOf(step);
			if (index < 0) {
				throw new Error('Unknown step');
			}
			parentSequence.splice(index, 1);
		}
	}

	class StepDuplicator {
		constructor(uidGenerator, definitionWalker) {
			this.uidGenerator = uidGenerator;
			this.definitionWalker = definitionWalker;
		}
		duplicate(step) {
			const newStep = ObjectCloner.deepClone(step);
			newStep.id = this.uidGenerator();
			this.definitionWalker.forEachChildren(newStep, s => {
				s.id = this.uidGenerator();
			});
			return newStep;
		}
	}

	class FolderPathDefinitionModifierDependency {
		constructor(state, definitionWalker) {
			this.state = state;
			this.definitionWalker = definitionWalker;
		}
		update() {
			for (let index = 0; index < this.state.folderPath.length; index++) {
				const stepId = this.state.folderPath[index];
				const found = this.definitionWalker.findById(this.state.definition, stepId);
				if (!found) {
					// We need to update path if any folder is deleted.
					const newPath = this.state.folderPath.slice(0, index);
					this.state.setFolderPath(newPath);
					break;
				}
			}
		}
	}

	class SelectedStepIdDefinitionModifierDependency {
		constructor(state, definitionWalker) {
			this.state = state;
			this.definitionWalker = definitionWalker;
		}
		update() {
			if (this.state.selectedStepId) {
				const found = this.definitionWalker.findById(this.state.definition, this.state.selectedStepId);
				if (!found) {
					// We need to unselect step when it's deleted.
					this.state.setSelectedStepId(null);
				}
			}
		}
	}

	class StateModifier {
		static create(definitionWalker, uidGenerator, state, configuration) {
			const dependencies = [];
			dependencies.push(new SelectedStepIdDefinitionModifierDependency(state, definitionWalker));
			dependencies.push(new FolderPathDefinitionModifierDependency(state, definitionWalker));
			return new StateModifier(definitionWalker, uidGenerator, state, configuration, dependencies);
		}
		constructor(definitionWalker, uidGenerator, state, configuration, dependencies) {
			this.definitionWalker = definitionWalker;
			this.uidGenerator = uidGenerator;
			this.state = state;
			this.configuration = configuration;
			this.dependencies = dependencies;
		}
		addDependency(dependency) {
			this.dependencies.push(dependency);
		}
		isSelectable(step, parentSequence) {
			return this.configuration.isSelectable ? this.configuration.isSelectable(step, parentSequence) : true;
		}
		trySelectStep(step, parentSequence) {
			if (this.isSelectable(step, parentSequence)) {
				this.state.setSelectedStepId(step.id);
				return true;
			}
			return false;
		}
		trySelectStepById(stepId) {
			if (this.configuration.isSelectable) {
				const result = this.definitionWalker.getParentSequence(this.state.definition, stepId);
				this.trySelectStep(result.step, result.parentSequence);
			}
			else {
				this.state.setSelectedStepId(stepId);
			}
		}
		isDeletable(stepId) {
			if (this.configuration.isDeletable) {
				const result = this.definitionWalker.getParentSequence(this.state.definition, stepId);
				return this.configuration.isDeletable(result.step, result.parentSequence);
			}
			return true;
		}
		tryDelete(stepId) {
			const result = this.definitionWalker.getParentSequence(this.state.definition, stepId);
			const canDeleteStep = this.configuration.canDeleteStep
				? this.configuration.canDeleteStep(result.step, result.parentSequence)
				: true;
			if (!canDeleteStep) {
				return false;
			}
			SequenceModifier.deleteStep(result.step, result.parentSequence);
			this.state.notifyDefinitionChanged(exports.DefinitionChangeType.stepDeleted, result.step.id);
			this.updateDependencies();
			return true;
		}
		tryInsert(step, targetSequence, targetIndex) {
			const canInsertStep = this.configuration.canInsertStep ? this.configuration.canInsertStep(step, targetSequence, targetIndex) : true;
			if (!canInsertStep) {
				return false;
			}
			SequenceModifier.insertStep(step, targetSequence, targetIndex);
			this.state.notifyDefinitionChanged(exports.DefinitionChangeType.stepInserted, step.id);
			if (!this.configuration.isAutoSelectDisabled) {
				this.trySelectStepById(step.id);
			}
			return true;
		}
		isDraggable(step, parentSequence) {
			return this.configuration.isDraggable ? this.configuration.isDraggable(step, parentSequence) : true;
		}
		tryMove(sourceSequence, step, targetSequence, targetIndex) {
			const apply = SequenceModifier.tryMoveStep(sourceSequence, step, targetSequence, targetIndex);
			if (!apply) {
				return false;
			}
			const canMoveStep = this.configuration.canMoveStep
				? this.configuration.canMoveStep(sourceSequence, step, targetSequence, targetIndex)
				: true;
			if (!canMoveStep) {
				return false;
			}
			apply();
			this.state.notifyDefinitionChanged(exports.DefinitionChangeType.stepMoved, step.id);
			if (!this.configuration.isAutoSelectDisabled) {
				this.trySelectStep(step, targetSequence);
			}
			return true;
		}
		isDuplicable(step, parentSequence) {
			return this.configuration.isDuplicable ? this.configuration.isDuplicable(step, parentSequence) : false;
		}
		tryDuplicate(step, parentSequence) {
			const duplicator = new StepDuplicator(this.uidGenerator, this.definitionWalker);
			const index = parentSequence.indexOf(step);
			const newStep = duplicator.duplicate(step);
			return this.tryInsert(newStep, parentSequence, index + 1);
		}
		replaceDefinition(definition) {
			if (!definition) {
				throw new Error('Definition is empty');
			}
			this.state.setDefinition(definition);
			this.updateDependencies();
		}
		updateDependencies() {
			this.dependencies.forEach(dependency => dependency.update());
		}
	}

	class DesignerState {
		constructor(definition, isReadonly, isToolboxCollapsed, isEditorCollapsed) {
			this.definition = definition;
			this.isReadonly = isReadonly;
			this.isToolboxCollapsed = isToolboxCollapsed;
			this.isEditorCollapsed = isEditorCollapsed;
			this.onViewportChanged = new SimpleEvent();
			this.onSelectedStepIdChanged = new SimpleEvent();
			this.onFolderPathChanged = new SimpleEvent();
			this.onIsReadonlyChanged = new SimpleEvent();
			this.onIsDraggingChanged = new SimpleEvent();
			this.onIsDragDisabledChanged = new SimpleEvent();
			this.onDefinitionChanged = new SimpleEvent();
			this.onIsToolboxCollapsedChanged = new SimpleEvent();
			this.onIsEditorCollapsedChanged = new SimpleEvent();
			this.viewport = {
				position: new Vector(0, 0),
				scale: 1
			};
			this.selectedStepId = null;
			this.folderPath = [];
			this.isDragging = false;
			this.isDragDisabled = false;
		}
		setSelectedStepId(stepId) {
			if (this.selectedStepId !== stepId) {
				this.selectedStepId = stepId;
				this.onSelectedStepIdChanged.forward(stepId);
			}
		}
		pushStepIdToFolderPath(stepId) {
			this.folderPath.push(stepId);
			this.onFolderPathChanged.forward(this.folderPath);
		}
		setFolderPath(path) {
			this.folderPath = path;
			this.onFolderPathChanged.forward(path);
		}
		tryGetLastStepIdFromFolderPath() {
			return this.folderPath.length > 0 ? this.folderPath[this.folderPath.length - 1] : null;
		}
		setDefinition(definition) {
			this.definition = definition;
			this.notifyDefinitionChanged(exports.DefinitionChangeType.rootReplaced, null);
		}
		notifyDefinitionChanged(changeType, stepId) {
			this.onDefinitionChanged.forward({ changeType, stepId });
		}
		setViewport(viewport) {
			this.viewport = viewport;
			this.onViewportChanged.forward(viewport);
		}
		setIsReadonly(isReadonly) {
			if (this.isReadonly !== isReadonly) {
				this.isReadonly = isReadonly;
				this.onIsReadonlyChanged.forward(isReadonly);
			}
		}
		setIsDragging(isDragging) {
			if (this.isDragging !== isDragging) {
				this.isDragging = isDragging;
				this.onIsDraggingChanged.forward(isDragging);
			}
		}
		setIsDragDisabled(isDragDisabled) {
			if (this.isDragDisabled !== isDragDisabled) {
				this.isDragDisabled = isDragDisabled;
				this.onIsDragDisabledChanged.forward(isDragDisabled);
			}
		}
		setIsToolboxCollapsed(isCollapsed) {
			if (this.isToolboxCollapsed !== isCollapsed) {
				this.isToolboxCollapsed = isCollapsed;
				this.onIsToolboxCollapsedChanged.forward(isCollapsed);
			}
		}
		setIsEditorCollapsed(isCollapsed) {
			if (this.isEditorCollapsed !== isCollapsed) {
				this.isEditorCollapsed = isCollapsed;
				this.onIsEditorCollapsedChanged.forward(isCollapsed);
			}
		}
	}

	class HistoryController {
		static create(initialStack, state, stateModifier, configuration) {
			if (!configuration.undoStackSize || configuration.undoStackSize < 1) {
				throw new Error('Invalid undo stack size');
			}
			const stack = initialStack || {
				index: 0,
				items: []
			};
			const controller = new HistoryController(stack, state, stateModifier, configuration.undoStackSize);
			if (!initialStack) {
				controller.rememberCurrent(exports.DefinitionChangeType.rootReplaced, null);
			}
			state.onDefinitionChanged.subscribe(event => {
				if (event.changeType !== exports.DefinitionChangeType.rootReplaced) {
					controller.rememberCurrent(event.changeType, event.stepId);
				}
			});
			return controller;
		}
		constructor(stack, state, stateModifier, stackSize) {
			this.stack = stack;
			this.state = state;
			this.stateModifier = stateModifier;
			this.stackSize = stackSize;
		}
		canUndo() {
			return this.stack.index > 1;
		}
		undo() {
			this.stack.index--;
			this.commit();
		}
		canRedo() {
			return this.stack.index < this.stack.items.length;
		}
		redo() {
			this.stack.index++;
			this.commit();
		}
		dump() {
			return Object.assign({}, this.stack);
		}
		replaceDefinition(definition) {
			if (definition == this.state.definition) {
				throw new Error('Cannot use the same instance of definition');
			}
			this.remember(definition, exports.DefinitionChangeType.rootReplaced, null);
			this.commit();
		}
		rememberCurrent(changeType, stepId) {
			this.remember(this.state.definition, changeType, stepId);
		}
		remember(sourceDefinition, changeType, stepId) {
			const definition = ObjectCloner.deepClone(sourceDefinition);
			if (this.stack.items.length > 0 && this.stack.index === this.stack.items.length) {
				const lastItem = this.stack.items[this.stack.items.length - 1];
				if (areItemsEqual(lastItem, changeType, stepId)) {
					lastItem.definition = definition;
					return;
				}
			}
			this.stack.items.splice(this.stack.index);
			this.stack.items.push({
				definition,
				changeType,
				stepId
			});
			if (this.stack.items.length > this.stackSize) {
				this.stack.items.splice(0, this.stack.items.length - this.stackSize - 1);
			}
			this.stack.index = this.stack.items.length;
		}
		commit() {
			const definition = ObjectCloner.deepClone(this.stack.items[this.stack.index - 1].definition);
			this.stateModifier.replaceDefinition(definition);
		}
	}
	function areItemsEqual(item, changeType, stepId) {
		return changeType !== exports.DefinitionChangeType.rootReplaced && item.changeType === changeType && item.stepId === stepId;
	}

	class LayoutController {
		constructor(placeholder) {
			this.placeholder = placeholder;
		}
		isMobile() {
			return this.placeholder.clientWidth < 400; // TODO
		}
	}

	class WorkspaceControllerWrapper {
		set(controller) {
			if (this.controller) {
				throw new Error('Controller is already set');
			}
			this.controller = controller;
		}
		get() {
			if (!this.controller) {
				throw new Error('Controller is not set');
			}
			return this.controller;
		}
		resolvePlaceholders(skipComponent) {
			return this.get().resolvePlaceholders(skipComponent);
		}
		getComponentByStepId(stepId) {
			return this.get().getComponentByStepId(stepId);
		}
		getCanvasPosition() {
			return this.get().getCanvasPosition();
		}
		getCanvasSize() {
			return this.get().getCanvasSize();
		}
		getRootComponentSize() {
			return this.get().getRootComponentSize();
		}
		updateBadges() {
			this.get().updateBadges();
		}
		updateRootComponent() {
			this.get().updateRootComponent();
		}
		updateCanvasSize() {
			this.get().updateCanvasSize();
		}
	}

	class MemoryPreferenceStorage {
		constructor() {
			this.map = {};
		}
		setItem(key, value) {
			this.map[key] = value;
		}
		getItem(key) {
			var _a;
			return (_a = this.map[key]) !== null && _a !== void 0 ? _a : null;
		}
	}

	class DesignerContext {
		static create(placeholder, startDefinition, configuration, services) {
			var _a, _b, _c, _d, _e, _f;
			const definition = ObjectCloner.deepClone(startDefinition);
			const layoutController = new LayoutController(placeholder);
			const isReadonly = Boolean(configuration.isReadonly);
			const isToolboxCollapsed = configuration.toolbox ? (_a = configuration.toolbox.isCollapsed) !== null && _a !== void 0 ? _a : layoutController.isMobile() : false;
			const isEditorCollapsed = configuration.editors ? (_b = configuration.editors.isCollapsed) !== null && _b !== void 0 ? _b : layoutController.isMobile() : false;
			const theme = configuration.theme || 'light';
			const state = new DesignerState(definition, isReadonly, isToolboxCollapsed, isEditorCollapsed);
			const workspaceController = new WorkspaceControllerWrapper();
			const behaviorController = BehaviorController.create(configuration.shadowRoot);
			const stepExtensionResolver = StepExtensionResolver.create(services);
			const placeholderController = PlaceholderController.create(configuration.placeholder);
			const definitionWalker = (_c = configuration.definitionWalker) !== null && _c !== void 0 ? _c : new DefinitionWalker();
			const i18n = (_d = configuration.i18n) !== null && _d !== void 0 ? _d : ((_, defaultValue) => defaultValue);
			const uidGenerator = (_e = configuration.uidGenerator) !== null && _e !== void 0 ? _e : Uid.next;
			const stateModifier = StateModifier.create(definitionWalker, uidGenerator, state, configuration.steps);
			const customActionController = new CustomActionController(configuration, state, stateModifier);
			let historyController = undefined;
			if (configuration.undoStackSize) {
				historyController = HistoryController.create(configuration.undoStack, state, stateModifier, configuration);
			}
			const preferenceStorage = (_f = configuration.preferenceStorage) !== null && _f !== void 0 ? _f : new MemoryPreferenceStorage();
			const componentContext = ComponentContext.create(configuration, state, stepExtensionResolver, placeholderController, definitionWalker, preferenceStorage, i18n, services);
			return new DesignerContext(theme, state, configuration, services, componentContext, definitionWalker, i18n, uidGenerator, stateModifier, layoutController, workspaceController, placeholderController, behaviorController, customActionController, historyController);
		}
		constructor(theme, state, configuration, services, componentContext, definitionWalker, i18n, uidGenerator, stateModifier, layoutController, workspaceController, placeholderController, behaviorController, customActionController, historyController) {
			this.theme = theme;
			this.state = state;
			this.configuration = configuration;
			this.services = services;
			this.componentContext = componentContext;
			this.definitionWalker = definitionWalker;
			this.i18n = i18n;
			this.uidGenerator = uidGenerator;
			this.stateModifier = stateModifier;
			this.layoutController = layoutController;
			this.workspaceController = workspaceController;
			this.placeholderController = placeholderController;
			this.behaviorController = behaviorController;
			this.customActionController = customActionController;
			this.historyController = historyController;
		}
		setWorkspaceController(controller) {
			this.workspaceController.set(controller);
		}
	}

	/******************************************************************************
	Copyright (c) Microsoft Corporation.

	Permission to use, copy, modify, and/or distribute this software for any
	purpose with or without fee is hereby granted.

	THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH
	REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY
	AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT,
	INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM
	LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR
	OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR
	PERFORMANCE OF THIS SOFTWARE.
	***************************************************************************** */
	/* global Reflect, Promise, SuppressedError, Symbol, Iterator */


	function __awaiter(thisArg, _arguments, P, generator) {
		function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
		return new (P || (P = Promise))(function (resolve, reject) {
			function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
			function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
			function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
			step((generator = generator.apply(thisArg, _arguments || [])).next());
		});
	}

	typeof SuppressedError === "function" ? SuppressedError : function (error, suppressed, message) {
		var e = new Error(message);
		return e.name = "SuppressedError", e.error = error, e.suppressed = suppressed, e;
	};

	function isElementAttached(dom, element) {
		return !(dom.compareDocumentPosition(element) & Node.DOCUMENT_POSITION_DISCONNECTED);
	}

	let lastGridPatternId = 0;
	const listenerOptions$1 = {
		passive: false
	};
	class WorkspaceView {
		static create(parent, componentContext) {
			const patternId = 'sqd-grid-pattern-' + lastGridPatternId++;
			const pattern = Dom.svg('pattern', {
				id: patternId,
				patternUnits: 'userSpaceOnUse'
			});
			const gridPattern = componentContext.services.grid.create();
			const defs = Dom.svg('defs');
			pattern.appendChild(gridPattern.element);
			defs.appendChild(pattern);
			const foreground = Dom.svg('g');
			const workspace = Dom.element('div', {
				class: 'sqd-workspace'
			});
			const canvas = Dom.svg('svg', {
				class: 'sqd-workspace-canvas'
			});
			canvas.appendChild(defs);
			canvas.appendChild(Dom.svg('rect', {
				width: '100%',
				height: '100%',
				fill: `url(#${patternId})`
			}));
			canvas.appendChild(foreground);
			workspace.appendChild(canvas);
			parent.appendChild(workspace);
			const view = new WorkspaceView(componentContext.shadowRoot, workspace, canvas, pattern, gridPattern, foreground, componentContext);
			window.addEventListener('resize', view.onResize, false);
			return view;
		}
		constructor(shadowRoot, workspace, canvas, pattern, gridPattern, foreground, context) {
			this.shadowRoot = shadowRoot;
			this.workspace = workspace;
			this.canvas = canvas;
			this.pattern = pattern;
			this.gridPattern = gridPattern;
			this.foreground = foreground;
			this.context = context;
			this.onResize = () => {
				this.refreshSize();
			};
		}
		render(sequence, parentPlaceIndicator) {
			if (this.rootComponent) {
				this.foreground.removeChild(this.rootComponent.view.g);
			}
			this.rootComponent = this.context.services.rootComponent.create(this.foreground, sequence, parentPlaceIndicator, this.context);
			this.refreshSize();
		}
		setPositionAndScale(position, scale) {
			const scaledSize = this.gridPattern.size.multiplyByScalar(scale);
			Dom.attrs(this.pattern, {
				x: position.x,
				y: position.y,
				width: scaledSize.x,
				height: scaledSize.y
			});
			this.gridPattern.setScale(scale, scaledSize);
			Dom.attrs(this.foreground, {
				transform: `translate(${position.x}, ${position.y}) scale(${scale})`
			});
		}
		getCanvasPosition() {
			return getAbsolutePosition(this.canvas);
		}
		getCanvasSize() {
			return new Vector(this.canvas.clientWidth, this.canvas.clientHeight);
		}
		bindMouseDown(handler) {
			this.canvas.addEventListener('mousedown', e => {
				e.preventDefault();
				handler(readMousePosition(e), e.target, e.button, e.altKey);
			}, false);
		}
		bindTouchStart(clickHandler, pinchToZoomHandler) {
			this.canvas.addEventListener('touchstart', e => {
				var _a;
				e.preventDefault();
				if (e.touches.length === 2) {
					pinchToZoomHandler(calculateFingerDistance(e), readFingerCenterPoint(e));
					return;
				}
				const clientPosition = readTouchClientPosition(e);
				const dom = (_a = this.shadowRoot) !== null && _a !== void 0 ? _a : document;
				const element = dom.elementFromPoint(clientPosition.x, clientPosition.y);
				if (element) {
					const position = readTouchPosition(e);
					clickHandler(position, element, 0, false);
				}
			}, listenerOptions$1);
		}
		bindContextMenu(handler) {
			this.canvas.addEventListener('contextmenu', e => {
				e.preventDefault();
				handler(readMousePosition(e), e.target);
			}, false);
		}
		bindWheel(handler) {
			this.canvas.addEventListener('wheel', handler, listenerOptions$1);
		}
		destroy() {
			window.removeEventListener('resize', this.onResize, false);
		}
		refreshSize() {
			Dom.attrs(this.canvas, {
				width: this.workspace.offsetWidth,
				height: this.workspace.offsetHeight
			});
		}
	}

	class MoveViewportBehavior {
		static create(resetSelectedStep, context) {
			return new MoveViewportBehavior(context.state.viewport.position, resetSelectedStep, context.state, context.stateModifier);
		}
		constructor(startPosition, resetSelectedStep, state, stateModifier) {
			this.startPosition = startPosition;
			this.resetSelectedStep = resetSelectedStep;
			this.state = state;
			this.stateModifier = stateModifier;
		}
		onStart() {
			if (this.resetSelectedStep) {
				const stepIdOrNull = this.state.tryGetLastStepIdFromFolderPath();
				if (stepIdOrNull) {
					this.stateModifier.trySelectStepById(stepIdOrNull);
				}
				else {
					this.state.setSelectedStepId(null);
				}
			}
		}
		onMove(delta) {
			this.state.setViewport({
				position: this.startPosition.subtract(delta),
				scale: this.state.viewport.scale
			});
		}
		onEnd() {
			// Nothing to do.
		}
	}

	class SelectStepBehavior {
		static create(pressedStepComponent, forceMove, context) {
			const isDragDisabled = forceMove ||
				context.state.isDragDisabled ||
				!context.stateModifier.isDraggable(pressedStepComponent.step, pressedStepComponent.parentSequence);
			return new SelectStepBehavior(pressedStepComponent, isDragDisabled, context.state, context.stateModifier, context);
		}
		constructor(pressedStepComponent, isDragDisabled, state, stateModifier, context) {
			this.pressedStepComponent = pressedStepComponent;
			this.isDragDisabled = isDragDisabled;
			this.state = state;
			this.stateModifier = stateModifier;
			this.context = context;
		}
		onStart() {
			// Nothing to do.
		}
		onMove(delta) {
			if (delta.distance() > 2) {
				const canDrag = !this.state.isReadonly && !this.isDragDisabled;
				if (canDrag) {
					this.state.setSelectedStepId(null);
					return DragStepBehavior.create(this.context, this.pressedStepComponent.step, this.pressedStepComponent);
				}
				else {
					return MoveViewportBehavior.create(false, this.context);
				}
			}
		}
		onEnd(interrupt) {
			if (interrupt) {
				return;
			}
			if (!this.stateModifier.trySelectStep(this.pressedStepComponent.step, this.pressedStepComponent.parentSequence)) {
				// If we cannot select the step, we clear the selection.
				this.state.setSelectedStepId(null);
			}
			return new SelectStepBehaviorEndToken(this.pressedStepComponent.step.id, Date.now());
		}
	}

	class PressingBehavior {
		static create(clickedElement, handler) {
			return new PressingBehavior(clickedElement, handler);
		}
		constructor(clickedElement, handler) {
			this.clickedElement = clickedElement;
			this.handler = handler;
		}
		onStart() {
			// Nothing...
		}
		onMove() {
			// Nothing...
		}
		onEnd(interrupt, element) {
			if (!interrupt && element && this.clickedElement === element) {
				this.handler.handle();
			}
		}
	}

	class RerenderStepPressingBehaviorHandler {
		constructor(command, designerContext) {
			this.command = command;
			this.designerContext = designerContext;
		}
		handle() {
			if (this.command.beforeCallback) {
				this.command.beforeCallback();
			}
			this.designerContext.workspaceController.updateRootComponent();
		}
	}

	class OpenFolderPressingBehaviorHandler {
		constructor(command, designerContext) {
			this.command = command;
			this.designerContext = designerContext;
		}
		handle() {
			const stepId = this.command.step.id;
			this.designerContext.state.pushStepIdToFolderPath(stepId);
		}
	}

	class TriggerCustomActionPressingBehaviorHandler {
		constructor(command, customActionController) {
			this.command = command;
			this.customActionController = customActionController;
		}
		handle() {
			this.customActionController.trigger(this.command.action, this.command.step, this.command.sequence);
		}
	}

	class ClickBehaviorResolver {
		constructor(context) {
			this.context = context;
		}
		resolve(commandOrNull, element, forceMove) {
			if (!commandOrNull) {
				return MoveViewportBehavior.create(!forceMove, this.context);
			}
			switch (commandOrNull.type) {
				case exports.ClickCommandType.selectStep:
					return SelectStepBehavior.create(commandOrNull.component, forceMove, this.context);
				case exports.ClickCommandType.rerenderStep:
					return PressingBehavior.create(element, new RerenderStepPressingBehaviorHandler(commandOrNull, this.context));
				case exports.ClickCommandType.openFolder:
					return PressingBehavior.create(element, new OpenFolderPressingBehaviorHandler(commandOrNull, this.context));
				case exports.ClickCommandType.triggerCustomAction:
					return PressingBehavior.create(element, new TriggerCustomActionPressingBehaviorHandler(commandOrNull, this.context.customActionController));
				default:
					throw new Error('Not supported behavior type');
			}
		}
	}

	class BadgesResultFactory {
		static create(services) {
			return services.badges.map(ext => ext.createStartValue());
		}
	}

	function findValidationBadgeIndex(extensions) {
		return extensions.findIndex(ext => ext.id === 'validationError');
	}

	class ContextMenu {
		static create(shadowRoot, position, theme, items) {
			const menu = document.createElement('div');
			menu.style.left = `${position.x}px`;
			menu.style.top = `${position.y}px`;
			menu.className = `sqd-context-menu sqd-theme-${theme}`;
			const elements = [];
			for (let index = 0; index < items.length; index++) {
				const item = items[index];
				const element = document.createElement('div');
				if (item.callback) {
					element.className = 'sqd-context-menu-item';
					element.innerText = item.label;
				}
				else {
					element.className = 'sqd-context-menu-group';
					element.innerText = item.label;
				}
				elements.push(element);
				menu.appendChild(element);
			}
			const body = shadowRoot !== null && shadowRoot !== void 0 ? shadowRoot : document.body;
			const dom = shadowRoot !== null && shadowRoot !== void 0 ? shadowRoot : document;
			const instance = new ContextMenu(body, dom, menu, elements, items, Date.now());
			dom.addEventListener('mousedown', instance.mouseDown, false);
			dom.addEventListener('mouseup', instance.mouseUp, false);
			dom.addEventListener('touchstart', instance.mouseDown, false);
			dom.addEventListener('touchend', instance.mouseUp, false);
			body.appendChild(menu);
			return instance;
		}
		constructor(body, dom, menu, elements, items, startTime) {
			this.body = body;
			this.dom = dom;
			this.menu = menu;
			this.elements = elements;
			this.items = items;
			this.startTime = startTime;
			this.isAttached = true;
			this.mouseDown = (e) => {
				const index = this.findIndex(e.target);
				if (index === null) {
					this.tryDestroy();
				}
				else {
					e.preventDefault();
					e.stopPropagation();
				}
			};
			this.mouseUp = (e) => {
				const dt = Date.now() - this.startTime;
				if (dt < 300) {
					e.preventDefault();
					e.stopPropagation();
					return;
				}
				try {
					const index = this.findIndex(e.target);
					if (index !== null) {
						const item = this.items[index];
						if (item.callback) {
							item.callback();
						}
					}
				}
				finally {
					this.tryDestroy();
				}
			};
		}
		findIndex(element) {
			for (let index = 0; index < this.elements.length; index++) {
				if (this.elements[index].contains(element)) {
					return index;
				}
			}
			return null;
		}
		tryDestroy() {
			if (this.isAttached) {
				this.body.removeChild(this.menu);
				this.dom.removeEventListener('mousedown', this.mouseDown, false);
				this.dom.removeEventListener('mouseup', this.mouseUp, false);
				this.dom.removeEventListener('touchstart', this.mouseDown, false);
				this.dom.removeEventListener('touchend', this.mouseUp, false);
				this.isAttached = false;
			}
		}
	}

	class ContextMenuController {
		constructor(theme, configuration, itemsBuilder) {
			this.theme = theme;
			this.configuration = configuration;
			this.itemsBuilder = itemsBuilder;
		}
		tryOpen(position, commandOrNull) {
			if (this.configuration.contextMenu === false) {
				// Context menu is disabled.
				return;
			}
			if (this.current) {
				this.current.tryDestroy();
			}
			const items = this.itemsBuilder.build(commandOrNull);
			this.current = ContextMenu.create(this.configuration.shadowRoot, position, this.theme, items);
		}
		destroy() {
			if (this.current) {
				this.current.tryDestroy();
			}
		}
	}

	class ContextMenuItemsBuilder {
		constructor(viewportApi, workspaceApi, i18n, stateModifier, state, customMenuItemsProvider) {
			this.viewportApi = viewportApi;
			this.workspaceApi = workspaceApi;
			this.i18n = i18n;
			this.stateModifier = stateModifier;
			this.state = state;
			this.customMenuItemsProvider = customMenuItemsProvider;
		}
		build(commandOrNull) {
			const items = [];
			if (commandOrNull && commandOrNull.type === exports.ClickCommandType.selectStep) {
				const ssc = commandOrNull;
				const step = ssc.component.step;
				const parentSequence = ssc.component.parentSequence;
				const name = this.i18n(`step.${step.type}.name`, step.name);
				items.push({
					label: name,
					order: 0
				});
				this.tryAppendCustomItems(items, step, parentSequence);
				if (this.stateModifier.isSelectable(step, parentSequence)) {
					if (this.state.selectedStepId === step.id) {
						items.push({
							label: this.i18n('contextMenu.unselect', 'Unselect'),
							order: 10,
							callback: () => {
								this.state.setSelectedStepId(null);
							}
						});
					}
					else {
						items.push({
							label: this.i18n('contextMenu.select', 'Select'),
							order: 20,
							callback: () => {
								this.stateModifier.trySelectStepById(step.id);
							}
						});
					}
				}
				if (!this.state.isReadonly) {
					if (this.stateModifier.isDeletable(step.id)) {
						items.push({
							label: this.i18n('contextMenu.delete', 'Delete'),
							order: 30,
							callback: () => {
								this.stateModifier.tryDelete(step.id);
							}
						});
					}
					if (this.stateModifier.isDuplicable(step, parentSequence)) {
						items.push({
							label: this.i18n('contextMenu.duplicate', 'Duplicate'),
							order: 40,
							callback: () => {
								this.stateModifier.tryDuplicate(step, parentSequence);
							}
						});
					}
				}
			}
			else if (!commandOrNull) {
				const rootSequence = this.workspaceApi.getRootSequence();
				this.tryAppendCustomItems(items, null, rootSequence.sequence);
			}
			items.push({
				label: this.i18n('contextMenu.resetView', 'Reset view'),
				order: 50,
				callback: () => {
					this.viewportApi.resetViewport();
				}
			});
			items.sort((a, b) => a.order - b.order);
			return items;
		}
		tryAppendCustomItems(items, step, parentSequence) {
			if (this.customMenuItemsProvider) {
				const customItems = this.customMenuItemsProvider.getItems(step, parentSequence, this.state.definition);
				for (const customItem of customItems) {
					items.push(customItem);
				}
			}
		}
	}

	const nonPassiveOptions = {
		passive: false
	};
	const notInitializedError = 'State is not initialized';
	class PinchToZoomController {
		static create(workspaceApi, viewportApi, shadowRoot) {
			return new PinchToZoomController(workspaceApi, viewportApi, shadowRoot);
		}
		constructor(workspaceApi, viewportApi, shadowRoot) {
			this.workspaceApi = workspaceApi;
			this.viewportApi = viewportApi;
			this.shadowRoot = shadowRoot;
			this.state = null;
			this.onTouchMove = (e) => {
				e.preventDefault();
				if (!this.state) {
					throw new Error(notInitializedError);
				}
				const touchEvent = e;
				const distance = calculateFingerDistance(touchEvent);
				const centerPoint = readFingerCenterPoint(touchEvent);
				const deltaCenterPoint = centerPoint.subtract(this.state.lastCenterPoint);
				const scale = this.viewportApi.limitScale(this.state.startScale * (distance / this.state.startDistance));
				const zoomPoint = centerPoint.subtract(this.state.canvasPosition);
				const zoomRealPoint = zoomPoint
					.divideByScalar(this.state.lastViewport.scale)
					.subtract(this.state.lastViewport.position.divideByScalar(this.state.lastViewport.scale));
				const position = zoomRealPoint.multiplyByScalar(-scale).add(zoomPoint).add(deltaCenterPoint);
				const newViewport = {
					position,
					scale
				};
				this.workspaceApi.setViewport(newViewport);
				this.state.lastCenterPoint = centerPoint;
				this.state.lastViewport = newViewport;
			};
			this.onTouchEnd = (e) => {
				e.preventDefault();
				if (!this.state) {
					throw new Error(notInitializedError);
				}
				if (this.shadowRoot) {
					this.unbind(this.shadowRoot);
				}
				this.unbind(window);
				this.state = null;
			};
		}
		start(startDistance, centerPoint) {
			if (this.state) {
				throw new Error(`State is already initialized`);
			}
			if (this.shadowRoot) {
				this.bind(this.shadowRoot);
			}
			this.bind(window);
			const viewport = this.workspaceApi.getViewport();
			this.state = {
				canvasPosition: this.workspaceApi.getCanvasPosition(),
				startScale: viewport.scale,
				startDistance,
				lastViewport: viewport,
				lastCenterPoint: centerPoint
			};
		}
		bind(target) {
			target.addEventListener('touchmove', this.onTouchMove, nonPassiveOptions);
			target.addEventListener('touchend', this.onTouchEnd, nonPassiveOptions);
		}
		unbind(target) {
			target.removeEventListener('touchmove', this.onTouchMove, nonPassiveOptions);
			target.removeEventListener('touchend', this.onTouchEnd, nonPassiveOptions);
		}
	}

	class Workspace {
		static create(parent, designerContext, api) {
			var _a;
			const view = WorkspaceView.create(parent, designerContext.componentContext);
			const clickBehaviorResolver = new ClickBehaviorResolver(designerContext);
			const clickBehaviorWrapper = designerContext.services.clickBehaviorWrapperExtension.create(designerContext.customActionController);
			const wheelController = designerContext.services.wheelController.create(api.viewport, api.workspace);
			const pinchToZoomController = PinchToZoomController.create(api.workspace, api.viewport, api.shadowRoot);
			const contextMenuItemsBuilder = new ContextMenuItemsBuilder(api.viewport, api.workspace, api.i18n, designerContext.stateModifier, designerContext.state, ((_a = designerContext.services.contextMenu) === null || _a === void 0 ? void 0 : _a.createItemsProvider)
				? designerContext.services.contextMenu.createItemsProvider(designerContext.customActionController)
				: undefined);
			const contextMenuController = new ContextMenuController(designerContext.theme, designerContext.configuration, contextMenuItemsBuilder);
			const workspace = new Workspace(view, designerContext.state, designerContext.behaviorController, wheelController, pinchToZoomController, contextMenuController, clickBehaviorResolver, clickBehaviorWrapper, api.viewport, api.workspace, designerContext.services);
			designerContext.setWorkspaceController(workspace);
			designerContext.state.onViewportChanged.subscribe(workspace.onViewportChanged);
			race(0, designerContext.state.onDefinitionChanged, designerContext.state.onSelectedStepIdChanged, designerContext.state.onFolderPathChanged).subscribe(r => {
				workspace.onStateChanged(r[0], r[1], r[2]);
			});
			view.bindMouseDown(workspace.onClick);
			view.bindTouchStart(workspace.onClick, workspace.onPinchToZoom);
			view.bindWheel(workspace.onWheel);
			view.bindContextMenu(workspace.onContextMenu);
			workspace.scheduleInit();
			return workspace;
		}
		constructor(view, state, behaviorController, wheelController, pinchToZoomController, contextMenuController, clickBehaviorResolver, clickBehaviorWrapper, viewportApi, workspaceApi, services) {
			this.view = view;
			this.state = state;
			this.behaviorController = behaviorController;
			this.wheelController = wheelController;
			this.pinchToZoomController = pinchToZoomController;
			this.contextMenuController = contextMenuController;
			this.clickBehaviorResolver = clickBehaviorResolver;
			this.clickBehaviorWrapper = clickBehaviorWrapper;
			this.viewportApi = viewportApi;
			this.workspaceApi = workspaceApi;
			this.services = services;
			this.onRendered = new SimpleEvent();
			this.isValid = false;
			this.initTimeout = null;
			this.selectedStepComponent = null;
			this.validationErrorBadgeIndex = null;
			this.onClick = (position, target, buttonIndex, altKey) => {
				const isPrimaryButton = buttonIndex === 0;
				const isMiddleButton = buttonIndex === 1;
				if (isPrimaryButton || isMiddleButton) {
					const forceMove = isMiddleButton || altKey;
					const commandOrNull = this.resolveClick(target, position);
					const behavior = this.clickBehaviorResolver.resolve(commandOrNull, target, forceMove);
					const wrappedBehavior = this.clickBehaviorWrapper.wrap(behavior, commandOrNull);
					this.behaviorController.start(position, wrappedBehavior);
				}
			};
			this.onPinchToZoom = (distance, centerPoint) => {
				this.pinchToZoomController.start(distance, centerPoint);
			};
			this.onWheel = (e) => {
				e.preventDefault();
				e.stopPropagation();
				this.wheelController.onWheel(e);
			};
			this.onContextMenu = (position, target) => {
				const commandOrNull = this.resolveClick(target, position);
				this.contextMenuController.tryOpen(position, commandOrNull);
			};
			this.onViewportChanged = (viewport) => {
				this.view.setPositionAndScale(viewport.position, viewport.scale);
			};
		}
		scheduleInit() {
			this.initTimeout = setTimeout(() => {
				this.initTimeout = null;
				this.updateRootComponent();
				this.viewportApi.resetViewport();
			});
		}
		updateRootComponent() {
			this.selectedStepComponent = null;
			const rootSequence = this.workspaceApi.getRootSequence();
			const parentPlaceIndicator = rootSequence.parentStep
				? {
					sequence: rootSequence.parentStep.parentSequence,
					index: rootSequence.parentStep.index
				}
				: null;
			this.view.render(rootSequence.sequence, parentPlaceIndicator);
			this.trySelectStepComponent(this.state.selectedStepId);
			this.updateBadges();
			this.onRendered.forward();
		}
		updateBadges() {
			const result = BadgesResultFactory.create(this.services);
			this.getRootComponent().updateBadges(result);
			if (this.validationErrorBadgeIndex === null) {
				this.validationErrorBadgeIndex = findValidationBadgeIndex(this.services.badges);
			}
			this.isValid = Boolean(result[this.validationErrorBadgeIndex]);
		}
		resolvePlaceholders(skipComponent) {
			const result = {
				placeholders: [],
				components: []
			};
			this.getRootComponent().resolvePlaceholders(skipComponent, result);
			return result;
		}
		getComponentByStepId(stepId) {
			const component = this.getRootComponent().findById(stepId);
			if (!component) {
				throw new Error(`Cannot find component for step id: ${stepId}`);
			}
			return component;
		}
		getCanvasPosition() {
			return this.view.getCanvasPosition();
		}
		getCanvasSize() {
			return this.view.getCanvasSize();
		}
		getRootComponentSize() {
			const view = this.getRootComponent().view;
			return new Vector(view.width, view.height);
		}
		updateCanvasSize() {
			setTimeout(() => this.view.refreshSize());
		}
		destroy() {
			if (this.initTimeout) {
				clearTimeout(this.initTimeout);
				this.initTimeout = null;
			}
			this.contextMenuController.destroy();
			this.view.destroy();
		}
		onStateChanged(definitionChanged, selectedStepIdChanged, folderPathChanged) {
			if (folderPathChanged) {
				this.updateRootComponent();
				this.viewportApi.resetViewport();
			}
			else if (definitionChanged) {
				if (definitionChanged.changeType === exports.DefinitionChangeType.stepPropertyChanged) {
					this.updateBadges();
				}
				else {
					this.updateRootComponent();
				}
			}
			else if (selectedStepIdChanged !== undefined) {
				this.trySelectStepComponent(selectedStepIdChanged);
			}
		}
		trySelectStepComponent(stepId) {
			if (this.selectedStepComponent) {
				this.selectedStepComponent.setIsSelected(false);
				this.selectedStepComponent = null;
			}
			if (stepId) {
				this.selectedStepComponent = this.getRootComponent().findById(stepId);
				if (this.selectedStepComponent) {
					this.selectedStepComponent.setIsSelected(true);
				}
			}
		}
		resolveClick(element, position) {
			const click = {
				element,
				position,
				scale: this.state.viewport.scale
			};
			return this.getRootComponent().resolveClick(click);
		}
		getRootComponent() {
			if (this.view.rootComponent) {
				return this.view.rootComponent;
			}
			throw new Error('Root component not found');
		}
	}

	class DesignerView {
		static create(parent, designerContext, api) {
			const root = Dom.element('div', {
				class: `sqd-designer sqd-theme-${designerContext.theme}`
			});
			parent.appendChild(root);
			const workspace = Workspace.create(root, designerContext, api);
			const uiComponents = designerContext.services.uiComponents.map(factory => factory.create(root, api));
			const daemons = designerContext.services.daemons.map(factory => factory.create(api));
			const view = new DesignerView(root, designerContext.layoutController, workspace, uiComponents, daemons);
			view.applyLayout();
			window.addEventListener('resize', view.onResize, false);
			return view;
		}
		constructor(root, layoutController, workspace, uiComponents, daemons) {
			this.root = root;
			this.layoutController = layoutController;
			this.workspace = workspace;
			this.uiComponents = uiComponents;
			this.daemons = daemons;
			this.onResize = () => {
				this.updateLayout();
			};
		}
		updateLayout() {
			this.applyLayout();
			for (const component of this.uiComponents) {
				component.updateLayout();
			}
		}
		destroy() {
			var _a;
			window.removeEventListener('resize', this.onResize, false);
			this.workspace.destroy();
			this.uiComponents.forEach(component => component.destroy());
			this.daemons.forEach(daemon => daemon.destroy());
			(_a = this.root.parentElement) === null || _a === void 0 ? void 0 : _a.removeChild(this.root);
		}
		applyLayout() {
			const isMobile = this.layoutController.isMobile();
			Dom.toggleClass(this.root, !isMobile, 'sqd-layout-desktop');
			Dom.toggleClass(this.root, isMobile, 'sqd-layout-mobile');
		}
	}

	const SAFE_OFFSET = 10;
	class DefaultDraggedComponent {
		static create(parent, step, componentContext) {
			const canvas = Dom.svg('svg');
			canvas.style.marginLeft = -10 + 'px';
			canvas.style.marginTop = -10 + 'px';
			parent.appendChild(canvas);
			const previewStepContext = {
				parentSequence: [],
				step,
				depth: 0,
				position: 0,
				isInputConnected: true,
				isOutputConnected: true,
				isPreview: true
			};
			const stepComponent = componentContext.stepComponentFactory.create(canvas, previewStepContext, componentContext);
			Dom.attrs(canvas, {
				width: stepComponent.view.width + SAFE_OFFSET * 2,
				height: stepComponent.view.height + SAFE_OFFSET * 2
			});
			Dom.translate(stepComponent.view.g, SAFE_OFFSET, SAFE_OFFSET);
			return new DefaultDraggedComponent(stepComponent.view.width, stepComponent.view.height);
		}
		constructor(width, height) {
			this.width = width;
			this.height = height;
		}
		destroy() {
			// Nothing to destroy...
		}
	}

	class DefaultDraggedComponentExtension {
		constructor() {
			this.create = DefaultDraggedComponent.create;
		}
	}

	class ControlBarView {
		static create(parent, isUndoRedoSupported, i18n) {
			const root = Dom.element('div', {
				class: 'sqd-control-bar'
			});
			const resetButton = createButton(Icons.center, i18n('controlBar.resetView', 'Reset view'));
			root.appendChild(resetButton);
			const zoomInButton = createButton(Icons.zoomIn, i18n('controlBar.zoomIn', 'Zoom in'));
			root.appendChild(zoomInButton);
			const zoomOutButton = createButton(Icons.zoomOut, i18n('controlBar.zoomOut', 'Zoom out'));
			root.appendChild(zoomOutButton);
			let undoButton = null;
			let redoButton = null;
			if (isUndoRedoSupported) {
				undoButton = createButton(Icons.undo, i18n('controlBar.undo', 'Undo'));
				root.appendChild(undoButton);
				redoButton = createButton(Icons.redo, i18n('controlBar.redo', 'Redo'));
				root.appendChild(redoButton);
			}
			const disableDragButton = createButton(Icons.move, i18n('controlBar.turnOnOffDragAndDrop', 'Turn on/off drag and drop'));
			disableDragButton.classList.add('sqd-disabled');
			root.appendChild(disableDragButton);
			const deleteButton = createButton(Icons.delete, i18n('controlBar.deleteSelectedStep', 'Delete selected step'));
			deleteButton.classList.add('sqd-delete');
			deleteButton.classList.add('sqd-hidden');
			root.appendChild(deleteButton);
			parent.appendChild(root);
			return new ControlBarView(resetButton, zoomInButton, zoomOutButton, undoButton, redoButton, disableDragButton, deleteButton);
		}
		constructor(resetButton, zoomInButton, zoomOutButton, undoButton, redoButton, disableDragButton, deleteButton) {
			this.resetButton = resetButton;
			this.zoomInButton = zoomInButton;
			this.zoomOutButton = zoomOutButton;
			this.undoButton = undoButton;
			this.redoButton = redoButton;
			this.disableDragButton = disableDragButton;
			this.deleteButton = deleteButton;
		}
		bindResetButtonClick(handler) {
			bindClick(this.resetButton, handler);
		}
		bindZoomInButtonClick(handler) {
			bindClick(this.zoomInButton, handler);
		}
		bindZoomOutButtonClick(handler) {
			bindClick(this.zoomOutButton, handler);
		}
		bindUndoButtonClick(handler) {
			if (!this.undoButton) {
				throw new Error('Undo button is disabled');
			}
			bindClick(this.undoButton, handler);
		}
		bindRedoButtonClick(handler) {
			if (!this.redoButton) {
				throw new Error('Redo button is disabled');
			}
			bindClick(this.redoButton, handler);
		}
		bindDisableDragButtonClick(handler) {
			bindClick(this.disableDragButton, handler);
		}
		bindDeleteButtonClick(handler) {
			bindClick(this.deleteButton, handler);
		}
		setIsDeleteButtonHidden(isHidden) {
			Dom.toggleClass(this.deleteButton, isHidden, 'sqd-hidden');
		}
		setDisableDragButtonDisabled(isDisabled) {
			Dom.toggleClass(this.disableDragButton, isDisabled, 'sqd-disabled');
		}
		setUndoButtonDisabled(isDisabled) {
			if (!this.undoButton) {
				throw new Error('Undo button is disabled');
			}
			Dom.toggleClass(this.undoButton, isDisabled, 'sqd-disabled');
		}
		setRedoButtonDisabled(isDisabled) {
			if (!this.redoButton) {
				throw new Error('Redo button is disabled');
			}
			Dom.toggleClass(this.redoButton, isDisabled, 'sqd-disabled');
		}
	}
	function bindClick(element, handler) {
		element.addEventListener('click', e => {
			e.preventDefault();
			handler();
		}, false);
	}
	function createButton(d, title) {
		const button = Dom.element('div', {
			class: 'sqd-control-bar-button',
			title
		});
		const icon = Icons.createSvg('sqd-control-bar-button-icon', d);
		button.appendChild(icon);
		return button;
	}

	class ControlBar {
		static create(parent, api) {
			const isUndoRedoSupported = api.controlBar.isUndoRedoSupported();
			const view = ControlBarView.create(parent, isUndoRedoSupported, api.i18n);
			const bar = new ControlBar(view, api.controlBar, api.viewport, isUndoRedoSupported);
			view.bindResetButtonClick(() => bar.onResetButtonClicked());
			view.bindZoomInButtonClick(() => bar.onZoomInButtonClicked());
			view.bindZoomOutButtonClick(() => bar.onZoomOutButtonClicked());
			view.bindDisableDragButtonClick(() => bar.onMoveButtonClicked());
			view.bindDeleteButtonClick(() => bar.onDeleteButtonClicked());
			api.controlBar.onStateChanged.subscribe(() => bar.refreshButtons());
			if (isUndoRedoSupported) {
				view.bindUndoButtonClick(() => bar.onUndoButtonClicked());
				view.bindRedoButtonClick(() => bar.onRedoButtonClicked());
			}
			bar.refreshButtons();
			return bar;
		}
		constructor(view, controlBarApi, viewportApi, isUndoRedoSupported) {
			this.view = view;
			this.controlBarApi = controlBarApi;
			this.viewportApi = viewportApi;
			this.isUndoRedoSupported = isUndoRedoSupported;
		}
		updateLayout() {
			//
		}
		destroy() {
			//
		}
		onResetButtonClicked() {
			this.viewportApi.resetViewport();
		}
		onZoomInButtonClicked() {
			this.viewportApi.zoom(true);
		}
		onZoomOutButtonClicked() {
			this.viewportApi.zoom(false);
		}
		onMoveButtonClicked() {
			this.controlBarApi.toggleIsDragDisabled();
		}
		onUndoButtonClicked() {
			this.controlBarApi.tryUndo();
		}
		onRedoButtonClicked() {
			this.controlBarApi.tryRedo();
		}
		onDeleteButtonClicked() {
			this.controlBarApi.tryDelete();
		}
		refreshButtons() {
			this.refreshDeleteButtonVisibility();
			this.refreshIsDragDisabled();
			if (this.isUndoRedoSupported) {
				this.refreshUndoRedoAvailability();
			}
		}
		//
		refreshIsDragDisabled() {
			const isDragDisabled = this.controlBarApi.isDragDisabled();
			this.view.setDisableDragButtonDisabled(!isDragDisabled);
		}
		refreshUndoRedoAvailability() {
			const canUndo = this.controlBarApi.canUndo();
			const canRedo = this.controlBarApi.canRedo();
			this.view.setUndoButtonDisabled(!canUndo);
			this.view.setRedoButtonDisabled(!canRedo);
		}
		refreshDeleteButtonVisibility() {
			const canDelete = this.controlBarApi.canDelete();
			this.view.setIsDeleteButtonHidden(!canDelete);
		}
	}

	class ControlBarExtension {
		constructor() {
			this.create = ControlBar.create;
		}
	}

	const ignoreTagNames = ['INPUT', 'TEXTAREA', 'SELECT'];
	class KeyboardDaemon {
		static create(api, configuration) {
			const dom = api.shadowRoot || document;
			const controller = new KeyboardDaemon(dom, api.controlBar, configuration);
			dom.addEventListener('keyup', controller.onKeyUp, false);
			return controller;
		}
		constructor(dom, controlBarApi, configuration) {
			this.dom = dom;
			this.controlBarApi = controlBarApi;
			this.configuration = configuration;
			this.onKeyUp = (e) => {
				const ke = e;
				const action = detectAction(ke);
				if (!action) {
					return;
				}
				if (document.activeElement && ignoreTagNames.includes(document.activeElement.tagName)) {
					return;
				}
				if (this.configuration.canHandleKey && !this.configuration.canHandleKey(action, ke)) {
					return;
				}
				const isDeletable = this.controlBarApi.canDelete();
				if (isDeletable) {
					e.preventDefault();
					e.stopPropagation();
					this.controlBarApi.tryDelete();
				}
			};
		}
		destroy() {
			this.dom.removeEventListener('keyup', this.onKeyUp, false);
		}
	}
	function detectAction(e) {
		if (e.key === 'Backspace' || e.key === 'Delete') {
			return exports.KeyboardAction.delete;
		}
		return null;
	}

	class KeyboardDaemonExtension {
		static create(configuration) {
			if (configuration === undefined || configuration === true) {
				configuration = {};
			}
			return new KeyboardDaemonExtension(configuration);
		}
		constructor(configuration) {
			this.configuration = configuration;
		}
		create(api) {
			return KeyboardDaemon.create(api, this.configuration);
		}
	}

	class SmartEditorView {
		static create(parent, api, i18n, configuration) {
			const root = Dom.element('div', {
				class: 'sqd-smart-editor'
			});
			const toggle = Dom.element('div', {
				class: 'sqd-smart-editor-toggle',
				title: i18n('smartEditor.toggle', 'Toggle editor')
			});
			parent.appendChild(toggle);
			parent.appendChild(root);
			const editor = Editor.create(root, api, 'sqd-editor sqd-step-editor', configuration.stepEditorProvider, 'sqd-editor sqd-root-editor', configuration.rootEditorProvider, null);
			return new SmartEditorView(root, toggle, editor);
		}
		constructor(root, toggle, editor) {
			this.root = root;
			this.toggle = toggle;
			this.editor = editor;
		}
		bindToggleClick(handler) {
			this.toggle.addEventListener('click', e => {
				e.preventDefault();
				handler();
			}, false);
		}
		setIsCollapsed(isCollapsed) {
			Dom.toggleClass(this.root, isCollapsed, 'sqd-hidden');
			Dom.toggleClass(this.toggle, isCollapsed, 'sqd-collapsed');
			if (this.toggleIcon) {
				this.toggle.removeChild(this.toggleIcon);
			}
			this.toggleIcon = Icons.createSvg('sqd-smart-editor-toggle-icon', isCollapsed ? Icons.options : Icons.close);
			this.toggle.appendChild(this.toggleIcon);
		}
		destroy() {
			this.editor.destroy();
		}
	}

	class SmartEditor {
		static create(parent, api, configuration) {
			const view = SmartEditorView.create(parent, api.editor, api.i18n, configuration);
			const editor = new SmartEditor(view, api.editor, api.workspace);
			editor.updateVisibility();
			view.bindToggleClick(() => editor.onToggleClicked());
			api.editor.subscribeIsCollapsed(() => editor.onIsCollapsedChanged());
			return editor;
		}
		constructor(view, editorApi, workspaceApi) {
			this.view = view;
			this.editorApi = editorApi;
			this.workspaceApi = workspaceApi;
		}
		onToggleClicked() {
			this.editorApi.toggleIsCollapsed();
		}
		setIsCollapsed(isCollapsed) {
			this.view.setIsCollapsed(isCollapsed);
		}
		onIsCollapsedChanged() {
			this.updateVisibility();
			this.workspaceApi.updateCanvasSize();
		}
		updateVisibility() {
			this.setIsCollapsed(this.editorApi.isCollapsed());
		}
		updateLayout() {
			//
		}
		destroy() {
			this.view.destroy();
		}
	}

	class SmartEditorExtension {
		constructor(configuration) {
			this.configuration = configuration;
		}
		create(root, api) {
			return SmartEditor.create(root, api, this.configuration);
		}
	}

	const MAX_DELTA_Y = 25;
	const listenerOptions = {
		passive: false
	};
	class ScrollBoxView {
		static create(parent, viewport) {
			const root = Dom.element('div', {
				class: 'sqd-scrollbox'
			});
			parent.appendChild(root);
			const view = new ScrollBoxView(root, viewport);
			root.addEventListener('wheel', e => view.onWheel(e), listenerOptions);
			root.addEventListener('touchstart', e => view.onTouchStart(e), listenerOptions);
			root.addEventListener('mousedown', e => view.onMouseDown(e), false);
			return view;
		}
		constructor(root, viewport) {
			this.root = root;
			this.viewport = viewport;
			this.onTouchStart = (e) => {
				e.preventDefault();
				this.startScroll(readTouchPosition(e));
			};
			this.onMouseDown = (e) => {
				e.preventDefault();
				this.startScroll(readMousePosition(e));
			};
			this.onTouchMove = (e) => {
				e.preventDefault();
				this.moveScroll(readTouchPosition(e));
			};
			this.onMouseMove = (e) => {
				e.preventDefault();
				this.moveScroll(readMousePosition(e));
			};
			this.onTouchEnd = (e) => {
				e.preventDefault();
				this.stopScroll();
			};
			this.onMouseUp = (e) => {
				e.preventDefault();
				this.stopScroll();
			};
		}
		setContent(element) {
			if (this.content) {
				this.root.removeChild(this.content.element);
			}
			element.classList.add('sqd-scrollbox-body');
			this.root.appendChild(element);
			this.reload(element);
		}
		updateLayout() {
			if (this.content) {
				this.reload(this.content.element);
			}
		}
		destroy() {
			//
		}
		reload(element) {
			const maxHeightPercent = 0.7;
			const minDistance = 206;
			let height = Math.min(this.viewport.clientHeight * maxHeightPercent, element.clientHeight);
			height = Math.min(height, this.viewport.clientHeight - minDistance);
			this.root.style.height = height + 'px';
			element.style.top = '0px';
			this.content = {
				element,
				height
			};
		}
		onWheel(e) {
			e.preventDefault();
			e.stopPropagation();
			if (this.content) {
				const delta = Math.sign(e.deltaY) * Math.min(Math.abs(e.deltaY), MAX_DELTA_Y);
				const scrollTop = this.getScrollTop();
				this.setScrollTop(scrollTop - delta);
			}
		}
		startScroll(startPosition) {
			if (!this.scroll) {
				window.addEventListener('touchmove', this.onTouchMove, listenerOptions);
				window.addEventListener('mousemove', this.onMouseMove, false);
				window.addEventListener('touchend', this.onTouchEnd, listenerOptions);
				window.addEventListener('mouseup', this.onMouseUp, false);
			}
			this.scroll = {
				startPositionY: startPosition.y,
				startScrollTop: this.getScrollTop()
			};
		}
		moveScroll(position) {
			if (this.scroll) {
				const delta = position.y - this.scroll.startPositionY;
				this.setScrollTop(this.scroll.startScrollTop + delta);
			}
		}
		stopScroll() {
			if (this.scroll) {
				window.removeEventListener('touchmove', this.onTouchMove, listenerOptions);
				window.removeEventListener('mousemove', this.onMouseMove, false);
				window.removeEventListener('touchend', this.onTouchEnd, listenerOptions);
				window.removeEventListener('mouseup', this.onMouseUp, false);
				this.scroll = undefined;
			}
		}
		getScrollTop() {
			if (this.content && this.content.element.style.top) {
				return parseInt(this.content.element.style.top);
			}
			return 0;
		}
		setScrollTop(scrollTop) {
			if (this.content) {
				const max = this.content.element.clientHeight - this.content.height;
				const limited = Math.max(Math.min(scrollTop, 0), -max);
				this.content.element.style.top = limited + 'px';
			}
		}
	}

	class ToolboxItemView {
		static create(parent, data) {
			const root = Dom.element('div', {
				class: `sqd-toolbox-item sqd-type-${data.step.type}`,
				title: data.description
			});
			const icon = Dom.element('div', {
				class: 'sqd-toolbox-item-icon'
			});
			if (data.iconUrl) {
				const iconImage = Dom.element('img', {
					class: 'sqd-toolbox-item-icon-image',
					src: data.iconUrl,
					draggable: 'false'
				});
				icon.appendChild(iconImage);
			}
			else {
				icon.classList.add('sqd-no-icon');
			}
			const text = Dom.element('div', {
				class: 'sqd-toolbox-item-text'
			});
			text.textContent = data.label;
			root.appendChild(icon);
			root.appendChild(text);
			parent.appendChild(root);
			return new ToolboxItemView(root);
		}
		constructor(root) {
			this.root = root;
		}
		bindMousedown(handler) {
			this.root.addEventListener('mousedown', handler, false);
		}
		bindTouchstart(handler) {
			this.root.addEventListener('touchstart', handler, false);
		}
		bindContextMenu(handler) {
			this.root.addEventListener('contextmenu', handler, false);
		}
	}

	class ToolboxItem {
		static create(parent, data, api) {
			const view = ToolboxItemView.create(parent, data);
			const item = new ToolboxItem(data.step, api);
			view.bindMousedown(e => item.onMousedown(e));
			view.bindTouchstart(e => item.onTouchstart(e));
			view.bindContextMenu(e => item.onContextMenu(e));
			return item;
		}
		constructor(step, api) {
			this.step = step;
			this.api = api;
		}
		onTouchstart(e) {
			e.preventDefault();
			if (e.touches.length === 1) {
				// We stop propagation only if it was a single touch event (we can start dragging).
				// Otherwise we want to bubble up the event to the scrollbox.
				e.stopPropagation();
				this.tryDrag(readTouchPosition(e));
			}
		}
		onMousedown(e) {
			e.preventDefault();
			e.stopPropagation();
			const isPrimaryButton = e.button === 0;
			if (isPrimaryButton) {
				this.tryDrag(readMousePosition(e));
			}
		}
		onContextMenu(e) {
			e.preventDefault();
		}
		tryDrag(position) {
			this.api.tryDrag(position, this.step);
		}
	}

	class ToolboxView {
		static create(parent, api, i18n) {
			const root = Dom.element('div', {
				class: 'sqd-toolbox'
			});
			const header = Dom.element('div', {
				class: 'sqd-toolbox-header'
			});
			const headerTitle = Dom.element('div', {
				class: 'sqd-toolbox-header-title'
			});
			headerTitle.innerText = i18n('toolbox.title', 'Toolbox');
			const body = Dom.element('div', {
				class: 'sqd-toolbox-body'
			});
			const filter = Dom.element('div', {
				class: 'sqd-toolbox-filter'
			});
			const filterInput = Dom.element('input', {
				class: 'sqd-toolbox-filter-input',
				type: 'text',
				placeholder: i18n('toolbox.search', 'Search...')
			});
			root.appendChild(header);
			root.appendChild(body);
			header.appendChild(headerTitle);
			filter.appendChild(filterInput);
			body.appendChild(filter);
			parent.appendChild(root);
			const scrollBoxView = ScrollBoxView.create(body, parent);
			return new ToolboxView(header, body, filterInput, scrollBoxView, api);
		}
		constructor(header, body, filterInput, scrollBoxView, api) {
			this.header = header;
			this.body = body;
			this.filterInput = filterInput;
			this.scrollBoxView = scrollBoxView;
			this.api = api;
		}
		updateLayout() {
			this.scrollBoxView.updateLayout();
		}
		bindToggleClick(handler) {
			function forward(e) {
				e.preventDefault();
				handler();
			}
			this.header.addEventListener('click', forward, false);
		}
		bindFilterInputChange(handler) {
			function forward(e) {
				handler(e.target.value);
			}
			this.filterInput.addEventListener('keyup', forward, false);
			this.filterInput.addEventListener('blur', forward, false);
		}
		setIsCollapsed(isCollapsed) {
			Dom.toggleClass(this.body, isCollapsed, 'sqd-hidden');
			if (this.headerToggleIcon) {
				this.header.removeChild(this.headerToggleIcon);
			}
			this.headerToggleIcon = Icons.createSvg('sqd-toolbox-toggle-icon', isCollapsed ? Icons.expand : Icons.close);
			this.header.appendChild(this.headerToggleIcon);
			if (!isCollapsed) {
				this.updateLayout();
			}
		}
		setGroups(groups) {
			const list = Dom.element('div');
			groups.forEach(group => {
				const groupTitle = Dom.element('div', {
					class: 'sqd-toolbox-group-title'
				});
				groupTitle.innerText = group.name;
				list.appendChild(groupTitle);
				group.items.forEach(item => ToolboxItem.create(list, item, this.api));
			});
			this.scrollBoxView.setContent(list);
		}
		destroy() {
			this.scrollBoxView.destroy();
		}
	}

	class Toolbox {
		static create(root, api, i18n) {
			const allGroups = api.getAllGroups();
			const view = ToolboxView.create(root, api, i18n);
			const toolbox = new Toolbox(view, api, allGroups);
			toolbox.render();
			toolbox.onIsCollapsedChanged();
			view.bindToggleClick(() => toolbox.onToggleClicked());
			view.bindFilterInputChange(v => toolbox.onFilterInputChanged(v));
			api.subscribeIsCollapsed(() => toolbox.onIsCollapsedChanged());
			return toolbox;
		}
		constructor(view, api, allGroups) {
			this.view = view;
			this.api = api;
			this.allGroups = allGroups;
		}
		updateLayout() {
			this.view.updateLayout();
		}
		destroy() {
			this.view.destroy();
		}
		render() {
			const groups = this.api.applyFilter(this.allGroups, this.filter);
			this.view.setGroups(groups);
		}
		onIsCollapsedChanged() {
			this.view.setIsCollapsed(this.api.isCollapsed());
		}
		onToggleClicked() {
			this.api.toggleIsCollapsed();
		}
		onFilterInputChanged(value) {
			this.filter = value;
			this.render();
		}
	}

	class ToolboxExtension {
		create(root, api) {
			return Toolbox.create(root, api.toolbox, api.i18n);
		}
	}

	const defaultConfiguration = {
		gapWidth: 88,
		gapHeight: 24,
		radius: 6,
		iconSize: 16
	};
	class RectPlaceholderExtension {
		static create(configuration) {
			return new RectPlaceholderExtension(configuration !== null && configuration !== void 0 ? configuration : defaultConfiguration);
		}
		constructor(configuration) {
			this.configuration = configuration;
			this.alongGapSize = new Vector(defaultConfiguration.gapWidth, defaultConfiguration.gapHeight);
			this.perpendicularGapSize = new Vector(defaultConfiguration.gapHeight, defaultConfiguration.gapWidth);
		}
		getGapSize(orientation) {
			return orientation === exports.PlaceholderGapOrientation.perpendicular ? this.perpendicularGapSize : this.alongGapSize;
		}
		createForGap(parent, parentSequence, index, orientation) {
			const gapSize = this.getGapSize(orientation);
			return RectPlaceholder.create(parent, gapSize, exports.PlaceholderDirection.gap, parentSequence, index, this.configuration.radius, this.configuration.iconSize);
		}
		createForArea(parent, size, direction, parentSequence, index) {
			return RectPlaceholder.create(parent, size, direction, parentSequence, index, this.configuration.radius, this.configuration.iconSize);
		}
	}

	class DefaultSequenceComponentExtension {
		constructor() {
			this.create = DefaultSequenceComponent.create;
		}
	}

	class DefaultStepComponentViewWrapperExtension {
		constructor() {
			this.wrap = (view) => view;
		}
	}

	class DefaultClickBehaviorWrapper {
		constructor() {
			this.wrap = (behavior) => behavior;
		}
	}

	class DefaultClickBehaviorWrapperExtension {
		create() {
			return new DefaultClickBehaviorWrapper();
		}
	}

	class DefaultStepBadgesDecoratorExtension {
		create(g, view, badges) {
			const position = new Vector(view.width, 0);
			return new DefaultBadgesDecorator(position, badges, g);
		}
	}

	class ServicesResolver {
		static resolve(extensions, configuration) {
			const services = {};
			merge(services, extensions || []);
			setDefaults(services, configuration);
			return services;
		}
	}
	function merge(services, extensions) {
		for (const ext of extensions) {
			if (ext.steps) {
				services.steps = (services.steps || []).concat(ext.steps);
			}
			if (ext.stepComponentViewWrapper) {
				services.stepComponentViewWrapper = ext.stepComponentViewWrapper;
			}
			if (ext.stepBadgesDecorator) {
				services.stepBadgesDecorator = ext.stepBadgesDecorator;
			}
			if (ext.clickBehaviorWrapperExtension) {
				services.clickBehaviorWrapperExtension = ext.clickBehaviorWrapperExtension;
			}
			if (ext.badges) {
				services.badges = (services.badges || []).concat(ext.badges);
			}
			if (ext.uiComponents) {
				services.uiComponents = (services.uiComponents || []).concat(ext.uiComponents);
			}
			if (ext.draggedComponent) {
				services.draggedComponent = ext.draggedComponent;
			}
			if (ext.wheelController) {
				services.wheelController = ext.wheelController;
			}
			if (ext.placeholder) {
				services.placeholder = ext.placeholder;
			}
			if (ext.regionComponentView) {
				services.regionComponentView = ext.regionComponentView;
			}
			if (ext.viewportController) {
				services.viewportController = ext.viewportController;
			}
			if (ext.grid) {
				services.grid = ext.grid;
			}
			if (ext.rootComponent) {
				services.rootComponent = ext.rootComponent;
			}
			if (ext.sequenceComponent) {
				services.sequenceComponent = ext.sequenceComponent;
			}
			if (ext.contextMenu) {
				services.contextMenu = ext.contextMenu;
			}
			if (ext.daemons) {
				services.daemons = (services.daemons || []).concat(ext.daemons);
			}
		}
	}
	function setDefaults(services, configuration) {
		if (!services.steps) {
			services.steps = [];
		}
		services.steps.push(ContainerStepExtension.create());
		services.steps.push(SwitchStepExtension.create());
		services.steps.push(TaskStepExtension.create());
		services.steps.push(LaunchPadStepExtension.create());
		if (!services.stepComponentViewWrapper) {
			services.stepComponentViewWrapper = new DefaultStepComponentViewWrapperExtension();
		}
		if (!services.stepBadgesDecorator) {
			services.stepBadgesDecorator = new DefaultStepBadgesDecoratorExtension();
		}
		if (!services.clickBehaviorWrapperExtension) {
			services.clickBehaviorWrapperExtension = new DefaultClickBehaviorWrapperExtension();
		}
		if (!services.badges) {
			services.badges = [];
		}
		if (findValidationBadgeIndex(services.badges) < 0) {
			services.badges.push(ValidationErrorBadgeExtension.create());
		}
		if (!services.draggedComponent) {
			services.draggedComponent = new DefaultDraggedComponentExtension();
		}
		if (!services.uiComponents) {
			services.uiComponents = [];
		}
		if (configuration.controlBar) {
			services.uiComponents.push(new ControlBarExtension());
		}
		if (configuration.editors) {
			services.uiComponents.push(new SmartEditorExtension(configuration.editors));
		}
		if (configuration.toolbox) {
			services.uiComponents.push(new ToolboxExtension());
		}
		if (!services.wheelController) {
			services.wheelController = new ClassicWheelControllerExtension();
		}
		if (!services.placeholder) {
			services.placeholder = RectPlaceholderExtension.create();
		}
		if (!services.regionComponentView) {
			services.regionComponentView = new DefaultRegionComponentViewExtension();
		}
		if (!services.viewportController) {
			services.viewportController = DefaultViewportControllerExtension.create();
		}
		if (!services.grid) {
			services.grid = LineGridExtension.create();
		}
		if (!services.rootComponent) {
			services.rootComponent = StartStopRootComponentExtension.create();
		}
		if (!services.sequenceComponent) {
			services.sequenceComponent = new DefaultSequenceComponentExtension();
		}
		if (!services.daemons) {
			services.daemons = [];
		}
		if (configuration.keyboard === undefined || configuration.keyboard) {
			services.daemons.push(KeyboardDaemonExtension.create(configuration.keyboard));
		}
	}

	function validateConfiguration(configuration) {
		const validateProperty = (key) => {
			if (configuration[key] === undefined) {
				throw new Error(`The "${key}" property is not defined in the configuration`);
			}
		};
		if (!configuration) {
			throw new Error('Configuration is not defined');
		}
		validateProperty('steps');
		validateProperty('toolbox');
		validateProperty('editors');
		validateProperty('controlBar');
	}

	class Designer {
		/**
		 * Creates a designer.
		 * @param placeholder Placeholder where the designer will be attached.
		 * @param startDefinition Start definition of a flow.
		 * @param configuration Designer's configuration.
		 * @returns An instance of the designer.
		 */
		static create(placeholder, startDefinition, configuration) {
			var _a;
			if (!placeholder) {
				throw new Error('Placeholder is not defined');
			}
			if (!startDefinition) {
				throw new Error('Start definition is not defined');
			}
			const config = configuration;
			validateConfiguration(config);
			if (!isElementAttached((_a = config.shadowRoot) !== null && _a !== void 0 ? _a : document, placeholder)) {
				throw new Error('Placeholder is not attached to the DOM');
			}
			const services = ServicesResolver.resolve(configuration.extensions, config);
			const designerContext = DesignerContext.create(placeholder, startDefinition, config, services);
			const designerApi = DesignerApi.create(designerContext);
			const view = DesignerView.create(placeholder, designerContext, designerApi);
			const designer = new Designer(view, designerContext.state, designerContext.stateModifier, designerContext.definitionWalker, designerContext.historyController, designerApi);
			view.workspace.onRendered.first().then(designer.onReady.forward);
			race(0, designerContext.state.onDefinitionChanged, designerContext.state.onSelectedStepIdChanged).subscribe(([definition, selectedStepId]) => {
				if (definition !== undefined) {
					designer.onDefinitionChanged.forward(designerContext.state.definition);
				}
				if (selectedStepId !== undefined) {
					designer.onSelectedStepIdChanged.forward(designerContext.state.selectedStepId);
				}
			});
			designerContext.state.onViewportChanged.subscribe(designer.onViewportChanged.forward);
			designerContext.state.onIsToolboxCollapsedChanged.subscribe(designer.onIsToolboxCollapsedChanged.forward);
			designerContext.state.onIsEditorCollapsedChanged.subscribe(designer.onIsEditorCollapsedChanged.forward);
			return designer;
		}
		constructor(view, state, stateModifier, walker, historyController, api) {
			this.view = view;
			this.state = state;
			this.stateModifier = stateModifier;
			this.walker = walker;
			this.historyController = historyController;
			this.api = api;
			/**
			 * @description Fires when the designer is initialized and ready to use.
			 */
			this.onReady = new SimpleEvent();
			/**
			 * @description Fires when the definition has changed.
			 */
			this.onDefinitionChanged = new SimpleEvent();
			/**
			 * @description Fires when the viewport has changed.
			 */
			this.onViewportChanged = new SimpleEvent();
			/**
			 * @description Fires when the selected step has changed.
			 */
			this.onSelectedStepIdChanged = new SimpleEvent();
			/**
			 * @description Fires when the toolbox is collapsed or expanded.
			 */
			this.onIsToolboxCollapsedChanged = new SimpleEvent();
			/**
			 * @description Fires when the editor is collapsed or expanded.
			 */
			this.onIsEditorCollapsedChanged = new SimpleEvent();
		}
		/**
		 * @returns the current definition of the workflow.
		 */
		getDefinition() {
			return this.state.definition;
		}
		/**
		 * @returns the validation result of the current definition.
		 */
		isValid() {
			return this.view.workspace.isValid;
		}
		/**
		 * @returns the readonly flag.
		 */
		isReadonly() {
			return this.state.isReadonly;
		}
		/**
		 * @description Changes the readonly flag.
		 */
		setIsReadonly(isReadonly) {
			this.state.setIsReadonly(isReadonly);
		}
		/**
		 * @returns current selected step id or `null` if nothing is selected.
		 */
		getSelectedStepId() {
			return this.state.selectedStepId;
		}
		/**
		 * @description Selects a step by the id.
		 */
		selectStepById(stepId) {
			this.stateModifier.trySelectStepById(stepId);
		}
		/**
		 * @returns the current viewport.
		 */
		getViewport() {
			return this.state.viewport;
		}
		/**
		 * @description Sets the viewport.
		 * @param viewport Viewport.
		 */
		setViewport(viewport) {
			this.state.setViewport(viewport);
		}
		/**
		 * @description Resets the viewport.
		 */
		resetViewport() {
			this.api.viewport.resetViewport();
		}
		/**
		 * @description Unselects the selected step.
		 */
		clearSelectedStep() {
			this.state.setSelectedStepId(null);
		}
		/**
		 * @description Moves the viewport to the step with the animation.
		 */
		moveViewportToStep(stepId) {
			this.api.viewport.moveViewportToStep(stepId);
		}
		/**
		 * @description Rerender the root component and all its children.
		 */
		updateRootComponent() {
			this.api.workspace.updateRootComponent();
		}
		/**
		 * @description Updates the layout of the designer.
		 */
		updateLayout() {
			this.api.workspace.updateCanvasSize();
			this.view.updateLayout();
		}
		/**
		 * @description Updates all badges.
		 */
		updateBadges() {
			this.api.workspace.updateBadges();
		}
		/**
		 * @returns a flag that indicates whether the toolbox is collapsed.
		 */
		isToolboxCollapsed() {
			return this.state.isToolboxCollapsed;
		}
		/**
		 * @description Sets a flag that indicates whether the toolbox is collapsed.
		 */
		setIsToolboxCollapsed(isCollapsed) {
			this.state.setIsToolboxCollapsed(isCollapsed);
		}
		/**
		 * @returns a flag that indicates whether the editor is collapsed.
		 */
		isEditorCollapsed() {
			return this.state.isEditorCollapsed;
		}
		/**
		 * @description Sets a flag that indicates whether the editor is collapsed.
		 */
		setIsEditorCollapsed(isCollapsed) {
			this.state.setIsEditorCollapsed(isCollapsed);
		}
		/**
		 * @description Dump the undo stack.
		 */
		dumpUndoStack() {
			return this.getHistoryController().dump();
		}
		/**
		 * Replaces the current definition with a new one and adds the previous definition to the undo stack.
		 * @param definition A new definition.
		 */
		replaceDefinition(definition) {
			return __awaiter(this, void 0, void 0, function* () {
				this.getHistoryController().replaceDefinition(definition);
				yield Promise.all([
					this.view.workspace.onRendered.first(),
					this.onDefinitionChanged.first()
				]);
			});
		}
		/**
		 * @param needle A step, a sequence or a step id.
		 * @returns parent steps and branch names.
		 */
		getStepParents(needle) {
			return this.walker.getParents(this.state.definition, needle);
		}
		/**
		 * @returns the definition walker.
		 */
		getWalker() {
			return this.walker;
		}
		/**
		 * @description Destroys the designer and deletes all nodes from the placeholder.
		 */
		destroy() {
			this.view.destroy();
		}
		getHistoryController() {
			if (!this.historyController) {
				throw new Error('Undo feature is not activated');
			}
			return this.historyController;
		}
	}

	exports.Badges = Badges;
	exports.CenteredViewportCalculator = CenteredViewportCalculator;
	exports.ClassicWheelControllerExtension = ClassicWheelControllerExtension;
	exports.ComponentContext = ComponentContext;
	exports.ComponentDom = ComponentDom;
	exports.ControlBarApi = ControlBarApi;
	exports.CustomActionController = CustomActionController;
	exports.DefaultRegionComponentViewExtension = DefaultRegionComponentViewExtension;
	exports.DefaultRegionView = DefaultRegionView;
	exports.DefaultSequenceComponent = DefaultSequenceComponent;
	exports.DefaultSequenceComponentView = DefaultSequenceComponentView;
	exports.DefaultViewportController = DefaultViewportController;
	exports.DefaultViewportControllerDesignerExtension = DefaultViewportControllerDesignerExtension;
	exports.DefaultViewportControllerExtension = DefaultViewportControllerExtension;
	exports.DefinitionWalker = DefinitionWalker;
	exports.Designer = Designer;
	exports.DesignerApi = DesignerApi;
	exports.DesignerContext = DesignerContext;
	exports.DesignerState = DesignerState;
	exports.Dom = Dom;
	exports.Editor = Editor;
	exports.EditorApi = EditorApi;
	exports.Icons = Icons;
	exports.InputView = InputView;
	exports.JoinView = JoinView;
	exports.LabelView = LabelView;
	exports.LineGridDesignerExtension = LineGridDesignerExtension;
	exports.ObjectCloner = ObjectCloner;
	exports.OutputView = OutputView;
	exports.PathBarApi = PathBarApi;
	exports.PlaceholderController = PlaceholderController;
	exports.RectPlaceholder = RectPlaceholder;
	exports.RectPlaceholderView = RectPlaceholderView;
	exports.SelectStepBehaviorEndToken = SelectStepBehaviorEndToken;
	exports.ServicesResolver = ServicesResolver;
	exports.SimpleEvent = SimpleEvent;
	exports.StartStopRootComponentDesignerExtension = StartStopRootComponentDesignerExtension;
	exports.StartStopRootComponentExtension = StartStopRootComponentExtension;
	exports.StepComponent = StepComponent;
	exports.StepExtensionResolver = StepExtensionResolver;
	exports.StepsDesignerExtension = StepsDesignerExtension;
	exports.TYPE = TYPE;
	exports.ToolboxApi = ToolboxApi;
	exports.Uid = Uid;
	exports.ValidationErrorBadgeExtension = ValidationErrorBadgeExtension;
	exports.Vector = Vector;
	exports.ViewportApi = ViewportApi;
	exports.WorkspaceApi = WorkspaceApi;
	exports.createContainerStepComponentViewFactory = createContainerStepComponentViewFactory;
	exports.createLaunchPadStepComponentViewFactory = createLaunchPadStepComponentViewFactory;
	exports.createSwitchStepComponentViewFactory = createSwitchStepComponentViewFactory;
	exports.createTaskStepComponentViewFactory = createTaskStepComponentViewFactory;
	exports.getAbsolutePosition = getAbsolutePosition;
	exports.race = race;

}));