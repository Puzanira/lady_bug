using UnityEngine;

namespace LadyBug
{

// Gameplay tuning — trick windows, jump/hover durations, optional empty-road debug.
public static class DebugRunConfig
{
    public const bool EmptyRoad = false;
    public const float RoadHalfWidth = 8f;

    public const float RingTrickWindow = 1.5f;
    public const float SyncPartnerMoveTolerance = 0.6f;
    public const float MoveHistoryWindow = 15f;
    public const float TrickStepMaxGap = 1.8f;
    // How recently the last step of a multi-step trick must have happened
    // for it to still suppress ЗАВИСАНИЕ — stale tail matches must not stick.
    public const float MultiStepTrickActiveWindow = 4f;
    public const float LeapfrogDismountWindow = 3.5f;
    public const float BigRingPatternWindow = 9f;
    public const float BigRingPartnerSyncWindow = 6f;
    public const float InfinityPatternWindow = 18f;
    public const float InfinityPartnerSyncWindow = 12f;
    public const float HoverTrickDuration = 5f;

    public const float JumpDuration = 2f;
    public const bool JumpUntilTimerExpires = false;

    public static bool AllowsRoadSpawns => !EmptyRoad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyAfterSceneLoad()
    {
        if (!EmptyRoad)
            return;

        DisableRoadSpawners();
        ClearRoadEntities();
    }

    public static void DisableRoadSpawners()
    {
        if (!EmptyRoad)
            return;

        foreach (var spawner in Object.FindObjectsByType<EntitySpawner>())
            spawner.enabled = false;

        foreach (var spawner in Object.FindObjectsByType<BigArchSpawner>())
            spawner.enabled = false;
    }

    public static void ClearRoadEntities()
    {
        if (!EmptyRoad)
            return;

        foreach (var entity in Object.FindObjectsByType<MovingEntity>())
        {
            if (entity == null)
                continue;

            if (Mathf.Abs(entity.transform.position.x) <= RoadHalfWidth)
                Object.Destroy(entity.gameObject);
        }
    }
}
}
