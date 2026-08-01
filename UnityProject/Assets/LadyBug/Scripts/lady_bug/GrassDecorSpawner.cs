using UnityEngine;

namespace LadyBug
{

// Small grass tuft billboards on the green side strip (past the gravel
// shoulder) — separate from ShoulderDecorSpawner's rocks on the shoulder.
public class GrassDecorSpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private float roadHalfWidth = 8f;
    [SerializeField] private float shoulderWidth = 2.5f;
    [SerializeField] private float pavementOverlap = 0.5f;
    [SerializeField] private float innerSpawnMargin = 0.5f;
    [SerializeField] private float maxOffsetFromShoulder = 12f;
    [SerializeField] private float spawnZ = 70f;
    [SerializeField] private float minSpawnDistance = 6f;
    [SerializeField] private float maxSpawnDistance = 16f;
    [SerializeField] private float spawnChancePerSide = 0.8f;

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
        float halfWidth = info != null ? info.visibleHalfWidth : 0.2f;
        float visibleHeight = info != null ? info.visibleHeight : 0.4f;

        float grassInnerEdge = roadHalfWidth + shoulderWidth - pavementOverlap;
        float inner = grassInnerEdge + innerSpawnMargin + halfWidth;
        float outer = grassInnerEdge + maxOffsetFromShoulder - halfWidth;
        if (inner > outer)
            inner = outer;

        float x = side * Random.Range(inner, outer);
        float z = spawnZ + Random.Range(-2f, 2f);
        const float grassGroundY = 0f;
        float y = grassGroundY + visibleHeight * 0.5f;
        Instantiate(prefab, new Vector3(x, y, z), prefab.transform.rotation);
    }
}
}
