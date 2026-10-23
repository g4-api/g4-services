using G4.Models;
using G4.Services.Domain.V4.Repositories;
using G4.Services.UnitTests.Mcp;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace G4.Services.UnitTests.Repositories
{
    [TestClass]
    [TestCategory("FlowsRepository")]
    [TestCategory("UnitTest")]
    public sealed class FlowsRepositoryTests
    {
        [TestMethod(DisplayName = "Verify invalid manifests and identity conflicts raise root exceptions " +
            "that both transports map.")]
        public void AddFlowFailureTest()
        {
            // Arrange: one manifest without a key, one with an invalid automation, and one that conflicts.
            var repository = new FlowsRepository(flows: new FakeFlowsClient());
            var missingKey = NewManifest(key: "", @namespace: null);
            var invalidAutomation = NewManifest(key: "Broken", @namespace: null);
            invalidAutomation.Automation = "not base64";
            var conflicting = NewManifest(key: FakeFlowsClient.ConflictKey, @namespace: null);

            // Act: register each manifest and capture the raised failures.
            var missingKeyError = Assert.ThrowsExactly<ArgumentException>(() => repository.AddFlow(missingKey));
            var automationError = Assert.ThrowsExactly<ArgumentException>(
                () => repository.AddFlow(invalidAutomation));
            var conflict = Assert.ThrowsExactly<InvalidOperationException>(() => repository.AddFlow(conflicting));

            // Assert: invalid input names the manifest, and the wrapped conflict is unwrapped to its root.
            Assert.AreEqual(expected: "manifest", actual: missingKeyError.ParamName);
            Assert.AreEqual(expected: "manifest", actual: automationError.ParamName);
            Assert.Contains(substring: FakeFlowsClient.ConflictKey, value: conflict.Message);
            Assert.IsNull(value: conflict.InnerException);
        }

        [TestMethod(DisplayName = "Verify register, read, filter, invoke, stop, and remove through the repository.")]
        public void FlowLifecycleTest()
        {
            // Arrange: register one default-namespace flow and one team flow.
            var client = new FakeFlowsClient();
            var repository = new FlowsRepository(flows: client);
            repository.AddFlow(NewManifest(key: "Login", @namespace: null));
            repository.AddFlow(NewManifest(key: "Report", @namespace: "Team.Flows"));

            // Act: read, filter, run without an invoke model, stop, and remove.
            var manifest = repository.GetFlow(@namespace: null, key: "Login");
            var everyFlow = repository.GetFlows(@namespace: null);
            var teamFlows = repository.GetFlows(@namespace: "Team.Flows");
            var responses = repository.InvokeFlow(@namespace: null, key: "Login", invokeModel: null);
            var stopResult = repository.StopInvocation(automationId: "run-1");
            repository.RemoveFlow(@namespace: "Team.Flows", key: "Report");

            // Assert: lookups and filters follow the namespace, and a missing invoke model runs the flow as stored.
            Assert.AreEqual(expected: "Login", actual: manifest.Key);
            Assert.HasCount(expected: 2, collection: everyFlow);
            Assert.HasCount(expected: 1, collection: teamFlows);
            Assert.HasCount(expected: 1, collection: responses);
            Assert.IsNotNull(value: client.Invocations.Single());
            Assert.AreEqual(expected: 0, actual: stopResult);
            Assert.AreEqual(expected: "run-1", actual: client.Stopped.Single());
            Assert.HasCount(expected: 1, collection: client.Manifests);
        }

        [TestMethod(DisplayName = "Verify invalid flows and unreadable data sources raise argument exceptions " +
            "without inner exceptions.")]
        public void InvokeFlowFailureTest()
        {
            // Arrange: the fake client reproduces both invocation failures by reserved key.
            var repository = new FlowsRepository(flows: new FakeFlowsClient());

            // Act: invoke the invalid flow and the flow with an unreadable data source.
            var invalidFlow = Assert.ThrowsExactly<ArgumentException>(
                () => repository.InvokeFlow(@namespace: null, key: FakeFlowsClient.InvalidFlowKey, invokeModel: null));
            var dataSourceKey = FakeFlowsClient.InvalidDataSourceKey;
            var invalidDataSource = Assert.ThrowsExactly<ArgumentException>(
                () => repository.InvokeFlow(@namespace: null, key: dataSourceKey, invokeModel: null));

            // Assert: both failures are root argument exceptions, so the REST middleware maps them to 400.
            Assert.Contains(substring: "not valid JSON after its parameters were applied", value: invalidFlow.Message);
            Assert.Contains(substring: "fixture.csv", value: invalidDataSource.Message);
            Assert.IsNull(value: invalidDataSource.InnerException);
        }

        [TestMethod(DisplayName = "Verify unknown flows raise key-not-found exceptions " +
            "naming the normalized identity.")]
        public void UnknownFlowTest()
        {
            // Arrange: an empty flows client, so every lookup misses.
            var repository = new FlowsRepository(flows: new FakeFlowsClient());

            // Act: get, remove, and invoke a flow that was never registered, with and without a namespace.
            var getError = Assert.ThrowsExactly<KeyNotFoundException>(
                () => repository.GetFlow(@namespace: null, key: "Missing"));
            var removeError = Assert.ThrowsExactly<KeyNotFoundException>(
                () => repository.RemoveFlow(@namespace: "Team.Flows", key: "Missing"));
            var invokeError = Assert.ThrowsExactly<KeyNotFoundException>(
                () => repository.InvokeFlow(@namespace: null, key: "Missing", invokeModel: new G4FlowInvokeModel()));

            // Assert: an omitted namespace is reported as the default namespace the client searched.
            var defaultIdentity = $"'{FlowsRepository.DefaultNamespace}.Missing'";
            Assert.Contains(substring: defaultIdentity, value: getError.Message);
            Assert.Contains(substring: "'Team.Flows.Missing'", value: removeError.Message);
            Assert.Contains(substring: defaultIdentity, value: invokeError.Message);
        }

        // Creates a valid manifest whose automation is the Base64 encoding of an empty automation object.
        private static G4FlowManifestModel NewManifest(string key, string @namespace)
        {
            // Arrange: encode the automation exactly as REST callers do.
            var automation = Convert.ToBase64String(inArray: Encoding.UTF8.GetBytes(s: "{\"stages\":[]}"));
            return new G4FlowManifestModel { Automation = automation, Key = key, Namespace = @namespace };
        }
    }
}
