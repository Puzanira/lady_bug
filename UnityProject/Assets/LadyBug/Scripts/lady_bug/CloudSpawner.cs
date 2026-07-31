using UnityEngine;

// Spawns ambient background clouds at a random high altitude/x, always
// drifting (not gated on the game running) — pure atmosphere, same spirit
// as the good pickups that drift by on the start screen.
public class CloudSpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] prefabs;
    // At spawnZ=90 the camera's visible half-width is roughly ±100 (60°
    // vertical FOV, ~16:9 aspect, ~98 units away) — a narrow x range here
    // bunches every cloud near the middle of the screen instead of spanning
    // the sky, so this needs to track that width, not just "some spread".
    [SerializeField] private float minX = -85f;
    [SerializeField] private float maxX = 85f;
    [SerializeField] private float minY = 8f;
    [SerializeField] private float maxY = 16f;
    [SerializeField] private float spawnZ = 90f;
    [SerializeField] private float minInterval = 4f;
    [SerializeField] private float maxInterval = 9f;

    private float _timer;
    private float _nextInterval;

    private void Start()
    {
        ScheduleNext();
    }

    private void Update()
    {
        if (prefabs == null || prefabs.Length == 0)
            return;

        _timer += Time.deltaTime;
        if (_timer >= _nextInterval)
        {
            _timer = 0f;
            Spawn();
            ScheduleNext();
        }
    }

    private void ScheduleNext()
    {
        _nextInterval = Random.Range(minInterval, maxInterval);
    }

    private void Spawn()
    {
        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
        Vector3 pos = new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY), spawnZ);
        Instantiate(prefab, pos, prefab.transform.rotation);
    }
}
