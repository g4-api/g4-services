using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace G4.Services.Domain.V4.Formatters
{
    /// <inheritdoc />
    public sealed class DictionaryStringObjectJsonConverter : JsonConverter<IDictionary<string, object>>
    {
        /// <inheritdoc />
        public override IDictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Delegate the actual JSON-to-dictionary materialization to the helper.
            // This is expected to return either a mutable Dictionary<string, object> or null
            // when the current token is JSON null.
            var dictionary = ReadDictionary(ref reader, options);

            // If nothing was read (e.g., JSON null), propagate null; otherwise, wrap the
            // concrete dictionary with a ReadOnlyDictionary to enforce immutability.
            // Note: Even though the return type is IDictionary<string, object>, callers should
            // treat the instance as read-only to avoid runtime exceptions on mutation attempts.
            return dictionary is null
                ? null
                : new ReadOnlyDictionary<string, object>(dictionary);
        }

        /// <inheritdoc />
        public override void Write(
            Utf8JsonWriter writer,
            IDictionary<string, object> value, JsonSerializerOptions options) => WriteDictionary(writer, value, options);

        // Reads the current JSON value as a case-insensitive Dictionary mapping string keys to boxed object values.
        private static Dictionary<string, object> ReadDictionary(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            // Allow top-level nulls for optional dictionaries.
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            // Validate that we're at the start of a JSON object: { ... }
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject for dictionary.");
            }

            // Create a case-insensitive dictionary so "Name" and "name" are treated the same.
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // Iterate through the object members until we hit the closing '}'.
            while (reader.Read())
            {
                // Reached the end of the current object scope.
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                // Each property must begin with a PropertyName token.
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Expected PropertyName.");
                }

                // Extract the property name as the dictionary key.
                var key = reader.GetString();

                // Advance to the property's value token.
                reader.Read();

                // Delegate to ReadAny to materialize the value (object, array, primitive, or null).
                // If the same key appears again (case-insensitively), this overwrites the prior value.
                result[key] = ReadAny(ref reader, options);
            }

            // Return the fully populated dictionary.
            return result;
        }

        // Recursively reads the current JSON token and materializes it as a boxed CLR value.
        // Supports primitives, arrays, and objects, returning:
        private static object ReadAny(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            // Local helper: read an array recursively into List<object>.
            static List<object> ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                // Initialize a new list to hold the elements of the array.
                var list = new List<object>();

                // Advance through the array and read each element until we hit the closing bracket.
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    // Recursively materialize each element and append to the list.
                    list.Add(ReadAny(ref reader, options));
                }

                // Return the populated list.
                return list;
            }

            // Local helper: read an object recursively into a case-insensitive Dictionary<string, object>.
            static Dictionary<string, object> ReadObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                // Initialize a new case-insensitive dictionary to hold the properties of the object.
                var inner = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                // Iterate over properties until the end of the object is reached.
                while (reader.Read())
                {
                    // If we hit the end of the object, break out of the loop.
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        break;
                    }

                    // Each property must start with a PropertyName token.
                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        throw new JsonException("Expected PropertyName in object.");
                    }

                    // Extract the property name (null-forgiving operator because PropertyName guarantees a string).
                    string key = reader.GetString();

                    // Move to the property's value token.
                    reader.Read();

                    // Recursively read the property's value.
                    inner[key] = ReadAny(ref reader, options);
                }

                // Return the populated dictionary.
                return inner;
            }

            // Local helper: capture the current value as a JsonElement by parsing and cloning it.
            // Cloning ensures the element remains valid after the JsonDocument is disposed and the reader moves on.
            static object ReadJsonElement(ref Utf8JsonReader reader)
            {
                // Parse the current JSON value into a JsonDocument, which allows us to read it as a JsonElement.
                using var document = JsonDocument.ParseValue(ref reader);

                // Clone the root element to ensure it remains valid after the document is disposed.
                return document.RootElement.Clone();
            }

            // Dispatch based on the current token type.
            return reader.TokenType switch
            {
                // Null literal -> null
                JsonTokenType.Null => null,

                // Booleans -> bool
                JsonTokenType.True => true,
                JsonTokenType.False => false,

                // Strings -> string
                JsonTokenType.String => reader.GetString(),

                // Arrays -> List<object> (recursive)
                JsonTokenType.StartArray => ReadArray(ref reader, options),

                // Objects -> Dictionary<string, object> (recursive, case-insensitive keys)
                JsonTokenType.StartObject => ReadObject(ref reader, options),

                // Numbers -> prefer Int64, then Double, finally Decimal to preserve precision on large/precise values.
                JsonTokenType.Number =>
                    reader.TryGetInt64(out long l) ? l :
                    reader.TryGetDouble(out double d) ? d :
                    JsonDocument.ParseValue(ref reader).RootElement.GetDecimal(),

                // Fallback for other token kinds (e.g., comments, unexpected): materialize as JsonElement.
                _ => ReadJsonElement(ref reader)
            };
        }

        // Writes a boxed CLR value to the provided Utf8JsonWriter using
        // sensible mappings for primitives, numbers, JSON elements, dictionaries, and lists.
        // Falls back to JsonSerializerfor unsupported types (e.g., POCOs).
        private static void WriteAny(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            // Write JSON null for null values and return early.
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            // Type-directed write: choose the most specific and efficient mapping.
            switch (value)
            {
                // Strings → JSON string
                case string s:
                    {
                        writer.WriteStringValue(s);
                        return;
                    }

                // Booleans → JSON boolean
                case bool b:
                    {
                        writer.WriteBooleanValue(b);
                        return;
                    }

                // Common numeric primitives → JSON number
                case int i:
                    {
                        writer.WriteNumberValue(i);
                        return;
                    }
                case long l:
                    {
                        writer.WriteNumberValue(l);
                        return;
                    }
                case float f:
                    {
                        writer.WriteNumberValue(f);
                        return;
                    }
                case double d:
                    {
                        writer.WriteNumberValue(d);
                        return;
                    }
                case decimal m:
                    {
                        writer.WriteNumberValue(m);
                        return;
                    }

                // JsonElement → write the element's raw JSON directly.
                case JsonElement je:
                    {
                        je.WriteTo(writer);
                        return;
                    }

                // Read-only dictionary of string→object → JSON object
                case IReadOnlyDictionary<string, object> ro:
                    {
                        // Delegate to a helper that enumerates keys/values and writes recursively.
                        WriteDictionary(writer, ro, options);
                        return;
                    }

                // Mutable dictionary of string→object → JSON object
                case IDictionary<string, object> d1:
                    {
                        // Same helper handles mutable dictionaries as well.
                        WriteDictionary(writer, d1, options);
                        return;
                    }

                // List-like sequence of objects → JSON array
                case IEnumerable<object> list:
                    {
                        writer.WriteStartArray();
                        // Recursively write each element with the same rules.
                        foreach (var item in list) WriteAny(writer, item, options);
                        writer.WriteEndArray();
                        return;
                    }

                // Fallback: for any other type (including other IEnumerable<T> or POCOs),
                // use JsonSerializer with the provided options to honor converters/formatting.
                default:
                    {
                        // Note: value.GetType() preserves runtime type for accurate serialization.
                        JsonSerializer.Serialize(writer, value, value.GetType(), options);
                        return;
                    }
            }
        }

        // Writes a JSON object from a sequence of KeyValuePair entries, using each pair's Key as
        // the property name and serializing the Value via JsonSerializerOptions.
        private static void WriteDictionary(Utf8JsonWriter writer, IEnumerable<KeyValuePair<string, object>> value, JsonSerializerOptions options)
        {
            // Begin the JSON object scope: write '{'
            writer.WriteStartObject();

            // Iterate all entries in the provided sequence. Enumeration order determines the
            // property order in the resulting JSON.
            foreach (var item in value)
            {
                // Write the property name (key) for the next property.
                writer.WritePropertyName(item.Key);

                // Recursively write the value using type-directed logic (primitives, arrays, objects, etc.).
                WriteAny(writer, item.Value, options);
            }

            // Close the JSON object scope: write '}'
            writer.WriteEndObject();
        }
    }
}