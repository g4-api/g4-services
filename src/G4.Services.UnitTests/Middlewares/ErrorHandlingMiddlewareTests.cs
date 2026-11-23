using G4.Exceptions;
using G4.Services.Domain.V4.Middlewares;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace G4.Services.UnitTests.Middlewares
{
    [TestClass]
    [TestCategory("ErrorHandling")]
    [TestCategory("UnitTest")]
    public sealed class ErrorHandlingMiddlewareTests
    {
        [TestMethod(DisplayName = "Verify a driver session that was not created is answered with 502 and its own " +
            "message, even when the session error is wrapped and wraps the failure of the driver.")]
        public async Task SessionNotCreatedTest()
        {
            // Arrange: the session error as it reaches the hub, wrapped by the task that ran the automation and
            // wrapping the failure of the driver.
            const string message = "The driver 'ChromeDriver' session was not created, so the automation did not run.";
            var driverFailure = new InvalidOperationException("No connection could be made.");
            var sessionError = new G4SessionNotCreatedException(message, innerException: driverFailure);
            var exception = new AggregateException(sessionError);

            // Act: let the request fail with the wrapped session error.
            var (statusCode, body) = await InvokeAsync(exception);
            var error = JsonDocument.Parse(body).RootElement;

            // Assert: the status names an upstream failure, and the client reads the session message, not the
            // conflict status and message of the driver failure under it.
            Assert.AreEqual(expected: StatusCodes.Status502BadGateway, actual: statusCode);
            Assert.AreEqual(expected: message, actual: error.GetProperty("detail").GetString());
            Assert.AreEqual(expected: message, actual: error.GetProperty("errors").GetProperty("SessionNotCreated")[0].GetString());
        }

        [TestMethod(DisplayName = "Verify an exception that is not a session error keeps the status of its root cause.")]
        public async Task OtherExceptionTest()
        {
            // Arrange: a failure with no session error anywhere in its chain.
            var exception = new AggregateException(new InvalidOperationException("The request conflicts."));

            // Act: let the request fail with it.
            var (statusCode, body) = await InvokeAsync(exception);

            // Assert: the root cause still decides the status, and no session error is reported.
            Assert.AreEqual(expected: StatusCodes.Status409Conflict, actual: statusCode);
            Assert.DoesNotContain(substring: "SessionNotCreated", value: body);
        }

        // Runs a request that fails with the given exception through the middleware and returns the status code
        // and the body it answered with.
        private static async Task<(int StatusCode, string Body)> InvokeAsync(Exception exception)
        {
            // Give the request a body stream that can be read back after the middleware wrote the answer.
            var context = new DefaultHttpContext();
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            // Fail the request inside the pipeline, where the middleware catches it.
            var middleware = new ErrorHandlingMiddleware(
                logger: NullLogger<ErrorHandlingMiddleware>.Instance,
                next: _ => throw exception);
            await middleware.InvokeAsync(context);

            return (context.Response.StatusCode, Encoding.UTF8.GetString(responseBody.ToArray()));
        }
    }
}
