using G4.Services.Domain.V4.Extensions;

using HtmlAgilityPack;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace G4.Services.Domain.V4
{
    /// <summary>
    /// Compnent for partitioning a raw HTML document into deterministic
    /// semantic segments based on configurable rules and heuristics.
    /// </summary>
    public static class DomPartitioner
    {
        #region *** Methods      ***
        /// <summary>
        /// Creates DOM segments from the serialized HTML of the supplied node using default segment options.
        /// </summary>
        /// <param name="node">The HTML node whose outer HTML should be segmented.</param>
        /// <returns>A list of generated <see cref="Segment" /> instances.</returns>
        public static List<Segment> NewSegments(HtmlNode node)
        {
            // Use the node's serialized HTML as the segmentation source,
            // and apply the default option set.
            return NewSegments(
                html: node.OuterHtml,
                options: new SegmentOptions()
            );
        }

        /// <summary>
        /// Creates DOM segments from the serialized HTML of the supplied node using explicit segment options.
        /// </summary>
        /// <param name="node">The HTML node whose outer HTML should be segmented.</param>
        /// <param name="options">The segmentation options that control chunking, preview formatting, and metadata export.</param>
        /// <returns>A list of generated <see cref="Segment" /> instances.</returns>
        public static List<Segment> NewSegments(HtmlNode node, SegmentOptions options)
        {
            // Use the node's serialized HTML as the segmentation source,
            // and apply the caller-provided option set.
            return NewSegments(
                html: node.OuterHtml,
                options
            );
        }

        /// <summary>
        /// Splits the supplied HTML into DOM chunks using the default chunking options.
        /// </summary>
        /// <param name="html">The HTML content to split into chunks. The input may be raw, sanitized, or partially sanitized.</param>
        /// <returns>A list of <see cref="Segment"/> instances created from the supplied HTML using the default <see cref="SegmentOptions"/>.</returns>
        public static List<Segment> NewSegments(string html)
        {
            // Delegate to the overload that accepts explicit chunking options,
            // using the default option set.
            return NewSegments(html, new SegmentOptions());
        }

        /// <summary>
        /// Splits the supplied HTML into DOM chunks using the specified chunking options.
        /// </summary>
        /// <param name="html">The HTML content to split into chunks.</param>
        /// <param name="options">The chunking options that control how the DOM is grouped into chunks.</param>
        /// <returns>A list of <see cref="Segment"/> instances created from the supplied HTML using the specified <see cref="SegmentOptions"/>.</returns>
        public static List<Segment> NewSegments(string html, SegmentOptions options)
        {
            // Return an empty result when there is no HTML to process.
            if (string.IsNullOrWhiteSpace(html))
            {
                return [];
            }

            // Use default chunking options when no explicit options were provided.
            options ??= new SegmentOptions();

            // Load the HTML into a document so chunk grouping can operate on the DOM tree.
            var document = new HtmlDocument();
            document.LoadHtml(html);

            // Prefer the body element as the chunking root when it exists.
            // Fall back to the document root when the body element is missing.
            var searchRoot = document
                .DocumentNode
                .SelectSingleNode("//body") ?? document.DocumentNode;

            // Group the DOM into chunks starting from the selected search root.
            var segments = GroupSegments(searchRoot, options);

            // Ensure at least one generic chunk is returned when grouping produced no chunks.
            if (segments.Count == 0)
            {
                segments.Add(NewSegment(searchRoot, options, 0, SegmentKind.Generic));
            }

            // Return the generated chunk collection.
            return segments;
        }

        // Recursively groups a DOM subtree into semantic chunks based on the supplied
        // chunking options and chunk root selection rules.
        private static List<Segment> GroupSegments(HtmlNode node, SegmentOptions options)
        {
            // Create the result collection that will accumulate all chunks found
            // under the current subtree.
            var result = new List<Segment>();

            foreach (var childNode in node.ChildNodes)
            {
                // Only element nodes participate in semantic chunking.
                if (childNode.NodeType != HtmlNodeType.Element)
                {
                    continue;
                }

                // Normalize the tag name once so all registry checks use the same value.
                var tag = childNode.Name.ToLowerInvariant();

                // When the current node is not a valid chunk root, recurse into its children
                // and continue searching deeper in the subtree.
                if (!AssertRoot(childNode, tag, options, out string kind))
                {
                    var items = GroupSegments(childNode, options);
                    result.AddRange(items);
                    continue;
                }

                // Count the size of the current subtree so oversized semantic chunks
                // can be split when needed.
                var nodeCount = childNode.Count();

                // Never-stop tags are generic structural wrappers that should always
                // be broken down instead of emitted as a single large chunk.
                var isNeverStop = kind == SegmentKind.Generic && SegmentsRegistry.NeverStopTags.Contains(tag);

                // Some tags are always split into smaller semantic units.
                var isSplit = SegmentsRegistry.AlwaysSplitTags.Contains(tag);

                // Oversized semantic chunks can be split when the current options allow it.
                var isOversized = nodeCount > options.MaxSegmentNodeCount && options.SplitOversizedSemanticSegments;

                if (isNeverStop && nodeCount > options.MaxSegmentNodeCount)
                {
                    // Recurse into large layout wrappers instead of emitting them as one chunk.
                    var segments = GroupSegments(childNode, options);
                    result.AddRange(segments);
                }
                else if (isSplit || isOversized)
                {
                    // Try to split the current subtree into smaller chunks first.
                    // Emit the current node as a fallback only when splitting produced nothing.
                    var before = result.Count;
                    var segments = GroupSegments(childNode, options);

                    result.AddRange(segments);

                    if (result.Count == before)
                    {
                        var chunk = NewSegment(childNode, options, result.Count, kind);
                        result.Add(chunk);
                    }
                }
                else
                {
                    // Emit the current node as a normal chunk when it does not require
                    // forced splitting, then scan inside it for breakout chunks such as dialogs.
                    var chunk = NewSegment(childNode, options, result.Count, kind);
                    result.Add(chunk);

                    var breakoutChunks = GroupBreakoutSegments(childNode, options);
                    result.AddRange(breakoutChunks);
                }
            }

            // Return all chunks collected for the current subtree.
            return result;

            // Recursively finds breakout chunks inside an existing chunk subtree, such as
            // dialog-like regions that should be emitted as their own semantic chunks.
            static List<Segment> GroupBreakoutSegments(HtmlNode node, SegmentOptions options)
            {
                // Create the result collection that will hold all breakout chunks found
                // under the current subtree.
                var result = new List<Segment>();

                foreach (var childNode in node.ChildNodes)
                {
                    // Only element nodes can become breakout chunks.
                    if (childNode.NodeType != HtmlNodeType.Element)
                    {
                        continue;
                    }

                    // Read the role attribute used to identify dialog-like breakout regions.
                    var role = childNode
                        .GetAttributeValue("role", "")
                        .Trim();

                    // Emit a dedicated dialog chunk when the current node role is marked
                    // as a breakout role in the registry.
                    if (SegmentsRegistry.BreakoutRoles.Contains(role))
                    {
                        var segment = NewSegment(childNode, options, result.Count, SegmentKind.Dialog);
                        result.Add(segment);
                    }
                    else
                    {
                        // Continue scanning deeper when the current node is not itself
                        // a breakout chunk root.
                        var breakoutSegments = GroupBreakoutSegments(childNode, options);
                        result.AddRange(breakoutSegments);
                    }
                }

                // Return all breakout chunks discovered under the current subtree.
                return result;
            }
        }

        // Determines whether the supplied node should be treated as a semantic chunk root
        // and identifies the chunk kind that should be assigned to it.
        private static bool AssertRoot(HtmlNode node, string tag, SegmentOptions options, out string kind)
        {
            // Give ARIA role-based chunk detection the highest priority because
            // accessibility roles often describe the real semantic purpose of a region.
            var role = node.GetAttributeValue("role", "").Trim();

            if (!string.IsNullOrEmpty(role) && SegmentsRegistry.AriaRoleKinds.TryGetValue(role, out kind))
            {
                // Promote certain role-derived chunk kinds to AccountActions when
                // the node also carries a strong account-related signal.
                if (SegmentsRegistry.AccountPromotableKinds.Contains(kind) && AssertStrongAccountSignal(node))
                {
                    kind = SegmentKind.AccountActions;
                }

                return true;
            }

            // Treat semantic HTML roots as chunk roots even without an ARIA role.
            if (SegmentsRegistry.SemanticRoots.Contains(tag))
            {
                // Resolve the default chunk kind from the semantic tag itself.
                kind = node.ResolveTagKind(tag);

                // Promote certain semantic chunk kinds to AccountActions when
                // the node carries strong account-related signals.
                if (SegmentsRegistry.AccountPromotableKinds.Contains(kind) && AssertStrongAccountSignal(node))
                {
                    kind = SegmentKind.AccountActions;
                }

                return true;
            }

            // Evaluate generic container tags with stricter qualification rules.
            if (SegmentsRegistry.GenericContainers.Contains(tag))
            {
                // Promote generic containers to AccountActions when they look like
                // account-related regions and have enough direct structure to stand alone.
                if (AssertStrongAccountSignal(node) && node.Count(recursive: false) >= 2)
                {
                    kind = SegmentKind.AccountActions;
                    return true;
                }

                // Accept the generic container as a chunk root when it satisfies the
                // configured generic-container heuristics.
                if (AssertGenericContainer(node, options))
                {
                    kind = SegmentKind.Generic;
                    return true;
                }
            }

            // Default the output kind even when the node does not qualify as a root.
            kind = SegmentKind.Generic;

            // The current node does not meet any chunk root criteria.
            return false;

            // Determines whether the supplied node qualifies as a generic semantic container
            // that should be emitted as its own DOM chunk.
            static bool AssertGenericContainer(HtmlNode node, SegmentOptions options)
            {
                // Count direct element children and track how many of them are considered noise.
                var directElements = 0;
                var noiseElements = 0;

                foreach (var child in node.ChildNodes)
                {
                    // Only direct element children contribute to container qualification.
                    if (child.NodeType != HtmlNodeType.Element)
                    {
                        continue;
                    }

                    directElements++;

                    // Count child elements whose tags are classified as noise.
                    if (SegmentsRegistry.NoiseTags.Contains(child.Name))
                    {
                        noiseElements++;
                    }
                }

                // Reject the node when it does not contain enough direct child elements
                // to qualify as a meaningful container.
                if (directElements < options.MinContainerChildElements)
                {
                    return false;
                }

                // Reject the node when more than 80 percent of its direct element children
                // are noise tags, which usually indicates structural clutter instead of content.
                if (directElements > 0 && noiseElements * 100 / directElements > 80)
                {
                    return false;
                }

                // Accept the node when it exposes meaningful text, actionable descendants,
                // or useful identifying attributes.
                return AssertMeaningfulText(node, options.MinMeaningfulTextLength)
                    || AssertActionableDescendant(node)
                    || AssertUsefulIdentifier(node);
            }

            // Determines whether the supplied node contains meaningful visible text
            // that is strong enough to qualify the subtree as content-bearing.
            static bool AssertMeaningfulText(HtmlNode node, int minLength)
            {
                // Traverse the subtree in a depth-first manner to find any text node that
                // meets the meaningful text criteria, while skipping noise elements that
                // are unlikely to carry relevant text and would add unnecessary overhead if traversed.
                foreach (var child in node.ChildNodes)
                {
                    // Treat direct text as meaningful only when it is a text node and its
                    // trimmed content reaches the required minimum length.
                    var isText = child.NodeType == HtmlNodeType.Text;
                    var isLength = child.InnerText.Trim().Length >= minLength;

                    // Return true immediately when meaningful text is found without
                    // needing to check the rest of the subtree.
                    if (isText && isLength)
                    {
                        return true;
                    }

                    // Recurse into non-noise element children because meaningful text may
                    // be nested deeper in the subtree.
                    var isElement = child.NodeType == HtmlNodeType.Element;
                    var isNotNoise = !SegmentsRegistry.NoiseTags.Contains(child.Name);

                    // Only consider element children that are not classified as noise,
                    // which usually do not carry meaningful text and would only add
                    // unnecessary overhead if traversed.
                    if (isElement && isNotNoise)
                    {
                        if (AssertMeaningfulText(child, minLength))
                        {
                            return true;
                        }
                    }
                }

                // Return false when no meaningful text was found in the subtree.
                return false;
            }

            // Determines whether the supplied node contains any actionable descendant
            // that suggests the subtree represents an interactive region.
            static bool AssertActionableDescendant(HtmlNode node)
            {
                // Traverse the subtree in a depth-first manner to
                // find any actionable descendant element
                foreach (var child in node.ChildNodes)
                {
                    // Only element nodes can contribute actionable structure.
                    if (child.NodeType != HtmlNodeType.Element)
                    {
                        continue;
                    }

                    // Return true when the child tag is known to represent an actionable element.
                    if (SegmentsRegistry.ActionTags.Contains(child.Name))
                    {
                        return true;
                    }

                    // Return true when the child exposes a well-known interactive role.
                    var role = child.GetAttributeValue("role", "").Trim();
                    var isRole = !string.IsNullOrEmpty(role) && SegmentsRegistry.WellKnownRoles.Contains(role);

                    // Return true immediately when an actionable descendant is
                    // found without needing to check the rest of the subtree.
                    if (isRole)
                    {
                        return true;
                    }

                    // Recurse into the child subtree because actionable descendants
                    // may be nested deeper in the structure.
                    if (AssertActionableDescendant(child))
                    {
                        return true;
                    }
                }

                // Return false when no actionable descendant was found.
                return false;
            }

            // Determines whether the supplied node or one of its direct element children
            // exposes a stable identifier that makes the subtree useful as a semantic chunk.
            static bool AssertUsefulIdentifier(HtmlNode node)
            {
                // Check the current node first for stable identifying attributes.
                var isId = !string.IsNullOrWhiteSpace(node.GetAttributeValue("id", ""));
                var isAriaLabel = !string.IsNullOrWhiteSpace(node.GetAttributeValue("aria-label", ""));
                var isTestId = !string.IsNullOrWhiteSpace(node.GetAttributeValue("data-testid", ""));

                // Return true immediately when a useful identifier is found on
                // the current node without needing to check its children.
                if (isId || isAriaLabel || isTestId)
                {
                    return true;
                }

                // Check direct element children because a subtree may still be identifiable
                // even when the current node itself has no useful identifier.
                foreach (var child in node.ChildNodes)
                {
                    // Only element children can carry useful identifiers relevant for chunk qualification.
                    var isElement = child.NodeType == HtmlNodeType.Element;

                    // Skip non-element children that cannot contribute identifying attributes,
                    // which are usually just noise text nodes that would add unnecessary overhead if traversed.
                    if (!isElement)
                    {
                        continue;
                    }

                    // Check the child node for stable identifying attributes.
                    isId = !string.IsNullOrWhiteSpace(child.GetAttributeValue("id", ""));
                    isAriaLabel = !string.IsNullOrWhiteSpace(child.GetAttributeValue("aria-label", ""));
                    isTestId = !string.IsNullOrWhiteSpace(child.GetAttributeValue("data-testid", ""));

                    // Return true immediately when a useful identifier is
                    // found on any direct child element without needing to check the rest of the children.
                    if (isId || isAriaLabel || isTestId)
                    {
                        return true;
                    }
                }

                // Return false when no useful identifier was found on the node
                // or its direct element children.
                return false;
            }
        }

        // Determines whether the supplied node carries strong signals that it represents
        // an account-related region, such as sign-in, profile, or rewards actions.
        private static bool AssertStrongAccountSignal(HtmlNode node)
        {
            // Read the main identifying attributes and normalize them for keyword matching.
            var id = node.GetAttributeValue("id", "").ToLowerInvariant();
            var cls = node.GetAttributeValue("class", "").ToLowerInvariant();
            var label = node.GetAttributeValue("aria-label", "").ToLowerInvariant();
            var name = node.GetAttributeValue("name", "").ToLowerInvariant();

            // Combine the attribute values into one searchable string.
            var combined = $"{id} {cls} {label} {name}";

            // Return true when any known account keyword appears in the combined attribute text.
            foreach (var keyword in SegmentsRegistry.AccountAttributeKeywords)
            {
                if (combined.Contains(keyword))
                {
                    return true;
                }
            }

            // Check visible text only for relatively small subtrees to keep the test fast
            // and reduce false positives from large content regions.
            if (node.Count() <= 40)
            {
                var text = node.InnerText.ToLowerInvariant();

                // Recognize common account-entry actions.
                if (text.Contains("sign in") || text.Contains("sign out"))
                {
                    return true;
                }

                // Recognize short rewards-related regions, which are often account areas.
                if (text.Contains("rewards") && text.Length < 300)
                {
                    return true;
                }
            }

            // Search descendants for known account-related aria-label values.
            foreach (var descendantLabel in SegmentsRegistry.AccountDescendantLabels)
            {
                if (AssertDescendantWithAriaLabel(node, descendantLabel))
                {
                    return true;
                }
            }

            // Return false when no strong account signal was found.
            return false;

            // Recursively checks whether any descendant element has an aria-label
            // that contains the supplied substring.
            static bool AssertDescendantWithAriaLabel(HtmlNode node, string labelSubstring)
            {
                // Traverse the subtree in a depth-first manner to find any
                // descendant element with a matching aria-label, while skipping
                // noise elements that are unlikely to carry relevant labels
                // and would add unnecessary overhead if traversed.
                foreach (var childNode in node.ChildNodes)
                {
                    // Only element nodes can carry aria-label attributes.
                    if (childNode.NodeType != HtmlNodeType.Element)
                    {
                        continue;
                    }

                    // Read and normalize the aria-label value for case-insensitive matching.
                    var ariaLabel = childNode
                        .GetAttributeValue("aria-label", "")
                        .ToLowerInvariant();

                    // Return true as soon as a matching aria-label is found.
                    if (ariaLabel.Contains(labelSubstring))
                    {
                        return true;
                    }

                    // Continue scanning deeper descendants when the current node does not match.
                    if (AssertDescendantWithAriaLabel(childNode, labelSubstring))
                    {
                        return true;
                    }
                }

                // Return false when no matching descendant aria-label was found.
                return false;
            }
        }

        // Creates a fully populated Segment from the supplied DOM node.
        private static Segment NewSegment(HtmlNode node, SegmentOptions options, int index, string kind)
        {
            // Read the original outer HTML because it is used both for identifier
            // generation and optional raw encoded DOM output.
            var outerHtml = node.OuterHtml;

            // Normalize the root tag name for consistent downstream use.
            var tag = node.Name.ToLowerInvariant();

            // Extract normalized visible text from the segment subtree.
            var text = ExportText(node);

            // Encode the original DOM only when raw encoded output is enabled.
            var encodedDom = options.IncludeRawEncodedDom
                ? ConvertToBase64(outerHtml, options.MaxEncodedLength)
                : string.Empty;

            // Build and return the final segment with all extracted metadata.
            return new Segment
            {
                Base64 = encodedDom,
                Id = FormatId(tag, outerHtml, index),
                Identifiers = ExportIdentifiers(node, options),
                Keywords = ExportKeywords(text, options.MaxKeywords),
                Kind = kind,
                Metrics = MeasureActions(node),
                Preview = FormatPreview(node, options),
                Roles = ExportRoles(node, options.MaxRoles),
                RootTag = tag,
                Stats = ResolveStats(node, text),
                Text = text
            };

            // Converts the supplied HTML string into a base64-encoded UTF-8 representation,
            // optionally truncating the encoded output to the requested maximum length.
            static string ConvertToBase64(string outerHtml, int maxLength)
            {
                // Encode the HTML as UTF-8 bytes before converting it to base64 text.
                var bytes = Encoding.UTF8.GetBytes(outerHtml);

                // Convert the UTF-8 byte array into a base64 string.
                var base64 = Convert.ToBase64String(inArray: bytes);

                // Truncate the encoded output when a maximum length was supplied
                // and the generated base64 exceeds that limit.
                if (base64.Length > maxLength)
                {
                    base64 = base64[..maxLength];
                }

                // Return the encoded base64 representation.
                return base64;
            }

            // Creates a stable segment identifier from the root tag, segment position,
            // and original DOM content.
            static string FormatId(string tag, string outerHtml, int index)
            {
                // Combine the segment index and original HTML so the hash stays stable
                // for the same content at the same logical position.
                var input = Encoding.UTF8.GetBytes($"{index}:{outerHtml}");

                // Hash the combined input using SHA-256.
                var hash = SHA256.HashData(input);

                // Convert the hash to lowercase hexadecimal text.
                var hex = Convert.ToHexString(hash).ToLowerInvariant();

                // Return the segment identifier using the root tag and the first 8 hex characters.
                return $"{tag}-{hex[..8]}";
            }

            // Measures action-oriented DOM metrics for the supplied node and all of its descendants.
            static DomMetrics MeasureActions(HtmlNode node)
            {
                // Create a metrics container for the current subtree.
                var metrics = new DomMetrics();

                // Add metrics for the current node before processing its descendants.
                metrics.AddMetrics(node);

                // Recursively measure each child node and merge its metrics into the current result.
                foreach (var child in node.ChildNodes)
                {
                    var childMetrics = MeasureActions(child);
                    metrics.AddMetrics(other: childMetrics);
                }

                // Return the aggregated metrics for this node and its descendants.
                return metrics;
            }
        }

        // Extracts locator-relevant identifier values from the supplied DOM subtree.
        private static ReadOnlyCollection<string> ExportIdentifiers(HtmlNode node, SegmentOptions options)
        {
            // Track identifier values case-insensitively so duplicates are removed
            // while preserving the first collected form.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Pre-size the result list using the configured identifier limit.
            var result = new List<string>(options.MaxIdentifiers);

            // Collect identifier values from the subtree until the configured limits are reached.
            GetIdentifiers(
                node,
                seen,
                result,
                max: options.MaxIdentifiers,
                maxLength: options.MaxAttributeValueLength);

            // Return the collected identifiers as a read-only list.
            return result.AsReadOnly();

            // Collects stable identifier attribute values from the supplied HTML node and its descendants.
            static void GetIdentifiers(
                HtmlNode node,
                HashSet<string> seen,
                List<string> result,
                int max,
                int maxLength)
            {
                // Stop immediately once the caller's collection limit has been reached.
                if (result.Count >= max)
                {
                    return;
                }

                // Only element nodes can have HTML attributes.
                // Text, comment, document, and other node types are skipped here,
                // but their child nodes are still handled later if they have any.
                if (node.NodeType == HtmlNodeType.Element)
                {
                    // Read identifier-like attributes in the registry-defined priority order.
                    foreach (string attrName in SegmentsRegistry.IdentifierAttributes)
                    {
                        // Avoid doing extra attribute work after reaching the limit.
                        if (result.Count >= max)
                        {
                            break;
                        }

                        // Missing attributes do not contribute identifiers.
                        var attr = node.Attributes[attrName];
                        if (attr == null)
                        {
                            continue;
                        }

                        // Normalize whitespace so identifiers are compact and comparable.
                        // Example: "main   menu" and "main menu" become the same value.
                        var value = Regex.Replace(attr.Value.Trim(), @"\s+", " ");

                        // Ignore empty or whitespace-only attribute values.
                        if (value.Length == 0)
                        {
                            continue;
                        }

                        // Keep exported identifiers bounded so a single large attribute
                        // cannot dominate the segment metadata.
                        if (value.Length > maxLength)
                        {
                            value = string.Concat(value.AsSpan(0, maxLength), "...");
                        }

                        // Keep only the first occurrence of each identifier value.
                        if (!seen.Add(value))
                        {
                            continue;
                        }

                        // Add the normalized, optionally truncated identifier.
                        result.Add(value);
                    }
                }

                // Continue depth-first traversal so identifiers are collected from descendants
                // in the same order they appear in the DOM.
                foreach (var child in node.ChildNodes)
                {
                    GetIdentifiers(child, seen, result, max, maxLength);
                }
            }
        }

        // Extracts a bounded, de-duplicated list of searchable keyword tokens from the supplied text.
        private static ReadOnlyCollection<string> ExportKeywords(string text, int max)
        {
            // No keywords can be extracted from null, empty, or whitespace-only text.
            if (string.IsNullOrEmpty(text.Trim()))
            {
                return [];
            }

            // Track emitted tokens case-insensitively so repeated words with different casing
            // are exported only once.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Pre-size the result list to the requested maximum to avoid unnecessary resizing.
            var result = new List<string>(max);

            // Split the text using the registry-defined separators.
            // Empty entries are ignored so repeated separators do not produce blank tokens.
            foreach (var raw in text.Split(SegmentsRegistry.SplitChars, StringSplitOptions.RemoveEmptyEntries))
            {
                // Stop once the requested keyword limit has been reached.
                if (result.Count >= max)
                {
                    break;
                }

                // Normalize tokens to lower-case so downstream scoring/searching is consistent.
                var token = raw.ToLowerInvariant();

                // Ignore very short tokens because they are usually too noisy for matching.
                if (token.Length < 3)
                {
                    continue;
                }

                // Ignore numeric-only tokens because they rarely help as general keywords.
                if (AssertNumeric(token))
                {
                    continue;
                }

                // Ignore common words that add little semantic value.
                if (SegmentsRegistry.Stopwords.Contains(token))
                {
                    continue;
                }

                // Keep only the first occurrence of each keyword.
                if (!seen.Add(token))
                {
                    continue;
                }

                // Add the normalized keyword to the exported result.
                result.Add(token);
            }

            // Return a read-only wrapper to prevent callers from mutating the exported keyword list.
            return result.AsReadOnly();

            // Determines whether the supplied string contains only ASCII numeric digits.
            static bool AssertNumeric(string s)
            {
                // Check each character manually so the behavior stays limited to ASCII digits only.
                // This avoids treating non-ASCII numeric characters as valid digits.
                foreach (char c in s)
                {
                    // Any character outside the ASCII digit range means the string is not numeric.
                    if (c < '0' || c > '9')
                    {
                        return false;
                    }
                }

                // An empty string should not be considered numeric.
                return s.Length > 0;
            }
        }

        // Exports a bounded list of unique, well-known ARIA role values from the supplied DOM subtree.
        private static List<string> ExportRoles(HtmlNode node, int max)
        {
            // Track discovered roles case-insensitively so repeated role values with different
            // casing are exported only once.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Delegate traversal and filtering to GetRoles so all role collection rules remain
            // centralized in one place.
            return GetRoles(node, seen, max);

            // Collects unique, well-known ARIA role values from the supplied node and its descendants.
            static List<string> GetRoles(HtmlNode node, HashSet<string> seen, int max)
            {
                // Store roles found in this node's subtree.
                var result = new List<string>();

                // Stop immediately when the caller's requested role limit has already been reached.
                if (result.Count >= max)
                {
                    return result;
                }

                // Only element nodes can carry HTML attributes such as "role".
                if (node.NodeType == HtmlNodeType.Element)
                {
                    // Normalize the role value so matching is consistent and case-insensitive.
                    var role = node.GetAttributeValue("role", "").Trim().ToLowerInvariant();

                    // Only export non-empty, registry-approved, not-yet-seen role values.
                    var isLength = role.Length > 0;
                    var isKnown = SegmentsRegistry.WellKnownRoles.Contains(role);
                    var isNew = seen.Add(role);

                    if (isLength && isKnown && isNew)
                    {
                        result.Add(role);
                    }
                }

                // Continue depth-first traversal and merge roles discovered in child subtrees.
                foreach (var childNodes in node.ChildNodes)
                {
                    var items = GetRoles(childNodes, seen, max);
                    result.AddRange(items);
                }

                // Return all roles found under the current node.
                return result;
            }
        }

        // Extracts normalized visible text from the supplied DOM subtree.
        private static string ExportText(HtmlNode node)
        {
            // Collect text from the subtree into a single string builder.
            var stringBuilder = AddText(node);

            // Trim boundary whitespace before collapsing internal whitespace runs.
            var input = stringBuilder.ToString().Trim();

            // Normalize all whitespace runs to a single space.
            return Regex.Replace(
                input,
                pattern: @"\s+",
                replacement: " ");

            // Recursively collects visible text from the supplied DOM subtree and returns it
            // as a single <see cref="StringBuilder"/> instance.
            static StringBuilder AddText(HtmlNode node)
            {
                // Create the result builder for the current subtree.
                var stringBuilder = new StringBuilder();

                // Handle text nodes directly by trimming and appending meaningful text.
                if (node.NodeType == HtmlNodeType.Text)
                {
                    var text = node.InnerText.Trim();

                    // Append the text only when something meaningful remains after trimming.
                    if (text.Length > 0)
                    {
                        stringBuilder.Append(text);
                    }

                    return stringBuilder;
                }

                // Recurse into all child nodes and append their extracted text.
                foreach (var child in node.ChildNodes)
                {
                    var text = AddText(child);

                    // Insert a separator only when both the current builder and the
                    // child result already contain text.
                    if (stringBuilder.Length > 0 && text.Length > 0)
                    {
                        stringBuilder.Append(' ');
                    }

                    stringBuilder.Append(text);
                }

                // Return the collected text for the current subtree.
                return stringBuilder;
            }
        }

        // Builds a compact preview HTML string for the supplied DOM node.
        private static string FormatPreview(HtmlNode node, SegmentOptions options)
        {
            // Clone the node by re-parsing its serialized HTML.
            // This keeps preview cleanup isolated from the original DOM used by EncodedDom.
            var document = new HtmlDocument
            {
                OptionOutputAsXml = false
            };

            document.LoadHtml(node.OuterHtml);

            // Get the first element from the parsed clone.
            // If the source node does not produce an element, there is no preview to export.
            var root = document.DocumentNode.GetFirstElement();
            if (root == null)
            {
                return string.Empty;
            }

            // Optionally remove noisy/large preview content such as scripts, non-whitelisted
            // attributes, long attribute values, and SVG internals.
            if (options.StripLargePayloads)
            {
                root.ClearPreviewNode(options.MaxAttributeValueLength);
            }

            // Optionally cap preview depth while keeping the root at depth 0.
            if (options.MaxPreviewDepth > 0)
            {
                SetDepthTruncation(root, options.MaxPreviewDepth, 0);
            }

            // Serialize once after cleanup so we can check whether the preview already fits.
            var html = root.OuterHtml;

            // Return the cleaned preview unchanged when it is already within the configured limit.
            if (html.Length <= options.MaxPreviewLength)
            {
                return html;
            }

            // Remove trailing child nodes until the serialized preview fits the length limit.
            SetLength(root, options.MaxPreviewLength);

            // Return the final bounded preview.
            return root.OuterHtml;

            // Reduces a node's serialized HTML length by removing trailing
            // child nodes until it fits the requested limit.
            static void SetLength(HtmlNode node, int maxLength)
            {
                // Track whether any content was removed so the caller/output can see that
                // this node no longer contains the full original content.
                var truncated = false;

                // Remove children from the end until the serialized node is short enough.
                // This keeps the earlier DOM content intact, which is usually more useful
                // for previews and matching context.
                while (node.HasChildNodes && node.OuterHtml.Length > maxLength)
                {
                    node.RemoveChild(node.LastChild);
                    truncated = true;
                }

                // Add a visible marker only when content was actually removed.
                // OwnerDocument is required because HtmlAgilityPack creates text nodes
                // through the owning document instance.
                if (truncated && node.OwnerDocument != null)
                {
                    node.AppendChild(node.OwnerDocument.CreateTextNode("...[truncated]"));
                }
            }

            // Truncates preview DOM content below a maximum element depth.
            static void SetDepthTruncation(HtmlNode node, int maxDepth, int depth)
            {
                // Only element nodes participate in preview depth truncation.
                // Text/comment/document nodes are ignored.
                if (node.NodeType != HtmlNodeType.Element)
                {
                    return;
                }

                // Once the maximum depth is reached, remove the element's children and replace
                // them with a compact marker. The element itself is kept for structural context.
                if (depth >= maxDepth)
                {
                    // Nothing to truncate when the element is already empty.
                    if (!node.HasChildNodes)
                    {
                        return;
                    }

                    // Remove all deeper content from the preview.
                    node.RemoveAllChildren();

                    // Add a visible marker so consumers know deeper content existed here.
                    // OwnerDocument is required to create a valid HtmlAgilityPack text node.
                    if (node.OwnerDocument != null)
                    {
                        node.AppendChild(node.OwnerDocument.CreateTextNode("...[truncated]"));
                    }

                    return;
                }

                // Continue walking descendants until the configured depth limit is reached.
                // ToArray() protects traversal if future logic mutates child collections.
                foreach (var childNode in node.ChildNodes.ToArray())
                {
                    SetDepthTruncation(
                        node: childNode,
                        maxDepth,
                        depth: depth + 1);
                }
            }
        }

        // Computes structural metrics for an HTML subtree, combining traversal statistics
        // (node count, element count, maximum depth) with the length of the already-extracted text.
        private static SegmentMetrics ResolveStats(HtmlNode node, string extractedText)
        {
            // Walk the subtree once, starting at depth 0, and destructure the aggregated
            // totals returned by the recursive helper. Each value is a pure sum/max over
            // the subtree -- no shared mutable state is involved.
            var (nodeCount, elementCount, maxDepth) = ResolveStats(node, 0);

            // Assemble the result. TextLength is taken from the caller-supplied string
            // rather than recomputed during the walk, since text extraction is the
            // caller's responsibility and may apply its own normalization rules.
            return new SegmentMetrics
            {
                NodeCount = nodeCount,
                ElementCount = elementCount,
                TextLength = extractedText.Length,
                MaxDepth = maxDepth
            };
        }

        // Recursively aggregates structural statistics for the subtree rooted at <paramref name="node"/>.
        // Returns subtree-local totals; the caller combines results across siblings.
        private static (int NodeCount, int ElementCount, int MaxDepth) ResolveStats(HtmlNode node, int depth)
        {
            // The current node always contributes exactly one to the node count.
            var nodeCount = 1;

            // Element-typed nodes (e.g. <div>, <p>) contribute to elementCount; text nodes,
            // comments, and the document node itself do not.
            var elementCount = node.NodeType == HtmlNodeType.Element ? 1 : 0;

            // Initialize maxDepth to the current depth. If this node is a leaf, this is the
            // final value; otherwise it will be raised by any deeper descendant below.
            var maxDepth = depth;

            // Recurse into each child, incrementing depth by one. Iteration order doesn't
            // affect the result -- sum and max are order-independent -- but ChildNodes
            // preserves document order for callers that care.
            foreach (var childNode in node.ChildNodes)
            {
                var (_nodeCount, _elementCount, _maxDepth) = ResolveStats(
                    node: childNode,
                    depth: depth + 1
                );

                // Fold the child's subtree totals into the running totals for this subtree.
                nodeCount += _nodeCount;
                elementCount += _elementCount;

                // maxDepth is a running maximum, not a sum: only update when a child's
                // subtree reached deeper than anything seen so far at this level.
                if (_maxDepth > maxDepth)
                {
                    maxDepth = _maxDepth;
                }
            }

            // Return the aggregated totals for this subtree to the caller,
            // which will combine them with sibling results or return them
            // as the final SegmentMetrics when at the original root.
            return (nodeCount, elementCount, maxDepth);
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Defines the semantic classification names used for DOM segments produced by
        /// the segmentation pipeline.
        /// </summary>
        /// <remarks>
        /// These values are emitted as stable string names in downstream payloads and
        /// are used to describe the semantic role of each segment.
        /// </remarks>
        public static class SegmentKind
        {
            /// <summary>
            /// Represents a segment that contains account-related actions such as sign in,
            /// sign out, profile access, or rewards actions.
            /// </summary>
            public const string AccountActions = "AccountActions";

            /// <summary>
            /// Represents a segment that groups actionable controls such as buttons,
            /// links, or compact command regions.
            /// </summary>
            public const string ActionGroup = "ActionGroup";

            /// <summary>
            /// Represents a segment that visually or semantically behaves like a card.
            /// </summary>
            public const string Card = "Card";

            /// <summary>
            /// Represents a general content segment that holds meaningful page content.
            /// </summary>
            public const string Content = "Content";

            /// <summary>
            /// Represents a dialog or modal region that should stand as its own segment.
            /// </summary>
            public const string Dialog = "Dialog";

            /// <summary>
            /// Represents a footer region of the page or a major section.
            /// </summary>
            public const string Footer = "Footer";

            /// <summary>
            /// Represents a form region that contains input fields and submission controls.
            /// </summary>
            public const string Form = "Form";

            /// <summary>
            /// Represents a generic segment that does not match a more specific semantic kind.
            /// </summary>
            public const string Generic = "Generic";

            /// <summary>
            /// Represents a header region of the page or a major section.
            /// </summary>
            public const string Header = "Header";

            /// <summary>
            /// Represents a menu-like region that groups related navigation or action items.
            /// </summary>
            public const string Menu = "Menu";

            /// <summary>
            /// Represents a navigation region such as a nav bar, side navigation, or breadcrumb area.
            /// </summary>
            public const string Navigation = "Navigation";

            /// <summary>
            /// Represents a search region such as a search form or search results control area.
            /// </summary>
            public const string Search = "Search";

            /// <summary>
            /// Represents a tabular region such as a table or grid-like structure.
            /// </summary>
            public const string Table = "Table";
        }

        /// <summary>
        /// Provides the central registry of semantic DOM segmentation rules, lookup sets,
        /// and normalization helpers used by the segmentation pipeline.
        /// </summary>
        /// <remarks>
        /// This registry defines semantic roots, breakout roles, action-oriented tags and roles,
        /// account-detection heuristics, preview filtering rules, tokenization helpers, and other
        /// static data used to classify and shape DOM segments deterministically.
        /// </remarks>
        public static class SegmentsRegistry
        {
            /// <summary>
            /// Gets the set of account-related attribute keywords used to detect account,
            /// profile, authentication, and user-identity regions.
            /// </summary>
            /// <remarks>
            /// These keywords are matched against selected attribute values such as
            /// <c>id</c>, <c>class</c>, <c>aria-label</c>, and <c>name</c>.
            /// </remarks>
            public static HashSet<string> AccountAttributeKeywords { get; } = new(StringComparer.OrdinalIgnoreCase)
            {
                "account",
                "auth",
                "authentication",
                "avatar",
                "identity",
                "log-in",
                "log-out",
                "login",
                "logout",
                "member",
                "preferences",
                "profile",
                "session",
                "settings",
                "sign-in",
                "sign-out",
                "signin",
                "signout",
                "user"
            };

            /// <summary>
            /// Gets the set of descendant <c>aria-label</c> fragments that signal an
            /// account-related or identity-related region.
            /// </summary>
            /// <remarks>
            /// These values are used when scanning descendant elements for identity-specific
            /// labels such as sign-in, profile, settings, and related actions.
            /// </remarks>
            public static HashSet<string> AccountDescendantLabels { get; } = new(StringComparer.OrdinalIgnoreCase)
            {
                "account",
                "log in",
                "log out",
                "login",
                "logout",
                "preferences",
                "profile",
                "settings",
                "sign in",
                "sign out",
                "user profile"
            };

            /// <summary>
            /// Gets the segment kinds that may be promoted to <see cref="SegmentKind.AccountActions"/>
            /// when strong account-related signals are detected.
            /// </summary>
            public static HashSet<string> AccountPromotableKinds { get; } =
            [
                SegmentKind.ActionGroup,
                SegmentKind.Content,
                SegmentKind.Generic,
                SegmentKind.Navigation
            ];

            /// <summary>
            /// Gets the ARIA roles that represent actionable or interactive controls.
            /// </summary>
            /// <remarks>
            /// These roles help identify interactive regions even when native HTML tags
            /// do not directly express the action semantics.
            /// </remarks>
            public static HashSet<string> ActionRoles { get; } = new(StringComparer.OrdinalIgnoreCase)
            {
                "button",
                "combobox",
                "link",
                "menuitem",
                "menuitemcheckbox",
                "menuitemradio",
                "option",
                "searchbox",
                "switch",
                "tab",
                "textbox"
            };

            /// <summary>
            /// Gets the native HTML tags that are treated as actionable elements.
            /// </summary>
            public static HashSet<string> ActionTags { get; } = new(StringComparer.OrdinalIgnoreCase)
            {
                "a",
                "button",
                "input",
                "select",
                "textarea"
            };

            /// <summary>
            /// Gets the tags that are always recursively split instead of being emitted
            /// as a single segment when encountered as a segment root.
            /// </summary>
            public static HashSet<string> AlwaysSplitTags { get; } = new(StringComparer.OrdinalIgnoreCase)
            {
                "header"
            };

            /// <summary>
            /// Gets the mapping from ARIA role names to semantic segment kinds.
            /// </summary>
            /// <remarks>ARIA role classification has higher priority than plain tag-based classification.</remarks>
            public static Dictionary<string, string> AriaRoleKinds { get; } = new(StringComparer.OrdinalIgnoreCase)
            {
                ["alertdialog"] = SegmentKind.Dialog,
                ["banner"] = SegmentKind.Header,
                ["complementary"] = SegmentKind.Content,
                ["contentinfo"] = SegmentKind.Footer,
                ["dialog"] = SegmentKind.Dialog,
                ["form"] = SegmentKind.Form,
                ["listbox"] = SegmentKind.Menu,
                ["main"] = SegmentKind.Content,
                ["menu"] = SegmentKind.Menu,
                ["menubar"] = SegmentKind.Menu,
                ["navigation"] = SegmentKind.Navigation,
                ["region"] = SegmentKind.Content,
                ["search"] = SegmentKind.Search,
                ["toolbar"] = SegmentKind.ActionGroup
            };

            /// <summary>
            /// Gets the ARIA roles that should be emitted as breakout segments when found
            /// inside an already emitted parent segment.
            /// </summary>
            public static HashSet<string> BreakoutRoles { get; } = new(StringComparer.OrdinalIgnoreCase)
            {
                "alertdialog",
                "dialog"
            };

            /// <summary>
            /// Gets the generic container tags that may qualify as segment roots when they
            /// satisfy structural and content heuristics.
            /// </summary>
            public static HashSet<string> GenericContainers { get; } = new(StringComparer.OrdinalIgnoreCase)
            {
                "div",
                "ol",
                "ul"
            };

            /// <summary>
            /// Gets the attribute names whose values are collected as useful identifiers
            /// during segment extraction.
            /// </summary>
            public static HashSet<string> IdentifierAttributes { get; } = new(StringComparer.OrdinalIgnoreCase)
            {
                "alt",
                "aria-describedby",
                "aria-label",
                "aria-labelledby",
                "data-cy",
                "data-qa",
                "data-test",
                "data-testid",
                "id",
                "name",
                "placeholder",
                "title",
                "value"
            };

            /// <summary>
            /// Gets the tags that should never be emitted as a single large generic segment
            /// when they exceed size thresholds.
            /// </summary>
            public static HashSet<string> NeverStopTags { get; } = new(StringComparer.OrdinalIgnoreCase)
            {
                "body",
                "div",
                "html",
                "span"
            };

            /// <summary>
            /// Gets the tags treated as structural or content noise during chunk quality checks.
            /// </summary>
            public static HashSet<string> NoiseTags { get; } = new(StringComparer.OrdinalIgnoreCase)
            {
                "img",
                "link",
                "meta",
                "noscript",
                "script",
                "style",
                "svg",
                "template"
            };

            /// <summary>
            /// Gets the attribute names preserved in segment preview HTML.
            /// </summary>
            /// <remarks>
            /// These attributes retain useful locator or semantic context while keeping
            /// the preview small and stable.
            /// </remarks>
            public static HashSet<string> PreviewKeepAttributes { get; } = new(StringComparer.OrdinalIgnoreCase)
            {
                "action",
                "alt",
                "aria-describedby",
                "aria-label",
                "aria-labelledby",
                "class",
                "href",
                "id",
                "name",
                "placeholder",
                "role",
                "title",
                "type",
                "value"
            };

            /// <summary>
            /// Gets the tags removed from preview HTML because they add noise or do not help
            /// downstream exploration.
            /// </summary>
            public static HashSet<string> PreviewRemoveTags { get; } = new(StringComparer.OrdinalIgnoreCase)
            {
                "noscript",
                "script",
                "style",
                "template"
            };

            /// <summary>
            /// Gets the semantic HTML tags that always qualify as segment roots.
            /// </summary>
            public static HashSet<string> SemanticRoots { get; } = new(StringComparer.OrdinalIgnoreCase)
            {
                "article",
                "aside",
                "dialog",
                "footer",
                "form",
                "header",
                "main",
                "nav",
                "section",
                "table"
            };

            /// <summary>
            /// Gets the characters used to split visible text into tokens.
            /// </summary>
            public static char[] SplitChars { get; } =
            [
                ' ',
                '!',
                '"',
                '\'',
                '(',
                ')',
                '+',
                ',',
                '-',
                '.',
                '/',
                ':',
                ';',
                '?',
                '[',
                '\\',
                ']',
                '_',
                '{',
                '|',
                '}',
                '\t',
                '\n',
                '\r'
            ];

            /// <summary>
            /// Gets the stopwords excluded from keyword extraction.
            /// </summary>
            public static HashSet<string> Stopwords { get; } = new(StringComparer.OrdinalIgnoreCase)
            {
                "and",
                "are",
                "can",
                "for",
                "from",
                "get",
                "here",
                "into",
                "know",
                "more",
                "or",
                "that",
                "the",
                "there",
                "this",
                "use",
                "was",
                "were",
                "with",
                "you",
                "your"
            };

            /// <summary>
            /// Gets the SVG attribute names preserved when building cleaned preview output.
            /// </summary>
            public static HashSet<string> SvgKeepAttributes { get; } = new(StringComparer.OrdinalIgnoreCase)
            {
                "aria-label",
                "class",
                "id",
                "role",
                "title"
            };

            /// <summary>
            /// Gets the ARIA roles treated as meaningful semantic or interactive signals.
            /// </summary>
            public static HashSet<string> WellKnownRoles { get; } = new(StringComparer.OrdinalIgnoreCase)
            {
                "alert",
                "alertdialog",
                "banner",
                "button",
                "checkbox",
                "combobox",
                "complementary",
                "contentinfo",
                "dialog",
                "form",
                "link",
                "listbox",
                "main",
                "menu",
                "menubar",
                "menuitem",
                "menuitemcheckbox",
                "menuitemradio",
                "navigation",
                "option",
                "radio",
                "region",
                "search",
                "searchbox",
                "status",
                "switch",
                "tab",
                "tablist",
                "tabpanel",
                "textbox",
                "toolbar"
            };
        }

        /// <summary>
        /// Defines the configurable limits and thresholds used by the DOM segmentation process.
        /// </summary>
        /// <remarks>
        /// These settings control how segment roots are selected, how much metadata is kept,
        /// how previews are generated, and when large regions are recursively split.
        /// Each property has a default value and can be overridden independently.
        /// </remarks>
        public sealed class SegmentOptions
        {
            /// <summary>
            /// Gets or sets a value indicating whether the raw original DOM should be included
            /// in encoded form for each segment.
            /// </summary>
            /// <remarks>
            /// When set to <see langword="false"/>, the encoded DOM field is emitted as an empty string.
            /// The default value is <see langword="true"/>.
            /// </remarks>
            public bool IncludeRawEncodedDom { get; set; } = true;

            /// <summary>
            /// Gets or sets the maximum number of identifier values retained for each segment.
            /// </summary>
            /// <remarks>The default value is 40.</remarks>
            public int MaxIdentifiers { get; set; } = 40;

            /// <summary>
            /// Gets or sets the maximum number of keyword tokens retained for each segment.
            /// </summary>
            /// <remarks>The default value is 40.</remarks>
            public int MaxKeywords { get; set; } = 40;

            /// <summary>
            /// Gets or sets the maximum number of direct child elements required before
            /// a generic container is considered large enough to qualify as a segment root.
            /// </summary>
            /// <remarks>
            /// This threshold applies to generic containers and does not apply to
            /// account-related regions. The default value is 5.
            /// </remarks>
            public int MaxPreviewDepth { get; set; } = 4;

            /// <summary>
            /// Gets or sets the maximum length of a single attribute value that may appear
            /// in the segment preview.
            /// </summary>
            /// <remarks>Longer attribute values are truncated in preview output. The default value is 160.</remarks>
            public int MaxAttributeValueLength { get; set; } = 160;

            /// <summary>
            /// Gets or sets the maximum number of characters allowed in the encoded DOM output.
            /// </summary>
            /// <remarks>
            /// A value of <see langword="null"/> means unlimited length.
            /// Arbitrary truncation can make the tail of a base64 payload non-decodable.
            /// The default value is <see langword="null"/>.
            /// </remarks>
            public int MaxEncodedLength { get; set; }

            /// <summary>
            /// Gets or sets the maximum preview text length written for each segment.
            /// </summary>
            /// <remarks>The default value is 800.</remarks>
            public int MaxPreviewLength { get; set; } = 800;

            /// <summary>
            /// Gets or sets the maximum number of distinct ARIA role values retained
            /// for each segment.
            /// </summary>
            /// <remarks>The default value is 20.</remarks>
            public int MaxRoles { get; set; } = 20;

            /// <summary>
            /// Gets or sets the maximum node count allowed before recursive splitting
            /// is attempted for an oversized segment candidate.
            /// </summary>
            /// <remarks>Oversized generic containers are never emitted as a single segment. The default value is 250.</remarks>
            public int MaxSegmentNodeCount { get; set; } = 250;

            /// <summary>
            /// Gets or sets the minimum number of direct child elements required for a
            /// generic container to qualify as a segment root.
            /// </summary>
            /// <remarks>
            /// This threshold applies to generic containers such as div, ul, and ol.
            /// It does not apply to account-related or identity-related regions.
            /// The default value is 5.
            /// </remarks>
            public int MinContainerChildElements { get; set; } = 5;

            /// <summary>
            /// Gets or sets the minimum visible text length required for text to be treated
            /// as meaningful during generic-container quality checks.
            /// </summary>
            /// <remarks>Text is measured after trimming. The default value is 2.</remarks>
            public int MinMeaningfulTextLength { get; set; } = 2;

            /// <summary>
            /// Gets or sets a value indicating whether oversized semantic segment roots
            /// should be recursively split before being emitted.
            /// </summary>
            /// <remarks>The default value is <see langword="true"/>.</remarks>
            public bool SplitOversizedSemanticSegments { get; set; } = true;

            /// <summary>
            /// Gets or sets a value indicating whether large inline payloads and noisy details
            /// should be stripped from the segment preview.
            /// </summary>
            /// <remarks>
            /// When enabled, preview output is cleaned by stripping SVG internals,
            /// removing noisy attributes, replacing base64 payloads with placeholders,
            /// and truncating long values. The encoded DOM remains unchanged.
            /// The default value is <see langword="true"/>.
            /// </remarks>
            public bool StripLargePayloads { get; set; } = true;
        }

        /// <summary>
        /// Represents structural metrics collected for a DOM segment.
        /// </summary>
        /// <remarks>
        /// This model captures lightweight size and shape measurements for a segment,
        /// such as node counts, text size, and nesting depth.
        /// </remarks>
        public sealed class SegmentMetrics
        {
            /// <summary>
            /// Gets the number of element nodes contained in the segment.
            /// </summary>
            public int ElementCount { get; init; }

            /// <summary>
            /// Gets the maximum nesting depth within the segment, relative to the segment root.
            /// </summary>
            public int MaxDepth { get; init; }

            /// <summary>
            /// Gets the total number of DOM nodes contained in the segment,
            /// including element nodes, text nodes, and other node types.
            /// </summary>
            public int NodeCount { get; init; }

            /// <summary>
            /// Gets the character length of the normalized visible text contained in the segment.
            /// </summary>
            public int TextLength { get; init; }
        }

        /// <summary>
        /// Represents a single deterministic DOM segment produced for exploration,
        /// inspection, and downstream locator workflows.
        /// </summary>
        /// <remarks>
        /// Each segment contains a stable identifier, semantic classification,
        /// structural metadata, normalized text, and both preview and raw DOM forms.
        /// The preview is cleaned for readability, while <see cref="Base64"/>
        /// preserves the original HTML in base64-encoded UTF-8 form.
        /// </remarks>
        public sealed class Segment
        {
            /// <summary>
            /// Gets the base64-encoded UTF-8 representation of the full original
            /// segment HTML.
            /// </summary>
            /// <remarks>
            /// This value always represents the unmodified original DOM, regardless of
            /// preview cleanup. Decode it with
            /// <c>Encoding.UTF8.GetString(Convert.FromBase64String(EncodedDom))</c>.
            /// This value is an empty string when
            /// <see cref="SegmentOptions.IncludeRawEncodedDom"/> is <see langword="false"/>.
            /// </remarks>
            public string Base64 { get; init; }

            /// <summary>
            /// Gets the deduplicated values of locator-relevant attributes found on the
            /// segment root or its descendants.
            /// </summary>
            /// <remarks>
            /// Typical values come from attributes such as <c>id</c>, <c>name</c>,
            /// <c>aria-label</c>, <c>placeholder</c>, and <c>data-testid</c>.
            /// Role values are reported separately in <see cref="Roles"/>.
            /// </remarks>
            public IEnumerable<string> Identifiers { get; init; }

            /// <summary>
            /// Gets the stable unique identifier derived from the segment content
            /// and position.
            /// </summary>
            /// <remarks>The format is <c>{rootTag}-{8-char hex hash}</c>.</remarks>
            public string Id { get; init; }

            /// <summary>
            /// Gets the semantic classification assigned to the segment.
            /// </summary>
            public string Kind { get; init; }

            /// <summary>
            /// Gets the deduplicated lowercase keyword tokens extracted from
            /// <see cref="Text"/>.
            /// </summary>
            public IEnumerable<string> Keywords { get; init; }

            /// <summary>
            /// Gets the actionable-element and role counts collected for the segment.
            /// </summary>
            public DomMetrics Metrics { get; init; }

            /// <summary>
            /// Gets the cleaned HTML preview of the segment.
            /// </summary>
            /// <remarks>
            /// The preview removes noise, compresses SVG internals, strips script and
            /// style elements, replaces base64 payloads, and truncates long values.
            /// Its length is bounded by <see cref="SegmentOptions.MaxPreviewLength"/>.
            /// Truncation is DOM-safe, so the result remains parseable HTML.
            /// </remarks>
            public string Preview { get; init; }

            /// <summary>
            /// Gets the deduplicated meaningful ARIA role values found on the segment
            /// root or its descendants.
            /// </summary>
            /// <remarks>
            /// This list is limited to known useful roles such as button, menu,
            /// menubar, search, dialog, and navigation. It supports fast capability
            /// scanning without reparsing the full HTML.
            /// </remarks>
            public IEnumerable<string> Roles { get; init; }

            /// <summary>
            /// Gets the tag name of the segment root element.
            /// </summary>
            /// <remarks>Example values include <c>nav</c>, <c>form</c>, and <c>div</c>.</remarks>
            public string RootTag { get; init; }

            /// <summary>
            /// Gets the structural metrics collected for the segment.
            /// </summary>
            public SegmentMetrics Stats { get; init; }

            /// <summary>
            /// Gets the normalized visible text extracted from descendant text nodes.
            /// </summary>
            /// <remarks>Whitespace is collapsed during extraction.</remarks>
            public string Text { get; init; }
        }

        /// <summary>
        /// Represents aggregated DOM interaction metrics collected from HTML elements
        /// and ARIA roles within a DOM subtree.
        /// </summary>
        /// <remarks>
        /// This model tracks counts of common actionable and form-related elements,
        /// such as buttons, links, inputs, selects, text areas, and forms. It also
        /// supports incremental aggregation from another metrics instance or directly
        /// from an <see cref="HtmlNode"/>.
        /// </remarks>
        public sealed class DomMetrics
        {
            /// <summary>
            /// Gets or sets the number of button-like elements found in the DOM.
            /// </summary>
            /// <remarks>
            /// This count includes native <c>button</c> elements and ARIA button-like
            /// roles that are not already represented by a native button tag.
            /// </remarks>
            public int Buttons { get; set; }

            /// <summary>
            /// Gets or sets the number of form elements found in the DOM.
            /// </summary>
            public int Forms { get; set; }

            /// <summary>
            /// Gets or sets the number of input-like elements found in the DOM.
            /// </summary>
            /// <remarks>
            /// This count includes native <c>input</c> and <c>textarea</c>-like controls
            /// through both native tags and ARIA textbox-style roles.
            /// </remarks>
            public int Inputs { get; set; }

            /// <summary>
            /// Gets or sets the number of link-like elements found in the DOM.
            /// </summary>
            /// <remarks>
            /// This count includes native anchor elements and ARIA link roles that are
            /// not already represented by a native anchor tag.
            /// </remarks>
            public int Links { get; set; }

            /// <summary>
            /// Gets or sets the number of select-like elements found in the DOM.
            /// </summary>
            /// <remarks>
            /// This count includes native <c>select</c> elements and ARIA combobox roles
            /// that are not already represented by a native select tag.
            /// </remarks>
            public int Selects { get; set; }

            /// <summary>
            /// Gets or sets the number of textarea elements found in the DOM.
            /// </summary>
            public int TextAreas { get; set; }

            /// <summary>
            /// Adds the values from another <see cref="DomMetrics"/> instance into the current instance.
            /// </summary>
            /// <param name="other">The metrics instance whose values will be accumulated into the current instance.</param>
            /// <returns>The current <see cref="DomMetrics"/> instance after aggregation.</returns>
            public DomMetrics AddMetrics(DomMetrics other)
            {
                // Accumulate each metric count from the supplied metrics instance.
                Buttons += other.Buttons;
                Forms += other.Forms;
                Inputs += other.Inputs;
                Links += other.Links;
                Selects += other.Selects;
                TextAreas += other.TextAreas;

                // Return the current instance to allow fluent chaining.
                return this;
            }

            /// <summary>
            /// Inspects the supplied HTML node and updates the current metrics instance
            /// based on its native tag and ARIA role.
            /// </summary>
            /// <param name="node">The HTML node to inspect.</param>
            /// <returns>The current <see cref="DomMetrics"/> instance after the node has been evaluated.</returns>
            /// <remarks>
            /// Native HTML tags are counted first. ARIA roles are then counted only when
            /// the native tag did not already represent the same interaction type, which
            /// avoids double-counting standard elements.
            /// </remarks>
            public DomMetrics AddMetrics(HtmlNode node)
            {
                // Only element nodes contribute to DOM interaction metrics.
                if (node.NodeType != HtmlNodeType.Element)
                {
                    return this;
                }

                // Normalize the tag and role values for consistent matching.
                var tag = node.Name.ToLowerInvariant();
                var role = node.GetAttributeValue("role", "").Trim().ToLowerInvariant();

                // Count native HTML elements first.
                switch (tag)
                {
                    case "a":
                        Links++;
                        break;
                    case "button":
                        Buttons++;
                        break;
                    case "form":
                        Forms++;
                        break;
                    case "input":
                        Inputs++;
                        break;
                    case "select":
                        Selects++;
                        break;
                    case "textarea":
                        TextAreas++;
                        break;
                }

                // Count ARIA role equivalents only when the native tag has not already
                // been counted for the same interaction type.
                switch (role)
                {
                    case "button":
                        if (tag != "button")
                        {
                            Buttons++;
                        }
                        break;

                    case "combobox":
                        if (tag != "select")
                        {
                            Selects++;
                        }
                        break;

                    case "link":
                        if (tag != "a")
                        {
                            Links++;
                        }
                        break;

                    case "menuitem":
                    case "menuitemcheckbox":
                    case "menuitemradio":
                        Buttons++;
                        break;

                    case "searchbox":
                    case "textbox":
                        if (tag != "input" && tag != "textarea")
                        {
                            Inputs++;
                        }
                        break;
                }

                // Return the current instance to allow fluent chaining.
                return this;
            }
        }
        #endregion
    }

    namespace Extensions
    {
        /// <summary>
        /// Extension methods for HtmlNode to support DOM traversal
        /// and preview sanitization during segment creation.
        /// </summary>
        internal static class Extentions
        {
            extension(HtmlNode node)
            {
                // Sanitizes a cloned DOM subtree so it can be used as a compact preview representation.
                public void ClearPreviewNode(int maxValueLen)
                {
                    // Non-element nodes do not have attributes or tag names.
                    // Continue walking their children so nested elements are still sanitized.
                    if (node.NodeType != HtmlNodeType.Element)
                    {
                        // To avoid issues with nodes detaching themselves during
                        // traversal, we call ClearPreviewNode on child nodes without
                        // using a foreach loop directly on the ChildNodes collection.
                        foreach (var childNode in node.ChildNodes)
                        {
                            childNode.ClearPreviewNode(maxValueLen);
                        }

                        // Return early because there are no attributes to
                        // sanitize or tags to check on non-element nodes.
                        return;
                    }

                    // Normalize the tag name once so registry lookups are case-insensitive.
                    var tagName = node.Name.ToLowerInvariant();

                    // Remove preview-noise elements completely.
                    // This is safe during traversal because callers should enumerate child snapshots
                    // when a node may detach itself from the tree.
                    if (DomPartitioner.SegmentsRegistry.PreviewRemoveTags.Contains(tagName))
                    {
                        node.Remove();
                        return;
                    }

                    // SVG nodes are treated specially:
                    // keep only a small SVG-safe attribute set and discard all inner SVG content.
                    var isSvg = tagName == "svg";
                    var keepSet = isSvg
                        ? DomPartitioner.SegmentsRegistry.SvgKeepAttributes
                        : DomPartitioner.SegmentsRegistry.PreviewKeepAttributes;

                    // Collect attributes first, then remove them after enumeration.
                    // This avoids mutating the attribute collection while iterating it.
                    var toRemove = new List<HtmlAttribute>();

                    // Iterate over attributes to find ones that should be removed or sanitized for preview.
                    foreach (var attribute in node.Attributes)
                    {
                        // Remove attributes that are not useful for preview or locator context.
                        if (!keepSet.Contains(attribute.Name))
                        {
                            toRemove.Add(attribute);
                        }
                        else
                        {
                            // Sanitize kept values so large/base64/noisy values do not bloat the preview.
                            attribute.Value = FormatAttributeValue(attribute.Value, maxValueLen);
                        }
                    }

                    // Apply the removals after the scan is complete.
                    foreach (var attr in toRemove)
                    {
                        node.Attributes.Remove(attr);
                    }

                    // For SVG elements, remove all child nodes to
                    // keep the preview compact and avoid rendering issues,
                    if (isSvg)
                    {
                        // Keep the SVG element itself as a compact placeholder with useful attributes only.
                        node.RemoveAllChildren();
                        return;
                    }

                    // Continue sanitizing descendants.
                    // ToArray() protects traversal when child nodes remove themselves.
                    foreach (var child in node.ChildNodes.ToArray())
                    {
                        ClearPreviewNode(child, maxValueLen);
                    }

                    // Formats an HTML attribute value so it remains useful but does not make preview output too large.
                    static string FormatAttributeValue(string value, int maxLen)
                    {
                        // Preserve null and empty values as-is.
                        if (string.IsNullOrEmpty(value))
                        {
                            return value;
                        }

                        // Detect embedded base64 data URIs.
                        // These can be extremely large, especially for inline images or fonts.
                        var base64Flag = value.IndexOf(";base64,", StringComparison.OrdinalIgnoreCase);

                        // Replace base64 payloads with a compact placeholder, while keeping the data URI
                        // prefix so the media/content type remains visible in the preview.
                        if (base64Flag >= 0 && value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        {
                            var prefix = value[..(base64Flag + 8)];
                            return prefix + "[truncated]";
                        }

                        // Cap long regular attribute values to keep preview metadata compact.
                        if (value.Length > maxLen)
                        {
                            return value[..maxLen] + "...";
                        }

                        // Return short regular values unchanged.
                        return value;
                    }
                }

                /// <summary>
                /// Counts all nodes in the current node subtree.
                /// </summary>
                /// <returns>The total number of nodes in the subtree, including the current node.</returns>
                public int Count()
                {
                    // Default to recursive counting so the full subtree size is returned.
                    return node.Count(recursive: true);
                }

                /// <summary>
                /// Counts nodes related to the current node.
                /// </summary>
                /// <param name="recursive">When true, counts the current node and all descendant nodes. When false, counts only direct element children.</param>
                /// <returns>The number of nodes found according to the selected counting mode.</returns>
                public int Count(bool recursive)
                {
                    // Choose between full subtree counting and direct-child element counting.
                    return recursive
                        ? CountRecurse(node)
                        : CountDirect(node);

                    // Counts the supplied node and every descendant node recursively.
                    static int CountRecurse(HtmlNode node)
                    {
                        // Start with 1 to include the current node itself.
                        var count = 1;

                        // Add the count of every child subtree.
                        foreach (var child in node.ChildNodes)
                        {
                            count += CountRecurse(child);
                        }

                        // Return the full subtree count for this node.
                        return count;
                    }

                    // Counts only direct child elements of the supplied node.
                    static int CountDirect(HtmlNode node)
                    {
                        // Direct mode intentionally starts at 0 because the current node itself
                        // is not included, and non-element children are ignored.
                        var count = 0;

                        // Count only direct children that are actual HTML elements.
                        foreach (var child in node.ChildNodes)
                        {
                            if (child.NodeType == HtmlNodeType.Element)
                            {
                                count++;
                            }
                        }

                        // Return the number of direct element children.
                        return count;
                    }
                }

                /// <summary>
                /// Gets the first direct element child of the supplied HTML node.
                /// </summary>
                /// <param name="node">The parent node whose direct children should be inspected.</param>
                /// <returns>The first direct child whose NodeType is Element, or null when no direct element child exists.</returns>
                public HtmlNode GetFirstElement()
                {
                    // Walk only direct children.
                    // Text, comment, document, and other non-element nodes are skipped.
                    foreach (var childNode in node.ChildNodes)
                    {
                        // If this child is an element, return it
                        // immediately as the first direct element child.
                        if (childNode.NodeType == HtmlNodeType.Element)
                        {
                            return childNode;
                        }
                    }

                    // No direct element child was found.
                    return null;
                }

                /// <summary>
                /// Resolves the segment kind for an HTML element based on its tag name and selected semantic attributes.
                /// </summary>
                /// <param name="node">The HTML node being classified.</param>
                /// <param name="tag">The normalized tag name of the node.</param>
                /// <returns>A <see cref="SegmentKind" /> value that describes the semantic purpose of the node.</returns>
                public string ResolveTagKind(string tag)
                {
                    // Classify well-known structural tags first.
                    // Some tags require additional inspection because their semantic role can vary.
                    return tag switch
                    {
                        // A form can represent either a regular form or a search area.
                        "form" => AssertSearchForm(node) ? DomPartitioner.SegmentKind.Search : DomPartitioner.SegmentKind.Form,

                        // Direct semantic HTML mappings.
                        "nav" => DomPartitioner.SegmentKind.Navigation,
                        "header" => DomPartitioner.SegmentKind.Header,
                        "footer" => DomPartitioner.SegmentKind.Footer,
                        "dialog" => DomPartitioner.SegmentKind.Dialog,
                        "main" => DomPartitioner.SegmentKind.Content,
                        "table" => DomPartitioner.SegmentKind.Table,

                        // Aside is usually supporting content, but still treated as content for segmentation.
                        "aside" => DomPartitioner.SegmentKind.Content,

                        // Articles are commonly reusable content cards/items.
                        "article" => DomPartitioner.SegmentKind.Card,

                        // Sections are generic containers, so inspect role attributes before deciding.
                        "section" => ResolveSection(node),

                        // Unknown or unsupported tags fall back to a generic segment.
                        _ => DomPartitioner.SegmentKind.Generic
                    };

                    // Determines whether the supplied form node represents a search form.
                    static bool AssertSearchForm(HtmlNode node)
                    {
                        // ARIA role="search" is the strongest explicit signal.
                        var role = node.GetAttributeValue("role", "");
                        var isSearchRole = string.Equals(
                            a: role,
                            b: "search",
                            comparisonType: StringComparison.OrdinalIgnoreCase);

                        // When role="search" is present, this node is very likely
                        // a search form even if it uses a non-search tag.
                        if (isSearchRole)
                        {
                            return true;
                        }

                        // Search forms often include "search" in the action URL.
                        var action = node.GetAttributeValue("action", "");
                        var isSearchAction = action.Contains(
                            value: "search",
                            comparisonType: StringComparison.InvariantCultureIgnoreCase);

                        // When "search" appears in the action URL, this is a strong
                        // signal that this form is intended for search.
                        if (isSearchAction)
                        {
                            return true;
                        }

                        // A descendant input with type="search" is also a strong search-form signal.
                        return node.SelectSingleNode(".//*[@type='search']") != null;
                    }

                    // Classifies a section element using ARIA role information when available.
                    static string ResolveSection(HtmlNode node)
                    {
                        // Normalize the role value so role matching is stable and case-insensitive.
                        var role = node.GetAttributeValue("role", "").Trim().ToLowerInvariant();

                        // Section tags are broad, so role is used to refine their segment kind.
                        return role switch
                        {
                            "dialog" or "alertdialog" => DomPartitioner.SegmentKind.Dialog,
                            "search" => DomPartitioner.SegmentKind.Search,
                            "navigation" => DomPartitioner.SegmentKind.Navigation,
                            "form" => DomPartitioner.SegmentKind.Form,
                            _ => DomPartitioner.SegmentKind.Content
                        };
                    }
                }
            }
        }
    }
}
