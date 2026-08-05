using UnityEngine;

namespace LadyBug
{


// Spawns schematic sky birds (curved wing lines) — ambient motion like
// CloudSpawner, independent of whether the run has started.
public class BirdSpawner : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private float minX = -90f;
    [SerializeField] private float maxX = 90f;
    [SerializeField] private float minY = 14f;
    [SerializeField] private float maxY = 22f;
    [SerializeField] private float spawnZ = 95f;
    // Overlaps CloudSpawner depth so birds and clouds randomly occlude each other.
    [SerializeField] private float spawnZJitter = 7f;
    [SerializeField] private float minInterval = 2.5f;
    [SerializeField] private float maxInterval = 6f;
    [SerializeField] private int minSpawnCount = 1;
    [SerializeField] private int maxSpawnCount = 2;
    [SerializeField] private int initialMinCount = 4;
    [SerializeField] private int initialMaxCount = 8;

    float _timer;
    float _nextInterval;

    void Start()
    {
        int initial = Random.Range(initialMinCount, initialMaxCount + 1);
        for (int i = 0; i < initial; i++)
            Spawn();
        ScheduleNext();
    }

    void Update()
    {
        if (prefab == null)
            return;

        _timer += Time.deltaTime;
        if (_timer >= _nextInterval)
        {
            _timer = 0f;
            int count = Random.Range(minSpawnCount, maxSpawnCount + 1);
            for (int i = 0; i < count; i++)
                Spawn();
            ScheduleNext();
        }
    }

    void ScheduleNext()
    {
        _nextInterval = Random.Range(minInterval, maxInterval);
    }

    void Spawn()
    {
        float x = Random.Range(minX, maxX);
        float y = Random.Range(minY, maxY);
        float z = spawnZ + Random.Range(-spawnZJitter, spawnZJitter);
        Vector3 pos = new Vector3(x, y, z);
        GameObject instance = Instantiate(prefab, pos, Quaternion.identity);

        SkyBird bird = instance.GetComponent<SkyBird>();
        if (bird != null)
        {
            float lateral = Random.Range(-1.6f, 1.6f);
            if (Mathf.Abs(lateral) < 0.25f)
                lateral = 0.7f * (Random.value < 0.5f ? -1f : 1f);

            float span = Random.Range(1.0f, 3.2f);
            float sizeT = Mathf.InverseLerp(1.0f, 3.2f, span);
            float width = Mathf.Lerp(0.14f, 0.34f, sizeT) * Random.Range(0.9f, 1.1f);
            float drift = Random.Range(2.8f, 7.2f);
            float flapSpeed = Random.Range(6f, 15f);

            bird.Configure(lateral, width, span, drift, flapSpeed);
        }
    }
}
}
