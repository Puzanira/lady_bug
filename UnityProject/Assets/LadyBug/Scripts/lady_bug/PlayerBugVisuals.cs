using UnityEngine;

namespace LadyBug
{


// P1 (PlayerLeft) always light LadyBug1 + white.
// P2 (PlayerRight, 2P only) dark LadyBug2 — the other art set.
public static class PlayerBugVisuals
{
    private const string LightBugName = "LadyBug1";
    private const string DarkBugName = "LadyBug2";
    // LadyBug1/2 read similarly at full white; P2 is darkened so 2P stays readable.
    public static readonly Color PlayerTwoDarkTint = new Color(0.52f, 0.52f, 0.56f);

    private struct BugLook
    {
        public Texture2D[] GroundFrames;
        public Texture2D[] AirFrames;
        public Color Tint;
    }

    private static BugLook _lightBug;
    private static BugLook _darkBug;
    private static bool _looksReady;

    public static void ApplyForPlayerCount(int playerCount, GameObject playerLeft, GameObject playerRight)
    {
        EnsureLooks(playerLeft, playerRight);

        if (playerLeft != null)
            ApplyLook(playerLeft, WithTint(_lightBug, Color.white));

        if (playerRight != null && playerCount == 2)
            ApplyLook(playerRight, WithTint(_darkBug, PlayerTwoDarkTint));
    }

    public static Texture2D FindBugTexturePublic(string textureName) => FindTexture(textureName);

    public static bool TryGetBugTextures(string baseName, out Texture2D normal, out Texture2D air1, out Texture2D air2)
    {
        EnsureLooks(null, null);
        BugLook look = baseName.Contains(DarkBugName) ? _darkBug : _lightBug;
        if (!HasFrames(look))
            look = LoadBugLook(baseName);

        normal = look.GroundFrames != null && look.GroundFrames.Length > 0 ? look.GroundFrames[0] : null;
        air1 = look.AirFrames != null && look.AirFrames.Length > 0 ? look.AirFrames[0] : null;
        air2 = look.AirFrames != null && look.AirFrames.Length > 1 ? look.AirFrames[1] : null;
        return normal != null;
    }

    private static void EnsureLooks(GameObject playerLeft, GameObject playerRight)
    {
        if (_looksReady)
            return;

        if (playerLeft == null)
            playerLeft = GameObject.Find("PlayerLeft");
        if (playerRight == null)
            playerRight = GameObject.Find("PlayerRight");

        // Always bind by player slot, never infer from whichever bug happens
        // to be on the left/right animator in the scene.
        _lightBug = ResolveLook(LightBugName, playerLeft);
        _darkBug = ResolveLook(DarkBugName, playerRight);

        _looksReady = HasFrames(_lightBug) && HasFrames(_darkBug);
    }

    private static BugLook ResolveLook(string baseName, GameObject player)
    {
        if (player != null)
        {
            PlayerAnimator animator = player.GetComponent<PlayerAnimator>();
            BugLook fromAnimator = LookFromAnimator(animator);
            if (HasFrames(fromAnimator) && FrameSetMatches(fromAnimator, baseName))
                return fromAnimator;
        }

        return LoadBugLook(baseName);
    }

    private static bool FrameSetMatches(BugLook look, string baseName)
    {
        return look.GroundFrames != null && look.GroundFrames.Length > 0
            && look.GroundFrames[0].name.Contains(baseName);
    }

    private static BugLook LoadBugLook(string baseName)
    {
        Texture2D frame1 = FindTexture(baseName);
        Texture2D frame2 = FindTexture(baseName + "Frame2");
        Texture2D frame3 = FindTexture(baseName + "Frame3");
        Texture2D frame4 = FindTexture(baseName + "Frame4");
        Texture2D air1 = FindTexture(baseName + "Air1");
        Texture2D air2 = FindTexture(baseName + "Air2");

        var ground = new[] { frame1, frame3, frame2, frame4 };
        int groundCount = 0;
        for (int i = 0; i < ground.Length; i++)
        {
            if (ground[i] != null)
                groundCount++;
        }

        Texture2D[] groundFrames = groundCount > 0 ? new Texture2D[groundCount] : null;
        if (groundFrames != null)
        {
            int j = 0;
            for (int i = 0; i < ground.Length; i++)
            {
                if (ground[i] == null)
                    continue;
                groundFrames[j++] = ground[i];
            }
        }

        Texture2D[] airFrames = null;
        if (air1 != null || air2 != null)
            airFrames = new[] { air1, air2 };

        return new BugLook { GroundFrames = groundFrames, AirFrames = airFrames };
    }

    private static Texture2D FindTexture(string textureName)
    {
        Texture2D[] all = Resources.FindObjectsOfTypeAll<Texture2D>();
        for (int i = 0; i < all.Length; i++)
        {
            Texture2D candidate = all[i];
            if (candidate == null || candidate.name != textureName)
                continue;
            if (candidate.hideFlags != HideFlags.None)
                continue;
            return candidate;
        }

        return null;
    }

    private static bool HasFrames(BugLook look)
    {
        return look.GroundFrames != null && look.GroundFrames.Length > 0;
    }

    private static BugLook LookFromAnimator(PlayerAnimator animator)
    {
        if (animator == null)
            return default;

        return new BugLook
        {
            GroundFrames = animator.GroundFrames,
            AirFrames = animator.AirFrames,
        };
    }

    private static BugLook WithTint(BugLook look, Color tint)
    {
        look.Tint = tint;
        return look;
    }

    private static void ApplyLook(GameObject player, BugLook look)
    {
        if (!HasFrames(look))
            return;

        PlayerAnimator animator = player.GetComponent<PlayerAnimator>();
        if (animator != null)
            animator.SetBugFrames(look.GroundFrames, look.AirFrames);

        Transform sprite = player.transform.Find("Sprite");
        if (sprite == null)
            return;

        Renderer renderer = sprite.GetComponent<Renderer>();
        if (renderer == null)
            return;

        Material mat = renderer.material;
        mat.mainTexture = look.GroundFrames[0];
        mat.color = look.Tint;
    }
}
}
