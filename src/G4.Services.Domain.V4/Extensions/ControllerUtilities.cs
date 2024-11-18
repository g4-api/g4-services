using System;

namespace G4.Services.Domain.V4.Extensions
{
    /// <summary>
    /// Provides utility methods for controllers in the G4™ application.
    /// </summary>
    public static class ControllerUtilities
    {
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

            // Set the console output encoding to UTF-8 to support Unicode characters.
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Output the logo to the console by joining the array elements with a newline character.
            Console.WriteLine(string.Join("\n", logo));
        }
    }
}
