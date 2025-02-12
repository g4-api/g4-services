let _averageActionTime = 0;
let _cache = {};
let _cacheKeys = [];
let _client = {};
let _cliFactory = {};
let _designer;
let _manifests = {};
let _manifestsGroups = [];
let _stateMachine = {};
let _timer;
let _totalActions = 0;

const _includeTypes = ["ACTION", "CONTENT", "JOB", "STAGE", "TRANSFORMER"];

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

	Utilities.waitForElement('#designer--timer', 5000).then(() => {
		const timerElement = document.getElementById('designer--timer');
		_timer = new Timer(timerElement);
	});
})();
