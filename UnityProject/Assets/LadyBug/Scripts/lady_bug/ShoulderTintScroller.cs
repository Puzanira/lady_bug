using UnityEngine;

namespace LadyBug
{

/// <summary>
/// Scrolls ShoulderTile UV along Z — texture only, no procedural tint.
/// </summary>
public class ShoulderTintScroller : MonoBehaviour
{
    const float GrassTextureTileSize = 4f;
    const float RoadLength = 150f;

    static Mesh _stripMesh;

    /// <summary>Single quad on the cube top face — no side faces that read as edge stripes.</summary>
    public static Mesh StripMesh
    {
        get
        {
            if (_stripMesh != null)
                return _stripMesh;

            _stripMesh = new Mesh { name = "ShoulderStrip" };
            _stripMesh.vertices = new[]
            {
                new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
            };
            _stripMesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            _stripMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _stripMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            _stripMesh.RecalculateBounds();
            return _stripMesh;
        }
    }

    [SerializeField] private float scrollPeriod = GrassTextureTileSize;
    [SerializeField] private bool flipVertical;
    // Left shoulder: road is at local +X (mesh U=1). Right: road at U=0.
    [SerializeField] private bool roadEdgeAtHighU;

    public const float EdgeWaveAmp = 0.04f;
    public const float EdgeWaveFreq = 5f;
    public const float EdgeAmpVarAmount = 1f;
    public const float EdgeInset = 0f;
    public const float EdgeAmpScaleMax = 2.15f * 1.55f;
    public const float EdgeInsetJitterMax = 0.012f;
    public const float EdgeSoftness = 0.032f;
    public const float EdgeBlurRadius = 0.014f;

    /// <summary>Max road-facing protrusion in strip UV — matches shader peak amplitude.</summary>
    public static float MaxRoadEdgeOverlapUv =>
        EdgeWaveAmp * EdgeAmpScaleMax * EdgeAmpVarAmount + EdgeInsetJitterMax * EdgeAmpVarAmount;

    public static float MaxRoadEdgeOverlapWorld(float stripWidthWorld) =>
        MaxRoadEdgeOverlapUv * stripWidthWorld;

    static readonly int RoadEdgeAtHighUId = Shader.PropertyToID("_RoadEdgeAtHighU");
    static readonly int EdgeInsetId = Shader.PropertyToID("_EdgeInset");
    static readonly int EdgeWaveAmpId = Shader.PropertyToID("_EdgeWaveAmp");
    static readonly int EdgeWaveFreqId = Shader.PropertyToID("_EdgeWaveFreq");
    static readonly int EdgeAmpVarId = Shader.PropertyToID("_EdgeAmpVar");
    static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    static readonly int EdgeBlurRadiusId = Shader.PropertyToID("_EdgeBlurRadius");

    private Material _material;
    private Vector2 _offset;

    public void SetScrollPeriod(float period)
    {
        scrollPeriod = period;
    }

    public void SetFlipVertical(bool flip)
    {
        flipVertical = flip;
        if (_material != null)
            ApplyMaterialLayout();
    }

    public void SetRoadEdgeAtHighU(bool atHighU)
    {
        roadEdgeAtHighU = atHighU;
        if (_material != null)
            ApplyMaterialLayout();
    }

    private void Awake()
    {
        EnsureStripMesh();
        _material = GetComponent<Renderer>().material;
        ApplyMaterialLayout();
    }

    void EnsureStripMesh()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || (meshFilter.sharedMesh != null && meshFilter.sharedMesh.vertexCount == 4))
            return;

        meshFilter.sharedMesh = StripMesh;
    }

    private void ApplyMaterialLayout()
    {
        scrollPeriod = GrassTextureTileSize;

        Vector2 scale = _material.mainTextureScale;
        scale.x = 1f;
        scale.y = flipVertical ? -(RoadLength / GrassTextureTileSize) : (RoadLength / GrassTextureTileSize);
        _material.mainTextureScale = scale;
        _offset.y = flipVertical ? 1f : 0f;

        _material.SetFloat(RoadEdgeAtHighUId, roadEdgeAtHighU ? 1f : 0f);
        _material.SetFloat(EdgeInsetId, EdgeInset);
        _material.SetFloat(EdgeWaveAmpId, EdgeWaveAmp);
        _material.SetFloat(EdgeWaveFreqId, EdgeWaveFreq);
        _material.SetFloat(EdgeAmpVarId, EdgeAmpVarAmount);
        _material.SetFloat(EdgeSoftnessId, EdgeSoftness);
        _material.SetFloat(EdgeBlurRadiusId, EdgeBlurRadius);

        _material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void Update()
    {
        float speed = SpeedController.Instance != null ? SpeedController.Instance.CurrentSpeed : 10f;
        float delta = (speed / scrollPeriod) * Time.deltaTime;
        // ShoulderTile V runs opposite to road asphalt along the strip — flipVertical
        // handles upright stones; scroll sign is inverted vs ScrollingTexture.
        _offset.y += flipVertical ? -delta : delta;
        _material.mainTextureOffset = _offset;
    }
}
}
