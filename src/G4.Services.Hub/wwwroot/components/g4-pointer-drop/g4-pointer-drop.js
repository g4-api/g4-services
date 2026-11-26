/*
 * G4(TM) pointer drop: a G4 designer plugin (see components/g4-designer-plugins).
 *
 * Makes a dragged step land on the gap under the pointer. Without the plugin the designer draws the whole
 * step as the dragged shape, centres it on the pointer, and drops on the first gap, counted from the
 * top-left, that the shape covers. A shape as large as a Stage, a Job, or a Parallel container covers
 * several gaps at once, and the gap at the end of a Job's rules sits directly above the gap after that Job,
 * so a container aimed at "after the Job" landed inside the Job.
 *
 * What the plugin changes:
 * - The dragged shape is a small chip with the step's icon and name, drawn beside the pointer, so the gaps
 *   on the canvas, and the one the designer highlights as the drop place, stay in view.
 * - The area the designer tests the gaps against is a tiny square on the pointer, so only the gap under the
 *   pointer can be picked. A drop with the pointer outside every gap changes nothing, as before.
 *
 * Contract:
 * - Uses only the designer's draggedComponent extension, which the bundle offers for the dragged shape. It
 *   never edits js/canvas.umd.js or js/canvas.js.
 * - Applies to every drag: a new step from the toolbox, and a step moved on the canvas.
 * - Keeps no state.
 * - Load after components/g4-designer-plugins and before the script that creates the designer.
 */
(() => {
    'use strict';

    // Component name: the CSS block name of the chip.
    const COMPONENT_NAME = 'g4-pointer-drop';

    // Plugin name: how the plugin host names the plugin in its reports.
    const PLUGIN_NAME = 'pointer-drop';

    // Side of the square the designer tests the gaps against. The designer centres the square on the pointer,
    // so two pixels make it the pointer itself; the side cannot be zero, because a gap counts only when the
    // square overlaps it.
    const HIT_SQUARE_SIZE_PIXELS = 2;

    /**
     * Draws the icon of a step into the chip, as inline SVG when the page can provide it.
     *
     * @remarks
     * The icon address comes from the designer configuration, the same way the toolbox gets it, and is read
     * when the drag starts, so an icon another plugin adds to the configuration is found too. The page's
     * helper then replaces the image with inline SVG, which takes the theme's icon colour; on a page without
     * the helper the image stays and is tinted by the theme's image filter. A step without an icon gets none.
     *
     * @param {{ chipElement: HTMLElement, configuration: Object, step: Object }} options - The chip, the
     *     designer configuration, and the dragged step.
     */
    function addIcon(options) {
        const { chipElement, configuration, step } = options;

        // Ask the configuration for the step's icon; a configuration without a provider has no icons.
        const iconUrlProvider = configuration.steps?.iconUrlProvider;
        const iconUrl = typeof iconUrlProvider === 'function'
            ? iconUrlProvider(step.componentType, step.type)
            : null;

        if (typeof iconUrl !== 'string' || iconUrl === '') {
            return;
        }

        // Draw the icon as an image first, so the chip has it at once.
        const iconElement = document.createElement('span');
        const imageElement = document.createElement('img');

        iconElement.classList.add(`${COMPONENT_NAME}__icon`);
        imageElement.alt = '';
        imageElement.src = iconUrl;
        iconElement.appendChild(imageElement);
        chipElement.appendChild(iconElement);

        // Swap the image for inline SVG when the page offers its helper; a failed swap keeps the image.
        const isSwitchable = typeof Utilities !== 'undefined' && typeof Utilities.switchImage === 'function';

        if (!isSwitchable) {
            return;
        }

        const icons = typeof _svgsCache === 'undefined' ? {} : _svgsCache;

        void Utilities.switchImage(iconElement, icons);
    }

    /**
     * Creates the designer extension that draws the dragged shape.
     *
     * @remarks
     * Compute-only factory. The designer calls create when a drag starts and reads three numbers from the
     * result: it puts the top-left corner of the shape's layer half a width and half a height away from the
     * pointer, and it tests the gaps against a rectangle of that width and height at that corner. The chip
     * is positioned by the component's layout styles, away from that corner, so what is drawn and what is
     * tested are independent.
     *
     * @param {Object} configuration - The designer configuration, read for the step icons.
     * @returns {{ create: Function }} The draggedComponent extension.
     */
    function newDraggedComponentExtension(configuration) {
        return {
            create(parentElement, step) {
                // Draw the chip: the step's icon, then its name.
                const chipElement = document.createElement('div');
                const nameElement = document.createElement('span');

                chipElement.classList.add(COMPONENT_NAME);
                chipElement.dataset.testId = 'pointer-drop-chip';
                addIcon({ chipElement, configuration, step });

                nameElement.classList.add(`${COMPONENT_NAME}__name`);
                nameElement.textContent = typeof step.name === 'string' ? step.name : '';
                chipElement.appendChild(nameElement);
                parentElement.appendChild(chipElement);

                // Report the square on the pointer as the shape's size, at the page's own scale, so the
                // designer tests only the gap under the pointer whatever the canvas zoom is.
                return {
                    height: HIT_SQUARE_SIZE_PIXELS,
                    scale: 1,
                    width: HIT_SQUARE_SIZE_PIXELS,
                    destroy() {
                        // The designer removes the layer that holds the chip; nothing else was created.
                    }
                };
            }
        };
    }

    // Register the plugin; it installs into every designer the page creates.
    globalThis.G4DesignerPlugins?.register({
        name: PLUGIN_NAME,

        /**
         * Installs the plugin into a designer's configuration.
         *
         * @param {Object} configuration - The designer configuration, before the designer is created.
         */
        install(configuration) {
            // Add the dragged shape to the extensions the configuration already has; the designer takes the
            // last draggedComponent extension it is given, and no other plugin provides one.
            const extensions = configuration.extensions ?? [];

            configuration.extensions = [...extensions, { draggedComponent: newDraggedComponentExtension(configuration) }];
        }
    });
})();
