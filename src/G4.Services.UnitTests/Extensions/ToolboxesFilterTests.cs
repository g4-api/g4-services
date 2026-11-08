using G4.Attributes;
using G4.Extensions;
using G4.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace G4.Services.UnitTests.Extensions
{
    [TestClass]
    [TestCategory("ToolboxesFilter")]
    [TestCategory("UnitTest")]
    public sealed class ToolboxesFilterTests
    {
        // Provides the namespace of the plugins the filter tests decide.
        private const string PluginNamespace = "G4.Plugins.Ui";

        // Provides the plugin type of the cache bucket the selection tests filter.
        private const string PluginType = "Action";

        // Provides the manifest keys the manifest selection test expects, in their original order.
        private static readonly string[] s_expectedManifestKeys = ["WriteLog", "NoCategory"];

        #region *** Data Set ***
        [DataRow("G4.Plugins.Ui:SendKeys", true, "SendKeys", false, true)]
        [DataRow("g4.plugins.ui:sendkeys", false, "SendKeys", true, false)]
        [DataRow("Other.Namespace:SendKeys", false, "SendKeys", true, true)]
        [DataRow("", false, "sendkeys", false, false)]
        #endregion
        [TestMethod(DisplayName = "Verify a qualified tool entry beats a bare one, and a qualified entry of " +
            "another namespace does not apply.")]
        public void ToolEntryPrecedenceTest(
            string qualifiedName,
            bool isQualifiedEnabled,
            string bareName,
            bool isBareEnabled,
            bool expected)
        {
            // Arrange: name the plugin by both forms, with its toolbox disabled so only tool entries can show it.
            var tools = new List<ToolboxesFilterModel.FilterEntryModel>
            {
                NewEntry(bareName, isBareEnabled)
            };
            if (!string.IsNullOrEmpty(qualifiedName))
            {
                tools.Add(NewEntry(qualifiedName, isQualifiedEnabled));
            }

            var filter = new ToolboxesFilterModel
            {
                Toolboxes = [NewEntry(name: "UiControls", isEnabled: false)],
                Tools = [.. tools]
            };

            // Act: decide the plugin.
            var isEnabled = filter.TestEnabled(NewManifest(key: "SendKeys", categories: ["UiControls"]));

            // Assert: the matching rule decides the plugin as the data row expects.
            Assert.AreEqual(expected, isEnabled);
        }

        #region *** Data Set ***
        [DataRow("UiControls", false, false)]
        [DataRow("Ui Controls", false, false)]
        [DataRow("ui controls", false, false)]
        [DataRow("Elements", false, true)]
        [DataRow("UiControls", true, true)]
        #endregion
        [TestMethod(DisplayName = "Verify toolbox names match ignoring case and white space, and an unlisted " +
            "toolbox is enabled.")]
        public void ToolboxNameMatchingTest(string toolboxName, bool isToolboxEnabled, bool expected)
        {
            // Arrange: one toolbox entry against a plugin in the UiControls category.
            var filter = new ToolboxesFilterModel
            {
                Toolboxes = [NewEntry(toolboxName, isToolboxEnabled)]
            };

            // Act: decide the plugin.
            var isEnabled = filter.TestEnabled(NewManifest(key: "SendKeys", categories: ["UiControls"]));

            // Assert: the toolbox entry applies only when its name matches the category.
            Assert.AreEqual(expected, isEnabled);
        }

        [TestMethod(DisplayName = "Verify a plugin in several toolboxes stays shown while any of them is shown.")]
        public void PluginInSeveralToolboxesTest()
        {
            // Arrange: disable one of two categories, then both.
            var oneDisabled = new ToolboxesFilterModel { Toolboxes = [NewEntry(name: "UiControls", isEnabled: false)] };
            var bothDisabled = new ToolboxesFilterModel
            {
                Toolboxes =
                [
                    NewEntry(name: "UiControls", isEnabled: false),
                    NewEntry(name: "Elements", isEnabled: false)
                ]
            };
            var manifest = NewManifest(key: "SendKeys", categories: ["UiControls", "Elements"]);

            // Act: decide the plugin under both filters.
            var isEnabledWithOneDisabled = oneDisabled.TestEnabled(manifest);
            var isEnabledWithBothDisabled = bothDisabled.TestEnabled(manifest);

            // Assert: the plugin hides only when every one of its toolboxes is hidden.
            Assert.IsTrue(isEnabledWithOneDisabled);
            Assert.IsFalse(isEnabledWithBothDisabled);
        }

        [TestMethod(DisplayName = "Verify the last entry for a name wins and incomplete entries are ignored.")]
        public void LastEntryWinsAndIncompleteEntriesIgnoredTest()
        {
            // Arrange: an earlier disabled entry overridden by a later enabled one, then only incomplete entries.
            var overridden = new ToolboxesFilterModel
            {
                Toolboxes =
                [
                    NewEntry(name: "UiControls", isEnabled: false),
                    NewEntry(name: "UiControls", isEnabled: true)
                ]
            };
            var incomplete = new ToolboxesFilterModel
            {
                Toolboxes = [new() { Name = "UiControls" }, NewEntry(name: " ", isEnabled: false), null],
                Tools = [new() { Name = "SendKeys" }, NewEntry(name: null, isEnabled: false)]
            };
            var manifest = NewManifest(key: "SendKeys", categories: ["UiControls"]);

            // Act: decide the plugin under both filters, and without any filter.
            var isOverriddenEnabled = overridden.TestEnabled(manifest);
            var isIncompleteEnabled = incomplete.TestEnabled(manifest);
            var isNoFilterEnabled = ((ToolboxesFilterModel)null).TestEnabled(manifest);

            // Assert: the later entry decides, and entries without a name or an enabled value change nothing.
            Assert.IsTrue(isOverriddenEnabled);
            Assert.IsTrue(isIncompleteEnabled);
            Assert.IsTrue(isNoFilterEnabled);
        }

        [TestMethod(DisplayName = "Verify cache selection keeps every lookup name of a shown plugin and drops " +
            "every lookup name of a hidden one.")]
        public void SelectCacheKeepsLookupNamesTest()
        {
            // Arrange: two plugins stored under qualified, bare, and alias names, with one toolbox disabled.
            var shown = NewCacheModel(key: "WriteLog", categories: ["Logging"]);
            var hidden = NewCacheModel(key: "SendKeys", categories: ["UiControls"]);
            var bucket = new ConcurrentDictionary<string, G4PluginCacheModel>(StringComparer.OrdinalIgnoreCase)
            {
                [$"{PluginNamespace}:WriteLog"] = shown,
                ["WriteLog"] = shown,
                ["Log"] = shown,
                [$"{PluginNamespace}:SendKeys"] = hidden,
                ["SendKeys"] = hidden
            };
            var cache = new Dictionary<string, ConcurrentDictionary<string, G4PluginCacheModel>>
            {
                [PluginType] = bucket
            };
            var filter = new ToolboxesFilterModel { Toolboxes = [NewEntry(name: "UiControls", isEnabled: false)] };

            // Act: select the cache, and select it again without a filter.
            var filteredCache = filter.SelectCache(cache);
            var unfilteredCache = ((ToolboxesFilterModel)null).SelectCache(cache);

            // Assert: the shown plugin keeps all three names, the hidden plugin is gone, and no filter changes nothing.
            var filteredBucket = filteredCache[PluginType];
            Assert.HasCount(3, filteredBucket);
            Assert.IsTrue(filteredBucket.Values.All(model => ReferenceEquals(model, shown)));
            Assert.IsTrue(filteredBucket.ContainsKey("log"));
            Assert.AreSame(cache, unfilteredCache);
            Assert.HasCount(5, cache[PluginType]);
        }

        [TestMethod(DisplayName = "Verify manifest selection drops hidden plugins and keeps the rest in order.")]
        public void SelectManifestsTest()
        {
            // Arrange: three manifests, one hidden by a bare tool entry and one plugin without categories.
            var manifests = new IG4PluginManifest[]
            {
                NewManifest(key: "WriteLog", categories: ["Logging"]),
                NewManifest(key: "SendKeys", categories: ["UiControls"]),
                NewManifest(key: "NoCategory", categories: [])
            };
            var filter = new ToolboxesFilterModel { Tools = [NewEntry(name: "SendKeys", isEnabled: false)] };

            // Act: select the manifests.
            var selected = filter.SelectManifests(manifests).Select(manifest => manifest.Key).ToArray();

            // Assert: only the hidden plugin is removed.
            Assert.AreSequenceEqual(s_expectedManifestKeys, selected);
        }

        // Creates a cache model whose manifest belongs to the test namespace.
        private static G4PluginCacheModel NewCacheModel(string key, string[] categories)
        {
            return new G4PluginCacheModel { Manifest = NewManifest(key, categories) };
        }

        // Creates one complete filter entry.
        private static ToolboxesFilterModel.FilterEntryModel NewEntry(string name, bool isEnabled)
        {
            return new ToolboxesFilterModel.FilterEntryModel { Enabled = isEnabled, Name = name };
        }

        // Creates a manifest in the test namespace with the given key and categories.
        private static G4PluginAttribute NewManifest(string key, string[] categories)
        {
            return new G4PluginAttribute
            {
                Categories = categories,
                Key = key,
                Namespace = PluginNamespace,
                PluginType = PluginType
            };
        }
    }
}
