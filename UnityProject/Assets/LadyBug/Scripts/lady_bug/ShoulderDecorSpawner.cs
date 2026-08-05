using UnityEngine;

namespace LadyBug
{

// Spawns rock and grass tuft decals on the gravel shoulder — separate from
// GrassDecorSpawner's tufts on the green side strip and from
// SideScenerySpawner's big cactuses/trees further out.
public class ShoulderDecorSpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private float roadHalfWidth = 8f;
    // Must match CreateRoadShoulder — shoulder from (roadHalf + gap) outward.
    [SerializeField] private float shoulderWidth = 2.5f;
    [SerializeField] private float shoulderGap = RoadGeometryRuntime.ShoulderGap;
    [SerializeField] private float innerSpawnMargin = 0.2f;
    [SerializeField] private float outerSpawnMargin = 0.35f;
    [SerializeField] private float spawnZ = 70f;
    [SerializeField] private float minSpawnDistance = 5f;
    [SerializeField] private float maxSpawnDistance = 14f;
    [SerializeField] private float spawnChancePerSide = 0.85f;

    private const float ShoulderGroundY = 0.02f;

    private float _distanceSinceLastSpawn;
    private float _nextSpawnDistance;

    private void Start()
    {
        ScheduleNext();
    }

    private void Update()
    {
        float speed = SpeedController.Instance != null ? SpeedController.Instance.CurrentSpeed : 0f;
        if (speed <= 0f)
            return;

        _distanceSinceLastSpawn += speed * Time.deltaTime;
        if (_distanceSinceLastSpawn >= _nextSpawnDistance)
        {
            _distanceSinceLastSpawn = 0f;
            Spawn();
            ScheduleNext();
        }
    }

    private void ScheduleNext()
    {
        _nextSpawnDistance = Random.Range(minSpawnDistance, maxSpawnDistance);
    }

    public void ConfigureRoadHalfWidth(float halfWidth)
    {
        roadHalfWidth = halfWidth;
    }

    private void Spawn()
    {
        if (prefabs == null || prefabs.Length == 0)
            return;

        if (Random.value < spawnChancePerSide)
            SpawnSide(-1f);
        if (Random.value < spawnChancePerSide)
            SpawnSide(1f);
    }

    private void SpawnSide(float side)
    {
        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
        GroundDecorInfo info = prefab.GetComponent<GroundDecorInfo>();
        float halfWidth = info != null ? info.visibleHalfWidth : GetPrefabHalfWidth(prefab);
        float visibleHeight = info != null ? info.visibleHeight : prefab.transform.position.y * 2f;
        float shoulderInnerEdge = roadHalfWidth + shoulderGap;
        float shoulderOuterEdge = roadHalfWidth + shoulderGap + RoadGeometryRuntime.ShoulderRenderWidth;
        float inner = shoulderInnerEdge + innerSpawnMargin + halfWidth;
        float outer = shoulderOuterEdge - outerSpawnMargin - halfWidth;
        if (inner > outer)
            inner = outer;
        float x = side * Random.Range(inner, outer);
        float z = spawnZ + Random.Range(-2f, 2f);
        float sink = prefab.name.Contains("Rock")
            ? Random.Range(0f, visibleHeight * 0.5f)
            : 0f;
        float y = ShoulderGroundY + visibleHeight * 0.5f - sink;
        Instantiate(prefab, new Vector3(x, y, z), prefab.transform.rotation);
    }

    private static float GetPrefabHalfWidth(GameObject prefab)
    {
        Transform sprite = prefab.transform.Find("Sprite");
        return sprite != null ? sprite.localScale.x * 0.5f : 0.3f;
    }
}
}
