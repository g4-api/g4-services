using G4.Api.Models;
using G4.Models;
using G4.Settings;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Collections.Generic;
using System.Text.Json;

namespace G4.Services.UnitTests.Models
{
    [TestClass]
    [TestCategory("CacheSyncResultModel")]
    [TestCategory("UnitTest")]
    public class CacheSyncResultModelTests
    {
        [TestMethod(DisplayName = "Verify that the cache sync result serializes to valid JSON with the Hub's options " +
            "and reports a synchronized server, a failed server, and the plugins the engine rejected.")]
        public void CacheSyncResultModelSerializationTest()
        {
            // Arrange: one server that synchronized with a rejected plugin, and one server that could not be reached.
            const string FailedError = "No connection could be made because the target machine actively refused it.";
            const string RejectedPlugin = "broken-tool";
            const string RejectedReason = "The manifest has no plugin name.";
            var resultModel = new CacheSyncResultModel
            {
                Servers =
                [
                    new McpServerSyncResultModel
                    {
                        IsSynced = true,
                        Name = "atp-live",
                        PluginCount = 3,
                        RejectedPlugins = new Dictionary<string, string> { [RejectedPlugin] = RejectedReason },
                        Url = "http://localhost:9944/api/v4/g4/mcp"
                    },
                    new McpServerSyncResultModel
                    {
                        Error = FailedError,
                        Name = "atp-dead",
                        Url = "http://localhost:1/mcp"
                    }
                ]
            };

            // Act: serialize with the options the Hub's MVC output uses, then parse the text like a client does.
            var json = JsonSerializer.Serialize(resultModel, options: AppSettings.JsonOptions);
            using var document = JsonDocument.Parse(json);
            var servers = document.RootElement.GetProperty("servers");
            var syncedServer = servers[0];
            var failedServer = servers[1];

            // Assert: both servers are reported in request order under camel-case names.
            Assert.AreEqual(2, servers.GetArrayLength());
            Assert.AreEqual("atp-live", syncedServer.GetProperty("name").GetString());
            Assert.AreEqual("atp-dead", failedServer.GetProperty("name").GetString());

            // Assert: the synchronized server carries its plugin count and the rejected plugin with its reason.
            Assert.IsTrue(syncedServer.GetProperty("isSynced").GetBoolean());
            Assert.AreEqual(3, syncedServer.GetProperty("pluginCount").GetInt32());
            var rejectedPlugins = syncedServer.GetProperty("rejectedPlugins");
            Assert.AreEqual(RejectedReason, rejectedPlugins.GetProperty(RejectedPlugin).GetString());

            // Assert: the failed server carries its error, and its default false and zero values are omitted, so a
            // client reads an absent isSynced as not synchronized.
            Assert.AreEqual(FailedError, failedServer.GetProperty("error").GetString());
            Assert.IsFalse(failedServer.TryGetProperty("isSynced", out _));
            Assert.IsFalse(failedServer.TryGetProperty("pluginCount", out _));
        }
    }
}
