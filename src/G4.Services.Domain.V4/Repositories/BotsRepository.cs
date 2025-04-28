using G4.Models;

using LiteDB;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Repository for managing <see cref="ConnectedBotModel"/> instances:
    /// handles persistence in LiteDB, in-memory caching, and HTTP callbacks to bots.
    /// Implements <see cref="IBotsRepository"/>.
    /// </summary>
    /// <param name="clientFactory">Factory to create <see cref="HttpClient"/> instances with named configurations.</param>
    /// <param name="liteDatabase">LiteDB instance used for persistent storage of bot records.</param>
    /// <param name="connectedBots">Thread-safe in-memory cache of bots keyed by their <c>Id</c>.</param>
    public class BotsRepository(
        IHttpClientFactory clientFactory,
        ILiteDatabase liteDatabase,
        ConcurrentDictionary<string, ConnectedBotModel> connectedBots) : IBotsRepository
    {
        #region *** Fields     ***
        // The LiteDB collection name where ConnectedBotModel documents are stored.
        private const string CollectionName = "ConnectedBots";

        // HttpClient configured for bot callbacks, created via IHttpClientFactory with name "bots-callback".
        private readonly HttpClient _httpClient = clientFactory.CreateClient(name: "bots-callback");

        // LiteDB database instance for performing upsert, delete, and query operations on bots.
        private readonly ILiteDatabase _liteDatabase = liteDatabase;
        #endregion

        #region *** Properties ***
        public ConcurrentDictionary<string, ConnectedBotModel> ConnectedBots { get; } = connectedBots;
        #endregion

        #region *** Methods    ***
        /// <inheritdoc />
        public IEnumerable<ConnectedBotModel> GetStatus()
        {
            // Gather all IDs from the in-memory dictionary
            var connectedIds = ConnectedBots.Keys.ToArray();

            // Query the database for any stored bot IDs
            var storedIds = _liteDatabase
                .GetCollection<ConnectedBotModel>(CollectionName)
                .Query()
                .Select(i => i.Id)
                .ToArray();

            // Combine both sets of IDs, remove duplicates, and build the final list to process
            var ids = connectedIds.Concat(storedIds).Distinct().ToArray();

            // Delegate to the bulk-GetStatus method for actual retrieval
            return GetStatus(ids);
        }

        /// <inheritdoc />
        public IEnumerable<ConnectedBotModel> GetStatus(string[] ids)
        {
            // For each ID, attempt to get its ConnectedBotModel (null if not found),
            // then filter out any null results, keeping only successful lookups
            var bots = ids.Select(GetStatus).Where(bot => bot is not null);

            // Return all found bot models
            return [.. bots];
        }

        /// <inheritdoc />
        public ConnectedBotModel GetStatus(string id)
        {
            // Attempt to retrieve the bot from the in-memory dictionary
            return GetBot(ConnectedBots, _liteDatabase, id);
        }

        /// <inheritdoc />
        public ConnectedBotModel Register(ConnectedBotModel botModel)
        {
            // Determine the bot Id: use provided Id or generate one based on the current UTC timestamp
            var id = string.IsNullOrEmpty(botModel.Id)
                ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
                : botModel.Id;

            // Parse the callback URI to extract host and port for callback routing
            var uri = new Uri(botModel.CallbackUri);
            var callbackHost = uri.Host;
            var callbackPort = uri.Port;

            // Build a new ConnectedBotModel with all required fields populated
            var connectedBot = new ConnectedBotModel
            {
                CallbackHost = callbackHost,
                CallbackPort = callbackPort,
                CallbackUri = botModel.CallbackUri,
                ConnectionId = string.Empty,
                CreatedOn = DateTime.UtcNow,
                Id = id,
                LastModifiedOn = DateTime.UtcNow,
                Machine = botModel.Machine,
                Name = botModel.Name,
                OsVersion = botModel.OsVersion,
                Status = "Ready",
                Type = botModel.Type
            };

            // Upsert the bot record into the LiteDB collection (insert or update)
            _liteDatabase
                .GetCollection<ConnectedBotModel>(CollectionName)
                .Upsert(connectedBot);

            // Keep the in-memory lookup in sync by adding or replacing the entry
            ConnectedBots[id] = connectedBot;

            // Return the newly registered or updated bot model
            return connectedBot;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<(int StatusCode, ConnectedBotModel ConnectedBot)>> TestConnection()
        {
            // Gather all IDs currently tracked in the in-memory dictionary
            var connectedIds = ConnectedBots.Keys.ToArray();

            // Query the database collection for any persisted bot IDs
            var storedIds = _liteDatabase
                .GetCollection<ConnectedBotModel>(CollectionName)
                .Query()
                .Select(i => i.Id)
                .ToArray();

            // Combine in-memory and stored IDs, eliminate duplicates, and prepare the final list
            var ids = connectedIds
                .Concat(storedIds)
                .Distinct()
                .ToArray();

            // Delegate the actual connectivity checks to the overload that accepts an array of IDs
            return await TestConnection(ids);
        }

        // TODO: Implement parallel processing for multiple IDs
        /// <inheritdoc />
        public async Task<IEnumerable<(int StatusCode, ConnectedBotModel ConnectedBot)>> TestConnection(string[] ids)
        {
            // Create a list to accumulate results for each bot ID
            var bots = new List<(int StatusCode, ConnectedBotModel ConnectedBot)>();

            // Iterate over each provided identifier
            foreach (var id in ids)
            {
                // Invoke the single-ID TestConnection overload and await its result
                var (statusCode, connectedBot) = await TestConnection(id);

                // Store the outcome tuple in our result list
                bots.Add((statusCode, connectedBot));
            }

            // Return the full set of connectivity results
            return bots;
        }

        /// <inheritdoc />
        public async Task<(int StatusCode, ConnectedBotModel ConnectedBot)> TestConnection(string id)
        {
            // Retrieve the bot from cache or fallback to database
            var connectedBot = GetBot(ConnectedBots, _liteDatabase, id);

            // If the bot isn’t found, return 404 Not Found
            if (connectedBot == null)
            {
                return (StatusCode: 404, ConnectedBot: null);
            }

            // Determine if the bot already has an active SignalR connection
            var isConnected = !string.IsNullOrEmpty(connectedBot.ConnectionId);
            if (isConnected)
            {
                // Bot is connected via SignalR; the real-time channel will handle status updates
                return (StatusCode: 200, ConnectedBot: connectedBot);
            }

            try
            {
                // Build an HTTP GET request to the bot's callback endpoint
                using var request = new HttpRequestMessage(
                    method: HttpMethod.Get,
                    requestUri: connectedBot.CallbackUri);

                // Send the request to the bot and await its response
                using var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    // On HTTP 2xx response, check if the bot is already Ready or Working
                    var isReady = connectedBot.Status.Equals("Ready", StringComparison.OrdinalIgnoreCase);
                    var isWorking = connectedBot.Status.Equals("Working", StringComparison.OrdinalIgnoreCase);

                    // On HTTP 2xx, mark the bot Ready if it wasn’t already Ready or Working
                    connectedBot.Status = isReady || isWorking
                        ? connectedBot.Status
                        : "Ready";
                }
                else
                {
                    // Any non-2xx response code means the bot is offline or unreachable
                    connectedBot.Status = "Offline";
                }

                // Return 200 OK if reachable, otherwise 410 Gone
                return response.IsSuccessStatusCode
                    ? (StatusCode: 200, ConnectedBot: connectedBot)
                    : (StatusCode: 502, ConnectedBot: connectedBot);
            }
            catch
            {
                // On exception (network failure, timeout, etc.), mark offline and return 410 Gone
                connectedBot.Status = "Offline";
                return (StatusCode: 502, ConnectedBot: connectedBot);
            }
            finally
            {
                // Update the in-memory cache with the new status
                ConnectedBots[id] = connectedBot;

                // Persist the updated status back to the LiteDB collection
                _liteDatabase
                    .GetCollection<ConnectedBotModel>(CollectionName)
                    .Upsert(connectedBot);
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<(int StatusCode, ConnectedBotModel ConnectedBot)>> Unregister()
        {
            // Gather all IDs from the in-memory dictionary
            var connectedIds = ConnectedBots.Keys.ToArray();

            // Query the database for any stored bot IDs
            var storedIds = _liteDatabase
                .GetCollection<ConnectedBotModel>(CollectionName)
                .Query()
                .Select(i => i.Id)
                .ToArray();

            // Combine both sets of IDs, remove duplicates, and build the final list to process
            var ids = connectedIds.Concat(storedIds).Distinct().ToArray();

            // Delegate to the bulk-Unregister method for actual removal and callback
            return await Unregister(ids);
        }

        // TODO: Implement parallel processing for multiple IDs
        /// <inheritdoc />
        public async Task<IEnumerable<(int StatusCode, ConnectedBotModel ConnectedBot)>> Unregister(string[] ids)
        {
            // Prepare a list to collect results for each ID
            var bots = new List<(int StatusCode, ConnectedBotModel ConnectedBot)>();

            // Process each provided bot identifier
            foreach (var id in ids)
            {
                // Invoke the single-ID unregister method and capture its outcome
                var (statusCode, connectedBot) = await Unregister(id);

                // Add the result tuple to our collection
                bots.Add((statusCode, connectedBot));
            }

            // Return all accumulated results
            return bots;
        }

        /// <inheritdoc />
        public async Task<(int StatusCode, ConnectedBotModel ConnectedBot)> Unregister(string id)
        {
            // Attempt to remove the bot from the in-memory dictionary and database
            static void RemoveBot(string id, ConcurrentDictionary<string, ConnectedBotModel> connectedBots, ILiteDatabase liteDatabase)
            {
                // Remove the bot from the in-memory dictionary
                connectedBots.TryRemove(id, out _);

                // Remove the bot document from the LiteDB collection
                liteDatabase
                    .GetCollection<ConnectedBotModel>(CollectionName)
                    .Delete(id);
            }

            // Attempt to retrieve the bot from the in-memory dictionary or database collection
            var connectedBot = GetBot(ConnectedBots, _liteDatabase, id);

            // Attempt to retrieve the bot from the in-memory cache
            if (connectedBot == null)
            {
                // Bot not found → return 404 Not Found with no model
                return (StatusCode: 404, ConnectedBot: new ConnectedBotModel
                {
                    Id = id,
                    Status = "NotFound"
                });
            }

            // If the bot has an active socket connection, do not proceed
            if (!string.IsNullOrEmpty(connectedBot.ConnectionId))
            {
                // Conflict: the bot is still connected and must be disconnected first
                return (StatusCode: 409, ConnectedBot: connectedBot);
            }

            try
            {
                // Build an HTTP DELETE request to the bot's callback URI
                using var request = new HttpRequestMessage(
                    method: HttpMethod.Delete,
                    requestUri: connectedBot.CallbackUri);

                // Send HTTP DELETE request to the bot's callback URI to notify it of unregistration
                var response = await _httpClient.SendAsync(request);

                // Throw if the response indicates failure (4xx or 5xx)
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                // On error, update bot status with the error message and return 500 Internal Server Error
                connectedBot.Status = $"Error; {e.GetBaseException().Message}";

                // Remove the bot from the in-memory dictionary and from the LiteDB collection
                RemoveBot(id, ConnectedBots, _liteDatabase);

                // Return 500 Internal Server Error with the updated bot model
                return (StatusCode: 502, ConnectedBot: connectedBot);
            }

            // Remove the bot from the in-memory dictionary and from the LiteDB collection
            RemoveBot(id, ConnectedBots, _liteDatabase);

            // Successful deletion → return 200 OK with the removed bot model
            return (StatusCode: 200, ConnectedBot: connectedBot);
        }

        /// <inheritdoc />
        public (int StatusCode, ConnectedBotModel ConnectedBot) Update(string id, ConnectedBotModel botModel)
        {
            // Attempt to retrieve the existing bot from in-memory cache or fallback to the database
            var connectedBot = GetBot(ConnectedBots, _liteDatabase, id);

            // If no bot was found with the given id, return 404 Not Found
            if (connectedBot == null)
            {
                return (StatusCode: 404, ConnectedBot: null);
            }

            // Apply incoming changes
            connectedBot.Status = botModel.Status;
            connectedBot.LastModifiedOn = DateTime.UtcNow;

            // Persist the updated bot back to the LiteDB collection (upsert = insert or update)
            _liteDatabase
                .GetCollection<ConnectedBotModel>(CollectionName)
                .Upsert(connectedBot);

            // Sync the in-memory dictionary with the latest model
            ConnectedBots[id] = connectedBot;

            // Return 200 OK to indicate a successful update with no response body
            return (StatusCode: 200, connectedBot);
        }

        // Retrieves a connected bot by its identifier, first checking the in-memory cache, then falling back to the database if needed.
        private static ConnectedBotModel GetBot(
            ConcurrentDictionary<string, ConnectedBotModel> connectedBots, ILiteDatabase liteDatabase, string id)
        {
            // Try to get the bot from the in-memory dictionary
            var isConnected = connectedBots.TryGetValue(id, out var connectedBot);

            // If found in memory, return it; otherwise query the LiteDB collection for a matching Id
            return isConnected
                ? connectedBot
                : liteDatabase.GetCollection<ConnectedBotModel>(CollectionName).Find(i => i.Id == id).FirstOrDefault();
        }
        #endregion
    }
}
