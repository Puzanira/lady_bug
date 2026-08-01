using UnityEngine;

namespace LadyBug
{

// Baked into shoulder/grass decal prefabs — visible size after bottom
// transparent padding is cropped via UV trim in SceneSetup.
public class GroundDecorInfo : MonoBehaviour
{
    public float visibleHeight;
    public float visibleHalfWidth;
}
}
