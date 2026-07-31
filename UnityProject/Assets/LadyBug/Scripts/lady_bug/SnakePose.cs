using UnityEngine;

// Swaps the snake's sprite between a rearing "cobra" pose while idle and a
// slithering zigzag pose while crossing lanes (LaneWalker.IsMoving) — the
// one bad object where a genuine pose swap made more sense than the
// wiggle-only animation every other wandering creature uses (see
// LaneWalker's own comment): the two states actually look different, not
// just more or less animated. Rescales the sprite quad's width to match
// each texture's own aspect ratio so swapping poses doesn't stretch/squash
// the art — height stays fixed since that's what the collider was sized from.
public class SnakePose : MonoBehaviour
{
    [SerializeField] private LaneWalker walker;
    [SerializeField] private Renderer spriteRenderer;
    [SerializeField] private Texture2D idleTexture;
    [SerializeField] private Texture2D movingTexture;
    [SerializeField] private float height = 1f;

    private bool _lastMoving;
    private bool _initialized;

    private void Update()
    {
        if (walker == null || spriteRenderer == null)
            return;

        bool moving = walker.IsMoving;
        if (_initialized && moving == _lastMoving)
            return;

        Texture2D tex = moving ? movingTexture : idleTexture;
        if (tex == null)
            return;

        spriteRenderer.material.mainTexture = tex;

        float aspect = (float)tex.width / tex.height;
        Transform spriteTransform = spriteRenderer.transform;
        Vector3 scale = spriteTransform.localScale;
        // Preserve whichever way LaneWalker currently has this sprite
        // facing (its own scale.x sign, flipped to match the direction of
        // travel — see LaneWalker.TryStartMove) — only the magnitude needs
        // updating here for the new texture's aspect ratio, overwriting the
        // sign back to always-positive would un-flip the snake mid-turn.
        float sign = scale.x < 0f ? -1f : 1f;
        scale.x = height * aspect * sign;
        scale.y = height;
        spriteTransform.localScale = scale;

        _lastMoving = moving;
        _initialized = true;
    }
}
