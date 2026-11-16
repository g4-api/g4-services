using G4.Models;
using G4.Services.Domain.V4.Extensions;
using G4.Services.Domain.V4.Repositories;
using G4.Settings;

using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace G4.Services.UnitTests.Repositories
{
    [TestClass]
    [TestCategory("Branding")]
    [TestCategory("UnitTest")]
    public sealed class BrandingRepositoryTests
    {
        // The tab-indented settings file every test starts from, with one unrelated section that must survive a save.
        private const string SettingsFileText = "{\n\t\"Branding\": {\n\t\t\"ProductName\": \"G4\"\n\t},\n\t\"Other\": {\n\t\t\"Keep\": true\n\t}\n}\n";

        // The temporary folder this test owns; it holds appsettings.json and the wwwroot folder.
        private string _folderPath;

        [TestInitialize]
        public void InitializeFolder()
        {
            // Arrange: a private folder with a settings file and a web root, so tests never share files.
            _folderPath = Path.Combine(Path.GetTempPath(), $"g4-branding-repository-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(_folderPath, "wwwroot"));
            File.WriteAllText(Path.Combine(_folderPath, "appsettings.json"), SettingsFileText);
        }

        [TestCleanup]
        public void RemoveFolder()
        {
            // Cleanup: remove the temporary folder, so test runs never see each other's files.
            Directory.Delete(_folderPath, recursive: true);
        }

        [TestMethod(DisplayName = "Verify SetBrandingAsync changes the shared model in place, saves the raw section, " +
            "keeps other sections and tabs, and rewrites branding.json.")]
        public async Task SetBrandingTest()
        {
            // Arrange: the startup branding and a repository over the temporary folder.
            var (branding, repository) = NewRepository();
            var previousVersion = branding.Version;

            // Act: replace the branding with a new product name only.
            var result = await repository.SetBrandingAsync(new BrandingSettingsModel { ProductName = "MLM" });

            // Assert: the startup instance itself carries the new labels and a new version.
            Assert.AreSame(branding, result);
            Assert.AreEqual("MLM™ Engine", branding.Tokens["engineTm"]);
            Assert.AreEqual("MLM Hub", branding.Names.Hub);
            Assert.AreNotEqual(previousVersion, branding.Version);

            // Assert: the saved section holds the raw values without computed members, keeps the operator's member
            // order with the product name first, and the file keeps the other section and its tab indentation.
            var settingsText = File.ReadAllText(Path.Combine(_folderPath, "appsettings.json"));
            var settings = JsonNode.Parse(settingsText).AsObject();

            Assert.AreEqual("MLM", settings["Branding"]["ProductName"].GetValue<string>());
            Assert.AreEqual("ProductName", settings["Branding"].AsObject().First().Key);
            Assert.IsFalse(settings["Branding"].AsObject().ContainsKey("Tokens"));
            Assert.IsFalse(settings["Branding"].AsObject().ContainsKey("Version"));
            Assert.IsTrue(settings["Other"]["Keep"].GetValue<bool>());
            Assert.Contains("\n\t\"Other\"", settingsText);

            // Assert: the generated branding file publishes the new version.
            var brandingFilePath = Path.Combine(_folderPath, "wwwroot", BrandingExtensions.BrandingFileName);
            using var document = JsonDocument.Parse(File.ReadAllText(brandingFilePath));
            Assert.AreEqual(branding.Version, document.RootElement.GetProperty("version").GetString());
        }

        [TestMethod(DisplayName = "Verify SetBrandingAsync rejects a relative address and leaves the branding and " +
            "the settings file unchanged.")]
        public async Task SetBrandingInvalidAddressTest()
        {
            // Arrange: the startup branding and a settings model with a relative terms address.
            var (branding, repository) = NewRepository();
            var previousVersion = branding.Version;
            var settings = new BrandingSettingsModel { ProductName = "MLM", TermsUrl = "terms.html" };

            // Act: try to apply the invalid settings.
            var exception = await Assert.ThrowsExactlyAsync<ArgumentException>(() => repository.SetBrandingAsync(settings));

            // Assert: the failure names the member, and nothing was applied or saved.
            Assert.Contains("TermsUrl", exception.Message);
            Assert.AreEqual(previousVersion, branding.Version);
            Assert.AreEqual(SettingsFileText, File.ReadAllText(Path.Combine(_folderPath, "appsettings.json")));
        }

        [TestMethod(DisplayName = "Verify NewBranding merges camel-case changes into the saved settings and a null " +
            "member returns to its default.")]
        public async Task NewBrandingMergeTest()
        {
            // Arrange: a saved branding with a product name and a custom Hub name.
            var (branding, repository) = NewRepository();
            await repository.SetBrandingAsync(new BrandingSettingsModel { ProductName = "MLM", Names = new BrandingNamesModel { Hub = "Control" } });

            // Act: change the tagline and remove the custom Hub name, then apply the merged settings.
            var changes = JsonNode.Parse("{ \"tagline\": \"Bots for all\", \"names\": { \"hub\": null } }").AsObject();
            var merged = repository.NewBranding(changes);
            await repository.SetBrandingAsync(merged);

            // Assert: the product name survives, the tagline changes, and the Hub name is derived again.
            Assert.AreEqual("MLM", branding.ProductName);
            Assert.AreEqual("Bots for all", branding.Tagline);
            Assert.AreEqual("MLM Hub", branding.Names.Hub);
        }

        [TestMethod(DisplayName = "Verify ResetBrandingAsync restores the G4 labels and saves every default explicitly.")]
        public async Task ResetBrandingTest()
        {
            // Arrange: a repository whose branding was changed to another product.
            var (branding, repository) = NewRepository();
            await repository.SetBrandingAsync(new BrandingSettingsModel { ProductName = "MLM" });

            // Act: reset the branding.
            await repository.ResetBrandingAsync();

            // Assert: the labels are the G4 defaults again, and the saved section lists the derived names explicitly.
            var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(_folderPath, "appsettings.json")));

            Assert.AreEqual("G4™ Engine", branding.Tokens["engineTm"]);
            Assert.AreEqual("G4 Test Wright", settings["Branding"]["Names"]["TestWright"].GetValue<string>());
            Assert.AreEqual(BrandingExtensions.NewDefaultBranding().Version, branding.Version);
        }

        [TestMethod(DisplayName = "Verify the version is stable for equal settings and changes with any published value.")]
        public void BrandingVersionTest()
        {
            // Arrange: two equal settings and one with a different tagline.
            var first = new BrandingSettingsModel { ProductName = "MLM" }.NewBranding();
            var second = new BrandingSettingsModel { ProductName = "MLM" }.NewBranding();
            var changed = new BrandingSettingsModel { ProductName = "MLM", Tagline = "Other" }.NewBranding();

            // Assert: equal settings share one version, and the changed tagline produces another one.
            Assert.AreEqual(16, first.Version.Length);
            Assert.AreEqual(first.Version, second.Version);
            Assert.AreNotEqual(first.Version, changed.Version);
        }

        // Builds the startup branding from the temporary settings file and a repository that owns it, the same way the
        // Hub wires them, without a notifications hub so no broadcast is attempted.
        private (BrandingSettingsModel Branding, BrandingRepository Repository) NewRepository()
        {
            // Load the temporary settings file, so the repository starts from the raw section on disk.
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(_folderPath, "appsettings.json"), optional: false, reloadOnChange: false)
                .Build();

            // Resolve the startup branding and wire the repository to the temporary folders.
            var branding = configuration.NewBranding();
            var options = new BrandingRepository.RepositoryOptions
            {
                JsonOptions = AppSettings.JsonOptions,
                SettingsFolderPath = _folderPath,
                WebRootPath = Path.Combine(_folderPath, "wwwroot")
            };

            return (branding, new BrandingRepository(branding, configuration, options));
        }
    }
}
