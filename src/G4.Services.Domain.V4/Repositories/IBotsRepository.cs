using G4.Models;

using LiteDB;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Defines the contract for a repository that manages connected bot instances.
    /// </summary>
    public interface IBotsRepository
    {
        #region *** Static Methods ***
        /// <summary>
        /// Loads all ConnectedBotModel entries from the LiteDB database into a thread-safe dictionary.
        /// </summary>
        /// <param name="liteDatabase">An <see cref="ILiteDatabase"/> instance representing the database to read from. May be null if the database is not available; in that case, an empty dictionary is returned.</param>
        /// <param name="logger">An <see cref="ILogger"/> used to record warnings if loading fails. May be null.</param>
        /// <returns>A <see cref="ConcurrentDictionary{TKey,TValue}"/> mapping each bot's <c>Id</c> to its <see cref="ConnectedBotModel"/>.</returns>
        public static ConcurrentDictionary<string, ConnectedBotModel> InitializeConnectedBots(
            ILiteDatabase liteDatabase,
            ILogger logger)
        {
            // Prepare an empty, case-insensitive dictionary for storing bots
            var connectedBots = new ConcurrentDictionary<string, ConnectedBotModel>(StringComparer.OrdinalIgnoreCase);
            const string collectionName = "ConnectedBots";

            // If the database is null or the collection doesn't exist, bail out with an empty dictionary
            if (liteDatabase?.CollectionExists(collectionName) != true)
            {
                return connectedBots;
            }

            try
            {
                // Retrieve the collection from the database
                var connectedBotsCollection = liteDatabase.GetCollection<ConnectedBotModel>(collectionName);

                // Iterate every document and upsert into our dictionary by its Id
                foreach (var bot in connectedBotsCollection.FindAll())
                {
                    connectedBots[bot.Id] = bot;
                }
            }
            catch (Exception e)
            {
                // Log a warning if anything goes wrong reading from the database
                logger?.LogWarning(
                    exception: e,
                    message: "Failed to load ConnectedBots from database '{CollectionName}'.",
                    nameof(collectionName));
            }

            // Return the populated (or empty) dictionary
            return connectedBots;
        }
        #endregion

        #region *** Properties     ***
        /// <summary>
        /// Gets or sets the thread-safe collection of currently connected bot instances,
        /// keyed by their SignalR connection IDs.
        /// </summary>
        ConcurrentDictionary<string, ConnectedBotModel> ConnectedBots { get; }
        #endregion

        #region *** Methods        ***
        /// <summary>
        /// Retrieves all currently connected bots from the in-memory cache.
        /// </summary>
        /// <returns>A sequence of <see cref="ConnectedBotModel"/> instances representing every bot currently connected in memory.</returns>
        IEnumerable<ConnectedBotModel> GetStatus();

        /// <summary>
        /// Retrieves the statuses for a specific set of bot IDs.
        /// </summary>
        /// <param name="ids">An array of bot identifiers to look up.</param>
        /// <returns>A sequence of <see cref="ConnectedBotModel"/> instances for each ID that was found in memory; IDs not found are omitted.</returns>
        IEnumerable<ConnectedBotModel> GetStatus(string[] ids);

        /// <summary>
        /// Retrieves the status of a single bot by its ID from the in-memory cache.
        /// </summary>
        /// <param name="id">The unique identifier of the bot to retrieve.</param>
        /// <returns>The <see cref="ConnectedBotModel"/> for the given ID if present in memory; otherwise the default value (null) if not found.</returns>
        ConnectedBotModel GetStatus(string id);

        /// <summary>
        /// Registers a new bot or updates an existing one in the database and in-memory dictionary.
        /// </summary>
        /// <param name="botModel">The incoming bot data containing at least a CallbackUri and other identifying properties.</param>
        /// <returns>The <see cref="ConnectedBotModel"/> instance that was saved, with populated metadata fields.</returns>
        /// <remarks>If <c>botModel.Id</c> is null or empty, a new unique Id will be generated.</remarks>
        ConnectedBotModel Register(ConnectedBotModel botModel);

        /// <summary>
        /// Tests connectivity for all registered bots by aggregating IDs from both in-memory cache and persistent storage.
        /// </summary>
        /// <returns>
        /// A collection of tuples for each bot ID processed:
        /// <list type="bullet">
        ///   <item><description><c>StatusCode</c>: HTTP-style status code indicating reachability (e.g., 200, 404, 410).</description></item>
        ///   <item><description><c>ConnectedBot</c>: The <see cref="ConnectedBotModel"/> with its updated status, or <c>null</c> if not found.</description></item>
        /// </list>
        /// </returns>
        Task<IEnumerable<(int StatusCode, ConnectedBotModel ConnectedBot)>> TestConnection();

        /// <summary>
        /// Tests connectivity for multiple bots by invoking the single-ID <see cref="TestConnection(string)"/> method for each identifier.
        /// </summary>
        /// <param name="ids">An array of bot IDs to test connectivity for.</param>
        /// <returns>
        /// A collection of tuples, each containing:
        /// <list type="bullet">
        ///   <item><description><c>StatusCode</c>: HTTP-style status code result for that bot (e.g., 200, 404, 410).</description></item>
        ///   <item><description><c>ConnectedBot</c>: The <see cref="ConnectedBotModel"/> with its updated status, or <c>null</c> if not found.</description></item>
        /// </list>
        /// </returns>
        Task<IEnumerable<(int StatusCode, ConnectedBotModel ConnectedBot)>> TestConnection(string[] ids);

        /// <summary>
        /// Tests the connectivity of a registered bot by either checking its SignalR connection or sending an HTTP GET to its callback URI.
        /// </summary>
        /// <param name="id">The unique identifier of the bot to test.</param>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///   <item><description><c>StatusCode</c>: HTTP-style status code (200 if reachable or already connected, 410 if unreachable, 404 if not found).</description></item>
        ///   <item><description><c>ConnectedBot</c>: The <see cref="ConnectedBotModel"/> with its updated status, or <c>null</c> if not found.</description></item>
        /// </list>
        /// </returns>
        Task<(int StatusCode, ConnectedBotModel ConnectedBot)> TestConnection(string id);

        /// <summary>
        /// Unregisters **all** bots known in memory or persistent store, invoking the single-ID <see cref="Unregister(string[])"/> overload.
        /// </summary>
        /// <returns>
        /// A collection of tuples for each bot ID processed:
        /// <list type="bullet">
        ///   <item><description><c>StatusCode</c>: HTTP-style status code indicating the result (e.g., 204, 404, 409, 500).</description></item>
        ///   <item><description><c>ConnectedBot</c>: The <see cref="ConnectedBotModel"/> involved, or <c>null</c> if not found.</description></item>
        /// </list>
        /// </returns>
        Task<IEnumerable<(int StatusCode, ConnectedBotModel ConnectedBot)>> Unregister();

        /// <summary>
        /// Unregisters a set of bots by their identifiers by invoking the single-ID <see cref="Unregister(string)"/> method for each.
        /// </summary>
        /// <param name="ids">An array of bot IDs to unregister.</param>
        /// <returns>
        /// A collection of tuples, each containing:
        /// <list type="bullet">
        ///   <item><description><c>StatusCode</c>: HTTP-style status code indicating the result for that ID (e.g. 204, 404, 409, 500).</description></item>
        ///   <item><description><c>ConnectedBot</c>: The associated <see cref="ConnectedBotModel"/> instance, or <c>null</c> if not found.</description></item>
        /// </list>
        /// </returns>
        Task<IEnumerable<(int StatusCode, ConnectedBotModel ConnectedBot)>> Unregister(string[] ids);

        /// <summary>
        /// Unregisters a bot by its identifier: sends a DELETE callback, removes it from in-memory store and database.
        /// </summary>
        /// <param name="id">The unique identifier of the bot to unregister.</param>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///   <item><description><c>StatusCode</c>: HTTP-like status code indicating outcome (404 if not found, 409 if still connected, 500 on callback failure, 204 on success).</description></item>
        ///   <item><description><c>ConnectedBot</c>: The bot model involved in the operation, or <c>null</c> if none was found.</description></item>
        /// </list>
        /// </returns>
        Task<(int StatusCode, ConnectedBotModel ConnectedBot)> Unregister(string id);

        /// <summary>
        /// Updates the status of an existing connected bot in both the database and in-memory store.
        /// </summary>
        /// <param name="id">The unique identifier of the bot to update.</param>
        /// <param name="botModel">A model containing the new values to apply (e.g. <see cref="ConnectedBotModel.Status"/>).</param>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///   <item><description><c>StatusCode</c>: HTTP-style status code (404 if not found, 204 on success).</description></item>
        ///   <item><description><c>ConnectedBot</c>: The updated <see cref="ConnectedBotModel"/>, or <c>null</c> if not found.</description></item>
        /// </list>
        /// </returns>
        (int StatusCode, ConnectedBotModel ConnectedBot) Update(string id, ConnectedBotModel botModel);
        #endregion
    }
}
