using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// visionOS 스타일 프로스티드 글래스 UI를 한 줄로 찍어내는 공용 킷 (런타임/에디터 공용).
/// 테마 색 · 라운드 스프라이트 · 프로스티드 글래스 머티리얼 · 절차적 아이콘을 한곳에서 관리한다.
///
/// 사용 예:
///   var canvas = GlassUIKit.WorldCanvas("UI", parent, worldPos, new Vector2(480,240), 0.0035f, interactive:true);
///   var card   = GlassUIKit.Card("Panel", canvas.transform, Vector2.zero, new Vector2(480,240));
///   GlassUIKit.IconImage("Ic", card, new Vector2(0,70), 56, GlassUIKit.IconBreath, GlassUIKit.Accent);
///   GlassUIKit.Label("Title", card, new Vector2(0,10), new Vector2(420,60), 30, TextAlignmentOptions.Center, bold:true);
///   var btn = GlassUIKit.IconButton("Go", card, new Vector2(0,-80), new Vector2(280,74), GlassUIKit.IconChevron, "호흡 시작", out _);
///
/// 아이콘은 외부 에셋 없이 절차적으로 그린다(발자국/동심원 호흡/하트/체크/셰브런).
/// </summary>
public static class GlassUIKit
{
    // ============================================================
    // 테마 (프로젝트 UI 단일 소스)
    // ============================================================
    public static readonly Color PanelFill   = new Color(0.88f, 0.90f, 0.94f, 0.60f);
    public static readonly Color PanelBorder = new Color(1f, 1f, 1f, 0.55f);
    public static readonly Color Accent      = new Color(0.18f, 0.48f, 1f, 1f);     // 시스템 블루
    public static readonly Color TextMain    = new Color(0.08f, 0.13f, 0.22f, 1f);  // 다크(라이트 글래스 위)
    public static readonly Color TextMuted   = new Color(0.26f, 0.31f, 0.41f, 1f);
    public static readonly Color ButtonFill  = new Color(0.18f, 0.48f, 1f, 0.95f);
    public static readonly Color ButtonBorder = new Color(1f, 1f, 1f, 0.5f);
    public static readonly Color ButtonText  = Color.white;
    public static readonly Color Amber       = new Color(0.91f, 0.54f, 0.17f, 1f);  // 동물/경계
    public static readonly Color Rose        = new Color(0.90f, 0.41f, 0.54f, 1f);  // 불안
    public static readonly Color Mint        = new Color(0.15f, 0.72f, 0.57f, 1f);  // 동료/성공

    public static readonly Color GlassTint   = new Color(0.92f, 0.94f, 0.98f, 0.42f);
    public const float GlassBlurRadius = 0.014f;

    private const string GlassShaderName = "SOOM/UIFrostedGlass";
    private const string GlassMaterialPath = "Assets/_Project/Arts/Mat_UIFrostedGlass.mat";

    // ============================================================
    // 공유 리소스 (라운드 스프라이트 / 글래스 머티리얼)
    // ============================================================
    private static Sprite _rounded;
    public static Sprite RoundedSprite
    {
        get
        {
            if (_rounded != null) return _rounded;
#if UNITY_EDITOR
            _rounded = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (_rounded != null) return _rounded;
#endif
            _rounded = BuildRoundedSprite(96, 30);
            return _rounded;
        }
    }

    private static Material _glass;
    private static bool _glassResolved;
    public static Material GlassMaterial
    {
        get
        {
            if (_glassResolved) return _glass;
            _glassResolved = true;
#if UNITY_EDITOR
            _glass = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
            if (_glass != null) return _glass;
#endif
            Shader sh = Shader.Find(GlassShaderName);
            if (sh != null)
            {
                _glass = new Material(sh) { name = "Mat_UIFrostedGlass_Runtime" };
                if (_glass.HasProperty("_TintColor")) _glass.SetColor("_TintColor", GlassTint);
                if (_glass.HasProperty("_BlurRadius")) _glass.SetFloat("_BlurRadius", GlassBlurRadius);
            }
            return _glass;
        }
    }

    // ============================================================
    // 한 줄 UI 헬퍼
    // ============================================================

    public static RectTransform NewRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static Image RoundedImage(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var rt = NewRect(name, parent, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.sprite = RoundedSprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1.1f;
        return img;
    }

    public static Image IconImage(string name, Transform parent, Vector2 pos, float size, Sprite sprite, Color color)
    {
        var rt = NewRect(name, parent, pos, new Vector2(size, size));
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        img.preserveAspect = true;
        return img;
    }

    public static TMP_Text Label(string name, Transform parent, Vector2 pos, Vector2 size,
        float fontSize, TextAlignmentOptions align, Color? color = null, bool bold = false)
    {
        var rt = NewRect(name, parent, pos, size);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.alignment = align;
        t.fontSize = fontSize;
        t.color = color ?? TextMain;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        if (bold) t.fontStyle = FontStyles.Bold;
        return t;
    }

    /// <summary>얇은 화이트 엣지 + 프로스티드 글래스 채움의 둥근 카드. 콘텐츠를 얹을 안쪽(Fill)을 반환.</summary>
    public static RectTransform Card(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var border = RoundedImage(name, parent, pos, size, PanelBorder);
        var fill = RoundedImage("Fill", border.transform, Vector2.zero, new Vector2(size.x - 4f, size.y - 4f), PanelFill);
        Material mat = GlassMaterial;
        if (mat != null)
        {
            fill.material = mat;
            fill.color = Color.white; // 셰이더가 _TintColor로 유리색을 결정 → 이미지 색은 흰색(알파 1)
        }
        return fill.rectTransform;
    }

    public static Canvas WorldCanvas(string name, Transform parent, Vector3 worldPos, Vector2 size, float worldScale, bool interactive = false)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        float ps = parent != null ? parent.lossyScale.x : 1f;
        if (Mathf.Approximately(ps, 0f)) ps = 1f;
        go.transform.localScale = Vector3.one * (worldScale / ps);
        go.transform.position = worldPos;
        go.transform.localRotation = Quaternion.identity;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        go.GetComponent<RectTransform>().sizeDelta = size;

        if (interactive)
        {
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
            if (Camera.main != null) canvas.worldCamera = Camera.main;
        }
        go.AddComponent<FaceCamera>();
        return canvas;
    }

    /// <summary>아이콘(옵션) + 라벨의 둥근 알약 버튼. label로 라벨 TMP를 돌려준다.</summary>
    public static Button IconButton(string name, Transform parent, Vector2 pos, Vector2 size, Sprite icon, string text, out TMP_Text label)
    {
        var border = RoundedImage(name, parent, pos, size, ButtonBorder);
        var fill = RoundedImage("Fill", border.transform, Vector2.zero, new Vector2(size.x - 6f, size.y - 6f), ButtonFill);

        var button = border.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        var cb = button.colors;
        cb.normalColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        cb.highlightedColor = Color.white;
        cb.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        cb.selectedColor = Color.white;
        cb.fadeDuration = 0.08f;
        button.colors = cb;

        float iconSize = size.y * 0.44f;
        float pad = 20f;
        if (icon != null)
            IconImage("Icon", fill.transform, new Vector2(-size.x * 0.5f + pad + iconSize * 0.5f, 0f), iconSize, icon, ButtonText);

        float labelShift = icon != null ? iconSize * 0.5f : 0f;
        label = Label("Label", fill.transform, new Vector2(labelShift, 0f),
            new Vector2(size.x - pad * 2f - (icon != null ? iconSize : 0f), size.y),
            Mathf.Min(size.y * 0.34f, 26f), TextAlignmentOptions.Center, ButtonText, bold: true);
        label.text = text;
        return button;
    }

    // ============================================================
    // 절차적 아이콘 (외부 에셋 없이 SDF로 그림)
    // ============================================================
    private static Sprite _icBreath, _icPaw, _icHeart, _icCheck, _icChevron;
    public static Sprite IconBreath  { get { if (_icBreath == null)  _icBreath  = BuildIcon(DrawBreath);  return _icBreath;  } }
    public static Sprite IconPaw     { get { if (_icPaw == null)     _icPaw     = BuildIcon(DrawPaw);     return _icPaw;     } }
    public static Sprite IconHeart   { get { if (_icHeart == null)   _icHeart   = BuildIcon(DrawHeart);   return _icHeart;   } }
    public static Sprite IconCheck   { get { if (_icCheck == null)   _icCheck   = BuildIcon(DrawCheck);   return _icCheck;   } }
    public static Sprite IconChevron { get { if (_icChevron == null) _icChevron = BuildIcon(DrawChevron); return _icChevron; } }

    private const int IconRes = 128;

    private static Sprite BuildIcon(Func<float, float, float> coverage)
    {
        var tex = new Texture2D(IconRes, IconRes, TextureFormat.RGBA32, false)
        { name = "GlassIcon", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[IconRes * IconRes];
        for (int y = 0; y < IconRes; y++)
            for (int x = 0; x < IconRes; x++)
            {
                float u = (x + 0.5f) / IconRes;
                float v = (y + 0.5f) / IconRes;
                float a = Mathf.Clamp01(coverage(u, v));
                px[y * IconRes + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, IconRes, IconRes), new Vector2(0.5f, 0.5f), IconRes);
    }

    // -- SDF 프리미티브 (음수 = 내부) --
    private static float Cov(float sd)
    {
        float aa = 1.6f / IconRes;
        return Mathf.Clamp01(0.5f - sd / aa);
    }
    private static float SdCircle(float ux, float uy, float cx, float cy, float r)
        => Mathf.Sqrt((ux - cx) * (ux - cx) + (uy - cy) * (uy - cy)) - r;
    private static float SdRing(float sdCircle, float half) => Mathf.Abs(sdCircle) - half;
    private static float SdDiamond(float ux, float uy, float cx, float cy, float r)
        => (Mathf.Abs(ux - cx) + Mathf.Abs(uy - cy)) - r;
    private static float SdSeg(float px, float py, float ax, float ay, float bx, float by, float half)
    {
        float vx = bx - ax, vy = by - ay, wx = px - ax, wy = py - ay;
        float t = Mathf.Clamp01((wx * vx + wy * vy) / (vx * vx + vy * vy));
        float dx = px - (ax + t * vx), dy = py - (ay + t * vy);
        return Mathf.Sqrt(dx * dx + dy * dy) - half;
    }
    private static float U(float a, float b) => Mathf.Min(a, b);

    // -- 아이콘 정의 (u,v ∈ 0..1, v는 위쪽) --
    private static float DrawBreath(float u, float v) // 동심원 3겹 = 호흡/집중
    {
        float r1 = SdRing(SdCircle(u, v, 0.5f, 0.5f, 0.15f), 0.020f);
        float r2 = SdRing(SdCircle(u, v, 0.5f, 0.5f, 0.28f), 0.020f);
        float r3 = SdRing(SdCircle(u, v, 0.5f, 0.5f, 0.41f), 0.020f);
        return Cov(U(U(r1, r2), r3));
    }
    private static float DrawPaw(float u, float v) // 발자국 = 동물/여우
    {
        float pad = SdCircle(u, v, 0.5f, 0.40f, 0.17f);
        float t1 = SdCircle(u, v, 0.29f, 0.63f, 0.085f);
        float t2 = SdCircle(u, v, 0.435f, 0.71f, 0.095f);
        float t3 = SdCircle(u, v, 0.565f, 0.71f, 0.095f);
        float t4 = SdCircle(u, v, 0.71f, 0.63f, 0.085f);
        return Cov(U(U(U(U(pad, t1), t2), t3), t4));
    }
    private static float DrawHeart(float u, float v) // 하트(원 2 + 다이아몬드) = 동료
    {
        float l = SdCircle(u, v, 0.35f, 0.605f, 0.185f);
        float r = SdCircle(u, v, 0.65f, 0.605f, 0.185f);
        float d = SdDiamond(u, v, 0.5f, 0.47f, 0.30f); // 아래 꼭짓점이 하트 끝(≈0.17)
        return Cov(U(U(l, r), d));
    }
    private static float DrawCheck(float u, float v) // 원 + 체크 = 제거/완료
    {
        float ring = SdRing(SdCircle(u, v, 0.5f, 0.5f, 0.40f), 0.030f);
        float s1 = SdSeg(u, v, 0.33f, 0.50f, 0.45f, 0.37f, 0.045f);
        float s2 = SdSeg(u, v, 0.45f, 0.37f, 0.69f, 0.63f, 0.045f);
        return Cov(U(U(ring, s1), s2));
    }
    private static float DrawChevron(float u, float v) // > = 진행/시작
    {
        float s1 = SdSeg(u, v, 0.40f, 0.72f, 0.64f, 0.50f, 0.050f);
        float s2 = SdSeg(u, v, 0.64f, 0.50f, 0.40f, 0.28f, 0.050f);
        return Cov(U(s1, s2));
    }

    // ============================================================
    // 절차적 라운드 사각형(런타임 폴백용 9-slice 스프라이트)
    // ============================================================
    private static Sprite BuildRoundedSprite(int size, int radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { name = "GlassRounded", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - (half - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - (half - radius), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - dist + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        var border = new Vector4(radius, radius, radius, radius);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }
}
