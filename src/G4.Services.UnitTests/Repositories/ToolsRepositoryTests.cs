using G4.Api;
using G4.Attributes;
using G4.Cache;
using G4.Models;
using G4.Models.Schema;
using G4.Services.Domain.V4.Mcp;
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
using System.Text.Json.Nodes;

namespace G4.Services.UnitTests.Repositories
{
    [TestClass]
    [DoNotParallelize]
    [TestCategory("ToolsRepository")]
    [TestCategory("UnitTest")]
    public class ToolsRepositoryTests
    {
        // Provides a second namespace that shares plugin keys with the default test namespace.
        private const string OtherNamespace = "G4.Services.UnitTests.Other";

        // Provides the default namespace of the manifest-backed test capabilities.
        private const string PluginNamespace = "G4.Services.UnitTests";

        // Identifies the cache bucket that contributes capabilities to the domain tool catalog.
        private const string PluginType = "Action";

        // Supplies one unique lexical signal shared by the retrieval boundary tests.
        private const string RetrievalPhrase = "domainlexicalsentinel";

        // Supplies the keyboard chord that the rule conversion tests expand into repeated G4 switches.
        private static readonly string[] s_chord = ["Ctrl", "A"];

        // Supplies the catalog type filter that selects the built-in system tools.
        private static readonly string[] s_systemToolTypes = ["system-tool"];

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

        [TestMethod(DisplayName = "Verify that rule conversion preserves an explicitly supplied G4 argument.")]
        public void ConvertRuleDataPreservesExplicitArgumentTest()
        {
            // Arrange: provide both the authoritative expression and structured parameters that previously replaced it.
            const string Argument = "{{$ --Key:Ctrl --Key:A --Sticky}}";
            var ruleData = JsonSerializer.SerializeToElement(new
            {
                toolParameters = new
                {
                    Key = s_chord,
                    Sticky = string.Empty
                },
                toolProperties = new
                {
                    argument = Argument
                }
            });

            // Act: convert through the shared path used by SendRule and SendRules.
            var rule = ConvertRuleData(ruleData);

            // Assert: structured parameters cannot rewrite a valid caller-supplied G4 expression.
            Assert.AreEqual(Argument, rule.Argument);
        }

        [TestMethod(DisplayName = "Verify that rule conversion expands array parameters into repeated G4 switches.")]
        public void ConvertRuleDataExpandsArrayParametersTest()
        {
            // Arrange: omit the expression so conversion must synthesize it from structured parameters.
            var ruleData = JsonSerializer.SerializeToElement(new
            {
                toolParameters = new
                {
                    Key = s_chord,
                    Sticky = string.Empty
                },
                toolProperties = new { }
            });

            // Act: convert through the shared fallback formatter.
            var rule = ConvertRuleData(ruleData);

            // Assert: every array item becomes its own switch and scalar flag behavior remains unchanged.
            Assert.AreEqual("{{$ --Key:Ctrl --Key:A --Sticky}}", rule.Argument);
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
            Assert.AreEqual("G4.Services.UnitTests", tool.Namespace);
        }

        [TestMethod(DisplayName = "Verify that FindTool returns the complete tool model with its authoritative namespace.")]
        public void CallToolFindToolReturnsCompleteModelTest()
        {
            // Arrange: register one manifest-backed tool with a deterministic namespace.
            const string Key = "UnitTestFindToolModel";
            var (cacheManager, repository) = NewContext();
            cacheManager.SyncCache(NewCacheModel(Key, "find tool model phrase"));

            // Act: invoke the same g4.FindTool boundary used by MCP clients and serialize its response.
            var result = repository.CallTool(NewFindToolParameters(Key));
            var json = JsonSerializer.SerializeToElement(result, AppSettings.JsonOptions);
            var tool = json.GetProperty("tool");

            // Assert: the authoritative model and its client definition are both returned.
            Assert.AreEqual(Key, tool.GetProperty("name").GetString());
            Assert.AreEqual("G4.Services.UnitTests", tool.GetProperty("namespace").GetString());
            Assert.AreEqual(Key, tool.GetProperty("clientTool").GetProperty("name").GetString());
            Assert.IsTrue(tool.GetProperty("clientTool").TryGetProperty("inputSchema", out _));
        }

        [TestMethod(DisplayName = "Verify that FindTool keeps tools sharing a name across namespaces and " +
            "resolves each by its qualified name.")]
        public void FindToolResolvesQualifiedNamesAcrossNamespacesTest()
        {
            // Arrange: register the same key in two namespaces.
            const string Key = "UnitTestSharedTool";
            var (cacheManager, repository) = NewContext();
            cacheManager.SyncCache(NewCacheModel(Key, "shared tool phrase", exampleCount: 1, PluginNamespace));
            cacheManager.SyncCache(NewCacheModel(Key, "shared tool phrase", exampleCount: 1, OtherNamespace));

            // Act: resolve both qualified names and the ambiguous bare name.
            var tool = repository.FindTool(intent: string.Empty, toolName: $"{PluginNamespace}:{Key}");
            var otherTool = repository.FindTool(intent: string.Empty, toolName: $"{OtherNamespace}:{Key}");
            var bareTool = repository.FindTool(intent: string.Empty, toolName: Key);

            // Assert: each qualified name selects its own namespace, and the bare name selects neither.
            Assert.AreEqual(PluginNamespace, tool?.Namespace);
            Assert.AreEqual(OtherNamespace, otherTool?.Namespace);
            Assert.IsNull(bareTool);
        }

        [TestMethod(DisplayName = "Verify that lexical tool discovery returns tools sharing a name across " +
            "namespaces as separate qualified entries.")]
        public void FindToolsKeepsToolsSharingNameTest()
        {
            // Arrange: register the same key in two namespaces with one matching phrase.
            const string Key = "UnitTestSharedLexicalTool";
            var (cacheManager, repository) = NewContext();
            cacheManager.SyncCache(NewCacheModel(Key, RetrievalPhrase, exampleCount: 1, PluginNamespace));
            cacheManager.SyncCache(NewCacheModel(Key, RetrievalPhrase, exampleCount: 1, OtherNamespace));

            // Act: discover the tools through the lexical overload.
            var tools = repository.FindTools(RetrievalPhrase, maxResults: 10);

            // Assert: both providers are returned, keyed by their qualified names.
            Assert.HasCount(2, tools);
            Assert.IsTrue(tools.ContainsKey($"{PluginNamespace}:{Key}"));
            Assert.IsTrue(tools.ContainsKey($"{OtherNamespace}:{Key}"));
        }

        #region *** Data Set ***
        [DataRow("UnitTestUniqueRule", "G4.Services.UnitTests:UnitTestUniqueRule")]
        [DataRow("G4.Services.UnitTests.Other:UnitTestSharedRule", "G4.Services.UnitTests.Other:UnitTestSharedRule")]
        [DataRow("UnitTestSharedRule", "UnitTestSharedRule")]
        #endregion
        [TestMethod(DisplayName = "Verify that rule conversion sends the qualified plugin name and passes an " +
            "ambiguous name through for the engine to reject.")]
        public void ConvertToRuleUsesQualifiedPluginNameTest(string toolName, string expected)
        {
            // Arrange: register one unique key and one key shared by two namespaces.
            var (cacheManager, _) = NewContext();
            cacheManager.SyncCache(NewCacheModel("UnitTestUniqueRule", "unique rule phrase"));
            cacheManager.SyncCache(NewCacheModel("UnitTestSharedRule", "shared rule phrase", 1, PluginNamespace));
            cacheManager.SyncCache(NewCacheModel("UnitTestSharedRule", "shared rule phrase", 1, OtherNamespace));
            var ruleData = JsonSerializer.SerializeToElement(new { toolName });

            // Act: convert the rule through the shared path used by SendRule and SendRules, over the live catalog.
            var rule = ConvertRuleData(ruleData, tools: GetTools());

            // Assert: the rule carries the resolved qualified name, or the unresolved name unchanged.
            Assert.AreEqual(expected, rule.PluginName);
        }

        [TestMethod(DisplayName = "Verify that the FindTool system definition uses the G4.System namespace and requires only a tool name.")]
        public void FindToolSystemDefinitionContractTest()
        {
            // Arrange: build the repository containing its attribute-defined system tools.
            var (_, repository) = NewContext();

            // Act: resolve the FindTool definition from the authoritative tool catalog.
            var tool = repository.FindTool(intent: string.Empty, toolName: "g4.FindTool");
            var inputSchema = tool.ClientTool.InputSchema;
            var required = inputSchema
                .GetProperty("required")
                .EnumerateArray()
                .Select(item => item.GetString())
                .ToArray();

            // Assert: callers discover the namespace from the response instead of supplying one as input.
            Assert.AreEqual("G4.System", tool.Namespace);
            Assert.AreSequenceEqual(expected: ["toolName"], actual: required);
            Assert.IsFalse(inputSchema.GetProperty("properties").TryGetProperty("namespace", out _));
            Assert.IsFalse(inputSchema.GetProperty("properties").TryGetProperty("intent", out _));
        }

        [TestMethod(DisplayName = "Verify that the system catalog entries are the SDK definitions " +
            "of the MCP attribute tools.")]
        public void SystemToolsFromAttributeDefinitionsTest()
        {
            // Arrange: build the repository and the definitions the MCP server registration advertises.
            var (_, repository) = NewContext();
            var definitions = McpToolTypes.NewDefinitions();
            var filter = JsonSerializer.SerializeToElement(new { arguments = new { types = s_systemToolTypes } });

            // Act: list the system tools exactly as the catalog type filter returns them.
            var systemTools = repository.FindTools(parameters: filter);

            // Assert: the catalog holds one G4.System entry per attribute tool, with the identical client schema.
            Assert.HasCount(expected: definitions.Length, collection: systemTools);
            foreach (var definition in definitions)
            {
                var tool = systemTools[definition.Name];
                var expected = JsonSerializer.SerializeToNode(definition.InputSchema);
                var actual = JsonSerializer.SerializeToNode(tool.ClientTool.InputSchema);
                Assert.AreEqual(expected: "G4.System", actual: tool.Namespace);
                Assert.IsTrue(condition: JsonNode.DeepEquals(node1: expected, node2: actual), message: definition.Name);
            }
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
            Assert.HasCount(expected: 10, collection: actual.Tools);
            Assert.AreSequenceEqual(
                expected: expected.Tools.Select(tool => $"{tool.Name}:{tool.Score}").ToArray(),
                actual: actual.Tools.Select(tool => $"{tool.Name}:{tool.Score}").ToArray());
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
            Assert.HasCount(expected: 3, collection: actual.Tools);
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
            Assert.HasCount(expected: 3, collection: actual.Tools);
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
            Assert.HasCount(expected: 3, collection: zeroResult.Tools);
            Assert.HasCount(expected: 3, collection: negativeResult.Tools);
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
            Assert.HasCount(expected: 4, collection: actual.Tools);
            Assert.IsTrue(actual.Tools.All(tool => tool.Score > 0));
        }

        [TestMethod(DisplayName = "Verify that ResolveTools ranks tools exactly as FindTools and pairs each with the " +
            "examples FindExamples returns for the same intent.")]
        public void CallToolResolveToolsMatchesExplicitCallsTest()
        {
            // Arrange: create more matching capabilities than the requested limit and capture the explicit ranking.
            var repository = NewRetrievalRepository(toolCount: 12);
            var parameters = NewResolveToolsParameters(RetrievalPhrase, take: 10, examplesTake: null);
            var expected = GetRetrievalManager(repository).FindTools(RetrievalPhrase, take: 10);

            // Act: resolve the tools and their examples through the single MCP boundary.
            var tools = ResolveTools(repository, parameters);

            // Assert: the tools keep the explicit ranking and carry the complete g4.FindTool model.
            Assert.AreSequenceEqual(
                expected: expected.Tools.Select(tool => $"{tool.Name}:{tool.Score}").ToArray(),
                actual: tools.Select(tool => $"{ReadToolName(tool)}:{tool.GetProperty("score").GetInt32()}").ToArray());
            Assert.IsTrue(condition: tools.All(tool => tool.GetProperty("tool").TryGetProperty("clientTool", out _)));

            // Assert: every tool's examples are identical to the explicit g4.FindExamples response.
            foreach (var tool in tools)
            {
                // Arrange: build the explicit example query for the same tool, namespace, intent, and default limit.
                var toolName = ReadToolName(tool);
                var query = new ExamplesQueryModel
                {
                    Intent = new IntentModel { AgentIntent = RetrievalPhrase },
                    MaxResults = 3,
                    Namespace = tool
                        .GetProperty("tool")
                        .GetProperty("namespace")
                        .GetString(),
                    ToolName = toolName
                };

                // Act: run the explicit lookup and read both example arrays in their serialized wire shape.
                var explicitResult = repository.FindExamples(query);
                var explicitExamples = JsonSerializer.SerializeToNode(explicitResult, options: AppSettings.JsonOptions);
                var resolvedExamples = JsonSerializer.SerializeToNode(tool.GetProperty("examples"));

                // Assert: the single call returns exactly the examples the explicit call returns.
                Assert.HasCount(expected: 1, collection: explicitExamples["examples"].AsArray());
                var isEqual = JsonNode.DeepEquals(node1: explicitExamples["examples"], node2: resolvedExamples);
                Assert.IsTrue(condition: isEqual, message: toolName);
            }
        }

        [TestMethod(DisplayName = "Verify that ResolveTools defaults missing, invalid, and non-positive limits to three.")]
        public void CallToolResolveToolsInvalidLimitsDefaultTest()
        {
            // Arrange: create enough tools and examples per tool to expose both default limits.
            var cacheManager = NewCacheManager();
            for (var index = 0; index < 5; index++)
            {
                cacheManager.SyncCache(NewCacheModel($"UnitTestResolve{index:D2}", RetrievalPhrase, exampleCount: 5));
            }

            // Arrange: bind the repository to the populated cache so both projections index every tool.
            var repository = NewRepository(cacheManager);

            // Act: resolve once without limits and once with invalid and non-positive limits.
            var missing = ResolveTools(repository, NewResolveToolsParameters(RetrievalPhrase, take: null, examplesTake: null));
            var invalid = ResolveTools(repository, NewResolveToolsParameters(RetrievalPhrase, take: "ten", examplesTake: 0));

            // Assert: both calls keep the established three-tool and three-example defaults.
            foreach (var tools in new[] { missing, invalid })
            {
                Assert.HasCount(expected: 3, collection: tools);
                Assert.IsTrue(condition: tools.All(tool => tool.GetProperty("examples").GetArrayLength() == 3));
            }
        }

        [TestMethod(DisplayName = "Verify that ResolveTools honors the requested number of examples per tool.")]
        public void CallToolResolveToolsHonorsExamplesTakeTest()
        {
            // Arrange: register one tool that owns more matching examples than the requested limit.
            const string Key = "UnitTestResolveExamples";
            var (cacheManager, repository) = NewContext();
            cacheManager.SyncCache(NewCacheModel(Key, RetrievalPhrase, exampleCount: 5));
            var parameters = NewResolveToolsParameters(RetrievalPhrase, take: null, examplesTake: 2);

            // Act: resolve the tool with a smaller example limit than its example count.
            var tools = ResolveTools(repository, parameters);

            // Assert: the single tool returns exactly the requested number of positively scored examples.
            Assert.HasCount(expected: 1, collection: tools);
            var examples = tools[0]
                .GetProperty("examples")
                .EnumerateArray()
                .ToArray();
            Assert.HasCount(expected: 2, collection: examples);
            Assert.IsTrue(condition: examples.All(example => example.GetProperty("score").GetInt32() > 0));
        }

        [TestMethod(DisplayName = "Verify that ResolveTools returns an empty tools array when nothing matches the intent.")]
        public void CallToolResolveToolsNoMatchReturnsEmptyTest()
        {
            // Arrange: create matching capabilities and an intent that shares no token with them.
            var repository = NewRetrievalRepository(toolCount: 4);
            var parameters = NewResolveToolsParameters("unmatchedresolvesentinel", take: null, examplesTake: null);

            // Act: resolve the tools for the unmatched intent.
            var tools = ResolveTools(repository, parameters);

            // Assert: the response keeps its object shape with an empty array instead of null.
            Assert.IsEmpty(tools);
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

        // Invokes the shared private conversion boundary without involving a live automation session.
        private static ActionRuleModel ConvertRuleData(JsonElement ruleData)
        {
            // Delegate to the complete overload with an empty tool catalog.
            return ConvertRuleData(
                ruleData,
                tools: new ConcurrentDictionary<string, McpToolModel>(StringComparer.OrdinalIgnoreCase));
        }

        // Invokes the shared private conversion boundary over the supplied tool catalog.
        private static ActionRuleModel ConvertRuleData(
            JsonElement ruleData,
            ConcurrentDictionary<string, McpToolModel> tools)
        {
            const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
            var method = typeof(ToolsRepository).GetMethod("ConvertRuleData", Flags);

            Assert.IsNotNull(method);
            return (ActionRuleModel)method.Invoke(null, [ruleData, new IntentModel(), tools]);
        }

        // Reads the repository's current formatted tool catalog, which the latest cache synchronization published.
        private static ConcurrentDictionary<string, McpToolModel> GetTools()
        {
            const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
            var field = typeof(ToolsRepository).GetField("_tools", Flags);

            Assert.IsNotNull(field);
            return (ConcurrentDictionary<string, McpToolModel>)field.GetValue(null);
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

        // Creates a manifest-backed capability with one example that can populate both domain and lexical projections.
        private static G4PluginCacheModel NewCacheModel(string key, string description)
        {
            // Delegate to the complete overload with the single example most catalog tests need.
            return NewCacheModel(key, description, exampleCount: 1);
        }

        // Creates a capability in the default test namespace with the requested number of examples.
        private static G4PluginCacheModel NewCacheModel(string key, string description, int exampleCount)
        {
            // Delegate to the complete overload with the default test namespace.
            return NewCacheModel(key, description, exampleCount, PluginNamespace);
        }

        // Creates a manifest-backed capability whose examples all match the description and carry an example rule.
        private static G4PluginCacheModel NewCacheModel(
            string key,
            string description,
            int exampleCount,
            string @namespace)
        {
            // Include deterministic example and summary text used by the respective derived catalogs.
            var examples = Enumerable.Range(start: 0, count: exampleCount).Select(index => new PluginExampleModel
            {
                Context = new Dictionary<string, object>
                {
                    ["labels"] = new[] { description }
                },
                Description = [description],
                Rule = new RuleExampleModel
                {
                    Argument = $"{{{{$ --Index:{index}}}}}",
                    PluginName = key
                }
            });
            return new G4PluginCacheModel
            {
                Manifest = new G4PluginAttribute
                {
                    Description = [description],
                    Examples = [.. examples],
                    Key = key,
                    Namespace = @namespace,
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

        // Creates one MCP FindTool payload using the exact-name-only public contract.
        private static JsonElement NewFindToolParameters(string toolName)
        {
            return JsonSerializer.SerializeToElement(
                new Dictionary<string, object>
                {
                    ["name"] = "g4.FindTool",
                    ["arguments"] = new Dictionary<string, object>
                    {
                        ["toolName"] = toolName
                    }
                },
                AppSettings.JsonOptions);
        }

        // Creates one MCP ResolveTools payload whose optional limits are included only when supplied.
        private static JsonElement NewResolveToolsParameters(string intent, object take, object examplesTake)
        {
            // Build the arguments separately so tests can omit limits or provide invalid JSON-compatible values.
            var arguments = new Dictionary<string, object>
            {
                ["intent"] = new IntentModel
                {
                    AgentIntent = intent,
                    UserIntent = intent
                }
            };

            // Include each optional limit only when the test supplies a value.
            if (take != null)
            {
                arguments["take"] = take;
            }

            if (examplesTake != null)
            {
                arguments["examplesTake"] = examplesTake;
            }

            // Serialize through application options so the test uses the production JSON naming contract.
            return JsonSerializer.SerializeToElement(
                new Dictionary<string, object>
                {
                    ["name"] = "g4.ResolveTools",
                    ["arguments"] = arguments
                },
                AppSettings.JsonOptions);
        }

        // Reads the catalog tool name of one resolved tool entry from its embedded g4.FindTool model.
        private static string ReadToolName(JsonElement resolvedTool)
        {
            // Read the authoritative name from the tool model rather than from any derived ranking field.
            return resolvedTool
                .GetProperty("tool")
                .GetProperty("name")
                .GetString();
        }

        // Invokes g4.ResolveTools and returns its resolved tool entries in the serialized wire shape.
        private static JsonElement[] ResolveTools(ToolsRepository repository, JsonElement parameters)
        {
            // Serialize the result with the application options so the assertions read the MCP response contract.
            var result = repository.CallTool(parameters);
            var json = JsonSerializer.SerializeToElement(result, options: AppSettings.JsonOptions);

            // Return the resolved tool entries; a missing "tools" member fails the calling test immediately.
            return json
                .GetProperty("tools")
                .EnumerateArray()
                .ToArray();
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
