using UnityEngine;

namespace LadyBug
{

// Scrolls ShoulderTile UVs and drives Custom/ShoulderTint segment lookup.
// A new random seed is rolled each time a run begins so tint bands change
// every game without baking ShoulderTileVariants.png.
public class ShoulderTintScroller : MonoBehaviour
{
    [SerializeField] private float scrollPeriod = 4f;
    [SerializeField] private float tileWorldSize = 4f;
    [SerializeField] private float segOrigin = 500f;

    private static readonly int ScrollWorldId = Shader.PropertyToID("_ScrollWorld");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly int SegOriginId = Shader.PropertyToID("_SegOrigin");

    private Renderer _renderer;
    private Material _material;
    private float _uvScroll;
    private bool _wasRunning;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _material = _renderer.material;
        _material.SetFloat(SegOriginId, segOrigin);
        RollNewSeed();
    }

    private void OnDestroy()
    {
        if (_material != null)
            Destroy(_material);
    }

    private void Update()
    {
        SpeedController speed = SpeedController.Instance;
        bool running = speed != null && speed.IsRunning;
        if (running && !_wasRunning)
            RollNewSeed();
        _wasRunning = running;

        float currentSpeed = speed != null ? speed.CurrentSpeed : 0f;
        _uvScroll -= currentSpeed / scrollPeriod * Time.deltaTime;
        _material.mainTextureOffset = new Vector2(0f, _uvScroll);
        _material.SetFloat(ScrollWorldId, -_uvScroll * tileWorldSize);
    }

    public void RollNewSeed()
    {
        _uvScroll = 0f;
        _material.SetFloat(SeedId, Random.Range(0f, 10000f));
        _material.mainTextureOffset = Vector2.zero;
        _material.SetFloat(ScrollWorldId, 0f);
    }
}
}
