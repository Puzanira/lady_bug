using UnityEngine;

public class EntitySpawner : MonoBehaviour
{
    // Selection happens in two steps: good vs. bad, then — only for bad —
    // whether it's a jump-over or duck-under obstacle. Each pool is picked
    // uniformly at random once the category is decided.
    [SerializeField] private GameObject[] goodPrefabs;
    [SerializeField] private GameObject[] badJumpPrefabs;
    [SerializeField] private GameObject[] badDuckPrefabs;

    [SerializeField] [Range(0f, 1f)] private float goodChance = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float duckChance = 0.5f;

    [SerializeField] private int laneCount = 3;
    [SerializeField] private float laneWidth = 3f;
    [SerializeField] private float spawnZ = 70f;
    [SerializeField] private float minInterval = 1.1f;
    [SerializeField] private float maxInterval = 2.6f;

    private float _timer;
    private float _nextInterval;

    private void Start()
    {
        ScheduleNext();
    }

    private void Update()
    {
        float speed = SpeedController.Instance != null ? SpeedController.Instance.CurrentSpeed : 0f;
        if (speed <= 0f)
            return; // road isn't moving — nothing should appear yet

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
        GameObject prefab = PickPrefab();
        if (prefab == null)
            return;

        int lane = Random.Range(0, laneCount);
        float x = (lane - (laneCount - 1) / 2f) * laneWidth;
        Vector3 pos = new Vector3(x, prefab.transform.position.y, spawnZ);
        Instantiate(prefab, pos, prefab.transform.rotation);
    }

    private GameObject PickPrefab()
    {
        // Pre-game (start screen, ambient background scroll) — only good
        // pickups drift by; real obstacles only start once the game begins.
        bool gameRunning = SpeedController.Instance != null && SpeedController.Instance.IsRunning;
        if (!gameRunning || Random.value < goodChance)
            return PickFrom(goodPrefabs);

        bool duck = Random.value < duckChance;
        GameObject picked = PickFrom(duck ? badDuckPrefabs : badJumpPrefabs);
        if (picked == null)
            picked = PickFrom(duck ? badJumpPrefabs : badDuckPrefabs); // other bad pool as fallback
        return picked;
    }

    private static GameObject PickFrom(GameObject[] pool)
    {
        if (pool == null || pool.Length == 0)
            return null;
        return pool[Random.Range(0, pool.Length)];
    }
}
