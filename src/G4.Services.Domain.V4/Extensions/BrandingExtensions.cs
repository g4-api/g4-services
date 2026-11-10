using G4.Models;

using Microsoft.Extensions.Configuration;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace G4.Services.Domain.V4.Extensions
{
    /// <summary>
    /// Reads, resolves, and publishes the white-label settings that replace the user-facing product labels.
    /// </summary>
    /// <remarks>
    /// The Hub resolves the settings once at startup. Authored text names a product through a placeholder such as
    /// <c>{engineTm}</c> instead of the literal "G4™ Engine", and <see cref="ResolveText"/> replaces the
    /// placeholders. Only placeholders listed in <see cref="BrandingSettingsModel.Tokens"/> are replaced, so
    /// identifiers such as <c>G4AutomationModel</c>, <c>G4.System</c>, or <c>/api/v4/g4</c> are never touched.
    /// </remarks>
    public static class BrandingExtensions
    {
        #region *** Constants    ***

        /// <summary>
        /// The name of the file the Hub writes under <c>wwwroot</c> with the resolved settings.
        /// </summary>
        public const string BrandingFileName = "branding.json";

        /// <summary>
        /// The name of the configuration section that holds the white-label settings.
        /// </summary>
        public const string BrandingSection = "Branding";

        // The favicon served when the settings name none.
        private const string DefaultFavicon = "/images/favicon.ico";

        // The logo served when the settings name none.
        private const string DefaultLogo = "/images/avatar.png";

        // The product name used when the settings name none, which keeps every label as it was before branding.
        private const string DefaultProductName = "G4";

        // The project home page shown in the console banner when the settings name none.
        private const string DefaultProjectUrl = "https://github.com/g4-api";

        // The support email published in the API documentation when the settings name none.
        private const string DefaultSupportEmail = "g4.platforms@gmail.com";

        // The support address published in the API documentation when the settings name none.
        private const string DefaultSupportUrl = "https://github.com/g4-api/g4-docs";

        // The tagline shown in the console banner when the settings name none.
        private const string DefaultTagline = "Automation as a Service";

        // The terms of service address published in the API documentation when the settings name none.
        private const string DefaultTermsUrl = "https://github.com/g4-api/g4-docs/blob/main/responsible-use/usage-policy.md";

        // The trademark symbol used when the settings leave it unset; an empty setting removes the symbol.
        private const string DefaultTrademarkSymbol = "™";

        // Indents the branding file and keeps symbols such as the trademark sign readable instead of escaped.
        private static readonly JsonSerializerOptions IndentedJsonOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

        // Matches one placeholder such as {engineTm}. The name is a bounded run of word characters between braces,
        // so the match runs in linear time and never spans text outside one pair of braces.
        private static readonly Regex TokenPattern = new(pattern: @"\{(\w{1,64})\}", RegexOptions.CultureInvariant);

        #endregion

        #region *** Methods      ***

        /// <summary>
        /// Converts the resolved settings into the script the Hub pages load before any other script.
        /// </summary>
        /// <param name="branding">The resolved settings.</param>
        /// <param name="jsonOptions">The JSON options that shape the published settings.</param>
        /// <returns>A script that sets <c>globalThis.G4BrandingSettings</c> to the settings.</returns>
        /// <remarks>
        /// The pages load this script synchronously, so the branding is known before the first label is drawn.
        /// </remarks>
        public static string ConvertToBrandingScript(this BrandingSettingsModel branding, JsonSerializerOptions jsonOptions)
        {
            // Serialize the settings with the shared options, so the script and the JSON endpoint publish one shape.
            var settingsJson = JsonSerializer.Serialize(branding, jsonOptions);

            // Assign the settings to a global the branding component reads when it starts.
            return $"globalThis.G4BrandingSettings = {settingsJson};";
        }

        /// <summary>
        /// Reads the white-label settings from the <c>Branding</c> configuration section and resolves every value.
        /// </summary>
        /// <param name="configuration">The application configuration, including environment variable overrides.</param>
        /// <returns>The resolved settings, with every name, address, and placeholder value filled.</returns>
        /// <remarks>
        /// A missing section or a missing value falls back to the current G4 text. Each empty name is derived from
        /// the product name, and the placeholder map is rebuilt from the resolved names.
        /// </remarks>
        public static BrandingSettingsModel NewBranding(this IConfiguration configuration)
        {
            // Require the configuration so a wiring mistake fails at startup instead of silently using defaults.
            ArgumentNullException.ThrowIfNull(argument: configuration);

            // Bind the configured section, or start from an empty model so every value takes its default.
            var branding = configuration.GetSection(BrandingSection).Get<BrandingSettingsModel>() ?? new BrandingSettingsModel();

            // Resolve the values in place, so every consumer reads complete settings from one model.
            return ResolveBranding(branding);
        }

        /// <summary>
        /// Resolves the default branding, which keeps every label as the current G4 text.
        /// </summary>
        /// <returns>The resolved default branding.</returns>
        /// <remarks>
        /// Used where no configured branding is registered, such as tests and tool definitions built outside the Hub.
        /// </remarks>
        public static BrandingSettingsModel NewDefaultBranding()
        {
            // Resolve an empty configuration, so every value takes its default.
            return new ConfigurationBuilder().Build().NewBranding();
        }

        /// <summary>
        /// Replaces every known placeholder in the text with its branded value.
        /// </summary>
        /// <param name="branding">The resolved settings that hold the placeholder values.</param>
        /// <param name="text">The text to resolve; null or empty text is returned unchanged.</param>
        /// <returns>The text with every known placeholder replaced.</returns>
        /// <remarks>
        /// An unknown placeholder is kept as written, so text that uses braces for another purpose, such as a route
        /// template, is never changed.
        /// </remarks>
        public static string ResolveText(this BrandingSettingsModel branding, string text)
        {
            // Return empty text unchanged, so callers can resolve optional values without their own guard.
            var isNothingToResolve = string.IsNullOrEmpty(text) || branding is null;

            if (isNothingToResolve)
            {
                return text;
            }

            // Replace each known placeholder with its value and keep every other braced text as written.
            return TokenPattern.Replace(text, match =>
            {
                var isKnown = branding.Tokens.TryGetValue(match.Groups[1].Value, out var value);
                return isKnown ? value : match.Value;
            });
        }

        /// <summary>
        /// Writes the resolved settings to <c>branding.json</c> under the web root.
        /// </summary>
        /// <param name="branding">The resolved settings.</param>
        /// <param name="wwwrootPath">The absolute path of the web root.</param>
        /// <param name="jsonOptions">The JSON options that shape the published settings.</param>
        /// <returns>The path of the written file, or null when the web root is missing or cannot be written.</returns>
        /// <remarks>
        /// The file lets clients that download the web root, such as the VS Code designer, read the branding with the
        /// other static files. A failed write is reported to the caller and never stops the Hub, because the
        /// branding endpoints publish the same settings.
        /// </remarks>
        public static string WriteBrandingFile(this BrandingSettingsModel branding, string wwwrootPath, JsonSerializerOptions jsonOptions)
        {
            // Skip a host without a web root, such as a worker, since nothing serves the file there.
            var isWebRootMissing = string.IsNullOrEmpty(wwwrootPath) || !Directory.Exists(wwwrootPath);

            if (isWebRootMissing)
            {
                return null;
            }

            // Shape the settings with the shared options, then indent them, so an operator can read the file.
            var settingsJson = JsonSerializer.SerializeToNode(branding, jsonOptions).ToJsonString(IndentedJsonOptions);
            var filePath = Path.Combine(wwwrootPath, BrandingFileName);

            // Write the settings, and report a read-only or locked web root as no file instead of failing startup.
            try
            {
                File.WriteAllText(filePath, settingsJson);
                return filePath;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Inserts the trademark symbol right after the first occurrence of the product name, or appends it when the
        // name does not contain the product name. An empty symbol leaves the name unchanged.
        private static string ConvertToMarkedName(string name, string productName, string trademarkSymbol)
        {
            // Leave the name unchanged when there is no symbol to place.
            if (string.IsNullOrEmpty(trademarkSymbol))
            {
                return name;
            }

            // Append the symbol when the product name is not part of this name, for example a custom Hub name.
            var productIndex = name.IndexOf(productName, StringComparison.Ordinal);

            if (productIndex < 0)
            {
                return $"{name}{trademarkSymbol}";
            }

            // Place the symbol after the product name, so "G4 Engine" becomes "G4™ Engine".
            var markIndex = productIndex + productName.Length;
            return name.Insert(markIndex, trademarkSymbol);
        }

        // Returns the configured value, or the fallback when the value is missing or blank.
        private static string GetValueOrDefault(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        // Builds the placeholder map from the resolved names. Every product name has a plain placeholder and a marked
        // placeholder with the trademark symbol, so authored text keeps today's exact wording, for example "G4™ Engine".
        private static Dictionary<string, string> NewTokens(BrandingSettingsModel branding)
        {
            // Collect the plain names once, so the plain and marked placeholders come from the same values.
            var names = new Dictionary<string, string>
            {
                ["engine"] = branding.Names.Engine,
                ["engineClient"] = branding.Names.EngineClient,
                ["hub"] = branding.Names.Hub,
                ["product"] = branding.ProductName,
                ["studio"] = branding.Names.Studio,
                ["testWright"] = branding.Names.TestWright
            };

            // Start the map with the trademark symbol, which has no marked form.
            var tokens = new Dictionary<string, string>
            {
                ["tm"] = branding.TrademarkSymbol
            };

            // Add each name in its plain and marked form.
            foreach (var (key, name) in names)
            {
                tokens[key] = name;
                tokens[$"{key}Tm"] = ConvertToMarkedName(name, branding.ProductName, branding.TrademarkSymbol);
            }

            return tokens;
        }

        // Fills every missing value of the bound settings in place and rebuilds the placeholder map.
        private static BrandingSettingsModel ResolveBranding(BrandingSettingsModel branding)
        {
            // Normalize the nested sections, so a partial configuration never leaves a null section behind.
            branding.Assets ??= new BrandingAssetsModel();
            branding.ConsoleBanner ??= new BrandingBannerModel();
            branding.Names ??= new BrandingNamesModel();
            branding.Support ??= new BrandingSupportModel();

            // Resolve the product name and the symbol first, because every derived name depends on them. A null
            // symbol takes the default, while an explicit empty symbol removes it.
            var productName = GetValueOrDefault(branding.ProductName, DefaultProductName);
            branding.ProductName = productName;
            branding.TrademarkSymbol = branding.TrademarkSymbol?.Trim() ?? DefaultTrademarkSymbol;

            // Derive each missing product name from the product name.
            branding.Names.Engine = GetValueOrDefault(branding.Names.Engine, $"{productName} Engine");
            branding.Names.EngineClient = GetValueOrDefault(branding.Names.EngineClient, $"{productName} Engine Client");
            branding.Names.Hub = GetValueOrDefault(branding.Names.Hub, $"{productName} Hub");
            branding.Names.Studio = GetValueOrDefault(branding.Names.Studio, $"{productName} Studio");
            branding.Names.TestWright = GetValueOrDefault(branding.Names.TestWright, $"{productName} Test Wright");

            // Resolve the images; an empty extension icon keeps the icon built into each extension.
            branding.Assets.ActivityBarIcon = branding.Assets.ActivityBarIcon?.Trim() ?? string.Empty;
            branding.Assets.Favicon = GetValueOrDefault(branding.Assets.Favicon, DefaultFavicon);
            branding.Assets.Logo = GetValueOrDefault(branding.Assets.Logo, DefaultLogo);
            branding.Assets.TestWrightIcon = branding.Assets.TestWrightIcon?.Trim() ?? string.Empty;

            // Resolve the banner mode, so an empty or unknown mode writes the built-in banner.
            branding.ConsoleBanner.Mode = GetValueOrDefault(branding.ConsoleBanner.Mode, BrandingBannerModes.Default);

            // Resolve the addresses and the contact published in the console banner and the API documentation.
            branding.ProjectUrl = GetValueOrDefault(branding.ProjectUrl, DefaultProjectUrl);
            branding.Support.Email = GetValueOrDefault(branding.Support.Email, DefaultSupportEmail);
            branding.Support.Name = GetValueOrDefault(branding.Support.Name, $"{productName} Support");
            branding.Support.Url = GetValueOrDefault(branding.Support.Url, DefaultSupportUrl);
            branding.Tagline = GetValueOrDefault(branding.Tagline, DefaultTagline);
            branding.TermsUrl = GetValueOrDefault(branding.TermsUrl, DefaultTermsUrl);

            // Rebuild the placeholder map from the resolved names, replacing any configured map.
            branding.Tokens = NewTokens(branding);

            return branding;
        }

        #endregion
    }
}
