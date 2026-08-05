using UnityEngine;

namespace LadyBug
{

// Spawns the road-wide arch (all 3 lanes at once) on its own rare interval,
// separate from EntitySpawner — that one picks a single lane per spawn, but
// this obstacle always spans every lane, so it needs its own timer and
// always spawns centered.
public class BigArchSpawner : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private int laneCount = 1;
    [SerializeField] private float spawnZ = 70f;
    [SerializeField] private float minInterval = 9f;
    [SerializeField] private float maxInterval = 16f;

    private float _timer;
    private float _nextInterval;

    private void Awake()
    {
        if (DebugRunConfig.EmptyRoad)
            enabled = false;
    }

    private void Start()
    {
        if (DebugRunConfig.EmptyRoad)
            return;

        ScheduleNext();
    }

    private void Update()
    {
        if (DebugRunConfig.EmptyRoad)
            return;

        bool gameRunning = SpeedController.Instance != null && SpeedController.Instance.IsRunning;
        if (!gameRunning || prefab == null)
            return;

        _timer += Time.deltaTime;
        if (_timer >= _nextInterval)
        {
            _timer = 0f;
            Vector3 pos = new Vector3(0f, prefab.transform.position.y, spawnZ);
            GameObject instance = Instantiate(prefab, pos, prefab.transform.rotation);
            ApplySpan(instance);
            ScheduleNext();
        }
    }

    public void ConfigureLanes(int count)
    {
        laneCount = Mathf.Clamp(count, RoadLayout.MinLaneCount, RoadLayout.MaxLaneCount);
    }

    private void ApplySpan(GameObject instance)
    {
        BigArchLayout layout = instance.GetComponent<BigArchLayout>();
        if (layout == null)
            layout = instance.AddComponent<BigArchLayout>();
        layout.ApplySpan(laneCount);
    }

    private void ScheduleNext()
    {
        _nextInterval = Random.Range(minInterval, maxInterval);
    }
}
}
