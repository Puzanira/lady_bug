using UnityEngine;

namespace LadyBug
{

public class ScrollingTexture : MonoBehaviour
{
    [SerializeField] private float dashPeriod = 4f;
    [SerializeField] private bool flipVertical;

    static Mesh _stripMesh;

    /// <summary>Flat top-face quad — no cube sides that read as a dark edge stripe.</summary>
    public static Mesh StripMesh
    {
        get
        {
            if (_stripMesh != null)
                return _stripMesh;

            _stripMesh = new Mesh { name = "GroundStrip" };
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

    private Renderer _renderer;
    private Vector2 _offset;

    public void SetDashPeriod(float period)
    {
        dashPeriod = period;
    }

    public void SetFlipVertical(bool flip)
    {
        flipVertical = flip;
        if (_renderer != null)
            ApplyFlipToMaterial();
    }

    private void Awake()
    {
        if (IsSideGround())
            EnsureStripMesh();
        _renderer = GetComponent<Renderer>();
        if (IsSideGround())
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null && meshFilter.sharedMesh.vertexCount == 4)
                flipVertical = false;
        }
        ApplyFlipToMaterial();
    }

    public void EnsureStripMesh()
    {
        if (!IsSideGround())
            return;

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || (meshFilter.sharedMesh != null && meshFilter.sharedMesh.vertexCount == 4))
            return;

        meshFilter.sharedMesh = StripMesh;
    }

    private void ApplyFlipToMaterial()
    {
        Material mat = _renderer.material;
        Vector2 scale = mat.mainTextureScale;
        float absY = Mathf.Abs(scale.y);
        if (flipVertical)
        {
            mat.mainTextureScale = new Vector2(scale.x, -absY);
            _offset.y = 1f;
        }
        else
        {
            mat.mainTextureScale = new Vector2(scale.x, absY);
            _offset.y = 0f;
        }
    }

    private void Update()
    {
        float speed = SpeedController.Instance != null ? SpeedController.Instance.CurrentSpeed : 10f;
        float delta = (speed / dashPeriod) * Time.deltaTime;
        // Flat strip quads scroll opposite to cube top faces when flipVertical is off.
        bool scrollWithFlip = flipVertical || UsesStripMesh();
        _offset.y += scrollWithFlip ? delta : -delta;
        _renderer.material.mainTextureOffset = _offset;
    }

    bool IsSideGround()
    {
        return gameObject.name is "SideGroundLeft" or "SideGroundRight";
    }

    bool UsesStripMesh()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        return meshFilter != null && meshFilter.sharedMesh != null && meshFilter.sharedMesh.vertexCount == 4;
    }
}
}
