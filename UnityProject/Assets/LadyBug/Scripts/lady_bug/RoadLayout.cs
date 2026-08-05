using UnityEngine;

namespace LadyBug
{


// Shared road geometry — lane width shrinks when there are many lanes so
// the outermost players stay on screen (7 lanes × 4 units was too wide).
public static class RoadLayout
{
    // Reference span that fit on screen at 4 lanes × 4 units each.
    public const float MaxRoadSpan = 16f;
    public const float BaseLaneWidth = 4f;
    public const int MinLaneCount = 1;
    public const int MaxLaneCount = 7;

    public static float LaneWidthFor(int laneCount)
    {
        if (laneCount <= 0)
            return BaseLaneWidth;
        return Mathf.Min(BaseLaneWidth, MaxRoadSpan / laneCount);
    }

    public static float LaneObjectScaleFactor(int laneCount) =>
        LaneWidthFor(laneCount) / BaseLaneWidth;

    public static float HalfRoadSpan(int laneCount) =>
        laneCount * LaneWidthFor(laneCount) / 2f;

    public static float LaneCenterX(int lane, int laneCount) =>
        (lane - (laneCount - 1) / 2f) * LaneWidthFor(laneCount);

    // Co-op start lanes — mirrors SceneSetup.GetStartLanes.
    public static void GetStartLanes(int laneCount, out int startLaneRight, out int startLaneLeft)
    {
        if (laneCount <= 1)
        {
            startLaneRight = 0;
            startLaneLeft = 0;
            return;
        }

        if (laneCount % 2 == 0)
        {
            startLaneRight = laneCount / 2;
            startLaneLeft = startLaneRight - 1;
        }
        else
        {
            int midLane = laneCount / 2;
            startLaneRight = midLane + 1;
            startLaneLeft = midLane - 1;
        }
    }

    public static int SoloStartLane(int laneCount) => laneCount / 2;

    // Full road width plus a little past the shoulders — the sign art insets
    // its posts from the quad edges, so (laneCount-1)*laneWidth reads too
    // narrow (posts land mid-lane, not on the outer lanes).
    public static float BigArchSpanWidth(int laneCount)
    {
        laneCount = Mathf.Max(1, laneCount);
        const float shoulderMargin = 4f;
        return laneCount * LaneWidthFor(laneCount) + shoulderMargin;
    }
}
}
