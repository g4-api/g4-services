using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace G4.Services.Domain.V4.Extensions
{
    /// <summary>
    /// Provides utility methods for controllers in the G4™ application.
    /// </summary>
    public static class ControllerUtilities
    {
        /// <summary>
        /// Reads all SVG files from the specified wwwroot directory and returns a dictionary
        /// where the keys are the formatted file names (without "icon-" prefix and ".svg" extension),
        /// and the values are the content of the SVG files.
        /// </summary>
        /// <param name="wwwrootPath">The absolute path to the wwwroot directory.</param>
        /// <returns>A dictionary with formatted file names as keys and SVG file contents as values.</returns>
        public static Dictionary<string, string> ReadSvgs(string wwwrootPath)
        {
            // Static method to format file paths by removing the "icon-" prefix and the ".svg" extension.
            static string FormatKey(string input)
            {
                // Define a case-insensitive string comparison for replacements.
                const StringComparison comparison = StringComparison.OrdinalIgnoreCase;

                // Extract the file name without its extension from the input path.
                var name = Path.GetFileNameWithoutExtension(input);

                // Remove "icon-" prefix from the input string to generate the dictionary key.
                return name.Replace(
                    oldValue: "icon-",
                    newValue: string.Empty,
                    comparison);
            }

            // If the specified wwwroot directory does not exist, return an empty dictionary.
            if (!Directory.Exists(wwwrootPath))
            {
                return [];
            }

            // Enumerate all files recursively under wwwroot, filtering to include only SVG files.
            // Get all files recursively.
            // Convert file paths to relative and use forward slashes.
            // Filter for files that end with ".svg".
            // Use the FormatKey function to generate the dictionary key (formatted file name).
            // Read the content of each SVG file and use it as the dictionary value.
            return Directory.EnumerateFiles(wwwrootPath, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(wwwrootPath, f).Replace("\\", "/"))
                .Where(i => i.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    FormatKey,
                    i => File.ReadAllText(Path.Combine(wwwrootPath, i))
                );
        }

        /// <summary>
        /// Writes the G4™ Hub ASCII logo to the console, including the specified version number.
        /// </summary>
        /// <param name="version">The version number to display in the logo.</param>
        public static void WriteHubAsciiLogo(string version)
        {
            // Define the ASCII art logo with placeholders for version information.
            var logo = new string[]
            {
                "    ____ _  _     _   _       _        ",
                "   / ___| || |   | | | |_   _| |__     ",
                "  | |  _| || |_  | |_| | | | | '_ \\   ",
                "  | |_| |__   _| |  _  | |_| | |_) |   ",
                "   \\____|  |_|   |_| |_|\\__,_|_.__/  ",
                "                                       ",
                "       G4™ - Automation as a Service   ",
                "               Powered by G4™-Engine   ",
                "                                       ",
                "  Version: " + version + "             ",
                "  Project: https://github.com/g4-api   ",
                "                                       "
             };

            // Clear the console before writing the logo to ensure a clean display.
            Console.Clear();

            // Set the console output encoding to UTF-8 to support Unicode characters.
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Output the logo to the console by joining the array elements with a newline character.
            Console.WriteLine(string.Join("\n", logo));
        }

        /// <summary>
        /// Writes the G4™ Worker ASCII logo to the console, including the specified version number.
        /// </summary>
        /// <param name="version">The version number to display in the logo.</param>
        public static void WriteWorkerAsciiLogo(string version)
        {
            // Define the ASCII art logo with placeholders for version information.
            var logo = new string[]
            {
                "    ____ _  _    __        __         _                    ",
                "   / ___| || |   \\ \\      / /__  _ __| | _____ _ __      ",
                "  | |  _| || |_   \\ \\ /\\ / / _ \\| '__| |/ / _ \\ '__|  ",
                "  | |_| |__   _|   \\ V  V / (_) | |  |   <  __/ |         ",
                "   \\____|  |_|      \\_/\\_/ \\___/|_|  |_|\\_\\___|_|    ",
                "                                                           ",
                "                       G4™ - Automation as a Service       ",
                "                               Powered by G4™-Engine       ",
                "                                                           ",
                "  Version: " + version + "                                 ",
                "  Project: https://github.com/g4-api                       ",
                "                                                           "
             };

            // Clear the console before writing the logo to ensure a clean display.
            Console.Clear();

            // Set the console output encoding to UTF-8 to support Unicode characters.
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Output the logo to the console by joining the array elements with a newline character.
            Console.WriteLine(string.Join("\n", logo));
        }
    }
}
