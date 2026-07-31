using System.Collections.Generic;
using UnityEngine;

// Per-category collect/hit/trick counts for the post-win achievements
// screen — separate from ScoreManager/TricksManager's own totals, which
// only know the point value, not *what* was collected/hit. Fed directly at
// the moment of collision/trick (not tied to the popup-arrival timing those
// two use), so it's always accurate the instant WinSequence reads it.
public class AchievementStats : MonoBehaviour
{
    public static AchievementStats Instance { get; private set; }

    // Keyed by the entity's own displayed texture (whatever PlayerController
    // read off its "Sprite" child at the moment of collision) rather than a
    // fixed set of named categories — every collected/hit object, however
    // many distinct visual variants exist (e.g. the 9 different flower/
    // daisy/sunflower/lotus sprites), gets counted under its own real
    // picture instead of being lumped under one shared icon that wouldn't
    // match what the player actually saw.
    public readonly Dictionary<Texture2D, int> CollectedByIcon = new Dictionary<Texture2D, int>();
    public readonly Dictionary<Texture2D, int> HitByIcon = new Dictionary<Texture2D, int>();
    // Counts a collision whose texture couldn't be read (icon == null) —
    // Dictionary<TKey,TValue> throws on a null key regardless of TKey being
    // a reference type, so this can't just be another entry in the
    // dictionaries above. Kept separate and folded into a fallback icon by
    // WinSequence, so a run can never end up with a total that doesn't
    // match what's actually drawn on the page (the original bug this whole
    // texture-keyed design replaced).
    public int UnknownCollected { get; private set; }
    public int UnknownHit { get; private set; }
    public int TotalCollected { get; private set; }
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

    public void RecordCollected(Texture2D icon)
    {
        TotalCollected++;
        if (icon == null)
        {
            UnknownCollected++;
            return;
        }
        CollectedByIcon[icon] = CollectedByIcon.TryGetValue(icon, out int c) ? c + 1 : 1;
    }

    public void RecordHit(Texture2D icon)
    {
        TotalHit++;
        if (icon == null)
        {
            UnknownHit++;
            return;
        }
        HitByIcon[icon] = HitByIcon.TryGetValue(icon, out int c) ? c + 1 : 1;
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
}
