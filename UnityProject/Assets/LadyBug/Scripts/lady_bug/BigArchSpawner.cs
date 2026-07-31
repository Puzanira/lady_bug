using UnityEngine;

// Spawns the road-wide arch (all 3 lanes at once) on its own rare interval,
// separate from EntitySpawner — that one picks a single lane per spawn, but
// this obstacle always spans every lane, so it needs its own timer and
// always spawns centered.
public class BigArchSpawner : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private float spawnZ = 70f;
    [SerializeField] private float minInterval = 9f;
    [SerializeField] private float maxInterval = 16f;

    private float _timer;
    private float _nextInterval;

    private void Start()
    {
        ScheduleNext();
    }

    private void Update()
    {
        bool gameRunning = SpeedController.Instance != null && SpeedController.Instance.IsRunning;
        if (!gameRunning || prefab == null)
            return;

        _timer += Time.deltaTime;
        if (_timer >= _nextInterval)
        {
            _timer = 0f;
            Vector3 pos = new Vector3(0f, prefab.transform.position.y, spawnZ);
            Instantiate(prefab, pos, prefab.transform.rotation);
            ScheduleNext();
        }
    }

    private void ScheduleNext()
    {
        _nextInterval = Random.Range(minInterval, maxInterval);
    }
}
