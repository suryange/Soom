using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PretendardVariable.ttf로부터 Dynamic SDF TMP 폰트 에셋을 생성하고 TMP 기본 폰트로 지정한다.
/// Dynamic 모드라 한글 11,000자를 미리 굽지 않고 런타임에 온디맨드로 렌더한다.
/// 메뉴: SOOM/Setup Pretendard Font (Dynamic + Default)
/// </summary>
public static class SetupPretendardFont
{
    private const string TtfPath = "Assets/_Project/Fonts/PretendardVariable.ttf";
    private const string OutPath = "Assets/_Project/Fonts/PretendardVariable SDF.asset";
    private const string NotoPath = "Assets/_Project/Resources/Fonts/NotoSansKR SDF.asset";

    [MenuItem("SOOM/Setup Pretendard Font (Dynamic + Default)")]
    private static void Setup()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if (font == null)
        {
            Debug.LogError($"[SetupPretendardFont] '{TtfPath}'에서 폰트를 찾지 못했습니다.");
            return;
        }

        // 기존(빈 Static) 폰트 에셋이 있으면 지우고 다시 만든다.
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutPath) != null)
            AssetDatabase.DeleteAsset(OutPath);

        // Dynamic SDF 폰트 에셋 생성 (단일 인자 오버로드 = 기본값: SDFAA, 1024 아틀라스, Dynamic).
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font);
        if (fontAsset == null)
        {
            Debug.LogError("[SetupPretendardFont] TMP_FontAsset 생성에 실패했습니다.");
            return;
        }
        fontAsset.name = "PretendardVariable SDF";

        AssetDatabase.CreateAsset(fontAsset, OutPath);

        // 머티리얼 + 아틀라스 텍스처를 서브에셋으로 저장(안 하면 참조가 유실됨).
        if (fontAsset.material != null)
        {
            fontAsset.material.name = "PretendardVariable SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }
        if (fontAsset.atlasTextures != null)
        {
            foreach (var atlas in fontAsset.atlasTextures)
            {
                if (atlas == null) continue;
                atlas.name = "PretendardVariable SDF Atlas";
                AssetDatabase.AddObjectToAsset(atlas, fontAsset);
            }
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(OutPath);

        // TMP 기본 폰트로 지정 + NotoSansKR 폴백 등록.
        TMP_Settings settings = TMP_Settings.instance;
        if (settings != null)
        {
            var so = new SerializedObject(settings);

            var defProp = so.FindProperty("m_defaultFontAsset");
            if (defProp != null) defProp.objectReferenceValue = fontAsset;

            var noto = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NotoPath);
            var fb = so.FindProperty("m_fallbackFontAssets");
            if (noto != null && fb != null)
            {
                bool has = false;
                for (int i = 0; i < fb.arraySize; i++)
                    if (fb.GetArrayElementAtIndex(i).objectReferenceValue == noto) has = true;
                if (!has)
                {
                    fb.arraySize++;
                    fb.GetArrayElementAtIndex(fb.arraySize - 1).objectReferenceValue = noto;
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[SetupPretendardFont] Pretendard 동적 폰트 생성 + TMP 기본 폰트 지정 완료. 다시 Play하면 적용됩니다.");
        }
        else
        {
            Debug.LogWarning("[SetupPretendardFont] TMP_Settings를 찾지 못해 기본 폰트를 지정하지 못했습니다. " +
                "Project Settings ▸ TextMeshPro ▸ Default Font Asset을 'PretendardVariable SDF'로 수동 지정하세요.");
        }

        Selection.activeObject = fontAsset;
    }
}
