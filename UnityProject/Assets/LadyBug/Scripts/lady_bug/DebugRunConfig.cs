using UnityEngine;

namespace LadyBug
{

// Gameplay tuning — trick windows, jump/hover durations, optional empty-road debug.
public static class DebugRunConfig
{
    public const bool EmptyRoad = false;

    // Debug: when non-empty, this is the ONLY thing that appears on or beside
    // the road — every other pickup and obstacle is dropped from the spawner
    // pools, and the big arch, roadside scenery, shoulder decor, clouds and
    // birds are not built at all. For studying one object's animation without
    // waiting for it to come round in the rotation or picking it out of a busy
    // screen. The road, ground, shoulder and sky backdrop stay, otherwise there
    // is nothing to judge the motion against.
    //
    // Set to "" for a normal run. Applied at scene-build time, so changing it
    // needs Tools -> Rebuild Scene.
    public const string OnlyEntity = "";

    public static bool IsolatingSingleEntity => !string.IsNullOrEmpty(OnlyEntity);

    public static float RoadHalfWidth
    {
        get
        {
            foreach (var pc in Object.FindObjectsByType<PlayerController>())
                return RoadLayout.HalfRoadSpan(pc.LaneCount) + 2f;
            return RoadLayout.HalfRoadSpan(1) + 2f;
        }
    }

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
        foreach (var entity in Object.FindObjectsByType<MovingEntity>())
        {
            if (entity != null)
                Object.Destroy(entity.gameObject);
        }
    }
}
}
