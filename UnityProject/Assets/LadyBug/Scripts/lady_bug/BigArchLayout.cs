using UnityEngine;

namespace LadyBug
{


// Scales a spawned BigArch to match the current road lane count — the prefab
// is baked at one reference width (SceneSetup's LaneCount at build time).
public class BigArchLayout : MonoBehaviour
{
    [SerializeField] private float referenceSpanWidth = 8f;

    public void ApplySpan(int laneCount) => ApplySpan(RoadLayout.BigArchSpanWidth(laneCount));

    public void ApplySpan(float spanWidth)
    {
        if (referenceSpanWidth <= 0f)
            referenceSpanWidth = 8f;

        Transform sprite = transform.Find("Sprite");
        float heightRatio = 0.39f;
        if (sprite != null)
        {
            Vector3 spriteScale = sprite.localScale;
            if (spriteScale.x > 0f)
                heightRatio = spriteScale.y / spriteScale.x;
            sprite.localScale = new Vector3(spanWidth, spanWidth * heightRatio, spriteScale.z);
            sprite.localPosition = new Vector3(0f, sprite.localScale.y / 2f, 0f);
        }

        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Vector3 size = box.size;
            size.x = spanWidth;
            size.y = spanWidth * heightRatio;
            box.size = size;
            box.center = new Vector3(0f, size.y / 2f, box.center.z);
        }

        Transform shadow = transform.Find("Shadow");
        if (shadow != null)
        {
            Vector3 shadowScale = shadow.localScale;
            shadowScale.x = spanWidth;
            shadow.localScale = shadowScale;
        }
    }
}
}
