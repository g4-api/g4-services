using G4.Models;
using G4.Services.Domain.V4.Extensions;
using G4.Services.Domain.V4.Hubs;
using G4.Services.Domain.V4.Models;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Owns the live white-label branding of the Hub and applies runtime changes to it.
    /// </summary>
    /// <remarks>
    /// Registered as an application singleton next to the resolved <see cref="BrandingSettingsModel"/> it changes in
    /// place. The repository keeps the raw settings, the values as configured or posted, apart from the resolved
    /// model, so a later partial change still derives the empty names from the product name. Changes run one at a
    /// time: each validates, saves the raw settings to <c>appsettings.json</c>, applies the resolved values, rewrites
    /// <c>wwwroot/branding.json</c>, rebrands the API documents, and broadcasts the new version. A failed save leaves
    /// the live branding unchanged; a failed broadcast does not undo an applied change, because clients also read
    /// the branding when they start.
    /// </remarks>
    public sealed class BrandingRepository : IBrandingRepository
    {
        #region *** Constants    ***

        /// <summary>
        /// The SignalR event the notifications hub broadcasts after the branding changed.
        /// </summary>
        /// <remarks>
        /// The payload is an object with the new <c>version</c>; clients read the full settings from the branding
        /// endpoint, so the event stays small and carries no labels.
        /// </remarks>
        public const string BrandingChangedEvent = "ReceiveBrandingChangedEvent";

        // The name of the Hub settings file whose Branding section holds the saved raw settings.
        private const string SettingsFileName = "appsettings.json";

        // The members of the resolved model that the Hub computes; they are never saved to the settings file.
        private static readonly string[] ComputedMembers = ["Tokens", "Version"];

        // Reads the settings file with comments and trailing commas tolerated, as the configuration loader accepts them.
        private static readonly JsonDocumentOptions SettingsDocumentOptions = new()
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };

        // Binds posted or merged settings regardless of member casing, so camel-case clients and the Pascal-case
        // settings file describe the same members.
        private static readonly JsonSerializerOptions SettingsReadOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // The UTF-8 byte order mark, detected so a saved settings file keeps the encoding it had.
        private static readonly byte[] Utf8Preamble = [0xEF, 0xBB, 0xBF];

        #endregion

        #region *** Fields       ***

        // The live resolved branding shared with every consumer; changed in place so held references stay current.
        private readonly BrandingSettingsModel _branding;

        // Serializes changes, so two concurrent updates never interleave their save and apply stages.
        private readonly SemaphoreSlim _changeLock = new(initialCount: 1, maxCount: 1);

        // The paths, serializer options, and notifications hub this repository writes to.
        private readonly RepositoryOptions _options;

        // The raw settings as configured or last posted; the base for partial changes and the saved section.
        private BrandingSettingsModel _settings;

        #endregion

        #region *** Constructors ***

        /// <summary>
        /// Initializes the repository with the live branding and the raw settings it was resolved from.
        /// </summary>
        /// <param name="branding">The resolved branding registered at startup; changed in place by every update.</param>
        /// <param name="configuration">The application configuration whose Branding section holds the raw settings.</param>
        /// <param name="options">The settings file folder, the web root, the serializer options, and the hub context.</param>
        public BrandingRepository(BrandingSettingsModel branding, IConfiguration configuration, RepositoryOptions options)
        {
            // Require every dependency, so a wiring mistake fails at startup instead of at the first update.
            AssertArguments(branding, configuration, options);

            // Keep the live model and the options for the update stages.
            _branding = branding;
            _options = options;

            // Read the raw section, including environment overrides, so partial changes start from the values in use.
            var section = configuration.GetSection(BrandingExtensions.BrandingSection);
            _settings = section.Get<BrandingSettingsModel>() ?? new BrandingSettingsModel();

            return;

            // Asserts the constructor dependencies before any of them is stored. The helper mutates nothing and reports
            // the first missing dependency through a guard exception that names the parameter.
            static void AssertArguments(BrandingSettingsModel branding, IConfiguration configuration, RepositoryOptions options)
            {
                ArgumentNullException.ThrowIfNull(argument: branding);
                ArgumentNullException.ThrowIfNull(argument: configuration);
                ArgumentNullException.ThrowIfNull(argument: options);
            }
        }

        #endregion

        #region *** Methods      ***

        /// <inheritdoc />
        public BrandingSettingsModel GetBranding()
        {
            // Return the shared instance, so the caller publishes exactly what every other consumer reads.
            return _branding;
        }

        /// <inheritdoc />
        public BrandingSettingsModel GetDefaultBranding()
        {
            // Resolve a fresh default model, so no caller can change the defaults another caller reads.
            return BrandingExtensions.NewDefaultBranding();
        }

        /// <inheritdoc />
        public BrandingSettingsModel NewBranding(JsonObject changes)
        {
            // Require the changes, so an empty request body is reported instead of resetting nothing silently.
            if (changes is null)
            {
                var message = "The branding changes are missing; send a JSON object with the members to change.";
                throw new ArgumentException(message, nameof(changes));
            }

            // Start from the saved raw settings without computed members, so the merge only touches configured values.
            var mergedSettings = NewSettingsNode(settings: _settings);

            // Merge the changes member by member, so a nested change keeps its unchanged siblings.
            SetMergedValues(target: mergedSettings, changes);

            // Bind the merged section, and report a member of the wrong type as invalid settings.
            try
            {
                return mergedSettings.Deserialize<BrandingSettingsModel>(SettingsReadOptions) ?? new BrandingSettingsModel();
            }
            catch (JsonException exception)
            {
                var message = $"The branding changes do not describe branding settings: {exception.Message}";
                throw new ArgumentException(message, nameof(changes), exception);
            }
        }

        /// <inheritdoc />
        public async Task<BrandingSettingsModel> ResetBrandingAsync()
        {
            // Save the resolved defaults as raw settings, so the settings file lists every value the Hub uses.
            return await SetBrandingAsync(GetDefaultBranding()).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<BrandingSettingsModel> SetBrandingAsync(BrandingSettingsModel settings)
        {
            // Require the settings, so a missing body never replaces the branding with defaults by accident.
            ArgumentNullException.ThrowIfNull(argument: settings);

            // Resolve a copy and validate it before anything is saved, so invalid settings change nothing.
            var resolved = settings.NewBranding();
            AssertAddresses(resolved, settings);

            // Run one change at a time, so the saved file and the live model always describe the same branding.
            await _changeLock.WaitAsync().ConfigureAwait(false);

            try
            {
                // Save the raw settings first, so a read-only settings file fails the change before it is applied.
                var settingsFilePath = Path.Combine(_options.SettingsFolderPath, SettingsFileName);
                SaveSettingsFile(filePath: settingsFilePath, settings);
                _settings = settings;

                // Apply the resolved values to the shared model, so every holder of it publishes the new labels.
                SetValues(target: _branding, source: resolved);

                // Rewrite the generated branding file; a failed write is ignored, as at startup, because the
                // endpoints publish the same settings.
                _branding.WriteBrandingFile(wwwrootPath: _options.WebRootPath, jsonOptions: _options.JsonOptions);

                // Rebrand the API document headers, which were resolved from the previous branding.
                OpenApiDocuments.SetBranding(_branding);
            }
            finally
            {
                // Release the change lock, so the next change can run even when this one failed.
                _changeLock.Release();
            }

            // Tell the connected clients the branding changed, so they read it again without polling.
            await SendChangedEventAsync(hubContext: _options.NotificationsHubContext, version: _branding.Version).ConfigureAwait(false);

            return _branding;
        }

        // Rejects resolved settings whose published addresses are not absolute URLs. The API documents turn the
        // support and terms addresses into URIs, so a relative or malformed value fails here, before it is saved,
        // instead of at the next document request. The settings parameter names the argument in the diagnostic.
        private static void AssertAddresses(BrandingSettingsModel resolved, BrandingSettingsModel settings)
        {
            // Collect each published address with the member name the operator configures.
            var addresses = new[]
            {
                (Name: "ProjectUrl", Value: resolved.ProjectUrl),
                (Name: "Support.Url", Value: resolved.Support.Url),
                (Name: "TermsUrl", Value: resolved.TermsUrl)
            };

            // Report the first address that is not absolute, naming the member so the operator can fix it.
            foreach (var (name, value) in addresses)
            {
                var isAbsolute = Uri.TryCreate(value, UriKind.Absolute, out _);

                if (!isAbsolute)
                {
                    var message = $"The branding address '{name}' must be an absolute URL, but it is '{value}'.";
                    throw new ArgumentException(message, nameof(settings));
                }
            }
        }

        // Finds the spelling of a member in a JSON object regardless of casing, as the configuration loader matches
        // members. Returns null when the object has no such member, so the caller appends the member as given.
        private static string FindMemberName(JsonObject node, string name)
        {
            return node
                .Select(member => member.Key)
                .FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));
        }

        // Converts raw settings into the JSON shape of the Branding section, without the members the Hub computes.
        // The settings serialize with their Pascal-case member names, matching the configuration file convention.
        private static JsonObject NewSettingsNode(BrandingSettingsModel settings)
        {
            // Serialize the raw settings into a mutable JSON object.
            var node = JsonSerializer.SerializeToNode(settings)?.AsObject() ?? [];

            // Drop the computed members, so a saved file never pins the placeholder map or the version.
            foreach (var member in ComputedMembers)
            {
                node.Remove(member);
            }

            return node;
        }

        // Writes the raw settings into the Branding section of the settings file and keeps every other section, the
        // indentation style, the line endings, the byte order mark, and the final newline the file had. The file is
        // written next to itself and then moved over the original, so a failed write never leaves a truncated file.
        // Comments in the file are not preserved, because the JSON model does not keep them.
        private static void SaveSettingsFile(string filePath, BrandingSettingsModel settings)
        {
            // Read the current file, or start an empty document when the Hub runs without one.
            var fileBytes = File.Exists(filePath) ? File.ReadAllBytes(filePath) : [];
            var isBomPresent = fileBytes.AsSpan().StartsWith(Utf8Preamble);

            // Decode the text without its byte order mark, so the JSON parser and the format detection see plain JSON.
            var textBytes = isBomPresent ? fileBytes[Utf8Preamble.Length..] : fileBytes;
            var fileText = textBytes.Length == 0 ? "{}" : Encoding.UTF8.GetString(textBytes);

            // Parse the document and require an object root, so the Branding section has a place to live.
            if (JsonNode.Parse(fileText, nodeOptions: null, documentOptions: SettingsDocumentOptions) is not JsonObject root)
            {
                var message = $"The settings file '{filePath}' does not hold a JSON object.";
                throw new InvalidOperationException(message);
            }

            // Write the raw settings into the existing Branding section, so its members keep the order the operator
            // wrote them in; a file without the section gets a new one.
            var settingsNode = NewSettingsNode(settings);

            if (root[BrandingExtensions.BrandingSection] is JsonObject section)
            {
                SetSectionValues(section, settingsNode);
            }
            else
            {
                root[BrandingExtensions.BrandingSection] = settingsNode;
            }

            // Match the file's formatting, so the saved file differs from the original only in the Branding section.
            var isTabIndented = fileText.Contains("\n\t");
            var newLine = fileText.Contains("\r\n") ? "\r\n" : "\n";
            var isFinalNewLine = fileText.EndsWith('\n');
            var writeOptions = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                IndentCharacter = isTabIndented ? '\t' : ' ',
                IndentSize = isTabIndented ? 1 : 2,
                NewLine = newLine,
                WriteIndented = true
            };
            var settingsJson = root.ToJsonString(writeOptions) + (isFinalNewLine ? newLine : string.Empty);

            // Write a temporary file and move it over the original, so readers never see a partial file.
            var temporaryPath = $"{filePath}.tmp";
            File.WriteAllText(temporaryPath, settingsJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: isBomPresent));
            File.Move(temporaryPath, filePath, overwrite: true);
        }

        // Broadcasts the branding change to every client of the notifications hub. A Hub without a hub context, such
        // as a unit test, skips the broadcast. A failed broadcast is ignored, because the change is already applied
        // and saved, and every client reads the branding again when it starts.
        private static async Task SendChangedEventAsync(IHubContext<G4AutomationNotificationsHub> hubContext, string version)
        {
            // Skip the broadcast when no hub is available.
            if (hubContext is null)
            {
                return;
            }

            // Send only the version, so clients read the full settings through the branding endpoint.
            try
            {
                await hubContext.Clients.All.SendAsync(BrandingChangedEvent, new { version }).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Keep the applied change; a client that missed the event reads the branding at its next start.
            }
        }

        // Merges a JSON object of changes into a target object, member by member and regardless of member casing.
        // A nested object merges recursively, a null value removes the member so its default applies, and any other
        // value replaces the member. The target is changed in place; the changes are copied, never moved.
        private static void SetMergedValues(JsonObject target, JsonObject changes)
        {
            foreach (var (name, value) in changes)
            {
                // Find the existing member regardless of casing, so a camel-case change replaces a Pascal-case member.
                var memberName = FindMemberName(node: target, name) ?? name;

                // Remove a member set to null, so the resolver fills its default again.
                if (value is null)
                {
                    target.Remove(memberName);
                    continue;
                }

                // Merge a nested object into the existing nested object, so unchanged siblings stay as they are.
                var isNestedMerge = value is JsonObject && target[memberName] is JsonObject;

                if (isNestedMerge)
                {
                    SetMergedValues(target: target[memberName].AsObject(), changes: value.AsObject());
                    continue;
                }

                // Replace every other member with a copy of the changed value.
                target[memberName] = value.DeepClone();
            }
        }

        // Makes an existing settings section hold exactly the values of a replacement section, in place. A member present
        // in both keeps its position, and a nested object is synchronized the same way, so the saved file keeps the
        // operator's member order. A member only in the replacement is appended, and a member only in the section is
        // removed. Members match regardless of casing, as the configuration loader matches them.
        private static void SetSectionValues(JsonObject section, JsonObject replacement)
        {
            // Remove the members the replacement no longer holds, so the section never keeps a stale value.
            var removedNames = section
                .Select(member => member.Key)
                .Where(name => FindMemberName(node: replacement, name) is null)
                .ToArray();

            foreach (var name in removedNames)
            {
                section.Remove(name);
            }

            foreach (var (name, value) in replacement)
            {
                // Find the existing member regardless of casing, so its position and spelling are kept.
                var existingName = FindMemberName(node: section, name);

                // Synchronize a nested object in place, so its members keep their order too.
                var isNestedObject = existingName is not null && value is JsonObject && section[existingName] is JsonObject;

                if (isNestedObject)
                {
                    SetSectionValues(section: section[existingName].AsObject(), replacement: value.AsObject());
                    continue;
                }

                // Replace or append every other member with a copy of the new value.
                section[existingName ?? name] = value?.DeepClone();
            }
        }

        // Copies every resolved value into the shared live model. Nested sections are replaced as whole objects, so a
        // reader that is serializing the model sees either the old or the new section, never a mix of both.
        private static void SetValues(BrandingSettingsModel target, BrandingSettingsModel source)
        {
            // Replace the nested sections and the placeholder map as whole objects.
            target.Assets = source.Assets;
            target.ConsoleBanner = source.ConsoleBanner;
            target.Names = source.Names;
            target.Support = source.Support;
            target.Tokens = source.Tokens;

            // Replace the plain values, ending with the version, so the version changes after the values it describes.
            target.ProductName = source.ProductName;
            target.ProjectUrl = source.ProjectUrl;
            target.Tagline = source.Tagline;
            target.TermsUrl = source.TermsUrl;
            target.TrademarkSymbol = source.TrademarkSymbol;
            target.Version = source.Version;
        }

        #endregion

        #region *** Nested Types ***

        /// <summary>
        /// Holds the locations and services the branding repository writes to.
        /// </summary>
        /// <remarks>
        /// The Hub fills these values from its host environment at startup. Tests point the folders at a temporary
        /// directory and leave the hub context empty, which skips the change broadcast.
        /// </remarks>
        public sealed class RepositoryOptions
        {
            /// <summary>
            /// Gets or sets the serializer options used for <c>wwwroot/branding.json</c>, matching the endpoints' shape.
            /// </summary>
            public JsonSerializerOptions JsonOptions { get; set; }

            /// <summary>
            /// Gets or sets the notifications hub context that broadcasts the branding change.
            /// </summary>
            /// <remarks>
            /// Null skips the broadcast, for hosts and tests without SignalR.
            /// </remarks>
            public IHubContext<G4AutomationNotificationsHub> NotificationsHubContext { get; set; }

            /// <summary>
            /// Gets or sets the folder that holds the Hub's <c>appsettings.json</c>, normally the content root.
            /// </summary>
            public string SettingsFolderPath { get; set; }

            /// <summary>
            /// Gets or sets the web root that holds the generated <c>branding.json</c>.
            /// </summary>
            /// <remarks>
            /// A missing folder skips the generated file, as it does at startup.
            /// </remarks>
            public string WebRootPath { get; set; }
        }

        #endregion
    }
}
