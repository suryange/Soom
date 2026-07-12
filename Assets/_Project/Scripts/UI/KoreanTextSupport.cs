using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Registers a Korean-capable TMP fallback font before scene text is generated.
/// </summary>
public static class KoreanTextSupport
{
    private const string KoreanFontResourcePath = "Fonts/NotoSansKR-VariableFont_wght";
    private const string KoreanFontAssetName = "NotoSansKR Runtime SDF";

    private static TMP_FontAsset koreanFallbackFontAsset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterFallbackFont()
    {
        if (koreanFallbackFontAsset == null)
        {
            Font sourceFont = Resources.Load<Font>(KoreanFontResourcePath);
            if (sourceFont == null)
            {
                Debug.LogWarning($"Korean TMP fallback font was not found at Resources/{KoreanFontResourcePath}.");
                return;
            }

            koreanFallbackFontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true);

            if (koreanFallbackFontAsset == null)
            {
                Debug.LogWarning("Failed to create Korean TMP fallback font asset.");
                return;
            }

            koreanFallbackFontAsset.name = KoreanFontAssetName;
        }

        List<TMP_FontAsset> globalFallbackFonts = TMP_Settings.fallbackFontAssets;
        if (globalFallbackFonts == null)
        {
            globalFallbackFonts = new List<TMP_FontAsset>();
            TMP_Settings.fallbackFontAssets = globalFallbackFonts;
        }

        AddFallback(globalFallbackFonts, koreanFallbackFontAsset);

        TMP_FontAsset defaultFontAsset = TMP_Settings.defaultFontAsset;
        if (defaultFontAsset != null)
        {
            if (defaultFontAsset.fallbackFontAssetTable == null)
                defaultFontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();

            AddFallback(defaultFontAsset.fallbackFontAssetTable, koreanFallbackFontAsset);
        }
    }

    private static void AddFallback(List<TMP_FontAsset> fallbackFonts, TMP_FontAsset fontAsset)
    {
        if (fallbackFonts == null || fontAsset == null)
            return;

        if (!fallbackFonts.Contains(fontAsset))
            fallbackFonts.Add(fontAsset);
    }
}
