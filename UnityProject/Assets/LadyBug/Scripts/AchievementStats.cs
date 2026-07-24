using UnityEngine;

// Per-category collect/hit/trick counts for the post-win achievements
// screen — separate from ScoreManager/TricksManager's own totals, which
// only know the point value, not *what* was collected/hit. Fed directly at
// the moment of collision/trick (not tied to the popup-arrival timing those
// two use), so it's always accurate the instant WinSequence reads it.
public class AchievementStats : MonoBehaviour
{
    public static AchievementStats Instance { get; private set; }

    public int CherriesCollected { get; private set; }
    public int FlowersCollected { get; private set; }
    public int HeartsCollected { get; private set; }
    public int TotalCollected { get; private set; }

    public int BicyclesHit { get; private set; }
    public int CatsHit { get; private set; }
    public int DogsHit { get; private set; }
    public int TotalHit { get; private set; }

    public int RingTricks { get; private set; }
    public int ArchTricks { get; private set; }
    public int LeapfrogTricks { get; private set; }
    public int SyncTricks { get; private set; }
    public int HoverTricks { get; private set; }
    public int BigRingTricks { get; private set; }
    public int InfinityTricks { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    // entityName: the spawned prefab instance's GameObject name, e.g.
    // "Cherry(Clone)" — same name Instantiate() leaves it with, same
    // name-prefix convention SfxManager.PlayBad already dispatches on.
    public void RecordCollected(string entityName)
    {
        TotalCollected++;
        if (entityName.StartsWith("Cherry"))
            CherriesCollected++;
        else if (entityName.StartsWith("Heart"))
            HeartsCollected++;
        else if (IsFlowerName(entityName))
            FlowersCollected++;
    }

    public void RecordHit(string entityName)
    {
        TotalHit++;
        if (entityName.StartsWith("Dog"))
            DogsHit++;
        else if (entityName.StartsWith("Cat"))
            CatsHit++;
        else if (IsBicycleName(entityName))
            BicyclesHit++;
    }

    public void RecordTrick(string trickName)
    {
        if (trickName == "КОЛЬЦО")
            RingTricks++;
        else if (trickName == "АРКА")
            ArchTricks++;
        else if (trickName == "ЧЕХАРДА")
            LeapfrogTricks++;
        else if (trickName == "СИНХРОН")
            SyncTricks++;
        else if (trickName == "ЗАВИСАНИЕ")
            HoverTricks++;
        else if (trickName == "БОЛЬШОЕ КОЛЬЦО")
            BigRingTricks++;
        else if (trickName == "БЕСКОНЕЧНОСТЬ")
            InfinityTricks++;
    }

    private static bool IsFlowerName(string name)
    {
        return name.StartsWith("Flower") || name.StartsWith("Daisy")
            || name.StartsWith("Sunflower") || name.StartsWith("Lotus");
    }

    private static bool IsBicycleName(string name)
    {
        return name.StartsWith("Bicycle") || name.StartsWith("Motorbike") || name.StartsWith("Motorcycle");
    }
}
