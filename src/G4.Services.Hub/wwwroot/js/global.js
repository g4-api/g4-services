let _averageCounter = 0;
let _counter;
let _cache = {};
let _cacheKeys = [];
let _client = {};
let _cliFactory = {};
let _designer;
let _editorObserver;
let _manifests = {};
let _manifestsGroups = [];
let _stateMachine = {};
let _timer;

const _flowableTypes = ["ACTION", "CONTENT", "JOB", "STAGE", "TRANSFORMER"];
const _auditableTypes = ["ACTION", "CONTENT", "TRANSFORMER"];

const _connection = new signalR
	.HubConnectionBuilder()
	.withUrl("/hub/v4/g4/notifications")
	.withAutomaticReconnect()
	.build();

// Start the SiognalR connection
_connection
	.start()
	.catch(err => console.error("Connection failed:", err.message));

(async () => {
	// Create a new G4Client instance.
	_client = new G4Client();

	// Create a new CliFactory instance.
	_cliFactory = new CliFactory();

	// Fetch manifests and manifest groups from the G4Client.
	_manifests = await _client.getManifests();
	_manifestsGroups = await _client.getGroups();

	// Store the cache in a global variable for later use.
	_cache = await _client.getCache();

	// Store the cache keys in a global variable for later use.
	_cacheKeys = Object.keys(_cache).map(key => key.toUpperCase());

    /**
     * Wait for the timer element to be available in the DOM.
     * Once the element is found or after 5000ms, execute the callback.
     */
    Utilities.waitForElement('#designer--timer', 5000).then(() => {
        // Get the timer element from the DOM.
		const timerElement = document.querySelector('#designer--timer');

        // Create a new Timer instance with the timer element.
        _timer = new Timer(timerElement);
	});

    /**
     * Wait for the counter element to be available in the DOM.
     * Once the element is found or after 5000ms, execute the callback.
     */
    Utilities.waitForElement('#designer--total-actions', 5000).then(() => {
        // Get the counter element from the DOM.
        const counterElement = document.querySelector('#designer--total-actions');

        // Create a new Counter instance with the counter element.
		_counter = new Counter(counterElement);
	});

    /**
     * Wait for the average counter element to be available in the DOM.
     * Once the element is found or after 5000ms, execute the callback.
     */
    Utilities.waitForElement('#designer--average-action-time', 5000).then(() => {
        // Get the average counter element from the DOM.
		const averageElement = document.querySelector('#designer--average-action-time');

        // Create a new AverageCounter instance with the average counter element.
        _averageCounter = new AverageCounter(averageElement);
	});

    /**
     * Wait for the smart editor element to be available in the DOM.
     * Once the element is found or after 5000ms, execute the callback.
     */
    Utilities.waitForElement('#designer .sqd-smart-editor', 5000).then(() => {
        // Select the target node that we want to observe for DOM changes.
        const targetNode = document.querySelector('#designer .sqd-smart-editor');

        // Query all elements in the document that match the provided selector.
        const elements = document.querySelectorAll('#designer .sqd-smart-editor textarea') || [];

        // Loop through each element and dispatch the event.
        for (const element of elements) {
            Utilities.setTextareaSize(element, 8);
        }

        // Define the configuration for the MutationObserver.
        // This configuration listens for changes to the child nodes and the entire subtree.
        const config = {
            attributes: false,
            childList: true,
            subtree: true
        };

        // Create an Observer instance for the target node.
        _editorObserver = new Observer(targetNode);

        // Start observing the target node for DOM mutations.
        _editorObserver.observeDOMChanges(config, (mutationsList) => {
            // Convert each mutation's addedNodes (a NodeList) into an array and flatten them into a single array.
            const addedNodes = mutationsList.flatMap(mutation => Array.from(mutation.addedNodes));

            // If no nodes were added during the mutations, exit early.
            if (addedNodes.length === 0) {
                return;
            }

            // Query all elements in the document that match the provided selector.
            const elements = document.querySelectorAll('#designer .sqd-smart-editor textarea') || [];

            // Loop through each element and dispatch the event.
            for (const element of elements) {
                Utilities.setTextareaSize(element, 8);
            }
        });
    });

    /**
     * Wait for the canvas element to be available in the DOM.
     * Once the element is found or after 5000ms, execute the callback.
     */
    Utilities.waitForElement('.sqd-workspace', 5000);
})();
