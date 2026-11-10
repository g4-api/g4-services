using G4.Models;

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
        /// Writes the Hub banner to the console, including the specified version number.
        /// </summary>
        /// <param name="version">The version number to display in the banner.</param>
        /// <param name="branding">The resolved branding that selects the banner mode and supplies its text.</param>
        /// <remarks>
        /// The Default mode writes the built-in G4 Hub art, the Text mode writes the branded Hub name as plain text,
        /// and the File mode writes the art read from <see cref="BrandingBannerModel.File"/>; a missing or unreadable
        /// file falls back to the Text mode. Every mode then writes the tagline, the version, and the project page.
        /// The "Powered by G4™-Engine" credit is always written, because the license requires the credit.
        /// </remarks>
        public static void WriteHubAsciiLogo(string version, BrandingSettingsModel branding)
        {
            // Require the branding, so the banner never falls back silently to text the operator did not choose.
            ArgumentNullException.ThrowIfNull(argument: branding);

            // Select the banner head by mode, so the operator decides whether the G4 art is shown.
            var head = GetBannerHead(branding);

            // Append the branded tagline, the fixed engine credit, the version, and the project page to the head.
            var logo = new List<string>(head)
            {
                "                                       ",
                $"       {branding.ResolveText("{productTm}")} - {branding.Tagline}   ",
                "               Powered by G4™-Engine   ",
                "                                       ",
                "  Version: " + version + "             ",
                $"  Project: {branding.ProjectUrl}   ",
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

        // Returns the banner head lines for the selected mode: the built-in G4 Hub art, the branded Hub name, or the
        // lines of the banner art file. A File mode whose file is missing or unreadable returns the branded Hub name,
        // so a bad path never stops the Hub from starting.
        private static string[] GetBannerHead(BrandingSettingsModel branding)
        {
            // Write the branded Hub name when the operator chose plain text.
            var hubNameHead = new[] { "", $"  {branding.ResolveText("{hubTm}")}" };

            if (string.Equals(branding.ConsoleBanner.Mode, BrandingBannerModes.Text, StringComparison.OrdinalIgnoreCase))
            {
                return hubNameHead;
            }

            // Read the operator's banner art when the File mode names a readable file.
            if (string.Equals(branding.ConsoleBanner.Mode, BrandingBannerModes.File, StringComparison.OrdinalIgnoreCase))
            {
                return ReadBannerFile(branding.ConsoleBanner.File) ?? hubNameHead;
            }

            // Write the built-in G4 Hub art for the Default mode and for any unknown mode.
            return
            [
                "    ____ _  _     _   _       _        ",
                "   / ___| || |   | | | |_   _| |__     ",
                "  | |  _| || |_  | |_| | | | | '_ \\   ",
                "  | |_| |__   _| |  _  | |_| | |_) |   ",
                "   \\____|  |_|   |_| |_|\\__,_|_.__/  "
            ];
        }

        // Reads the lines of the banner art file, or returns null when no file is named or it cannot be read.
        private static string[] ReadBannerFile(string filePath)
        {
            // Treat an unnamed or missing file as no art, so the caller falls back to the branded Hub name.
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            // Read the art, and treat a locked or unreadable file as no art instead of failing startup.
            try
            {
                return File.ReadAllLines(filePath);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
