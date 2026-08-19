using G4.Api;
using G4.Attributes;
using G4.Cache;
using G4.Models;
using G4.Models.Schema;
using G4.Services.Domain.V4.Models;
using G4.Services.Domain.V4.Repositories;
using G4.Settings;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace G4.Services.UnitTests.Repositories
{
    [TestClass]
    [DoNotParallelize]
    [TestCategory("ToolsRepository")]
    [TestCategory("UnitTest")]
    public class ToolsRepositoryTests
    {
        // Identifies the cache bucket that contributes capabilities to the domain tool catalog.
        private const string PluginType = "Action";

        // Supplies one unique lexical signal shared by the retrieval boundary tests.
        private const string RetrievalPhrase = "domainlexicalsentinel";

        [TestMethod(DisplayName = "Verify that buffer response entries serialize their timestamp and rule content.")]
        public void BufferResponseModelSerializationTest()
        {
            // Arrange: create one deterministic buffer entry with both values that disappeared from tuple serialization.
            const long Timestamp = 1700000000000;
            var response = new BufferResponseModel.BufferItem
            {
                Timestamp = Timestamp,
                Rule = new ActionRuleModel("NoAction")
            };

            // Act: serialize through the application options used by both MCP response boundaries.
            var json = JsonSerializer.SerializeToElement(new[] { response }, AppSettings.JsonOptions);
            var entry = json[0];

            // Assert: the property-backed model exposes both values instead of producing an empty JSON object.
            Assert.AreEqual(Timestamp, entry.GetProperty("timestamp").GetInt64());
            Assert.AreEqual("NoAction", entry.GetProperty("rule").GetProperty("pluginName").GetString());
        }

        [TestMethod(DisplayName = "Verify that an added cache capability appears in the domain tool catalog.")]
        public void CacheAdditionRefreshesToolsTest()
        {
            // Arrange: initialize one repository before registering a capability in its authoritative cache.
            const string Key = "UnitTestDomainAddition";
            var (cacheManager, repository) = NewContext();

            // Act: add one model through CacheManager's event-producing operation and query the domain projection.
            cacheManager.SyncCache(NewCacheModel(Key, "domain addition phrase"));
            var tool = repository.FindTool(intent: string.Empty, toolName: Key);

            // Assert: the synchronous addition notification refreshes the formatted domain entry before returning.
            Assert.IsNotNull(tool);
            Assert.AreEqual("domain addition phrase", tool.Description);
        }

        [TestMethod(DisplayName = "Verify that direct cache mutation requires a compatibility notification to refresh domain tools.")]
        public void DirectCacheMutationRequiresNotificationTest()
        {
            // Arrange: initialize one repository before bypassing CacheManager's supported mutation operations deliberately.
            const string Key = "UnitTestDirectMutation";
            var (cacheManager, repository) = NewContext();
            var cacheModel = NewCacheModel(Key, "direct mutation phrase");

            // Act: stage the model directly and observe that the copied domain projection remains unchanged.
            cacheManager.PluginsCache[PluginType][Key] = cacheModel;
            var toolBeforeNotification = repository.FindTool(intent: string.Empty, toolName: Key);

            // Act: publish the compatibility notification required after a caller-owned direct mutation.
            cacheManager.InvokeCacheChanged(
                changeType: CacheManager.CacheChangeTypes.Added,
                entityType: PluginType,
                entityKeys: [Key]);
            var toolAfterNotification = repository.FindTool(intent: string.Empty, toolName: Key);

            // Assert: only the explicit notification refreshes the domain's derived formatted catalog.
            Assert.IsNull(toolBeforeNotification);
            Assert.IsNotNull(toolAfterNotification);
        }

        [TestMethod(DisplayName = "Verify that removing a cache capability removes it from the domain tool catalog.")]
        public void CacheRemovalRefreshesToolsTest()
        {
            // Arrange: register one capability before constructing the repository that formats the current cache generation.
            const string Key = "UnitTestDomainRemoval";
            var cacheManager = NewCacheManager();
            cacheManager.SyncCache(NewCacheModel(Key, "domain removal phrase"));
            var repository = NewRepository(cacheManager);

            // Act: remove the authoritative model through the event-producing operation and query the domain projection.
            cacheManager.RemoveCacheEntity(PluginType, Key);
            var tool = repository.FindTool(intent: string.Empty, toolName: Key);

            // Assert: the completed removal notification eliminates the formatted tool before the mutation call returns.
            Assert.IsNull(tool);
        }

        [TestMethod(DisplayName = "Verify that a cache reset rebuilds the domain tool catalog from authoritative entries.")]
        public void CacheResetRebuildsToolsTest()
        {
            // Arrange: initialize an empty domain projection before staging a complete replacement cache generation.
            const string Key = "UnitTestDomainReset";
            var (cacheManager, repository) = NewContext();
            cacheManager.PluginsCache[PluginType][Key] = NewCacheModel(Key, "domain reset phrase");

            // Act: publish the reset that marks the staged cache generation authoritative.
            cacheManager.InvokeCacheChanged(
                changeType: CacheManager.CacheChangeTypes.Reset,
                entityType: string.Empty,
                entityKeys: []);
            var tool = repository.FindTool(intent: string.Empty, toolName: Key);

            // Assert: reset rebuilds the domain projection from the currently authoritative cache contents.
            Assert.IsNotNull(tool);
            Assert.AreEqual("domain reset phrase", tool.Description);
        }

        [TestMethod(DisplayName = "Verify that repeated tool synchronization retains the repository lexical manager.")]
        public void SyncToolsRetainsLexicalManagerTest()
        {
            // Arrange: capture the application-scoped lexical manager created with the singleton-style repository.
            var (_, repository) = NewContext();
            var initialManager = GetRetrievalManager(repository);

            // Act: rebuild the formatted tool catalog repeatedly through its public compatibility operation.
            repository.SyncTools();
            repository.SyncTools();
            var currentManager = GetRetrievalManager(repository);

            // Assert: formatted catalog rebuilds never replace or multiply the cache-subscribed lexical manager.
            Assert.AreSame(initialManager, currentManager);
        }

        [TestMethod(DisplayName = "Verify that updating a cache capability replaces its domain tool metadata.")]
        public void CacheUpdateRefreshesToolsTest()
        {
            // Arrange: register one stable identity before constructing its domain projection.
            const string Key = "UnitTestDomainUpdate";
            var cacheManager = NewCacheManager();
            cacheManager.SyncCache(NewCacheModel(Key, "initial domain phrase"));
            var repository = NewRepository(cacheManager);

            // Act: replace the same namespace-plus-key identity and query the formatted entry after notification.
            cacheManager.SyncCache(NewCacheModel(Key, "updated domain phrase"));
            var tool = repository.FindTool(intent: string.Empty, toolName: Key);

            // Assert: the domain catalog exposes only the current metadata for the stable cache identity.
            Assert.IsNotNull(tool);
            Assert.AreEqual("updated domain phrase", tool.Description);
        }

        [TestMethod(DisplayName = "Verify that MCP tool discovery returns the requested ten scored results.")]
        public void CallToolFindToolsHonorsTakeTest()
        {
            // Arrange: create more positively matching capabilities than the requested MCP result limit.
            var repository = NewRetrievalRepository(toolCount: 12);
            var parameters = NewFindToolsParameters(RetrievalPhrase, take: 10);
            var expected = GetRetrievalManager(repository).FindTools(RetrievalPhrase, take: 10);

            // Act: invoke the same FindTools boundary used by the MCP controller.
            var actual = (LexicalRetrievalManager.ScoresResultModel)repository.CallTool(parameters);

            // Assert: the MCP boundary preserves the requested count, ranking, and scored response shape.
            Assert.AreEqual(10, actual.Tools.Length);
            CollectionAssert.AreEqual(
                expected.Tools.Select(tool => $"{tool.Name}:{tool.Score}").ToArray(),
                actual.Tools.Select(tool => $"{tool.Name}:{tool.Score}").ToArray());
        }

        [TestMethod(DisplayName = "Verify that MCP tool discovery defaults a missing result limit to three.")]
        public void CallToolFindToolsMissingTakeDefaultsTest()
        {
            // Arrange: create enough positively matching capabilities to expose the default result limit.
            var repository = NewRetrievalRepository(toolCount: 12);
            var parameters = NewFindToolsParameters(RetrievalPhrase);

            // Act: invoke FindTools without a take argument.
            var actual = (LexicalRetrievalManager.ScoresResultModel)repository.CallTool(parameters);

            // Assert: the established default returns three scored matches.
            Assert.AreEqual(3, actual.Tools.Length);
        }

        [TestMethod(DisplayName = "Verify that MCP tool discovery defaults an invalid result limit to three.")]
        public void CallToolFindToolsInvalidTakeDefaultsTest()
        {
            // Arrange: provide a non-numeric result limit against a catalog with enough positive matches.
            var repository = NewRetrievalRepository(toolCount: 12);
            var parameters = NewFindToolsParameters(RetrievalPhrase, take: "ten");

            // Act: invoke FindTools with the invalid take argument.
            var actual = (LexicalRetrievalManager.ScoresResultModel)repository.CallTool(parameters);

            // Assert: invalid input cannot replace the established three-result default.
            Assert.AreEqual(3, actual.Tools.Length);
        }

        [TestMethod(DisplayName = "Verify that MCP tool discovery defaults non-positive result limits to three.")]
        public void CallToolFindToolsNonPositiveTakeDefaultsTest()
        {
            // Arrange: create a matching catalog and both non-positive integer variants.
            var repository = NewRetrievalRepository(toolCount: 12);
            var zeroParameters = NewFindToolsParameters(RetrievalPhrase, take: 0);
            var negativeParameters = NewFindToolsParameters(RetrievalPhrase, take: -1);

            // Act: invoke FindTools once for each non-positive limit.
            var zeroResult = (LexicalRetrievalManager.ScoresResultModel)repository.CallTool(zeroParameters);
            var negativeResult = (LexicalRetrievalManager.ScoresResultModel)repository.CallTool(negativeParameters);

            // Assert: both invalid integer variants retain the established three-result default.
            Assert.AreEqual(3, zeroResult.Tools.Length);
            Assert.AreEqual(3, negativeResult.Tools.Length);
        }

        [TestMethod(DisplayName = "Verify that MCP tool discovery returns only positive matches below the requested limit.")]
        public void CallToolFindToolsReturnsAvailableMatchesTest()
        {
            // Arrange: create fewer positively matching capabilities than the requested MCP result limit.
            var repository = NewRetrievalRepository(toolCount: 4);
            var parameters = NewFindToolsParameters(RetrievalPhrase, take: 10);

            // Act: request more results than the lexical catalog can positively match.
            var actual = (LexicalRetrievalManager.ScoresResultModel)repository.CallTool(parameters);

            // Assert: retrieval returns every positive match without padding the result.
            Assert.AreEqual(4, actual.Tools.Length);
            Assert.IsTrue(actual.Tools.All(tool => tool.Score > 0));
        }

        // Reads the private lifecycle dependency so tests can prove SyncTools never replaces it.
        private static LexicalRetrievalManager GetRetrievalManager(ToolsRepository repository)
        {
            // Resolve the declared field from the concrete repository rather than its public abstraction.
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var field = typeof(ToolsRepository).GetField("_retrievalManager", Flags);

            // Assert the known repository structure before converting the lifecycle dependency to its concrete type.
            Assert.IsNotNull(field);
            return (LexicalRetrievalManager)field.GetValue(repository);
        }

        // Creates an isolated cache and its repository so direct-event tests share one assigned application instance.
        private static (CacheManager CacheManager, ToolsRepository Repository) NewContext()
        {
            // Construct the repository with the same cache supplied to its G4 client dependency.
            var cacheManager = NewCacheManager();
            var repository = NewRepository(cacheManager);
            return (cacheManager, repository);
        }

        // Creates an isolated cache whose Action contents are owned entirely by one test.
        private static CacheManager NewCacheManager()
        {
            // Preserve CacheManager event behavior while removing environment-derived Action capabilities.
            var cacheManager = new CacheManager();
            cacheManager.PluginsCache[PluginType] =
                new ConcurrentDictionary<string, G4PluginCacheModel>(StringComparer.OrdinalIgnoreCase);
            return cacheManager;
        }

        // Creates a manifest-backed capability that can populate both domain and lexical projections.
        private static G4PluginCacheModel NewCacheModel(string key, string description)
        {
            // Include deterministic example and summary text used by the respective derived catalogs.
            var example = new PluginExampleModel
            {
                Context = new Dictionary<string, object>
                {
                    ["labels"] = new[] { description }
                },
                Description = [description]
            };
            return new G4PluginCacheModel
            {
                Manifest = new G4PluginAttribute
                {
                    Examples = [example],
                    Key = key,
                    Namespace = "G4.Services.UnitTests",
                    PluginType = PluginType,
                    Summary = [description]
                }
            };
        }

        // Creates one MCP FindTools payload with no explicit result limit.
        private static JsonElement NewFindToolsParameters(string intent)
        {
            // Delegate to the shared payload builder while omitting the optional take property.
            return NewFindToolsParameters(intent, take: null);
        }

        // Creates one MCP FindTools payload with a caller-supplied result limit value.
        private static JsonElement NewFindToolsParameters(string intent, object take)
        {
            // Build the arguments separately so tests can omit take or provide invalid JSON-compatible values.
            var arguments = new Dictionary<string, object>
            {
                ["intent"] = new IntentModel
                {
                    AgentIntent = intent,
                    UserIntent = intent
                }
            };

            // Include the optional property only when the test supplies a value.
            if (take != null)
            {
                arguments["take"] = take;
            }

            // Serialize through application options so the test uses the production JSON naming contract.
            return JsonSerializer.SerializeToElement(
                new Dictionary<string, object>
                {
                    ["name"] = "g4.FindTools",
                    ["arguments"] = arguments
                },
                AppSettings.JsonOptions);
        }

        // Creates a repository whose Action catalog contains the requested number of positive lexical matches.
        private static ToolsRepository NewRetrievalRepository(int toolCount)
        {
            // Populate the authoritative cache before repository construction initializes both tool projections.
            var cacheManager = NewCacheManager();
            for (var index = 0; index < toolCount; index++)
            {
                cacheManager.SyncCache(NewCacheModel($"UnitTestRetrieval{index:D2}", RetrievalPhrase));
            }

            // Bind the repository and lexical manager to the populated cache.
            return NewRepository(cacheManager);
        }

        // Creates a domain repository and G4 client bound to the same authoritative cache instance.
        private static ToolsRepository NewRepository(CacheManager cacheManager)
        {
            // Supply an inert HTTP client because catalog synchronization performs no network operations.
            return new ToolsRepository(
                clientFactory: new TestHttpClientFactory(),
                cache: cacheManager,
                client: new G4Client(cacheManager));
        }

        // Supplies the named client dependency without performing external network operations.
        private sealed class TestHttpClientFactory : IHttpClientFactory
        {
            // Creates an unconfigured client because repository construction only stores the instance.
            public HttpClient CreateClient(string name)
            {
                return new HttpClient();
            }
        }
    }
}
