using G4.Models;

using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace G4.Services.Domain.V4.Repositories
{
    /// <summary>
    /// Defines the runtime lifecycle of the white-label branding: read, replace, merge, and reset.
    /// </summary>
    /// <remarks>
    /// The repository owns the one resolved <see cref="BrandingSettingsModel"/> instance registered at startup and
    /// changes it in place, so every consumer that holds it, such as the Swagger document filter and the branding
    /// endpoints, publishes the new labels without a restart. A change is saved to the <c>Branding</c> section of the
    /// Hub's <c>appsettings.json</c>, rewrites <c>wwwroot/branding.json</c>, and notifies every client connected to the
    /// notifications hub. Invalid settings throw <see cref="ArgumentException"/>, which the transports map to a bad
    /// request; the live branding stays unchanged in that case.
    /// </remarks>
    public interface IBrandingRepository
    {
        #region *** Methods      ***

        /// <summary>
        /// Gets the live resolved branding.
        /// </summary>
        /// <returns>The shared resolved settings, including the placeholder map and the version.</returns>
        BrandingSettingsModel GetBranding();

        /// <summary>
        /// Gets the resolved built-in branding, which keeps every label as the G4 text.
        /// </summary>
        /// <returns>A new resolved model that nothing else holds, so the caller can change it freely.</returns>
        BrandingSettingsModel GetDefaultBranding();

        /// <summary>
        /// Merges partial changes into the saved raw settings without applying them.
        /// </summary>
        /// <param name="changes">
        /// A JSON object with the changed members, in the shape of the <c>Branding</c> section. Nested objects merge
        /// member by member, and a member set to null removes it from the saved settings, so its default applies.
        /// </param>
        /// <returns>The merged raw settings, ready for <see cref="SetBrandingAsync(BrandingSettingsModel)"/>.</returns>
        /// <exception cref="ArgumentException">The changes do not describe branding settings.</exception>
        BrandingSettingsModel NewBranding(JsonObject changes);

        /// <summary>
        /// Replaces the branding with the built-in defaults, saves them, and notifies the clients.
        /// </summary>
        /// <returns>The live resolved branding after the reset.</returns>
        /// <remarks>
        /// The saved section lists every default explicitly, so an operator sees each value the Hub uses.
        /// </remarks>
        Task<BrandingSettingsModel> ResetBrandingAsync();

        /// <summary>
        /// Replaces the branding with raw settings, saves them, applies them live, and notifies the clients.
        /// </summary>
        /// <param name="settings">The raw settings; missing values take their defaults when resolved.</param>
        /// <returns>The live resolved branding after the change.</returns>
        /// <remarks>
        /// The Swagger UI document list, the console banner, and the MCP protocol tools are built at startup and keep
        /// the previous labels until the Hub restarts; every other label follows the new branding at once. An
        /// environment variable override of a branding value wins again at the next start.
        /// </remarks>
        /// <exception cref="ArgumentException">The settings are missing, or an address is not an absolute URL.</exception>
        Task<BrandingSettingsModel> SetBrandingAsync(BrandingSettingsModel settings);

        #endregion
    }
}
