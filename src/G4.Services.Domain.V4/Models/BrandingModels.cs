using System.Collections.Generic;

namespace G4.Models
{
    /// <summary>
    /// Represents the white-label settings that replace the user-facing product labels of the Hub, the G4 VS Code
    /// extension, and the Test Wright extension.
    /// </summary>
    /// <remarks>
    /// The Hub reads the settings from the <c>Branding</c> configuration section, fills every missing value from the
    /// product name, and publishes the resolved settings through the branding endpoints and the generated
    /// <c>wwwroot/branding.json</c> file. Only labels and visible text follow these settings; routes, identifiers,
    /// and library names never change. Every value defaults to the current G4 text, so a deployment without the
    /// section looks the same as before.
    /// </remarks>
    public class BrandingSettingsModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets the image paths and the mark text that represent the product.
        /// </summary>
        public BrandingAssetsModel Assets { get; set; } = new();

        /// <summary>
        /// Gets or sets how the Hub writes its console banner at startup.
        /// </summary>
        public BrandingBannerModel ConsoleBanner { get; set; } = new();

        /// <summary>
        /// Gets or sets the product names shown to users.
        /// </summary>
        public BrandingNamesModel Names { get; set; } = new();

        /// <summary>
        /// Gets or sets the base product name that replaces "G4" in labels and visible text.
        /// </summary>
        /// <remarks>
        /// Every name left empty in <see cref="Names"/> is derived from this value, for example "G4 Hub".
        /// </remarks>
        public string ProductName { get; set; }

        /// <summary>
        /// Gets or sets the project home page shown in the console banner.
        /// </summary>
        public string ProjectUrl { get; set; }

        /// <summary>
        /// Gets or sets the support contact published in the API documentation.
        /// </summary>
        public BrandingSupportModel Support { get; set; } = new();

        /// <summary>
        /// Gets or sets the tagline shown in the console banner.
        /// </summary>
        public string Tagline { get; set; }

        /// <summary>
        /// Gets or sets the terms of service address published in the API documentation.
        /// </summary>
        public string TermsUrl { get; set; }

        /// <summary>
        /// Gets or sets the placeholder values, keyed by placeholder name without braces.
        /// </summary>
        /// <remarks>
        /// The Hub fills this map when it resolves the settings, so every client replaces a placeholder such as
        /// <c>{engineTm}</c> with one lookup instead of deriving the names again. Values configured here are
        /// replaced by the resolved values.
        /// </remarks>
        public Dictionary<string, string> Tokens { get; set; } = [];

        /// <summary>
        /// Gets or sets the trademark symbol placed after the product name in marked names, for example "G4™ Engine".
        /// </summary>
        /// <remarks>
        /// An empty value removes the symbol from every marked name.
        /// </remarks>
        public string TrademarkSymbol { get; set; }

        #endregion
    }

    /// <summary>
    /// Represents the images and the short mark text that represent the product in the user interface.
    /// </summary>
    public class BrandingAssetsModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets the address of the favicon shown in browser tabs.
        /// </summary>
        /// <remarks>
        /// The value is either a path served by the Hub, such as a file under <c>wwwroot</c>, or an absolute URL.
        /// </remarks>
        public string Favicon { get; set; }

        /// <summary>
        /// Gets or sets the address of the logo shown in page headers.
        /// </summary>
        /// <remarks>
        /// The value is either a path served by the Hub, such as a file under <c>wwwroot</c>, or an absolute URL.
        /// </remarks>
        public string Logo { get; set; }

        /// <summary>
        /// Gets or sets the short text drawn in badges and icons, for example "G4".
        /// </summary>
        /// <remarks>
        /// Defaults to the product name when empty.
        /// </remarks>
        public string MarkText { get; set; }

        #endregion
    }

    /// <summary>
    /// Represents how the Hub writes its console banner at startup.
    /// </summary>
    /// <remarks>
    /// The credit line that names the G4 engine is always written, whichever mode is selected, because the license
    /// requires the credit.
    /// </remarks>
    public class BrandingBannerModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets the path of a text file that holds custom banner art, used by the File mode.
        /// </summary>
        /// <remarks>
        /// A relative path is resolved against the Hub's working directory. A missing or unreadable file falls back
        /// to the Text mode, so a bad path never stops the Hub from starting.
        /// </remarks>
        public string File { get; set; }

        /// <summary>
        /// Gets or sets the banner mode, one of the <see cref="BrandingBannerModes"/> values.
        /// </summary>
        /// <remarks>
        /// An empty or unknown value selects the Default mode.
        /// </remarks>
        public string Mode { get; set; }

        #endregion
    }

    /// <summary>
    /// Defines the console banner modes accepted by <see cref="BrandingBannerModel.Mode"/>.
    /// </summary>
    public static class BrandingBannerModes
    {
        #region *** Constants    ***

        /// <summary>
        /// Writes the built-in G4 banner art.
        /// </summary>
        public const string Default = "Default";

        /// <summary>
        /// Writes the banner art read from <see cref="BrandingBannerModel.File"/>.
        /// </summary>
        public const string File = "File";

        /// <summary>
        /// Writes the Hub name and the tagline as plain text instead of banner art.
        /// </summary>
        public const string Text = "Text";

        #endregion
    }

    /// <summary>
    /// Represents the product names shown to users.
    /// </summary>
    /// <remarks>
    /// Each name left empty is derived from <see cref="BrandingSettingsModel.ProductName"/>, so setting only the
    /// product name renames every product at once.
    /// </remarks>
    public class BrandingNamesModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets the engine name, for example "G4 Engine".
        /// </summary>
        public string Engine { get; set; }

        /// <summary>
        /// Gets or sets the name of the VS Code extension that hosts the designer, for example "G4 Engine Client".
        /// </summary>
        public string EngineClient { get; set; }

        /// <summary>
        /// Gets or sets the Hub name, for example "G4 Hub".
        /// </summary>
        public string Hub { get; set; }

        /// <summary>
        /// Gets or sets the studio name, for example "G4 Studio".
        /// </summary>
        public string Studio { get; set; }

        /// <summary>
        /// Gets or sets the test workbench name, for example "G4 Test Wright".
        /// </summary>
        public string TestWright { get; set; }

        #endregion
    }

    /// <summary>
    /// Represents the support contact published in the API documentation.
    /// </summary>
    public class BrandingSupportModel
    {
        #region *** Properties   ***

        /// <summary>
        /// Gets or sets the support email address.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Gets or sets the support contact name, for example "G4 Support".
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the support or documentation address.
        /// </summary>
        public string Url { get; set; }

        #endregion
    }
}
