using G4.Api;
using G4.Attributes;
using G4.Cache;
using G4.Models;
using G4.Services.Domain.V4.Repositories;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;

namespace G4.Services.UnitTests.Repositories
{
    [TestClass]
    [DoNotParallelize]
    [TestCategory("TemplateSupplyChain")]
    [TestCategory("UnitTest")]
    public class TemplateSupplyChainTests
    {
        // Identifies the physical cache bucket used by template capabilities.
        private const string PluginType = "Action";

        // Qualifies test capabilities so logical identities cannot collide with production plugins.
        private const string PluginNamespace = "G4.Services.UnitTests.Templates";

        [TestMethod(DisplayName = "Verify that a client template alias cannot replace a domain capability canonical key.")]
        public void AliasMatchingCanonicalKeyIsRejectedTest()
        {
            // Arrange: reserve one canonical cache identity and expose it through the domain catalog.
            const string Key = "UnitTestDomainAliasConflict";
            const string ReservedKey = "UnitTestDomainReservedCanonical";
            var (cacheManager, client, repository) = NewContext();
            CleanupTemplates(client, Key);
            var ownerModel = NewCacheModel(ReservedKey, "canonical owner phrase");
            cacheManager.SyncCache(ownerModel);
            var notifications = new List<CacheManager.CacheChangedEventArgs>();
            cacheManager.CacheChanged += (_, eventArgs) => notifications.Add(eventArgs);

            try
            {
                // Act: attempt to persist a template whose alias conflicts with the existing canonical lookup.
                _ = Assert.ThrowsExactly<InvalidOperationException>(() =>
                    client.Templates.AddTemplate(NewManifest(Key, "alias collision phrase", ReservedKey)));

                // Assert: preflight rejection leaves persistence, authoritative cache, and domain projection unchanged.
                Assert.AreEqual(404, client.Templates.GetTemplate(Key).StatusCode);
                Assert.AreSame(ownerModel, cacheManager.PluginsCache[PluginType][ReservedKey]);
                Assert.IsNotNull(repository.FindTool(intent: string.Empty, toolName: ReservedKey));
                Assert.IsNull(repository.FindTool(intent: string.Empty, toolName: Key));
                Assert.IsEmpty(notifications);
            }
            finally
            {
                // Cleanup: remove any test document left by an unexpected partial persistence failure.
                CleanupTemplates(client, Key);
            }
        }

        [TestMethod(DisplayName = "Verify that a client template key cannot replace a domain capability alias.")]
        public void CanonicalKeyMatchingAliasIsRejectedTest()
        {
            // Arrange: reserve the incoming canonical key as a backward-compatible alias owned by another model.
            const string Key = "UnitTestDomainReservedAlias";
            const string OwnerKey = "UnitTestDomainAliasOwner";
            var (cacheManager, client, repository) = NewContext();
            CleanupTemplates(client, Key);
            var ownerModel = NewCacheModel(OwnerKey, "alias owner phrase", Key);
            cacheManager.SyncCache(ownerModel);
            var notifications = new List<CacheManager.CacheChangedEventArgs>();
            cacheManager.CacheChanged += (_, eventArgs) => notifications.Add(eventArgs);

            try
            {
                // Act: attempt to persist a canonical template over the existing alias lookup path.
                _ = Assert.ThrowsExactly<InvalidOperationException>(() =>
                    client.Templates.AddTemplate(NewManifest(Key, "canonical collision phrase")));

                // Assert: collision rejection preserves the alias owner throughout persistence, cache, and domain state.
                Assert.AreEqual(404, client.Templates.GetTemplate(Key).StatusCode);
                Assert.AreSame(ownerModel, cacheManager.PluginsCache[PluginType][Key]);
                Assert.IsNotNull(repository.FindTool(intent: string.Empty, toolName: OwnerKey));
                Assert.IsNull(repository.FindTool(intent: string.Empty, toolName: Key));
                Assert.IsEmpty(notifications);
            }
            finally
            {
                // Cleanup: remove any test document left by an unexpected partial persistence failure.
                CleanupTemplates(client, Key);
            }
        }

        [TestMethod(DisplayName = "Verify that client template addition reaches persistence, cache, lexical indexes, and domain tools.")]
        public void ClientAdditionSynchronizesSupplyChainTest()
        {
            // Arrange: create every in-process consumer before registering the template through the public client.
            const string Key = "UnitTestDomainTemplateAddition";
            const string Alias = "UnitTestDomainTemplateAdditionAlias";
            const string Phrase = "cobalt meadow";
            var (cacheManager, client, repository) = NewContext();
            CleanupTemplates(client, Key);
            var notifications = new List<CacheManager.CacheChangedEventArgs>();
            cacheManager.CacheChanged += (_, eventArgs) => notifications.Add(eventArgs);

            try
            {
                // Act: add the template through G4Client and query every derived layer after the synchronous mutation.
                var statusCode = client.Templates.AddTemplate(NewManifest(Key, Phrase, Alias));
                var persisted = client.Templates.GetTemplate(Key);
                var tool = repository.FindTool(intent: string.Empty, toolName: Key);
                var lexicalTools = repository.FindTools(Phrase, maxResults: 3, threshold: 0);
                var lexicalExamples = GetRetrievalManager(repository)
                    .FindExamples(Key, PluginNamespace, Phrase, take: 3);

                // Assert: one authoritative addition supplies persistence, canonical and alias lookups, and both projections.
                Assert.AreEqual(204, statusCode);
                Assert.AreEqual(200, persisted.StatusCode);
                Assert.AreSame(cacheManager.PluginsCache[PluginType][Key], cacheManager.PluginsCache[PluginType][Alias]);
                Assert.IsNotNull(tool);
                Assert.AreEqual(Phrase, tool.Description);
                Assert.IsTrue(lexicalTools.ContainsKey(Key));
                Assert.HasCount(1, lexicalExamples.Examples);

                // Assert: the completed mutation emits one addition after every cache lookup path is visible.
                Assert.HasCount(1, notifications);
                Assert.AreEqual(CacheManager.CacheChangeTypes.Added, notifications[0].ChangeType);
                Assert.Contains(Key, notifications[0].EntityKeys);
                Assert.Contains(Alias, notifications[0].EntityKeys);
            }
            finally
            {
                // Cleanup: remove the persisted template so the process-wide LiteDB remains isolated for later tests.
                CleanupTemplates(client, Key);
            }
        }

        [TestMethod(DisplayName = "Verify that client template clear removes templates from every layer and retains static tools.")]
        public void ClientClearSynchronizesSupplyChainTest()
        {
            // Arrange: register two persisted templates and one unrelated static capability in the same Action bucket.
            const string FirstKey = "UnitTestDomainTemplateClearFirst";
            const string SecondKey = "UnitTestDomainTemplateClearSecond";
            const string StaticKey = "UnitTestDomainStaticAction";
            var (cacheManager, client, repository) = NewContext();
            CleanupTemplates(client, FirstKey, SecondKey);
            cacheManager.SyncCache(NewCacheModel(StaticKey, "static domain phrase"));
            client.Templates.AddTemplate(NewManifest(FirstKey, "scarlet island"));
            client.Templates.AddTemplate(NewManifest(SecondKey, "silver orchard"));
            var notifications = new List<CacheManager.CacheChangedEventArgs>();
            cacheManager.CacheChanged += (_, eventArgs) => notifications.Add(eventArgs);

            try
            {
                // Act: clear persisted templates through the public client and query all affected derived representations.
                client.Templates.ClearTemplates();
                var firstTools = repository.FindTools("scarlet island", maxResults: 3, threshold: 0);
                var secondTools = repository.FindTools("silver orchard", maxResults: 3, threshold: 0);

                // Assert: every template identity disappears while the unrelated static capability remains formatted.
                Assert.AreEqual(404, client.Templates.GetTemplate(FirstKey).StatusCode);
                Assert.AreEqual(404, client.Templates.GetTemplate(SecondKey).StatusCode);
                Assert.IsFalse(cacheManager.PluginsCache[PluginType].ContainsKey(FirstKey));
                Assert.IsFalse(cacheManager.PluginsCache[PluginType].ContainsKey(SecondKey));
                Assert.IsTrue(cacheManager.PluginsCache[PluginType].ContainsKey(StaticKey));
                Assert.IsNull(repository.FindTool(intent: string.Empty, toolName: FirstKey));
                Assert.IsNull(repository.FindTool(intent: string.Empty, toolName: SecondKey));
                Assert.IsNotNull(repository.FindTool(intent: string.Empty, toolName: StaticKey));
                Assert.IsEmpty(firstTools);
                Assert.IsEmpty(secondTools);

                // Assert: clear publishes one completed removal for each persisted template identity.
                Assert.HasCount(2, notifications);
                Assert.IsTrue(notifications.All(i => i.ChangeType == CacheManager.CacheChangeTypes.Removed));
            }
            finally
            {
                // Cleanup: keep test-owned persistence empty even when an assertion interrupts the clear scenario.
                CleanupTemplates(client, FirstKey, SecondKey);
            }
        }

        [TestMethod(DisplayName = "Verify that a different client template key remains an independent addition until removal.")]
        public void ClientDifferentKeyRemainsIndependentTest()
        {
            // Arrange: create one client and domain repository over the same isolated authoritative cache instance.
            const string PreviousKey = "UnitTestDomainPreviousTemplate";
            const string CurrentKey = "UnitTestDomainCurrentTemplate";
            var (cacheManager, client, repository) = NewContext();
            CleanupTemplates(client, PreviousKey, CurrentKey);
            var notifications = new List<CacheManager.CacheChangedEventArgs>();
            cacheManager.CacheChanged += (_, eventArgs) => notifications.Add(eventArgs);

            try
            {
                // Act: register two namespace-equal manifests whose different keys define independent identities.
                client.Templates.AddTemplate(NewManifest(PreviousKey, "previous identity phrase"));
                client.Templates.AddTemplate(NewManifest(CurrentKey, "current identity phrase"));

                // Assert: the client never infers a rename and exposes both identities through every catalog.
                Assert.AreEqual(200, client.Templates.GetTemplate(PreviousKey).StatusCode);
                Assert.AreEqual(200, client.Templates.GetTemplate(CurrentKey).StatusCode);
                Assert.IsTrue(cacheManager.PluginsCache[PluginType].ContainsKey(PreviousKey));
                Assert.IsTrue(cacheManager.PluginsCache[PluginType].ContainsKey(CurrentKey));
                Assert.IsNotNull(repository.FindTool(intent: string.Empty, toolName: PreviousKey));
                Assert.IsNotNull(repository.FindTool(intent: string.Empty, toolName: CurrentKey));
                Assert.HasCount(2, notifications);
                Assert.IsTrue(notifications.All(i => i.ChangeType == CacheManager.CacheChangeTypes.Added));
            }
            finally
            {
                // Cleanup: explicitly remove both client-owned identities because a key change never retires the old key.
                CleanupTemplates(client, PreviousKey, CurrentKey);
            }
        }

        [TestMethod(DisplayName = "Verify that client template removal clears persistence, cache, lexical indexes, and domain tools.")]
        public void ClientRemovalSynchronizesSupplyChainTest()
        {
            // Arrange: add and index one aliased template before observing only its removal transition.
            const string Key = "UnitTestDomainTemplateRemoval";
            const string Alias = "UnitTestDomainTemplateRemovalAlias";
            const string Phrase = "crimson harbor";
            var (cacheManager, client, repository) = NewContext();
            CleanupTemplates(client, Key);
            client.Templates.AddTemplate(NewManifest(Key, Phrase, Alias));
            var notifications = new List<CacheManager.CacheChangedEventArgs>();
            cacheManager.CacheChanged += (_, eventArgs) => notifications.Add(eventArgs);

            try
            {
                // Act: remove the template through G4Client and query every downstream state holder afterward.
                var statusCode = client.Templates.RemoveTemplate(Key);
                var lexicalTools = repository.FindTools(Phrase, maxResults: 3, threshold: 0);
                var lexicalExamples = GetRetrievalManager(repository)
                    .FindExamples(Key, PluginNamespace, Phrase, take: 3);

                // Assert: persistence, canonical cache entry, alias, lexical indexes, and domain tool all retire together.
                Assert.AreEqual(204, statusCode);
                Assert.AreEqual(404, client.Templates.GetTemplate(Key).StatusCode);
                Assert.IsFalse(cacheManager.PluginsCache[PluginType].ContainsKey(Key));
                Assert.IsFalse(cacheManager.PluginsCache[PluginType].ContainsKey(Alias));
                Assert.IsNull(repository.FindTool(intent: string.Empty, toolName: Key));
                Assert.IsEmpty(lexicalTools);
                Assert.IsEmpty(lexicalExamples.Examples);

                // Assert: one removal identifies every lookup path retired by the completed cache mutation.
                Assert.HasCount(1, notifications);
                Assert.AreEqual(CacheManager.CacheChangeTypes.Removed, notifications[0].ChangeType);
                Assert.Contains(Key, notifications[0].EntityKeys);
                Assert.Contains(Alias, notifications[0].EntityKeys);
            }
            finally
            {
                // Cleanup: tolerate both the expected missing document and any unexpected incomplete removal.
                CleanupTemplates(client, Key);
            }
        }

        [TestMethod(DisplayName = "Verify that client template update replaces content and retires stale aliases in every layer.")]
        public void ClientUpdateSynchronizesSupplyChainTest()
        {
            // Arrange: register one stable template identity with original lexical content and a compatibility alias.
            const string Key = "UnitTestDomainTemplateUpdate";
            const string RetiredAlias = "UnitTestDomainRetiredAlias";
            const string CurrentAlias = "UnitTestDomainCurrentAlias";
            const string RetiredPhrase = "amber glacier";
            const string CurrentPhrase = "violet harbor";
            var (cacheManager, client, repository) = NewContext();
            CleanupTemplates(client, Key);
            client.Templates.AddTemplate(NewManifest(Key, RetiredPhrase, RetiredAlias));
            var notifications = new List<CacheManager.CacheChangedEventArgs>();
            cacheManager.CacheChanged += (_, eventArgs) => notifications.Add(eventArgs);

            try
            {
                // Act: replace the same namespace-plus-key identity through the public client and query both generations.
                var statusCode = client.Templates.AddTemplate(NewManifest(Key, CurrentPhrase, CurrentAlias));
                var retiredTools = repository.FindTools(RetiredPhrase, maxResults: 3, threshold: 0);
                var currentTools = repository.FindTools(CurrentPhrase, maxResults: 3, threshold: 0);
                var retiredExamples = GetRetrievalManager(repository)
                    .FindExamples(Key, PluginNamespace, RetiredPhrase, take: 3);
                var currentExamples = GetRetrievalManager(repository)
                    .FindExamples(Key, PluginNamespace, CurrentPhrase, take: 3);

                // Assert: one persisted identity and its current model replace every retired cache and retrieval projection.
                Assert.AreEqual(204, statusCode);
                Assert.HasCount(1, client.Templates.GetTemplates()
                    .Where(i => i.Key.Equals(Key, StringComparison.OrdinalIgnoreCase)));
                Assert.IsFalse(cacheManager.PluginsCache[PluginType].ContainsKey(RetiredAlias));
                Assert.IsTrue(cacheManager.PluginsCache[PluginType].ContainsKey(CurrentAlias));
                Assert.AreEqual(CurrentPhrase, repository.FindTool(string.Empty, Key).Description);
                Assert.IsEmpty(retiredTools);
                Assert.IsTrue(currentTools.ContainsKey(Key));
                Assert.IsEmpty(retiredExamples.Examples);
                Assert.HasCount(1, currentExamples.Examples);

                // Assert: stable identity replacement emits one update containing retired and current alias paths.
                Assert.HasCount(1, notifications);
                Assert.AreEqual(CacheManager.CacheChangeTypes.Updated, notifications[0].ChangeType);
                Assert.Contains(RetiredAlias, notifications[0].EntityKeys);
                Assert.Contains(CurrentAlias, notifications[0].EntityKeys);
            }
            finally
            {
                // Cleanup: remove the current persisted generation and its active cache lookups.
                CleanupTemplates(client, Key);
            }
        }

        [TestMethod(DisplayName = "Verify that a client namespace collision is rejected before any supply-chain state changes.")]
        public void NamespaceCollisionLeavesSupplyChainUnchangedTest()
        {
            // Arrange: reserve the physical key under another logical namespace before attempting client persistence.
            const string Key = "UnitTestDomainNamespaceConflict";
            var (cacheManager, client, repository) = NewContext();
            CleanupTemplates(client, Key);
            var ownerModel = NewCacheModel(Key, "amber glacier");
            ownerModel.Manifest.Namespace = "G4.Services.UnitTests.Existing";
            cacheManager.SyncCache(ownerModel);
            var notifications = new List<CacheManager.CacheChangedEventArgs>();
            cacheManager.CacheChanged += (_, eventArgs) => notifications.Add(eventArgs);

            try
            {
                // Act: attempt to register the same physical key under the template test namespace.
                _ = Assert.ThrowsExactly<InvalidOperationException>(() =>
                    client.Templates.AddTemplate(NewManifest(Key, "violet harbor")));

                // Assert: preflight rejection leaves persistence, cache, lexical search, and domain metadata unchanged.
                Assert.AreEqual(404, client.Templates.GetTemplate(Key).StatusCode);
                Assert.AreSame(ownerModel, cacheManager.PluginsCache[PluginType][Key]);
                Assert.AreEqual("amber glacier", repository.FindTool(string.Empty, Key).Description);
                Assert.IsEmpty(repository.FindTools("violet harbor", maxResults: 3, threshold: 0));
                Assert.IsEmpty(notifications);
            }
            finally
            {
                // Cleanup: remove any test document left by an unexpected partial persistence failure.
                CleanupTemplates(client, Key);
            }
        }

        // Removes test-owned persisted templates while tolerating identities that are already absent.
        private static void CleanupTemplates(G4Client client, params string[] keys)
        {
            // Route cleanup through the same client contract so LiteDB and owned cache entries remain aligned.
            foreach (var key in keys)
            {
                _ = client.Templates.RemoveTemplate(key);
            }
        }

        // Reads the repository-owned lexical manager so tests can assert its materialized example index directly.
        private static LexicalRetrievalManager GetRetrievalManager(ToolsRepository repository)
        {
            // Resolve the declared lifecycle field and fail clearly if the repository contract changes.
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var field = typeof(ToolsRepository).GetField("_retrievalManager", Flags);
            Assert.IsNotNull(field);
            return (LexicalRetrievalManager)field.GetValue(repository);
        }

        // Creates the public client and domain repository over one isolated authoritative cache instance.
        private static (CacheManager CacheManager, G4Client Client, ToolsRepository Repository) NewContext()
        {
            // Remove environment-derived Action capabilities while preserving real CacheManager mutation and event behavior.
            var cacheManager = new CacheManager();
            cacheManager.PluginsCache[PluginType] =
                new ConcurrentDictionary<string, G4PluginCacheModel>(StringComparer.OrdinalIgnoreCase);

            // Bind both upper-layer consumers to the exact same cache so tests represent one application instance.
            var client = new G4Client(cacheManager);
            var repository = new ToolsRepository(
                clientFactory: new TestHttpClientFactory(),
                cache: cacheManager,
                client);
            return (cacheManager, client, repository);
        }

        // Creates a non-template capability used to reserve identities or verify clear preservation.
        private static G4PluginCacheModel NewCacheModel(string key, string phrase, params string[] aliases)
        {
            // Reuse the manifest shape while omitting the TemplatePlugin type marker assigned by TemplatesClient.
            return new G4PluginCacheModel
            {
                Manifest = NewManifest(key, phrase, aliases)
            };
        }

        // Creates a valid deterministic template manifest with searchable summary, example, and label content.
        private static G4PluginAttribute NewManifest(string key, string phrase, params string[] aliases)
        {
            // Keep the example executable while using a non-self-referencing rule accepted by ConfirmTemplate.
            var example = new PluginExampleModel
            {
                Context = new Dictionary<string, object>
                {
                    ["labels"] = new[] { phrase }
                },
                Description = [phrase],
                Rule = new RuleExampleModel
                {
                    PluginName = "NoAction"
                }
            };

            // Populate every field consumed by persistence, cache identity, lexical retrieval, and domain formatting.
            return new G4PluginAttribute
            {
                Aliases = aliases,
                Description = [phrase],
                Examples = [example],
                Key = key,
                Namespace = PluginNamespace,
                Parameters = [],
                PluginType = PluginType,
                Properties = [],
                Rules = [new ActionRuleModel("NoAction")],
                Source = "Template",
                Summary = [phrase]
            };
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
