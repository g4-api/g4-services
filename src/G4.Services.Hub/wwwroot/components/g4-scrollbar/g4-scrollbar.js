/*
 * G4(TM) scroll bars: G4Scrollbars.attach({ host, scrollElement, testId, isFramed }).
 *
 * Draws the designer's own vertical and horizontal scroll bars over the edges of a scrolling element,
 * in the look of the number input steppers: a column (or row) with step buttons at its ends, a track,
 * and a centered thumb. The element keeps scrolling natively (wheel, touchpad, keyboard, the text
 * cursor, assistive technology); only the browser's own scroll bars are hidden, and the drawn bars
 * mirror and drive the native scroll position.
 *
 * Contract:
 * - host: a positioned wrapper that contains the scrolling element (made relative when static); the
 *   bars are added to it and laid over the element's padding box.
 * - scrollElement: the element that scrolls (a text area or a list).
 * - testId: prefix of every part's data-test-id.
 * - isFramed: optional; borders each bar on every side, for an element without a border of its own
 *   (a panel), where a bar drawn against the page edge would otherwise look sunk in.
 * - Host classes: g4-scrollbar-host--vertical and g4-scrollbar-host--horizontal while a bar shows, so
 *   the owner can pad its content clear of the bars.
 * - Returns a controller: refresh() after the owner changes the content in code; detach() removes the
 *   bars and every listener. Content added, removed, or restyled inside the element (for example a
 *   panel that swaps its editor) is followed by itself, once per frame.
 * - A bar shows only when its direction overflows and the element's overflow style for it is not hidden.
 */
(() => {
    'use strict';

    // Component name: the CSS block name of the bars.
    const COMPONENT_NAME = 'g4-scrollbar';

    // Scrolls this far per step when the element has no numeric line height (one toolbox row).
    const DEFAULT_STEP_PIXELS = 26;

    // Waits this long while a step button is held before it starts repeating, as a native scroll bar does.
    const REPEAT_DELAY_MILLISECONDS = 300;

    // Repeats a held step button at this pace.
    const REPEAT_INTERVAL_MILLISECONDS = 50;

    // Keeps the thumb large enough to grab when the content is very long.
    const THUMB_MINIMUM_PIXELS = 24;

    // SVG path of the up caret: the number input's increase icon; the CSS turns it left and right.
    const UP_ICON_PATH = 'M300.3 199.2C312.9 188.9 331.4 189.7 343.1 201.4L471.1 329.4C480.3 338.6 483 352.3 '
        + '478 364.3C473 376.3 461.4 384 448.5 384L192.5 384C179.6 384 167.9 376.2 162.9 364.2C157.9 352.2 160.7 '
        + '338.5 169.9 329.4L297.9 201.4L300.3 199.2z';

    // SVG path of the down caret: the number input's decrease icon.
    const DOWN_ICON_PATH = 'M300.3 440.8C312.9 451 331.4 450.3 343.1 438.6L471.1 310.6C480.3 301.4 483 287.7 '
        + '478 275.7C473 263.7 461.4 256 448.5 256L192.5 256C179.6 256 167.9 263.8 162.9 275.8C157.9 287.8 160.7 '
        + '301.5 169.9 310.6L297.9 438.6L300.3 440.8z';

    // Property names of one scroll direction, so one axis class serves both bars.
    const AXES = {
        horizontal: {
            clientSize: 'clientWidth',
            endIconPath: UP_ICON_PATH,
            endName: 'right',
            name: 'horizontal',
            overflowProperty: 'overflowX',
            pointerCoordinate: 'clientX',
            rectStart: 'left',
            scrollPosition: 'scrollLeft',
            scrollProperty: 'left',
            scrollSize: 'scrollWidth',
            sizeStyle: 'width',
            startIconPath: UP_ICON_PATH,
            startName: 'left',
            translateFunction: 'translateX'
        },
        vertical: {
            clientSize: 'clientHeight',
            endIconPath: DOWN_ICON_PATH,
            endName: 'down',
            name: 'vertical',
            overflowProperty: 'overflowY',
            pointerCoordinate: 'clientY',
            rectStart: 'top',
            scrollPosition: 'scrollTop',
            scrollProperty: 'top',
            scrollSize: 'scrollHeight',
            sizeStyle: 'height',
            startIconPath: UP_ICON_PATH,
            startName: 'up',
            translateFunction: 'translateY'
        }
    };

    /**
     * One drawn scroll bar for one direction: step buttons at its ends, a track, and a thumb.
     *
     * @remarks
     * Owns its elements, the thumb drag, and the held-button repeat; it reads and writes only the scroll
     * element's position for its own direction. Its owner decides when it shows.
     */
    class G4ScrollbarAxis {
        // Property names of this bar's direction.
        #axis = null;

        // Thumb drag start (pointer position and scroll position); null while no drag runs.
        #dragStart = null;

        // Step button at the end of the bar (down or right).
        #endButton = null;

        // Step button at the start of the bar (up or left).
        #startButton = null;

        // Timer of a held step button: the repeat delay first, then the repeat interval; null when idle.
        #repeatTimer = null;

        // The bar element.
        #root = null;

        // The thumb, sized to the visible share and moved to the scroll position.
        #thumb = null;

        // The track the thumb travels; a press on it pages the element.
        #track = null;

        // The element this bar scrolls.
        #scrollElement = null;

        // Distance one step button press scrolls.
        #stepPixels = DEFAULT_STEP_PIXELS;

        /**
         * Steps the element once, and keeps stepping while the step button is held.
         *
         * @remarks
         * Captures the pointer, so releasing it anywhere ends the repeat. The button's data-direction is
         * -1 (toward the start) or 1 (toward the end).
         *
         * @param {PointerEvent} event - The press on a step button.
         */
        #onButtonPointerDown = (event) => {
            // Keep the press from focusing the button, moving the text cursor, or reaching the track.
            event.preventDefault();
            event.stopPropagation();

            // Step once now, as a click does.
            const button = event.currentTarget;
            const direction = Number(button.dataset.direction);
            const onScrollStep = () => {
                this.#scrollElement.scrollBy({ [this.#axis.scrollProperty]: direction * this.#stepPixels });
            };

            onScrollStep();

            // Keep stepping after a short delay while the button stays held.
            const onRepeatDelayElapsed = () => {
                this.#repeatTimer = setInterval(onScrollStep, REPEAT_INTERVAL_MILLISECONDS);
            };

            button.setPointerCapture(event.pointerId);
            this.#stopRepeat();
            this.#repeatTimer = setTimeout(onRepeatDelayElapsed, REPEAT_DELAY_MILLISECONDS);
        };

        /**
         * Ends a held step button.
         *
         * @param {PointerEvent} event - The pointer release or cancel.
         */
        #onButtonPointerUp = (event) => {
            // Stop stepping, then release the pointer the press captured.
            const button = event.currentTarget;

            this.#stopRepeat();

            if (button.hasPointerCapture(event.pointerId)) {
                button.releasePointerCapture(event.pointerId);
            }
        };

        /**
         * Starts a thumb drag.
         *
         * @remarks
         * Captures the pointer, so the drag continues when the pointer leaves the thumb or the element.
         *
         * @param {PointerEvent} event - The press on the thumb.
         */
        #onThumbPointerDown = (event) => {
            // Keep the press from moving the text cursor or reaching the track's paging handler.
            event.preventDefault();
            event.stopPropagation();

            // Capture the pointer and remember where the drag started.
            this.#thumb.setPointerCapture(event.pointerId);
            this.#thumb.classList.add('is-dragging');
            this.#dragStart = {
                pointerPosition: event[this.#axis.pointerCoordinate],
                scrollPosition: this.#scrollElement[this.#axis.scrollPosition]
            };
        };

        /**
         * Scrolls the element while the thumb is dragged.
         *
         * @param {PointerEvent} event - The pointer move.
         */
        #onThumbPointerMove = (event) => {
            if (!this.#dragStart) {
                return;
            }

            // Turn the pointer travel along the free track into the same share of the scroll range.
            const { maximumScrollPosition, thumbTravel } = this.#getMetrics();
            const pointerTravel = event[this.#axis.pointerCoordinate] - this.#dragStart.pointerPosition;
            const scrollPerPixel = maximumScrollPosition / Math.max(1, thumbTravel);

            const scrollPosition = this.#dragStart.scrollPosition + pointerTravel * scrollPerPixel;

            this.#scrollElement[this.#axis.scrollPosition] = scrollPosition;
        };

        /**
         * Ends a thumb drag, and releases the pointer and the dragging look.
         *
         * @param {PointerEvent} event - The pointer release or cancel.
         */
        #onThumbPointerUp = (event) => {
            if (!this.#dragStart) {
                return;
            }

            this.#dragStart = null;
            this.#thumb.classList.remove('is-dragging');

            if (this.#thumb.hasPointerCapture(event.pointerId)) {
                this.#thumb.releasePointerCapture(event.pointerId);
            }
        };

        /**
         * Pages the element toward a press on the track, as a native scroll bar does.
         *
         * @param {PointerEvent} event - The press on the track.
         */
        #onTrackPointerDown = (event) => {
            // Ignore presses on the thumb; the thumb drags instead.
            if (event.target !== this.#track) {
                return;
            }

            // Keep the press from moving the text cursor.
            event.preventDefault();

            // Scroll one visible size toward the side of the thumb that was pressed.
            const thumbStart = this.#thumb.getBoundingClientRect()[this.#axis.rectStart];
            const direction = event[this.#axis.pointerCoordinate] < thumbStart ? -1 : 1;
            const pageSize = this.#scrollElement[this.#axis.clientSize];

            this.#scrollElement.scrollBy({ [this.#axis.scrollProperty]: direction * pageSize });
        };

        /**
         * Creates the bar for one direction.
         *
         * @param {{ axis: object, scrollElement: HTMLElement, stepPixels: number, testId: string }} options - The
         *     direction's property names, the element to scroll, the step distance, and the test id prefix.
         */
        constructor(options) {
            const { axis, scrollElement, stepPixels, testId } = options;

            this.#axis = axis;
            this.#scrollElement = scrollElement;
            this.#stepPixels = stepPixels;

            // Build the bar: start button, track with the thumb, end button.
            this.#root = document.createElement('div');
            this.#root.className = `${COMPONENT_NAME} ${COMPONENT_NAME}--${axis.name}`;
            this.#root.hidden = true;
            this.#root.dataset.testId = `${testId}-scrollbar-${axis.name}`;

            this.#track = document.createElement('div');
            this.#track.className = `${COMPONENT_NAME}__track`;
            this.#track.dataset.testId = `${testId}-scrollbar-${axis.name}-track`;

            this.#thumb = document.createElement('div');
            this.#thumb.className = `${COMPONENT_NAME}__thumb`;
            this.#thumb.dataset.testId = `${testId}-scrollbar-${axis.name}-thumb`;

            this.#startButton = this.#newButton({
                direction: -1,
                iconPath: axis.startIconPath,
                name: axis.startName,
                testId
            });
            this.#endButton = this.#newButton({
                direction: 1,
                iconPath: axis.endIconPath,
                name: axis.endName,
                testId
            });

            this.#track.append(this.#thumb);
            this.#root.append(this.#startButton, this.#track, this.#endButton);

            // Listen for the thumb drag and track presses.
            this.#thumb.addEventListener('pointerdown', this.#onThumbPointerDown);
            this.#thumb.addEventListener('pointermove', this.#onThumbPointerMove);
            this.#thumb.addEventListener('pointerup', this.#onThumbPointerUp);
            this.#thumb.addEventListener('pointercancel', this.#onThumbPointerUp);
            this.#track.addEventListener('pointerdown', this.#onTrackPointerDown);
        }

        /**
         * Gets the bar element.
         *
         * @returns {HTMLDivElement} The bar.
         */
        get element() {
            return this.#root;
        }

        /**
         * Stops a held step button, so a removed bar leaves no timer running.
         */
        dispose() {
            this.#stopRepeat();
        }

        /**
         * Shows or hides the bar.
         *
         * @param {boolean} isVisible - Whether the bar shows.
         */
        setVisible(isVisible) {
            this.#root.hidden = !isVisible;
        }

        /**
         * Determines whether the element overflows in this bar's direction and is allowed to scroll in it.
         *
         * @returns {boolean} `true` when the bar should show.
         */
        testOverflow() {
            // Respect an owner that turned scrolling off for this direction, as a native scroll bar would.
            const overflowStyle = getComputedStyle(this.#scrollElement)[this.#axis.overflowProperty];
            const isScrollAllowed = overflowStyle !== 'hidden' && overflowStyle !== 'visible';
            const scrollSize = this.#scrollElement[this.#axis.scrollSize];
            const clientSize = this.#scrollElement[this.#axis.clientSize];
            const isOverflowing = scrollSize > clientSize + 1;

            return isScrollAllowed && isOverflowing;
        }

        /**
         * Sizes and places the thumb from the scroll state, and dims the step buttons at the ends.
         */
        update() {
            if (this.#root.hidden) {
                return;
            }

            // Size the thumb to the visible share and move it to the same share of its travel.
            const { maximumScrollPosition, thumbSize, thumbTravel } = this.#getMetrics();
            const scrollPosition = this.#scrollElement[this.#axis.scrollPosition];
            const thumbOffset = (scrollPosition / Math.max(1, maximumScrollPosition)) * thumbTravel;

            this.#thumb.style[this.#axis.sizeStyle] = `${thumbSize}px`;
            this.#thumb.style.transform = `${this.#axis.translateFunction}(${thumbOffset}px)`;

            // Dim the step buttons at the ends, where they cannot scroll further.
            const isAtStart = scrollPosition <= 0;
            const isAtEnd = scrollPosition >= maximumScrollPosition - 1;

            this.#startButton.classList.toggle('is-disabled', isAtStart);
            this.#endButton.classList.toggle('is-disabled', isAtEnd);
        }

        /**
         * Gets the thumb geometry for the element's current size and content.
         *
         * @remarks
         * Compute-only over the current layout. The thumb shows the visible share, never less than
         * THUMB_MINIMUM_PIXELS, and travels the rest of the track.
         *
         * @returns {{ maximumScrollPosition: number, thumbSize: number, thumbTravel: number }} The scroll
         *     range, the thumb length, and the distance the thumb can travel, in pixels.
         */
        #getMetrics() {
            const clientSize = this.#scrollElement[this.#axis.clientSize];
            const scrollSize = this.#scrollElement[this.#axis.scrollSize];
            const trackSize = this.#track[this.#axis.clientSize];
            const visibleShare = clientSize / Math.max(1, scrollSize);
            const thumbSize = Math.max(THUMB_MINIMUM_PIXELS, Math.round(trackSize * visibleShare));

            return {
                maximumScrollPosition: Math.max(0, scrollSize - clientSize),
                thumbSize,
                thumbTravel: Math.max(0, trackSize - thumbSize)
            };
        }

        /**
         * Creates one step button with its caret.
         *
         * @remarks
         * The button is not focusable: the bars are hidden from assistive technology, and the element
         * itself scrolls with the keyboard.
         *
         * @param {{ direction: number, iconPath: string, name: string, testId: string }} options - The step
         *     direction (-1 toward the start, 1 toward the end), the caret path, the part name, and the test
         *     id prefix.
         * @returns {HTMLButtonElement} The button.
         */
        #newButton(options) {
            const { direction, iconPath, name, testId } = options;
            const button = document.createElement('button');

            // Build the button with its caret; the CSS turns the horizontal carets left and right.
            button.type = 'button';
            button.tabIndex = -1;
            button.className = `${COMPONENT_NAME}__button ${COMPONENT_NAME}__button--${name}`;
            button.dataset.direction = `${direction}`;
            button.dataset.testId = `${testId}-scroll-${name}-button`;
            button.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 640 640" aria-hidden="true">'
                + `<path fill="currentColor" d="${iconPath}"/></svg>`;

            // Step on press and keep stepping while held; any release or cancel ends the repeat.
            button.addEventListener('pointerdown', this.#onButtonPointerDown);
            button.addEventListener('pointerup', this.#onButtonPointerUp);
            button.addEventListener('pointercancel', this.#onButtonPointerUp);

            return button;
        }

        /**
         * Stops a held step button's pending delay or repeat.
         */
        #stopRepeat() {
            // One timer field holds either the delay timeout or the repeat interval; clearing both kinds is safe.
            clearTimeout(this.#repeatTimer);
            clearInterval(this.#repeatTimer);
            this.#repeatTimer = null;
        }
    }

    /**
     * The vertical and horizontal bars of one scrolling element, with the corner between them.
     *
     * @remarks
     * Owns the overlay that lies over the element's padding box, the host classes that tell the owner
     * which bars show, and the scroll, input, and size listeners that keep the bars in step.
     */
    class G4ScrollbarSet {
        // The bar along the bottom edge.
        #horizontal = null;

        // The bar along the right edge.
        #vertical = null;

        // Square that fills the corner where both bars meet.
        #corner = null;

        // The positioned wrapper that holds the overlay.
        #host = null;

        // Pending once-per-frame refresh after content changes; 0 while none is pending.
        #frameRequest = 0;

        // Watches the element's content, so swapped or restyled content updates the bars.
        #mutationObserver = null;

        // Overlay laid over the scroll element's padding box; it holds the bars and the corner.
        #overlay = null;

        // Watches the element and the host size, so the bars follow resizes and layout changes.
        #resizeObserver = null;

        // The element that scrolls.
        #scrollElement = null;

        /**
         * Updates the bars after the element scrolls, the user types, or a size changes.
         */
        #onChange = () => {
            this.refresh();
        };

        /**
         * Updates the bars once per frame after the element's content changes.
         *
         * @remarks
         * Content can change many times in one frame (an editor rebuilt node by node), and the bars'
         * own updates inside nested scrolling content also count as changes, so one refresh per frame
         * is enough and keeps the work bounded.
         */
        #onContentChange = () => {
            if (this.#frameRequest !== 0) {
                return;
            }

            // Refresh after the browser lays out the changed content.
            const onFrame = () => {
                this.#frameRequest = 0;
                this.refresh();
            };

            this.#frameRequest = requestAnimationFrame(onFrame);
        };

        /**
         * Adds the bars to a host and starts following its scrolling element.
         *
         * @param {{ host: HTMLElement, scrollElement: HTMLElement, testId: string, isFramed?: boolean }} options -
         *     The positioned wrapper, the element that scrolls, the test id prefix, and whether each bar is
         *     bordered on every side.
         */
        constructor(options) {
            const { host, isFramed, scrollElement, testId } = options;

            this.#host = host;
            this.#scrollElement = scrollElement;

            // Make the host the overlay's positioning parent, and hide the browser's own scroll bars.
            if (getComputedStyle(host).position === 'static') {
                host.style.position = 'relative';
            }

            scrollElement.classList.add(`${COMPONENT_NAME}-target`);

            // Step by the element's line height, so a text area moves one line per press.
            const lineHeight = Number.parseFloat(getComputedStyle(scrollElement).lineHeight);
            const stepPixels = Number.isFinite(lineHeight) ? lineHeight : DEFAULT_STEP_PIXELS;

            // Build the overlay with both bars and the corner; assistive technology uses the element itself.
            this.#vertical = new G4ScrollbarAxis({ axis: AXES.vertical, scrollElement, stepPixels, testId });
            this.#horizontal = new G4ScrollbarAxis({ axis: AXES.horizontal, scrollElement, stepPixels, testId });

            this.#corner = document.createElement('div');
            this.#corner.className = `${COMPONENT_NAME}__corner`;
            this.#corner.hidden = true;

            this.#overlay = document.createElement('div');
            this.#overlay.className = `${COMPONENT_NAME}s`;
            this.#overlay.classList.toggle(`${COMPONENT_NAME}s--framed`, Boolean(isFramed));
            this.#overlay.setAttribute('aria-hidden', 'true');
            this.#overlay.dataset.testId = `${testId}-scrollbars`;
            this.#overlay.append(this.#vertical.element, this.#horizontal.element, this.#corner);
            host.append(this.#overlay);

            // Follow scrolling, typing, and size changes of the element and its host.
            scrollElement.addEventListener('scroll', this.#onChange, { passive: true });
            scrollElement.addEventListener('input', this.#onChange);

            this.#resizeObserver = new ResizeObserver(this.#onChange);
            this.#resizeObserver.observe(scrollElement, { box: 'border-box' });
            this.#resizeObserver.observe(host, { box: 'border-box' });

            // Follow content swapped, added, or restyled inside the element, which changes what overflows.
            this.#mutationObserver = new MutationObserver(this.#onContentChange);
            this.#mutationObserver.observe(scrollElement, { attributes: true, childList: true, subtree: true });

            this.refresh();
        }

        /**
         * Removes the bars, the host classes, and every listener.
         */
        detach() {
            // Stop following the element, then remove everything this set added.
            this.#scrollElement.removeEventListener('scroll', this.#onChange);
            this.#scrollElement.removeEventListener('input', this.#onChange);
            this.#resizeObserver.disconnect();
            this.#mutationObserver.disconnect();
            cancelAnimationFrame(this.#frameRequest);
            this.#vertical.dispose();
            this.#horizontal.dispose();

            this.#overlay.remove();
            this.#scrollElement.classList.remove(`${COMPONENT_NAME}-target`);
            this.#host.classList.remove(`${COMPONENT_NAME}-host--vertical`, `${COMPONENT_NAME}-host--horizontal`);
        }

        /**
         * Shows the bars the content needs, lays them over the element, and updates their thumbs.
         *
         * @remarks
         * Call it after changing the element's content in code; scrolling, typing, and resizes call it
         * themselves. The host classes can pad the content, which can change what overflows, so the
         * decision is taken twice; the second pass settles it.
         */
        refresh() {
            // A detached or hidden element has no layout to measure.
            if (!this.#scrollElement.isConnected) {
                return;
            }

            // Decide which bars show, letting the owner's padding for them take effect once.
            for (let pass = 0; pass < 2; pass++) {
                const isVertical = this.#vertical.testOverflow();
                const isHorizontal = this.#horizontal.testOverflow();

                this.#host.classList.toggle(`${COMPONENT_NAME}-host--vertical`, isVertical);
                this.#host.classList.toggle(`${COMPONENT_NAME}-host--horizontal`, isHorizontal);
                this.#overlay.classList.toggle('has-vertical', isVertical);
                this.#overlay.classList.toggle('has-horizontal', isHorizontal);
                this.#vertical.setVisible(isVertical);
                this.#horizontal.setVisible(isHorizontal);
                this.#corner.hidden = !(isVertical && isHorizontal);
            }

            // Lay the overlay over the element's padding box, inside its border.
            const element = this.#scrollElement;

            this.#overlay.style.left = `${element.offsetLeft + element.clientLeft}px`;
            this.#overlay.style.top = `${element.offsetTop + element.clientTop}px`;
            this.#overlay.style.width = `${element.clientWidth}px`;
            this.#overlay.style.height = `${element.clientHeight}px`;

            // Stack the overlay just above an element that sits on its own layer (the designer's right panel
            // does); otherwise the stylesheet's layer applies.
            const elementZIndex = Number.parseInt(getComputedStyle(element).zIndex, 10);

            this.#overlay.style.zIndex = Number.isFinite(elementZIndex) ? `${elementZIndex + 1}` : '';

            // Size and place each shown thumb.
            this.#vertical.update();
            this.#horizontal.update();
        }
    }

    // Publish one entry point for the components that scroll.
    globalThis.G4Scrollbars = {
        /**
         * Adds the designer's scroll bars to a scrolling element.
         *
         * @param {{ host: HTMLElement, scrollElement: HTMLElement, testId: string, isFramed?: boolean }} options -
         *     The positioned wrapper, the element that scrolls, the test id prefix, and whether each bar is
         *     bordered on every side.
         * @returns {{ detach: Function, refresh: Function }} The controller of the bars.
         */
        attach(options) {
            return new G4ScrollbarSet(options);
        }
    };
})();
