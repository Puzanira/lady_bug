using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SceneSetup
{
    const int LaneCount = 3;
    const float LaneWidth = 4f;
    const float RoadLength = 150f;
    const float RoadCenterZ = 1f;
    const float DashPeriod = 4f;
    const float ScrollSpeed = 10f;
    const float RoadTextureTileSize = 1.5f; // world units per asphalt-texture tile — must match CreateRoadTexture's mainTextureScale divisor

    [MenuItem("Tools/Rebuild Scene")]
    public static void BuildScene()
    {
        // EditorSceneManager.NewScene throws InvalidOperationException in
        // Play Mode (has to use SceneManager.CreateScene there instead) —
        // stop the run first so Rebuild Scene fails with a clear message
        // instead of a cryptic engine exception.
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Rebuild Scene: остановите Play Mode перед пересборкой сцены.");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateLight();
        CreateSpeedController();
        CreateGestureSensorSerial();
        CreateJoystickSerial();
        CreateAchievementStats();
        CreatePlayerPhotoCapture();

        // Two players sharing one road/score/speed — co-op, not split-screen.
        // Right player: arrows for lane/jump/duck. Left player: WASD for
        // lane/jump/duck. No accel/brake keys anymore — the road always
        // accelerates on its own (SpeedController) and braking was removed
        // from every control scheme entirely.
        // Each also gets a gesture-sensor simulator (disabled by default): 2
        // distance sensors per player (left hand, right hand), 2 keys each
        // (up/down) — the up key doubles as the flap key (rapid taps =
        // jump), matching the on-screen square grid in CreateGesturePanel:
        // Right player:  U O       Left player:   Q E
        //                J L                       A D
        GameObject playerRight = CreatePlayer("PlayerRight", KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow,
            KeyCode.U, KeyCode.J, KeyCode.O, KeyCode.L, LaneCount - 1, Color.white, "LadyBug1.png");
        GameObject playerLeft = CreatePlayer("PlayerLeft", KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.S,
            KeyCode.Q, KeyCode.A, KeyCode.E, KeyCode.D, 0, new Color(0.55f, 0.75f, 1f), "LadyBug2.png");

        CreateCamera(playerRight.transform);
        CreateRoad();
        CreateSideGround();
        CreateSpawner();
        CreateBigArchSpawner();
        CreateSideScenery();
        CreateSky();
        CreateAudio();
        CreateHelpScreen();
        RectTransform lastStackedPanel = CreateScoreUI(out Canvas scoreCanvas, out GameObject topScoresPanel);
        CreateWinSequence(scoreCanvas);
        CreateTricksUI(lastStackedPanel);
        (GameObject gestureCanvasLeft, GameObject gestureCanvasRight) = CreateGestureIndicators(playerRight, playerLeft);
        CreateStartScreen(playerRight, playerLeft, gestureCanvasLeft, gestureCanvasRight, topScoresPanel);
        CreatePauseDialog();
        CreateExitGesture();
        CreateIntroScreen();
        CreateScreenInfoLabel();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        string scenePath = "Assets/Scenes/Main.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

        Debug.Log("Scene setup complete: " + scenePath);
    }

    static void CreateSpeedController()
    {
        var go = new GameObject("GameManager");
        SpeedController speed = go.AddComponent<SpeedController>();

        SerializedObject so = new SerializedObject(speed);
        so.FindProperty("minSpeed").floatValue = 3f;
        so.FindProperty("maxSpeed").floatValue = 200f;
        so.FindProperty("baseAccel").floatValue = 6f;
        so.FindProperty("accelFalloffSpeed").floatValue = 15f;
        so.FindProperty("gearStepKmh").floatValue = 10f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Single shared reader for the 6-channel gesture-sensor board (both
    // players read out of it — see GestureSensorSerial and
    // ArduinoFirmware/GestureSensors). Harmless if no board is plugged in:
    // it just keeps retrying in the background and IsConnected stays false.
    static void CreateGestureSensorSerial()
    {
        var go = new GameObject("GestureSensorSerial");
        go.AddComponent<GestureSensorSerial>();
    }

    // Player 2's joystick board — separate hardware from the gesture-sensor
    // board above (see JoystickSerial and ArduinoFirmware/Joystick).
    // Harmless if no board is plugged in, same as GestureSensorSerial.
    static void CreateJoystickSerial()
    {
        var go = new GameObject("JoystickSerial");
        go.AddComponent<JoystickSerial>();
    }

    // Per-category collect/hit/trick counters, read once at the end by
    // WinSequence's achievements screen — see AchievementStats.cs.
    static void CreateAchievementStats()
    {
        var go = new GameObject("AchievementStats");
        go.AddComponent<AchievementStats>();
    }

    // Full-screen "smile, the camera is taking your picture" overlay used
    // when a run lands in a leaderboard's top 10 — see PlayerPhotoCapture.cs.
    static void CreatePlayerPhotoCapture()
    {
        var canvasGo = new GameObject("PhotoCaptureCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 210; // above everything, including the pause dialog (200)

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var backdropGo = new GameObject("Backdrop");
        backdropGo.transform.SetParent(canvasGo.transform, false);
        Image backdrop = backdropGo.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.8f);
        RectTransform backdropRt = backdropGo.GetComponent<RectTransform>();
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;

        var messageGo = new GameObject("Message");
        messageGo.transform.SetParent(canvasGo.transform, false);
        Text message = messageGo.AddComponent<Text>();
        message.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        message.fontSize = 56;
        message.fontStyle = FontStyle.Bold;
        message.alignment = TextAnchor.MiddleCenter;
        message.color = new Color(1f, 0.85f, 0.2f);
        messageGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform messageRt = message.GetComponent<RectTransform>();
        messageRt.anchorMin = new Vector2(0.5f, 0.5f);
        messageRt.anchorMax = new Vector2(0.5f, 0.5f);
        messageRt.pivot = new Vector2(0.5f, 0.5f);
        // Taller box (was 140) and pushed further up (was y=340) — the
        // 2-line "НОВЫЙ РЕКОРД!\n<category>" case (PlayerPhotoCapture's own
        // message string) at fontSize 56 nearly filled the old box's full
        // height, leaving the bottom line's glyphs right at its edge with
        // almost no real gap before SmileText below it. Moved up rather
        // than pushing SmileText down, so as not to crowd CameraPreview's
        // own top edge underneath it any further than before.
        messageRt.sizeDelta = new Vector2(1400f, 170f);
        messageRt.anchoredPosition = new Vector2(0f, 390f);

        var smileGo = new GameObject("SmileText");
        smileGo.transform.SetParent(canvasGo.transform, false);
        Text smile = smileGo.AddComponent<Text>();
        smile.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        smile.fontSize = 30;
        smile.fontStyle = FontStyle.Bold;
        smile.alignment = TextAnchor.MiddleCenter;
        smile.color = Color.white;
        smile.text = "УЛЫБНИТЕСЬ — ВАС СНИМАЕТ КАМЕРА ДЛЯ ИСТОРИИ";
        smileGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform smileRt = smile.GetComponent<RectTransform>();
        smileRt.anchorMin = new Vector2(0.5f, 0.5f);
        smileRt.anchorMax = new Vector2(0.5f, 0.5f);
        smileRt.pivot = new Vector2(0.5f, 0.5f);
        smileRt.sizeDelta = new Vector2(1200f, 50f);
        smileRt.anchoredPosition = new Vector2(0f, 250f); // left as-is — message (below) pushed further up instead, so this doesn't crowd CameraPreview's own top edge any more than before

        var previewGo = new GameObject("CameraPreview");
        previewGo.transform.SetParent(canvasGo.transform, false);
        RawImage preview = previewGo.AddComponent<RawImage>();
        preview.color = Color.white;
        RectTransform previewRt = preview.GetComponent<RectTransform>();
        previewRt.anchorMin = new Vector2(0.5f, 0.5f);
        previewRt.anchorMax = new Vector2(0.5f, 0.5f);
        previewRt.pivot = new Vector2(0.5f, 0.5f);
        previewRt.sizeDelta = new Vector2(800f, 600f);
        previewRt.anchoredPosition = new Vector2(0f, -60f);

        var countdownGo = new GameObject("Countdown");
        countdownGo.transform.SetParent(canvasGo.transform, false);
        Text countdown = countdownGo.AddComponent<Text>();
        countdown.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        countdown.fontSize = 180;
        countdown.fontStyle = FontStyle.Bold;
        countdown.alignment = TextAnchor.MiddleCenter;
        countdown.color = new Color(1f, 1f, 1f, 0.85f);
        Outline countdownOutline = countdownGo.AddComponent<Outline>();
        countdownOutline.effectColor = Color.black;
        countdownOutline.effectDistance = new Vector2(4f, -4f);
        RectTransform countdownRt = countdown.GetComponent<RectTransform>();
        countdownRt.anchorMin = new Vector2(0.5f, 0.5f);
        countdownRt.anchorMax = new Vector2(0.5f, 0.5f);
        countdownRt.pivot = new Vector2(0.5f, 0.5f);
        countdownRt.sizeDelta = new Vector2(400f, 400f);
        countdownRt.anchoredPosition = new Vector2(0f, -60f);

        canvasGo.SetActive(false);

        var controllerGo = new GameObject("PlayerPhotoCapture");
        PlayerPhotoCapture capture = controllerGo.AddComponent<PlayerPhotoCapture>();
        SerializedObject so = new SerializedObject(capture);
        so.FindProperty("overlayRoot").objectReferenceValue = canvasGo;
        so.FindProperty("messageText").objectReferenceValue = message;
        so.FindProperty("smileText").objectReferenceValue = smile;
        so.FindProperty("cameraPreview").objectReferenceValue = preview;
        so.FindProperty("countdownText").objectReferenceValue = countdown;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CreateLight()
    {
        var lightGo = new GameObject("Directional Light");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    static GameObject CreatePlayer(string name, KeyCode left, KeyCode right, KeyCode up, KeyCode down,
        KeyCode leftHandUp, KeyCode leftHandDown, KeyCode rightHandUp, KeyCode rightHandDown,
        int startLane, Color tint, string spriteFile)
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Cube);
        player.name = name;

        // Spawn already in the assigned lane — no startup slide-in.
        float startX = (startLane - (LaneCount - 1) / 2f) * LaneWidth;
        player.transform.position = new Vector3(startX, 0.5f, 0f);
        ApplyColor(player, new Color(0.85f, 0.1f, 0.1f));
        player.GetComponent<Renderer>().enabled = false; // cube stays as collider/anchor only
        PlayerController controller = player.AddComponent<PlayerController>();

        // Keep the player's lane spacing in sync with the visual road width —
        // these used to coincidentally match at LaneWidth=3, but must be
        // driven from the same constant now that it can change.
        SerializedObject playerSo = new SerializedObject(controller);
        playerSo.FindProperty("laneCount").intValue = LaneCount;
        playerSo.FindProperty("laneWidth").floatValue = LaneWidth;
        playerSo.FindProperty("startLane").intValue = startLane;
        playerSo.FindProperty("leftKey").intValue = (int)left;
        playerSo.FindProperty("rightKey").intValue = (int)right;
        playerSo.FindProperty("upKey").intValue = (int)up;
        playerSo.FindProperty("downKey").intValue = (int)down;
        playerSo.ApplyModifiedPropertiesWithoutUndo();

        // Gesture (distance-sensor) input — keyboard-simulated for now, since
        // no hardware is connected. Disabled by default; the start screen
        // enables it only if the sensor simulator is the chosen controller.
        GestureInput gesture = player.AddComponent<GestureInput>();
        SerializedObject gestureSo = new SerializedObject(gesture);
        gestureSo.FindProperty("leftHandUpKey").intValue = (int)leftHandUp;
        gestureSo.FindProperty("leftHandDownKey").intValue = (int)leftHandDown;
        gestureSo.FindProperty("rightHandUpKey").intValue = (int)rightHandUp;
        gestureSo.FindProperty("rightHandDownKey").intValue = (int)rightHandDown;
        gestureSo.ApplyModifiedPropertiesWithoutUndo();
        gesture.enabled = false;

        // Joystick input — only ever driven for player 2 (left), see
        // StartScreenController's "Датчики" handling, but added uniformly to
        // both players like GestureInput above for the same reason: keeps
        // this shared constructor simple, and an unused disabled component
        // on player 1 costs nothing.
        JoystickInput joystick = player.AddComponent<JoystickInput>();
        joystick.enabled = false;

        // Trigger detection against the spawned entities' colliders needs a
        // Rigidbody on at least one side of the pair; the player moves via
        // transform, not physics, so it stays kinematic with gravity off.
        Rigidbody rb = player.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        Transform sprite = CreatePlayerSprite(player.transform, tint, spriteFile);
        CreatePlayerShadow(player.transform);
        CreatePlayerMovementSfx(player, controller);

        if (sprite != null)
        {
            PlayerAnimator animator = player.AddComponent<PlayerAnimator>();
            SerializedObject animatorSo = new SerializedObject(animator);
            animatorSo.FindProperty("player").objectReferenceValue = controller;
            animatorSo.FindProperty("sprite").objectReferenceValue = sprite;

            // Frames 2-4 are edited variants of frame1 (leg pose only, same
            // filename + "FrameN" suffix) — see PlayerAnimator's comment for
            // why they're edits of frame1 rather than independent generations.
            // Cycle order alternates a "tucked" pose with a "wide" one
            // instead of the raw generation order, so it reads as an actual
            // stride rather than 2 poses each held twice as long.
            Texture2D frame1Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + spriteFile);
            Texture2D frame2Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + spriteFile.Replace(".png", "Frame2.png"));
            Texture2D frame3Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + spriteFile.Replace(".png", "Frame3.png"));
            Texture2D frame4Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + spriteFile.Replace(".png", "Frame4.png"));

            var groundFrames = new System.Collections.Generic.List<Texture2D> { frame1Tex, frame3Tex, frame2Tex, frame4Tex };
            groundFrames.RemoveAll(t => t == null);

            SerializedProperty framesProp = animatorSo.FindProperty("groundFrames");
            framesProp.arraySize = groundFrames.Count;
            for (int i = 0; i < groundFrames.Count; i++)
                framesProp.GetArrayElementAtIndex(i).objectReferenceValue = groundFrames[i];

            // Airborne cycle — wings-open frames, same "FrameN" edit-of-frame1
            // convention but under an "AirN" suffix instead.
            Texture2D air1Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + spriteFile.Replace(".png", "Air1.png"));
            Texture2D air2Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + spriteFile.Replace(".png", "Air2.png"));

            var airFrames = new System.Collections.Generic.List<Texture2D> { air1Tex, air2Tex };
            airFrames.RemoveAll(t => t == null);

            SerializedProperty airFramesProp = animatorSo.FindProperty("airFrames");
            airFramesProp.arraySize = airFrames.Count;
            for (int i = 0; i < airFrames.Count; i++)
                airFramesProp.GetArrayElementAtIndex(i).objectReferenceValue = airFrames[i];

            animatorSo.ApplyModifiedPropertiesWithoutUndo();
        }

        return player;
    }

    // Tracks the player's X/Z every frame so it's always directly beneath
    // them on the road, regardless of jump/duck/bounce height — makes it
    // much easier to judge when to jump. Not baked into a prefab (players
    // live directly in the scene), so an in-memory Material is fine here.
    static void CreatePlayerShadow(Transform target)
    {
        GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shadow.name = "Shadow";
        Object.DestroyImmediate(shadow.GetComponent<Collider>());
        // Parented so it's deactivated along with the player in 1-player
        // mode — GroundShadow still overrides world position every frame,
        // so the parenting doesn't affect its tracking.
        shadow.transform.SetParent(target, false);
        shadow.transform.localScale = new Vector3(1.1f, 0.01f, 0.75f);
        shadow.transform.position = new Vector3(target.position.x, 0.02f, target.position.z);

        Renderer renderer = shadow.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { color = new Color(0f, 0f, 0f, 0.4f) };
        renderer.sharedMaterial = material;

        GroundShadow follower = shadow.AddComponent<GroundShadow>();
        SerializedObject so = new SerializedObject(follower);
        so.FindProperty("target").objectReferenceValue = target;
        so.FindProperty("groundY").floatValue = 0.02f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Transform CreatePlayerSprite(Transform parent, Color tint, string spriteFile)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + spriteFile);
        if (tex == null)
        {
            Debug.LogWarning("LadyBug sprite not found at Assets/Sprites/" + spriteFile);
            return null;
        }

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = "Sprite";
        Object.DestroyImmediate(sprite.GetComponent<Collider>());
        sprite.transform.SetParent(parent);

        float aspect = (float)tex.width / tex.height;
        float height = 1.3f;
        sprite.transform.localScale = new Vector3(height * aspect, height, 1f);
        sprite.transform.localPosition = new Vector3(0f, 0.15f, -0.51f);

        Renderer renderer = sprite.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { mainTexture = tex, color = tint };
        renderer.sharedMaterial = material;

        return sprite.transform;
    }

    static void CreateCamera(Transform lookTarget)
    {
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 60f;
        cam.farClipPlane = 1500f;
        camGo.transform.position = new Vector3(0f, 4f, -8f);
        camGo.transform.LookAt(new Vector3(0f, 0.5f, 20f));
        camGo.AddComponent<AudioListener>();
    }

    static void CreateRoad()
    {
        float roadWidth = LaneCount * LaneWidth;

        // Static road surface — starts a bit behind the player and stretches
        // far enough that it visually fades into the horizon instead of
        // showing a hard edge.
        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
        surface.name = "RoadSurface";
        surface.transform.position = new Vector3(0f, -0.05f, RoadCenterZ);
        surface.transform.localScale = new Vector3(roadWidth, 0.1f, RoadLength);

        Renderer surfaceRenderer = surface.GetComponent<Renderer>();
        Shader roadShader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material roadMaterial = new Material(roadShader)
        {
            mainTexture = CreateRoadTexture(),
            mainTextureScale = new Vector2(roadWidth / RoadTextureTileSize, RoadLength / RoadTextureTileSize)
        };
        surfaceRenderer.sharedMaterial = roadMaterial;

        // Same scroll trick as the lane dividers below — the road itself
        // never moves, but the texture offset animates so the (slightly
        // uneven) asphalt grain streams toward the camera along with
        // everything else on the road. ScrollingTexture's "dashPeriod" is
        // really "world distance per full UV cycle" — for this texture that
        // is RoadTextureTileSize, NOT the lane-divider's DashPeriod (using
        // that here previously made the asphalt visibly crawl slower than
        // the dashed lines instead of scrolling in lockstep with them).
        ScrollingTexture roadScroller = surface.AddComponent<ScrollingTexture>();
        SerializedObject roadSo = new SerializedObject(roadScroller);
        roadSo.FindProperty("dashPeriod").floatValue = RoadTextureTileSize;
        roadSo.ApplyModifiedPropertiesWithoutUndo();

        // Dashed lane markings scroll toward the camera via texture offset —
        // no moving/recycled geometry, so there is no seam or stutter.
        for (int i = 1; i < LaneCount; i++)
        {
            float dividerX = -roadWidth / 2f + i * LaneWidth;
            CreateDashedDivider(dividerX);
        }
    }

    // Flat ground strips flanking the road on both sides — without these,
    // roadside scenery (CreateSideScenery) sat on bare void with only the
    // skybox behind it and no surface connecting its base down to y=0,
    // which read as "floating in mid-air" rather than standing on ground.
    // Wide enough to cover every spawn position SideScenerySpawner can pick
    // (road edge + sideOffset + widest object's half-width + maxExtraOffset),
    // with margin to spare.
    static void CreateSideGround()
    {
        float roadWidth = LaneCount * LaneWidth;
        const float sideWidth = 140f;
        foreach (float side in new[] { -1f, 1f })
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = side < 0 ? "SideGroundLeft" : "SideGroundRight";
            float x = side * (roadWidth / 2f + sideWidth / 2f);
            ground.transform.position = new Vector3(x, -0.05f, RoadCenterZ);
            ground.transform.localScale = new Vector3(sideWidth, 0.1f, RoadLength);
            Object.DestroyImmediate(ground.GetComponent<Collider>());

            Renderer renderer = ground.GetComponent<Renderer>();
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
            Material material = new Material(shader) { color = new Color(0.55f, 0.72f, 0.35f) };
            renderer.sharedMaterial = material;
        }
    }

    static void CreateDashedDivider(float x)
    {
        GameObject divider = GameObject.CreatePrimitive(PrimitiveType.Plane);
        divider.name = "LaneDivider";
        // Default Plane is 10x10 units; scale to a thin strip covering the road length.
        divider.transform.position = new Vector3(x, 0.02f, RoadCenterZ);
        divider.transform.localScale = new Vector3(0.03f, 1f, RoadLength / 10f);
        Object.DestroyImmediate(divider.GetComponent<MeshCollider>());

        Renderer renderer = divider.GetComponent<Renderer>();
        Shader shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material material = new Material(shader)
        {
            mainTexture = CreateDashTexture(),
            mainTextureScale = new Vector2(1f, RoadLength / DashPeriod)
        };
        renderer.sharedMaterial = material;

        ScrollingTexture scroller = divider.AddComponent<ScrollingTexture>();
        SerializedObject so = new SerializedObject(scroller);
        so.FindProperty("dashPeriod").floatValue = DashPeriod;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // (name, texture file, on-road height, score: +1 good pickup / -1 bad obstacle)
    static readonly (string, string, float, int)[] LaneObjects =
    {
        ("Flower", "Flower.png", 0.8f, 1),
        ("Heart", "Heart.png", 1.2f, 1),
        ("Cherry", "Cherry.png", 1.2f, 1),
        ("FlowerPink", "FlowerPink.png", 0.8f, 1),
        ("FlowerYellow", "FlowerYellow.png", 0.8f, 1),
        ("DaisyWhite", "DaisyWhite.png", 0.8f, 1),
        ("DaisyPink", "DaisyPink.png", 0.8f, 1),
        ("SunflowerYellow", "SunflowerYellow.png", 0.8f, 1),
        ("LotusYellow", "LotusYellow.png", 0.8f, 1),
        ("LotusBlue", "LotusBlue.png", 0.8f, 1),
        ("LotusPink", "LotusPink.png", 0.8f, 1),
        ("Star", "Star.png", 1.1f, 1),
        ("MoneyBag", "MoneyBag.png", 1.45f, 1),
        ("HoneyBarrel", "HoneyBarrel.png", 1.3f, 1),
        ("Candy", "Candy.png", 1.1f, 1),
        ("TrafficCone", "TrafficCone.png", 1.1f, -1),
        ("Wheel", "Wheel.png", 1f, -1),
        ("Bicycle", "Bicycle.png", 1.5f, -1),
        ("Motorbike", "Motorbike.png", 1.6f, -1),
        ("Motorcycle", "Motorcycle.png", 1.6f, -1),
        ("Dog", "Dog.png", 1.5f, -1),
        ("Cat", "Cat.png", 1.2f, -1),
        ("Rabbit", "Rabbit.png", 1.45f, -1),
        ("Crow", "Crow.png", 1.1f, -1),
        ("SandPile", "SandPile.png", 1.3f, -1),
        ("BrickPile", "BrickPile.png", 1.3f, -1),
        ("WoodPile", "WoodPile.png", 1.2f, -1),
        ("RockPile", "RockPile.png", 1.4f, -1),
    };

    // Living creatures among LaneObjects that drift sideways between lanes (LaneWalker).
    static readonly string[] WanderingAnimals = { "Dog", "Cat", "Crow", "Rabbit" };

    // Piles are meant to block the whole lane, not just sit in a corner of
    // it — stretched wider than their (roughly square) art would give on
    // its own, via CreateEntityPrefab's width override. Lane is LaneWidth
    // (4) wide; a bit past that so it can't be dodged by hugging the edge.
    static readonly System.Collections.Generic.Dictionary<string, float> LaneObjectWidthOverrides =
        new System.Collections.Generic.Dictionary<string, float>
    {
        { "SandPile", 4.3f },
        { "BrickPile", 4.3f },
        { "WoodPile", 4.3f },
        { "RockPile", 4.3f },
        { "HoneyBarrel", 1.7f }, // wider than its own aspect ratio gives — a stubbier, "thicker" barrel
        { "MoneyBag", 2.1f }, // much wider than its own aspect ratio — a fat, bulging sack
    };

    // The trigger box normally covers the sprite's full drawn height, which
    // for these three made a clean jump-over impossible: the player only
    // rises jumpHeightDelta (1.4) above the road, well under their tall
    // full-height art (handlebars/raised cobra head). Shrunk here to just
    // the ground-level "solid" part a jump should actually need to clear —
    // the sprite itself is untouched, only the invisible box is shorter.
    static readonly System.Collections.Generic.Dictionary<string, float> LaneObjectColliderHeightOverrides =
        new System.Collections.Generic.Dictionary<string, float>
    {
        { "Bicycle", 1.0f },
        { "Motorbike", 1.0f },
        { "Motorcycle", 1.0f },
        { "Dog", 1.0f },
        { "Rabbit", 1.0f },
    };

    // (name, texture file, roadside height)
    static readonly (string, string, float)[] SceneryObjects =
    {
        ("PalmTree", "PalmTree.png", 3.5f),
        ("Cactus", "Cactus.png", 1.8f),
        ("BigCactus", "BigCactus.png", 2f),
        ("PineForest", "PineForest.png", 4f),
        ("Mountain", "Mountain.png", 5f),
        ("GreenHill", "GreenHill.png", 3.5f),
        ("CactusFlowerOrange", "CactusFlowerOrange.png", 1.7f),
    };

    // Flat ground-level decals (lie flat on the road, same "Quad rotated 90°
    // on X" treatment) — (name, texture file, world size). Puddles used to be
    // plain tinted discs in 4 colors; now real images, just water (blue) and
    // an oil spill (dark) — the other 2 colors didn't mean anything specific.
    static readonly (string, string, float)[] GroundDecals =
    {
        ("Pothole", "Pothole.png", 2.2f),
        ("PuddleBlue", "PuddleBlue.png", 1.8f),
        ("PuddleDark", "PuddleDark.png", 1.8f),
    };

    static void CreateSpawner()
    {
        System.IO.Directory.CreateDirectory("Assets/Prefabs");

        // Three separate pools so the spawner can pick in two steps:
        // good vs. bad, then — only for bad — jump-over vs. duck-under.
        var goodPrefabs = new System.Collections.Generic.List<GameObject>();
        var badJumpPrefabs = new System.Collections.Generic.List<GameObject>();

        foreach (var (name, file, height, score) in LaneObjects)
        {
            bool canWander = System.Array.IndexOf(WanderingAnimals, name) >= 0;
            float? widthOverride = LaneObjectWidthOverrides.TryGetValue(name, out float overrideWidth) ? overrideWidth : (float?)null;
            float? colliderHeightOverride = LaneObjectColliderHeightOverrides.TryGetValue(name, out float overrideColliderHeight) ? overrideColliderHeight : (float?)null;
            GameObject prefab = CreateEntityPrefab(name, "Assets/Sprites/" + file, height, "Assets/Prefabs/" + name + ".prefab", score, canWander, widthOverride, colliderHeightOverride);
            if (prefab == null)
                continue;
            (score > 0 ? goodPrefabs : badJumpPrefabs).Add(prefab);
        }

        foreach (var (name, file, size) in GroundDecals)
        {
            GameObject decal = CreateGroundDecalPrefab(name, file, size);
            if (decal != null)
                badJumpPrefabs.Add(decal);
        }

        GameObject snake = CreateSnakePrefab();
        if (snake != null)
            badJumpPrefabs.Add(snake);

        var badDuckPrefabs = new System.Collections.Generic.List<GameObject>();
        GameObject arch = CreateArchPrefab();
        if (arch != null)
            badDuckPrefabs.Add(arch);

        var spawnerGo = new GameObject("Spawner");
        EntitySpawner spawner = spawnerGo.AddComponent<EntitySpawner>();

        SerializedObject so = new SerializedObject(spawner);
        SetPrefabArray(so, "goodPrefabs", goodPrefabs);
        SetPrefabArray(so, "badJumpPrefabs", badJumpPrefabs);
        SetPrefabArray(so, "badDuckPrefabs", badDuckPrefabs);
        so.FindProperty("laneCount").intValue = LaneCount;
        so.FindProperty("laneWidth").floatValue = LaneWidth;
        so.FindProperty("spawnZ").floatValue = RoadCenterZ + RoadLength / 2f - 5f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetPrefabArray(SerializedObject so, string propertyName, System.Collections.Generic.List<GameObject> list)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        prop.arraySize = list.Count;
        for (int i = 0; i < list.Count; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
    }

    static void CreateSideScenery()
    {
        System.IO.Directory.CreateDirectory("Assets/Prefabs");

        var prefabs = new System.Collections.Generic.List<GameObject>();
        foreach (var (name, file, height) in SceneryObjects)
        {
            GameObject prefab = CreateEntityPrefab(name, "Assets/Sprites/" + file, height, "Assets/Prefabs/" + name + ".prefab");
            if (prefab != null)
                prefabs.Add(prefab);
        }

        var spawnerGo = new GameObject("SceneSpawner");
        SideScenerySpawner spawner = spawnerGo.AddComponent<SideScenerySpawner>();

        SerializedObject so = new SerializedObject(spawner);
        SetPrefabArray(so, "prefabs", prefabs);
        so.FindProperty("sideOffset").floatValue = LaneCount * LaneWidth / 2f + 2f;
        so.FindProperty("spawnZ").floatValue = RoadCenterZ + RoadLength / 2f - 5f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // (name, texture file, world height) — two shape variants for a bit of
    // visual variety as they drift by overhead.
    static readonly (string, string, float)[] CloudSprites =
    {
        ("CloudWhite1", "CloudWhite1.png", 6f),
        ("CloudWhite2", "CloudWhite2.png", 5f),
    };

    // Sun sweeps back and forth along a dome-shaped arc (SunArc) instead of
    // sitting fixed — these are the arc's center/radii, in world units.
    // center.z is deliberately farther out than CloudSpawner's spawnZ (90)
    // so the sun's depth is always greater than any cloud's — with the
    // Cutout shader's normal depth testing, that guarantees it renders
    // behind every cloud, never in front. radiusY/center.y are tuned so the
    // arc peaks near the top of the screen at its centre and dips down to
    // roughly the horizon at its two ends (see the per-point FOV projection
    // math worked out for this camera — not just eyeballed).
    const float SunHeight = 16f;
    static readonly Vector3 SunArcCenter = new Vector3(0f, 5f, 120f);
    const float SunArcRadiusX = 100f;
    const float SunArcRadiusY = 44f;

    static void CreateSky()
    {
        System.IO.Directory.CreateDirectory("Assets/Prefabs");

        var prefabs = new System.Collections.Generic.List<GameObject>();
        foreach (var (name, file, height) in CloudSprites)
        {
            GameObject prefab = CreateCloudPrefab(name, "Assets/Sprites/" + file, height);
            if (prefab != null)
                prefabs.Add(prefab);
        }

        var spawnerGo = new GameObject("CloudSpawner");
        CloudSpawner spawner = spawnerGo.AddComponent<CloudSpawner>();
        SerializedObject so = new SerializedObject(spawner);
        SetPrefabArray(so, "prefabs", prefabs);
        so.ApplyModifiedPropertiesWithoutUndo();

        CreateSunSprite();
    }

    static GameObject CreateCloudPrefab(string name, string texturePath, float height)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (tex == null)
        {
            Debug.LogWarning("Texture not found: " + texturePath);
            return null;
        }

        var root = new GameObject(name);
        root.AddComponent<CloudDrift>();

        float aspect = (float)tex.width / tex.height;

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = "Sprite";
        Object.DestroyImmediate(sprite.GetComponent<Collider>());
        sprite.transform.SetParent(root.transform);
        sprite.transform.localScale = new Vector3(height * aspect, height, 1f);
        sprite.transform.localPosition = Vector3.zero;

        Renderer renderer = sprite.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { mainTexture = tex };

        System.IO.Directory.CreateDirectory("Assets/Materials");
        string materialPath = "Assets/Materials/" + name + ".mat";
        AssetDatabase.DeleteAsset(materialPath); // safe to rerun Rebuild Scene
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;

        string savePath = "Assets/Prefabs/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void CreateSunSprite()
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Sun.png");
        if (tex == null)
        {
            Debug.LogWarning("Texture not found: Assets/Sprites/Sun.png");
            return;
        }

        float aspect = (float)tex.width / tex.height;

        var sun = new GameObject("Sun");
        sun.transform.position = SunArcCenter;

        SunArc arc = sun.AddComponent<SunArc>();
        SerializedObject arcSo = new SerializedObject(arc);
        arcSo.FindProperty("center").vector3Value = SunArcCenter;
        arcSo.FindProperty("radiusX").floatValue = SunArcRadiusX;
        arcSo.FindProperty("radiusY").floatValue = SunArcRadiusY;
        arcSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = "Sprite";
        Object.DestroyImmediate(sprite.GetComponent<Collider>());
        sprite.transform.SetParent(sun.transform);
        sprite.transform.localScale = new Vector3(SunHeight * aspect, SunHeight, 1f);
        sprite.transform.localPosition = Vector3.zero;
        sprite.transform.localRotation = Quaternion.identity;

        Renderer renderer = sprite.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { mainTexture = tex };
        renderer.sharedMaterial = material; // scene object, not a prefab — in-memory material is fine here
    }

    static void CreateAudio()
    {
        var audioGo = new GameObject("Audio");

        AudioSource sfxSource = audioGo.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        SfxManager sfx = audioGo.AddComponent<SfxManager>();
        SerializedObject sfxSo = new SerializedObject(sfx);
        sfxSo.FindProperty("source").objectReferenceValue = sfxSource;
        sfxSo.FindProperty("pickupClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PickupPositive.mp3");
        sfxSo.FindProperty("dogClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BadDog.mp3");
        sfxSo.FindProperty("catClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BadCat.mp3");
        sfxSo.FindProperty("crowClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BadCrow.mp3");
        sfxSo.FindProperty("snakeClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SnakeHiss.mp3");
        sfxSo.FindProperty("hitGenericClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/HitGeneric.mp3");
        sfxSo.FindProperty("trickClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/TrickApplause.mp3");
        sfxSo.ApplyModifiedPropertiesWithoutUndo();

        AudioSource shiftSource = audioGo.AddComponent<AudioSource>();
        shiftSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/GearShift.wav");
        shiftSource.playOnAwake = false;

        AudioSource humSource = audioGo.AddComponent<AudioSource>();
        humSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/EngineHum.mp3");
        humSource.loop = true;
        humSource.playOnAwake = false;

        GearSfx gearSfx = audioGo.AddComponent<GearSfx>();
        SerializedObject gearSo = new SerializedObject(gearSfx);
        gearSo.FindProperty("shiftSource").objectReferenceValue = shiftSource;
        gearSo.FindProperty("humSource").objectReferenceValue = humSource;
        gearSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // Per-player looping movement sound — running feet while grounded, wing
    // buzz while airborne (jumping/flying). Both sources play continuously
    // from the start at volume 0; PlayerMovementSfx just swaps which one is
    // audible each frame, so there's no restart/click when the state flips.
    static void CreatePlayerMovementSfx(GameObject player, PlayerController controller)
    {
        AudioSource feetSource = player.AddComponent<AudioSource>();
        feetSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/RunFeet.mp3");
        feetSource.loop = true;
        feetSource.playOnAwake = true;
        feetSource.volume = 0f;

        AudioSource wingsSource = player.AddComponent<AudioSource>();
        wingsSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Buzz.wav");
        wingsSource.loop = true;
        wingsSource.playOnAwake = true;
        wingsSource.volume = 0f;

        PlayerMovementSfx sfx = player.AddComponent<PlayerMovementSfx>();
        SerializedObject so = new SerializedObject(sfx);
        so.FindProperty("player").objectReferenceValue = controller;
        so.FindProperty("feetSource").objectReferenceValue = feetSource;
        so.FindProperty("wingsSource").objectReferenceValue = wingsSource;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // The snake — a wandering bad object like Dog/Cat/Crow/Rabbit (LaneWalker
    // drives its side-to-side lane changes), but with an actual pose swap
    // instead of just a wiggle: reared up like a cobra while idle, a
    // zigzagging slither while crossing lanes (SnakePose reads
    // LaneWalker.IsMoving). Bespoke instead of going through
    // CreateEntityPrefab since it needs two textures wired to a component,
    // not one texture into a plain material.
    static GameObject CreateSnakePrefab()
    {
        Texture2D idleTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/SnakeCobra.png");
        Texture2D movingTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/SnakeSlither.png");
        if (idleTex == null || movingTex == null)
        {
            Debug.LogWarning("Snake textures not found in Assets/Sprites/");
            return null;
        }

        const string name = "Snake";
        const float height = 1.9f;

        var root = new GameObject(name);
        root.transform.position = new Vector3(0f, height / 2f, 0f);
        root.AddComponent<MovingEntity>();
        root.AddComponent<ScoreValue>().value = -1;

        LaneWalker walker = root.AddComponent<LaneWalker>();
        SerializedObject walkerSo = new SerializedObject(walker);
        walkerSo.FindProperty("laneWidth").floatValue = LaneWidth;
        walkerSo.FindProperty("laneCount").intValue = LaneCount;
        walkerSo.ApplyModifiedPropertiesWithoutUndo();

        float aspect = (float)idleTex.width / idleTex.height;

        // Full sprite height includes the raised cobra head, well above what
        // a jump (jumpHeightDelta 1.4) can clear — trigger box only covers
        // the coiled body at ground level, same fix as Bicycle/Motorcycle.
        const float colliderHeight = 1.0f;
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(height * aspect, colliderHeight, 0.3f);
        box.center = new Vector3(0f, -(height - colliderHeight) / 2f, 0f);

        AddStaticGroundShadow(root, height * aspect * 0.7f, height * 0.35f, name + "_Shadow");

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = "Sprite";
        Object.DestroyImmediate(sprite.GetComponent<Collider>());
        sprite.transform.SetParent(root.transform);
        sprite.transform.localScale = new Vector3(height * aspect, height, 1f);
        sprite.transform.localPosition = Vector3.zero;

        Renderer renderer = sprite.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { mainTexture = idleTex };

        System.IO.Directory.CreateDirectory("Assets/Materials");
        string materialPath = "Assets/Materials/" + name + ".mat";
        AssetDatabase.DeleteAsset(materialPath);
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;

        SnakePose pose = root.AddComponent<SnakePose>();
        SerializedObject poseSo = new SerializedObject(pose);
        poseSo.FindProperty("walker").objectReferenceValue = walker;
        poseSo.FindProperty("spriteRenderer").objectReferenceValue = renderer;
        poseSo.FindProperty("idleTexture").objectReferenceValue = idleTex;
        poseSo.FindProperty("movingTexture").objectReferenceValue = movingTex;
        poseSo.FindProperty("height").floatValue = height;
        poseSo.ApplyModifiedPropertiesWithoutUndo();

        string savePath = "Assets/Prefabs/Snake.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject CreateEntityPrefab(string name, string texturePath, float height, string savePath, int? score = null, bool canWander = false, float? width = null, float? colliderHeight = null)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (tex == null)
        {
            Debug.LogWarning("Texture not found: " + texturePath);
            return null;
        }

        var root = new GameObject(name);
        root.transform.position = new Vector3(0f, height / 2f, 0f);
        root.AddComponent<MovingEntity>();
        if (score.HasValue)
            root.AddComponent<ScoreValue>().value = score.Value;

        // A few living bad objects (dog/cat/crow) drift sideways between
        // lanes at random — see LaneWalker.
        if (canWander)
        {
            LaneWalker walker = root.AddComponent<LaneWalker>();
            SerializedObject walkerSo = new SerializedObject(walker);
            walkerSo.FindProperty("laneWidth").floatValue = LaneWidth;
            walkerSo.FindProperty("laneCount").intValue = LaneCount;
            walkerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        float aspect = (float)tex.width / tex.height;
        // Normally width just follows the image's own aspect ratio — an
        // explicit override lets a few obstacles (the road-spanning piles)
        // be stretched wider than their art alone would give, without
        // distorting everything else that goes through this function.
        float spriteWidth = width ?? height * aspect;

        float ch = colliderHeight ?? height;
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(spriteWidth, ch, 0.3f);
        // A shorter box still needs to sit on the ground, not float centered
        // in the middle of the (taller) sprite — shift it down by half the
        // height that got trimmed off.
        box.center = new Vector3(0f, -(height - ch) / 2f, 0f);

        // Bad obstacles get a ground shadow so their lane position is easy
        // to judge when jumping — good pickups don't need it.
        if (score.HasValue && score.Value < 0)
            AddStaticGroundShadow(root, spriteWidth * 0.7f, height * 0.35f, name + "_Shadow");

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = "Sprite";
        Object.DestroyImmediate(sprite.GetComponent<Collider>());
        sprite.transform.SetParent(root.transform);

        sprite.transform.localScale = new Vector3(spriteWidth, height, 1f);
        sprite.transform.localPosition = Vector3.zero;

        Renderer renderer = sprite.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { mainTexture = tex };

        // Prefab assets can only reference materials that are themselves saved
        // assets — an in-memory Material here would serialize as a broken
        // (magenta) reference once written to disk.
        System.IO.Directory.CreateDirectory("Assets/Materials");
        string materialPath = "Assets/Materials/" + name + ".mat";
        AssetDatabase.DeleteAsset(materialPath); // safe to rerun Rebuild Scene
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // Flat ground decal — a Quad rotated flat on the road (same trick as
    // every other decal in this file) instead of a standing sprite. Used for
    // the pothole and both puddle colors — jump-over obstacles, no duck rule
    // (there's nothing to duck under, it's a hole/spill in the road).
    static GameObject CreateGroundDecalPrefab(string name, string textureFile, float size)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + textureFile);
        if (tex == null)
        {
            Debug.LogWarning("Texture not found: Assets/Sprites/" + textureFile);
            return null;
        }

        var root = new GameObject(name);
        root.AddComponent<MovingEntity>();
        root.AddComponent<ScoreValue>().value = -1;

        float aspect = (float)tex.width / tex.height;

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = "Sprite";
        Object.DestroyImmediate(sprite.GetComponent<Collider>());
        sprite.transform.SetParent(root.transform);
        sprite.transform.localPosition = new Vector3(0f, 0.01f, 0f);
        sprite.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lie flat, facing up
        sprite.transform.localScale = new Vector3(size * aspect, size, 1f);

        Renderer renderer = sprite.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { mainTexture = tex };

        System.IO.Directory.CreateDirectory("Assets/Materials");
        string materialPath = "Assets/Materials/" + name + ".mat";
        AssetDatabase.DeleteAsset(materialPath);
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(size * aspect, 0.1f, size);

        string savePath = "Assets/Prefabs/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void CreateHelpScreen()
    {
        var canvasGo = new GameObject("HelpCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150; // above gameplay UI, below the pause dialog

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var backdropGo = new GameObject("Backdrop");
        backdropGo.transform.SetParent(canvasGo.transform, false);
        Image backdrop = backdropGo.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.65f);
        RectTransform backdropRt = backdropGo.GetComponent<RectTransform>();
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;

        var textGo = new GameObject("HelpText");
        textGo.transform.SetParent(canvasGo.transform, false);
        Text text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 23;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = "СУТЬ ИГРЫ\n"
                  + "Дорога сама разгоняется всё быстрее, собирать хорошее, избегать плохое, вдвоём — делать трюки\n\n"
                  + "ЦЕЛЬ\n"
                  + "Проехать 100 км за самое короткое время, дополнительно набирая очки, трюки и скорость\n\n"
                  + "УПРАВЛЕНИЕ\n"
                  + "Правый: ← → полоса, ↑ прыжок, ↓ пригнуться\n"
                  + "Левый: A D полоса, W прыжок, S пригнуться\n"
                  + "Газа и тормоза больше нет — дорога разгоняется сама, столкновения замедляют\n\n"
                  + "ТРЮКИ\n"
                  + "АРКА: один приседает под аркой, другой в этот момент перепрыгивает её вместе с ним\n"
                  + "КОЛЬЦО: игроки одновременно меняются полосами — один в прыжке, другой понизу\n\n"
                  + "ДАТЧИКИ РАССТОЯНИЯ (ИМИТАТОР)\n"
                  + "2 датчика на игрока — по одному на руку (только верх/низ):\n"
                  + "обе руки вниз — пригнуться, одна вверх/другая вниз — полоса в сторону опущенной руки,\n"
                  + "быстро жать «верх» на обеих руках разом — прыжок-полёт\n"
                  + "Имитатор: левый Q/A верх/низ левой руки, E/D верх/низ правой; правый U/J, O/L\n\n"
                  + "ВЫХОД ИЗ ИГРЫ\n"
                  + "Все игроки разом держат «пригнуться» 10 секунд — после первых 5 молча, "
                  + "следующие 5 с обратным отсчётом на экране";

        Outline outline = textGo.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);

        RectTransform rt = text.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1700f, 950f);
        rt.anchoredPosition = new Vector2(0f, 40f);

        var hintGo = new GameObject("Hint");
        hintGo.transform.SetParent(canvasGo.transform, false);
        Text hint = hintGo.AddComponent<Text>();
        hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hint.fontSize = 26;
        hint.alignment = TextAnchor.MiddleCenter;
        hint.color = new Color(0.85f, 0.85f, 0.85f);
        hint.text = "F1 — закрыть      Q — выйти из игры";
        RectTransform hintRt = hint.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.5f, 0f);
        hintRt.anchorMax = new Vector2(0.5f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.sizeDelta = new Vector2(600f, 60f);
        hintRt.anchoredPosition = new Vector2(0f, 60f);

        canvasGo.SetActive(false);

        var controllerGo = new GameObject("HelpController");
        HelpController controller = controllerGo.AddComponent<HelpController>();
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("helpRoot").objectReferenceValue = canvasGo;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Small title pinned to the top of a HUD panel (e.g. "ОЧКИ" above the
    // score number) — same look TricksPanel already used, pulled out so
    // every stacked panel in CreateScoreUI can share it.
    static void CreatePanelLabel(Transform parent, string text)
    {
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(parent, false);
        Text label = labelGo.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 18;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.UpperCenter;
        label.color = new Color(0.85f, 0.85f, 0.85f);
        label.text = text;
        RectTransform labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 1f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.pivot = new Vector2(0.5f, 1f);
        labelRt.sizeDelta = new Vector2(0f, 26f);
        labelRt.anchoredPosition = new Vector2(0f, -6f);
    }

    static RectTransform CreateScoreUI(out Canvas canvas, out GameObject topScoresPanel)
    {
        var canvasGo = new GameObject("ScoreCanvas");
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // Distance leads the stack now, not score — it's the actual win
        // condition (100 km), the other three are secondary/bonus
        // categories. True top-right-corner anchor (not a hardcoded
        // 1920x1080 offset) — stays flush against the real edge on any
        // aspect ratio, instead of drifting/clipping when the actual screen
        // isn't exactly 16:9.
        var distancePanelGo = new GameObject("DistancePanel");
        distancePanelGo.transform.SetParent(canvasGo.transform, false);
        Image distancePanelImage = distancePanelGo.AddComponent<Image>();
        distancePanelImage.color = new Color(0f, 0f, 0f, 0.45f);

        RectTransform distancePanelRt = distancePanelGo.GetComponent<RectTransform>();
        distancePanelRt.anchorMin = new Vector2(1f, 1f);
        distancePanelRt.anchorMax = new Vector2(1f, 1f);
        distancePanelRt.pivot = new Vector2(1f, 1f);
        distancePanelRt.sizeDelta = new Vector2(260f, 90f);
        distancePanelRt.anchoredPosition = new Vector2(-20f, -20f);

        CreatePanelLabel(distancePanelGo.transform, "ДИСТАНЦИЯ");

        var distanceTextGo = new GameObject("DistanceText");
        distanceTextGo.transform.SetParent(distancePanelGo.transform, false);
        Text distanceText = distanceTextGo.AddComponent<Text>();
        distanceText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        distanceText.fontSize = 40;
        distanceText.fontStyle = FontStyle.Bold;
        distanceText.alignment = TextAnchor.MiddleCenter;
        distanceText.color = new Color(0.7f, 1f, 0.7f);
        distanceText.text = "0 из 100";

        Outline distanceOutline = distanceTextGo.AddComponent<Outline>();
        distanceOutline.effectColor = Color.black;
        distanceOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform distanceTextRt = distanceTextGo.GetComponent<RectTransform>();
        distanceTextRt.anchorMin = new Vector2(0f, 0f);
        distanceTextRt.anchorMax = new Vector2(1f, 0.75f);
        distanceTextRt.offsetMin = Vector2.zero;
        distanceTextRt.offsetMax = Vector2.zero;

        var distanceManagerGo = new GameObject("DistanceIndicator");
        DistanceIndicator distanceIndicator = distanceManagerGo.AddComponent<DistanceIndicator>();
        SerializedObject distanceSo = new SerializedObject(distanceIndicator);
        distanceSo.FindProperty("distanceText").objectReferenceValue = distanceText;
        distanceSo.ApplyModifiedPropertiesWithoutUndo();

        // Score panel, directly under the distance panel — continues the stack.
        var panelGo = new GameObject("ScorePanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        Image panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.45f);

        RectTransform panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 1f);
        panelRt.anchorMax = new Vector2(1f, 1f);
        panelRt.pivot = new Vector2(1f, 1f);
        panelRt.sizeDelta = new Vector2(260f, 100f);
        panelRt.anchoredPosition = new Vector2(distancePanelRt.anchoredPosition.x, distancePanelRt.anchoredPosition.y - distancePanelRt.sizeDelta.y - 10f);

        CreatePanelLabel(panelGo.transform, "ОЧКИ");

        var textGo = new GameObject("ScoreText");
        textGo.transform.SetParent(panelGo.transform, false);
        Text scoreText = textGo.AddComponent<Text>();
        scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        scoreText.fontSize = 48;
        scoreText.fontStyle = FontStyle.Bold;
        scoreText.alignment = TextAnchor.MiddleCenter;
        scoreText.color = new Color(1f, 0.85f, 0.2f);
        scoreText.text = "0";

        Outline textOutline = textGo.AddComponent<Outline>();
        textOutline.effectColor = Color.black;
        textOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 0f);
        textRt.anchorMax = new Vector2(1f, 0.75f);
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        // Invisible marker at the panel's center — popups fly toward this
        // point. Shares the same (0,0) anchor/pivot frame as the popups
        // themselves so their anchoredPosition values are directly
        // comparable (ScorePopup reads target.anchoredPosition raw) — so
        // this is deliberately expressed at reference resolution (1920x1080)
        // even though panelRt itself is now edge-anchored: converts
        // panelRt's real corner offset into that fixed frame, then finds
        // its center. Known minor approximation — on a non-16:9 screen the
        // popup's landing point can drift a little from the panel's actual
        // (correctly edge-anchored) position, but the panel itself always
        // renders correctly.
        var counterAnchorGo = new GameObject("CounterAnchor");
        counterAnchorGo.transform.SetParent(canvasGo.transform, false);
        RectTransform counterAnchor = counterAnchorGo.AddComponent<RectTransform>();
        counterAnchor.anchorMin = new Vector2(0f, 0f);
        counterAnchor.anchorMax = new Vector2(0f, 0f);
        counterAnchor.pivot = new Vector2(0f, 0f);
        counterAnchor.anchoredPosition = new Vector2(1920f + panelRt.anchoredPosition.x, 1080f + panelRt.anchoredPosition.y) - panelRt.sizeDelta / 2f;

        var managerGo = new GameObject("ScoreManager");
        ScoreManager manager = managerGo.AddComponent<ScoreManager>();

        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("scoreText").objectReferenceValue = scoreText;
        so.FindProperty("popupParent").objectReferenceValue = canvasGo.GetComponent<RectTransform>();
        so.FindProperty("counterAnchor").objectReferenceValue = counterAnchor;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Timer panel, directly under the score panel.
        var timerPanelGo = new GameObject("TimerPanel");
        timerPanelGo.transform.SetParent(canvasGo.transform, false);
        Image timerPanelImage = timerPanelGo.AddComponent<Image>();
        timerPanelImage.color = new Color(0f, 0f, 0f, 0.45f);

        RectTransform timerPanelRt = timerPanelGo.GetComponent<RectTransform>();
        timerPanelRt.anchorMin = new Vector2(1f, 1f);
        timerPanelRt.anchorMax = new Vector2(1f, 1f);
        timerPanelRt.pivot = new Vector2(1f, 1f);
        timerPanelRt.sizeDelta = new Vector2(260f, 70f);
        // pivot=(1,1) means anchoredPosition IS this panel's top edge, so
        // stacking the next one down only needs the PREVIOUS panel's own
        // height (not this one's) plus the gap.
        timerPanelRt.anchoredPosition = new Vector2(panelRt.anchoredPosition.x, panelRt.anchoredPosition.y - panelRt.sizeDelta.y - 10f);

        CreatePanelLabel(timerPanelGo.transform, "ВРЕМЯ");

        var timerTextGo = new GameObject("TimerText");
        timerTextGo.transform.SetParent(timerPanelGo.transform, false);
        Text timerText = timerTextGo.AddComponent<Text>();
        timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timerText.fontSize = 30;
        timerText.fontStyle = FontStyle.Bold;
        timerText.alignment = TextAnchor.MiddleCenter;
        timerText.color = Color.white;
        timerText.text = "00:00";

        Outline timerOutline = timerTextGo.AddComponent<Outline>();
        timerOutline.effectColor = Color.black;
        timerOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform timerTextRt = timerTextGo.GetComponent<RectTransform>();
        timerTextRt.anchorMin = new Vector2(0f, 0f);
        timerTextRt.anchorMax = new Vector2(1f, 0.7f);
        timerTextRt.offsetMin = Vector2.zero;
        timerTextRt.offsetMax = Vector2.zero;

        var timerManagerGo = new GameObject("GameTimer");
        GameTimer gameTimer = timerManagerGo.AddComponent<GameTimer>();
        SerializedObject timerSo = new SerializedObject(gameTimer);
        timerSo.FindProperty("timerText").objectReferenceValue = timerText;
        timerSo.ApplyModifiedPropertiesWithoutUndo();

        // Gear indicator panel, directly under the timer panel — just the
        // current gear's digit and the lever icon (per feedback: a proper
        // panel of its own above Speed, not a transient popup and not
        // squeezed in beside the speed panel). Speed panel below now
        // chains from this one instead of straight from Timer.
        var gearPanelGo = new GameObject("GearPanel");
        gearPanelGo.transform.SetParent(canvasGo.transform, false);
        Image gearPanelImage = gearPanelGo.AddComponent<Image>();
        gearPanelImage.color = new Color(0f, 0f, 0f, 0.45f);

        RectTransform gearPanelRt = gearPanelGo.GetComponent<RectTransform>();
        gearPanelRt.anchorMin = new Vector2(1f, 1f);
        gearPanelRt.anchorMax = new Vector2(1f, 1f);
        gearPanelRt.pivot = new Vector2(1f, 1f);
        gearPanelRt.sizeDelta = new Vector2(260f, 100f);
        gearPanelRt.anchoredPosition = new Vector2(timerPanelRt.anchoredPosition.x, timerPanelRt.anchoredPosition.y - timerPanelRt.sizeDelta.y - 10f);

        CreatePanelLabel(gearPanelGo.transform, "ПЕРЕДАЧА");

        // Lever icon on the left half of the content band, digit on the
        // right half — same content band (below the label) every other
        // panel here uses (anchors y 0..0.75).
        var gearLeverGo = new GameObject("Lever");
        gearLeverGo.transform.SetParent(gearPanelGo.transform, false);
        RawImage gearLeverImage = gearLeverGo.AddComponent<RawImage>();
        RectTransform gearLeverRt = gearLeverGo.GetComponent<RectTransform>();
        gearLeverRt.anchorMin = new Vector2(0f, 0f);
        gearLeverRt.anchorMax = new Vector2(0.45f, 0.75f);
        gearLeverRt.offsetMin = new Vector2(6f, 4f);
        gearLeverRt.offsetMax = new Vector2(-4f, -4f);

        var gearDigitGo = new GameObject("GearDigit");
        gearDigitGo.transform.SetParent(gearPanelGo.transform, false);
        Text gearDigitText = gearDigitGo.AddComponent<Text>();
        gearDigitText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        gearDigitText.fontSize = 44;
        gearDigitText.fontStyle = FontStyle.Bold;
        gearDigitText.alignment = TextAnchor.MiddleCenter;
        gearDigitText.color = new Color(0.6f, 0.9f, 1f);
        gearDigitText.text = "1";
        gearDigitGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform gearDigitRt = gearDigitText.GetComponent<RectTransform>();
        gearDigitRt.anchorMin = new Vector2(0.45f, 0f);
        gearDigitRt.anchorMax = new Vector2(1f, 0.75f);
        gearDigitRt.offsetMin = Vector2.zero;
        gearDigitRt.offsetMax = Vector2.zero;

        // Speed panel, directly under the gear panel now — continues the stack.
        var speedPanelGo = new GameObject("SpeedPanel");
        speedPanelGo.transform.SetParent(canvasGo.transform, false);
        Image speedPanelImage = speedPanelGo.AddComponent<Image>();
        speedPanelImage.color = new Color(0f, 0f, 0f, 0.45f);

        RectTransform speedPanelRt = speedPanelGo.GetComponent<RectTransform>();
        speedPanelRt.anchorMin = new Vector2(1f, 1f);
        speedPanelRt.anchorMax = new Vector2(1f, 1f);
        speedPanelRt.pivot = new Vector2(1f, 1f);
        speedPanelRt.sizeDelta = new Vector2(260f, 100f);
        speedPanelRt.anchoredPosition = new Vector2(gearPanelRt.anchoredPosition.x, gearPanelRt.anchoredPosition.y - gearPanelRt.sizeDelta.y - 10f);

        CreatePanelLabel(speedPanelGo.transform, "СКОРОСТЬ");

        var speedTextGo = new GameObject("SpeedText");
        speedTextGo.transform.SetParent(speedPanelGo.transform, false);
        Text speedText = speedTextGo.AddComponent<Text>();
        speedText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        speedText.fontSize = 34;
        speedText.fontStyle = FontStyle.Bold;
        speedText.alignment = TextAnchor.MiddleCenter;
        speedText.color = new Color(0.6f, 0.9f, 1f);
        speedText.text = "0.0 км/ч"; // gear no longer duplicated here — see GearPanel above

        Outline speedOutline = speedTextGo.AddComponent<Outline>();
        speedOutline.effectColor = Color.black;
        speedOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform speedTextRt = speedTextGo.GetComponent<RectTransform>();
        speedTextRt.anchorMin = new Vector2(0f, 0f);
        speedTextRt.anchorMax = new Vector2(1f, 0.75f);
        speedTextRt.offsetMin = Vector2.zero;
        speedTextRt.offsetMax = Vector2.zero;

        var speedManagerGo = new GameObject("SpeedIndicator");
        SpeedIndicator speedIndicator = speedManagerGo.AddComponent<SpeedIndicator>();
        SerializedObject speedSo = new SerializedObject(speedIndicator);
        speedSo.FindProperty("speedText").objectReferenceValue = speedText;
        speedSo.FindProperty("gearDigitText").objectReferenceValue = gearDigitText;
        speedSo.FindProperty("leverImage").objectReferenceValue = gearLeverImage;
        // Lever-shift animation frames (yandex_api/gen_asset.sh, see
        // Assets/Sprites/GearLever*.png) — flickered through at random on
        // each shift (SpeedIndicator.PlayGearShift), settling back on
        // leverFrames[0] as the resting frame the icon shows the rest of
        // the time.
        SerializedProperty leverFramesProp = speedSo.FindProperty("leverFrames");
        // 4/5 are 1/3 mirrored horizontally — doubles the tilted-pose
        // variety (left/right leans) the random flicker can draw from
        // instead of just the one generated angle each way.
        string[] leverFrameFiles = { "GearLever1.png", "GearLever2.png", "GearLever3.png", "GearLever4.png", "GearLever5.png" };
        leverFramesProp.arraySize = leverFrameFiles.Length;
        for (int i = 0; i < leverFrameFiles.Length; i++)
            leverFramesProp.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + leverFrameFiles[i]);
        speedSo.ApplyModifiedPropertiesWithoutUndo();

        // Top-3 panel — on the left side of the screen (true corner anchor,
        // same pattern the ОЧКИ/ВРЕМЯ/СКОРОСТЬ/ДИСТАНЦИЯ/ТРЮКИ stack uses on
        // the right), clear of that whole stack instead of crowding next to
        // it on the same side.
        var topPanelGo = new GameObject("TopScoresPanel");
        topPanelGo.transform.SetParent(canvasGo.transform, false);
        Image topPanelImage = topPanelGo.AddComponent<Image>();
        topPanelImage.color = new Color(0f, 0f, 0f, 0.45f);

        RectTransform topPanelRt = topPanelGo.GetComponent<RectTransform>();
        topPanelRt.anchorMin = new Vector2(0f, 1f);
        topPanelRt.anchorMax = new Vector2(0f, 1f);
        topPanelRt.pivot = new Vector2(0f, 1f);
        topPanelRt.sizeDelta = new Vector2(320f, 400f);
        topPanelRt.anchoredPosition = new Vector2(20f, -20f);

        var topTitleGo = new GameObject("TopScoresTitle");
        topTitleGo.transform.SetParent(topPanelGo.transform, false);
        Text topTitle = topTitleGo.AddComponent<Text>();
        topTitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        topTitle.fontSize = 22;
        topTitle.fontStyle = FontStyle.Bold;
        topTitle.alignment = TextAnchor.MiddleCenter;
        topTitle.color = Color.white;
        topTitle.text = "ТОП: ВРЕМЯ";
        topTitleGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform topTitleRt = topTitle.GetComponent<RectTransform>();
        topTitleRt.anchorMin = new Vector2(0.5f, 0.5f);
        topTitleRt.anchorMax = new Vector2(0.5f, 0.5f);
        topTitleRt.pivot = new Vector2(0.5f, 0.5f);
        topTitleRt.sizeDelta = new Vector2(300f, 40f);
        topTitleRt.anchoredPosition = new Vector2(0f, 160f);

        // One row per rank — each with its own photo (if that slot ever had
        // one attached), same idea as TopResultsPage on the start screen but
        // scaled down to fit this narrower in-game panel.
        var topRowTexts = new Text[3];
        var topRowPhotos = new RawImage[3];
        float[] topRowY = { 80f, -20f, -120f };

        for (int i = 0; i < 3; i++)
        {
            var rowTextGo = new GameObject("TopRowText" + i);
            rowTextGo.transform.SetParent(topPanelGo.transform, false);
            Text rowText = rowTextGo.AddComponent<Text>();
            rowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rowText.fontSize = 20;
            rowText.fontStyle = FontStyle.Bold;
            rowText.alignment = TextAnchor.MiddleLeft;
            rowText.color = Color.white;
            rowTextGo.AddComponent<Outline>().effectColor = Color.black;
            RectTransform rowTextRt = rowText.GetComponent<RectTransform>();
            rowTextRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowTextRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowTextRt.pivot = new Vector2(0.5f, 0.5f);
            rowTextRt.sizeDelta = new Vector2(150f, 40f);
            rowTextRt.anchoredPosition = new Vector2(-75f, topRowY[i]);
            topRowTexts[i] = rowText;

            var rowPhotoGo = new GameObject("TopRowPhoto" + i);
            rowPhotoGo.transform.SetParent(topPanelGo.transform, false);
            RawImage rowPhoto = rowPhotoGo.AddComponent<RawImage>();
            rowPhoto.color = Color.white;
            RectTransform rowPhotoRt = rowPhoto.GetComponent<RectTransform>();
            rowPhotoRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowPhotoRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowPhotoRt.pivot = new Vector2(0.5f, 0.5f);
            rowPhotoRt.sizeDelta = new Vector2(70f, 70f);
            rowPhotoRt.anchoredPosition = new Vector2(115f, topRowY[i]);
            rowPhotoGo.SetActive(false);
            topRowPhotos[i] = rowPhoto;
        }

        var highScoreGo = new GameObject("HighScoreManager");
        HighScoreManager highScore = highScoreGo.AddComponent<HighScoreManager>();
        SerializedObject highScoreSo = new SerializedObject(highScore);
        highScoreSo.FindProperty("titleText").objectReferenceValue = topTitle;
        SerializedProperty topRowTextsProp = highScoreSo.FindProperty("rowTexts");
        topRowTextsProp.arraySize = topRowTexts.Length;
        for (int i = 0; i < topRowTexts.Length; i++)
            topRowTextsProp.GetArrayElementAtIndex(i).objectReferenceValue = topRowTexts[i];
        SerializedProperty topRowPhotosProp = highScoreSo.FindProperty("rowPhotos");
        topRowPhotosProp.arraySize = topRowPhotos.Length;
        for (int i = 0; i < topRowPhotos.Length; i++)
            topRowPhotosProp.GetArrayElementAtIndex(i).objectReferenceValue = topRowPhotos[i];
        highScoreSo.ApplyModifiedPropertiesWithoutUndo();

        topScoresPanel = topPanelGo;
        return speedPanelRt; // last in the stack — CreateTricksUI chains its panel below this one
    }

    // One checkbox+text row for the post-win recap (stats pages, record
    // reveal) — same visual language CreateChecklistRow already uses for
    // СУТЬ ИГРЫ/ЦЕЛЬ, but center-anchored around a given point instead of
    // page-local-left, since this recap's elements (recordText, the old
    // achievementsText) are all centered on screen rather than pinned to a
    // page's own left edge. rowGo is the single object to SetActive for
    // show/hide (checkbox + text move/hide together); the returned Text is
    // for content updates.
    static Text CreateWinCheckRow(Transform parent, Vector2 pos, float width, int fontSize, out GameObject rowGo)
    {
        rowGo = new GameObject("Row");
        rowGo.transform.SetParent(parent, false);
        RectTransform rowRt = rowGo.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 0.5f);
        rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta = Vector2.zero;
        rowRt.anchoredPosition = pos;

        const float checkSize = 56f;
        const float textGap = 20f;

        var checkGo = new GameObject("Check");
        checkGo.transform.SetParent(rowRt, false);
        Image checkImg = checkGo.AddComponent<Image>();
        checkImg.color = new Color(0.15f, 0.55f, 0.2f, 0.95f);
        Outline checkOutline = checkGo.AddComponent<Outline>();
        checkOutline.effectColor = Color.white;
        checkOutline.effectDistance = new Vector2(2f, -2f);
        RectTransform checkRt = checkImg.GetComponent<RectTransform>();
        checkRt.anchorMin = new Vector2(0.5f, 0.5f);
        checkRt.anchorMax = new Vector2(0.5f, 0.5f);
        checkRt.pivot = new Vector2(0f, 0.5f);
        checkRt.sizeDelta = new Vector2(checkSize, checkSize);
        checkRt.anchoredPosition = new Vector2(-width / 2f, 0f);

        var markGo = new GameObject("Mark");
        markGo.transform.SetParent(checkGo.transform, false);
        Text mark = markGo.AddComponent<Text>();
        mark.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mark.fontSize = Mathf.RoundToInt(checkSize * 0.6f);
        mark.fontStyle = FontStyle.Bold;
        mark.alignment = TextAnchor.MiddleCenter;
        mark.color = Color.white;
        mark.text = "✓";
        RectTransform markRt = mark.GetComponent<RectTransform>();
        markRt.anchorMin = Vector2.zero;
        markRt.anchorMax = Vector2.one;
        markRt.offsetMin = Vector2.zero;
        markRt.offsetMax = Vector2.zero;

        var lineGo = new GameObject("Line");
        lineGo.transform.SetParent(rowRt, false);
        Text line = lineGo.AddComponent<Text>();
        line.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        line.fontSize = fontSize;
        line.fontStyle = FontStyle.Bold;
        line.alignment = TextAnchor.MiddleLeft;
        // The row's own height is a fixed checkSize (56), but callers pass
        // whatever fontSize they want (recordText uses 58 — bigger than
        // that) — without this, Unity's default Truncate wrap mode clips
        // any line taller than the box down to nothing rendered at all,
        // not just a cosmetically-cut line, which is exactly what silently
        // blanked the "NEW RECORD" reveal (see WinSequence's RevealRecords).
        line.verticalOverflow = VerticalWrapMode.Overflow;
        line.color = Color.white;
        line.text = "";
        lineGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform lineRt = line.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0.5f, 0.5f);
        lineRt.anchorMax = new Vector2(0.5f, 0.5f);
        lineRt.pivot = new Vector2(0f, 0.5f);
        lineRt.sizeDelta = new Vector2(width - checkSize - textGap, checkSize);
        lineRt.anchoredPosition = new Vector2(-width / 2f + checkSize + textGap, 0f);

        rowGo.SetActive(false);
        return line;
    }

    static void CreateWinSequence(Canvas scoreCanvas)
    {
        // Shown first, before anything else in the sequence — the player's
        // controls go dead the instant this appears (RunSequence disables
        // them right after), so without a cue the game just stops
        // responding with no explanation. Held briefly, then the usual
        // shrink-and-fly-away plays.
        var finishTextGo = new GameObject("FinishText");
        finishTextGo.transform.SetParent(scoreCanvas.transform, false);

        Text finishText = finishTextGo.AddComponent<Text>();
        finishText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        finishText.fontSize = 70;
        finishText.fontStyle = FontStyle.Bold;
        finishText.alignment = TextAnchor.MiddleCenter;
        finishText.color = new Color(1f, 0.85f, 0.15f);
        finishText.text = "ФИНИШ!";

        Outline finishOutline = finishTextGo.AddComponent<Outline>();
        finishOutline.effectColor = Color.black;
        finishOutline.effectDistance = new Vector2(3f, -3f);

        RectTransform finishRt = finishTextGo.GetComponent<RectTransform>();
        finishRt.anchorMin = new Vector2(0.5f, 0.5f);
        finishRt.anchorMax = new Vector2(0.5f, 0.5f);
        finishRt.pivot = new Vector2(0.5f, 0.5f);
        finishRt.sizeDelta = new Vector2(900f, 160f);
        finishRt.anchoredPosition = new Vector2(0f, 100f);
        finishTextGo.SetActive(false);

        var winTextGo = new GameObject("WinText");
        winTextGo.transform.SetParent(scoreCanvas.transform, false);

        Text winText = winTextGo.AddComponent<Text>();
        winText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        winText.fontSize = 76; // was 90 for the old shorter "ВЫ ПОБЕДИЛИ!" — smaller so the longer replacement stays on one line
        winText.fontStyle = FontStyle.Bold;
        winText.alignment = TextAnchor.MiddleCenter;
        winText.color = new Color(1f, 0.85f, 0.15f);
        winText.text = "ВЫ ПРОШЛИ ДО КОНЦА";

        Outline winOutline = winTextGo.AddComponent<Outline>();
        winOutline.effectColor = Color.black;
        winOutline.effectDistance = new Vector2(3f, -3f);

        RectTransform winRt = winTextGo.GetComponent<RectTransform>();
        winRt.anchorMin = new Vector2(0.5f, 0.5f);
        winRt.anchorMax = new Vector2(0.5f, 0.5f);
        winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(1300f, 200f);
        winRt.anchoredPosition = new Vector2(0f, 300f); // was 220 — raised, was getting covered
        winTextGo.SetActive(false);

        // Shared backdrop behind the record reveal and the stats pages below
        // it — same dark-tint-plus-outline treatment every other table in
        // the game uses (carousel, end-game leaderboard), so this recap
        // reads as one more table instead of bare floating text. Sized to
        // cover both text boxes (record reveal above, stats pages below)
        // since WinSequence shows them one after another with this staying
        // up for both, not two separate backdrops popping in and out.
        var statsBackdropGo = new GameObject("StatsBackdrop");
        statsBackdropGo.transform.SetParent(scoreCanvas.transform, false);
        Image statsBackdropImg = statsBackdropGo.AddComponent<Image>();
        statsBackdropImg.color = new Color(0f, 0f, 0f, 0.22f);
        Outline statsBackdropOutline = statsBackdropGo.AddComponent<Outline>();
        statsBackdropOutline.effectColor = Color.gray;
        statsBackdropOutline.effectDistance = new Vector2(4f, -4f);
        RectTransform statsBackdropRt = statsBackdropGo.GetComponent<RectTransform>();
        statsBackdropRt.anchorMin = new Vector2(0.5f, 0.5f);
        statsBackdropRt.anchorMax = new Vector2(0.5f, 0.5f);
        statsBackdropRt.pivot = new Vector2(0.5f, 0.5f);
        // Wide enough for the longer ИТОГИ ЗАБЕГА rows now that each can
        // carry an inline "— НОВЫЙ РЕКОРД ТОП-N" tag (see WinSequence.
        // ShowStatsPages) — was 1100 before that, 1600 during a brief
        // two-column layout that's since been folded back into one. Taller
        // too (was 440), to fit 7 rows at their own now-wider vertical
        // spacing below without the bottom ones spilling out past the
        // backdrop's own edge.
        statsBackdropRt.sizeDelta = new Vector2(1200f, 600f);
        statsBackdropRt.anchoredPosition = new Vector2(0f, -110f);
        statsBackdropGo.SetActive(false);

        // Sequential "new record!" reveal — shown below the win text once
        // the run's final stats are checked against all 4 leaderboards.
        // Bigger than the old plain-text version (58 vs 54) and now a
        // checkbox row like the stats pages below it, not a standalone
        // colored line.
        Text recordText = CreateWinCheckRow(scoreCanvas.transform, new Vector2(0f, 60f), 1000f, 58, out GameObject recordRowGo);
        recordText.color = new Color(0.4f, 1f, 0.5f);

        // Post-win achievements summary — cycles a few pages (totals,
        // collected, hit, tricks+rank), lower on screen than the record
        // reveal since both can briefly overlap in time. Title + a pool of
        // checkbox rows (CreateWinCheckRow) instead of one big multi-line
        // text block — bigger text, and matches the checklist style already
        // used elsewhere (СУТЬ ИГРЫ/ЦЕЛЬ). 7 rows covers the largest page —
        // ТРЮКИ, which (unlike СОБРАНО/СБИТО) has no "Всего" total line and
        // so can need one row per trick type, all 7 of them (see
        // WinSequence's tricks list); WinSequence hides whichever ones a
        // given page doesn't need.
        // Page title + achievement rows — single centered column (briefly
        // moved off-center to sit beside a separate rating column; that
        // column's own info now folds inline into ИТОГИ ЗАБЕГА's own rows
        // instead, see WinSequence.ShowStatsPages, so back to centered).
        var statsTitleGo = new GameObject("StatsTitle");
        statsTitleGo.transform.SetParent(scoreCanvas.transform, false);
        Text statsTitle = statsTitleGo.AddComponent<Text>();
        statsTitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statsTitle.fontSize = 40;
        statsTitle.fontStyle = FontStyle.Bold;
        statsTitle.alignment = TextAnchor.MiddleCenter;
        statsTitle.color = new Color(1f, 0.85f, 0.2f);
        statsTitle.text = "";
        statsTitleGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform statsTitleRt = statsTitle.GetComponent<RectTransform>();
        statsTitleRt.anchorMin = new Vector2(0.5f, 0.5f);
        statsTitleRt.anchorMax = new Vector2(0.5f, 0.5f);
        statsTitleRt.pivot = new Vector2(0.5f, 0.5f);
        statsTitleRt.sizeDelta = new Vector2(1000f, 60f);
        statsTitleRt.anchoredPosition = new Vector2(0f, 90f);
        statsTitleGo.SetActive(false);

        const int statsRowCount = 7;
        var statsRows = new Text[statsRowCount];
        var statsRowRoots = new GameObject[statsRowCount];
        for (int i = 0; i < statsRowCount; i++)
        {
            // 60, not 45 — CreateWinCheckRow's own checkSize is 56, so 45
            // had adjacent rows overlapping by 11px regardless of their
            // text content. 60 clears that with a small real gap.
            float y = 20f - i * 60f;
            statsRows[i] = CreateWinCheckRow(scoreCanvas.transform, new Vector2(0f, y), 1000f, 36, out statsRowRoots[i]);
        }

        // Icon grid for СОБРАНО/СБИТО (see WinSequence.ShowStatsPages) —
        // replaces the checkbox+text rows for just those two pages with
        // small repeated icons (one per unit collected/hit, e.g. 3
        // cherries = 3 little cherry icons) plus a single "ИТОГО ±N" line
        // below, per feedback that the per-type text breakdown wasn't
        // wanted there. Pool sized generously (50) for a big haul; if an
        // actual run's total ever exceeds that, WinSequence just shows as
        // many as fit — the ИТОГО line still states the true total.
        const int iconCols = 10;
        const int iconRows = 5;
        const float iconGridTop = 40f;
        const float iconGridLeft = -540f;
        const float iconCellWidth = 108f; // (iconGridRight - iconGridLeft) / iconCols
        const float iconCellHeight = 72f;
        const float iconSize = 54f;

        var statsIconSlots = new RawImage[iconCols * iconRows];
        for (int i = 0; i < statsIconSlots.Length; i++)
        {
            int col = i % iconCols;
            int row = i / iconCols;
            float cx = iconGridLeft + iconCellWidth * (col + 0.5f);
            float cy = iconGridTop - iconCellHeight * (row + 0.5f);

            var iconGo = new GameObject("StatsIcon" + i);
            iconGo.transform.SetParent(scoreCanvas.transform, false);
            RawImage icon = iconGo.AddComponent<RawImage>();
            RectTransform iconRt = icon.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(iconSize, iconSize);
            iconRt.anchoredPosition = new Vector2(cx, cy);
            iconGo.SetActive(false);
            statsIconSlots[i] = icon;
        }

        var statsTotalGo = new GameObject("StatsTotal");
        statsTotalGo.transform.SetParent(scoreCanvas.transform, false);
        Text statsTotalText = statsTotalGo.AddComponent<Text>();
        statsTotalText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statsTotalText.fontSize = 44;
        statsTotalText.fontStyle = FontStyle.Bold;
        statsTotalText.alignment = TextAnchor.MiddleCenter;
        statsTotalText.color = new Color(1f, 0.85f, 0.2f);
        statsTotalGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform statsTotalRt = statsTotalText.GetComponent<RectTransform>();
        statsTotalRt.anchorMin = new Vector2(0.5f, 0.5f);
        statsTotalRt.anchorMax = new Vector2(0.5f, 0.5f);
        statsTotalRt.pivot = new Vector2(0.5f, 0.5f);
        statsTotalRt.sizeDelta = new Vector2(600f, 60f);
        statsTotalRt.anchoredPosition = new Vector2(0f, -350f);
        statsTotalGo.SetActive(false);

        // The finale of the post-win recap: the real per-category top-3
        // tables (same TopResultsPage the start screen carousel uses, photo
        // slots included) instead of a plain numeric rank line — right after
        // the photo capture (RevealRecords), so the just-taken photo is
        // visible immediately instead of only later on the start screen.
        var leaderboardRootGo = new GameObject("WinLeaderboardRoot");
        leaderboardRootGo.transform.SetParent(scoreCanvas.transform, false);
        RectTransform leaderboardRootRt = leaderboardRootGo.AddComponent<RectTransform>();
        leaderboardRootRt.anchorMin = new Vector2(0.5f, 0.5f);
        leaderboardRootRt.anchorMax = new Vector2(0.5f, 0.5f);
        leaderboardRootRt.pivot = new Vector2(0.5f, 0.5f);
        // Must match the start-screen carousel's own box (1400 wide, see
        // CreateStartScreen) exactly — CreateTopResultsPage's arrow geometry
        // hardcodes half that width (700f) assuming its parent is that same
        // size, and the two screens are meant to look identical anyway.
        leaderboardRootRt.sizeDelta = new Vector2(1400f, 720f);
        leaderboardRootRt.anchoredPosition = Vector2.zero;

        // Same dark tinted frame the start-screen carousel uses behind its
        // own tables — without it these floated directly over the 3D scene
        // with no visual grouping.
        var leaderboardBgGo = new GameObject("Background");
        leaderboardBgGo.transform.SetParent(leaderboardRootRt, false);
        Image leaderboardBg = leaderboardBgGo.AddComponent<Image>();
        leaderboardBg.color = new Color(0f, 0f, 0f, 0.22f);
        Outline leaderboardBgOutline = leaderboardBgGo.AddComponent<Outline>();
        leaderboardBgOutline.effectColor = Color.gray;
        leaderboardBgOutline.effectDistance = new Vector2(4f, -4f);
        RectTransform leaderboardBgRt = leaderboardBgGo.GetComponent<RectTransform>();
        leaderboardBgRt.anchorMin = Vector2.zero;
        leaderboardBgRt.anchorMax = Vector2.one;
        leaderboardBgRt.offsetMin = Vector2.zero;
        leaderboardBgRt.offsetMax = Vector2.zero;

        var leaderboardPages = new GameObject[4];
        for (int category = 0; category < 4; category++)
        {
            GameObject page = CreateTopResultsPage(leaderboardRootRt, category);
            page.SetActive(false);
            leaderboardPages[category] = page;
        }

        // The whole root (background included) stays hidden until
        // WinSequence actually shows the leaderboard tables — otherwise its
        // background panel (a sibling of the pages, not gated by their own
        // SetActive) rendered as an empty tinted box floating on screen
        // from the moment the game started.
        leaderboardRootGo.SetActive(false);

        var winGo = new GameObject("WinSequence");
        WinSequence win = winGo.AddComponent<WinSequence>();

        SerializedObject so = new SerializedObject(win);
        so.FindProperty("finishText").objectReferenceValue = finishTextGo;
        so.FindProperty("winTextRoot").objectReferenceValue = winRt;
        so.FindProperty("statsBackdrop").objectReferenceValue = statsBackdropGo;
        so.FindProperty("recordRowRoot").objectReferenceValue = recordRowGo;
        so.FindProperty("recordText").objectReferenceValue = recordText;
        so.FindProperty("statsTitle").objectReferenceValue = statsTitle;
        SerializedProperty statsRowsProp = so.FindProperty("statsRows");
        statsRowsProp.arraySize = statsRows.Length;
        for (int i = 0; i < statsRows.Length; i++)
            statsRowsProp.GetArrayElementAtIndex(i).objectReferenceValue = statsRows[i];
        SerializedProperty statsRowRootsProp = so.FindProperty("statsRowRoots");
        statsRowRootsProp.arraySize = statsRowRoots.Length;
        for (int i = 0; i < statsRowRoots.Length; i++)
            statsRowRootsProp.GetArrayElementAtIndex(i).objectReferenceValue = statsRowRoots[i];
        SerializedProperty statsIconSlotsProp = so.FindProperty("statsIconSlots");
        statsIconSlotsProp.arraySize = statsIconSlots.Length;
        for (int i = 0; i < statsIconSlots.Length; i++)
            statsIconSlotsProp.GetArrayElementAtIndex(i).objectReferenceValue = statsIconSlots[i];
        so.FindProperty("statsTotalText").objectReferenceValue = statsTotalText;
        so.FindProperty("cherryIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Cherry.png");
        so.FindProperty("heartIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Heart.png");
        so.FindProperty("flowerIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Flower.png");
        so.FindProperty("dogIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Dog.png");
        so.FindProperty("catIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Cat.png");
        so.FindProperty("bicycleIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Bicycle.png");
        so.FindProperty("leaderboardRoot").objectReferenceValue = leaderboardRootGo;
        SerializedProperty leaderboardPagesProp = so.FindProperty("leaderboardPages");
        leaderboardPagesProp.arraySize = leaderboardPages.Length;
        for (int i = 0; i < leaderboardPages.Length; i++)
            leaderboardPagesProp.GetArrayElementAtIndex(i).objectReferenceValue = leaderboardPages[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Freestyle-tricks counter — folded into the same stacked column as
    // distance/score/time/speed (same size, same background, same edge
    // anchor) instead of floating alone at mid-screen-right in a
    // differently-sized panel, so it reads as part of the same HUD group.
    static void CreateTricksUI(RectTransform chainFrom)
    {
        var canvasGo = new GameObject("TricksCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        canvasGo.AddComponent<GraphicRaycaster>();

        var panelGo = new GameObject("TricksPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        Image panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.45f);

        RectTransform panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 1f);
        panelRt.anchorMax = new Vector2(1f, 1f);
        panelRt.pivot = new Vector2(1f, 1f);
        panelRt.sizeDelta = new Vector2(260f, 120f);
        panelRt.anchoredPosition = new Vector2(chainFrom.anchoredPosition.x, chainFrom.anchoredPosition.y - chainFrom.sizeDelta.y - 10f);

        CreatePanelLabel(panelGo.transform, "ТРЮКИ");

        var textGo = new GameObject("TricksText");
        textGo.transform.SetParent(panelGo.transform, false);
        Text tricksText = textGo.AddComponent<Text>();
        tricksText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tricksText.fontSize = 56;
        tricksText.fontStyle = FontStyle.Bold;
        tricksText.alignment = TextAnchor.MiddleCenter;
        tricksText.color = new Color(0.6f, 0.9f, 1f);
        tricksText.text = "0";

        Outline textOutline = textGo.AddComponent<Outline>();
        textOutline.effectColor = Color.black;
        textOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 0f);
        textRt.anchorMax = new Vector2(1f, 0.75f);
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        // Marks the panel's own center in a (0,0)-anchored frame — popups
        // live in that same frame, so their anchoredPosition is directly
        // comparable/lerp-able against this point (mirrors ScoreManager's
        // counterAnchor setup — same reference-resolution approximation
        // noted there).
        var counterAnchorGo = new GameObject("TricksCounterAnchor");
        counterAnchorGo.transform.SetParent(canvasGo.transform, false);
        RectTransform counterAnchor = counterAnchorGo.AddComponent<RectTransform>();
        counterAnchor.anchorMin = Vector2.zero;
        counterAnchor.anchorMax = Vector2.zero;
        counterAnchor.pivot = Vector2.zero;
        counterAnchor.anchoredPosition = new Vector2(1920f + panelRt.anchoredPosition.x, 1080f + panelRt.anchoredPosition.y) - panelRt.sizeDelta / 2f;

        var managerGo = new GameObject("TricksManager");
        TricksManager manager = managerGo.AddComponent<TricksManager>();
        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("tricksText").objectReferenceValue = tricksText;
        so.FindProperty("popupParent").objectReferenceValue = canvasGo.GetComponent<RectTransform>();
        so.FindProperty("counterAnchor").objectReferenceValue = counterAnchor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Bottom-left/bottom-right HUD per player, for the gesture simulator:
    // a legend (keys + gesture descriptions), a row of raw key-state
    // squares, and the interpreted-gesture arrows — sits dim/idle when that
    // player is on keyboard controls (GestureInput disabled). Returns both
    // canvases (left, right) so the start screen can hide them while its
    // own menu is up (they're gameplay HUD, not menu chrome) and reveal
    // them once the game actually begins.
    static (GameObject left, GameObject right) CreateGestureIndicators(GameObject playerRight, GameObject playerLeft)
    {
        GameObject leftCanvas = CreateGesturePanel(playerLeft, new Vector2(0f, 0f),
            "КЛАВИШИ: лев. рука Q/A верх/низ; прав. рука E/D верх/низ\n"
            + "ЖЕСТЫ: обе вниз — присесть, по диагонали — смещение, "
            + "Q+E быстро жать вместе — прыжок-полёт",
            new[] { "Q", "A", "E", "D" });

        GameObject rightCanvas = CreateGesturePanel(playerRight, new Vector2(1f, 0f),
            "КЛАВИШИ: лев. рука U/J верх/низ; прав. рука O/L верх/низ\n"
            + "ЖЕСТЫ: обе вниз — присесть, по диагонали — смещение, "
            + "U+O быстро жать вместе — прыжок-полёт",
            new[] { "U", "J", "O", "L" });

        return (leftCanvas, rightCanvas);
    }

    // Index order matches GestureKeyIndicator.KeyOrder(): leftUp, leftDown,
    // rightUp, rightDown. Column/row place each key into the physical 2x2
    // block it's bound to (column0 = left hand, column1 = right hand;
    // row0 = up, row1 = down).
    static readonly int[] GestureGridCol = { 0, 0, 1, 1 };
    static readonly int[] GestureGridRow = { 0, 1, 0, 1 };

    static GameObject CreateGesturePanel(GameObject player, Vector2 anchor, string legendText, string[] keyLabels)
    {
        bool leftSide = anchor.x < 0.5f;
        float sign = leftSide ? 1f : -1f;

        var canvasGo = new GameObject(player.name + "GestureCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // One-word current-action readout, bottom-most.
        const float actionY = 20f;
        const float actionHeight = 70f;
        var actionGo = new GameObject("GestureAction");
        actionGo.transform.SetParent(canvasGo.transform, false);
        Text actionText = actionGo.AddComponent<Text>();
        actionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        actionText.fontSize = 40;
        actionText.fontStyle = FontStyle.Bold;
        actionText.alignment = leftSide ? TextAnchor.LowerLeft : TextAnchor.LowerRight;
        actionText.color = new Color(1f, 0.85f, 0.2f);
        actionText.text = "–";

        Outline actionOutline = actionGo.AddComponent<Outline>();
        actionOutline.effectColor = Color.black;
        actionOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform actionRt = actionText.GetComponent<RectTransform>();
        actionRt.anchorMin = anchor;
        actionRt.anchorMax = anchor;
        actionRt.pivot = anchor;
        actionRt.sizeDelta = new Vector2(340f, actionHeight);
        actionRt.anchoredPosition = new Vector2(sign * 20f, actionY);

        var actionIndicatorGo = new GameObject(player.name + "GestureActionIndicator");
        GestureActionIndicator actionIndicator = actionIndicatorGo.AddComponent<GestureActionIndicator>();
        SerializedObject actionSo = new SerializedObject(actionIndicator);
        actionSo.FindProperty("gestureInput").objectReferenceValue = player.GetComponent<GestureInput>();
        actionSo.FindProperty("actionText").objectReferenceValue = actionText;
        actionSo.ApplyModifiedPropertiesWithoutUndo();

        // Interpreted-gesture arrows (existing indicator), above the action readout.
        const float glyphY = actionY + actionHeight + 12f;
        var glyphGo = new GameObject("GestureGlyphs");
        glyphGo.transform.SetParent(canvasGo.transform, false);
        Text glyphText = glyphGo.AddComponent<Text>();
        glyphText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        glyphText.fontSize = 72;
        glyphText.fontStyle = FontStyle.Bold;
        glyphText.alignment = leftSide ? TextAnchor.LowerLeft : TextAnchor.LowerRight;
        glyphText.supportRichText = true;
        glyphText.text = "–  –  –";

        Outline glyphOutline = glyphGo.AddComponent<Outline>();
        glyphOutline.effectColor = Color.black;
        glyphOutline.effectDistance = new Vector2(3f, -3f);

        RectTransform glyphRt = glyphText.GetComponent<RectTransform>();
        glyphRt.anchorMin = anchor;
        glyphRt.anchorMax = anchor;
        glyphRt.pivot = anchor;
        glyphRt.sizeDelta = new Vector2(340f, 100f);
        glyphRt.anchoredPosition = new Vector2(sign * 20f, glyphY);

        var indicatorGo = new GameObject(player.name + "GestureIndicator");
        GestureIndicator indicator = indicatorGo.AddComponent<GestureIndicator>();
        SerializedObject so = new SerializedObject(indicator);
        so.FindProperty("gestureInput").objectReferenceValue = player.GetComponent<GestureInput>();
        so.FindProperty("indicatorText").objectReferenceValue = glyphText;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Raw sensor numbers (millimetres) — between the arrows and the raw
        // key-state grid: what a real distance sensor would report, before
        // it gets thresholded into the Up/Down/braking state shown above.
        const float rawValueHeight = 56f;
        const float rawValueY = glyphY + 100f + 10f;
        var rawValueGo = new GameObject("GestureRawValues");
        rawValueGo.transform.SetParent(canvasGo.transform, false);
        Text rawValueText = rawValueGo.AddComponent<Text>();
        rawValueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rawValueText.fontSize = 40;
        rawValueText.fontStyle = FontStyle.Bold;
        rawValueText.alignment = leftSide ? TextAnchor.LowerLeft : TextAnchor.LowerRight;
        rawValueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        rawValueText.verticalOverflow = VerticalWrapMode.Overflow;
        rawValueText.color = new Color(0.6f, 0.85f, 1f);
        rawValueText.text = "Л:0мм  П:0мм";

        Outline rawValueOutline = rawValueGo.AddComponent<Outline>();
        rawValueOutline.effectColor = Color.black;
        rawValueOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform rawValueRt = rawValueText.GetComponent<RectTransform>();
        rawValueRt.anchorMin = anchor;
        rawValueRt.anchorMax = anchor;
        rawValueRt.pivot = anchor;
        rawValueRt.sizeDelta = new Vector2(440f, rawValueHeight);
        rawValueRt.anchoredPosition = new Vector2(sign * 20f, rawValueY);

        var rawValueIndicatorGo = new GameObject(player.name + "GestureRawValueIndicator");
        GestureRawValueIndicator rawValueIndicator = rawValueIndicatorGo.AddComponent<GestureRawValueIndicator>();
        SerializedObject rawValueSo = new SerializedObject(rawValueIndicator);
        rawValueSo.FindProperty("gestureInput").objectReferenceValue = player.GetComponent<GestureInput>();
        rawValueSo.FindProperty("valueText").objectReferenceValue = rawValueText;
        rawValueSo.ApplyModifiedPropertiesWithoutUndo();

        // Raw key-state squares — one per physical key, laid out as the
        // actual 2x2 keyboard block above the arrows (see GestureGridCol/Row).
        const float squareSize = 56f;
        const float squareStep = 66f; // size + gap
        const float gridBaseY = rawValueY + rawValueHeight + 12f;
        var squareImages = new Image[keyLabels.Length];
        for (int i = 0; i < keyLabels.Length; i++)
        {
            int col = GestureGridCol[i];
            int row = GestureGridRow[i];
            float colFromEdge = leftSide ? col : (1 - col); // mirrored so left-hand column reads left on screen either side
            float x = sign * (20f + colFromEdge * squareStep);
            float y = gridBaseY + (1 - row) * squareStep;
            squareImages[i] = CreateKeySquare(canvasGo.transform, anchor, new Vector2(x, y), squareSize, keyLabels[i]);
        }

        var keyIndicatorGo = new GameObject(player.name + "GestureKeyIndicator");
        GestureKeyIndicator keyIndicator = keyIndicatorGo.AddComponent<GestureKeyIndicator>();
        SerializedObject keySo = new SerializedObject(keyIndicator);
        keySo.FindProperty("gestureInput").objectReferenceValue = player.GetComponent<GestureInput>();
        SerializedProperty squaresProp = keySo.FindProperty("squares");
        squaresProp.arraySize = squareImages.Length;
        for (int i = 0; i < squareImages.Length; i++)
            squaresProp.GetArrayElementAtIndex(i).objectReferenceValue = squareImages[i];
        keySo.ApplyModifiedPropertiesWithoutUndo();

        // Legend text (keys + gesture descriptions), above the grid — bigger
        // than the rest of this HUD since it's meant to be read, not glanced at.
        var legendGo = new GameObject("GestureLegend");
        legendGo.transform.SetParent(canvasGo.transform, false);
        Text legend = legendGo.AddComponent<Text>();
        legend.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        legend.fontSize = 26;
        legend.fontStyle = FontStyle.Bold;
        legend.alignment = leftSide ? TextAnchor.LowerLeft : TextAnchor.LowerRight;
        legend.color = new Color(0.9f, 0.9f, 0.9f);
        legend.text = legendText;

        Outline legendOutline = legendGo.AddComponent<Outline>();
        legendOutline.effectColor = Color.black;
        legendOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform legendRt = legend.GetComponent<RectTransform>();
        legendRt.anchorMin = anchor;
        legendRt.anchorMax = anchor;
        legendRt.pivot = anchor;
        legendRt.sizeDelta = new Vector2(780f, 150f);
        float legendY = gridBaseY + 1f * squareStep + squareSize + 15f;
        legendRt.anchoredPosition = new Vector2(sign * 20f, legendY);

        return canvasGo;
    }

    // One labeled square for GestureKeyIndicator's raw key-press display.
    static Image CreateKeySquare(Transform parent, Vector2 anchor, Vector2 anchoredPosition, float size, string label)
    {
        var go = new GameObject("Key_" + label);
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.75f);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = anchoredPosition;

        var textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        Text text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 26;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        return image;
    }

    // Used to be 6 color variants (random pick from badDuckPrefabs), tinted
    // at runtime via material.color — now a single real-construction-barrier
    // look (red/white hazard stripes baked into the texture itself), so no
    // tinting and just the one prefab.
    static GameObject CreateArchPrefab()
    {
        const string name = "Arch";
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/SmallArch.png");
        if (tex == null)
        {
            Debug.LogWarning("Texture not found: Assets/Sprites/SmallArch.png");
            return null;
        }

        var root = new GameObject(name);
        root.AddComponent<MovingEntity>();
        root.AddComponent<DuckUnderObstacle>();

        // Same physical footprint the old primitive-built arch used — the
        // sprite is stretched to fit it rather than driven by its own
        // aspect ratio, so it still spans the lane the way a gate should.
        float spanWidth = LaneWidth - 0.3f;
        float spriteHeight = 1.6f;

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = "Sprite";
        Object.DestroyImmediate(sprite.GetComponent<Collider>());
        sprite.transform.SetParent(root.transform);
        sprite.transform.localPosition = new Vector3(0f, spriteHeight / 2f, 0f);
        sprite.transform.localScale = new Vector3(spanWidth, spriteHeight, 1f);

        Renderer renderer = sprite.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { mainTexture = tex };

        System.IO.Directory.CreateDirectory("Assets/Materials");
        string materialPath = "Assets/Materials/" + name + ".mat";
        AssetDatabase.DeleteAsset(materialPath); // safe to rerun Rebuild Scene
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;

        // Full-height trigger — ducking bypasses it via PlayerController's
        // DuckUnderObstacle check, not literal geometric clearance.
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(spanWidth, spriteHeight, 0.6f);
        box.center = new Vector3(0f, spriteHeight / 2f, 0f);

        AddStaticGroundShadow(root, spanWidth, 0.4f, name + "_Shadow");

        string savePath = "Assets/Prefabs/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // Same sprite-on-a-quad shape as CreateArchPrefab, but spans the whole
    // road (all lanes) and stands tall enough to walk under normally —
    // jumping into it is what counts as a hit, handled via TallArchObstacle
    // in PlayerController.OnTriggerEnter.
    static GameObject CreateBigArchPrefab()
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/BigArchSign.png");
        if (tex == null)
        {
            Debug.LogWarning("Texture not found: Assets/Sprites/BigArchSign.png");
            return null;
        }

        var root = new GameObject("BigArch");
        root.AddComponent<MovingEntity>();
        root.AddComponent<TallArchObstacle>();

        float aspect = (float)tex.width / tex.height;
        // Wider than the road itself so the posts land out on the roadside,
        // not standing on the outer lanes — otherwise it visually reads as
        // if that lane specifically is what collides with the post, when
        // the actual pass/hit rule is purely about being airborne or not.
        // +4 puts them right where side scenery starts (SideScenerySpawner's
        // sideOffset), i.e. the actual shoulder of the road.
        float spanWidth = LaneCount * LaneWidth + 4f;
        // The image at full aspect-correct height (~8 units) put the
        // crossbar way above a player's jump peak (~2.4 units) — plenty of
        // apparent clearance, so jumping into it read as an arbitrary rule
        // rather than something the player could see coming. Squashed
        // vertically (posts still planted at road level, aspect ratio not
        // preserved) so the crossbar sits visibly closer to jump height.
        float spriteHeight = spanWidth / aspect * 0.78f;

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = "Sprite";
        Object.DestroyImmediate(sprite.GetComponent<Collider>());
        sprite.transform.SetParent(root.transform);
        sprite.transform.localPosition = new Vector3(0f, spriteHeight / 2f, 0f);
        sprite.transform.localScale = new Vector3(spanWidth, spriteHeight, 1f);

        Renderer renderer = sprite.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { mainTexture = tex };

        System.IO.Directory.CreateDirectory("Assets/Materials");
        string materialPath = "Assets/Materials/BigArchSign.mat";
        AssetDatabase.DeleteAsset(materialPath);
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;

        // Full-frame trigger, same as CreateArchPrefab — pass/hit is decided
        // by the player's vertical state in PlayerController, not by literal
        // geometric clearance under the bar.
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(spanWidth, spriteHeight, 0.8f);
        box.center = new Vector3(0f, spriteHeight / 2f, 0f);

        AddStaticGroundShadow(root, spanWidth, 0.6f, "BigArch_Shadow");

        string savePath = "Assets/Prefabs/BigArch.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void CreateBigArchSpawner()
    {
        System.IO.Directory.CreateDirectory("Assets/Prefabs");

        GameObject prefab = CreateBigArchPrefab();

        var spawnerGo = new GameObject("BigArchSpawner");
        BigArchSpawner spawner = spawnerGo.AddComponent<BigArchSpawner>();

        SerializedObject so = new SerializedObject(spawner);
        so.FindProperty("prefab").objectReferenceValue = prefab;
        so.FindProperty("spawnZ").floatValue = RoadCenterZ + RoadLength / 2f - 5f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CreateStartScreen(GameObject playerRight, GameObject playerLeft, GameObject gestureCanvasLeft, GameObject gestureCanvasRight, GameObject topScoresPanel)
    {
        var canvasGo = new GameObject("StartScreenCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // above the score/instructions/win canvases

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // Energetic loop while the menu/instructions carousel is up —
        // stops the instant the game actually starts (BeginGame). Also why
        // SfxManager mutes pickup/hit one-shots until SpeedController says
        // the game is running — this music, not silence, is the intended
        // backdrop for the start screen.
        var musicGo = new GameObject("StartScreenMusic");
        musicGo.transform.SetParent(canvasGo.transform, false);
        AudioSource musicSource = musicGo.AddComponent<AudioSource>();
        musicSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/StartScreenMusic.mp3");
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = 0.5f;

        // Dim backdrop so the menu reads clearly over the (frozen) road.
        var backdropGo = new GameObject("Backdrop");
        backdropGo.transform.SetParent(canvasGo.transform, false);
        Image backdrop = backdropGo.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.55f);
        RectTransform backdropRt = backdropGo.GetComponent<RectTransform>();
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(canvasGo.transform, false);
        Text title = titleGo.AddComponent<Text>();
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.fontSize = 44;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = new Color(1f, 0.85f, 0.2f);
        title.text = "LADYBUG — HIT THE ROAD!";
        titleGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform titleRt = title.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = new Vector2(1400f, 70f);
        titleRt.anchoredPosition = new Vector2(0f, 490f);

        // Info carousel — cycles between rules/controls, trick diagrams and
        // gesture diagrams every few seconds, filling the gap between the
        // title and the menu. One shared-size container; each page is its
        // own child GameObject, only one active at a time (see
        // StartScreenController.UpdateCarousel) — lets some pages be plain
        // text and others be actual schematic diagrams built from UI
        // primitives instead of text. Grown taller (was 200) now that the
        // title is smaller/higher and the menu rows below are smaller/lower.
        var carouselGo = new GameObject("Carousel");
        carouselGo.transform.SetParent(canvasGo.transform, false);
        RectTransform carouselRt = carouselGo.AddComponent<RectTransform>();
        carouselRt.anchorMin = new Vector2(0.5f, 0.5f);
        carouselRt.anchorMax = new Vector2(0.5f, 0.5f);
        carouselRt.pivot = new Vector2(0.5f, 0.5f);
        // Fills essentially all the vertical space between the title and the
        // (now shorter, tighter-packed) button rows — top edge almost
        // touching the title, bottom edge almost touching the button rows —
        // so the winner tables (shown first) read as a real full page
        // instead of a small strip with empty space around it.
        // Widened (was 1200) specifically so the TOP-results title column
        // (leftmost) has room for a category name like "СКОРОСТЬ" on one
        // line — at the old width it had no space to wrap at, so Unity fell
        // back to a mid-word break. See CreateTopResultsPage's matching
        // "700f" (this box's new half-width) in its arrow-geometry math.
        carouselRt.sizeDelta = new Vector2(1400f, 720f);
        carouselRt.anchoredPosition = new Vector2(0f, 90f);

        // Background/frame so the carousel content (especially the winner
        // tables' text+photos) reads clearly against the road behind it,
        // same treatment as the button rows below — kept light (low alpha)
        // so it reads as a tint over the road, not a solid block hiding it.
        var carouselBgGo = new GameObject("CarouselBackground");
        carouselBgGo.transform.SetParent(carouselGo.transform, false);
        carouselBgGo.transform.SetAsFirstSibling(); // behind every page, not on top
        Image carouselBg = carouselBgGo.AddComponent<Image>();
        carouselBg.color = new Color(0f, 0f, 0f, 0.22f);
        Outline carouselBgOutline = carouselBgGo.AddComponent<Outline>();
        carouselBgOutline.effectColor = Color.gray;
        carouselBgOutline.effectDistance = new Vector2(4f, -4f);
        RectTransform carouselBgRt = carouselBgGo.GetComponent<RectTransform>();
        carouselBgRt.anchorMin = Vector2.zero;
        carouselBgRt.anchorMax = Vector2.one;
        carouselBgRt.offsetMin = Vector2.zero;
        carouselBgRt.offsetMax = Vector2.zero;

        // Built separately (not inline in the list below) so the distance
        // line's Text can be grabbed afterward and wired to a live label —
        // "N км Проехат" needs to track WinSequence's actual win distance
        // (temporarily lowered for debug/test runs) instead of a hardcoded
        // number that'd lie while testing. Word order/spelling on this page
        // is deliberately mangled (meme reference, per the user) — verbatim
        // as given, not a typo to fix.
        var goalPage = CreateChecklistPage(carouselRt, "ЦЕЛЬ",
            "100 км проехат",
            "время - уменьшат",
            "очки набират",
            "трюки выполнят",
            "дорога сама разгонят");
        GoalDistanceLabel goalDistanceLabel = goalPage.rowTexts[0].gameObject.AddComponent<GoalDistanceLabel>();
        SerializedObject goalDistanceSo = new SerializedObject(goalDistanceLabel);
        goalDistanceSo.FindProperty("label").objectReferenceValue = goalPage.rowTexts[0];
        goalDistanceSo.ApplyModifiedPropertiesWithoutUndo();

        var carouselPages = new System.Collections.Generic.List<GameObject>
        {
            // Leaderboards first, control/rules instructions after — see
            // each page's own photo row for the winners' pictures.
            CreateTopResultsPage(carouselRt, 0),
            CreateTopResultsPage(carouselRt, 1),
            CreateTopResultsPage(carouselRt, 2),
            CreateTopResultsPage(carouselRt, 3),

            // Word order/spelling on this page is deliberately mangled
            // (meme reference, per the user) — verbatim as given, not a
            // typo to fix.
            CreateChecklistPage(carouselRt, "СУТЬ ИГРЫ",
                "хорошее собират",
                "плохое избегат",
                "трюки вдвоём делат").page,

            goalPage.page,

            CreateObjectGridPage(carouselRt, "ХОРОШИЕ ОБЪЕКТЫ", new Color(0.4f, 1f, 0.5f), GoodObjectNames),
            CreateObjectGridPage(carouselRt, "ПЛОХИЕ ОБЪЕКТЫ", new Color(1f, 0.4f, 0.3f), BadObjectNames),

            // All 4 gesture-with-sensors pages first, УПРАВЛЕНИЕ (which
            // hardware reads which player) right after them — that page no
            // longer repeats the actual moves itself, so it needs these
            // shown first for context.
            CreateGestureDiagramPage(carouselRt, "ПРИСЕСТЬ", false, false, false),

            // Split from the old single shared "В СТОРОНУ" page into its
            // own left/right page each — showing both directions on one
            // page read as "which one am I even looking at" mid-gesture.
            CreateGestureDiagramPage(carouselRt, "ВЛЕВО", false, true, false),
            CreateGestureDiagramPage(carouselRt, "ВПРАВО", true, false, false),

            CreateGestureDiagramPage(carouselRt, "МАХАТЬ КРЫЛЬЯМИ", true, true, true),

            CreateControlsPage(carouselRt),

            CreateArchTrickPage(carouselRt),

            CreateRingTrickPage(carouselRt),

            CreateLeapfrogTrickPage(carouselRt),
            CreateSyncTrickPage(carouselRt),
            CreateHoverTrickPage(carouselRt),
            CreateBigRingTrickPage(carouselRt),
            CreateInfinityTrickPage(carouselRt),
        };

        // Options row container — its Outline is the focus frame for row 0.
        // Lower and tighter to the other two rows than before — no bottom
        // hint text competing for space anymore (removed; the full control
        // scheme is already shown by InstructionsCanvas underneath).
        var rowGo = new GameObject("OptionsRow");
        rowGo.transform.SetParent(canvasGo.transform, false);
        Image rowBg = rowGo.AddComponent<Image>();
        rowBg.color = new Color(1f, 1f, 1f, 0.05f);
        // 0.35 (a third of the texture) fed a ~45px border into Sliced's
        // fixed-pixel-size corners/edges — comparable to or bigger than
        // these rows' own ~80-90px height, so the feather ate almost the
        // whole row and left no solid-looking center at all (read as the
        // yellow frame having shrunk). 0.15 keeps a real soft edge without
        // swallowing the row.
        rowBg.sprite = CreateSoftRectSprite(128, 0.15f);
        rowBg.type = Image.Type.Sliced;
        Outline rowOutline = rowGo.AddComponent<Outline>();
        rowOutline.effectDistance = new Vector2(4f, -4f);
        RectTransform rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 0.5f);
        rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta = new Vector2(780f, 80f); // width widened both sides (was 700) — height already right
        rowRt.anchoredPosition = new Vector2(0f, -320f);

        GameObject option1 = CreateMenuOption(rowGo.transform, "Option1", new Vector2(-180f, 0f), "[X] 1 ИГРОК", 280f, 32, 60f);
        GameObject option2 = CreateMenuOption(rowGo.transform, "Option2", new Vector2(180f, 0f), "[ ] 2 ИГРОКА", 280f, 32, 60f);

        // Controller-type row — its Outline is the focus frame for row 1.
        var controllerRowGo = new GameObject("ControllerRow");
        controllerRowGo.transform.SetParent(canvasGo.transform, false);
        Image controllerRowBg = controllerRowGo.AddComponent<Image>();
        controllerRowBg.color = new Color(1f, 1f, 1f, 0.05f);
        controllerRowBg.sprite = CreateSoftRectSprite(128, 0.15f); // see rowBg's own comment above
        controllerRowBg.type = Image.Type.Sliced;
        Outline controllerRowOutline = controllerRowGo.AddComponent<Outline>();
        controllerRowOutline.effectDistance = new Vector2(4f, -4f);
        RectTransform controllerRowRt = controllerRowGo.GetComponent<RectTransform>();
        controllerRowRt.anchorMin = new Vector2(0.5f, 0.5f);
        controllerRowRt.anchorMax = new Vector2(0.5f, 0.5f);
        controllerRowRt.pivot = new Vector2(0.5f, 0.5f);
        controllerRowRt.sizeDelta = new Vector2(980f, 80f); // width widened both sides (was 900) — height already right
        controllerRowRt.anchoredPosition = new Vector2(0f, -410f);

        // Narrower than the default 360px option box, and spaced a full box
        // width + gap apart, since three of these side by side would
        // otherwise overlap (360 wide but only 280 apart, in a prior version).
        GameObject controller1 = CreateMenuOption(controllerRowGo.transform, "Controller1", new Vector2(-300f, 0f), "[X] КЛАВИАТУРА", 260f, 26, 60f);
        GameObject controller2 = CreateMenuOption(controllerRowGo.transform, "Controller2", new Vector2(0f, 0f), "[ ] ДАТЧИКИ", 260f, 26, 60f);
        GameObject controller3 = CreateMenuOption(controllerRowGo.transform, "Controller3", new Vector2(300f, 0f), "[ ] ИМИТАТОР", 260f, 26, 60f);

        // Start row — same two-layer structure as the other two rows now
        // (outer row frame that tints yellow when focused, inner button
        // that tints green when it's the one that'll fire), not a single
        // merged element with its own dim, inconsistent focus color.
        var startRowGo = new GameObject("StartRow");
        startRowGo.transform.SetParent(canvasGo.transform, false);
        Image startRowBg = startRowGo.AddComponent<Image>();
        startRowBg.color = new Color(1f, 1f, 1f, 0.05f);
        startRowBg.sprite = CreateSoftRectSprite(128, 0.15f); // see rowBg's own comment above
        startRowBg.type = Image.Type.Sliced;
        Outline startRowOutline = startRowGo.AddComponent<Outline>();
        startRowOutline.effectDistance = new Vector2(4f, -4f);
        RectTransform startRowRt = startRowGo.GetComponent<RectTransform>();
        startRowRt.anchorMin = new Vector2(0.5f, 0.5f);
        startRowRt.anchorMax = new Vector2(0.5f, 0.5f);
        startRowRt.pivot = new Vector2(0.5f, 0.5f);
        startRowRt.sizeDelta = new Vector2(420f, 90f); // width widened both sides (was 340) — height already right
        startRowRt.anchoredPosition = new Vector2(0f, -500f);

        GameObject startBtn = CreateMenuOption(startRowGo.transform, "StartButton", Vector2.zero, "СТАРТ", 300f, 32, 60f);

        // Shown only if Start is pressed while "Датчики расстояния" is selected.
        var notImplementedGo = new GameObject("NotImplemented");
        notImplementedGo.transform.SetParent(canvasGo.transform, false);
        Text notImplemented = notImplementedGo.AddComponent<Text>();
        notImplemented.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        notImplemented.fontSize = 24;
        notImplemented.fontStyle = FontStyle.Bold;
        notImplemented.alignment = TextAnchor.MiddleCenter;
        notImplemented.color = new Color(1f, 0.4f, 0.3f);
        notImplementedGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform notImplementedRt = notImplemented.GetComponent<RectTransform>();
        notImplementedRt.anchorMin = new Vector2(0.5f, 0.5f);
        notImplementedRt.anchorMax = new Vector2(0.5f, 0.5f);
        notImplementedRt.pivot = new Vector2(0.5f, 0.5f);
        notImplementedRt.sizeDelta = new Vector2(900f, 50f);
        notImplementedRt.anchoredPosition = new Vector2(0f, -460f);
        notImplementedGo.SetActive(false);

        // Bottom-left free space left behind once the per-player gesture
        // HUDs (КЛАВИШИ/ЖЕСТЫ panels) are hidden for the menu — a short,
        // input-agnostic reminder of how to actually drive THIS menu
        // (works from keyboard, the keyboard gesture simulator, or real
        // sensors alike, see StartScreenController.Update).
        // Narrower (was 700, reached under the controller-selection row's
        // left edge — same class of bug as the page-caption text creeping
        // into the button area below it) and correspondingly taller/smaller
        // so the extra wrapped lines still fit above the canvas bottom edge
        // instead of being clipped by Text's default Truncate overflow.
        var menuHelpGo = new GameObject("MenuHelpText");
        menuHelpGo.transform.SetParent(canvasGo.transform, false);
        Text menuHelp = menuHelpGo.AddComponent<Text>();
        menuHelp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        menuHelp.fontSize = 20;
        menuHelp.fontStyle = FontStyle.Bold;
        menuHelp.alignment = TextAnchor.LowerLeft;
        menuHelp.horizontalOverflow = HorizontalWrapMode.Wrap;
        menuHelp.verticalOverflow = VerticalWrapMode.Overflow;
        menuHelp.color = new Color(0.9f, 0.9f, 0.9f);
        // Split across more, shorter explicit lines rather than 2 long ones
        // relying on auto-wrap — the "(клавиши, имитатор жестов или датчики
        // — любое)" line was dropped entirely, redundant with the
        // controller-selection row directly above this text.
        menuHelp.text = "ВЫБОР:\n"
            + "ВПРАВО ВЛЕВО / ВВЕРХ ВНИЗ\n"
            + "\n"
            + "НАЧАЛО:\n"
            + "выбрать СТАРТ\n"
            + "и взмахнуть руками";
        menuHelpGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform menuHelpRt = menuHelp.GetComponent<RectTransform>();
        menuHelpRt.anchorMin = new Vector2(0f, 0f);
        menuHelpRt.anchorMax = new Vector2(0f, 0f);
        menuHelpRt.pivot = new Vector2(0f, 0f);
        menuHelpRt.sizeDelta = new Vector2(450f, 150f); // verticalOverflow=Overflow handles the rest if 6 short lines run a touch past this
        menuHelpRt.anchoredPosition = new Vector2(30f, 30f);

        // Only the first page starts visible — StartScreenController swaps
        // active pages at runtime (see UpdateCarousel).
        for (int i = 1; i < carouselPages.Count; i++)
            carouselPages[i].SetActive(false);

        var controller = canvasGo.AddComponent<StartScreenController>();
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("canvasRoot").objectReferenceValue = canvasGo;
        SetPrefabArray(so, "carouselPages", carouselPages);
        so.FindProperty("carouselBackground").objectReferenceValue = carouselBgGo;
        so.FindProperty("option1Bg").objectReferenceValue = option1.GetComponent<Image>();
        so.FindProperty("option2Bg").objectReferenceValue = option2.GetComponent<Image>();
        so.FindProperty("option1Text").objectReferenceValue = option1.GetComponentInChildren<Text>();
        so.FindProperty("option2Text").objectReferenceValue = option2.GetComponentInChildren<Text>();
        so.FindProperty("optionsRowOutline").objectReferenceValue = rowOutline;
        so.FindProperty("optionsRowBg").objectReferenceValue = rowBg;
        so.FindProperty("controller1Bg").objectReferenceValue = controller1.GetComponent<Image>();
        so.FindProperty("controller2Bg").objectReferenceValue = controller2.GetComponent<Image>();
        so.FindProperty("controller3Bg").objectReferenceValue = controller3.GetComponent<Image>();
        so.FindProperty("controller1Text").objectReferenceValue = controller1.GetComponentInChildren<Text>();
        so.FindProperty("controller2Text").objectReferenceValue = controller2.GetComponentInChildren<Text>();
        so.FindProperty("controller3Text").objectReferenceValue = controller3.GetComponentInChildren<Text>();
        so.FindProperty("controllerRowOutline").objectReferenceValue = controllerRowOutline;
        so.FindProperty("controllerRowBg").objectReferenceValue = controllerRowBg;
        so.FindProperty("notImplementedText").objectReferenceValue = notImplemented;
        so.FindProperty("startBg").objectReferenceValue = startBtn.GetComponent<Image>();
        so.FindProperty("startOutline").objectReferenceValue = startRowOutline;
        so.FindProperty("startRowBg").objectReferenceValue = startRowBg;
        so.FindProperty("playerRight").objectReferenceValue = playerRight;
        so.FindProperty("playerLeft").objectReferenceValue = playerLeft;
        so.FindProperty("gestureRight").objectReferenceValue = playerRight.GetComponent<GestureInput>();
        so.FindProperty("gestureLeft").objectReferenceValue = playerLeft.GetComponent<GestureInput>();
        so.FindProperty("joystickLeft").objectReferenceValue = playerLeft.GetComponent<JoystickInput>();
        so.FindProperty("gestureCanvasRight").objectReferenceValue = gestureCanvasRight;
        so.FindProperty("gestureCanvasLeft").objectReferenceValue = gestureCanvasLeft;
        so.FindProperty("topScoresPanel").objectReferenceValue = topScoresPanel;
        so.FindProperty("musicSource").objectReferenceValue = musicSource;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Shared visual style for the start-screen's option boxes and Start button.
    static GameObject CreateMenuOption(Transform parent, string name, Vector2 anchoredPos, string label, float width = 360f, int fontSize = 38, float height = 100f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = anchoredPos;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = Color.gray;
        outline.effectDistance = new Vector2(3f, -3f);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        Text text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        return go;
    }

    // A page container that fills its carousel parent exactly — children
    // position themselves within it via their own anchored rects.
    static GameObject CreateFillPage(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    static void CreatePageTitle(Transform parent, string text, Color color)
    {
        var go = new GameObject("Title");
        go.transform.SetParent(parent, false);
        Text title = go.AddComponent<Text>();
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.fontSize = 40;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = color;
        title.text = text;
        go.AddComponent<Outline>().effectColor = Color.black;
        RectTransform rt = title.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1000f, 50f);
        rt.anchoredPosition = new Vector2(0f, 305f);
    }

    static void CreatePageCaption(Transform parent, string text)
    {
        var go = new GameObject("Caption");
        go.transform.SetParent(parent, false);
        Text caption = go.AddComponent<Text>();
        caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        caption.fontSize = 26;
        caption.fontStyle = FontStyle.Bold;
        caption.alignment = TextAnchor.MiddleCenter;
        caption.color = new Color(0.85f, 0.85f, 0.85f);
        caption.text = text;
        go.AddComponent<Outline>().effectColor = Color.black;
        RectTransform rt = caption.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        // Narrower than before on purpose — forces longer captions to wrap
        // onto 2-3 lines instead of one long line stretching almost as wide
        // as the whole box, and moved up a bit so those extra wrapped lines
        // still clear the button row underneath instead of creeping into it.
        rt.sizeDelta = new Vector2(650f, 110f);
        rt.anchoredPosition = new Vector2(0f, -230f);
    }

    // An instructional carousel page: title up top (CreatePageTitle), then
    // each point as its own checklist row — a checked checkbox icon on the
    // left, the point's text on the right — instead of one plain block of
    // centered text. Rows are spread evenly between the title and the
    // bottom of the page regardless of how many points there are. Also
    // returns each row's Text component so a caller can swap one out for a
    // runtime-driven label (see the ЦЕЛЬ page's distance line) without
    // string-matching GameObject names.
    static (GameObject page, Text[] rowTexts) CreateChecklistPage(Transform parent, string title, params string[] lines)
    {
        GameObject page = CreateFillPage(parent, "Page_Checklist_" + title);

        CreatePageTitle(page.transform, title, new Color(1f, 0.85f, 0.2f));

        const float topY = 140f;
        const float bottomY = -300f;
        int n = lines.Length;
        var rowTexts = new Text[n];
        for (int i = 0; i < n; i++)
        {
            float t = n <= 1 ? 0.5f : (float)i / (n - 1);
            float y = Mathf.Lerp(topY, bottomY, t);
            rowTexts[i] = CreateChecklistRow(page.transform, y, lines[i]);
        }

        return (page, rowTexts);
    }

    // One checklist row: a small green "checked" box with a checkmark glyph
    // (left), the point's text (right, left-aligned so lines of different
    // length still start at the same spot). Returns the line's Text
    // component.
    static Text CreateChecklistRow(Transform parent, float y, string text)
    {
        const float checkSize = 60f;
        const float leftPadding = 50f;
        const float textGap = 30f;

        var checkGo = new GameObject("Check");
        checkGo.transform.SetParent(parent, false);
        Image checkImg = checkGo.AddComponent<Image>();
        checkImg.color = new Color(0.15f, 0.55f, 0.2f, 0.95f);
        Outline checkOutline = checkGo.AddComponent<Outline>();
        checkOutline.effectColor = Color.white;
        checkOutline.effectDistance = new Vector2(2f, -2f);
        RectTransform checkRt = checkImg.GetComponent<RectTransform>();
        checkRt.anchorMin = new Vector2(0f, 0.5f);
        checkRt.anchorMax = new Vector2(0f, 0.5f);
        checkRt.pivot = new Vector2(0f, 0.5f);
        checkRt.sizeDelta = new Vector2(checkSize, checkSize);
        checkRt.anchoredPosition = new Vector2(leftPadding, y);

        var markGo = new GameObject("Mark");
        markGo.transform.SetParent(checkGo.transform, false);
        Text mark = markGo.AddComponent<Text>();
        mark.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mark.fontSize = 42;
        mark.fontStyle = FontStyle.Bold;
        mark.alignment = TextAnchor.MiddleCenter;
        mark.color = Color.white;
        mark.text = "✓"; // ✓
        RectTransform markRt = mark.GetComponent<RectTransform>();
        markRt.anchorMin = Vector2.zero;
        markRt.anchorMax = Vector2.one;
        markRt.offsetMin = Vector2.zero;
        markRt.offsetMax = Vector2.zero;

        var lineGo = new GameObject("Line");
        lineGo.transform.SetParent(parent, false);
        Text line = lineGo.AddComponent<Text>();
        line.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        line.fontSize = 40;
        line.fontStyle = FontStyle.Bold;
        line.alignment = TextAnchor.MiddleLeft;
        line.color = Color.white;
        line.text = text;
        lineGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform lineRt = line.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0f, 0.5f);
        lineRt.anchorMax = new Vector2(0f, 0.5f);
        lineRt.pivot = new Vector2(0f, 0.5f);
        lineRt.sizeDelta = new Vector2(1000f, 70f);
        lineRt.anchoredPosition = new Vector2(leftPadding + checkSize + textGap, y);

        return line;
    }

    // Display name + sprite file for every LaneObjects entry with score>0 —
    // shown on the "ХОРОШИЕ ОБЪЕКТЫ" instruction page (see
    // CreateObjectGridPage). Kept as its own list (not derived from
    // LaneObjects at build time) so the display names are real Russian
    // words, not the internal PascalCase identifiers.
    static readonly (string name, string file)[] GoodObjectNames =
    {
        ("Цветок", "Flower.png"),
        ("Сердце", "Heart.png"),
        ("Вишня", "Cherry.png"),
        ("Розовый цветок", "FlowerPink.png"),
        ("Жёлтый цветок", "FlowerYellow.png"),
        ("Белая ромашка", "DaisyWhite.png"),
        ("Розовая ромашка", "DaisyPink.png"),
        ("Подсолнух", "SunflowerYellow.png"),
        ("Жёлтый лотос", "LotusYellow.png"),
        ("Синий лотос", "LotusBlue.png"),
        ("Розовый лотос", "LotusPink.png"),
        ("Звезда", "Star.png"),
        ("Мешок денег", "MoneyBag.png"),
        ("Бочка мёда", "HoneyBarrel.png"),
        ("Конфета", "Candy.png"),
    };

    // Same idea as GoodObjectNames, for score<0 LaneObjects entries — shown
    // on "ПЛОХИЕ ОБЪЕКТЫ".
    static readonly (string name, string file)[] BadObjectNames =
    {
        ("Конус", "TrafficCone.png"),
        ("Колесо", "Wheel.png"),
        ("Велосипед", "Bicycle.png"),
        ("Мотобайк", "Motorbike.png"),
        ("Мотоцикл", "Motorcycle.png"),
        ("Собака", "Dog.png"),
        ("Кошка", "Cat.png"),
        ("Кролик", "Rabbit.png"),
        ("Ворона", "Crow.png"),
        ("Куча песка", "SandPile.png"),
        ("Куча кирпичей", "BrickPile.png"),
        ("Куча дров", "WoodPile.png"),
        ("Куча камней", "RockPile.png"),
    };

    // Width/height multipliers for a few specific icons on the instruction
    // grid that read as too small or oddly proportioned at the grid's
    // otherwise-uniform icon size — called out directly in the plan (money
    // bag/honey barrel bigger overall, rabbit wider, crow taller).
    static readonly System.Collections.Generic.Dictionary<string, Vector2> GridIconSizeOverrides =
        new System.Collections.Generic.Dictionary<string, Vector2>
    {
        { "MoneyBag.png", new Vector2(1.6f, 1.2f) },
        { "HoneyBarrel.png", new Vector2(1.5f, 1.15f) },
        { "Rabbit.png", new Vector2(1.75f, 1.15f) },
        { "Crow.png", new Vector2(1.15f, 1.85f) },
    };

    // Instruction page listing every good/bad LaneObjects entry as an
    // icon+name grid — the real sprites players will actually see on the
    // road, not a text list, so recognizing them at speed is the whole
    // point. Column count fixed at 5; rows follow from however many items
    // are passed in.
    static GameObject CreateObjectGridPage(Transform parent, string title, Color titleColor, (string name, string file)[] items)
    {
        GameObject page = CreateFillPage(parent, "Page_ObjectGrid_" + title);
        CreatePageTitle(page.transform, title, titleColor);

        const int cols = 5;
        int rows = Mathf.CeilToInt(items.Length / (float)cols);

        const float gridTop = 180f;
        const float gridBottom = -330f;
        const float gridLeft = -640f;
        const float gridRight = 640f;
        float cellWidth = (gridRight - gridLeft) / cols;
        float cellHeight = (gridTop - gridBottom) / rows;
        float iconSize = Mathf.Min(cellWidth, cellHeight) * 0.55f;

        for (int i = 0; i < items.Length; i++)
        {
            int col = i % cols;
            int row = i / cols;
            float cx = gridLeft + cellWidth * (col + 0.5f);
            float cy = gridTop - cellHeight * (row + 0.5f);

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + items[i].file);

            var iconGo = new GameObject("Icon_" + items[i].name);
            iconGo.transform.SetParent(page.transform, false);
            RawImage icon = iconGo.AddComponent<RawImage>();
            icon.texture = tex;
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            float aspect = tex != null ? (float)tex.width / tex.height : 1f;
            Vector2 baseIconSize = aspect >= 1f
                ? new Vector2(iconSize, iconSize / aspect)
                : new Vector2(iconSize * aspect, iconSize);
            Vector2 sizeMul = GridIconSizeOverrides.TryGetValue(items[i].file, out var mul) ? mul : Vector2.one;
            Vector2 finalIconSize = Vector2.Scale(baseIconSize, sizeMul);
            iconRt.sizeDelta = finalIconSize;
            iconRt.anchoredPosition = new Vector2(cx, cy + 16f);

            var labelGo = new GameObject("Label_" + items[i].name);
            labelGo.transform.SetParent(page.transform, false);
            Text label = labelGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 24; // was 18 — bigger per feedback
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = items[i].name;
            // Taller box to match the bigger font, and Overflow so a
            // two-line name (box wraps within cellWidth) never gets
            // Truncate-hidden — see CreateWinCheckRow's own comment on why
            // that default wrap mode can blank a line entirely.
            label.verticalOverflow = VerticalWrapMode.Overflow;
            labelGo.AddComponent<Outline>().effectColor = Color.black;
            RectTransform labelRt = label.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0.5f, 0.5f);
            labelRt.anchorMax = new Vector2(0.5f, 0.5f);
            labelRt.pivot = new Vector2(0.5f, 0.5f);
            labelRt.sizeDelta = new Vector2(cellWidth - 10f, 44f);
            labelRt.anchoredPosition = new Vector2(cx, cy - finalIconSize.y / 2f - 12f);
        }

        return page;
    }

    // One "distance sensor" glyph: a vertical laser line with a flat
    // rectangle marking where the palm sits, offset from the glyph's own
    // center by palmOffset (0 = centered/neutral — the animated gesture
    // pages all start here, see GestureDiagramAnimation). scale grows both
    // the laser and the palm together, for CreateGestureDiagramPage's
    // "чуть больше размером" pass without touching CreateControlsPage's own
    // (smaller, static) illustration. tiltAngle rotates only the palm — a
    // static lean for the ВЛЕВО/ВПРАВО diagram pages specifically, positive
    // = tilts left, negative = tilts right; the laser stays vertical always
    // (per feedback: a real sensor's beam doesn't tilt, only the hand
    // reading it does). The palm's own up/down bob (GestureDiagramAnimation)
    // still moves it along the parent's vertical axis regardless of this
    // tilt — anchoredPosition offsets aren't affected by the object's own
    // rotation. Returns the palm's RectTransform so a caller can wire it
    // into a live animation instead of just a static pose.
    static RectTransform CreateSensorGlyph(Transform parent, Vector2 anchoredPos, float palmOffset, float scale = 1f, float tiltAngle = 0f)
    {
        var laserGo = new GameObject("Laser");
        laserGo.transform.SetParent(parent, false);
        Image laser = laserGo.AddComponent<Image>();
        laser.color = new Color(1f, 0.2f, 0.15f, 0.9f);
        RectTransform laserRt = laserGo.GetComponent<RectTransform>();
        laserRt.anchorMin = new Vector2(0.5f, 0.5f);
        laserRt.anchorMax = new Vector2(0.5f, 0.5f);
        laserRt.pivot = new Vector2(0.5f, 0.5f);
        laserRt.sizeDelta = new Vector2(5f * scale, 110f * scale);
        laserRt.anchoredPosition = anchoredPos;

        var palmGo = new GameObject("Palm");
        palmGo.transform.SetParent(parent, false);
        Image palm = palmGo.AddComponent<Image>();
        palm.color = Color.white;
        RectTransform palmRt = palmGo.GetComponent<RectTransform>();
        palmRt.anchorMin = new Vector2(0.5f, 0.5f);
        palmRt.anchorMax = new Vector2(0.5f, 0.5f);
        palmRt.pivot = new Vector2(0.5f, 0.5f);
        palmRt.sizeDelta = new Vector2(70f * scale, 16f * scale);
        palmRt.anchoredPosition = anchoredPos + new Vector2(0f, palmOffset);
        palmRt.localRotation = Quaternion.Euler(0f, 0f, tiltAngle);
        return palmRt;
    }

    // Real generated artwork (yandex_api/gen_asset.sh) — a volumetric
    // red-ball-on-black-base arcade joystick, replacing the earlier flat
    // procedural base/shaft/knob composition (see git history) with
    // something that actually reads as a physical joystick at a glance.
    static void CreateJoystickIcon(Transform parent, Vector2 pos)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Joystick.png");

        var go = new GameObject("JoystickIcon");
        go.transform.SetParent(parent, false);
        RawImage img = go.AddComponent<RawImage>();
        img.texture = tex;
        RectTransform rt = img.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        float aspect = tex != null ? (float)tex.width / tex.height : 0.875f;
        rt.sizeDelta = new Vector2(160f * aspect, 160f);
        rt.anchoredPosition = pos + new Vector2(0f, -5f);
    }

    static void CreateControlsSubLabel(Transform parent, Vector2 pos, string text, int fontSize = 26, float boxWidth = 320f)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        Text label = go.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.text = text;
        go.AddComponent<Outline>().effectColor = Color.black;
        RectTransform rt = label.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(boxWidth, 40f);
        rt.anchoredPosition = pos;
    }

    // УПРАВЛЕНИЕ: keeps the original 2-line keyboard mapping (top/bottom,
    // same spread CreateChecklistPage used for these exact two lines), and
    // adds the two real hardware alternatives in the gap between them — the
    // co-op rig is one board of hand sensors for player 1 (right) and one
    // joystick board for player 2 (left), see GestureSensorSerial/
    // JoystickSerial — same up/down/left-right mapping either way, just
    // different hardware reading it.
    static GameObject CreateControlsPage(Transform parent)
    {
        GameObject page = CreateFillPage(parent, "Page_Controls");

        CreatePageTitle(page.transform, "УПРАВЛЕНИЕ", new Color(1f, 0.85f, 0.2f));

        // No keyboard-mapping rows and no "ТО ЖЕ САМОЕ" line anymore — the
        // gesture pages right before this one in the carousel (see
        // CreateStartScreen's page order) already cover the actual moves in
        // full; this page is purely "which hardware reads which player"
        // now. Each checkbox sits above its own 2-line label stack (not
        // beside a single line, like CreateWinCheckRow's rows) — checkbox,
        // then player, then hardware, read top to bottom.
        // Player columns pushed further out toward the page's own edges
        // (was ±260) so the two rigs read as clearly separate setups
        // instead of crowding the middle — still well clear of the page's
        // ±700 half-width even with the sensor/joystick art's own spread.
        const float playerX = 380f;

        CreateVerticalCheck(page.transform, new Vector2(-playerX, 170f));
        CreateControlsSubLabel(page.transform, new Vector2(-playerX, 110f), "ИГРОК 1");
        CreateControlsSubLabel(page.transform, new Vector2(-playerX, 65f), "ДАТЧИКИ");
        CreateSensorGlyph(page.transform, new Vector2(-playerX - 55f, -60f), -40f);
        CreateSensorGlyph(page.transform, new Vector2(-playerX + 55f, -60f), 40f);

        CreateVerticalCheck(page.transform, new Vector2(playerX, 170f));
        CreateControlsSubLabel(page.transform, new Vector2(playerX, 110f), "ИГРОК 2");
        CreateControlsSubLabel(page.transform, new Vector2(playerX, 65f), "ДЖОЙСТИК");
        CreateJoystickIcon(page.transform, new Vector2(playerX, -60f));

        // Exit instruction — bumped up from the shared 26pt label size and
        // spelled out in full ("СЕКУНД", not the abbreviated "СЕК") so it
        // reads clearly as its own important callout, not just another
        // sub-label like the ones above.
        CreateVerticalCheck(page.transform, new Vector2(0f, -180f));
        CreateControlsSubLabel(page.transform, new Vector2(0f, -240f), "ВЫХОД", 34);
        CreateControlsSubLabel(page.transform, new Vector2(0f, -288f), "ПРИСЕСТЬ ОБОИМ НА 5 СЕКУНД", 34, 620f);

        return page;
    }

    // Standalone checkbox (no attached text line) — same green-checkmark
    // visual CreateWinCheckRow's own checkbox uses, for pages that stack a
    // checkbox above its label(s) instead of beside a single line.
    static void CreateVerticalCheck(Transform parent, Vector2 pos)
    {
        const float checkSize = 56f;

        var checkGo = new GameObject("Check");
        checkGo.transform.SetParent(parent, false);
        Image checkImg = checkGo.AddComponent<Image>();
        checkImg.color = new Color(0.15f, 0.55f, 0.2f, 0.95f);
        Outline checkOutline = checkGo.AddComponent<Outline>();
        checkOutline.effectColor = Color.white;
        checkOutline.effectDistance = new Vector2(2f, -2f);
        RectTransform checkRt = checkImg.GetComponent<RectTransform>();
        checkRt.anchorMin = new Vector2(0.5f, 0.5f);
        checkRt.anchorMax = new Vector2(0.5f, 0.5f);
        checkRt.pivot = new Vector2(0.5f, 0.5f);
        checkRt.sizeDelta = new Vector2(checkSize, checkSize);
        checkRt.anchoredPosition = pos;

        var markGo = new GameObject("Mark");
        markGo.transform.SetParent(checkGo.transform, false);
        Text mark = markGo.AddComponent<Text>();
        mark.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mark.fontSize = Mathf.RoundToInt(checkSize * 0.6f);
        mark.fontStyle = FontStyle.Bold;
        mark.alignment = TextAnchor.MiddleCenter;
        mark.color = Color.white;
        mark.text = "✓";
        RectTransform markRt = mark.GetComponent<RectTransform>();
        markRt.anchorMin = Vector2.zero;
        markRt.anchorMax = Vector2.one;
        markRt.offsetMin = Vector2.zero;
        markRt.offsetMax = Vector2.zero;
    }

    // One gesture, explained as a live animated 2-sensor diagram instead of
    // a static pose: both hands start centered in their beam, then move
    // toward the gesture's target (or oscillate continuously for the flap/
    // jump gesture — see GestureDiagramAnimation), each with its own arrow
    // beside it pointing the direction THAT hand moves. Replaces the old
    // static version's side action-word label (redundant with the page
    // title above it) and bottom caption entirely, per feedback that
    // both just repeated information already on screen.
    static GameObject CreateGestureDiagramPage(Transform parent, string title, bool leftGoesUp, bool rightGoesUp, bool isFlap)
    {
        GameObject page = CreateFillPage(parent, "Page_Gesture_" + title);

        CreatePageTitle(page.transform, title, new Color(1f, 0.85f, 0.2f));

        // Bottom half: the sensor pair, side by side — like the physical
        // control panel on a real arcade cabinet (hand sensors mounted low,
        // screen above) rather than split left/right. 30% bigger than
        // CreateControlsPage's small illustrative pair.
        const float bottomHalfCenterY = -160f;
        const float glyphSpacing = 150f;
        const float glyphScale = 1.3f;
        // ВЛЕВО/ВПРАВО specifically get a static lean on both sensor
        // strips toward that same side — ПРИСЕСТЬ/МАХАТЬ КРЫЛЬЯМИ have no
        // side to lean toward, so they stay upright (0).
        float glyphTilt = 0f;
        if (!isFlap)
        {
            if (leftGoesUp && !rightGoesUp)
                glyphTilt = -18f; // ВПРАВО
            else if (!leftGoesUp && rightGoesUp)
                glyphTilt = 18f; // ВЛЕВО
        }
        RectTransform leftPalm = CreateSensorGlyph(page.transform, new Vector2(-glyphSpacing, bottomHalfCenterY), 0f, glyphScale, glyphTilt);
        RectTransform rightPalm = CreateSensorGlyph(page.transform, new Vector2(glyphSpacing, bottomHalfCenterY), 0f, glyphScale, glyphTilt);

        Color arrowColor = new Color(1f, 0.85f, 0.2f);
        // "↕" (not a plain "↑") for the flap gesture — a fixed "up" arrow
        // read as a static pose, not the repeated up/down motion the actual
        // gesture needs; it also bounces in sync with the palm at runtime
        // (see GestureDiagramAnimation.AnimateFlap) so the *motion* itself
        // carries the rhythm, not just the glyph.
        string leftGlyph = isFlap ? "↕" : (leftGoesUp ? "↑" : "↓");
        string rightGlyph = isFlap ? "↕" : (rightGoesUp ? "↑" : "↓");
        RectTransform leftArrow = CreateSingleArrow(page.transform, leftGlyph, arrowColor, new Vector2(-glyphSpacing - 80f, bottomHalfCenterY));
        RectTransform rightArrow = CreateSingleArrow(page.transform, rightGlyph, arrowColor, new Vector2(glyphSpacing + 80f, bottomHalfCenterY));
        leftArrow.gameObject.SetActive(false);
        rightArrow.gameObject.SetActive(false);

        // Top half: the ladybug that actually performs the resulting move,
        // real in-game poses and all (see GestureDiagramAnimation's class
        // comment) — like the screen above an arcade cabinet's controls,
        // making the abstract sensor reading concrete ("this hand shape
        // means THIS happens to you"). Low enough in the top half that even
        // at the flap animation's peak rise (+bugFlyRise, default 50) the
        // bug's top edge stays clear of the title above it.
        const float topHalfCenterY = 100f;
        const float bugHeight = 200f;
        Texture2D bugTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/LadyBug1.png");
        var bugGo = new GameObject("Bug");
        bugGo.transform.SetParent(page.transform, false);
        RawImage bugImage = bugGo.AddComponent<RawImage>();
        bugImage.texture = bugTex;
        RectTransform bugRt = bugImage.GetComponent<RectTransform>();
        bugRt.anchorMin = new Vector2(0.5f, 0.5f);
        bugRt.anchorMax = new Vector2(0.5f, 0.5f);
        bugRt.pivot = new Vector2(0.5f, 0.5f);
        float bugAspect = bugTex != null ? (float)bugTex.width / bugTex.height : 1f;
        bugRt.sizeDelta = new Vector2(bugHeight * bugAspect, bugHeight);
        bugRt.anchoredPosition = new Vector2(0f, topHalfCenterY);

        GestureDiagramAnimation anim = page.AddComponent<GestureDiagramAnimation>();
        SerializedObject so = new SerializedObject(anim);
        so.FindProperty("leftPalm").objectReferenceValue = leftPalm;
        so.FindProperty("rightPalm").objectReferenceValue = rightPalm;
        so.FindProperty("leftArrow").objectReferenceValue = leftArrow;
        so.FindProperty("rightArrow").objectReferenceValue = rightArrow;
        so.FindProperty("leftTargetOffset").floatValue = leftGoesUp ? 50f : -50f;
        so.FindProperty("rightTargetOffset").floatValue = rightGoesUp ? 50f : -50f;
        so.FindProperty("isFlap").boolValue = isFlap;
        so.FindProperty("bugImage").objectReferenceValue = bugImage;
        so.FindProperty("bugNormalTexture").objectReferenceValue = bugTex;
        so.FindProperty("bugAirTexture1").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/LadyBug1Air1.png");
        so.FindProperty("bugAirTexture2").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/LadyBug1Air2.png");
        so.ApplyModifiedPropertiesWithoutUndo();

        return page;
    }

    // The АРКА trick, animated rather than a static diagram: one ladybug
    // low (ducks), one high (jumps) — direction arrows appear next to each,
    // they react, an arch grows in from the distance and passes between
    // them, then a "trick complete" line flashes. No caption/static
    // per-icon labels — the animation itself carries the explanation.
    // See ArchTrickAnimation.cs for the actual playback.
    static GameObject CreateArchTrickPage(Transform parent)
    {
        GameObject page = CreateFillPage(parent, "Page_Trick_ArchAnim");
        // Clips the arch to the page bounds once it grows past archMid* —
        // it used to spill past the visible frame at the biggest sizes
        // instead of just filling it.
        page.AddComponent<RectMask2D>();
        CreatePageTitle(page.transform, "ТРЮК: АРКА", new Color(0.7f, 0.4f, 1f));

        const float bugHeight = 150f;
        const float bugY = 130f;
        const float arrowXOffset = 210f;

        RectTransform bottomBug = CreateTrickBugIcon(page.transform, "LadyBug1.png", new Vector2(0f, -bugY), bugHeight);
        RectTransform topBug = CreateTrickBugIcon(page.transform, "LadyBug2.png", new Vector2(0f, bugY), bugHeight);

        GameObject downArrows = CreateArrowPair(page.transform, "DownArrows", "↓", new Color(1f, 0.85f, 0.2f), -bugY, arrowXOffset);
        GameObject upArrows = CreateArrowPair(page.transform, "UpArrows", "↑", new Color(1f, 0.85f, 0.2f), bugY, arrowXOffset);

        Texture2D archTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/SmallArch.png");
        var archGo = new GameObject("Arch");
        archGo.transform.SetParent(page.transform, false);
        RawImage archImg = archGo.AddComponent<RawImage>();
        archImg.texture = archTex;
        RectTransform archRt = archImg.GetComponent<RectTransform>();
        archRt.anchorMin = new Vector2(0.5f, 0.5f);
        archRt.anchorMax = new Vector2(0.5f, 0.5f);
        archRt.pivot = new Vector2(0.5f, 0.5f);
        archRt.anchoredPosition = Vector2.zero;
        archGo.SetActive(false);

        var successGo = new GameObject("SuccessText");
        successGo.transform.SetParent(page.transform, false);
        Text successText = successGo.AddComponent<Text>();
        successText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        successText.fontSize = 44;
        successText.fontStyle = FontStyle.Bold;
        successText.alignment = TextAnchor.MiddleCenter;
        successText.color = new Color(0.4f, 1f, 0.5f);
        successText.text = "ТРЮК ВЫПОЛНЕН +1";
        successGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform successRt = successText.GetComponent<RectTransform>();
        successRt.anchorMin = new Vector2(0.5f, 0.5f);
        successRt.anchorMax = new Vector2(0.5f, 0.5f);
        successRt.pivot = new Vector2(0.5f, 0.5f);
        successRt.sizeDelta = new Vector2(700f, 80f);
        successRt.anchoredPosition = new Vector2(0f, -300f);
        successGo.SetActive(false);

        ArchTrickAnimation anim = page.AddComponent<ArchTrickAnimation>();
        SerializedObject so = new SerializedObject(anim);
        so.FindProperty("bottomBug").objectReferenceValue = bottomBug;
        so.FindProperty("topBug").objectReferenceValue = topBug;
        so.FindProperty("downArrows").objectReferenceValue = downArrows;
        so.FindProperty("upArrows").objectReferenceValue = upArrows;
        so.FindProperty("arch").objectReferenceValue = archRt;
        so.FindProperty("successText").objectReferenceValue = successGo;
        so.FindProperty("topBugAirTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/LadyBug2Air1.png");
        so.ApplyModifiedPropertiesWithoutUndo();

        return page;
    }

    // Only two tricks actually exist in the game (see PlayerController's
    // TryDetectRingTrick/AwardArchTrickOnce, and AchievementStats'
    // RingTricks/ArchTricks — nothing else spawns a trick popup) — this is
    // КОЛЬЦО's animated instruction page, same slide-by-slide treatment as
    // CreateArchTrickPage/ArchTrickAnimation replaces the old static
    // two-icon diagram with.
    static GameObject CreateRingTrickPage(Transform parent)
    {
        GameObject page = CreateFillPage(parent, "Page_Trick_RingAnim");
        CreatePageTitle(page.transform, "ТРЮК: КОЛЬЦО", new Color(0.7f, 0.4f, 1f));

        const float bugHeight = 150f;
        const float bugX = 220f;
        const float arrowY = 110f;

        // Big dashed oval behind the bugs — roughly the shape of the whole
        // rise/cross/come-down route, not a literal trace of it.
        const float ovalYRadius = arrowY * 1.4f;
        System.Func<float, Vector2> oval = t =>
        {
            float angle = t * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(angle) * bugX * 1.6f, Mathf.Sin(angle) * ovalYRadius);
        };
        // No arrowhead — a plain closed oval, not a directional cue (see
        // CreateDashedRouteBackdrop's own showArrowheads comment).
        CreateDashedRouteBackdrop(page.transform, null, 1400f, false, oval);

        // Both bugs start right at the oval's own bottom point, not y=0 —
        // the whole rise/cross/come-down animation is built relative to
        // wherever these two start (RingTrickAnimation.Awake reads their
        // position directly), so this lines the entire loop up with the
        // route drawn behind it instead of starting mid-air relative to it.
        float startY = -ovalYRadius;
        RectTransform airBug = CreateTrickBugIcon(page.transform, "LadyBug1.png", new Vector2(-bugX, startY), bugHeight);
        RectTransform groundBug = CreateTrickBugIcon(page.transform, "LadyBug2.png", new Vector2(bugX, startY), bugHeight);

        // Single reusable arrow per bug — glyph and position are swapped
        // per beat by RingTrickAnimation itself (up, then sideways in each
        // bug's own direction, then down), not a fixed pair shown throughout.
        Color arrowColor = new Color(1f, 0.85f, 0.2f);
        RectTransform airArrow = CreateSingleArrow(page.transform, "↑", arrowColor, new Vector2(-bugX, arrowY));
        RectTransform groundArrow = CreateSingleArrow(page.transform, "←", arrowColor, new Vector2(bugX, -arrowY));
        airArrow.gameObject.SetActive(false);
        groundArrow.gameObject.SetActive(false);

        var successGo = new GameObject("SuccessText");
        successGo.transform.SetParent(page.transform, false);
        Text successText = successGo.AddComponent<Text>();
        successText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        successText.fontSize = 44;
        successText.fontStyle = FontStyle.Bold;
        successText.alignment = TextAnchor.MiddleCenter;
        successText.color = new Color(0.4f, 1f, 0.5f);
        successText.text = "ТРЮК ВЫПОЛНЕН +1";
        successGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform successRt = successText.GetComponent<RectTransform>();
        successRt.anchorMin = new Vector2(0.5f, 0.5f);
        successRt.anchorMax = new Vector2(0.5f, 0.5f);
        successRt.pivot = new Vector2(0.5f, 0.5f);
        successRt.sizeDelta = new Vector2(700f, 80f);
        successRt.anchoredPosition = new Vector2(0f, -300f);
        successGo.SetActive(false);

        RingTrickAnimation anim = page.AddComponent<RingTrickAnimation>();
        SerializedObject ringSo = new SerializedObject(anim);
        ringSo.FindProperty("airBug").objectReferenceValue = airBug;
        ringSo.FindProperty("groundBug").objectReferenceValue = groundBug;
        ringSo.FindProperty("airArrow").objectReferenceValue = airArrow;
        ringSo.FindProperty("groundArrow").objectReferenceValue = groundArrow;
        ringSo.FindProperty("arrowOffset").vector2Value = new Vector2(0f, 115f);
        // Rise (then matching come-down) now spans the oval's full bottom-
        // to-top height, not the script's small default — both bugs start
        // at the oval's bottom point (startY above), so this lines the
        // rise up with the route drawn behind it instead of stopping well
        // short of the top.
        ringSo.FindProperty("arcHeight").floatValue = 2f * ovalYRadius;
        ringSo.FindProperty("successText").objectReferenceValue = successGo;
        ringSo.FindProperty("airBugAirTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/LadyBug1Air1.png");
        ringSo.ApplyModifiedPropertiesWithoutUndo();

        return page;
    }

    // Shared page builder for the 5 newer multi-step tricks (see
    // TrickDiagramAnimation) — builds the two bug icons at their path's
    // first waypoint plus the success line, then hands the scripted paths
    // straight to the component. TrickDiagramAnimation's fields are plain
    // public fields, not SerializeField+SerializedObject like
    // ArchTrickAnimation's — a Step[] path is exactly the kind of data
    // that's painful to round-trip through SerializedProperty, and it
    // needs no Inspector visibility since the scene is always regenerated
    // fresh from here.
    static GameObject CreateTrickDiagramPage(Transform parent, string title, string spriteA, string spriteB,
        string airTextureA, string airTextureB, TrickDiagramAnimation.Step[] pathA, TrickDiagramAnimation.Step[] pathB,
        float staggerDelay = 0f, (Vector2 pos, float radius)[] routeDots = null, float laneSpacing = 210f,
        bool showArrowheads = true, params System.Func<float, Vector2>[] routeCurves)
    {
        GameObject page = CreateFillPage(parent, "Page_Trick_" + title);
        CreatePageTitle(page.transform, "ТРЮК: " + title, new Color(0.7f, 0.4f, 1f));

        if ((routeCurves != null && routeCurves.Length > 0) || (routeDots != null && routeDots.Length > 0))
            CreateDashedRouteBackdrop(page.transform, routeDots, 1400f, showArrowheads, routeCurves);

        const float bugHeight = 150f;

        Vector2 startA = new Vector2((pathA[0].lane - 1) * laneSpacing, pathA[0].y);
        Vector2 startB = new Vector2((pathB[0].lane - 1) * laneSpacing, pathB[0].y);
        RectTransform bugA = CreateTrickBugIcon(page.transform, spriteA, startA, bugHeight);
        RectTransform bugB = CreateTrickBugIcon(page.transform, spriteB, startB, bugHeight);

        // Small — half the size CreateSingleArrow's other callers (Arch/
        // Ring's own big directional cues) use, since these ride right next
        // to the bug on every little step rather than standing alone.
        Color arrowColor = new Color(1f, 0.85f, 0.2f);
        RectTransform arrowA = CreateSingleArrow(page.transform, "→", arrowColor, startA);
        arrowA.localScale = Vector3.one * 0.5f;
        arrowA.gameObject.SetActive(false);
        RectTransform arrowB = CreateSingleArrow(page.transform, "→", arrowColor, startB);
        arrowB.localScale = Vector3.one * 0.5f;
        arrowB.gameObject.SetActive(false);

        var successGo = new GameObject("SuccessText");
        successGo.transform.SetParent(page.transform, false);
        Text successText = successGo.AddComponent<Text>();
        successText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        successText.fontSize = 44;
        successText.fontStyle = FontStyle.Bold;
        successText.alignment = TextAnchor.MiddleCenter;
        successText.color = new Color(0.4f, 1f, 0.5f);
        successText.text = "ТРЮК ВЫПОЛНЕН +1";
        successGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform successRt = successText.GetComponent<RectTransform>();
        successRt.anchorMin = new Vector2(0.5f, 0.5f);
        successRt.anchorMax = new Vector2(0.5f, 0.5f);
        successRt.pivot = new Vector2(0.5f, 0.5f);
        successRt.sizeDelta = new Vector2(700f, 80f);
        successRt.anchoredPosition = new Vector2(0f, -300f);
        successGo.SetActive(false);

        TrickDiagramAnimation anim = page.AddComponent<TrickDiagramAnimation>();
        anim.bugA = bugA;
        anim.bugB = bugB;
        anim.pathA = pathA;
        anim.pathB = pathB;
        anim.arrowA = arrowA;
        anim.arrowB = arrowB;
        anim.staggerDelay = staggerDelay;
        anim.successText = successGo;
        anim.laneSpacing = laneSpacing;
        anim.airTextureA = string.IsNullOrEmpty(airTextureA) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + airTextureA);
        anim.airTextureB = string.IsNullOrEmpty(airTextureB) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + airTextureB);

        return page;
    }

    // ЧЕХАРДА: both start stacked in an edge lane (A riding on B). A
    // dismounts to the middle lane sideways first, THEN comes down — two
    // separate steps, not one diagonal move — freeing B to clear both
    // remaining lanes in one continuous airborne hop, from the edge lane
    // straight to the opposite edge, never landing in between.
    static GameObject CreateLeapfrogTrickPage(Transform parent)
    {
        // Bigger vertical spread overall (per feedback) — ground pushed
        // lower and both air heights pushed higher than before (was 0/90/110),
        // and the arcs below share these same landmarks so the whole page
        // (routes + actual bug heights) scales together, not just the
        // routes on their own.
        const float groundY = -40f;
        const float riderAirY = 160f; // pathA's riding height
        const float hopAirY = 130f;   // pathB's leapfrog hop height
        var pathA = new[]
        {
            new TrickDiagramAnimation.Step { lane = 0, y = riderAirY, airborne = true,  travelDuration = 0f,    holdDuration = 1.0f },
            new TrickDiagramAnimation.Step { lane = 1, y = riderAirY, airborne = true,  travelDuration = 0.35f, holdDuration = 0.35f },
            new TrickDiagramAnimation.Step { lane = 1, y = groundY,   airborne = false, travelDuration = 0.3f,  holdDuration = 1.6f },
        };
        // Straight up first (still lane 0), THEN sideways at that height —
        // two separate steps, not one diagonal move like before.
        var pathB = new[]
        {
            new TrickDiagramAnimation.Step { lane = 0, y = groundY, airborne = false, travelDuration = 0f,    holdDuration = 1.8f },
            new TrickDiagramAnimation.Step { lane = 0, y = hopAirY, airborne = true,  travelDuration = 0.25f, holdDuration = 0.1f },
            new TrickDiagramAnimation.Step { lane = 1, y = hopAirY, airborne = true,  travelDuration = 0.3f,  holdDuration = 0.1f },
            new TrickDiagramAnimation.Step { lane = 2, y = hopAirY, airborne = true,  travelDuration = 0.3f,  holdDuration = 0.15f },
            new TrickDiagramAnimation.Step { lane = 2, y = groundY, airborne = false, travelDuration = 0.3f,  holdDuration = 0.6f },
        };
        // Two big arcs — a stylized crossing motif for the leapfrog shape,
        // not a literal trace of either bug's own path. Stretched almost to
        // the page's own edges horizontally (was ±260) to match the other
        // trick pages' own routes, with laneSpacing widened to match
        // (below) so the bugs' own lane-to-lane movement spans the same
        // width as the route drawn behind them. Starts sit at the same
        // riding/below-ground heights as the bugs above; both end at
        // groundY — arc1 stops in the middle (its own arrowhead lands
        // there); arc2 still runs the full width to the opposite edge and
        // has its own (much taller) bulge, see arc2Bulge below.
        const float wideLaneSpacing = 480f;
        System.Func<float, Vector2> sharedBulge = t => new Vector2(0f, Mathf.Sin(t * Mathf.PI) * 75f);
        System.Func<float, Vector2> arc1 = t =>
            Vector2.Lerp(new Vector2(-620f, riderAirY), new Vector2(0f, groundY), t) + sharedBulge(t);
        // arc2 (the long one, full width) gets its own taller bulge instead
        // of the shared one — its own midpoint sits at groundY-10, so this
        // amplitude puts its peak (at t≈0.5) exactly at riderAirY, the
        // same highest point the bug itself actually reaches, per feedback.
        float arc2BulgeAmplitude = riderAirY - (groundY - 10f);
        System.Func<float, Vector2> arc2Bulge = t => new Vector2(0f, Mathf.Sin(t * Mathf.PI) * arc2BulgeAmplitude);
        System.Func<float, Vector2> arc2 = t =>
            Vector2.Lerp(new Vector2(-620f, groundY - 20f), new Vector2(620f, groundY), t) + arc2Bulge(t);
        return CreateTrickDiagramPage(parent, "ЧЕХАРДА", "LadyBug2.png", "LadyBug1.png",
            "LadyBug2Air1.png", "LadyBug1Air1.png", pathA, pathB, 0f, null, wideLaneSpacing, true, arc1, arc2);
    }

    // СИНХРОН: same stacked start as ЧЕХАРДА, but this time the pair never
    // separates — both move together, 2 lane-steps, straight over to the
    // opposite edge lane, still stacked at the end. Base sits well below
    // the rider (110 vs -70, more than their combined half-heights apart)
    // so the two icons don't visually overlap.
    static GameObject CreateSyncTrickPage(Transform parent)
    {
        // 2 separate steps with a real pause between them, not one
        // continuous slide — matches the route below now being 2 distinct
        // arrow segments per row (start-to-dot, dot-to-end) rather than one
        // plain line with a dot on it.
        var pathA = new[]
        {
            new TrickDiagramAnimation.Step { lane = 0, y = 110f, airborne = true, travelDuration = 0f,    holdDuration = 1.0f },
            new TrickDiagramAnimation.Step { lane = 1, y = 110f, airborne = true, travelDuration = 0.35f, holdDuration = 0.35f },
            new TrickDiagramAnimation.Step { lane = 2, y = 110f, airborne = true, travelDuration = 0.35f, holdDuration = 0.6f },
        };
        var pathB = new[]
        {
            new TrickDiagramAnimation.Step { lane = 0, y = -70f, airborne = false, travelDuration = 0f,    holdDuration = 1.0f },
            new TrickDiagramAnimation.Step { lane = 1, y = -70f, airborne = false, travelDuration = 0.35f, holdDuration = 0.35f },
            new TrickDiagramAnimation.Step { lane = 2, y = -70f, airborne = false, travelDuration = 0.35f, holdDuration = 0.6f },
        };
        // 2 big horizontal arrows per row — start-to-dot and dot-to-end as
        // 2 separate dashed segments, not one plain line with a dot on it.
        // Stretched almost to the page's own edges horizontally (was ±260),
        // with laneSpacing widened to match (below) so the route and the
        // bugs' own lane-to-lane movement span the same width.
        const float wideLaneSpacing = 480f;
        // rowA1/rowB1 stop a bit short of the dot (dotGap) instead of
        // landing exactly on its center — otherwise their own arrowhead
        // (see CreateDashedPathTexture) draws right on top of the dot and
        // the two visually merge into one blob. rowA2/rowB2 don't need
        // this — their arrowhead is out at the far edge, nowhere near the dot.
        const float dotGap = 34f;
        System.Func<float, Vector2> rowA1 = t => Vector2.Lerp(new Vector2(-620f, 110f), new Vector2(-dotGap, 110f), t);
        System.Func<float, Vector2> rowA2 = t => Vector2.Lerp(new Vector2(0f, 110f), new Vector2(620f, 110f), t);
        System.Func<float, Vector2> rowB1 = t => Vector2.Lerp(new Vector2(-620f, -70f), new Vector2(-dotGap, -70f), t);
        System.Func<float, Vector2> rowB2 = t => Vector2.Lerp(new Vector2(0f, -70f), new Vector2(620f, -70f), t);
        var dots = new (Vector2, float)[] { (new Vector2(0f, 110f), 16f), (new Vector2(0f, -70f), 16f) };
        return CreateTrickDiagramPage(parent, "СИНХРОН", "LadyBug2.png", "LadyBug1.png",
            "LadyBug2Air1.png", null, pathA, pathB, 0f, dots, wideLaneSpacing, true, rowA1, rowA2, rowB1, rowB2);
    }

    // ЗАВИСАНИЕ: both jump up (each in their own lane) and stay up, bobbing
    // in place without ever touching down, before landing together —
    // holding Up (or tapping it again right as a jump would otherwise end)
    // keeps chaining into another jump as long as a partner is also
    // airborne, see PlayerController.UpdateVerticalState.
    static GameObject CreateHoverTrickPage(Transform parent)
    {
        // Each counted digit is one full up+down bounce (0.2s up-travel +
        // 0.05s hold at the peak + 0.2s down-travel + 0.05s hold at the
        // base = 0.5s), and counterStart/counterEnd further down are set to
        // line up with the FIRST of each pair's up-travel exactly — so the
        // digit only advances when the up arrow fires (per-step arrows are
        // already automatic, see TrickDiagramAnimation's own ArrowGlyph),
        // and stays the same all through the down bounce right after it,
        // instead of changing on every single step regardless of direction.
        const float baseY = 90f;
        const float peakY = 130f;
        const float bounceTravel = 0.2f;
        const float bounceHold = 0.05f;
        var pathA = new[]
        {
            new TrickDiagramAnimation.Step { lane = 0, y = 0f,    airborne = false, travelDuration = 0f,    holdDuration = 0.6f },
            new TrickDiagramAnimation.Step { lane = 0, y = baseY, airborne = true,  travelDuration = 0.3f,  holdDuration = 0.1f }, // rise to hover height — no counter yet
            new TrickDiagramAnimation.Step { lane = 0, y = peakY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold }, // up — "1"
            new TrickDiagramAnimation.Step { lane = 0, y = baseY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold, hideArrow = true }, // down — still "1"
            new TrickDiagramAnimation.Step { lane = 0, y = peakY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold }, // up — "2"
            new TrickDiagramAnimation.Step { lane = 0, y = baseY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold, hideArrow = true }, // down — still "2"
            new TrickDiagramAnimation.Step { lane = 0, y = peakY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold }, // up — "3"
            new TrickDiagramAnimation.Step { lane = 0, y = baseY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold, hideArrow = true }, // down — still "3"
            new TrickDiagramAnimation.Step { lane = 0, y = peakY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold }, // up — "4"
            new TrickDiagramAnimation.Step { lane = 0, y = baseY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold, hideArrow = true }, // down — still "4"
            new TrickDiagramAnimation.Step { lane = 0, y = peakY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold }, // up — "5"
            new TrickDiagramAnimation.Step { lane = 0, y = baseY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold, hideArrow = true }, // down — still "5"
            new TrickDiagramAnimation.Step { lane = 0, y = 0f,    airborne = false, travelDuration = 0.3f,  holdDuration = 0.6f, hideArrow = true },
        };
        var pathB = new[]
        {
            new TrickDiagramAnimation.Step { lane = 2, y = 0f,    airborne = false, travelDuration = 0f,    holdDuration = 0.6f },
            new TrickDiagramAnimation.Step { lane = 2, y = baseY, airborne = true,  travelDuration = 0.3f,  holdDuration = 0.1f },
            new TrickDiagramAnimation.Step { lane = 2, y = peakY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold },
            new TrickDiagramAnimation.Step { lane = 2, y = baseY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold, hideArrow = true },
            new TrickDiagramAnimation.Step { lane = 2, y = peakY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold },
            new TrickDiagramAnimation.Step { lane = 2, y = baseY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold, hideArrow = true },
            new TrickDiagramAnimation.Step { lane = 2, y = peakY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold },
            new TrickDiagramAnimation.Step { lane = 2, y = baseY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold, hideArrow = true },
            new TrickDiagramAnimation.Step { lane = 2, y = peakY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold },
            new TrickDiagramAnimation.Step { lane = 2, y = baseY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold, hideArrow = true },
            new TrickDiagramAnimation.Step { lane = 2, y = peakY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold },
            new TrickDiagramAnimation.Step { lane = 2, y = baseY, airborne = true,  travelDuration = bounceTravel, holdDuration = bounceHold, hideArrow = true },
            new TrickDiagramAnimation.Step { lane = 2, y = 0f,    airborne = false, travelDuration = 0.3f,  holdDuration = 0.6f, hideArrow = true },
        };
        GameObject page = CreateTrickDiagramPage(parent, "ЗАВИСАНИЕ", "LadyBug1.png", "LadyBug2.png",
            "LadyBug1Air1.png", "LadyBug2Air1.png", pathA, pathB);

        var counterGo = new GameObject("HoverCounter");
        counterGo.transform.SetParent(page.transform, false);
        Text counterText = counterGo.AddComponent<Text>();
        counterText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        counterText.fontSize = 60;
        counterText.fontStyle = FontStyle.Bold;
        counterText.alignment = TextAnchor.MiddleCenter;
        counterText.color = new Color(1f, 0.85f, 0.2f);
        counterGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform counterRt = counterText.GetComponent<RectTransform>();
        counterRt.anchorMin = new Vector2(0.5f, 0.5f);
        counterRt.anchorMax = new Vector2(0.5f, 0.5f);
        counterRt.pivot = new Vector2(0.5f, 0.5f);
        counterRt.sizeDelta = new Vector2(100f, 100f);
        counterRt.anchoredPosition = new Vector2(0f, 105f); // between the two bugs (lane0/lane2), roughly at their bobbing height
        counterGo.SetActive(false);

        TrickDiagramAnimation anim = page.GetComponent<TrickDiagramAnimation>();
        anim.counterText = counterText;
        // First rise plays with no counter at all (0.6 hold + 0.3 travel +
        // 0.1 hold = 1.0), THEN "1 2 3 4 5" starts — lined up exactly with
        // each pair's up-travel above (not the down-travel right after it),
        // so the digit only advances on the up bounce.
        anim.counterStart = 0.6f + 0.3f + 0.1f;
        anim.counterEnd = anim.counterStart + 5f * (bounceTravel + bounceHold) * 2f;

        return page;
    }

    // БОЛЬШОЕ КОЛЬЦО: one after another, not simultaneous — B starts a
    // beat after A (staggerDelay) instead of moving in lockstep with it.
    // Both sweep 2 lanes one way along the ground, then 2 lanes back the
    // other way in the air — a there-and-back loop across all 3 lanes.
    // Wide laneSpacing (below) matches the oval route's own reach instead
    // of a narrow band in the page's center, and B starts already at the
    // middle lane (rather than stacked on A's own edge start) so the two
    // icons read as distinct from the very first frame, not overlapping.
    static GameObject CreateBigRingTrickPage(Transform parent)
    {
        const float wideLaneSpacing = 480f;
        // Full 7-step there-and-back loop, both bugs running it
        // simultaneously (no staggerDelay) — B's own loop is the same
        // shape as A's, just entered starting from a different point
        // (center instead of the edge), so they read as distinct from the
        // first frame without needing a time offset.
        // Ground/air heights shared identically by both bugs (no more A-
        // above-B offset — per feedback they should land exactly on top of
        // each other when at the same height, not visibly staggered) and
        // pushed past the oval's own ±95 vertical reach so the low point
        // sits below the oval's bottom and the high point sits above its
        // top, instead of within it.
        const float ovalYRadius = 95f;
        const float groundY = -(ovalYRadius + 15f);
        const float airY = ovalYRadius + 15f;
        var pathA = new[]
        {
            new TrickDiagramAnimation.Step { lane = 0, y = groundY, airborne = false, travelDuration = 0f,    holdDuration = 0.8f },
            new TrickDiagramAnimation.Step { lane = 1, y = groundY, airborne = false, travelDuration = 0.35f, holdDuration = 0.7f },
            new TrickDiagramAnimation.Step { lane = 2, y = groundY, airborne = false, travelDuration = 0.35f, holdDuration = 0.7f },
            new TrickDiagramAnimation.Step { lane = 2, y = airY,    airborne = true,  travelDuration = 0.35f, holdDuration = 0.2f },
            new TrickDiagramAnimation.Step { lane = 1, y = airY,    airborne = true,  travelDuration = 0.35f, holdDuration = 0.4f },
            new TrickDiagramAnimation.Step { lane = 0, y = airY,    airborne = true,  travelDuration = 0.35f, holdDuration = 0.6f },
            new TrickDiagramAnimation.Step { lane = 0, y = groundY, airborne = false, travelDuration = 0.3f,  holdDuration = 0.8f },
        };
        var pathB = new[]
        {
            new TrickDiagramAnimation.Step { lane = 1, y = groundY, airborne = false, travelDuration = 0f,    holdDuration = 0.8f },
            new TrickDiagramAnimation.Step { lane = 2, y = groundY, airborne = false, travelDuration = 0.35f, holdDuration = 0.7f },
            new TrickDiagramAnimation.Step { lane = 2, y = airY,    airborne = true,  travelDuration = 0.35f, holdDuration = 0.7f },
            new TrickDiagramAnimation.Step { lane = 1, y = airY,    airborne = true,  travelDuration = 0.35f, holdDuration = 0.2f },
            new TrickDiagramAnimation.Step { lane = 0, y = airY,    airborne = true,  travelDuration = 0.35f, holdDuration = 0.4f },
            new TrickDiagramAnimation.Step { lane = 0, y = groundY, airborne = false, travelDuration = 0.3f,  holdDuration = 0.6f },
            new TrickDiagramAnimation.Step { lane = 1, y = groundY, airborne = false, travelDuration = 0.3f,  holdDuration = 0.8f },
        };
        // Big flattened oval traced behind them, stretched almost to the
        // page's own edges horizontally — the overall shape of the there-
        // and-back loop, not a literal trace of either bug's zigzag.
        System.Func<float, Vector2> oval = t =>
        {
            float angle = t * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(angle) * 650f, Mathf.Sin(angle) * ovalYRadius);
        };
        return CreateTrickDiagramPage(parent, "БОЛЬШОЕ КОЛЬЦО", "LadyBug1.png", "LadyBug2.png",
            "LadyBug1Air1.png", "LadyBug2Air1.png", pathA, pathB, 0f, null, wideLaneSpacing, true, oval);
    }

    // БЕСКОНЕЧНОСТЬ: both bugs run the same figure-8 shape simultaneously
    // (no staggerDelay) — B's own loop is A's loop shape entered from a
    // different point (edge instead of center), same idea as БОЛЬШОЕ
    // КОЛЬЦО. Each side-trip is now its own explicit arrive-then-land pair
    // (fly back to center, THEN a separate touch-down beat) rather than
    // landing directly off the flight. Wide laneSpacing (below) matches
    // the lemniscate route's own reach.
    static GameObject CreateInfinityTrickPage(Transform parent)
    {
        const float wideLaneSpacing = 480f;
        // Vertical spread pushed further still (per feedback: ground lower,
        // air higher) — was ∓15/105/75, then -30/-60/150/120, now this. Same
        // ±30 A-above-B gap kept at both ends so the pair's own established
        // relationship doesn't change, just the overall range.
        const float groundYA = -70f;
        const float groundYB = -100f;
        const float airYA = 190f;
        const float airYB = 160f;
        var pathA = new[]
        {
            new TrickDiagramAnimation.Step { lane = 1, y = groundYA, airborne = false, travelDuration = 0f,    holdDuration = 0.7f },
            new TrickDiagramAnimation.Step { lane = 0, y = groundYA, airborne = false, travelDuration = 0.35f, holdDuration = 0.4f },
            new TrickDiagramAnimation.Step { lane = 0, y = airYA,    airborne = true,  travelDuration = 0.35f, holdDuration = 0.4f },
            new TrickDiagramAnimation.Step { lane = 1, y = airYA,    airborne = true,  travelDuration = 0.25f, holdDuration = 0.3f },
            new TrickDiagramAnimation.Step { lane = 1, y = groundYA, airborne = false, travelDuration = 0.35f, holdDuration = 0.4f },
            new TrickDiagramAnimation.Step { lane = 2, y = groundYA, airborne = false, travelDuration = 0.35f, holdDuration = 0.4f },
            new TrickDiagramAnimation.Step { lane = 2, y = airYA,    airborne = true,  travelDuration = 0.35f, holdDuration = 0.4f },
            new TrickDiagramAnimation.Step { lane = 1, y = airYA,    airborne = true,  travelDuration = 0.35f, holdDuration = 0.4f },
            new TrickDiagramAnimation.Step { lane = 1, y = groundYA, airborne = false, travelDuration = 0.35f, holdDuration = 0.4f },
        };
        var pathB = new[]
        {
            new TrickDiagramAnimation.Step { lane = 0, y = groundYB, airborne = false, travelDuration = 0f,    holdDuration = 0.7f },
            new TrickDiagramAnimation.Step { lane = 0, y = airYB,    airborne = true,  travelDuration = 0.35f, holdDuration = 0.4f },
            new TrickDiagramAnimation.Step { lane = 1, y = airYB,    airborne = true,  travelDuration = 0.35f, holdDuration = 0.4f },
            new TrickDiagramAnimation.Step { lane = 1, y = groundYB, airborne = false, travelDuration = 0.25f, holdDuration = 0.3f },
            new TrickDiagramAnimation.Step { lane = 2, y = groundYB, airborne = false, travelDuration = 0.35f, holdDuration = 0.4f },
            new TrickDiagramAnimation.Step { lane = 2, y = airYB,    airborne = true,  travelDuration = 0.35f, holdDuration = 0.4f },
            new TrickDiagramAnimation.Step { lane = 1, y = airYB,    airborne = true,  travelDuration = 0.35f, holdDuration = 0.4f },
            new TrickDiagramAnimation.Step { lane = 1, y = groundYB, airborne = false, travelDuration = 0.35f, holdDuration = 0.4f },
            new TrickDiagramAnimation.Step { lane = 0, y = groundYB, airborne = false, travelDuration = 0.35f, holdDuration = 0.4f },
        };
        // Big infinity symbol (lemniscate of Bernoulli) traced behind them,
        // stretched almost to the page's own edges horizontally (its max
        // horizontal reach is exactly the scale value, at angle 0). Y
        // multiplier bumped (was 0.35) for the same bigger-vertical-spread
        // reason as the bug heights above.
        System.Func<float, Vector2> infinity = t =>
        {
            float angle = t * Mathf.PI * 2f;
            float scale = 600f;
            float denom = 1f + Mathf.Sin(angle) * Mathf.Sin(angle);
            float x = scale * Mathf.Cos(angle) / denom;
            float y = scale * Mathf.Sin(angle) * Mathf.Cos(angle) / denom * 0.5f;
            return new Vector2(x, y);
        };
        // showArrowheads: false — it's a closed loop (t=0 and t=1 meet at
        // the same point), same reasoning as the ring trick's own oval; a
        // direction arrow on it reads as clutter, not a cue. Just a plain
        // dashed line now.
        return CreateTrickDiagramPage(parent, "БЕСКОНЕЧНОСТЬ", "LadyBug1.png", "LadyBug2.png",
            "LadyBug1Air1.png", "LadyBug2Air1.png", pathA, pathB, 0f, null, wideLaneSpacing, false, infinity);
    }

    // spriteFile: LadyBug1.png/LadyBug2.png — same textures the players
    // actually wear in-game. No label (ArchTrickAnimation's arrows carry
    // the explanation instead).
    static RectTransform CreateTrickBugIcon(Transform parent, string spriteFile, Vector2 pos, float height)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + spriteFile);

        var iconGo = new GameObject("Bug_" + spriteFile);
        iconGo.transform.SetParent(parent, false);
        RawImage icon = iconGo.AddComponent<RawImage>();
        icon.texture = tex;
        RectTransform iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        float aspect = tex != null ? (float)tex.width / tex.height : 1f;
        iconRt.sizeDelta = new Vector2(height * aspect, height);
        iconRt.anchoredPosition = pos;
        return iconRt;
    }

    // A pair of arrow glyphs either side of the given row's y — one shared
    // GameObject so ArchTrickAnimation can show/hide both with one
    // SetActive call instead of tracking them individually.
    static GameObject CreateArrowPair(Transform parent, string name, string arrowGlyph, Color color, float y, float xOffset)
    {
        var rootGo = new GameObject(name);
        rootGo.transform.SetParent(parent, false);
        RectTransform rootRt = rootGo.AddComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0.5f);
        rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = Vector2.zero;
        rootRt.sizeDelta = Vector2.zero;

        CreateSingleArrow(rootRt, arrowGlyph, color, new Vector2(-xOffset, y));
        CreateSingleArrow(rootRt, arrowGlyph, color, new Vector2(xOffset, y));

        rootGo.SetActive(false);
        return rootGo;
    }

    static RectTransform CreateSingleArrow(Transform parent, string glyph, Color color, Vector2 pos)
    {
        var go = new GameObject("Arrow");
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 64;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.text = glyph;
        go.AddComponent<Outline>().effectColor = Color.black;
        RectTransform rt = text.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(90f, 90f);
        rt.anchoredPosition = pos;
        return rt;
    }

    // Top-3 for one leaderboard category, with the #1 entry's photo (if any
    // was ever attached) — populated at runtime by TopResultsPage, since
    // the leaderboard itself only exists in PlayerPrefs, not at build time.
    // Same order as HighScoreManager's private Category enum/CategoryNames —
    // duplicated here only for the placeholder text below (SceneSetup can't
    // reach into that runtime-only private array at edit time).
    static readonly string[] TopCategoryNames = { "ВРЕМЯ", "ОЧКИ", "ТРЮКИ", "СКОРОСТЬ" };

    static GameObject CreateTopResultsPage(Transform parent, int category)
    {
        GameObject page = CreateFillPage(parent, "Page_TopResults_" + category);

        // Placeholder text set immediately (rather than leaving these blank
        // until TopResultsPage.Refresh runs) so the carousel background
        // never shows as an empty box for even a frame before real values
        // land — Refresh overwrites this the moment HighScoreManager has
        // real data.
        string categoryName = category >= 0 && category < TopCategoryNames.Length ? TopCategoryNames[category] : "?";

        // Three columns: title (left, vertically centered), rank+result
        // (middle, split into its own 2 sub-columns), photo (right, sized
        // to touch the carousel background's top and bottom edges) — not
        // all three crowded near the center.
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(page.transform, false);
        Text titleText = titleGo.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 34;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 0.85f, 0.2f);
        titleText.text = "ТОП: " + categoryName; // matches TopResultsPage.Refresh's format exactly, no visible jump once real data lands
        titleGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform titleRt = titleText.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = new Vector2(260f, 200f);
        titleRt.anchoredPosition = new Vector2(-560f, 0f);

        // One row per rank — each with its own photo (if that slot ever had
        // one attached), not a single photo for #1 shown off to the side.
        var rowValueTexts = new Text[3];
        var rowMedals = new RawImage[3];
        var rowPhotos = new RawImage[3];
        var rowArrowShafts = new GameObject[3];
        var rowArrowHeads = new GameObject[3];
        float[] rowY = { 245f, 0f, -245f };

        // Real generated medal art (yandex_api/gen_asset.sh, gold/silver/
        // bronze, each with its own embossed star) — replaces an earlier
        // plain flat-tinted circle, which read as just another number next
        // to the result value rather than an actual prize. The medal
        // itself (color + star) already reads as "1st/2nd/3rd" the same
        // way a real medal ceremony does, so there's no separate rank
        // digit drawn on top of it anymore — nowhere clean to put one
        // without covering the star.
        Texture2D[] medalTextures =
        {
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/MedalGold.png"),
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/MedalSilver.png"),
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/MedalBronze.png"),
        };

        // Photos sit in a 2-column zigzag, not one plain vertical stack:
        // rank 1 and rank 3 share the right column, sized/positioned so
        // together they fill most of the box height; rank 2 sits alone in a
        // second column further in, centered vertically. Title/rank/value
        // columns above were narrowed to make room for this on the left.
        // Slightly smaller than an earlier pass (290) specifically to leave
        // enough gap before the near column for the connector arrows below.
        const float photoSize = 260f;
        const float photoPadding = 40f;
        const float photoColumnGap = 30f;
        float[] photoX = { -photoPadding, -photoPadding - photoSize - photoColumnGap, -photoPadding };
        float[] photoY = { 360f - photoSize / 2f, 0f, -360f + photoSize / 2f };

        // Value text's own right edge (x=-160, width190, see below) — the
        // arrows below start just past this point, regardless of row.
        const float valueRightEdge = -160f + 190f / 2f;
        Texture2D arrowHeadTexture = CreateTriangleTexture(64);

        for (int i = 0; i < 3; i++)
        {
            var medalGo = new GameObject("RowMedal" + i);
            medalGo.transform.SetParent(page.transform, false);
            RawImage medalImg = medalGo.AddComponent<RawImage>();
            Texture2D medalTex = medalTextures[i];
            medalImg.texture = medalTex;
            RectTransform medalRt = medalImg.GetComponent<RectTransform>();
            medalRt.anchorMin = new Vector2(0.5f, 0.5f);
            medalRt.anchorMax = new Vector2(0.5f, 0.5f);
            medalRt.pivot = new Vector2(0.5f, 0.5f);
            // Fit within an 80x80 box, aspect preserved — the 3 medal
            // images aren't all the same aspect ratio (different ribbon
            // shapes), unlike the plain circle this replaced. Silver and
            // bronze's own source art has much less side padding than gold
            // (narrower canvas, not just a narrower ribbon), so without
            // medalWidthBoost they render visibly thinner than gold at the
            // same box height — this brings all 3 back to roughly the same
            // on-screen width.
            const float medalBoxSize = 80f;
            float medalAspect = medalTex != null ? (float)medalTex.width / medalTex.height : 1f;
            Vector2 medalBaseSize = medalAspect >= 1f
                ? new Vector2(medalBoxSize, medalBoxSize / medalAspect)
                : new Vector2(medalBoxSize * medalAspect, medalBoxSize);
            float[] medalWidthBoost = { 1f, 1.55f, 1.45f }; // gold, silver, bronze
            medalRt.sizeDelta = new Vector2(medalBaseSize.x * medalWidthBoost[i], medalBaseSize.y);
            medalRt.anchoredPosition = new Vector2(-335f, rowY[i]);
            rowMedals[i] = medalImg;

            var rowValueGo = new GameObject("RowValue" + i);
            rowValueGo.transform.SetParent(page.transform, false);
            Text rowValue = rowValueGo.AddComponent<Text>();
            rowValue.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rowValue.fontSize = 36;
            rowValue.fontStyle = FontStyle.Bold;
            rowValue.alignment = TextAnchor.MiddleLeft;
            rowValue.color = Color.white;
            rowValue.text = "--";
            rowValueGo.AddComponent<Outline>().effectColor = Color.black;
            RectTransform rowValueRt = rowValue.GetComponent<RectTransform>();
            rowValueRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowValueRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowValueRt.pivot = new Vector2(0.5f, 0.5f);
            rowValueRt.sizeDelta = new Vector2(190f, 80f);
            rowValueRt.anchoredPosition = new Vector2(-160f, rowY[i]);
            rowValueTexts[i] = rowValue;

            // Connects the result to its photo with a real shaft+arrowhead
            // spanning (almost) the whole gap between them, not a small
            // glyph floating in the middle of it — the 2-column zigzag means
            // a row's photo isn't always directly beside its text anymore
            // (rank 2's photo sits in the nearer column), so without this
            // the link between a result and its picture isn't obvious.
            // Sized to whatever gap that row actually has (rank 2's is much
            // tighter than rank 1/3's, so its arrow comes out shorter too).
            float photoLeftEdge = 700f + photoX[i] - photoSize; // 700 = half of the carousel box's new 1400 width
            float arrowY = (rowY[i] + photoY[i]) / 2f;
            float gap = photoLeftEdge - valueRightEdge;
            float headSize = Mathf.Clamp(gap * 0.3f, 18f, 44f);
            float headX = photoLeftEdge - 8f - headSize / 2f;
            float shaftEndX = headX - headSize / 2f - 6f;
            float shaftStartX = valueRightEdge + 10f;
            float shaftWidth = Mathf.Max(6f, shaftEndX - shaftStartX);
            float shaftCenterX = (shaftStartX + shaftEndX) / 2f;

            var shaftGo = new GameObject("RowArrowShaft" + i);
            shaftGo.transform.SetParent(page.transform, false);
            Image shaftImg = shaftGo.AddComponent<Image>();
            shaftImg.color = new Color(1f, 0.85f, 0.2f, 0.95f);
            RectTransform shaftRt = shaftImg.GetComponent<RectTransform>();
            shaftRt.anchorMin = new Vector2(0.5f, 0.5f);
            shaftRt.anchorMax = new Vector2(0.5f, 0.5f);
            shaftRt.pivot = new Vector2(0.5f, 0.5f);
            shaftRt.sizeDelta = new Vector2(shaftWidth, 8f);
            shaftRt.anchoredPosition = new Vector2(shaftCenterX, arrowY);

            var headGo = new GameObject("RowArrowHead" + i);
            headGo.transform.SetParent(page.transform, false);
            RawImage headImg = headGo.AddComponent<RawImage>();
            headImg.texture = arrowHeadTexture;
            headImg.color = new Color(1f, 0.85f, 0.2f);
            RectTransform headRt = headImg.GetComponent<RectTransform>();
            headRt.anchorMin = new Vector2(0.5f, 0.5f);
            headRt.anchorMax = new Vector2(0.5f, 0.5f);
            headRt.pivot = new Vector2(0.5f, 0.5f);
            headRt.sizeDelta = new Vector2(headSize, headSize);
            headRt.anchoredPosition = new Vector2(headX, arrowY);

            rowArrowShafts[i] = shaftGo;
            rowArrowHeads[i] = headGo;

            var rowPhotoGo = new GameObject("RowPhoto" + i);
            rowPhotoGo.transform.SetParent(page.transform, false);
            RawImage rowPhoto = rowPhotoGo.AddComponent<RawImage>();
            rowPhoto.color = Color.white;
            RectTransform rowPhotoRt = rowPhoto.GetComponent<RectTransform>();
            rowPhotoRt.anchorMin = new Vector2(1f, 0.5f);
            rowPhotoRt.anchorMax = new Vector2(1f, 0.5f);
            rowPhotoRt.pivot = new Vector2(1f, 0.5f);
            rowPhotoRt.sizeDelta = new Vector2(photoSize, photoSize);
            rowPhotoRt.anchoredPosition = new Vector2(photoX[i], photoY[i]);
            rowPhotoGo.SetActive(false);
            rowPhotos[i] = rowPhoto;
        }

        TopResultsPage pageComp = page.AddComponent<TopResultsPage>();
        SerializedObject so = new SerializedObject(pageComp);
        so.FindProperty("category").intValue = category;
        so.FindProperty("titleText").objectReferenceValue = titleText;
        SerializedProperty rowValueTextsProp = so.FindProperty("rowValueTexts");
        rowValueTextsProp.arraySize = rowValueTexts.Length;
        for (int i = 0; i < rowValueTexts.Length; i++)
            rowValueTextsProp.GetArrayElementAtIndex(i).objectReferenceValue = rowValueTexts[i];
        SerializedProperty rowMedalsProp = so.FindProperty("rowMedals");
        rowMedalsProp.arraySize = rowMedals.Length;
        for (int i = 0; i < rowMedals.Length; i++)
            rowMedalsProp.GetArrayElementAtIndex(i).objectReferenceValue = rowMedals[i];
        SerializedProperty rowPhotosProp = so.FindProperty("rowPhotos");
        rowPhotosProp.arraySize = rowPhotos.Length;
        for (int i = 0; i < rowPhotos.Length; i++)
            rowPhotosProp.GetArrayElementAtIndex(i).objectReferenceValue = rowPhotos[i];
        so.FindProperty("noPhotoTexture").objectReferenceValue = CreateNoPhotoTexture(256);
        SerializedProperty rowArrowShaftsProp = so.FindProperty("rowArrowShafts");
        rowArrowShaftsProp.arraySize = rowArrowShafts.Length;
        for (int i = 0; i < rowArrowShafts.Length; i++)
            rowArrowShaftsProp.GetArrayElementAtIndex(i).objectReferenceValue = rowArrowShafts[i];
        SerializedProperty rowArrowHeadsProp = so.FindProperty("rowArrowHeads");
        rowArrowHeadsProp.arraySize = rowArrowHeads.Length;
        for (int i = 0; i < rowArrowHeads.Length; i++)
            rowArrowHeadsProp.GetArrayElementAtIndex(i).objectReferenceValue = rowArrowHeads[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        return page;
    }

    static void CreatePauseDialog()
    {
        var canvasGo = new GameObject("PauseCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // above the start screen too, just in case

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        canvasGo.AddComponent<GraphicRaycaster>();

        var backdropGo = new GameObject("Backdrop");
        backdropGo.transform.SetParent(canvasGo.transform, false);
        Image backdrop = backdropGo.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.65f);
        RectTransform backdropRt = backdropGo.GetComponent<RectTransform>();
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;

        var questionGo = new GameObject("Question");
        questionGo.transform.SetParent(canvasGo.transform, false);
        Text question = questionGo.AddComponent<Text>();
        question.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        question.fontSize = 54;
        question.fontStyle = FontStyle.Bold;
        question.alignment = TextAnchor.MiddleCenter;
        question.color = Color.white;
        question.text = "Закончить игру?";
        questionGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform questionRt = question.GetComponent<RectTransform>();
        questionRt.anchorMin = new Vector2(0.5f, 0.5f);
        questionRt.anchorMax = new Vector2(0.5f, 0.5f);
        questionRt.pivot = new Vector2(0.5f, 0.5f);
        questionRt.sizeDelta = new Vector2(1000f, 120f);
        questionRt.anchoredPosition = new Vector2(0f, 80f);

        Text yesText = CreateDialogOptionText(canvasGo.transform, "Yes", new Vector2(-160f, -40f), "ДА");
        Text noText = CreateDialogOptionText(canvasGo.transform, "No", new Vector2(160f, -40f), "НЕТ");

        var hintGo = new GameObject("Hint");
        hintGo.transform.SetParent(canvasGo.transform, false);
        Text hint = hintGo.AddComponent<Text>();
        hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hint.fontSize = 24;
        hint.alignment = TextAnchor.MiddleCenter;
        hint.color = new Color(0.85f, 0.85f, 0.85f);
        hint.text = "←→ выбор, Space — подтвердить";
        RectTransform hintRt = hint.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.5f, 0.5f);
        hintRt.anchorMax = new Vector2(0.5f, 0.5f);
        hintRt.pivot = new Vector2(0.5f, 0.5f);
        hintRt.sizeDelta = new Vector2(800f, 60f);
        hintRt.anchoredPosition = new Vector2(0f, -140f);

        canvasGo.SetActive(false);

        var controllerGo = new GameObject("PauseController");
        PauseController pause = controllerGo.AddComponent<PauseController>();
        SerializedObject so = new SerializedObject(pause);
        so.FindProperty("dialogRoot").objectReferenceValue = canvasGo;
        so.FindProperty("yesText").objectReferenceValue = yesText;
        so.FindProperty("noText").objectReferenceValue = noText;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Both hands down (or Down key, for the keyboard scheme) held on every
    // active player at once, for 10s total, opens the quit-confirm dialog —
    // see DuckToExitController for the 5s-silent / 5s-countdown timing.
    static void CreateExitGesture()
    {
        var canvasGo = new GameObject("ExitGestureCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 140; // above gameplay HUD, below the help screen (150) / pause dialog (200)

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var textGo = new GameObject("CountdownText");
        textGo.transform.SetParent(canvasGo.transform, false);
        Text text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 64;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.3f, 0.2f);

        Outline outline = textGo.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, -3f);

        RectTransform rt = text.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(900f, 140f);
        rt.anchoredPosition = new Vector2(0f, 250f);

        textGo.SetActive(false);

        var controllerGo = new GameObject("DuckToExitController");
        DuckToExitController controller = controllerGo.AddComponent<DuckToExitController>();
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("countdownText").objectReferenceValue = text;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Flowers used for the intro rain — reused art, not new assets, just
    // for visual variety ("разных цветов" — different colors/flowers).
    static readonly string[] IntroFlowerSprites =
    {
        "Flower.png", "FlowerPink.png", "FlowerYellow.png", "FlowerLavender.png",
        "DaisyWhite.png", "DaisyPink.png", "SunflowerYellow.png",
        "LotusYellow.png", "LotusBlue.png", "LotusPink.png",
    };

    // Very first thing the player sees: flowers rain from the top and pile
    // up until the whole screen is covered, then the canvas hides itself,
    // revealing the start menu that's been sitting ready underneath.
    // Highest sorting order of any canvas — this has to cover absolutely
    // everything, since it's the very first frame of the game.
    static void CreateIntroScreen()
    {
        var canvasGo = new GameObject("IntroCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 220;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // Solid backdrop so gaps between not-yet-landed flowers show a
        // plain color instead of the 3D scene bleeding through underneath.
        var backdropGo = new GameObject("Backdrop");
        backdropGo.transform.SetParent(canvasGo.transform, false);
        Image backdrop = backdropGo.AddComponent<Image>();
        backdrop.color = new Color(0.55f, 0.78f, 0.95f);
        RectTransform backdropRt = backdropGo.GetComponent<RectTransform>();
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;

        // Grid tiled a little past the reference screen's edges so full
        // coverage doesn't depend on exact rounding.
        const float cellSize = 140f;
        int columns = Mathf.CeilToInt(1920f / cellSize) + 1;
        int rows = Mathf.CeilToInt(1080f / cellSize) + 1;
        float gridWidth = columns * cellSize;
        float gridHeight = rows * cellSize;
        float startX = -gridWidth / 2f + cellSize / 2f;
        float startY = -gridHeight / 2f + cellSize / 2f;

        var rng = new System.Random();
        // Built row by row, bottom to top, columns shuffled within each row —
        // IntroSequence plays them back in this exact order, so the pile
        // visibly rises from the bottom until it reaches the top.
        var orderedFlowers = new System.Collections.Generic.List<RectTransform>();
        for (int row = 0; row < rows; row++)
        {
            var colOrder = new System.Collections.Generic.List<int>();
            for (int col = 0; col < columns; col++)
                colOrder.Add(col);
            for (int i = colOrder.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int tmp = colOrder[i];
                colOrder[i] = colOrder[j];
                colOrder[j] = tmp;
            }

            foreach (int col in colOrder)
            {
                string spriteFile = IntroFlowerSprites[rng.Next(IntroFlowerSprites.Length)];
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + spriteFile);

                var flowerGo = new GameObject("Flower_" + row + "_" + col);
                flowerGo.transform.SetParent(canvasGo.transform, false);
                RawImage img = flowerGo.AddComponent<RawImage>();
                img.texture = tex;
                RectTransform rt = img.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                const float sizeShrink = 0.9f; // slight gap so flowers read as individual pieces, not one solid mass
                rt.sizeDelta = new Vector2(cellSize * sizeShrink, cellSize * sizeShrink);
                // Small random offset off the exact grid point — a strict
                // grid read as too mechanical; this keeps the fill order
                // (still bottom row to top row) while looking organic.
                const float jitter = cellSize * 0.45f;
                Vector2 jitterOffset = new Vector2(
                    (float)(rng.NextDouble() * 2.0 - 1.0) * jitter,
                    (float)(rng.NextDouble() * 2.0 - 1.0) * jitter);
                rt.anchoredPosition = new Vector2(startX + col * cellSize, startY + row * cellSize) + jitterOffset;
                flowerGo.SetActive(false);

                orderedFlowers.Add(rt);
            }
        }

        // Full-screen brick wall, shown once the flower grid finishes
        // filling (and holds for a beat — see IntroSequence) — opaque, so
        // it fully covers the flowers/backdrop underneath just by sitting
        // on top of them (later sibling), no need to also deactivate those.
        // Stays completely still for the whole countdown; only the digit/
        // word layer on top of it (below) moves/pulses.
        var wallGo = new GameObject("GraffitiWall");
        wallGo.transform.SetParent(canvasGo.transform, false);
        RawImage wallImage = wallGo.AddComponent<RawImage>();
        wallImage.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/GraffitiWall.png");
        RectTransform wallRt = wallImage.GetComponent<RectTransform>();
        wallRt.anchorMin = Vector2.zero;
        wallRt.anchorMax = Vector2.one;
        wallRt.offsetMin = Vector2.zero;
        wallRt.offsetMax = Vector2.zero;
        wallGo.SetActive(false);

        // Full-screen black fade for the "buzz fades out, flowers darken,
        // wall reveals from the dark, digits appear a beat later" transition
        // (see IntroSequence.RunIntro) — a later sibling than the flowers
        // AND the wall, so the same overlay darkens the finished flower pile
        // first, then stays opaque while the wall switches on underneath it,
        // then fades back out to reveal the wall before the countdown starts.
        var darkOverlayGo = new GameObject("DarkOverlay");
        darkOverlayGo.transform.SetParent(canvasGo.transform, false);
        Image darkOverlay = darkOverlayGo.AddComponent<Image>();
        darkOverlay.color = new Color(0f, 0f, 0f, 0f);
        darkOverlay.raycastTarget = false;
        RectTransform darkOverlayRt = darkOverlayGo.GetComponent<RectTransform>();
        darkOverlayRt.anchorMin = Vector2.zero;
        darkOverlayRt.anchorMax = Vector2.one;
        darkOverlayRt.offsetMin = Vector2.zero;
        darkOverlayRt.offsetMax = Vector2.zero;
        darkOverlayGo.SetActive(false);

        // Digit/word overlay — real generated graffiti artwork
        // (yandex_api/gen_asset.sh, see Assets/Sprites/CountdownGraffiti*.png),
        // transparent cutouts (no wall baked in, unlike the wall image
        // above) so this can pulse on its own without the wall moving with
        // it. One texture per step (5/4/3/2/1/СТАРТ), swapped on a single
        // RawImage rather than 6 separate GameObjects (same texture-swap
        // pattern TopResultsPage already uses for its photo slots).
        // Full-screen, same as the wall it sits on top of.
        var countdownGo = new GameObject("CountdownImage");
        countdownGo.transform.SetParent(canvasGo.transform, false);
        RawImage countdownImage = countdownGo.AddComponent<RawImage>();
        RectTransform countdownRt = countdownImage.GetComponent<RectTransform>();
        countdownRt.anchorMin = Vector2.zero;
        countdownRt.anchorMax = Vector2.one;
        countdownRt.offsetMin = Vector2.zero;
        countdownRt.offsetMax = Vector2.zero;

        Texture2D[] countdownTextures =
        {
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/CountdownGraffiti5.png"),
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/CountdownGraffiti4.png"),
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/CountdownGraffiti3.png"),
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/CountdownGraffiti2.png"),
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/CountdownGraffiti1.png"),
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/CountdownGraffitiStart.png"),
        };

        // Continuous buzz while the flowers fill in (grows with them, see
        // IntroSequence) — same clip the players' own wing-flap loop uses,
        // reused rather than a new asset since it already reads as "in the
        // air, building energy". Gear-shift plays once per countdown digit.
        // Neither autoplays — IntroSequence starts/stops them on its own
        // schedule instead of the instant the scene loads.
        var introGo = new GameObject("IntroSequence");
        AudioSource introBuzzSource = introGo.AddComponent<AudioSource>();
        introBuzzSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Buzz.wav");
        introBuzzSource.loop = true;
        introBuzzSource.playOnAwake = false;
        introBuzzSource.volume = 0f;

        AudioSource introShiftSource = introGo.AddComponent<AudioSource>();
        introShiftSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/GearShift.wav");
        introShiftSource.playOnAwake = false;

        IntroSequence intro = introGo.AddComponent<IntroSequence>();
        SerializedObject introSo = new SerializedObject(intro);
        introSo.FindProperty("canvasRoot").objectReferenceValue = canvasGo;
        introSo.FindProperty("wall").objectReferenceValue = wallGo;
        introSo.FindProperty("darkOverlay").objectReferenceValue = darkOverlay;
        introSo.FindProperty("countdownImage").objectReferenceValue = countdownImage;
        SerializedProperty countdownTexturesProp = introSo.FindProperty("countdownTextures");
        countdownTexturesProp.arraySize = countdownTextures.Length;
        for (int i = 0; i < countdownTextures.Length; i++)
            countdownTexturesProp.GetArrayElementAtIndex(i).objectReferenceValue = countdownTextures[i];
        introSo.FindProperty("buzzSource").objectReferenceValue = introBuzzSource;
        introSo.FindProperty("shiftSource").objectReferenceValue = introShiftSource;
        introSo.FindProperty("startScreen").objectReferenceValue = Object.FindAnyObjectByType<StartScreenController>();
        SerializedProperty flowersProp = introSo.FindProperty("flowers");
        flowersProp.arraySize = orderedFlowers.Count;
        for (int i = 0; i < orderedFlowers.Count; i++)
            flowersProp.GetArrayElementAtIndex(i).objectReferenceValue = orderedFlowers[i];
        introSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // Маленькая надпись в углу с реальным разрешением рендера — чтобы можно
    // было на глаз понять, при каком разрешении/fullscreen-режиме сейчас
    // идёт билд (Editor Game view и отдельно собранный .app могут показывать
    // разное). Поверх абсолютно всего, включая интро-экран.
    static void CreateScreenInfoLabel()
    {
        var canvasGo = new GameObject("ScreenInfoCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 230;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        var textGo = new GameObject("ScreenInfoText");
        textGo.transform.SetParent(canvasGo.transform, false);
        Text text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 18;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.UpperLeft;
        text.color = new Color(1f, 1f, 1f, 0.6f);
        RectTransform rt = text.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(300f, 30f);
        rt.anchoredPosition = new Vector2(6f, -6f);

        var labelGo = new GameObject("ScreenInfoLabel");
        ScreenInfoLabel label = labelGo.AddComponent<ScreenInfoLabel>();
        SerializedObject so = new SerializedObject(label);
        so.FindProperty("label").objectReferenceValue = text;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Text CreateDialogOptionText(Transform parent, string name, Vector2 anchoredPos, string label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 46;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;
        go.AddComponent<Outline>().effectColor = Color.black;

        RectTransform rt = text.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(220f, 80f);
        rt.anchoredPosition = anchoredPos;

        return text;
    }

    // Tileable asphalt grain — per-pixel brightness jitter around the base
    // road-gray so the surface reads as slightly uneven instead of a flat
    // block of color. Fixed seed so re-running Rebuild Scene is deterministic.
    static Texture2D CreateRoadTexture()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        Color baseColor = new Color(0.25f, 0.25f, 0.25f);
        var rng = new System.Random(12345);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float jitter = ((float)rng.NextDouble() - 0.5f) * 0.12f;
                Color c = new Color(
                    Mathf.Clamp01(baseColor.r + jitter),
                    Mathf.Clamp01(baseColor.g + jitter),
                    Mathf.Clamp01(baseColor.b + jitter));
                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        return tex;
    }

    // Solid white right-pointing triangle (flat edge on the left, point on
    // the right), alpha elsewhere — used as an arrowhead via RawImage
    // (tinted through its own .color) instead of a Unicode "▶" glyph, which
    // isn't guaranteed to be in whatever font subset a standalone build
    // embeds (renders fine in the Editor via a system font fallback, but
    // came out as a blank/missing glyph — just the shaft bar — in an actual
    // build).
    static Texture2D CreateTriangleTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            float ny = (float)y / (size - 1);
            float distFromCenter = Mathf.Abs(ny - 0.5f) * 2f; // 0 at vertical center, 1 at top/bottom
            float rightEdge = (1f - distFromCenter) * size;
            for (int x = 0; x < size; x++)
            {
                // Soften the slanted edge by one pixel instead of a hard cutoff.
                float alpha = Mathf.Clamp01(rightEdge - x);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return tex;
    }

    static Texture2D CreateDashTexture()
    {
        // 1px wide, 8px tall: first half white (dash), second half road-gray (gap).
        var tex = new Texture2D(1, 8, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;

        Color gap = new Color(0.25f, 0.25f, 0.25f);
        for (int y = 0; y < 8; y++)
            tex.SetPixel(0, y, y < 5 ? Color.white : gap);

        tex.Apply();
        return tex;
    }

    // Dashed route line(s) traced along one or more parametric curves — the
    // "bold dashed line behind the bugs marking the overall route" backdrop
    // several trick instruction pages ask for (see TrickDiagramAnimation).
    // Each curve maps t in [0,1] to a page-space point, in the SAME
    // coordinate space the bugs themselves are positioned in
    // (anchoredPosition units, origin at page center) — worldSpan is how
    // many of those units the texture's full width/height covers, so page-
    // space and pixel-space line up. White line, alpha-only shape — tinted
    // via the Image's own color at display time, same convention
    // CreateTriangleTexture-style helpers use. Optional solidDots draw a
    // filled circle (not dashed) at specific page-space points, e.g. the
    // "big dot in the middle of the arrow" СИНХРОН's route wants.
    static Texture2D CreateDashedPathTexture(int texSize, float worldSpan, System.Func<float, Vector2>[] curves,
        (Vector2 pos, float radius)[] solidDots = null,
        float thickness = 10f, float dashLength = 26f, float gapLength = 16f, int samples = 1000,
        bool showArrowheads = true)
    {
        var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color[texSize * texSize];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(1f, 1f, 1f, 0f);

        float pixelsPerWorld = texSize / worldSpan;
        float halfThicknessPx = (thickness / 2f) * pixelsPerWorld;

        if (curves != null)
        {
            foreach (var curve in curves)
            {
                float travelled = 0f;
                Vector2 prevWorld = curve(0f);
                for (int s = 1; s <= samples; s++)
                {
                    float t = (float)s / samples;
                    Vector2 world = curve(t);
                    float segLen = Vector2.Distance(prevWorld, world) * pixelsPerWorld;
                    bool dashOn = Mathf.Repeat(travelled, dashLength + gapLength) < dashLength;
                    if (dashOn)
                    {
                        Vector2 pxA = new Vector2(texSize / 2f + prevWorld.x * pixelsPerWorld, texSize / 2f + prevWorld.y * pixelsPerWorld);
                        Vector2 pxB = new Vector2(texSize / 2f + world.x * pixelsPerWorld, texSize / 2f + world.y * pixelsPerWorld);
                        StampDashSegment(pixels, texSize, pxA, pxB, halfThicknessPx);
                    }
                    travelled += segLen;
                    prevWorld = world;
                }

                // Arrowhead at the curve's end, pointing the direction of
                // travel — a short solid (always-on, not dashed) chevron so
                // the route reads as a directional arrow, not just a plain
                // line with dashes. Every curve gets one by default,
                // including each half of a curve that's been split at a dot
                // (see CreateSyncTrickPage's rowA1/rowA2 etc.) — so a route
                // split before/after a point shows as 2 separate arrows
                // there, not one plain line straight through. showArrowheads
                // opts a closed-loop curve (ring's own oval, t=0 and t=1 at
                // the same point) out of this — a direction arrow on a
                // simple closed loop reads as pointless clutter, not a cue.
                if (showArrowheads)
                {
                    Vector2 endWorld = curve(1f);
                    Vector2 nearEndWorld = curve(0.97f);
                    Vector2 dir = endWorld - nearEndWorld;
                    if (dir.sqrMagnitude > 0.0001f)
                    {
                        dir.Normalize();
                        Vector2 perp = new Vector2(-dir.y, dir.x);
                        const float headLength = 34f;
                        const float headSpread = 16f;
                        Vector2 backPoint = endWorld - dir * headLength;
                        Vector2 wing1 = backPoint + perp * headSpread;
                        Vector2 wing2 = backPoint - perp * headSpread;

                        Vector2 pxEnd = new Vector2(texSize / 2f + endWorld.x * pixelsPerWorld, texSize / 2f + endWorld.y * pixelsPerWorld);
                        Vector2 pxWing1 = new Vector2(texSize / 2f + wing1.x * pixelsPerWorld, texSize / 2f + wing1.y * pixelsPerWorld);
                        Vector2 pxWing2 = new Vector2(texSize / 2f + wing2.x * pixelsPerWorld, texSize / 2f + wing2.y * pixelsPerWorld);
                        StampDashSegment(pixels, texSize, pxWing1, pxEnd, halfThicknessPx);
                        StampDashSegment(pixels, texSize, pxWing2, pxEnd, halfThicknessPx);
                    }
                }
            }
        }

        if (solidDots != null)
        {
            foreach (var (pos, radius) in solidDots)
            {
                Vector2 pxCenter = new Vector2(texSize / 2f + pos.x * pixelsPerWorld, texSize / 2f + pos.y * pixelsPerWorld);
                StampDashSegment(pixels, texSize, pxCenter, pxCenter, radius * pixelsPerWorld);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    static void StampDashSegment(Color[] pixels, int texSize, Vector2 pxA, Vector2 pxB, float halfThicknessPx)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pxA.x, pxB.x) - halfThicknessPx - 1f));
        int maxX = Mathf.Min(texSize - 1, Mathf.CeilToInt(Mathf.Max(pxA.x, pxB.x) + halfThicknessPx + 1f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pxA.y, pxB.y) - halfThicknessPx - 1f));
        int maxY = Mathf.Min(texSize - 1, Mathf.CeilToInt(Mathf.Max(pxA.y, pxB.y) + halfThicknessPx + 1f));

        Vector2 ab = pxB - pxA;
        float lenSq = Mathf.Max(ab.sqrMagnitude, 0.0001f);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(p - pxA, ab) / lenSq);
                Vector2 proj = pxA + ab * t;
                float dist = Vector2.Distance(p, proj);
                float alpha = Mathf.Clamp01(halfThicknessPx - dist + 1f);
                if (alpha > 0f)
                {
                    int idx = y * texSize + x;
                    pixels[idx].a = Mathf.Max(pixels[idx].a, alpha);
                    pixels[idx].r = pixels[idx].g = pixels[idx].b = 1f;
                }
            }
        }
    }

    // Soft-edged white rectangle — alpha fades out toward the border
    // instead of a hard cutoff, wrapped as a Sprite so it can go straight
    // into an existing Image component (which tints it via its own .color,
    // same as the plain solid-color rect it replaces). Used for the start
    // menu's row-focus glow (CreateStartScreen) — a flat solid-color box
    // read as a stark rectangle; this reads as a soft highlight instead.
    static Sprite CreateSoftRectSprite(int size, float featherFraction)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color[size * size];
        float feather = Mathf.Max(1f, size * featherFraction);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distX = Mathf.Min(x, size - 1 - x);
                float distY = Mathf.Min(y, size - 1 - y);
                float dist = Mathf.Min(distX, distY);
                float alpha = Mathf.Clamp01(dist / feather);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        // 9-slice border matching the feather width — without this, Image
        // (Type.Simple) would just stretch the whole 128x128 sprite to fit
        // each row's own (much wider than tall) rect, squashing the top/
        // bottom feather down to almost nothing while the left/right
        // feather stayed full width. Sliced keeps the border regions at
        // their real pixel size regardless of the target rect's aspect.
        float borderPx = feather;
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(borderPx, borderPx, borderPx, borderPx));
    }

    // Black square with a bold red diagonal cross — placeholder for a
    // leaderboard photo slot that has a real ranked entry but no photo was
    // ever attached to it (distinct from no entry at all, which just hides
    // the slot entirely — see TopResultsPage.Refresh).
    static Texture2D CreateNoPhotoTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(0f, 0f, 0f, 1f);

        float half = size * 0.03f; // was 0.05 — a bit thinner, per feedback
        StampSolidLine(pixels, size, new Vector2(0f, 0f), new Vector2(size, size), half, Color.red);
        StampSolidLine(pixels, size, new Vector2(0f, size), new Vector2(size, 0f), half, Color.red);

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    static void StampSolidLine(Color[] pixels, int texSize, Vector2 pxA, Vector2 pxB, float halfThicknessPx, Color color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pxA.x, pxB.x) - halfThicknessPx - 1f));
        int maxX = Mathf.Min(texSize - 1, Mathf.CeilToInt(Mathf.Max(pxA.x, pxB.x) + halfThicknessPx + 1f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pxA.y, pxB.y) - halfThicknessPx - 1f));
        int maxY = Mathf.Min(texSize - 1, Mathf.CeilToInt(Mathf.Max(pxA.y, pxB.y) + halfThicknessPx + 1f));

        Vector2 ab = pxB - pxA;
        float lenSq = Mathf.Max(ab.sqrMagnitude, 0.0001f);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(p - pxA, ab) / lenSq);
                Vector2 proj = pxA + ab * t;
                float dist = Vector2.Distance(p, proj);
                if (dist <= halfThicknessPx)
                    pixels[y * texSize + x] = color;
            }
        }
    }

    // Builds the dashed-route RawImage itself, full-page-sized and sitting
    // behind the bugs (earlier sibling — added right after the page title,
    // before anything else), for the trick pages that want a route shape
    // traced behind the animation.
    static void CreateDashedRouteBackdrop(Transform parent, (Vector2 pos, float radius)[] dots, float worldSpan,
        bool showArrowheads, params System.Func<float, Vector2>[] curves)
    {
        Texture2D tex = CreateDashedPathTexture(900, worldSpan, curves, dots, showArrowheads: showArrowheads);
        var go = new GameObject("RouteBackdrop");
        go.transform.SetParent(parent, false);
        RawImage img = go.AddComponent<RawImage>();
        img.texture = tex;
        img.color = new Color(1f, 1f, 1f, 0.35f);
        RectTransform rt = img.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(worldSpan, worldSpan);
        rt.anchoredPosition = Vector2.zero;
        go.transform.SetSiblingIndex(1); // right after the page title (index 0), behind everything else added after this call
    }

    static void ApplyColor(GameObject go, Color color)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        Shader shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material material = new Material(shader) { color = color };
        renderer.sharedMaterial = material;
    }

    // Flat, static shadow baked into a prefab (unlike CreatePlayerShadow,
    // this never moves relative to its parent — fine since obstacle height
    // never changes at runtime). Local Y cancels out root's own height
    // offset so it always lands right at road level regardless of how each
    // builder positions its root (some use height/2, some leave it at 0).
    static void AddStaticGroundShadow(GameObject root, float width, float depth, string materialName)
    {
        const float groundY = 0.02f;

        GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shadow.name = "Shadow";
        Object.DestroyImmediate(shadow.GetComponent<Collider>());
        shadow.transform.SetParent(root.transform);
        shadow.transform.localScale = new Vector3(width, 0.01f, depth);
        shadow.transform.localPosition = new Vector3(0f, -root.transform.position.y + groundY, 0f);

        Renderer renderer = shadow.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { color = new Color(0f, 0f, 0f, 0.35f) };

        System.IO.Directory.CreateDirectory("Assets/Materials");
        string materialPath = "Assets/Materials/" + materialName + ".mat";
        AssetDatabase.DeleteAsset(materialPath);
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;
    }
}
