using G4.Services.Domain.V4.Mcp;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ModelContextProtocol.Server;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace G4.Services.UnitTests.Mcp
{
    [TestClass]
    [TestCategory("McpCatalog")]
    [TestCategory("UnitTest")]
    public sealed class ToolCatalogTests
    {
        #region *** Data Set ***
        [DataRow(typeof(G4SystemTools))]
        [DataRow(typeof(FlowTools))]
        [DataRow(typeof(RecordingTools))]
        #endregion
        [TestMethod(DisplayName = "Verify every catalog entry of a tool type matches exactly one tool method " +
            "and declares its name, title, and description.")]
        public void ToolCatalogCoverageTest(Type toolType)
        {
            // Arrange: read the published tool names from the attributes and the catalog entries by reflection.
            var methodNames = GetToolMethods(toolType)
                .Select(method => method.GetCustomAttribute<McpServerToolAttribute>().Name)
                .Order(comparer: StringComparer.Ordinal)
                .ToArray();
            var entries = GetCatalogEntries(toolType);

            // Act: resolve the published name of every catalog entry.
            var entryNames = entries
                .Select(entry => GetConstant(entry, name: "Name"))
                .Order(comparer: StringComparer.Ordinal)
                .ToArray();

            // Assert: no tool method lacks an entry and no entry is orphaned, so a renamed tool fails here.
            Assert.AreSequenceEqual(expected: methodNames, actual: entryNames);

            // Assert: every entry carries the wording a client reads for the tool itself.
            foreach (var entry in entries)
            {
                var title = GetConstant(entry, name: "Title");
                var description = GetConstant(entry, name: "Description");
                Assert.IsFalse(condition: string.IsNullOrWhiteSpace(title), message: entry.Name);
                Assert.IsFalse(condition: string.IsNullOrWhiteSpace(description), message: entry.Name);
            }
        }

        #region *** Data Set ***
        [DataRow(typeof(G4SystemTools))]
        [DataRow(typeof(FlowTools))]
        [DataRow(typeof(RecordingTools))]
        #endregion
        [TestMethod(DisplayName = "Verify every tool method reads its title, description, and argument " +
            "descriptions from its own catalog entry.")]
        public void ToolCatalogWordingTest(Type toolType)
        {
            // Arrange: index the catalog entries by published name so each method finds its own entry.
            var entries = GetCatalogEntries(toolType)
                .ToDictionary(keySelector: entry => GetConstant(entry, name: "Name"));

            foreach (var method in GetToolMethods(toolType))
            {
                // Arrange: select the entry and the client-supplied parameters; the SDK injects cancellation tokens.
                var tool = method.GetCustomAttribute<McpServerToolAttribute>();
                var entry = entries[tool.Name];
                var arguments = entry.GetNestedType(name: "Arguments");
                var parameters = method
                    .GetParameters()
                    .Where(predicate: parameter => parameter.ParameterType != typeof(CancellationToken))
                    .ToArray();

                // Act: read the tool-level wording exactly where the SDK reads it, from the method attributes.
                var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description;
                var expectedTitle = GetConstant(entry, name: "Title");
                var expectedDescription = GetConstant(entry, name: "Description");

                // Assert: the tool-level wording is the entry wording, so no method reads another tool's text.
                Assert.AreEqual(expected: expectedTitle, actual: tool.Title, message: tool.Name);
                Assert.AreEqual(expected: expectedDescription, actual: description, message: tool.Name);

                // Assert: each argument reads the constant named after it, which keeps the PascalCase convention.
                foreach (var parameter in parameters)
                {
                    var constantName = ConvertToPascalCase(parameter.Name);
                    var expected = arguments is null ? null : GetConstant(arguments, name: constantName);
                    var actual = parameter.GetCustomAttribute<DescriptionAttribute>()?.Description;
                    var message = $"{tool.Name}.{parameter.Name}";
                    Assert.IsFalse(condition: string.IsNullOrWhiteSpace(actual), message);
                    Assert.AreEqual(expected, actual, message);
                }

                // Assert: the entry declares no argument wording that no parameter reads.
                var argumentNames = parameters
                    .Select(parameter => ConvertToPascalCase(parameter.Name))
                    .Order(comparer: StringComparer.Ordinal)
                    .ToArray();
                var constantNames = GetConstants(arguments)
                    .Select(field => field.Name)
                    .Order(comparer: StringComparer.Ordinal)
                    .ToArray();
                Assert.AreSequenceEqual(expected: argumentNames, actual: constantNames, message: tool.Name);
            }
        }

        // Converts a camelCase parameter name into the PascalCase constant name its catalog entry declares, so the
        // tests apply the same naming convention the catalog authors follow.
        private static string ConvertToPascalCase(string name)
        {
            // Raise the first character only; the remaining characters already follow the parameter casing.
            return char.ToUpperInvariant(name[0]) + name[1..];
        }

        // Returns the catalog entry classes of a tool type: every nested class of its private Catalog except the
        // private Common class, which holds reused wording and has no tool of its own. A missing catalog fails the
        // calling test, because every tool type is expected to keep its wording there.
        private static Type[] GetCatalogEntries(Type toolType)
        {
            // Resolve the private catalog by its conventional name; every tool type uses the same name.
            var catalog = toolType.GetNestedType(name: "Catalog", bindingAttr: BindingFlags.NonPublic);
            Assert.IsNotNull(value: catalog, message: $"{toolType.Name} declares no nested Catalog.");

            // Keep the public entries only; Common is private and never matches a tool.
            return catalog.GetNestedTypes(bindingAttr: BindingFlags.Public);
        }

        // Returns the value of one public string constant of a catalog class, or null when it is not declared, so a
        // missing constant surfaces as an assertion difference instead of a reflection exception.
        private static string GetConstant(Type type, string name)
        {
            // Read the compile-time value; a const field has no instance to read from.
            var field = type.GetField(name, bindingAttr: BindingFlags.Public | BindingFlags.Static);
            return field is { IsLiteral: true } ? (string)field.GetRawConstantValue() : null;
        }

        // Returns the public constants of a catalog class, or an empty sequence for a tool without arguments.
        private static IEnumerable<FieldInfo> GetConstants(Type type)
        {
            // Treat an absent Arguments class as an empty argument list.
            if (type is null)
            {
                return [];
            }

            // Keep literal fields only; the catalog never declares other members.
            return type
                .GetFields(bindingAttr: BindingFlags.Public | BindingFlags.Static)
                .Where(predicate: field => field.IsLiteral);
        }

        // Returns the methods of a tool type that the SDK publishes, identified by the tool attribute exactly as the
        // server registration discovers them.
        private static MethodInfo[] GetToolMethods(Type toolType)
        {
            // Select the public instance methods marked as tools.
            return toolType
                .GetMethods(bindingAttr: BindingFlags.Instance | BindingFlags.Public)
                .Where(predicate: method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
                .ToArray();
        }
    }
}
