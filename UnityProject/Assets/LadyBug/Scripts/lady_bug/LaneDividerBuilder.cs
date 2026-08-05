using UnityEngine;

namespace LadyBug
{


// Runtime lane dividers — same dashed texture + scroll setup as
// SceneSetup.CreateDashedDivider (editor scene build).
public static class LaneDividerBuilder
{
    public const float DashPeriod = 8f;
    public const int DashVariantCount = 3;

    public static GameObject Create(int seedOffset, float roadLength)
    {
        GameObject divider = GameObject.CreatePrimitive(PrimitiveType.Plane);
        divider.name = "LaneDivider";
        Object.Destroy(divider.GetComponent<Collider>());

        Renderer renderer = divider.GetComponent<Renderer>();
        ApplyDashedMaterial(renderer, seedOffset, roadLength);

        ScrollingTexture scroller = divider.AddComponent<ScrollingTexture>();
        scroller.SetDashPeriod(DashPeriod * DashVariantCount);

        return divider;
    }

    public static void ApplyDashedMaterial(Renderer renderer, int seedOffset, float roadLength)
    {
        if (renderer == null)
            return;

        Shader shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
        renderer.sharedMaterial = new Material(shader)
        {
            mainTexture = CreateDashTexture(seedOffset),
            mainTextureScale = new Vector2(1f, roadLength / (DashPeriod * DashVariantCount))
        };
    }

    static Texture2D CreateDashTexture(int seedOffset)
    {
        const int width = 16;
        const int bandHeight = 32;
        var tex = new Texture2D(width, bandHeight * DashVariantCount, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        DrawDashBand(tex, 0 * bandHeight, width, bandHeight, seed: 777 + seedOffset, edgeJitter: 3, chipChance: 0.05f, wavy: false);
        DrawDashBand(tex, 1 * bandHeight, width, bandHeight, seed: 991 + seedOffset, edgeJitter: 6, chipChance: 0.12f, wavy: false);
        DrawDashBand(tex, 2 * bandHeight, width, bandHeight, seed: 313 + seedOffset, edgeJitter: 2, chipChance: 0.08f, wavy: true);

        tex.Apply();
        return tex;
    }

    static void DrawDashBand(Texture2D tex, int yOffset, int width, int height, int seed, int edgeJitter, float chipChance, bool wavy)
    {
        var rng = new System.Random(seed);
        Color gap = new Color(0.25f, 0.25f, 0.25f);
        Color paint = new Color(0.92f, 0.92f, 0.88f);
        int baseDashEnd = Mathf.RoundToInt(height * 0.625f);

        var dashEndPerColumn = new int[width];
        for (int x = 0; x < width; x++)
        {
            int wave = wavy ? Mathf.RoundToInt(Mathf.Sin(x / (float)width * Mathf.PI * 2f) * 4f) : 0;
            dashEndPerColumn[x] = Mathf.Clamp(baseDashEnd + wave + rng.Next(-edgeJitter, edgeJitter + 1), 1, height - 1);
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                bool isDash = y < dashEndPerColumn[x];
                if (isDash && rng.NextDouble() < chipChance)
                    isDash = false;

                Color baseColor = isDash ? paint : gap;
                float jitter = ((float)rng.NextDouble() - 0.5f) * (isDash ? 0.1f : 0.12f);
                tex.SetPixel(x, yOffset + y, new Color(
                    Mathf.Clamp01(baseColor.r + jitter),
                    Mathf.Clamp01(baseColor.g + jitter),
                    Mathf.Clamp01(baseColor.b + jitter)));
            }
        }
    }
}
}
