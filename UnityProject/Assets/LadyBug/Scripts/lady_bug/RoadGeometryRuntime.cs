using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LadyBug
{


// Re-sizes road visuals and gameplay lane counts at run start from the menu
// selection — editor Rebuild Road Geometry uses SceneSetup instead.
public static class RoadGeometryRuntime
{
    const float RoadLength = 150f;
    const float RoadCenterZ = 1f;
    const float RoadTextureTileSize = 1.5f;
    const float GrassTextureTileSize = 4f;
    const float SideGroundWidth = 140f;
    public const float ShoulderWidth = 2.5f;
    public const float ShoulderGap = 0.2f;
    // Trim each long edge so the mesh stops before sand-only texture texels.
    public const float ShoulderEdgeTrim = 0.2f;
    public static float ShoulderRenderWidth => ShoulderWidth - 2f * ShoulderEdgeTrim;
    public static float ShoulderRoadOverlapWorld =>
        ShoulderTintScroller.MaxRoadEdgeOverlapWorld(ShoulderRenderWidth);
    // Inner shoulder edge overlaps road by peak wave amplitude.
    public static float ShoulderCenterOffset => ShoulderRenderWidth / 2f - ShoulderRoadOverlapWorld;
    public static float ShoulderGrassEdgeOffset => ShoulderCenterOffset + ShoulderRenderWidth / 2f;
    // Grass inner edge overlaps shoulder by peak wave amplitude (mirrors road side).
    public static float GrassCenterOffset =>
        SideGroundWidth / 2f - (ShoulderGrassEdgeOffset - ShoulderRoadOverlapWorld);

    public static void Apply(int laneCount, int playerCount)
    {
        laneCount = Mathf.Clamp(laneCount, RoadLayout.MinLaneCount, RoadLayout.MaxLaneCount);
        float laneWidth = RoadLayout.LaneWidthFor(laneCount);
        float roadWidth = laneCount * laneWidth;
        float roadHalf = roadWidth / 2f;

        // Menu preview spawns pickups at the old lane positions — clear them
        // whenever lane geometry changes so nothing lingers on the shoulder.
        DebugRunConfig.ClearRoadEntities();

        ResizeRoadSurface(roadWidth);
        RebuildDividers(laneCount, laneWidth, roadWidth);
        RepositionSideGround(roadHalf);
        RepositionShoulders(roadHalf);

        foreach (var spawner in Object.FindObjectsByType<ShoulderDecorSpawner>())
            spawner.ConfigureRoadHalfWidth(roadHalf);
        foreach (var spawner in Object.FindObjectsByType<GrassDecorSpawner>())
            spawner.ConfigureRoadHalfWidth(roadHalf);
        foreach (var spawner in Object.FindObjectsByType<SideScenerySpawner>())
            spawner.ConfigureSideOffset(roadHalf + ShoulderGap + ShoulderWidth + ShoulderGap + 2f);
        foreach (var spawner in Object.FindObjectsByType<EntitySpawner>())
            spawner.ConfigureLanes(laneCount);
        foreach (var spawner in Object.FindObjectsByType<BigArchSpawner>())
            spawner.ConfigureLanes(laneCount);

        RoadLayout.GetStartLanes(laneCount, out int startLaneRight, out int startLaneLeft);
        foreach (var player in Object.FindObjectsByType<PlayerController>())
        {
            bool solo = playerCount == 1;
            if (solo && player.name == "PlayerRight")
                continue;

            int startLane = player.name == "PlayerLeft"
                ? (solo ? RoadLayout.SoloStartLane(laneCount) : startLaneLeft)
                : startLaneRight;
            player.ConfigureForRun(laneCount, startLane);
        }
    }

    static void ResizeRoadSurface(float roadWidth)
    {
        GameObject surface = GameObject.Find("RoadSurface");
        if (surface == null)
            return;

        Vector3 scale = surface.transform.localScale;
        scale.x = roadWidth;
        surface.transform.localScale = scale;

        Renderer renderer = surface.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            renderer.material.mainTextureScale = new Vector2(
                roadWidth / RoadTextureTileSize,
                RoadLength / RoadTextureTileSize);
        }
    }

    static void RebuildDividers(int laneCount, float laneWidth, float roadWidth)
    {
        var dividers = CollectRootObjects("LaneDivider");
        int needed = Mathf.Max(0, laneCount - 1);

        while (dividers.Count > needed)
        {
            Object.Destroy(dividers[dividers.Count - 1]);
            dividers.RemoveAt(dividers.Count - 1);
        }

        if (needed == 0)
            return;

        // Reuse the first existing divider as a clone source; if the scene
        // was built with 1 lane (no dividers yet), create one in-place —
        // never leave a full-size fallback Plane at the origin as a stray
        // "template" (Instantiate(template) used to orphan it at 10×10 scale).
        while (dividers.Count < needed)
            dividers.Add(LaneDividerBuilder.Create((dividers.Count + 1) * 101, RoadLength));

        for (int i = 0; i < needed; i++)
        {
            float dividerX = -roadWidth / 2f + (i + 1) * laneWidth;
            GameObject divider = dividers[i];
            divider.name = "LaneDivider";
            divider.transform.position = new Vector3(dividerX, 0.02f, RoadCenterZ);
            divider.transform.localScale = new Vector3(0.03f, 1f, RoadLength / 10f);
        }
    }

    static void RepositionSideGround(float roadHalf)
    {
        float grassCenterOffset = GrassCenterOffset;
        RepositionStrip("SideGroundLeft", -1f, roadHalf, SideGroundWidth, grassCenterOffset, -0.052f);
        RepositionStrip("SideGroundRight", 1f, roadHalf, SideGroundWidth, grassCenterOffset, -0.052f);

        foreach (string name in new[] { "SideGroundLeft", "SideGroundRight" })
        {
            GameObject ground = GameObject.Find(name);
            if (ground == null)
                continue;
            Renderer renderer = ground.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                Vector2 scale = renderer.material.mainTextureScale;
                scale.x = SideGroundWidth / GrassTextureTileSize;
                scale.y = Mathf.Abs(RoadLength / GrassTextureTileSize);
                renderer.material.mainTextureScale = scale;
            }

            ScrollingTexture scroller = ground.GetComponent<ScrollingTexture>();
            if (scroller != null)
            {
                scroller.EnsureStripMesh();
                scroller.SetFlipVertical(false);
            }
        }
    }

    static void RepositionShoulders(float roadHalf)
    {
        float centerOffset = ShoulderCenterOffset;
        RepositionStrip("RoadShoulderLeft", -1f, roadHalf, ShoulderRenderWidth, centerOffset, -0.048f);
        RepositionStrip("RoadShoulderRight", 1f, roadHalf, ShoulderRenderWidth, centerOffset, -0.048f);

        foreach (string name in new[] { "RoadShoulderLeft", "RoadShoulderRight" })
        {
            GameObject shoulder = GameObject.Find(name);
            if (shoulder == null)
                continue;
            ShoulderTintScroller scroller = shoulder.GetComponent<ShoulderTintScroller>();
            if (scroller != null)
            {
                scroller.SetFlipVertical(false);
                scroller.SetRoadEdgeAtHighU(name == "RoadShoulderLeft");
            }
        }
    }

    static void RepositionStrip(string objectName, float side, float roadHalf, float stripWidth, float centerOffset = 0f, float y = -0.05f)
    {
        GameObject strip = GameObject.Find(objectName);
        if (strip == null)
            return;

        float x = side * (roadHalf + (centerOffset > 0f ? centerOffset : stripWidth / 2f));
        strip.transform.position = new Vector3(x, y, RoadCenterZ);
        strip.transform.localScale = new Vector3(stripWidth, 0.1f, RoadLength);
    }

    static List<GameObject> CollectRootObjects(string objectName)
    {
        var result = new List<GameObject>();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == objectName)
                result.Add(root);
        return result;
    }
}
}
