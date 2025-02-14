/**
 * Observer class to monitor DOM mutations on a specified element.
 */
class Observer {
    /**
     * Create an Observer instance.
     *
     * @param {Element} targetNode - The DOM element to be observed.
     */
    constructor(targetNode) {
        // Save the target DOM element for later use.
        this.targetNode = targetNode;
    }

    /**
     * Observe changes to the target DOM element using MutationObserver.
     *
     * @param {Object}   config   - Configuration object that specifies which mutations to observe.
     * @param {Function} callback - Function to execute when mutations are detected.
     *                              The callback receives a list of MutationRecords and the observer.
     * @returns {MutationObserver}  The MutationObserver instance, which can be used to disconnect observation later.
     */
    observeDOMChanges(config, callback) {
        // Create a new MutationObserver instance, passing in the callback function.
        const observer = new MutationObserver(callback);

        // Start observing the target node using the provided configuration.
        observer.observe(this.targetNode, config);

        // Return the observer instance so that it can be disconnected when necessary.
        return observer;
    }
}
