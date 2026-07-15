using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene 03의 단서 위치에서 다음 콘텐츠 지점(Fox_location)까지 Terrain을 따라
/// 길잡이 등불 Waypoint를 배치하고 프리팹 이동값을 튜닝한다.
/// </summary>
internal static class Scene03GuidingWaypointSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Scene_03_InGame_Outside.unity";
    private const string PrefabPath = "Assets/_Project/Prefabs/GuidingLight.prefab";
    private const string MaterialPath = "Assets/_Project/Prefabs/GuidingLightGlow.mat";
    private const string WaypointRootName = "Scene03_GuidingWaypoints";
    private const string PrimaryDestinationName = "Fox_location";
    private const string FallbackDestinationName = "Fox_Encounter";

    private const float TargetSpacing = 12f;
    private const float StartForwardOffset = 3f;
    private const float DestinationStopDistance = 2.5f;
    private const float HoverHeight = 1.6f;

    [MenuItem("SOOM/Scene 03/Setup Guiding Waypoint Route")]
    public static void SetupFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            Debug.LogError($"[Scene03GuidingWaypointSetup] {ScenePath} 씬을 연 상태에서 실행해주세요.");
            return;
        }

        Setup(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void ApplyToScene03()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Setup(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Scene03GuidingWaypointSetup] Scene 03 저장 완료.");
    }

    private static void Setup(Scene scene)
    {
        HologramMessage hologram = UnityEngine.Object.FindFirstObjectByType<HologramMessage>(
            FindObjectsInactive.Include);
        if (hologram == null)
            throw new MissingReferenceException("Scene 03에서 HologramMessage를 찾지 못했습니다.");
        if (hologram.spawnPoint == null)
            throw new MissingReferenceException("HologramMessage.spawnPoint가 연결되어 있지 않습니다.");

        Transform destination = FindDestination(scene);
        if (destination == null)
        {
            throw new MissingReferenceException(
                $"Scene 03에서 '{PrimaryDestinationName}' 또는 '{FallbackDestinationName}' 목적지를 찾지 못했습니다.");
        }

        Terrain terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>(FindObjectsInactive.Include);
        if (terrain == null)
            Debug.LogWarning("[Scene03GuidingWaypointSetup] Terrain을 찾지 못해 시작/목적지 높이를 보간합니다.");

        Transform root = FindOrCreateRoot(scene);
        Vector3 start = hologram.spawnPoint.position;
        Vector3 destinationPosition = destination.position;
        Vector3 horizontalDirection = destinationPosition - start;
        horizontalDirection.y = 0f;
        float fullDistance = horizontalDirection.magnitude;
        if (fullDistance <= StartForwardOffset + DestinationStopDistance + 1f)
            throw new InvalidOperationException("단서와 목적지 사이 거리가 너무 짧아 안내 경로를 만들 수 없습니다.");

        horizontalDirection.Normalize();
        Vector3 routeStart = start + horizontalDirection * StartForwardOffset;
        Vector3 routeEnd = destinationPosition - horizontalDirection * DestinationStopDistance;
        float routeDistance = Vector2.Distance(
            new Vector2(routeStart.x, routeStart.z), new Vector2(routeEnd.x, routeEnd.z));
        int waypointCount = Mathf.Clamp(Mathf.CeilToInt(routeDistance / TargetSpacing) + 1, 6, 16);

        Transform[] waypoints = ResizeWaypoints(root, waypointCount);
        for (int i = 0; i < waypoints.Length; i++)
        {
            float t = waypoints.Length == 1 ? 1f : i / (float)(waypoints.Length - 1);
            Vector3 position = Vector3.Lerp(routeStart, routeEnd, t);
            position.y = ResolveRouteHeight(position, start, destinationPosition, terrain, t);
            waypoints[i].position = position;
            waypoints[i].rotation = Quaternion.LookRotation(horizontalDirection, Vector3.up);
            waypoints[i].name = $"Waypoint_{i + 1:00}";
            EditorUtility.SetDirty(waypoints[i]);
        }

        hologram.missionWaypoints = waypoints;
        EditorUtility.SetDirty(hologram);
        EditorUtility.SetDirty(root);
        TuneGuidingLightPrefab();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root.gameObject;

        Debug.Log(
            $"[Scene03GuidingWaypointSetup] 완료: '{hologram.name}'에서 '{destination.name}'까지 " +
            $"{routeDistance:F1}m 경로에 Waypoint {waypointCount}개를 배치했습니다. " +
            "등불 speed=2.2, tolerance=0.35, 대기/재출발 거리=7/4.5m.");
    }

    private static Transform FindDestination(Scene scene)
    {
        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        Transform primary = transforms.FirstOrDefault(item =>
            item.gameObject.scene == scene && item.name == PrimaryDestinationName);
        if (primary != null) return primary;

        return transforms.FirstOrDefault(item =>
            item.gameObject.scene == scene && item.name == FallbackDestinationName);
    }

    private static Transform FindOrCreateRoot(Scene scene)
    {
        Transform root = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(item => item.gameObject.scene == scene && item.name == WaypointRootName);

        if (root == null)
        {
            GameObject rootObject = new GameObject(WaypointRootName);
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Scene 03 Guiding Waypoints");
            root = rootObject.transform;
        }

        Undo.RecordObject(root, "Configure Scene 03 Guiding Waypoint Root");
        root.SetParent(null);
        root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.localScale = Vector3.one;
        return root;
    }

    private static Transform[] ResizeWaypoints(Transform root, int count)
    {
        Undo.RecordObject(root, "Resize Scene 03 Guiding Waypoints");

        while (root.childCount > count)
            Undo.DestroyObjectImmediate(root.GetChild(root.childCount - 1).gameObject);

        while (root.childCount < count)
        {
            GameObject waypoint = new GameObject($"Waypoint_{root.childCount + 1:00}");
            Undo.RegisterCreatedObjectUndo(waypoint, "Create Guiding Waypoint");
            waypoint.transform.SetParent(root, false);
        }

        Transform[] result = new Transform[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = root.GetChild(i);
            Undo.RecordObject(result[i], "Place Guiding Waypoint");
        }
        return result;
    }

    private static float ResolveRouteHeight(
        Vector3 worldPosition, Vector3 start, Vector3 destination, Terrain terrain, float t)
    {
        if (terrain != null && terrain.terrainData != null)
        {
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            bool inside = worldPosition.x >= terrainPosition.x &&
                          worldPosition.x <= terrainPosition.x + terrainSize.x &&
                          worldPosition.z >= terrainPosition.z &&
                          worldPosition.z <= terrainPosition.z + terrainSize.z;
            if (inside)
                return terrain.SampleHeight(worldPosition) + terrainPosition.y + HoverHeight;
        }

        return Mathf.Lerp(start.y, destination.y + HoverHeight, t);
    }

    private static void TuneGuidingLightPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            GuidingLightController controller = prefabRoot.GetComponent<GuidingLightController>();
            if (controller == null)
                controller = prefabRoot.AddComponent<GuidingLightController>();
            if (controller == null)
                throw new MissingComponentException($"{PrefabPath}에 GuidingLightController가 없습니다.");

            controller.speed = 2.2f;
            controller.waypointTolerance = 0.35f;
            controller.maxLeadDistance = 7f;
            controller.resumeLeadDistance = 4.5f;
            controller.turnSpeed = 360f;
            controller.useSineMovement = true;
            controller.sineAmplitude = 0.75f;
            controller.sineWavelength = 5f;

            Transform glow = prefabRoot.transform.Find("GlowOrb");
            if (glow != null)
            {
                glow.localScale = Vector3.one * 0.55f;
                MeshRenderer renderer = glow.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }

            Light light = prefabRoot.GetComponentInChildren<Light>(true);
            if (light != null)
            {
                light.color = new Color(1f, 0.82f, 0.38f);
                light.intensity = 3.2f;
                light.range = 6f;
                light.shadows = LightShadows.None;
            }
            EditorUtility.SetDirty(controller);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
    }
}
