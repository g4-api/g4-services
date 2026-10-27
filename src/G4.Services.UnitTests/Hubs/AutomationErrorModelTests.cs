using G4.Models;
using G4.Settings;

using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace G4.Services.UnitTests.Hubs
{
    [TestClass]
    [TestCategory("AutomationNotificationsHub")]
    [TestCategory("UnitTest")]
    public class AutomationErrorModelTests
    {
        // Separates SignalR text frames; the JSON protocol appends it after every message.
        private const char RecordSeparator = '\u001e';

        // Names the client method that carries the hub's automation reply, which the designer subscribes to.
        private const string StartAutomationTarget = "StartAutomation";

        [TestMethod(DisplayName = "Verify that a failed-start reply is a valid StartAutomation frame on the Hub's " +
            "SignalR JSON protocol and carries the error message.")]
        public void AutomationErrorModelFailedStartFrameTest()
        {
            // Arrange: the reply the hub sends when the engine rejects the automation, including the request.
            const string Message = "Invalid credentials.";
            var errorModel = new AutomationErrorModel
            {
                Errors = new Dictionary<string, string[]> { ["Automation"] = [Message] },
                Request = new G4AutomationModel()
            };

            // Act: write the frame exactly as the hub's SignalR JSON protocol does, then parse it like a client.
            var frame = WriteStartAutomationFrame(errorModel);
            using var document = JsonDocument.Parse(json: frame);
            var argument = document.RootElement.GetProperty("arguments")[0];

            // Assert: the frame targets the designer's handler and exposes the reason and the request.
            Assert.AreEqual(StartAutomationTarget, document.RootElement.GetProperty("target").GetString());
            Assert.AreEqual(Message, argument.GetProperty("errors").GetProperty("Automation")[0].GetString());
            Assert.IsTrue(argument.TryGetProperty("request", out _));
            Assert.IsFalse(string.IsNullOrEmpty(argument.GetProperty("traceId").GetString()));
        }

        [TestMethod(DisplayName = "Verify that a stop reply is a valid StartAutomation frame on the Hub's SignalR " +
            "JSON protocol and omits the request.")]
        public void AutomationErrorModelStopFrameTest()
        {
            // Arrange: the reply the hub sends when the automation is stopped, which carries no request.
            const string Message = "Automation stopped";
            var errorModel = new AutomationErrorModel
            {
                Errors = new Dictionary<string, string[]> { ["Automation"] = [Message] }
            };

            // Act: write and parse the frame the same way the designer receives it.
            var frame = WriteStartAutomationFrame(errorModel);
            using var document = JsonDocument.Parse(json: frame);
            var argument = document.RootElement.GetProperty("arguments")[0];

            // Assert: the stop reason is readable, and the absent request is omitted rather than written as null.
            Assert.AreEqual(Message, argument.GetProperty("errors").GetProperty("Automation")[0].GetString());
            Assert.IsFalse(argument.TryGetProperty("request", out _));
        }

        // Writes one StartAutomation invocation through the SignalR JSON hub protocol configured with the Hub's
        // application serializer options, so the test sees the same bytes a client receives. Returns the frame text
        // without its record separator; the helper owns no state and allocates its buffer per call.
        private static string WriteStartAutomationFrame(AutomationErrorModel errorModel)
        {
            // Configure the protocol with the options the Hub registers through AddJsonProtocol.
            var protocolOptions = new JsonHubProtocolOptions { PayloadSerializerOptions = AppSettings.JsonOptions };
            var protocol = new JsonHubProtocol(options: Options.Create(protocolOptions));

            // Serialize the invocation into a buffer, as the hub connection does for Clients.Caller.SendAsync.
            var buffer = new ArrayBufferWriter<byte>();
            var message = new InvocationMessage(target: StartAutomationTarget, arguments: [errorModel]);
            protocol.WriteMessage(message, output: buffer);

            // Return the JSON text without the trailing record separator so the caller can parse it directly.
            return Encoding.UTF8.GetString(buffer.WrittenSpan).TrimEnd(RecordSeparator);
        }
    }
}
