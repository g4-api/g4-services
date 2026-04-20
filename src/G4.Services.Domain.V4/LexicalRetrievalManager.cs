using G4.Cache;
using G4.Extensions;
using G4.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace G4.Services.Domain.V4
{
    /// <summary>
    /// Provides lexical retrieval over cached plugin tools in order to find the most
    /// relevant matches for a prompt.
    /// </summary>
    public sealed class LexicalRetrievalManager
    {
        #region *** Fields       ***
        // Holds a reference to the cache manager used to build the retrieval catalog.
        private readonly CacheManager _cache;

        // Defines the plugin types that are allowed to participate in lexical retrieval.
        private readonly string[] _included;

        // Holds the raw plugin manifests from the cache for quick access during retrieval.
        private readonly Dictionary<string, IG4PluginManifest> _manifests;

        // Holds the initialized in-memory example catalog built from the supplied cache,
        // keyed by "{namespace.}toolName".
        private readonly Dictionary<string, ExampleScoreModel[]> _examples;

        // Holds the initialized in-memory tool catalog built from the supplied cache.
        private readonly IEnumerable<ToolScoreModel> _tools;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of <see cref="LexicalRetrievalManager"/> including only tools of type "Action".
        /// </summary>
        /// <param name="cache">The <see cref="CacheManager"/> instance containing cached tools.</param>
        public LexicalRetrievalManager(CacheManager cache)
            : this(cache, "Action")
        { }

        /// <summary>
        /// Initializes a new instance of <see cref="LexicalRetrievalManager"/> with specified tool types to include in the catalog.
        /// </summary>
        /// <param name="cache">The <see cref="CacheManager"/> instance containing cached tools.</param>
        /// <param name="included">Array of tool types to include (e.g., "Action", "Flow"). If null or empty, defaults to ["Action"].</param>
        public LexicalRetrievalManager(CacheManager cache, params string[] included)
        {
            // Store the supplied cache reference for use in catalog
            // initialization and potential future cache access.
            _cache = cache;

            // Set included tool types; default to ["Action"] if none provided
            _included = included == null || included.Length == 0
                ? ["Action"]
                : included;

            // Build a lookup dictionary of plugin manifests from the
            // cache for quick access during retrieval.
            var a = _cache
                .PluginsCache
                .Values
                .SelectMany(i => i.Values)
                .Select(i => i.Manifest);

            _manifests ??= new Dictionary<string, IG4PluginManifest>(StringComparer.OrdinalIgnoreCase);

            foreach (var i in a)
            {
                var key = $"{(string.IsNullOrEmpty(i.Namespace) ? "" : $"{i.Namespace}.")}{i.Key}";
                _manifests[key] = i;
            }

            //.ToDictionary(i => $"{(string.IsNullOrEmpty(i.Namespace) ? "" : $"{i.Namespace}.")}{i.Key}", i => i);

            // Initialize the tool catalog from cache for the included types
            _tools = InitializeCatalog(cache, _included);

            // Initialize the example catalog from cache for the included types
            _examples = InitializeExamples(cache, _included);
        }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Finds the most relevant examples for the supplied prompt within the specified tool.
        /// </summary>
        /// <param name="toolName">The key name of the tool whose examples are searched.</param>
        /// <param name="namespace">The namespace of the tool.</param>
        /// <param name="prompt">The user prompt used to score and rank matching examples.</param>
        /// <param name="take">The maximum number of matching examples to return. Defaults to 3.</param>
        /// <returns>A ResultModel whose <c>Examples</c> array contains the scored examples ordered by descending relevance. Only examples with a positive score are returned.</returns>
        public ResultModel FindExamples(string toolName, string @namespace, string prompt, int take = 3)
        {
            // Build the lookup key using the same format as the example catalog.
            var key = $"{(string.IsNullOrEmpty(@namespace)
                ? ""
                : $"{@namespace}.")}{toolName}";

            // Retrieve the pre-built example catalog entries for this tool.
            // Return an empty result when the tool key is not found in the catalog.
            if (!_examples.TryGetValue(key, out ExampleScoreModel[] catalogEntries))
            {
                return new ResultModel { Examples = [] };
            }

            // Normalize the incoming prompt so matching is performed against a consistent text format.
            var normalizedPrompt = Normalize(prompt);

            // Split the normalized prompt into tokens used for field-level scoring.
            var promptTokens = FormatTokens(normalizedPrompt).ToArray();

            // Retrieve the tool description once so it can be attached to every result entry.
            _manifests.TryGetValue(key, out IG4PluginManifest manifest);
            var toolDescription = manifest == null ? string.Empty : string.Join('\n', manifest.Summary);

            // Score each catalog entry against the prompt. Each entry already carries the original
            // PluginExampleModel data and the normalized fields; only the score is computed here.
            // The catalog entries are never mutated — a new ExampleScoreResultModel is produced
            // for each entry and the catalog entry itself is surfaced as the Example payload.
            var scored = catalogEntries
                .Select(entry => new ExampleScoreResultModel
                {
                    // Attach the tool-level context to every result entry.
                    Description = toolDescription,
                    Name        = toolName,
                    NameSpace   = @namespace,

                    // Surface the catalog entry as the example payload.
                    // It holds the original PluginExampleModel data alongside the normalized fields.
                    Example = entry,

                    // Compute the relevance score for this example against the prompt.
                    Score = SetExampleScore(entry, normalizedPrompt, promptTokens)
                })
                .Where(e => e.Score > 0)
                .OrderByDescending(e => e.Score)
                .Take(take)
                .ToArray();

            return new ResultModel { Examples = scored };
        }

        /// <summary>
        /// Finds the most relevant tools for the supplied prompt using lexical scoring.
        /// </summary>
        /// <param name="prompt">The user prompt used to score and rank matching tools.</param>
        /// <returns>
        /// A sequence of <see cref="ToolScoreResultModel"/> entries ordered by descending relevance score.
        /// Only tools with a positive score are returned.
        /// </returns>
        public ResultModel FindTools(string prompt)
        {
            return FindTools(prompt, take: 3);
        }

        /// <summary>
        /// Finds the most relevant tools for the supplied prompt using lexical scoring.
        /// </summary>
        /// <param name="prompt">The user prompt used to score and rank matching tools.</param>
        /// <param name="take">The maximum number of matching tools to return. Defaults to 3.</param>
        /// <returns>
        /// A sequence of <see cref="ToolScoreResultModel"/> entries ordered by descending relevance score.
        /// Only tools with a positive score are returned.
        /// </returns>
        public ResultModel FindTools(string prompt, int take = 3)
        {
            // Normalize the incoming prompt so matching is performed against a consistent text format.
            var normalizedPrompt = Normalize(prompt);

            // Split the normalized prompt into tokens used for field-level scoring.
            var promptTokens = FormatTokens(normalizedPrompt).ToArray();

            // Score all cataloged tools, keep only positive matches, sort by relevance,
            // and return up to the requested number of results.
            var results = new List<ToolScoreResultModel>();

            // Evaluate each tool in the initialized catalog against
            // the normalized prompt and its tokens.
            foreach (var tool in _tools)
            {
                // Initialize a new result model for this tool,
                // preserving original metadata and computing the relevance score.
                var score = new ToolScoreResultModel
                {
                    // Preserve the original tool description in the result model.
                    Description = tool.Description,

                    // Preserve the original tool name in the result model.
                    Name = tool.Name,

                    // Preserve the tool namespace for secondary sorting and display.
                    NameSpace = tool.Namespace,

                    // Compute the relevance score for this tool based on
                    // the normalized prompt and its tokens.
                    Score = SetToolScore(tool, normalizedPrompt, promptTokens)
                };

                // Add the scored tool to the list of results for
                // further filtering and sorting.
                results.Add(score);
            }

            // Keep only unique tools, remove any non-relevant matches, sort the remaining
            // tools by score and name, and limit the final result set.
            var tools = results
                .DistinctBy(x => new { x.Name, x.NameSpace })
                .Where(i => i.Score > 0)
                .OrderByDescending(i => i.Score)
                .Take(take)
                .ToArray();

            // Return the final scoring result.
            return new()
            {
                Tools = tools
            };
        }

        // Builds the lexical retrieval catalog from the current plugin cache.
        private static IEnumerable<ToolScoreModel> InitializeCatalog(CacheManager cache, string[] included)
        {
            // Enumerate all cached plugins across all plugin type groups.
            // Keep only plugin types that are allowed in the lexical retrieval catalog.
            // Convert each eligible plugin manifest into a scored catalog entry.
            return cache
                .PluginsCache
                .Values
                .SelectMany(i => i.Values)
                .Where(i => included.Contains(i.Manifest.PluginType, StringComparer.OrdinalIgnoreCase))
                .Select(NewToolScoreModel);

            // Maps the cached manifest into a retrieval-friendly model by preserving
            // the original display fields and also generating normalized values for lexical
            // matching and scoring.
            static ToolScoreModel NewToolScoreModel(G4PluginCacheModel cacheModel)
            {
                return new ToolScoreModel
                {
                    // Store the original summary text as the tool description.
                    Description = string.Join('\n', cacheModel.Manifest.Summary),

                    // Convert all manifest examples into the internal score example format.
                    Examples = [.. cacheModel.Manifest.Examples.Select(FormatExample)],

                    // Store the tool key as the display and lookup name.
                    Name = cacheModel.Manifest.Key,

                    // Store the plugin namespace for additional retrieval context.
                    Namespace = cacheModel.Manifest.Namespace,

                    // Store normalized fields used by the lexical retrieval process.
                    NormalizedDescription = Normalize(string.Join('\n', cacheModel.Manifest.Summary)),
                    NormalizedName = Normalize(cacheModel.Manifest.Key.ConvertToSpaceCase()),
                    NormalizedNamespace = Normalize(cacheModel.Manifest.Namespace)
                };
            }

            // Extracts the example description and optional labels from the plugin
            // example context, normalizes them, and returns the result in the format used by
            // the scoring pipeline. If the labels cannot be parsed, the example is still
            // returned without label values.
            static ToolScoreExampleModel FormatExample(PluginExampleModel pluginExample)
            {
                // Check whether the example contains a context dictionary.
                var isContext = pluginExample.Context?.Any() == true;

                // Try to read the labels value from the example context.
                // When no labels are present, use an empty string array.
                var labelsValue = isContext && pluginExample.Context.TryGetValue("labels", out object labelsOut)
                    ? labelsOut
                    : Array.Empty<string>();

                // Join the example description lines into a single text block.
                // When no description is available, use an empty string.
                var description = pluginExample.Description.Any() == true
                    ? string.Join("\n", pluginExample.Description)
                    : string.Empty;

                try
                {
                    // Serialize and deserialize the labels value to normalize it into
                    // a string array regardless of its original runtime shape.
                    var labelsJson = JsonSerializer.Serialize(labelsValue);
                    var labels = JsonSerializer.Deserialize<string[]>(labelsJson) ?? [];

                    // Create and return the formatted scoring example with both raw and
                    // normalized values for the example text and labels.
                    return new()
                    {
                        Example = description,
                        Labels = labels,
                        NormalizedExample = Normalize(description),
                        NormalizedLabels = labels != null ? Normalize(string.Join(' ', labels)) : string.Empty
                    };
                }
                catch
                {
                    // If the labels cannot be parsed, continue without them and still
                    // return the normalized example text.
                    return new()
                    {
                        Example = description,
                        NormalizedExample = Normalize(description)
                    };
                }
            }
        }








        // Builds the per-tool example catalog from the current plugin cache.
        // Each dictionary entry maps a "{namespace.}toolName" key to the full array of
        // ExampleScoreModel entries for that tool — ready for scoring at query time.
        private static Dictionary<string, ExampleScoreModel[]> InitializeExamples(CacheManager cache, string[] included)
        {
            var a = cache
                .PluginsCache
                .Values
                .SelectMany(i => i.Values)
                .Where(i => included.Contains(i.Manifest.PluginType, StringComparer.OrdinalIgnoreCase));
                //.ToDictionary(
                //    i => $"{(string.IsNullOrEmpty(i.Manifest.Namespace) ? "" : $"{i.Manifest.Namespace}.")}{i.Manifest.Key}",
                //    i => i.Manifest.Examples.Select(e => NewExampleScoreModel(i.Manifest, e)).ToArray());

            var d = new Dictionary<string, ExampleScoreModel[]>(StringComparer.OrdinalIgnoreCase);

            foreach (var i in a)
            {
                var key = $"{(string.IsNullOrEmpty(i.Manifest.Namespace) ? "" : $"{i.Manifest.Namespace}.")}{i.Manifest.Key}";
                var value = i.Manifest.Examples.Select(e => NewExampleScoreModel(i.Manifest, e));

                d[key] = [.. value];
            }

            return d;


            // Maps a raw PluginExampleModel into an ExampleScoreModel by preserving the original
            // example as the returnable payload and computing the normalized fields used for scoring.
            // NormalizedDescription holds the normalized example text; NormalizedName holds the
            // normalized label text — the two primary signals for per-example scoring.
            static ExampleScoreModel NewExampleScoreModel(IG4PluginManifest manifest, PluginExampleModel pluginExample)
            {
                // Check whether the example contains a context dictionary.
                var isContext = pluginExample.Context?.Any() == true;

                // Try to read the labels value from the example context.
                // When no labels are present, use an empty string array.
                var labelsValue = isContext && pluginExample.Context.TryGetValue("labels", out object labelsOut)
                    ? labelsOut
                    : Array.Empty<string>();

                // Join the example description lines into a single text block.
                var description = pluginExample.Description?.Any() == true
                    ? string.Join("\n", pluginExample.Description)
                    : string.Empty;

                try
                {
                    // Serialize and deserialize the labels value to normalize it into
                    // a string array regardless of its original runtime shape.
                    var labelsJson = JsonSerializer.Serialize(labelsValue);
                    var labels = JsonSerializer.Deserialize<string[]>(labelsJson) ?? [];

                    return new ExampleScoreModel
                    {
                        // Preserve the original PluginExampleModel as the returnable payload.
                        Example = pluginExample,

                        // Store tool-level metadata for context in the result model.
                        Name      = manifest.Key,
                        Namespace = manifest.Namespace,

                        // NormalizedDescription holds the normalized example text for token scoring.
                        NormalizedDescription = Normalize(description),

                        // NormalizedName holds the normalized label text — the primary intent signal.
                        NormalizedName = labels.Length > 0 ? Normalize(string.Join(' ', labels)) : string.Empty,

                        // NormalizedNamespace provides a supporting namespace-level signal.
                        NormalizedNamespace = Normalize(manifest.Namespace)
                    };
                }
                catch
                {
                    // If labels cannot be parsed, return the entry without label scoring support.
                    return new ExampleScoreModel
                    {
                        Example   = pluginExample,
                        Name      = manifest.Key,
                        Namespace = manifest.Namespace,

                        NormalizedDescription = Normalize(description),
                        NormalizedName        = string.Empty,
                        NormalizedNamespace   = Normalize(manifest.Namespace)
                    };
                }
            }
        }







        // Splits the supplied text into distinct normalized tokens for retrieval processing.
        private static IEnumerable<string> FormatTokens(string value)
        {
            // Return an empty sequence when the input is null, empty, or whitespace.
            if (string.IsNullOrWhiteSpace(value))
            {
                return [];
            }

            // Split the input on spaces, remove empty entries, trim each token,
            // filter out single-character tokens, and return distinct values.
            return value
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => x.Length > 1)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        // Normalizes a text value for consistent comparison and retrieval processing.
        private static string Normalize(string value)
        {
            // Return an empty string when the input is null, empty, or whitespace.
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            // Convert the value to lowercase to make comparisons case-insensitive.
            value = value.ToLowerInvariant();

            // Replace common word separators with spaces.
            value = value.Replace('_', ' ');
            value = value.Replace('-', ' ');

            // Replace punctuation and other non-word characters with spaces.
            value = Regex.Replace(value, @"[^\w\s]", " ");

            // Collapse repeated whitespace into a single space and trim the result.
            value = Regex.Replace(value, @"\s+", " ").Trim();

            // Return the normalized text value.
            return value;
        }

        // Calculates the relevance score for a single example against the supplied prompt.
        // Labels (NormalizedName) are treated as curated intent tags and carry a stronger
        // weight than free-text example content (NormalizedDescription). Signals reward both
        // per-token presence and phrase-level intent alignment.
        private static int SetExampleScore(ExampleScoreModel example, string normalizedPrompt, string[] promptTokens)
        {
            // Define the string comparison used for all field checks.
            const StringComparison comparison = StringComparison.Ordinal;

            // Initialize the score accumulator.
            var score = 0;

            // Pad the normalized fields so token matching respects word boundaries via simple
            // contains checks rather than full regex evaluation.
            // NormalizedDescription = normalized example text; NormalizedName = normalized labels.
            var paddedExample = $" {example.NormalizedDescription ?? string.Empty} ";
            var paddedLabels = $" {example.NormalizedName ?? string.Empty} ";

            // Per-token scoring across both fields.
            foreach (var token in promptTokens)
            {
                var paddedToken = $" {token} ";

                // Labels are curated intent tags and therefore carry the strongest per-token weight.
                if (paddedLabels.Contains(paddedToken, comparison))
                {
                    score += 10;
                }

                // Example text provides a supporting relevance signal.
                if (paddedExample.Contains(paddedToken, comparison))
                {
                    score += 6;
                }
            }

            // Leading-token bonus — the first prompt token usually carries the primary user intent.
            if (promptTokens.Length > 0)
            {
                var paddedFirstToken = $" {promptTokens[0]} ";

                if (paddedLabels.Contains(paddedFirstToken, comparison))
                {
                    score += 20;
                }

                if (paddedExample.Contains(paddedFirstToken, comparison))
                {
                    score += 12;
                }
            }

            // Leading-phrase bonus — the first two tokens often form an action phrase such as
            // "send keys" or "go to", which is a strong intent signal when it matches.
            if (promptTokens.Length > 1)
            {
                var paddedLeadingPhrase = $" {promptTokens[0]} {promptTokens[1]} ";

                if (paddedLabels.Contains(paddedLeadingPhrase, comparison))
                {
                    score += 22;
                }

                if (paddedExample.Contains(paddedLeadingPhrase, comparison))
                {
                    score += 14;
                }
            }

            // Full-prompt substring bonus — rewards an example whose text or labels closely
            // mirror the entire normalized prompt.
            if (!string.IsNullOrEmpty(normalizedPrompt))
            {
                if (paddedLabels.Contains(normalizedPrompt, comparison))
                {
                    score += 20;
                }

                if (paddedExample.Contains(normalizedPrompt, comparison))
                {
                    score += 20;
                }
            }

            return score;
        }

        // Calculates the final relevance score for a tool against the supplied prompt.
        // Combines multiple scoring heuristics, including example matches,
        // field coverage, leading token intent, leading phrase intent, and full tool
        // name presence in the normalized prompt.
        private static int SetToolScore(ToolScoreModel tool, string normalizedPrompt, string[] promptTokens)
        {
            // Define the string comparison method used for all full-name checks so
            // matching behavior stays consistent with the other scoring methods.
            const StringComparison comparison = StringComparison.Ordinal;

            // Initialize the score accumulator.
            var score = 0;

            // Read the normalized tool name once so it can be reused by the scoring logic.
            var name = tool.NormalizedName ?? string.Empty;

            // Score token matches across the tool name, examples, labels, namespace,
            // and description fields.
            score += MeasureExamples(promptTokens, tool);

            // Add a bonus when matches are distributed across multiple fields.
            score += MeasureFields(promptTokens, tool);

            // Add a bonus when the leading two-token phrase aligns with the tool.
            score += MeasureLeadingPhrase(promptTokens, tool);

            // Add a bonus when the leading prompt token aligns with the tool.
            score += MeasureLeadingTokens(promptTokens, tool);

            // Add a bonus when the leading action phrase appears in the tool name.
            score += MeasureToolNamePhrasePresence(promptTokens, tool);

            // Award a bonus when the full normalized tool name appears in the prompt.
            // This helps when the user phrasing is very close to the actual tool name.
            if (!string.IsNullOrEmpty(name) && normalizedPrompt.Contains(name, comparison))
            {
                score += 20;
            }

            // Return the final relevance score.
            return score;

            // Measures how strongly the supplied prompt tokens match the example-related
            // fields of the specified tool.
            // Rewards matches across multiple normalized fields with different
            // weights. Tool name matches contribute the most, example and label matches
            // provide strong intent signals, namespace matches provide medium relevance,
            // and description matches provide lighter support.
            static int MeasureExamples(string[] tokens, ToolScoreModel tool)
            {
                // Define the string comparison method used for all token checks so
                // matching behavior stays consistent across all evaluated fields.
                const StringComparison comparison = StringComparison.Ordinal;

                // Start with no score contribution.
                var score = 0;

                // Pad the normalized tool name so token matching can be performed with
                // simple contains checks while still respecting token boundaries.
                var paddedName = $" {tool.NormalizedName ?? string.Empty} ";

                // Pad the normalized namespace for the same token-boundary matching behavior.
                var paddedNamespace = $" {tool.NormalizedNamespace ?? string.Empty} ";

                // Pad the normalized description so it can also participate in token scoring.
                var paddedDescription = $" {tool.NormalizedDescription ?? string.Empty} ";

                // Score per-token matches across the normalized tool fields.
                foreach (var token in tokens)
                {
                    // Pad the token so matching behaves like a whole-token comparison.
                    var paddedToken = $" {token} ";

                    // The tool name is the strongest per-token signal.
                    if (paddedName.Contains(paddedToken, comparison))
                    {
                        score += 10;
                    }

                    // Track whether the token matched any example-related field at all,
                    // and how many distinct examples supported that match.
                    var matchedExamples = 0;

                    // Inspect all examples, but do not stack raw hits from every example
                    // or from both example text and labels separately.
                    foreach (var example in tool.Examples)
                    {
                        if (example.NormalizedExample.Contains(paddedToken, comparison) ||
                            example.NormalizedLabels.Contains(paddedToken, comparison))
                        {
                            matchedExamples++;
                        }
                    }

                    // Award a single example-field presence score per token.
                    if (matchedExamples > 0)
                    {
                        score += 8;
                    }

                    // Add a small bounded reinforcement bonus for repeated support across
                    // distinct examples, but keep it intentionally modest.
                    if (matchedExamples > 1)
                    {
                        score += Math.Min(matchedExamples - 1, 2) * 2;
                    }

                    // Namespace matches provide medium-strength relevance support.
                    if (paddedNamespace.Contains(paddedToken, comparison))
                    {
                        score += 4;
                    }

                    // Description matches provide lighter supporting relevance.
                    if (paddedDescription.Contains(paddedToken, comparison))
                    {
                        score += 2;
                    }
                }

                // Return the final accumulated score contribution.
                return score;
            }

            // Measures how broadly the supplied prompt tokens match the main normalized
            // fields of the specified tool.
            // Rewards tools whose matches are distributed across multiple fields,
            // such as name, examples, namespace, and description. Broader field coverage
            // increases confidence that the tool is relevant to the prompt.
            static int MeasureFields(string[] tokens, ToolScoreModel tool)
            {
                // Define the string comparison method used for all token checks so
                // matching behavior stays consistent across all evaluated fields.
                const StringComparison comparison = StringComparison.Ordinal;

                // Start with no score contribution.
                var score = 0;

                // Pad the normalized tool name so token matching can be performed with
                // simple contains checks while still respecting token boundaries.
                var paddedName = $" {tool.NormalizedName ?? string.Empty} ";

                // Pad the normalized namespace for the same token-boundary matching behavior.
                var paddedNamespace = $" {tool.NormalizedNamespace ?? string.Empty} ";

                // Pad the normalized description so it can also participate in field coverage scoring.
                var paddedDescription = $" {tool.NormalizedDescription ?? string.Empty} ";

                // Count how many distinct fields matched at least one prompt token.
                var matchedFields = 0;

                // Count the name field if any prompt token appears in the normalized tool name.
                if (tokens.Any(t => paddedName.Contains($" {t} ", comparison)))
                {
                    matchedFields++;
                }

                // Count the examples field if any prompt token appears in any normalized
                // example text or example label.
                if (tool.Examples.Any(e =>
                    tokens.Any(t =>
                        e.NormalizedLabels.Contains($" {t} ", comparison) ||
                        e.NormalizedExample.Contains($" {t} ", comparison))))
                {
                    matchedFields++;
                }

                // Count the namespace field if any prompt token appears in the normalized namespace.
                if (tokens.Any(t => paddedNamespace.Contains($" {t} ", comparison)))
                {
                    matchedFields++;
                }

                // Count the description field if any prompt token appears in the normalized description.
                if (tokens.Any(t => paddedDescription.Contains($" {t} ", comparison)))
                {
                    matchedFields++;
                }

                // Add a bonus when matches are spread across at least two distinct fields.
                if (matchedFields >= 2)
                {
                    score += 10;
                }

                // Add an extra bonus when matches are spread across three or more fields.
                if (matchedFields >= 3)
                {
                    score += 10;
                }

                // Return the final relevance score.
                return score;
            }

            // Measures how strongly the leading two-token phrase matches the supplied tool.
            // The first two tokens often represent an action phrase, such as
            // <c>send keys</c> or <c>go to</c>, so this method applies an additional weight
            // when that phrase appears in the tool name or in the example-derived fields.
            static int MeasureLeadingPhrase(string[] tokens, ToolScoreModel tool)
            {
                // Define the string comparison method used for all phrase checks so
                // matching behavior stays consistent across all evaluated fields.
                const StringComparison comparison = StringComparison.Ordinal;

                // Start with no score contribution.
                var score = 0;

                // A leading phrase requires at least two tokens.
                if (tokens.Length <= 1)
                {
                    return score;
                }

                // Build the leading two-token phrase from the prompt input.
                var leadingPhrase = $"{tokens[0]} {tokens[1]}";

                // Pad the phrase and normalized fields so matching behaves like
                // a whole-phrase comparison instead of a substring match.
                var paddedLeadingPhrase = $" {leadingPhrase} ";
                var paddedName = $" {tool.NormalizedName ?? string.Empty} ";
                var paddedNamespace = $" {tool.NormalizedNamespace ?? string.Empty} ";

                // Track whether the leading phrase appears in labels or example text,
                // and count how many distinct examples support that match.
                var matchedExamples = 0;
                var matchedExampleText = false;
                var matchedLabels = false;

                // Inspect all examples to see if the leading
                // phrase appears in either the normalized
                foreach (var example in tool.Examples)
                {
                    // Track whether this example supports a match for the
                    var matchedThisExample = false;

                    // Check whether the leading phrase appears in the normalized example text.
                    if (example.NormalizedExample.Contains(paddedLeadingPhrase, comparison))
                    {
                        matchedExampleText = true;
                        matchedThisExample = true;
                    }

                    // Check whether the leading phrase appears in the normalized example labels.
                    if (example.NormalizedLabels.Contains(paddedLeadingPhrase, comparison))
                    {
                        matchedLabels = true;
                        matchedThisExample = true;
                    }

                    // Count the example once when either its text or labels matched.
                    if (matchedThisExample)
                    {
                        matchedExamples++;
                    }
                }

                // Award the strongest bonus when the leading phrase appears in the tool name.
                if (paddedName.Contains(paddedLeadingPhrase, comparison))
                {
                    score += 30;
                }

                // Award a strong intent bonus when the leading phrase appears in labels.
                if (matchedLabels)
                {
                    score += 22;
                }

                // Award a smaller supporting bonus when the leading phrase appears
                // in the example text.
                if (matchedExampleText)
                {
                    score += 14;
                }

                // Add a bounded reinforcement bonus when multiple distinct examples
                // support the same leading phrase.
                if (matchedExamples > 1)
                {
                    score += Math.Min(matchedExamples - 1, 2) * 3;
                }

                // Allow a very small namespace assist, but keep it weak so it does not
                // overpower the stronger intent signals.
                if (paddedNamespace.Contains(paddedLeadingPhrase, comparison))
                {
                    score += 2;
                }

                // Return the final score contribution for the leading phrase.
                return score;
            }

            // Measures how strongly the leading prompt token matches the supplied tool.
            // The first token often represents the user's main intent verb, so this method
            // applies a higher weight when that token appears in the tool name or in the
            // example-derived matching fields.
            static int MeasureLeadingTokens(string[] tokens, ToolScoreModel tool)
            {
                // Define the string comparison method used for all token checks so
                // matching behavior stays consistent across the scoring logic.
                const StringComparison comparison = StringComparison.Ordinal;

                // Start with no score contribution.
                var score = 0;

                // Nothing to score when no prompt tokens are available.
                if (tokens.Length == 0)
                {
                    return score;
                }

                // Read the first token, which usually carries the primary user intent.
                var firstToken = tokens[0];

                // Pad the first token and normalized fields so matching behaves like
                // a whole-token comparison instead of a substring match.
                var paddedFirstToken = $" {firstToken} ";
                var paddedName = $" {tool.NormalizedName ?? string.Empty} ";
                var paddedNamespace = $" {tool.NormalizedNamespace ?? string.Empty} ";

                // Track whether the leading token appears in labels or example text,
                // and count how many distinct examples support that match.
                var matchedExamples = 0;
                var matchedExampleText = false;
                var matchedLabels = false;

                // Inspect all examples to see if the leading token appears in either the normalized
                foreach (var example in tool.Examples)
                {
                    // Track whether this example supports a match for the
                    // leading token in either its text or labels,
                    var matchedThisExample = false;

                    // Check whether the leading token appears in the normalized example text.
                    if (example.NormalizedExample.Contains(paddedFirstToken, comparison))
                    {
                        matchedExampleText = true;
                        matchedThisExample = true;
                    }

                    // Check whether the leading token appears in the normalized example labels.
                    if (example.NormalizedLabels.Contains(paddedFirstToken, comparison))
                    {
                        matchedLabels = true;
                        matchedThisExample = true;
                    }

                    // Count the example once when either its text or labels matched.
                    if (matchedThisExample)
                    {
                        matchedExamples++;
                    }
                }

                // Award the strongest bonus when the leading token appears in the tool name.
                if (paddedName.Contains(paddedFirstToken, comparison))
                {
                    score += 35;
                }

                // Award a strong intent bonus when the leading token appears in labels.
                if (matchedLabels)
                {
                    score += 20;
                }

                // Award a smaller supporting bonus when the leading token appears
                // in the example text.
                if (matchedExampleText)
                {
                    score += 12;
                }

                // Add a bounded reinforcement bonus when multiple distinct examples
                // support the same leading token.
                if (matchedExamples > 1)
                {
                    score += Math.Min(matchedExamples - 1, 2) * 3;
                }

                // Allow a small namespace assist, but keep it weak so it does not
                // overpower the stronger intent signals.
                if (paddedNamespace.Contains(paddedFirstToken, comparison))
                {
                    score += 3;
                }

                // Return the final score contribution for the leading token.
                return score;
            }

            // Measures how strongly the leading action phrase appears in the tool name.
            // This specifically strengthens ranking when the prompt starts with a concrete
            // action pattern such as <c>open url</c>, <c>send keys</c>, or <c>get text</c>.
            static int MeasureToolNamePhrasePresence(string[] tokens, ToolScoreModel tool)
            {
                // Define the string comparison method used for all phrase checks so
                // matching behavior stays consistent across all evaluated fields.
                const StringComparison comparison = StringComparison.Ordinal;

                // Start with no score contribution.
                var score = 0;

                // At least two tokens are required to form a phrase.
                if (tokens.Length <= 1)
                {
                    return score;
                }

                // Pad the normalized tool name so phrase matching behaves like a whole-phrase comparison.
                var paddedName = $" {tool.NormalizedName ?? string.Empty} ";

                // Build the leading two-token phrase from the prompt input.
                var leadingPhrase = $"{tokens[0]} {tokens[1]}";
                var paddedLeadingPhrase = $" {leadingPhrase} ";

                // Strongly reward direct tool-name phrase presence.
                if (paddedName.Contains(paddedLeadingPhrase, comparison))
                {
                    score += 20;
                }

                // Return the final score contribution.
                return score;
            }
        }
        #endregion

        #region *** Nested Types ***
        public sealed class ExampleScoreResultModel
        {
            /// <summary>
            /// Gets or sets the tool description.
            /// </summary>
            public string Description { get; set; }

            public ExampleScoreModel Example { get; set; }

            /// <summary>
            /// Gets or sets the tool name.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the tool namespace.
            /// </summary>
            public string NameSpace { get; set; }

            /// <summary>
            /// Gets or sets the computed lexical relevance score.
            /// </summary>
            public int Score { get; set; }
        }

        public sealed class ExampleScoreModel
        {
            /// <summary>
            /// Gets or sets the collection of example strings associated with this instance.
            /// </summary>
            public PluginExampleModel Example { get; set; }

            /// <summary>
            /// Gets or sets the original tool name.
            /// </summary>
            public string Name { get; set; } = "";

            /// <summary>
            /// Gets or sets the original tool namespace.
            /// </summary>
            public string Namespace { get; set; } = "";

            /// <summary>
            /// Gets or sets the normalized tool description used for matching.
            /// </summary>
            public string NormalizedDescription { get; set; }

            /// <summary>
            /// Gets or sets the normalized tool name used for matching.
            /// </summary>
            public string NormalizedName { get; set; }

            /// <summary>
            /// Gets or sets the normalized tool namespace used for matching.
            /// </summary>
            public string NormalizedNamespace { get; set; }
        }





        /// <summary>
        /// Represents a scored lexical retrieval result for a tool.
        /// </summary>
        public sealed class ToolScoreResultModel
        {
            /// <summary>
            /// Gets or sets the tool description.
            /// </summary>
            public string Description { get; set; }

            /// <summary>
            /// Gets or sets the tool name.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the tool namespace.
            /// </summary>
            public string NameSpace { get; set; }

            /// <summary>
            /// Gets or sets the computed lexical relevance score.
            /// </summary>
            public int Score { get; set; }
        }

        /// <summary>
        /// Represents the result returned by the tool scoring operation.
        /// </summary>
        public sealed class ResultModel
        {
            /// <summary>
            /// Gets or sets the collection of example score results associated with the model.
            /// </summary>
            public ExampleScoreResultModel[] Examples { get; set; }

            /// <summary>
            /// Gets or sets the scored tools returned by the matching operation.
            /// </summary>
            /// <remarks>
            /// Each entry contains a tool and its associated score, allowing the caller
            /// to evaluate how well each tool matched the scoring criteria.
            /// </remarks>
            public ToolScoreResultModel[] Tools { get; set; }
        }

        /// <summary>
        /// Represents a cached tool entry together with its normalized fields used
        /// during lexical scoring and retrieval.
        /// </summary>
        private sealed class ToolScoreModel
        {
            /// <summary>
            /// Gets or sets the original tool description.
            /// </summary>
            public string Description { get; set; } = "";

            /// <summary>
            /// Gets or sets the collection of example strings associated with this instance.
            /// </summary>
            public ToolScoreExampleModel[] Examples { get; set; } = [];

            /// <summary>
            /// Gets or sets the original tool name.
            /// </summary>
            public string Name { get; set; } = "";

            /// <summary>
            /// Gets or sets the original tool namespace.
            /// </summary>
            public string Namespace { get; set; } = "";

            /// <summary>
            /// Gets or sets the normalized tool description used for matching.
            /// </summary>
            public string NormalizedDescription { get; set; }

            /// <summary>
            /// Gets or sets the normalized tool name used for matching.
            /// </summary>
            public string NormalizedName { get; set; }

            /// <summary>
            /// Gets or sets the normalized tool namespace used for matching.
            /// </summary>
            public string NormalizedNamespace { get; set; }
        }

        /// <summary>
        /// Represents a formatted example used by the tool scoring pipeline.
        /// </summary>
        private sealed class ToolScoreExampleModel
        {
            /// <summary>
            /// Gets or sets the original example text.
            /// </summary>
            public string Example { get; set; } = "";

            /// <summary>
            /// Gets or sets the labels associated with the example.
            /// </summary>
            public string[] Labels { get; set; } = [];

            /// <summary>
            /// Gets or sets the normalized example text used during matching.
            /// </summary>
            public string NormalizedExample { get; set; } = "";

            /// <summary>
            /// Gets or sets the normalized label text used during matching.
            /// </summary>
            public string NormalizedLabels { get; set; } = "";
        }
        #endregion
    }
}
