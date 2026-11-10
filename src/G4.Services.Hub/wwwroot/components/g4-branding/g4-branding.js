/*
 * G4(TM) branding: G4Branding.getText('{engineTm} API') and data-branding-* attributes.
 *
 * Replaces the product labels of the Hub pages with the white-label branding the Hub resolved from the
 * Branding section of its appsettings.json. The Hub publishes the branding as a script,
 * /api/v4/g4/integration/branding.js, that sets globalThis.G4BrandingSettings; the VS Code designer sets the
 * same global itself. Without the global, the built-in G4 defaults apply, so every label stays as it was.
 *
 * Contract:
 * - Load right after the branding script and before every page script that shows a label.
 * - Text names a product through a placeholder such as {product}, {productTm}, {engine}, or {engineTm}.
 *   Only placeholders the branding knows are replaced; other braces, identifiers, and routes stay as written.
 * - Markup keeps its G4 text and adds the template in an attribute, so nothing flashes before the script runs:
 *   data-branding-text replaces the text, data-branding-title, data-branding-alt, and data-branding-aria-label
 *   replace those attributes, and data-branding-asset="logo|favicon" replaces the image or the icon address.
 * - getText(text) resolves a template; getAssetUrl(name) resolves an asset address against the site root;
 *   setSettings(settings) replaces the branding and updates the page again.
 */
(() => {
    'use strict';

    // Maps each data-branding-* attribute to the attribute it replaces with the resolved template.
    const BRANDED_ATTRIBUTES = [
        ['brandingAlt', 'alt'],
        ['brandingAriaLabel', 'aria-label'],
        ['brandingTitle', 'title']
    ];

    // The placeholder values the Hub publishes when it has no Branding section, so the page keeps the G4 text
    // even when the branding script is not available.
    const DEFAULT_SETTINGS = {
        assets: { activityBarIcon: '', favicon: '/images/favicon.ico', logo: '/images/avatar.png', testWrightIcon: '' },
        tokens: {
            engine: 'G4 Engine',
            engineClient: 'G4 Engine Client',
            engineClientTm: 'G4™ Engine Client',
            engineTm: 'G4™ Engine',
            hub: 'G4 Hub',
            hubTm: 'G4™ Hub',
            product: 'G4',
            productTm: 'G4™',
            studio: 'G4 Studio',
            studioTm: 'G4™ Studio',
            testWright: 'G4 Test Wright',
            testWrightTm: 'G4™ Test Wright',
            tm: '™'
        }
    };

    // Matches one placeholder such as {engineTm}: a bounded run of word characters between braces, so the match
    // runs in linear time and never spans text outside one pair of braces.
    const TOKEN_PATTERN = /\{(\w{1,64})\}/g;

    // The site root, two folders above this script, so an asset path such as /images/logo.png resolves both on the
    // Hub and inside the VS Code designer, which serves the same files from another root.
    const siteRootUrl = new URL('../../', document.currentScript?.src ?? document.baseURI);

    // The branding set by setSettings; null until then, so the global published by the branding script is read
    // when a label is resolved, after the VS Code shim had a chance to set it.
    let explicitSettings = null;

    // Publish the branding API.
    globalThis.G4Branding = {
        /**
         * Resolves the address of a branded asset against the site root.
         *
         * @param {'favicon'|'logo'} assetName - The asset to resolve.
         * @returns {string} The absolute address of the asset; absolute URLs in the branding stay as written.
         */
        getAssetUrl(assetName) {
            // Read the configured address, falling back to the G4 asset when the branding names none.
            const assetPath = getSettings().assets?.[assetName] ?? DEFAULT_SETTINGS.assets[assetName];

            // Resolve a root path against the site root rather than the server root, so the VS Code copy works.
            const relativePath = assetPath.startsWith('/') ? assetPath.slice(1) : assetPath;

            return new URL(relativePath, siteRootUrl).href;
        },

        /**
         * Returns the branding in use: the one set by setSettings, the one the branding script published, or the
         * built-in G4 defaults.
         *
         * @returns {object} The branding settings.
         */
        getSettings() {
            return getSettings();
        },

        /**
         * Replaces every known placeholder in the text with its branded value.
         *
         * @param {string} text - The text to resolve.
         * @returns {string} The resolved text; text that is not a string is returned unchanged.
         */
        getText(text) {
            return getText(text);
        },

        /**
         * Replaces the branding and updates every branded element of the page.
         *
         * @param {object} settings - The resolved branding, as the Hub publishes it.
         */
        setSettings(settings) {
            // Keep the new branding ahead of the published global, then brand the page again with it.
            explicitSettings = settings;
            updateDocument();
        }
    };

    // Brand the page once its markup is parsed; a page already parsed is branded at once.
    const isParsing = document.readyState === 'loading';

    if (isParsing) {
        document.addEventListener('DOMContentLoaded', updateDocument, { once: true });

        return;
    }

    updateDocument();

    /**
     * Converts a dataset name, such as brandingAriaLabel, into its attribute form, branding-aria-label.
     *
     * @param {string} datasetName - The camel-cased dataset name.
     * @returns {string} The kebab-cased attribute name without the data- prefix.
     */
    function convertToKebabCase(datasetName) {
        return datasetName.replaceAll(/[A-Z]/g, (letter) => `-${letter.toLowerCase()}`);
    }
    /**
     * Returns the branding in use, falling back to the published global and then to the G4 defaults.
     *
     * @returns {object} The branding settings.
     */
    function getSettings() {
        return explicitSettings ?? globalThis.G4BrandingSettings ?? DEFAULT_SETTINGS;
    }

    /**
     * Replaces every known placeholder in the text with its branded value.
     *
     * @param {string} text - The text to resolve.
     * @returns {string} The resolved text; text that is not a string is returned unchanged.
     *
     * @remarks
     * A placeholder missing from the branding falls back to its G4 default, and an unknown placeholder is kept
     * as written, so text that uses braces for another purpose never changes.
     */
    function getText(text) {
        // Return a value that is not text unchanged, so callers can pass optional values without a guard.
        if (typeof text !== 'string') {
            return text;
        }

        // Replace each placeholder with the branded value, then the G4 default, and keep unknown braces as written.
        const tokens = getSettings().tokens ?? {};

        return text.replaceAll(TOKEN_PATTERN, (placeholder, tokenName) => {
            return tokens[tokenName] ?? DEFAULT_SETTINGS.tokens[tokenName] ?? placeholder;
        });
    }

    /**
     * Applies the branding to every element marked with a data-branding-* attribute.
     *
     * @remarks
     * Runs again after setSettings, so a branding that arrives late, as in VS Code, replaces the earlier labels.
     */
    function updateDocument() {
        // Replace the text of each element that carries a text template, including the page title.
        for (const element of document.querySelectorAll('[data-branding-text]')) {
            element.textContent = getText(element.dataset.brandingText);
        }

        // Replace each templated attribute, such as a tooltip or an image description.
        for (const [datasetName, attributeName] of BRANDED_ATTRIBUTES) {
            const attributeSelector = `[data-${convertToKebabCase(datasetName)}]`;

            for (const element of document.querySelectorAll(attributeSelector)) {
                element.setAttribute(attributeName, getText(element.dataset[datasetName]));
            }
        }

        // Point each branded image and icon at the configured asset.
        for (const element of document.querySelectorAll('[data-branding-asset]')) {
            const assetUrl = globalThis.G4Branding.getAssetUrl(element.dataset.brandingAsset);
            const addressAttribute = element.tagName === 'LINK' ? 'href' : 'src';

            element.setAttribute(addressAttribute, assetUrl);
        }
    }
})();
