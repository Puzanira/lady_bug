using UnityEngine;

public class SideScenerySpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private float sideOffset = 6f; // clearance from the road edge to the object's near edge
    [SerializeField] private float maxExtraOffset = 45f; // random extra distance beyond that, so objects don't all line up at the same distance
    [SerializeField] private float spawnZ = 70f;
    // World units between spawns, not seconds — a fixed real-time interval
    // meant the world-space gap between objects was speed × interval, so at
    // low speed they bunched up close together (and stayed on screen a long
    // time, reading as "constant clutter"), while at high speed the gap
    // stretched out far enough that long empty stretches of roadside were
    // common. Distance-based keeps the spacing (and so the visual density)
    // roughly constant regardless of current speed.
    [SerializeField] private float minSpawnDistance = 20f;
    [SerializeField] private float maxSpawnDistance = 45f;

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
            return; // road isn't moving — nothing should appear yet

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

    // One object on EACH side every tick (not a coin-flip for a single
    // side) — a coin-flip let one side go quiet for several spawns in a
    // row while the other kept getting objects, leaving empty stretches of
    // roadside.
    private void Spawn()
    {
        if (prefabs == null || prefabs.Length == 0)
            return;

        SpawnSide(-1f);
        SpawnSide(1f);
    }

    private void SpawnSide(float side)
    {
        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];

        // Wide sprites (like the pine forest) need more clearance than
        // narrow ones (like the palm tree) or they'll poke onto the road —
        // push out by the object's own half-width on top of the base offset.
        float halfWidth = 0f;
        BoxCollider box = prefab.GetComponent<BoxCollider>();
        if (box != null)
            halfWidth = box.size.x / 2f;

        float x = side * (sideOffset + halfWidth + Random.Range(0f, maxExtraOffset));
        Vector3 pos = new Vector3(x, prefab.transform.position.y, spawnZ);
        Instantiate(prefab, pos, prefab.transform.rotation);
    }
}
