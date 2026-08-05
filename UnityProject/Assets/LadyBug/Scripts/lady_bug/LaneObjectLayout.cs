using UnityEngine;

namespace LadyBug
{


// Scales lane pickups/obstacles to match the current lane width. Prefabs are
// baked at BaseLaneWidth (4 units); on 7 lanes each lane is ~2.29 wide, so
// unscaled art/colliders bleed into neighbours. BigArch keeps its own layout.
public static class LaneObjectLayout
{
    const float FullLaneMargin = 0.3f;
    const float FullLaneWidthThreshold = RoadLayout.BaseLaneWidth - 0.5f;
    const float GroundDecalRotationX = 90f;

    public static void ApplyForLaneCount(GameObject instance, int laneCount)
    {
        if (instance == null)
            return;
        if (instance.GetComponent<BigArchLayout>() != null || instance.GetComponent<TallArchObstacle>() != null)
            return;

        float laneWidth = RoadLayout.LaneWidthFor(laneCount);
        float factor = RoadLayout.LaneObjectScaleFactor(laneCount);

        Transform sprite = instance.transform.Find("Sprite");
        BoxCollider box = instance.GetComponent<BoxCollider>();
        Transform shadow = instance.transform.Find("Shadow");
        bool groundDecal = IsGroundDecal(sprite);
        bool spansLane = instance.GetComponent<DuckUnderObstacle>() != null
            || (!groundDecal && box != null && box.size.x >= FullLaneWidthThreshold);

        float widthScale = factor;
        float depthScale = factor;
        if (spansLane)
        {
            float targetWidth = laneWidth - FullLaneMargin;
            float currentWidth = GetColliderWidth(box, sprite, groundDecal);
            widthScale = currentWidth > 0f ? targetWidth / currentWidth : factor;
        }

        ScaleSprite(sprite, widthScale, factor, groundDecal);
        ScaleCollider(box, widthScale, factor, groundDecal);

        if (sprite != null && instance.GetComponent<DuckUnderObstacle>() != null)
        {
            Vector3 localPos = sprite.localPosition;
            localPos.y = sprite.localScale.y / 2f;
            sprite.localPosition = localPos;
        }

        if (!groundDecal)
        {
            Vector3 pos = instance.transform.position;
            pos.y *= factor;
            instance.transform.position = pos;
        }

        if (shadow != null)
        {
            Vector3 shadowScale = shadow.localScale;
            shadow.localScale = new Vector3(shadowScale.x * widthScale, shadowScale.y, shadowScale.z * depthScale);
            shadow.localPosition = new Vector3(0f, 0.02f - instance.transform.position.y, 0f);
        }

        SnakePose pose = instance.GetComponent<SnakePose>();
        if (pose != null)
            pose.ApplyLaneScale(factor);
    }

    static bool IsGroundDecal(Transform sprite)
    {
        if (sprite == null)
            return false;
        return Mathf.Abs(sprite.localEulerAngles.x - GroundDecalRotationX) < 1f;
    }

    static float GetColliderWidth(BoxCollider box, Transform sprite, bool groundDecal)
    {
        if (box != null)
            return groundDecal ? Mathf.Max(box.size.x, box.size.z) : box.size.x;
        if (sprite == null)
            return 0f;
        return groundDecal
            ? Mathf.Max(Mathf.Abs(sprite.localScale.x), Mathf.Abs(sprite.localScale.y))
            : Mathf.Abs(sprite.localScale.x);
    }

    static void ScaleSprite(Transform sprite, float widthScale, float heightScale, bool groundDecal)
    {
        if (sprite == null)
            return;

        Vector3 scale = sprite.localScale;
        if (groundDecal)
        {
            float signX = scale.x < 0f ? -1f : 1f;
            float signY = scale.y < 0f ? -1f : 1f;
            sprite.localScale = new Vector3(scale.x * widthScale * signX, scale.y * heightScale * signY, scale.z);
            return;
        }

        float facing = scale.x < 0f ? -1f : 1f;
        sprite.localScale = new Vector3(Mathf.Abs(scale.x) * widthScale * facing, scale.y * heightScale, scale.z);
    }

    static void ScaleCollider(BoxCollider box, float widthScale, float heightScale, bool groundDecal)
    {
        if (box == null)
            return;

        Vector3 size = box.size;
        Vector3 center = box.center;
        if (groundDecal)
        {
            box.size = new Vector3(size.x * widthScale, size.y, size.z * heightScale);
            box.center = new Vector3(center.x * widthScale, center.y, center.z * heightScale);
            return;
        }

        box.size = new Vector3(size.x * widthScale, size.y * heightScale, size.z * widthScale);
        box.center = new Vector3(center.x * widthScale, center.y * heightScale, center.z * widthScale);
    }
}
}
