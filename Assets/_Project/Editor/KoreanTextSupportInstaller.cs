using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

[InitializeOnLoad]
internal static class KoreanTextSupportInstaller
{
    private const string SourceFontPath = "Assets/_Project/Resources/Fonts/NotoSansKR-VariableFont_wght.ttf";
    private const string FontAssetPath = "Assets/_Project/Resources/Fonts/NotoSansKR SDF.asset";
    private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    static KoreanTextSupportInstaller()
    {
        EditorApplication.delayCall += Install;
    }

    [MenuItem("Tools/SOOM/Install Korean TMP Font")]
    private static void Install()
    {
        AssetDatabase.ImportAsset(SourceFontPath, ImportAssetOptions.ForceUpdate);

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
            return;

        TMP_FontAsset koreanFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (koreanFontAsset == null)
        {
            koreanFontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true);

            if (koreanFontAsset == null)
            {
                Debug.LogWarning("Failed to create NotoSansKR TMP font asset.");
                return;
            }

            koreanFontAsset.name = "NotoSansKR SDF";
            AssetDatabase.CreateAsset(koreanFontAsset, FontAssetPath);

            if (koreanFontAsset.atlasTextures != null &&
                koreanFontAsset.atlasTextures.Length > 0 &&
                koreanFontAsset.atlasTextures[0] != null)
            {
                AssetDatabase.AddObjectToAsset(koreanFontAsset.atlasTextures[0], koreanFontAsset);
            }

            if (koreanFontAsset.material != null)
                AssetDatabase.AddObjectToAsset(koreanFontAsset.material, koreanFontAsset);

            AssetDatabase.SaveAssets();
        }

        RegisterInTmpSettings(koreanFontAsset);
    }

    private static void RegisterInTmpSettings(TMP_FontAsset koreanFontAsset)
    {
        TMP_Settings settings = TMP_Settings.GetSettings();
        if (settings == null)
            settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);

        if (settings == null)
            return;

        SerializedObject serializedSettings = new SerializedObject(settings);
        SerializedProperty fallbackFonts = serializedSettings.FindProperty("m_fallbackFontAssets");
        AddObjectToArray(fallbackFonts, koreanFontAsset);

        SerializedProperty useModernHangulLineBreakingRules =
            serializedSettings.FindProperty("m_UseModernHangulLineBreakingRules");
        if (useModernHangulLineBreakingRules != null)
            useModernHangulLineBreakingRules.boolValue = true;

        serializedSettings.ApplyModifiedProperties();
        EditorUtility.SetDirty(settings);

        TMP_FontAsset defaultFontAsset = TMP_Settings.defaultFontAsset;
        if (defaultFontAsset != null)
        {
            if (defaultFontAsset.fallbackFontAssetTable == null)
                defaultFontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();

            if (!defaultFontAsset.fallbackFontAssetTable.Contains(koreanFontAsset))
            {
                defaultFontAsset.fallbackFontAssetTable.Add(koreanFontAsset);
                EditorUtility.SetDirty(defaultFontAsset);
            }
        }

        AssetDatabase.SaveAssets();
    }

    private static void AddObjectToArray(SerializedProperty arrayProperty, UnityEngine.Object value)
    {
        if (arrayProperty == null || value == null)
            return;

        for (int i = 0; i < arrayProperty.arraySize; i++)
        {
            if (arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue == value)
                return;
        }

        arrayProperty.InsertArrayElementAtIndex(arrayProperty.arraySize);
        arrayProperty.GetArrayElementAtIndex(arrayProperty.arraySize - 1).objectReferenceValue = value;
    }
}
