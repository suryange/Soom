// BreathCircleSetup.cs  (Editor-only)
// One-click build for the shared circular breathing UI (BreathCircleUI) against the new
// BreathEventsSO architecture.
//
//   SOOM ▸ Build Breath Circle UI
//
// Idempotent: destroy + recreate.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

static class BreathCircleSetup
{
    // Assets/_Project/Scripts/System/BreathEventsChannel.asset
    const string EventsChannelGuid = "91e1c179be2cf2444978f568e7476427";

    [MenuItem("SOOM/Build Breath Circle UI")]
    static void Build()
    {
        var events = FindEventsChannel();
        if (events == null)
        {
            Debug.LogError("[SOOM] BreathEventsChannel.asset not found (guid " + EventsChannelGuid +
                            "). Aborting Breath Circle UI build.");
            return;
        }

        var cam = Camera.main;

        var root = GameObject.Find("SoomUI");
        if (root == null) root = new GameObject("SoomUI");

        var existing = FindChildByName(root.transform, "BreathCircle");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var canvasGo = MakeWorldCanvas("BreathCircle", root.transform, cam, new Vector2(900f, 900f));
        canvasGo.AddComponent<FaceCamera>(); // keeps the panel legible as the player moves in VR

        var group = canvasGo.AddComponent<CanvasGroup>();

        // --- outlines + bead --------------------------------------------------------
        var largeOutline = MakeImage(canvasGo.transform, "LargeOutline", new Vector2(700f, 700f),
            CircleSpriteFactory.CreateRing(Color.white, 0.06f), Color.white);
        var smallOutline = MakeImage(canvasGo.transform, "SmallOutline", new Vector2(280f, 280f),
            CircleSpriteFactory.CreateRing(Color.white, 0.1f), new Color(1f, 1f, 1f, 0.6f));
        // Bead base size = the LARGE outline, so BreathCircleUI's default scale range maps exactly:
        // smallScale 0.4 → 280 px (small outline), largeScale 1.0 → 700 px (large outline).
        var bead = MakeImage(canvasGo.transform, "Bead", new Vector2(700f, 700f),
            CircleSpriteFactory.CreateFilledCircle(Color.white), new Color(1f, 0.85f, 0.4f, 1f));
        bead.rectTransform.localScale = Vector3.one * 0.4f; // start at the small outline (breath value = 0)

        // --- top slots (left -> right) ----------------------------------------------
        const int slotCount = 3; // matches BreathManager.targetLoopCount
        var slotsParent = new GameObject("Slots", typeof(RectTransform));
        slotsParent.transform.SetParent(canvasGo.transform, false);
        var slotsRt = (RectTransform)slotsParent.transform;
        slotsRt.anchorMin = slotsRt.anchorMax = new Vector2(0.5f, 1f);
        slotsRt.anchoredPosition = new Vector2(0f, -60f);
        slotsRt.sizeDelta = new Vector2(600f, 140f);
        var layout = slotsParent.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 40f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        var slotRings = new Image[slotCount];
        var slotFills = new Image[slotCount];
        var ringSprite = CircleSpriteFactory.CreateRing(Color.white, 0.12f);
        var fillSprite = CircleSpriteFactory.CreateFilledCircle(Color.white);
        for (int i = 0; i < slotCount; i++)
        {
            var slotGo = new GameObject($"Slot{i}", typeof(RectTransform), typeof(LayoutElement));
            slotGo.transform.SetParent(slotsParent.transform, false);
            var le = slotGo.GetComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 130f;

            var ring = MakeImage(slotGo.transform, "Ring", new Vector2(130f, 130f), ringSprite, new Color(1f, 1f, 1f, 0.35f));
            var fill = MakeImage(slotGo.transform, "Fill", new Vector2(110f, 110f), fillSprite, new Color(1f, 0.85f, 0.4f, 1f));
            fill.enabled = false; // empty until a loop completes
            slotRings[i] = ring;
            slotFills[i] = fill;
        }

        var ui = canvasGo.AddComponent<BreathCircleUI>();
        Wire(ui, so =>
        {
            so.FindProperty("events").objectReferenceValue = events;
            so.FindProperty("largeCircleOutline").objectReferenceValue = largeOutline;
            so.FindProperty("smallCircleOutline").objectReferenceValue = smallOutline;
            so.FindProperty("bead").objectReferenceValue = bead;
            so.FindProperty("group").objectReferenceValue = group;

            var rings = so.FindProperty("slotRings");
            rings.arraySize = slotCount;
            for (int i = 0; i < slotCount; i++) rings.GetArrayElementAtIndex(i).objectReferenceValue = slotRings[i];

            var fills = so.FindProperty("slotFills");
            fills.arraySize = slotCount;
            for (int i = 0; i < slotCount; i++) fills.GetArrayElementAtIndex(i).objectReferenceValue = slotFills[i];
        });

        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeObject = canvasGo;
        Debug.Log("[SOOM] BreathCircle UI built under 'SoomUI' and wired to BreathEventsChannel. " +
                   "Call Show()/Hide() on its BreathCircleUI component from each content module.");
    }

    static BreathEventsSO FindEventsChannel()
    {
        string path = AssetDatabase.GUIDToAssetPath(EventsChannelGuid);
        var byGuid = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<BreathEventsSO>(path);
        if (byGuid != null) return byGuid;

        // Fallback: search by type/name in case the guid ever drifts (e.g. asset re-imported).
        var guids = AssetDatabase.FindAssets("t:BreathEventsSO BreathEventsChannel");
        foreach (var guid in guids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<BreathEventsSO>(assetPath);
            if (asset != null) return asset;
        }
        return null;
    }

    static Transform FindChildByName(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == name) return parent.GetChild(i);
        return null;
    }

    // Same 1 UI unit = 1 mm convention as the old SoomSetup world-space canvases.
    static GameObject MakeWorldCanvas(string name, Transform parent, Camera cam, Vector2 size)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one * 0.001f;
        return go;
    }

    static Image MakeImage(Transform parent, string name, Vector2 size, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.rectTransform.sizeDelta = size;
        return img;
    }

    static void Wire(Object component, System.Action<SerializedObject> set)
    {
        var so = new SerializedObject(component);
        set(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
