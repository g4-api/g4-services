using G4.Models;
using G4.Services.Domain.V4.Extensions;
using G4.Settings;

using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace G4.Services.UnitTests.Extensions
{
    [TestClass]
    [TestCategory("Branding")]
    [TestCategory("UnitTest")]
    public sealed class BrandingExtensionsTests
    {
        [TestMethod(DisplayName = "Verify a missing Branding section resolves to the current G4 labels.")]
        public void NewBrandingDefaultsTest()
        {
            // Arrange: a configuration without a Branding section, as a deployment that never configured branding.
            var configuration = NewConfiguration([]);

            // Act: resolve the branding.
            var branding = configuration.NewBranding();

            // Assert: every name and marked name keeps today's G4 wording.
            Assert.AreEqual("G4", branding.ProductName);
            Assert.AreEqual("™", branding.TrademarkSymbol);
            Assert.AreEqual("G4 Hub", branding.Names.Hub);
            Assert.AreEqual("G4™ Engine", branding.Tokens["engineTm"]);
            Assert.AreEqual("G4™ Test Wright", branding.Tokens["testWrightTm"]);
            Assert.AreEqual("G4™", branding.Tokens["productTm"]);
            Assert.IsFalse(branding.Tokens.ContainsKey("mark"));

            // Assert: the images, the support contact, and the banner mode take their defaults; the extension icons stay
            // empty, so each extension keeps its built-in icon.
            Assert.AreEqual("/images/avatar.png", branding.Assets.Logo);
            Assert.AreEqual("/images/favicon.ico", branding.Assets.Favicon);
            Assert.AreEqual(string.Empty, branding.Assets.ActivityBarIcon);
            Assert.AreEqual(string.Empty, branding.Assets.TestWrightIcon);
            Assert.AreEqual("G4 Support", branding.Support.Name);
            Assert.AreEqual(BrandingBannerModes.Default, branding.ConsoleBanner.Mode);
        }

        [TestMethod(DisplayName = "Verify the product name alone renames every product, and a configured name wins.")]
        public void NewBrandingProductNameTest()
        {
            // Arrange: a product name, one explicit name that does not contain the product name, and padded icon paths.
            var configuration = NewConfiguration(new Dictionary<string, string>
            {
                ["Branding:Assets:ActivityBarIcon"] = " /images/acme-activity.svg ",
                ["Branding:Assets:TestWrightIcon"] = "https://cdn.acme.example/test-wright.svg",
                ["Branding:Names:Hub"] = "Control Center",
                ["Branding:ProductName"] = "Acme"
            });

            // Act: resolve the branding.
            var branding = configuration.NewBranding();

            // Assert: the empty names are derived from the product name and marked after it.
            Assert.AreEqual("Acme Engine", branding.Names.Engine);
            Assert.AreEqual("Acme™ Engine", branding.Tokens["engineTm"]);
            Assert.AreEqual("Acme Studio", branding.Tokens["studio"]);

            // Assert: the extension icons are kept as configured, without surrounding spaces.
            Assert.AreEqual("/images/acme-activity.svg", branding.Assets.ActivityBarIcon);
            Assert.AreEqual("https://cdn.acme.example/test-wright.svg", branding.Assets.TestWrightIcon);

            // Assert: the configured name is kept, and its symbol is appended because it holds no product name.
            Assert.AreEqual("Control Center", branding.Tokens["hub"]);
            Assert.AreEqual("Control Center™", branding.Tokens["hubTm"]);
        }

        [TestMethod(DisplayName = "Verify an empty trademark symbol removes the symbol from every marked name.")]
        public void NewBrandingEmptyTrademarkTest()
        {
            // Arrange: an explicit empty symbol, as an operator who does not hold the trademark.
            var configuration = NewConfiguration(new Dictionary<string, string>
            {
                ["Branding:ProductName"] = "Acme",
                ["Branding:TrademarkSymbol"] = string.Empty
            });

            // Act: resolve the branding.
            var branding = configuration.NewBranding();

            // Assert: the marked names equal the plain names.
            Assert.AreEqual(string.Empty, branding.Tokens["tm"]);
            Assert.AreEqual("Acme Engine", branding.Tokens["engineTm"]);
            Assert.AreEqual("Acme", branding.Tokens["productTm"]);
        }

        [TestMethod(DisplayName = "Verify the branding script assigns the resolved settings to the page global.")]
        public void ConvertToBrandingScriptTest()
        {
            // Arrange: the default branding.
            var branding = NewConfiguration([]).NewBranding();

            // Act: convert the branding into the script the Hub pages load first.
            var script = branding.ConvertToBrandingScript(jsonOptions: AppSettings.JsonOptions);

            // Assert: the script sets the global to camel-cased settings that keep the trademark symbol intact.
            Assert.StartsWith("globalThis.G4BrandingSettings = {", script);
            Assert.EndsWith("};", script);
            Assert.Contains("\"engineTm\":\"G4™ Engine\"", script);
        }

        #region *** Data Set ***
        [DataRow("Powered by {engineTm}", "Powered by G4™ Engine")]
        [DataRow("{product} Capabilities", "G4 Capabilities")]
        [DataRow("Route /api/v4/g4/flows/{key} stays", "Route /api/v4/g4/flows/{key} stays")]
        [DataRow("G4AutomationModel and G4.System stay", "G4AutomationModel and G4.System stay")]
        [DataRow("{hub}{tm}", "G4 Hub™")]
        [DataRow("", "")]
        #endregion
        [TestMethod(DisplayName = "Verify ResolveText replaces known placeholders and keeps unknown braces and " +
            "identifiers as written.")]
        public void ResolveTextTest(string text, string expected)
        {
            // Arrange: the default branding.
            var branding = NewConfiguration([]).NewBranding();

            // Act: resolve the text.
            var resolvedText = branding.ResolveText(text);

            // Assert: only known placeholders change.
            Assert.AreEqual(expected, resolvedText);
        }

        [TestMethod(DisplayName = "Verify WriteBrandingFile writes branding.json to the web root and skips a " +
            "missing web root.")]
        public void WriteBrandingFileTest()
        {
            // Arrange: a branded product and a temporary web root that this test owns.
            var branding = NewConfiguration(new Dictionary<string, string> { ["Branding:ProductName"] = "Acme" }).NewBranding();
            var wwwrootPath = Path.Combine(Path.GetTempPath(), $"g4-branding-{Guid.NewGuid():N}");
            Directory.CreateDirectory(wwwrootPath);

            try
            {
                // Act: write the file, and try again against a web root that does not exist.
                var filePath = branding.WriteBrandingFile(wwwrootPath, jsonOptions: AppSettings.JsonOptions);
                var missingFilePath = branding.WriteBrandingFile(Path.Combine(wwwrootPath, "missing"), jsonOptions: AppSettings.JsonOptions);

                // Assert: the file holds the resolved settings, and the missing web root writes nothing.
                using var document = JsonDocument.Parse(File.ReadAllText(filePath));
                var tokens = document.RootElement.GetProperty("tokens");

                Assert.AreEqual(Path.Combine(wwwrootPath, BrandingExtensions.BrandingFileName), filePath);
                Assert.AreEqual("Acme™ Engine", tokens.GetProperty("engineTm").GetString());
                Assert.IsNull(missingFilePath);
            }
            finally
            {
                // Cleanup: remove the temporary web root, so test runs never share files.
                Directory.Delete(wwwrootPath, recursive: true);
            }
        }

        // Builds an in-memory configuration from flat keys, as appsettings.json and Branding__* environment
        // variables produce them.
        private static IConfiguration NewConfiguration(Dictionary<string, string> values)
        {
            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }
    }
}
