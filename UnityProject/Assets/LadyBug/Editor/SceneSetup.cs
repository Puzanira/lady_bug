using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LadyBug
{

public static class SceneSetup
{
    const int LaneCount = 4;
    static float LaneWidth => RoadLayout.LaneWidthFor(LaneCount);

    static readonly string PlayerOneBugSprite = "LadyBug1.png";
    static readonly string PlayerTwoBugSprite = "LadyBug2.png";

    // Воздушный вариант ВСЕГДА принадлежит тому же жуку, что и наземный спрайт.
    // Раньше он передавался отдельным аргументом — и на всех семи страницах-
    // образцах указывал на чужого игрока, из-за чего жук в полёте менял цвет и
    // оба образца становились одинаковыми. Вывод из имени убирает саму
    // возможность рассогласования.
    static string AirSpriteFor(string groundSprite) =>
        groundSprite.Replace(".png", "Air1.png");
    static readonly Color PlayerOneBugTint = Color.white;
    static readonly Color PlayerTwoBugTint = new Color(0.52f, 0.52f, 0.56f);

    // P1 = LadyBug1 (КРАСНЫЙ); P2 = LadyBug2 (СИНЕ-ФИОЛЕТОВЫЙ) — разный арт, а не тон
    // одного и того же. PlayerTwoBugTint затемняет P2 в игре и в превью «ВАШИ
    // ДЕЙСТВИЯ»; диаграммы ОБРАЗЦА рисуют обоих на белом (CreateTrickBugIcon).

    static void GetStartLanes(out int startLaneRight, out int startLaneLeft) =>
        RoadLayout.GetStartLanes(LaneCount, out startLaneRight, out startLaneLeft);

    const float RoadLength = 150f;
    const float RoadCenterZ = 1f;
    // World units per dash+gap cycle — was 4 (dash+gap blurred into one
    // solid line at higher speeds); doubled so both the dash and the gap
    // stay individually readable further into the speed range.
    const float DashPeriod = 8f;
    // How many differently-distorted dash styles are stacked into one
    // CreateDashTexture and cycled through along the road, instead of every
    // dash looking identical — see CreateDashTexture/DrawDashBand.
    const int DashVariantCount = 3;
    const float ScrollSpeed = 10f;
    const float RoadTextureTileSize = 1.5f; // world units per asphalt-texture tile — must match CreateRoadTexture's mainTextureScale divisor
    const float GrassTextureTileSize = 4f; // world units per side-grass-texture tile (Assets/LadyBug/Sprites/GrassTile.png)
    // false = old Standard scroll (no UV rotation); true = lady_bug/SideGrass upright tufts.
    const bool SideGrassUprightShader = true;

    // Every Text component in the game uses this instead of the engine's
    // built-in LegacyRuntime.ttf — Comic CAT (Vitaly Lazarenko, 2019),
    // downloaded from ffont.ru (the fonts-online.ru page the font was
    // requested from gates its own download behind a CAPTCHA; same font,
    // same author, no CAPTCHA on that mirror). Free for personal use per
    // that mirror's listing — worth confirming the license directly with
    // the author before any commercial release. Lives under Resources/ (not
    // just Assets/) specifically so runtime code (ScoreManager/
    // TricksManager's popups, built on the fly during play, not at scene-
    // build time) can load it too via Resources.Load, the same as this does.
    static Font GameFont => Resources.Load<Font>("lady_bug/Fonts/ComicCAT");

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

        _generatedThisRun.Clear(); // paired with PruneGeneratedTextures() at the end

        CreateLight();
        CreateSpeedController();
        CreateGestureSensorSerial();
        CreateJoystickSerial();
        CreateAchievementStats();
        CreatePlayerPhotoCapture();

        // Two players sharing one road/score/speed — co-op, not split-screen.
        // Right player: IJKL for lane/jump/duck (its own joystick's
        // keyboard fallback). Left player: WASD for lane/jump/duck (its own
        // distance-sensor's keyboard fallback). No accel/brake keys
        // anymore — the road always accelerates on its own (SpeedController)
        // and braking was removed from every control scheme entirely.
        // Each also gets a gesture-sensor simulator (disabled by default): 2
        // distance sensors per player (left hand, right hand), 2 keys each
        // (up/down) — the up key doubles as the flap key (rapid taps =
        // jump), matching the on-screen square grid in CreateGesturePanel:
        // Right player:  U O       Left player:   Q E
        //                J L                       A D
        //
        // Start lanes sit as close to the road's own true center as
        // possible instead of the outer edges — per feedback, the 2 lanes
        // closest to center (even LaneCount: directly adjacent, e.g. 1&2
        // of 0..3; odd LaneCount: flanking the exact middle lane, e.g. 0&2
        // of 0..2, skipping the middle one). For LaneCount==3 this lands on
        // the same lanes the old hardcoded "outermost" start already used,
        // so behavior only actually changes once LaneCount > 3.
        GetStartLanes(out int startLaneRight, out int startLaneLeft);
        // IJKL, not arrows — per feedback, player-right is now the joystick
        // player (see StartScreenController's joystickRight), and this is
        // its keyboard-only fallback; IJKL sits on the keyboard's own right
        // side, same relative hand position WASD gives player-left.
        // P1 (PlayerLeft, sensors) = красный LadyBug1; P2 (PlayerRight, joystick)
        // = сине-фиолетовый LadyBug2 — одинаково в меню, игре и превью тренировки.
        GameObject playerRight = CreatePlayer("PlayerRight", KeyCode.J, KeyCode.L, KeyCode.I, KeyCode.K,
            KeyCode.U, KeyCode.J, KeyCode.O, KeyCode.L, startLaneRight, PlayerTwoBugTint, PlayerTwoBugSprite);
        GameObject playerLeft = CreatePlayer("PlayerLeft", KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.S,
            KeyCode.Q, KeyCode.A, KeyCode.E, KeyCode.D, startLaneLeft, PlayerOneBugTint, PlayerOneBugSprite);

        CreateCamera(playerRight.transform);
        CreateRoad();
        CreateSideGround();
        CreateRoadShoulder();
        // DebugRunConfig.OnlyEntity isolates one object for inspection. Its
        // pool filtering inside CreateSpawner only reaches the road entities,
        // so everything that has its OWN spawner has to be skipped here:
        // the big arch, the roadside scenery, the shoulder decor, and (inside
        // CreateSky) the clouds and birds. The road, ground, shoulder and sky
        // backdrop stay — with those gone there is no sense of motion left to
        // judge the animation against.
        if (!DebugRunConfig.IsolatingSingleEntity)
            CreateShoulderDecor();
        CreateSpawner();
        if (!DebugRunConfig.IsolatingSingleEntity)
        {
            CreateBigArchSpawner();
            CreateSideScenery();
        }
        CreateSky();
        CreateAudio();
        CreateHelpScreen();
        CreateScoreUI(out Canvas scoreCanvas);
        CreateWinSequence(scoreCanvas);
        CreateTricksUI();
        (GameObject gestureCanvasLeft, GameObject gestureCanvasRight) = CreateGestureIndicators(playerRight, playerLeft);
        CreateStartScreen(playerRight, playerLeft, gestureCanvasLeft, gestureCanvasRight);
        CreatePauseDialog();
        CreateExitGesture();
        // Loader (attract-mode, hold one of the 7 game controls) + the
        // flower-fill/countdown intro for whichever slot was held. Both were
        // temporarily skipped so test runs booted straight into the menu; back
        // on now, which is what the real cabinet needs.
        //
        // StartScreenCanvas sits ready underneath these the whole time (see
        // IntroSequence's class comment) and is uncovered by
        // IntroSequence.Finish(), which is also what calls the menu's
        // OnRevealed() and — for slot 0 only — PlayMusic(). While these were
        // skipped, StartScreenController.Awake did both itself; it now checks
        // for an IntroSequence in the scene and stands down when one exists,
        // so re-enabling here does not start the menu music underneath the
        // loader.
        //
        // Both are built unconditionally, including for the arcade cabinet: the
        // cabinet's shared launcher shows an attract screen and a hold-to-launch
        // animation of its own before it loads this game, so there the two stand
        // down at RUNTIME rather than being cut from the scene. See
        // ArcadeControlsReader.InsideArcadeLauncher and its two call sites,
        // LoaderScreenController.Awake and StartScreenController.Awake. Nothing
        // in this generator, and nothing in the scene it writes, differs between
        // the two cases.
        IntroSequence[] gameIntros = CreateAllIntroScreens();
        CreateLoaderScreen(gameIntros);
        CreateScreenInfoLabel();

        // After every generator has run, so the set of live textures is final.
        PruneGeneratedTextures();

        System.IO.Directory.CreateDirectory("Assets/LadyBug/Scenes");
        string scenePath = "Assets/LadyBug/Scenes/Main.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

        Debug.Log("Scene setup complete: " + scenePath);
    }

    [MenuItem("Tools/Rebuild Road Geometry")]
    public static void RebuildRoadGeometry()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Rebuild Road Geometry: остановите Play Mode перед пересборкой.");
            return;
        }

        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "LaneDivider" ||
                root.name is "RoadSurface" or "SideGroundLeft" or "SideGroundRight" or
                "RoadShoulderLeft" or "RoadShoulderRight" or
                "ShoulderDecorSpawner" or "GrassDecorSpawner")
                Object.DestroyImmediate(root);
        }

        CreateRoad();
        CreateSideGround();
        CreateRoadShoulder();
        CreateShoulderDecor();
        CreateBigArchPrefab();

        GetStartLanes(out int startLaneRight, out int startLaneLeft);

        foreach (var player in Object.FindObjectsByType<PlayerController>())
        {
            SerializedObject so = new SerializedObject(player);
            so.FindProperty("laneCount").intValue = LaneCount;
            int startLane = player.name == "PlayerRight" ? startLaneRight : startLaneLeft;
            so.FindProperty("startLane").intValue = startLane;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach (var spawner in Object.FindObjectsByType<EntitySpawner>())
        {
            SerializedObject so = new SerializedObject(spawner);
            so.FindProperty("laneCount").intValue = LaneCount;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach (var spawner in Object.FindObjectsByType<SideScenerySpawner>())
        {
            SerializedObject so = new SerializedObject(spawner);
            so.FindProperty("sideOffset").floatValue = LaneCount * LaneWidth / 2f + 2f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Road geometry rebuilt for " + LaneCount + " lanes.");
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
        message.font = GameFont;
        message.fontSize = 40; // до 5 строк (заголовок + 4 категории) вместо прежних двух
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
        // Раньше сюда влезали ровно 2 строки. Теперь показываются ВСЕ
        // категории, где забег попал в топ (WinSequence.CaptureRecordPhoto),
        // то есть до 5 строк — бокс выше, шрифт мельче, и обязательно Overflow:
        // при Truncate строка, не влезшая в бокс, не обрезается, а выбрасывается
        // целиком (те же грабли, что у стрелок джойстика).
        message.horizontalOverflow = HorizontalWrapMode.Wrap;
        message.verticalOverflow = VerticalWrapMode.Overflow;
        messageRt.sizeDelta = new Vector2(1400f, 230f);
        // Центр там же, где был. Коробка занимает 275..505: сверху остаётся
        // 35px до края канваса (540), снизу 35px до верхнего края
        // CameraPreview (240) — растить дальше некуда ни в одну сторону.
        messageRt.anchoredPosition = new Vector2(0f, 390f);

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

        // Created after (so drawn in front of) CameraPreview above — its box
        // overlaps the top of that 800x600 preview by design (see below),
        // and UI siblings paint in creation order, so this used to render
        // BEHIND the preview once its texture actually had a live camera
        // frame in it (empty/transparent at first, which is why the text
        // still read as visible for a moment before being covered).
        var smileGo = new GameObject("SmileText");
        smileGo.transform.SetParent(canvasGo.transform, false);
        Text smile = smileGo.AddComponent<Text>();
        smile.font = GameFont;
        smile.fontSize = 30;
        smile.fontStyle = FontStyle.Bold;
        smile.alignment = TextAnchor.MiddleCenter;
        smile.color = Color.white;
        smile.text = "УЛЫБНИТЕСЬ\nВАС СНИМАЕТ КАМЕРА ДЛЯ ИСТОРИИ";
        smileGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform smileRt = smile.GetComponent<RectTransform>();
        smileRt.anchorMin = new Vector2(0.5f, 0.5f);
        smileRt.anchorMax = new Vector2(0.5f, 0.5f);
        smileRt.pivot = new Vector2(0.5f, 0.5f);
        // Sits right on top of CameraPreview's own top edge (800x600 at
        // y=-60, so its top is at 240) instead of floating above it with a
        // gap — 2 lines now (was 1, split at the old em-dash) need the
        // taller box. Narrower than the preview's own 800 width so it
        // doesn't spill past its edges.
        smileRt.sizeDelta = new Vector2(760f, 90f);
        smileRt.anchoredPosition = new Vector2(0f, 185f);

        var countdownGo = new GameObject("Countdown");
        countdownGo.transform.SetParent(canvasGo.transform, false);
        Text countdown = countdownGo.AddComponent<Text>();
        countdown.font = GameFont;
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
        // Nudged down slightly (was -60, dead center on the preview) to
        // keep clear of SmileText's now-2-line caption sitting on the
        // preview's own top edge just above.
        countdownRt.anchoredPosition = new Vector2(0f, -90f);

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

        // Gesture (distance-sensor) input — disabled by default; the start
        // screen enables it only when "Датчики" is the chosen controller.
        GestureInput gesture = player.AddComponent<GestureInput>();
        SerializedObject gestureSo = new SerializedObject(gesture);
        gestureSo.FindProperty("leftHandUpKey").intValue = (int)leftHandUp;
        gestureSo.FindProperty("leftHandDownKey").intValue = (int)leftHandDown;
        gestureSo.FindProperty("rightHandUpKey").intValue = (int)rightHandUp;
        gestureSo.FindProperty("rightHandDownKey").intValue = (int)rightHandDown;
        gestureSo.ApplyModifiedPropertiesWithoutUndo();
        gesture.enabled = false;

        // Joystick input — only ever driven for player 2 (right), see
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
            Texture2D frame1Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + spriteFile);
            Texture2D frame2Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + spriteFile.Replace(".png", "Frame2.png"));
            Texture2D frame3Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + spriteFile.Replace(".png", "Frame3.png"));
            Texture2D frame4Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + spriteFile.Replace(".png", "Frame4.png"));

            var groundFrames = new System.Collections.Generic.List<Texture2D> { frame1Tex, frame3Tex, frame2Tex, frame4Tex };
            groundFrames.RemoveAll(t => t == null);

            SerializedProperty framesProp = animatorSo.FindProperty("groundFrames");
            framesProp.arraySize = groundFrames.Count;
            for (int i = 0; i < groundFrames.Count; i++)
                framesProp.GetArrayElementAtIndex(i).objectReferenceValue = groundFrames[i];

            // Airborne cycle — wings-open frames, same "FrameN" edit-of-frame1
            // convention but under an "AirN" suffix instead.
            Texture2D air1Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + spriteFile.Replace(".png", "Air1.png"));
            Texture2D air2Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + spriteFile.Replace(".png", "Air2.png"));

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
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
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
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + spriteFile);
        if (tex == null)
        {
            Debug.LogWarning("LadyBug sprite not found at Assets/LadyBug/Sprites/" + spriteFile);
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
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        Material material = new Material(shader) { mainTexture = tex, color = tint };
        renderer.sharedMaterial = material;

        return sprite.transform;
    }

    const float MainCameraY = 4f;
    const float MainCameraZ = -8f;
    static readonly Vector3 MainCameraPosition = new Vector3(0f, MainCameraY, MainCameraZ);
    static readonly Vector3 MainCameraLookAt = new Vector3(0f, 0.5f, 20f);

    static void CreateCamera(Transform lookTarget)
    {
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        // Fallback fill only — the actual sky is CreateSkyBackground's big
        // painted quad (see CreateSky), which covers the frustum in front
        // of this. Matches that image's own top color in case of any edge
        // gap instead of Unity's plain default procedural skybox gradient.
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.fieldOfView = 60f;
        cam.farClipPlane = 1500f;
        camGo.transform.position = MainCameraPosition;
        camGo.transform.LookAt(MainCameraLookAt);
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
        Shader roadShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
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
            CreateDashedDivider(dividerX, i * 101); // distinct seedOffset per divider line — see CreateDashTexture
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
        float grassCenterOffset = RoadGeometryRuntime.GrassCenterOffset;
        // Real generated cartoon grass artwork (asset_gen/gen_asset.sh,
        // Assets/LadyBug/Sprites/GrassTile.png — small tufts scattered over a flat
        // green base, tiles reasonably cleanly) instead of a flat color —
        // a plain fill read as "green plastic", not grass, from any
        // distance close enough to actually see it.
        Texture2D grassTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/GrassTile.png");
        Shader grassShader = SideGrassUprightShader
            ? Shader.Find("lady_bug/SideGrass") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default")
            : Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        System.IO.Directory.CreateDirectory("Assets/LadyBug/Materials/lady_bug");

        foreach (float side in new[] { -1f, 1f })
        {
            bool isLeft = side < 0f;
            Material grassMaterial = new Material(grassShader) { color = new Color(0.55f, 0.72f, 0.35f) };
            if (grassTexture != null)
            {
                grassMaterial.mainTexture = grassTexture;
                grassMaterial.mainTextureScale = new Vector2(sideWidth / GrassTextureTileSize, RoadLength / GrassTextureTileSize);
            }
            if (SideGrassUprightShader && grassShader.name.Contains("SideGrass"))
            {
                grassMaterial.SetFloat("_StrokeRotation", -44f);
                grassMaterial.SetFloat("_PerspectiveSkew", 0.38f);
                grassMaterial.SetFloat("_SideSign", isLeft ? 1f : -1f);
            }

            string grassMatPath = isLeft
                ? "Assets/LadyBug/Materials/lady_bug/SideGrassLeft.mat"
                : "Assets/LadyBug/Materials/lady_bug/SideGrassRight.mat";
            AssetDatabase.DeleteAsset(grassMatPath);
            AssetDatabase.CreateAsset(grassMaterial, grassMatPath);
            Material sideGrassMaterial = AssetDatabase.LoadAssetAtPath<Material>(grassMatPath);

            GameObject ground = new GameObject(isLeft ? "SideGroundLeft" : "SideGroundRight");
            float x = side * (roadWidth / 2f + grassCenterOffset);
            ground.transform.position = new Vector3(x, -0.052f, RoadCenterZ);
            ground.transform.localScale = new Vector3(sideWidth, 0.1f, RoadLength);

            MeshFilter meshFilter = ground.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = ScrollingTexture.StripMesh;
            Renderer renderer = ground.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = sideGrassMaterial;

            // Same trick as the road surface (CreateRoad) — the strip itself
            // never moves, but its texture offset animates so the grass
            // streams past at the same rate as everything else on the road,
            // instead of looking frozen next to it.
            ScrollingTexture scroller = ground.AddComponent<ScrollingTexture>();
            scroller.EnsureStripMesh();
            SerializedObject grassSo = new SerializedObject(scroller);
            grassSo.FindProperty("dashPeriod").floatValue = GrassTextureTileSize;
            grassSo.FindProperty("flipVertical").boolValue = false;
            grassSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // Thin dirt-and-gravel strip straddling the road/grass seam on both
    // sides — the asphalt (CreateRoad) used to butt straight up against the
    // grass (CreateSideGround) with a razor-straight, two-flat-colors edge;
    // this sits on top of both, right at that boundary, so the transition
    // reads as a rough, worn shoulder instead of a clean cut. Weighted
    // mostly onto the grass side (just a sliver over the actual pavement)
    // so it doesn't cover much of the drivable lane itself.
    static void CreateRoadShoulder()
    {
        float roadWidth = LaneCount * LaneWidth;
        float shoulderRenderWidth = RoadGeometryRuntime.ShoulderRenderWidth;
        Texture2D shoulderTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/ShoulderTile.png");
        if (shoulderTexture == null)
            return;

        Shader shoulderShader = Shader.Find("Custom/ShoulderTint");
        if (shoulderShader == null)
        {
            Debug.LogWarning("CreateRoadShoulder: Custom/ShoulderTint shader not found — import Assets/LadyBug/Shaders/lady_bug/ShoulderTint.shader");
            return;
        }

        foreach (float side in new[] { -1f, 1f })
        {
            GameObject shoulder = new GameObject(side < 0 ? "RoadShoulderLeft" : "RoadShoulderRight");
            float centerOffset = RoadGeometryRuntime.ShoulderCenterOffset;
            float x = side * (roadWidth / 2f + centerOffset);
            shoulder.transform.position = new Vector3(x, -0.048f, RoadCenterZ);
            shoulder.transform.localScale = new Vector3(shoulderRenderWidth, 0.1f, RoadLength);

            MeshFilter meshFilter = shoulder.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = ShoulderTintScroller.StripMesh;
            Renderer renderer = shoulder.AddComponent<MeshRenderer>();
            Material material = new Material(shoulderShader)
            {
                mainTexture = shoulderTexture,
                // One full tile across shoulder width — a partial tile (2.5/4)
                // sliced stones at the inner/outer strip edges.
                mainTextureScale = new Vector2(1f, RoadLength / GrassTextureTileSize)
            };
            renderer.sharedMaterial = material;

            ShoulderTintScroller scroller = shoulder.AddComponent<ShoulderTintScroller>();
            SerializedObject shoulderSo = new SerializedObject(scroller);
            shoulderSo.FindProperty("scrollPeriod").floatValue = GrassTextureTileSize;
            shoulderSo.FindProperty("flipVertical").boolValue = false;
            shoulderSo.FindProperty("roadEdgeAtHighU").boolValue = side < 0f;
            shoulderSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // Rocks and grass tufts on the shoulder; grass-only copies also spawn
    // on the side grass strip via GrassDecorSpawner.
    static readonly (string name, string file, float height)[] ShoulderDecorSprites =
    {
        ("ShoulderRock1", "ShoulderRock1.png", 0.9f),
        ("ShoulderRock2", "ShoulderRock2.png", 0.75f),
        ("ShoulderRock3", "ShoulderRock3.png", 1.05f),
        ("ShoulderGrass1", "ShoulderGrass1.png", 0.6f),
        ("ShoulderGrass2", "ShoulderGrass2.png", 0.7f),
        ("ShoulderGrass3", "ShoulderGrass3.png", 0.85f),
    };

    static void CreateShoulderDecor()
    {
        System.IO.Directory.CreateDirectory("Assets/LadyBug/Prefabs/lady_bug");

        var shoulderPrefabs = new System.Collections.Generic.List<GameObject>();
        var grassPrefabs = new System.Collections.Generic.List<GameObject>();
        foreach (var (name, file, height) in ShoulderDecorSprites)
        {
            GameObject prefab = CreateShoulderDecorPrefab(name, "Assets/LadyBug/Sprites/lady_bug/" + file, height);
            if (prefab == null)
                continue;
            shoulderPrefabs.Add(prefab);
            if (name.Contains("Grass"))
                grassPrefabs.Add(prefab);
        }

        if (shoulderPrefabs.Count == 0 && grassPrefabs.Count == 0)
        {
            Debug.LogWarning("ShoulderDecor: no sprites found — run asset_gen/gen_shoulder_decal_assets.sh then Rebuild Scene");
            return;
        }

        float roadWidth = LaneCount * LaneWidth;
        const float shoulderGap = RoadGeometryRuntime.ShoulderGap;

        if (shoulderPrefabs.Count > 0)
        {
            var spawnerGo = new GameObject("ShoulderDecorSpawner");
            ShoulderDecorSpawner spawner = spawnerGo.AddComponent<ShoulderDecorSpawner>();
            SerializedObject so = new SerializedObject(spawner);
            SetPrefabArray(so, "prefabs", shoulderPrefabs);
            so.FindProperty("roadHalfWidth").floatValue = roadWidth / 2f;
            so.FindProperty("shoulderWidth").floatValue = 2.5f;
            so.FindProperty("shoulderGap").floatValue = shoulderGap;
            so.FindProperty("spawnZ").floatValue = RoadCenterZ + RoadLength / 2f - 5f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        if (grassPrefabs.Count > 0)
        {
            var grassGo = new GameObject("GrassDecorSpawner");
            GrassDecorSpawner grassSpawner = grassGo.AddComponent<GrassDecorSpawner>();
            SerializedObject grassSo = new SerializedObject(grassSpawner);
            SetPrefabArray(grassSo, "prefabs", grassPrefabs);
            grassSo.FindProperty("roadHalfWidth").floatValue = roadWidth / 2f;
            grassSo.FindProperty("shoulderWidth").floatValue = 2.5f;
            grassSo.FindProperty("shoulderGap").floatValue = shoulderGap;
            grassSo.FindProperty("spawnZ").floatValue = RoadCenterZ + RoadLength / 2f - 5f;
            grassSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // First opaque scan row from the PNG bottom — trims empty transparent
    // padding so decal bottoms sit flush on the ground/shoulder surface.
    static float GetBottomTrimNormalized(string assetPath, float alphaThreshold = 0.1f)
    {
        string relative = assetPath.StartsWith("Assets/") ? assetPath.Substring(7) : assetPath;
        string fullPath = System.IO.Path.Combine(Application.dataPath, relative);
        if (!System.IO.File.Exists(fullPath))
            return 0f;

        byte[] bytes = System.IO.File.ReadAllBytes(fullPath);
        var temp = new Texture2D(2, 2);
        if (!temp.LoadImage(bytes))
        {
            Object.DestroyImmediate(temp);
            return 0f;
        }

        int w = temp.width;
        int h = temp.height;
        Color32[] pixels = temp.GetPixels32();
        Object.DestroyImmediate(temp);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (pixels[y * w + x].a / 255f > alphaThreshold)
                    return y / (float)h;
            }
        }

        return 0f;
    }

    static GameObject CreateShoulderDecorPrefab(string name, string texturePath, float height)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (tex == null)
            return null;

        float bottomTrimNorm = GetBottomTrimNormalized(texturePath);
        float visibleFraction = 1f - bottomTrimNorm;
        float visibleHeight = height * visibleFraction;
        float aspect = (float)tex.width / tex.height;
        float spriteWidth = height * aspect;

        var root = new GameObject(name);
        root.transform.position = new Vector3(0f, visibleHeight / 2f, 0f);
        root.AddComponent<MovingEntity>();
        GroundDecorInfo decorInfo = root.AddComponent<GroundDecorInfo>();
        decorInfo.visibleHeight = visibleHeight;
        decorInfo.visibleHalfWidth = spriteWidth * 0.5f;

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = "Sprite";
        Object.DestroyImmediate(sprite.GetComponent<Collider>());
        sprite.transform.SetParent(root.transform);
        sprite.transform.localScale = new Vector3(spriteWidth, height, 1f);
        sprite.transform.localPosition = Vector3.zero;

        Renderer renderer = sprite.GetComponent<Renderer>();
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        Material material = new Material(shader)
        {
            mainTexture = tex,
            mainTextureOffset = new Vector2(0f, bottomTrimNorm),
            mainTextureScale = new Vector2(1f, visibleFraction)
        };

        System.IO.Directory.CreateDirectory("Assets/LadyBug/Materials/lady_bug");
        string materialPath = "Assets/LadyBug/Materials/lady_bug/" + name + ".mat";
        AssetDatabase.DeleteAsset(materialPath);
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;

        string savePath = "Assets/LadyBug/Prefabs/lady_bug/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void CreateDashedDivider(float x, int seedOffset)
    {
        GameObject divider = GameObject.CreatePrimitive(PrimitiveType.Plane);
        divider.name = "LaneDivider";
        // Default Plane is 10x10 units; scale to a thin strip covering the road length.
        divider.transform.position = new Vector3(x, 0.02f, RoadCenterZ);
        divider.transform.localScale = new Vector3(0.03f, 1f, RoadLength / 10f);
        Object.DestroyImmediate(divider.GetComponent<MeshCollider>());

        Renderer renderer = divider.GetComponent<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        Material material = new Material(shader)
        {
            mainTexture = CreateDashTexture(seedOffset),
            // One full texture tile now contains DashVariantCount stacked
            // dash styles (see CreateDashTexture), so a full tile-repeat
            // spans DashVariantCount * DashPeriod world units, not just
            // DashPeriod — keeps each individual dash the same on-road
            // length as before while cycling through the styles.
            mainTextureScale = new Vector2(1f, RoadLength / (DashPeriod * DashVariantCount))
        };
        renderer.sharedMaterial = material;

        ScrollingTexture scroller = divider.AddComponent<ScrollingTexture>();
        SerializedObject so = new SerializedObject(scroller);
        so.FindProperty("dashPeriod").floatValue = DashPeriod * DashVariantCount;
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
        ("Rabbit", "Rabbit.png", 1.74f, -1), // +20% — читался мелким рядом с остальными зверями; коллайдер не тронут (см. LaneObjectColliderHeightOverrides)
        ("Crow", "Crow.png", 1.1f, -1),
        ("Snake", "Snake.png", 1.9f, -1),
        ("SandPile", "SandPile.png", 1.3f, -1),
        ("BrickPile", "BrickPile.png", 1.3f, -1),
        ("WoodPile", "WoodPile.png", 1.2f, -1),
        ("RockPile", "RockPile.png", 1.4f, -1),
    };

    // Living creatures among LaneObjects that drift sideways between lanes (LaneWalker).
    static readonly string[] WanderingAnimals = { "Dog", "Cat", "Crow", "Rabbit", "Snake" };

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
        { "Snake", 1.0f }, // высокая стойка кобры выше прыжка (1.4) — бокс только по телу у земли
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
        System.IO.Directory.CreateDirectory("Assets/LadyBug/Prefabs/lady_bug");

        // Three separate pools so the spawner can pick in two steps:
        // good vs. bad, then — only for bad — jump-over vs. duck-under.
        var goodPrefabs = new System.Collections.Generic.List<GameObject>();
        var badJumpPrefabs = new System.Collections.Generic.List<GameObject>();

        foreach (var (name, file, height, score) in LaneObjects)
        {
            bool canWander = System.Array.IndexOf(WanderingAnimals, name) >= 0;
            float? widthOverride = LaneObjectWidthOverrides.TryGetValue(name, out float overrideWidth) ? overrideWidth : (float?)null;
            float? colliderHeightOverride = LaneObjectColliderHeightOverrides.TryGetValue(name, out float overrideColliderHeight) ? overrideColliderHeight : (float?)null;
            GameObject prefab = CreateEntityPrefab(name, "Assets/LadyBug/Sprites/lady_bug/" + file, height, "Assets/LadyBug/Prefabs/lady_bug/" + name + ".prefab", score, canWander, widthOverride, colliderHeightOverride);
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


        var badDuckPrefabs = new System.Collections.Generic.List<GameObject>();
        GameObject arch = CreateArchPrefab();
        if (arch != null)
            badDuckPrefabs.Add(arch);

        // Debug: keep only the one prefab under inspection, in whichever pool
        // it belongs to, and empty the rest. EntitySpawner picks good-vs-bad
        // first and then a prefab from that pool, so leaving the other pools
        // populated would still let everything else through.
        if (!string.IsNullOrEmpty(DebugRunConfig.OnlyEntity))
        {
            bool Keep(GameObject p) => p != null && p.name == DebugRunConfig.OnlyEntity;
            int Found(System.Collections.Generic.List<GameObject> list)
            {
                int n = 0;
                foreach (GameObject p in list)
                    if (Keep(p)) n++;
                return n;
            }
            int found = Found(goodPrefabs) + Found(badJumpPrefabs) + Found(badDuckPrefabs);
            if (found == 0)
                Debug.LogWarning($"DebugRunConfig.OnlyEntity = \"{DebugRunConfig.OnlyEntity}\" matches no prefab — the road will be empty.");

            GameObject only = null;
            foreach (var list in new[] { goodPrefabs, badJumpPrefabs, badDuckPrefabs })
                foreach (GameObject p in list)
                    if (Keep(p)) only = p;

            // Put it in ALL THREE pools, not just its own. EntitySpawner picks
            // good-vs-bad first and only then a prefab, so leaving the other
            // pools empty means every tick that rolled the empty side spawns
            // nothing — at goodChance 0.5 that halves the rate, and before the
            // game starts PickPrefab only ever reads goodPrefabs, so a bad
            // object would never show on the start screen at all.
            goodPrefabs.Clear(); badJumpPrefabs.Clear(); badDuckPrefabs.Clear();
            if (only != null)
            {
                goodPrefabs.Add(only); badJumpPrefabs.Add(only); badDuckPrefabs.Add(only);
            }
            Debug.LogWarning($"DebugRunConfig.OnlyEntity is set to \"{DebugRunConfig.OnlyEntity}\" — the road spawns nothing else, and the scenery, shoulder decor, clouds and birds are skipped. Clear it for a normal run.");
        }

        var spawnerGo = new GameObject("Spawner");
        EntitySpawner spawner = spawnerGo.AddComponent<EntitySpawner>();

        SerializedObject so = new SerializedObject(spawner);
        SetPrefabArray(so, "goodPrefabs", goodPrefabs);
        SetPrefabArray(so, "badJumpPrefabs", badJumpPrefabs);
        SetPrefabArray(so, "badDuckPrefabs", badDuckPrefabs);
        so.FindProperty("laneCount").intValue = LaneCount;
        so.FindProperty("laneWidth").floatValue = RoadLayout.LaneWidthFor(LaneCount);
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
        System.IO.Directory.CreateDirectory("Assets/LadyBug/Prefabs/lady_bug");

        var prefabs = new System.Collections.Generic.List<GameObject>();
        foreach (var (name, file, height) in SceneryObjects)
        {
            GameObject prefab = CreateEntityPrefab(name, "Assets/LadyBug/Sprites/lady_bug/" + file, height, "Assets/LadyBug/Prefabs/lady_bug/" + name + ".prefab");
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
        System.IO.Directory.CreateDirectory("Assets/LadyBug/Prefabs/lady_bug");

        CreateSkyBackground();

        // Backdrop only while isolating one object — drifting clouds and
        // passing birds are exactly the moving clutter that makes it hard to
        // watch a single animation (see DebugRunConfig.OnlyEntity).
        if (DebugRunConfig.IsolatingSingleEntity)
            return;

        var prefabs = new System.Collections.Generic.List<GameObject>();
        foreach (var (name, file, height) in CloudSprites)
        {
            GameObject prefab = CreateCloudPrefab(name, "Assets/LadyBug/Sprites/lady_bug/" + file, height);
            if (prefab != null)
                prefabs.Add(prefab);
        }

        var spawnerGo = new GameObject("CloudSpawner");
        CloudSpawner spawner = spawnerGo.AddComponent<CloudSpawner>();
        SerializedObject so = new SerializedObject(spawner);
        SetPrefabArray(so, "prefabs", prefabs);
        so.FindProperty("spawnZJitter").floatValue = 7f;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject birdPrefab = CreateBirdPrefab();
        if (birdPrefab != null)
        {
            var birdSpawnerGo = new GameObject("BirdSpawner");
            BirdSpawner birdSpawner = birdSpawnerGo.AddComponent<BirdSpawner>();
            SerializedObject birdSo = new SerializedObject(birdSpawner);
            birdSo.FindProperty("prefab").objectReferenceValue = birdPrefab;
            birdSo.FindProperty("minInterval").floatValue = 2.5f;
            birdSo.FindProperty("maxInterval").floatValue = 6f;
            birdSo.FindProperty("minSpawnCount").intValue = 1;
            birdSo.FindProperty("maxSpawnCount").intValue = 2;
            birdSo.FindProperty("initialMinCount").intValue = 4;
            birdSo.FindProperty("initialMaxCount").intValue = 8;
            birdSo.FindProperty("spawnZJitter").floatValue = 7f;
            birdSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // Sun removed from the scene per feedback — CreateSunSprite/SunArc
        // stay in place (not deleted) in case it comes back.
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
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        Material material = new Material(shader) { mainTexture = tex };

        System.IO.Directory.CreateDirectory("Assets/LadyBug/Materials/lady_bug");
        string materialPath = "Assets/LadyBug/Materials/lady_bug/" + name + ".mat";
        AssetDatabase.DeleteAsset(materialPath); // safe to rerun Rebuild Scene
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;

        string savePath = "Assets/LadyBug/Prefabs/lady_bug/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject CreateBirdPrefab()
    {
        const string name = "SkyBird";
        var root = new GameObject(name);
        SkyBird bird = root.AddComponent<SkyBird>();
        SerializedObject birdSo = new SerializedObject(bird);
        birdSo.FindProperty("wingSpan").floatValue = 2.0f;
        birdSo.FindProperty("wingDropMin").floatValue = -1.1f;
        birdSo.FindProperty("wingDropMax").floatValue = 1.15f;
        birdSo.FindProperty("wingBow").floatValue = 0.14f;
        birdSo.FindProperty("wingSegments").intValue = 8;
        birdSo.FindProperty("bobAmplitude").floatValue = 0.65f;
        birdSo.ApplyModifiedPropertiesWithoutUndo();

        LineRenderer line = root.GetComponent<LineRenderer>();
        line.numCornerVertices = 4;
        line.numCapVertices = 2;
        line.useWorldSpace = false;
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        Material material = new Material(shader) { color = new Color(0.12f, 0.12f, 0.16f, 0.9f) };

        System.IO.Directory.CreateDirectory("Assets/LadyBug/Materials/lady_bug");
        string materialPath = "Assets/LadyBug/Materials/lady_bug/" + name + ".mat";
        AssetDatabase.DeleteAsset(materialPath);
        AssetDatabase.CreateAsset(material, materialPath);
        line.material = material;
        line.startColor = material.color;
        line.endColor = material.color;

        string savePath = "Assets/LadyBug/Prefabs/lady_bug/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // Painted sky backdrop — full SkyBackground.png (UV 0–1) on a quad that
    // covers only the frame above the road vanishing point. Bottom edge aligns
    // with the far end of RoadSurface (z = RoadCenterZ + RoadLength/2), not
    // y=0 at infinity — that mismatch caused the black band. Camera clear
    // stays black below the horizon (and in ground-strip gaps).
    const float SkyBackdropZ = 400f;
    const float SkyBackdropCenterY = 82f;
    const float SkyBackdropHeight = 195f;
    const float SkyBackdropWidth = 1170f;
    // Hide texture below the sun disc (~centroid V≈0.18 in Unity UV space).
    const float SkyBackdropCropMinV = 0.28f;

    static void CreateSkyBackground()
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/SkyBackground.png");
        if (tex == null)
        {
            Debug.LogWarning("Texture not found: Assets/LadyBug/Sprites/SkyBackground.png");
            return;
        }

        var sky = new GameObject("SkyBackground");
        sky.transform.position = new Vector3(0f, SkyBackdropCenterY, SkyBackdropZ);
        // Face the camera so the backdrop is parallel to the view plane —
        // an axis-aligned wall at z=400 looked tilted because the camera
        // pitches down toward the road.
        sky.transform.LookAt(MainCameraPosition);

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = "Sprite";
        Object.DestroyImmediate(sprite.GetComponent<Collider>());
        sprite.transform.SetParent(sky.transform);
        sprite.transform.localPosition = Vector3.zero;
        sprite.transform.localScale = new Vector3(SkyBackdropWidth, SkyBackdropHeight, 1f);

        Renderer renderer = sprite.GetComponent<Renderer>();
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        Material material = new Material(shader) { mainTexture = tex };
        material.mainTextureScale = new Vector2(1f, 1f - SkyBackdropCropMinV);
        material.mainTextureOffset = new Vector2(0f, SkyBackdropCropMinV);

        System.IO.Directory.CreateDirectory("Assets/LadyBug/Materials/lady_bug");
        string materialPath = "Assets/LadyBug/Materials/lady_bug/SkyBackground.mat";
        AssetDatabase.DeleteAsset(materialPath); // safe to rerun Rebuild Scene
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;
    }

    static void CreateSunSprite()
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/Sun.png");
        if (tex == null)
        {
            Debug.LogWarning("Texture not found: Assets/LadyBug/Sprites/Sun.png");
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
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
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
        sfxSo.FindProperty("pickupClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/LadyBug/Audio/lady_bug/PickupPositive.mp3");
        sfxSo.FindProperty("dogClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/LadyBug/Audio/lady_bug/BadDog.mp3");
        sfxSo.FindProperty("catClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/LadyBug/Audio/lady_bug/BadCat.mp3");
        sfxSo.FindProperty("crowClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/LadyBug/Audio/lady_bug/BadCrow.mp3");
        sfxSo.FindProperty("snakeClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/LadyBug/Audio/lady_bug/SnakeHiss.mp3");
        sfxSo.FindProperty("hitGenericClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/LadyBug/Audio/lady_bug/HitGeneric.mp3");
        sfxSo.FindProperty("trickClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/LadyBug/Audio/lady_bug/TrickApplause.mp3");
        sfxSo.ApplyModifiedPropertiesWithoutUndo();

        AudioSource shiftSource = audioGo.AddComponent<AudioSource>();
        shiftSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/LadyBug/Audio/lady_bug/GearShift.wav");
        shiftSource.playOnAwake = false;

        AudioSource humSource = audioGo.AddComponent<AudioSource>();
        humSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/LadyBug/Audio/lady_bug/EngineHum.mp3");
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
        feetSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/LadyBug/Audio/lady_bug/RunFeet.mp3");
        feetSource.loop = true;
        feetSource.playOnAwake = true;
        feetSource.volume = 0f;

        AudioSource wingsSource = player.AddComponent<AudioSource>();
        wingsSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/LadyBug/Audio/lady_bug/Buzz.wav");
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
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        Material material = new Material(shader) { mainTexture = tex };

        // Prefab assets can only reference materials that are themselves saved
        // assets — an in-memory Material here would serialize as a broken
        // (magenta) reference once written to disk.
        System.IO.Directory.CreateDirectory("Assets/LadyBug/Materials/lady_bug");
        string materialPath = "Assets/LadyBug/Materials/lady_bug/" + name + ".mat";
        AssetDatabase.DeleteAsset(materialPath); // safe to rerun Rebuild Scene
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;

        AttachFrameAnimation(name, root, renderer);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // Objects that have a real frame set in Assets/Sprites/lady_bug/<name>Frames/
    // animate off it instead of LaneWalker's placeholder wiggle. Frames are
    // picked up by folder convention rather than a table, so adding a set for
    // the next creature is a matter of dropping the files in — nothing here
    // needs editing.
    static void AttachFrameAnimation(string name, GameObject root, Renderer renderer)
    {
        string dir = "Assets/LadyBug/Sprites/lady_bug/" + name + "Frames";
        if (!AssetDatabase.IsValidFolder(dir))
            return;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { dir });
        var paths = new System.Collections.Generic.List<string>();
        foreach (string guid in guids)
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        paths.Sort(System.StringComparer.Ordinal); // DogWalk01..10 — plain name order is frame order

        var frameList = new System.Collections.Generic.List<Texture2D>();
        foreach (string path in paths)
        {
            Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t != null)
                frameList.Add(t);
        }

        if (frameList.Count == 0)
            return;

        Texture2D[] frames = frameList.ToArray();

        SpriteFrameAnimator anim = root.AddComponent<SpriteFrameAnimator>();
        SerializedObject animSo = new SerializedObject(anim);
        animSo.FindProperty("targetRenderer").objectReferenceValue = renderer;
        SerializedProperty framesProp = animSo.FindProperty("frames");
        framesProp.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++)
            framesProp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        animSo.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"{name}: frame animation from {dir} ({frames.Length} frames)");
    }

    // Flat ground decal — a Quad rotated flat on the road (same trick as
    // every other decal in this file) instead of a standing sprite. Used for
    // the pothole and both puddle colors — jump-over obstacles, no duck rule
    // (there's nothing to duck under, it's a hole/spill in the road).
    static GameObject CreateGroundDecalPrefab(string name, string textureFile, float size)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + textureFile);
        if (tex == null)
        {
            Debug.LogWarning("Texture not found: Assets/LadyBug/Sprites/" + textureFile);
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
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        Material material = new Material(shader) { mainTexture = tex };

        System.IO.Directory.CreateDirectory("Assets/LadyBug/Materials/lady_bug");
        string materialPath = "Assets/LadyBug/Materials/lady_bug/" + name + ".mat";
        AssetDatabase.DeleteAsset(materialPath);
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(size * aspect, 0.1f, size);

        string savePath = "Assets/LadyBug/Prefabs/lady_bug/" + name + ".prefab";
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
        text.font = GameFont;
        text.fontSize = 23;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = "СУТЬ ИГРЫ\n"
                  + "Дорога сама разгоняется всё быстрее, собирать хорошее, избегать плохое, вдвоём — делать трюки\n\n"
                  + "ЦЕЛЬ\n"
                  + "Пробежать 100 км за самое короткое время, дополнительно набирая очки, трюки и скорость\n\n"
                  + "УПРАВЛЕНИЕ\n"
                  + "Правый: ← → полоса, ↑ прыжок, ↓ пригнуться\n"
                  + "Левый: A D полоса, W прыжок, S пригнуться\n"
                  + "Газа и тормоза больше нет — дорога разгоняется сама, столкновения замедляют\n\n"
                  + "ТРЮКИ\n"
                  + "АРКА: один приседает под аркой, другой в этот момент перепрыгивает её вместе с ним\n"
                  + "КОЛЬЦО: игроки одновременно меняются полосами — один в прыжке, другой понизу\n\n"
                  + "ДАТЧИКИ РАССТОЯНИЯ\n"
                  + "2 датчика на игрока — по одному на руку (только верх/низ):\n"
                  + "обе руки вниз — пригнуться, одна вверх/другая вниз — полоса в сторону опущенной руки,\n"
                  + "быстро жать «верх» на обеих руках разом — прыжок-полёт\n\n"
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
        hint.font = GameFont;
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
    static void CreatePanelLabel(Transform parent, string text, Color? color = null)
    {
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(parent, false);
        Text label = labelGo.AddComponent<Text>();
        label.font = GameFont;
        label.fontSize = 31; // 27 * 1.15
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.UpperCenter;
        label.color = color ?? new Color(1f, 1f, 1f); // bright white — was a dim 0.85 gray, per feedback all 4 panel labels should share one brighter color
        label.text = text;
        RectTransform labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 1f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.pivot = new Vector2(0.5f, 1f);
        labelRt.sizeDelta = new Vector2(0f, 45f); // 39 * 1.15
        labelRt.anchoredPosition = new Vector2(0f, -10f); // 9 * 1.15
    }

    // Gameplay HUD panels fan out from each top corner as real pizza-slice
    // wedges sharing one apex (see CreateWedgeTexture/PositionWedgePanel/
    // CreateWedgeContent) instead of stacking straight down the edge —
    // ДИСТАНЦИЯ/ВРЕМЯ split the left quarter into 2 equal slices (same as
    // ОЧКИ/ТРЮКИ on the right) so each pair of wedges fully fills its
    // corner's whole 90° edge to edge, no reserved gap between them — the
    // gear+speed hub floats on top of the shared seam instead of owning its
    // own slice (see gearSpeedCenterAngle below). angleDeg (the slice's own
    // center) is measured from the top edge (0°) sweeping down toward the
    // side edge (90°), same convention for both corners.
    const float FanRadius = 483f; // 420 * 1.15 — corner indicators sized up again per feedback
    const float LeftWedgeAngle = 45f; // 90° / 2 panels
    const float RightWedgeAngle = 45f; // 90° / 2 panels
    const float WedgeContentRadius = 328f; // 285 * 1.15, how far out along its slice's centerline a panel's label/value sits

    static void CreateScoreUI(out Canvas canvas)
    {
        var canvasGo = new GameObject("ScoreCanvas");
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // Left-corner fan (ДИСТАНЦИЯ/СКОРОСТЬ/ПЕРЕДАЧА/ВРЕМЯ) — true pizza-
        // slice wedges sharing one apex at the screen corner (see
        // CreateWedgeTexture/PositionWedgePanel), not just individually
        // rotated floating boxes — per feedback, neighbouring panels should
        // actually share an edge like real cut lines. True corner anchor
        // (not a hardcoded 1920x1080 offset) — stays flush against the real
        // corner on any aspect ratio, instead of drifting/clipping when the
        // actual screen isn't exactly 16:9.
        Texture2D distanceWedgeTexture = CreateWedgeTexture(512, LeftWedgeAngle * 2f, true);
        const float leftContentWidth = 259f; // 225 * 1.15

        var distancePanelGo = new GameObject("DistancePanel");
        distancePanelGo.transform.SetParent(canvasGo.transform, false);
        RawImage distancePanelImage = distancePanelGo.AddComponent<RawImage>();
        distancePanelImage.texture = distanceWedgeTexture;

        // Rotated to the corner's own center (45°, not ДИСТАНЦИЯ's own
        // 22.5°) — this panel now carries the WHOLE 90° baked frame image
        // (both ДИСТАНЦИЯ's and ВРЕМЯ's slices at once, see CreateWedgeTexture),
        // so it has to sit centered on the full quarter, not tucked into
        // just the first half of it. ДИСТАНЦИЯ's own content then needs an
        // explicit +22.5° offset to land back at its real 22.5° position
        // relative to this now-differently-rotated parent (45-22.5=22.5,
        // see repo history for the full derivation) — ВРЕМЯ's own panel
        // below is unaffected and keeps localAngleDeg=0 at its original 67.5°.
        RectTransform distancePanelRt = distancePanelGo.GetComponent<RectTransform>();
        PositionWedgePanel(distancePanelRt, false, LeftWedgeAngle, FanRadius, (float)distanceWedgeTexture.height / distanceWedgeTexture.width);

        // Taller box (was 155) — the distance value now reads across 3 lines
        // ("X" / "из" / "Y", see DistanceIndicator) instead of one "X из Y"
        // line, per feedback. Trimmed back down from an initial 290 (which
        // left visibly more empty space above the value text than the
        // smaller 50pt font actually needed, reading almost like a blank
        // line before the first number) now that the font is smaller.
        RectTransform distanceContentRt = CreateWedgeContent(distancePanelGo.transform, false, LeftWedgeAngle * 0.5f, WedgeContentRadius, leftContentWidth, 260f);
        // The taller box above grows from its own center, so its top edge
        // (where the "ДИСТАНЦИЯ" label hangs from, see CreatePanelLabel)
        // moves up along with it. Nudged right and (net) up a bit from
        // where it started, per feedback.
        NudgeContentScreenSpace(distanceContentRt, distancePanelGo.transform, new Vector2(54f, -20f)); // further right and higher still, per feedback

        CreatePanelLabel(distanceContentRt, "ДИСТАНЦИЯ");

        var distanceTextGo = new GameObject("DistanceText");
        distanceTextGo.transform.SetParent(distanceContentRt, false);
        Text distanceText = distanceTextGo.AddComponent<Text>();
        distanceText.font = GameFont;
        distanceText.fontSize = 50; // smaller, per feedback — 3 short lines don't need as large a font as the old single "X из Y" line did
        distanceText.fontStyle = FontStyle.Bold;
        distanceText.alignment = TextAnchor.MiddleCenter;
        distanceText.color = new Color(0.7f, 1f, 0.7f);
        distanceText.verticalOverflow = VerticalWrapMode.Overflow;
        distanceText.text = "0 км\n<size=26>из</size>\n100 км"; // DistanceIndicator overwrites this every frame with the real live value

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

        // Score panel — right-corner fan (ОЧКИ/ТРЮКИ), mirrored from the
        // left-corner one above.
        Texture2D rightWedgeTexture = CreateWedgeTexture(512, RightWedgeAngle * 2f, false);
        const float rightContentWidth = 397f; // 345 * 1.15
        const float scoreAngle = RightWedgeAngle * 0.5f;

        var panelGo = new GameObject("ScorePanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        RawImage panelImage = panelGo.AddComponent<RawImage>();
        panelImage.texture = rightWedgeTexture;

        // Rotated to the corner's own center (RightWedgeAngle=45°, not
        // ОЧКИ's own scoreAngle=22.5°) — this panel now carries the WHOLE
        // 90° baked frame image (both ОЧКИ's and ТРЮКИ's slices at once,
        // see CreateWedgeTexture), so it has to sit centered on the full
        // quarter. ОЧКИ's own content then needs an explicit scoreAngle
        // offset (45-22.5=22.5, see DistancePanel's own comment for the
        // full derivation) to land back at its real 22.5° position — the
        // counterAnchor math below still uses scoreAngle as the true
        // absolute angle, unaffected by this panel's own rotation changing.
        RectTransform panelRt = panelGo.GetComponent<RectTransform>();
        PositionWedgePanel(panelRt, true, RightWedgeAngle, FanRadius, (float)rightWedgeTexture.height / rightWedgeTexture.width);

        RectTransform scoreContentRt = CreateWedgeContent(panelGo.transform, true, scoreAngle, WedgeContentRadius, rightContentWidth, 173f);
        NudgeContentScreenSpace(scoreContentRt, panelGo.transform, new Vector2(-36f, 0f)); // further left still, per repeated feedback

        CreatePanelLabel(scoreContentRt, "ОЧКИ");

        var textGo = new GameObject("ScoreText");
        textGo.transform.SetParent(scoreContentRt, false);
        Text scoreText = textGo.AddComponent<Text>();
        scoreText.font = GameFont;
        scoreText.fontSize = 83;
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

        // Invisible marker at the panel's actual visible content center —
        // popups fly toward this point. Shares the same (0,0) anchor/pivot
        // frame as the popups themselves so their anchoredPosition values
        // are directly comparable (ScorePopup reads target.anchoredPosition
        // raw) — so this is deliberately expressed at reference resolution
        // (1920x1080). panelRt's own pivot is now the wedge's apex (corner),
        // not its visible content, so this reconstructs the content's real
        // on-screen offset the same way PositionWedgePanel/CreateWedgeContent
        // derive it (corner + rotated contentRadius offset), rather than
        // just reading panelRt.anchoredPosition directly. Known minor
        // approximation — on a non-16:9 screen the popup's landing point
        // can drift a little from the panel's actual position, but the
        // panel itself always renders correctly.
        float scoreRad = scoreAngle * Mathf.Deg2Rad;
        Vector2 scoreContentOffset = new Vector2(-Mathf.Cos(scoreRad), -Mathf.Sin(scoreRad)) * WedgeContentRadius;
        Vector2 scoreContentPos = panelRt.anchoredPosition + scoreContentOffset;
        var counterAnchorGo = new GameObject("CounterAnchor");
        counterAnchorGo.transform.SetParent(canvasGo.transform, false);
        RectTransform counterAnchor = counterAnchorGo.AddComponent<RectTransform>();
        counterAnchor.anchorMin = new Vector2(0f, 0f);
        counterAnchor.anchorMax = new Vector2(0f, 0f);
        counterAnchor.pivot = new Vector2(0f, 0f);
        counterAnchor.anchoredPosition = new Vector2(1920f + scoreContentPos.x, 1080f + scoreContentPos.y);

        var managerGo = new GameObject("ScoreManager");
        ScoreManager manager = managerGo.AddComponent<ScoreManager>();

        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("scoreText").objectReferenceValue = scoreText;
        so.FindProperty("popupParent").objectReferenceValue = canvasGo.GetComponent<RectTransform>();
        so.FindProperty("counterAnchor").objectReferenceValue = counterAnchor;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Timer panel — left-corner fan, outermost slice (closest to the
        // side edge). No background image of its own anymore — DistancePanel's
        // one baked frame already covers this whole quarter (see its own
        // comment above), so this is now just an invisible anchor point;
        // its own rotation/content angle are unchanged from before and
        // still land ВРЕМЯ at the correct 67.5°.
        var timerPanelGo = new GameObject("TimerPanel");
        timerPanelGo.transform.SetParent(canvasGo.transform, false);

        // No RawImage on this one anymore, so unlike before, nothing else
        // implicitly provides a RectTransform — has to be added explicitly.
        RectTransform timerPanelRt = timerPanelGo.AddComponent<RectTransform>();
        PositionWedgePanel(timerPanelRt, false, LeftWedgeAngle * 1.5f, FanRadius);

        RectTransform timerContentRt = CreateWedgeContent(timerPanelGo.transform, false, 0f, WedgeContentRadius, leftContentWidth, 121f);
        NudgeContentScreenSpace(timerContentRt, timerPanelGo.transform, new Vector2(0f, -36f)); // lower still, per repeated feedback

        CreatePanelLabel(timerContentRt, "ВРЕМЯ");

        var timerTextGo = new GameObject("TimerText");
        timerTextGo.transform.SetParent(timerContentRt, false);
        Text timerText = timerTextGo.AddComponent<Text>();
        timerText.font = GameFont;
        timerText.fontSize = 52;
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

        // Combined gear+speed hub — left-corner fan, floating right on top
        // of the shared seam between ДИСТАНЦИЯ and ВРЕМЯ (those two wedges
        // now fill the whole 90° corner edge to edge, same as ОЧКИ/ТРЮКИ do
        // on the right) as its own standalone dial instead of a third
        // bounded wedge: a small round badge for the gear digit, right at
        // the apex, with a curved row of tick dots arcing around it — speed
        // within the current gear (0..GearStepKmh) lights them up green
        // through red, like an analog gauge dial instead of a numeric
        // readout (SpeedIndicator drives both). No wedge background/seam of
        // its own — this whole hub just floats over whatever's behind it.
        const float gearSpeedCenterAngle = LeftWedgeAngle; // the seam between the two 45° slices — same convention as RightWedgeAngle for the empty hub below

        var gearSpeedPanelGo = new GameObject("GearSpeedPanel");
        gearSpeedPanelGo.transform.SetParent(canvasGo.transform, false);

        // The gear-digit hub is its own small nested quarter-sector now —
        // same pie shape as the wedge itself (same 90° width, same apex),
        // just smaller and closer in — per feedback that a plain circle
        // badge read as visually inconsistent sitting inside a
        // quarter-sector panel. Keeps the one border stripe removed from
        // every other sector (see CreateWedgeTexture's withBorder) — this
        // is the one shape it's kept for.
        const float gearHubWedgeRadius = 155f;
        Texture2D gearHubWedgeTexture = CreateWedgeTexture(400, LeftWedgeAngle * 2f, true, true, false);
        RawImage gearSpeedPanelImage = gearSpeedPanelGo.AddComponent<RawImage>();
        gearSpeedPanelImage.texture = gearHubWedgeTexture;
        RectTransform gearSpeedPanelRt = gearSpeedPanelGo.GetComponent<RectTransform>();
        PositionWedgePanel(gearSpeedPanelRt, false, gearSpeedCenterAngle, gearHubWedgeRadius, (float)gearHubWedgeTexture.height / gearHubWedgeTexture.width);

        RectTransform gearDigitContentRt = CreateWedgeContent(gearSpeedPanelGo.transform, false, 0f, gearHubWedgeRadius * 0.6f, 115f, 115f);

        var gearDigitGo = new GameObject("GearDigit");
        gearDigitGo.transform.SetParent(gearDigitContentRt, false);
        Text gearDigitText = gearDigitGo.AddComponent<Text>();
        gearDigitText.font = GameFont;
        gearDigitText.fontSize = 69;
        gearDigitText.fontStyle = FontStyle.Bold;
        gearDigitText.alignment = TextAnchor.MiddleCenter;
        gearDigitText.color = Color.white;
        gearDigitText.text = "1";
        gearDigitGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform gearDigitRt = gearDigitText.GetComponent<RectTransform>();
        gearDigitRt.anchorMin = Vector2.zero;
        gearDigitRt.anchorMax = Vector2.one;
        gearDigitRt.offsetMin = Vector2.zero;
        gearDigitRt.offsetMax = Vector2.zero;

        // Ring of tick dots, closer in toward the corner at a shared radius
        // — same radius for all of them is what makes them read as a
        // curved arc/dial instead of a scatter of dots. Colors (green ->
        // red) are assigned at runtime by SpeedIndicator itself, along
        // with which ones are "lit" — these just start dim. Bracketed on
        // both sides by a thin curved guide line, same radius margin
        // in/out, so the row reads as sitting on a dial track.
        const float speedTickRadius = 242f; // 210 * 1.15
        Image[] speedTicks = CreateTickRing(gearSpeedPanelGo.transform, false, 10, speedTickRadius, LeftWedgeAngle - 4f);
        Color speedArcGuideColor = new Color(1f, 1f, 1f, 0.35f);
        CreateArcGuide(gearSpeedPanelGo.transform, false, speedTickRadius - 30f, LeftWedgeAngle - 4f, speedArcGuideColor);
        CreateArcGuide(gearSpeedPanelGo.transform, false, speedTickRadius + 30f, LeftWedgeAngle - 4f, speedArcGuideColor);

        var speedManagerGo = new GameObject("SpeedIndicator");
        SpeedIndicator speedIndicator = speedManagerGo.AddComponent<SpeedIndicator>();
        SerializedObject speedSo = new SerializedObject(speedIndicator);
        speedSo.FindProperty("gearDigitText").objectReferenceValue = gearDigitText;
        SerializedProperty speedTicksProp = speedSo.FindProperty("speedTicks");
        speedTicksProp.arraySize = speedTicks.Length;
        for (int i = 0; i < speedTicks.Length; i++)
            speedTicksProp.GetArrayElementAtIndex(i).objectReferenceValue = speedTicks[i];
        speedSo.ApplyModifiedPropertiesWithoutUndo();

        // HighScoreManager still exists purely as the data layer (ReportRun/
        // GetTopEntries/SetPhotoPath — used by WinSequence and the start-
        // screen TopResultsPage carousel) — its own live in-game "ТОП" HUD
        // panel was removed per feedback: it only ever showed up during
        // actual gameplay (see the old StartScreenController.BeginGame
        // SetActive(true) call, now gone too), and nobody reads a small
        // side panel while actively driving. titleText/rowTexts/rowPhotos
        // are left unwired (null) — UpdateDisplay already no-ops safely
        // when they are.
        var highScoreGo = new GameObject("HighScoreManager");
        highScoreGo.AddComponent<HighScoreManager>();

        // Right-corner counterpart to the left-corner gear+speed hub — same
        // standalone dial shape (quarter-sector badge + arc of tick dots,
        // no wedge background/seam of its own, sitting between ОЧКИ and
        // ТРЮКИ). Same nested-quarter-sector badge shape as the left
        // corner's own, but driven by object pickups (ObjectFeedbackIndicator)
        // instead of live speed telemetry — a happy/sad face in the center
        // badge, and the tick arc fills from the centre outward: good → upper
        // half (green at top), bad → lower half (red at bottom).
        var emptyHubGo = new GameObject("RightHubPlaceholder");
        emptyHubGo.transform.SetParent(canvasGo.transform, false);
        Texture2D rightHubWedgeTexture = CreateWedgeTexture(400, RightWedgeAngle * 2f, false, true, false);
        RawImage emptyHubImage = emptyHubGo.AddComponent<RawImage>();
        emptyHubImage.texture = rightHubWedgeTexture;
        RectTransform emptyHubRt = emptyHubGo.GetComponent<RectTransform>();
        PositionWedgePanel(emptyHubRt, true, RightWedgeAngle, gearHubWedgeRadius, (float)rightHubWedgeTexture.height / rightHubWedgeTexture.width);

        RectTransform faceContentRt = CreateWedgeContent(emptyHubGo.transform, true, 0f, gearHubWedgeRadius * 0.6f, 115f, 115f);
        var faceGo = new GameObject("FeedbackFace");
        faceGo.transform.SetParent(faceContentRt, false);
        RawImage faceImage = faceGo.AddComponent<RawImage>();
        Texture2D happyFaceTexture = CreateSmileyTexture(200, true);
        Texture2D sadFaceTexture = CreateSmileyTexture(200, false);
        faceImage.texture = happyFaceTexture;
        RectTransform faceRt = faceImage.GetComponent<RectTransform>();
        faceRt.anchorMin = Vector2.zero;
        faceRt.anchorMax = Vector2.one;
        // Inset so the round face stays inside the quarter-sector badge —
        // at full bleed it clipped past the wedge edge toward the corner.
        const float faceInset = 14f;
        faceRt.offsetMin = new Vector2(faceInset, faceInset);
        faceRt.offsetMax = new Vector2(-faceInset, -faceInset);
        faceGo.SetActive(false);

        Image[] feedbackTicks = CreateTickRing(emptyHubGo.transform, true, 10, speedTickRadius, RightWedgeAngle - 4f);
        CreateArcGuide(emptyHubGo.transform, true, speedTickRadius - 30f, RightWedgeAngle - 4f, speedArcGuideColor);
        CreateArcGuide(emptyHubGo.transform, true, speedTickRadius + 30f, RightWedgeAngle - 4f, speedArcGuideColor);

        var feedbackGo = new GameObject("ObjectFeedbackIndicator");
        ObjectFeedbackIndicator feedbackIndicator = feedbackGo.AddComponent<ObjectFeedbackIndicator>();
        SerializedObject feedbackSo = new SerializedObject(feedbackIndicator);
        feedbackSo.FindProperty("faceImage").objectReferenceValue = faceImage;
        feedbackSo.FindProperty("happyFaceTexture").objectReferenceValue = happyFaceTexture;
        feedbackSo.FindProperty("sadFaceTexture").objectReferenceValue = sadFaceTexture;
        SerializedProperty feedbackTicksProp = feedbackSo.FindProperty("ticks");
        feedbackTicksProp.arraySize = feedbackTicks.Length;
        for (int i = 0; i < feedbackTicks.Length; i++)
            feedbackTicksProp.GetArrayElementAtIndex(i).objectReferenceValue = feedbackTicks[i];
        feedbackSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // One checkbox+text row for the post-win recap's stats pages — same
    // visual language CreateChecklistRow already uses for СУТЬ ИГРЫ/ЦЕЛЬ,
    // but center-anchored around a given point instead of page-local-left,
    // since this recap's rows are centered on screen rather than pinned to
    // a page's own left edge. rowGo is the single object to SetActive for
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
        mark.font = GameFont;
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
        line.font = GameFont;
        line.fontSize = fontSize;
        line.fontStyle = FontStyle.Bold;
        line.alignment = TextAnchor.MiddleLeft;
        // The row's own height is a fixed checkSize (56), but callers can
        // pass a bigger fontSize than that — without this, Unity's default
        // Truncate wrap mode clips any line taller than the box down to
        // nothing rendered at all, not just a cosmetically-cut line, which
        // is exactly what once silently blanked a reveal line here.
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
        finishText.font = GameFont;
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

        // Shown for a plain, unanimated 5s hold right before the webcam
        // screen (CaptureRecordPhoto) — same dark statsBackdrop already up
        // from the stats pages just before it, so this doesn't need its
        // own background. A nested Canvas with overrideSorting, not just a
        // plain child of scoreCanvas (sortingOrder 0 by default) — the
        // webcam screen's own PhotoCaptureCanvas sits at 210, so a plain
        // child here rendered behind its gray backdrop instead of over it.
        var newRecordAnnounceGo = new GameObject("NewRecordAnnounceText");
        newRecordAnnounceGo.transform.SetParent(scoreCanvas.transform, false);
        Canvas newRecordAnnounceCanvas = newRecordAnnounceGo.AddComponent<Canvas>();
        newRecordAnnounceCanvas.overrideSorting = true;
        newRecordAnnounceCanvas.sortingOrder = 215; // above PhotoCaptureCanvas's 210
        Text newRecordAnnounceText = newRecordAnnounceGo.AddComponent<Text>();
        newRecordAnnounceText.font = GameFont;
        newRecordAnnounceText.fontSize = 56; // 48, bumped up a bit per feedback
        newRecordAnnounceText.fontStyle = FontStyle.Bold;
        newRecordAnnounceText.alignment = TextAnchor.MiddleCenter;
        newRecordAnnounceText.color = new Color(1f, 0.85f, 0.15f);
        newRecordAnnounceText.lineSpacing = 1.25f;
        newRecordAnnounceText.text = "ВЫ УСТАНОВИЛИ НОВЫЙ РЕКОРД!\nСЕЙЧАС МЫ ВАС СФОТОГРАФИРУЕМ ДЛЯ ИСТОРИИ.\nПРИГОТОВЬТЕСЬ!";
        newRecordAnnounceGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform newRecordAnnounceRt = newRecordAnnounceText.GetComponent<RectTransform>();
        newRecordAnnounceRt.anchorMin = new Vector2(0.5f, 0.5f);
        newRecordAnnounceRt.anchorMax = new Vector2(0.5f, 0.5f);
        newRecordAnnounceRt.pivot = new Vector2(0.5f, 0.5f);
        newRecordAnnounceRt.sizeDelta = new Vector2(1300f, 340f);
        newRecordAnnounceRt.anchoredPosition = Vector2.zero;
        newRecordAnnounceGo.SetActive(false);

        // Shown the instant the goal distance is reached instead of
        // committing straight to the ending — see WinSequence.OfferContinue.
        // A plain prompt + 5-4-3-2-1 countdown; flapping in time pushes the
        // goal further out and this just disappears again.
        var continuePromptGo = new GameObject("ContinuePrompt");
        continuePromptGo.transform.SetParent(scoreCanvas.transform, false);

        var continuePromptTextGo = new GameObject("ContinuePromptText");
        continuePromptTextGo.transform.SetParent(continuePromptGo.transform, false);
        Text continuePromptText = continuePromptTextGo.AddComponent<Text>();
        continuePromptText.font = GameFont;
        continuePromptText.fontSize = 46;
        continuePromptText.fontStyle = FontStyle.Bold;
        continuePromptText.alignment = TextAnchor.MiddleCenter;
        continuePromptText.color = new Color(1f, 0.85f, 0.15f);
        continuePromptText.text = "ДЛЯ ПРОДОЛЖЕНИЯ — СДЕЛАЙТЕ ПРЫЖОК ВВЕРХ";
        continuePromptTextGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform continuePromptTextRt = continuePromptText.GetComponent<RectTransform>();
        continuePromptTextRt.anchorMin = new Vector2(0.5f, 0.5f);
        continuePromptTextRt.anchorMax = new Vector2(0.5f, 0.5f);
        continuePromptTextRt.pivot = new Vector2(0.5f, 0.5f);
        continuePromptTextRt.sizeDelta = new Vector2(1300f, 120f);
        continuePromptTextRt.anchoredPosition = new Vector2(0f, 120f);

        var continueCountdownGo = new GameObject("ContinueCountdownText");
        continueCountdownGo.transform.SetParent(continuePromptGo.transform, false);
        Text continueCountdownText = continueCountdownGo.AddComponent<Text>();
        continueCountdownText.font = GameFont;
        continueCountdownText.fontSize = 120;
        continueCountdownText.fontStyle = FontStyle.Bold;
        continueCountdownText.alignment = TextAnchor.MiddleCenter;
        continueCountdownText.color = Color.white;
        continueCountdownGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform continueCountdownRt = continueCountdownText.GetComponent<RectTransform>();
        continueCountdownRt.anchorMin = new Vector2(0.5f, 0.5f);
        continueCountdownRt.anchorMax = new Vector2(0.5f, 0.5f);
        continueCountdownRt.pivot = new Vector2(0.5f, 0.5f);
        continueCountdownRt.sizeDelta = new Vector2(300f, 160f);
        continueCountdownRt.anchoredPosition = new Vector2(0f, -60f);

        continuePromptGo.SetActive(false);

        var winTextGo = new GameObject("WinText");
        winTextGo.transform.SetParent(scoreCanvas.transform, false);

        Text winText = winTextGo.AddComponent<Text>();
        winText.font = GameFont;
        winText.fontSize = 76; // was 90 for the old shorter "ВЫ ПОБЕДИЛИ!" — smaller so the longer replacement stays on one line
        winText.fontStyle = FontStyle.Bold;
        winText.alignment = TextAnchor.MiddleCenter;
        winText.color = new Color(1f, 0.85f, 0.15f);
        winText.supportRichText = true;
        winText.text = "ВЫ ПРОШЛИ ДО КОНЦА";

        Outline winOutline = winTextGo.AddComponent<Outline>();
        winOutline.effectColor = Color.black;
        winOutline.effectDistance = new Vector2(3f, -3f);

        RectTransform winRt = winTextGo.GetComponent<RectTransform>();
        winRt.anchorMin = new Vector2(0.5f, 0.5f);
        winRt.anchorMax = new Vector2(0.5f, 0.5f);
        winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(1300f, 320f);
        winRt.anchoredPosition = new Vector2(0f, 300f); // was 220 — raised, was getting covered
        winTextGo.SetActive(false);

        var winCongratsGo = new GameObject("WinCongratsText");
        winCongratsGo.transform.SetParent(scoreCanvas.transform, false);
        Text winCongratsText = winCongratsGo.AddComponent<Text>();
        winCongratsText.font = GameFont;
        winCongratsText.fontSize = 110;
        winCongratsText.fontStyle = FontStyle.Bold;
        winCongratsText.alignment = TextAnchor.MiddleCenter;
        winCongratsText.color = new Color(1f, 0.85f, 0.15f);
        winCongratsText.text = "ПОЗДРАВЛЯЕМ!!!";

        Outline winCongratsOutline = winCongratsGo.AddComponent<Outline>();
        winCongratsOutline.effectColor = Color.black;
        winCongratsOutline.effectDistance = new Vector2(3f, -3f);

        RectTransform winCongratsRt = winCongratsGo.GetComponent<RectTransform>();
        winCongratsRt.anchorMin = new Vector2(0.5f, 0.5f);
        winCongratsRt.anchorMax = new Vector2(0.5f, 0.5f);
        winCongratsRt.pivot = new Vector2(0.5f, 0.5f);
        winCongratsRt.sizeDelta = new Vector2(1300f, 180f);
        winCongratsRt.anchoredPosition = new Vector2(0f, -60f);
        winCongratsGo.SetActive(false);

        // Shared backdrop behind the record reveal and the stats pages below
        // it — same dark-tint-plus-outline treatment every other table in
        // the game uses (carousel, end-game leaderboard), so this recap
        // reads as one more table instead of bare floating text. Sized to
        // cover both text boxes (record reveal above, stats pages below)
        // since WinSequence shows them one after another with this staying
        // up for both, not two separate backdrops popping in and out.
        // Затемнение всей сцены на время рекапа. Панели итогов и таблиц
        // закрывают лишь часть экрана и разного размера, поэтому без этого
        // фон за ними то темнел, то нет — в зависимости от того, какая
        // страница сейчас показана. Создаётся до панелей и явно ставится
        // первым ребёнком, чтобы рисоваться под всем остальным UI.
        var recapDimGo = new GameObject("RecapDim");
        recapDimGo.transform.SetParent(scoreCanvas.transform, false);
        Image recapDimImg = recapDimGo.AddComponent<Image>();
        recapDimImg.color = new Color(0f, 0f, 0f, 0.55f);
        recapDimImg.raycastTarget = false;
        RectTransform recapDimRt = recapDimGo.GetComponent<RectTransform>();
        recapDimRt.anchorMin = Vector2.zero;
        recapDimRt.anchorMax = Vector2.one;
        recapDimRt.offsetMin = Vector2.zero;
        recapDimRt.offsetMax = Vector2.zero;
        recapDimGo.transform.SetAsFirstSibling();
        recapDimGo.SetActive(false);

        var statsBackdropGo = new GameObject("StatsBackdrop");
        statsBackdropGo.transform.SetParent(scoreCanvas.transform, false);
        Image statsBackdropImg = statsBackdropGo.AddComponent<Image>();
        // Непрозрачная: при 0.22 сквозь панель просвечивала 3D-сцена и она
        // читалась серой, а не как окно поверх игры.
        statsBackdropImg.color = new Color(0.18f, 0.18f, 0.22f, 1f);
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
        statsTitle.font = GameFont;
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
            // 70, not 45 — CreateWinCheckRow's own checkSize is 56, so 45
            // had adjacent rows overlapping by 11px regardless of their
            // text content. Widened again (was 60) per feedback the rows
            // still read as too tight.
            float y = 20f - i * 70f;
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
        const float iconGridRight = 540f;
        const float iconCellWidth = (iconGridRight - iconGridLeft) / iconCols; // 108
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

        // Count badges for the first few slots — used only when a page's
        // total is too big to draw one icon per unit (see WinSequence.
        // ShowIconStatsPage's grouping threshold): collapses to one icon
        // per TYPE plus a "×N" badge instead. Types are read straight off
        // whatever distinct textures AchievementStats recorded (see
        // PlayerController.EntityIcon), not a fixed shortlist — up to 15
        // good / 13 bad LaneObjects entries plus the mystery fallback, so
        // sized for the worst case (every known good type collected at
        // least once in one run) rather than the old fixed 3-per-page
        // assumption. Corner-badge offset (small, tucked against the
        // icon's own bottom-right) so it can't reach into a neighbouring
        // slot's icon.
        const int iconCountLabelCount = 16;
        var statsIconCountLabels = new Text[iconCountLabelCount];
        for (int i = 0; i < iconCountLabelCount; i++)
        {
            int col = i % iconCols;
            int row = i / iconCols;
            float cx = iconGridLeft + iconCellWidth * (col + 0.5f);
            float cy = iconGridTop - iconCellHeight * (row + 0.5f);

            var labelGo = new GameObject("StatsIconCount" + i);
            labelGo.transform.SetParent(scoreCanvas.transform, false);
            Text label = labelGo.AddComponent<Text>();
            label.font = GameFont;
            label.fontSize = 26;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 0.85f, 0.2f);
            labelGo.AddComponent<Outline>().effectColor = Color.black;
            RectTransform labelRt = label.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0.5f, 0.5f);
            labelRt.anchorMax = new Vector2(0.5f, 0.5f);
            labelRt.pivot = new Vector2(0.5f, 0.5f);
            labelRt.sizeDelta = new Vector2(70f, 34f);
            labelRt.anchoredPosition = new Vector2(cx + 38f, cy - 32f);
            labelGo.SetActive(false);
            statsIconCountLabels[i] = label;
        }

        var statsTotalGo = new GameObject("StatsTotal");
        statsTotalGo.transform.SetParent(scoreCanvas.transform, false);
        Text statsTotalText = statsTotalGo.AddComponent<Text>();
        statsTotalText.font = GameFont;
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
        // the photo capture (WinSequence.CaptureRecordPhoto), so the
        // just-taken photo is visible immediately instead of only later on
        // the start screen.
        var leaderboardRootGo = new GameObject("WinLeaderboardRoot");
        leaderboardRootGo.transform.SetParent(scoreCanvas.transform, false);
        RectTransform leaderboardRootRt = leaderboardRootGo.AddComponent<RectTransform>();
        leaderboardRootRt.anchorMin = new Vector2(0.5f, 0.5f);
        leaderboardRootRt.anchorMax = new Vector2(0.5f, 0.5f);
        leaderboardRootRt.pivot = new Vector2(0.5f, 0.5f);
        // Must match the start-screen carousel's own box (1300 wide, see
        // CreateStartScreen) exactly — CreateTopResultsPage's arrow geometry
        // hardcodes half that width (650f) assuming its parent is that same
        // size, and the two screens are meant to look identical anyway.
        leaderboardRootRt.sizeDelta = new Vector2(1300f, 720f);
        leaderboardRootRt.anchoredPosition = Vector2.zero;

        // Same dark tinted frame the start-screen carousel uses behind its
        // own tables — without it these floated directly over the 3D scene
        // with no visual grouping.
        var leaderboardBgGo = new GameObject("Background");
        leaderboardBgGo.transform.SetParent(leaderboardRootRt, false);
        Image leaderboardBg = leaderboardBgGo.AddComponent<Image>();
        leaderboardBg.color = new Color(0.18f, 0.18f, 0.22f, 1f); // непрозрачная, см. StatsBackdrop выше
        Outline leaderboardBgOutline = leaderboardBgGo.AddComponent<Outline>();
        leaderboardBgOutline.effectColor = Color.gray;
        leaderboardBgOutline.effectDistance = new Vector2(4f, -4f);
        RectTransform leaderboardBgRt = leaderboardBgGo.GetComponent<RectTransform>();
        leaderboardBgRt.anchorMin = Vector2.zero;
        leaderboardBgRt.anchorMax = Vector2.one;
        leaderboardBgRt.offsetMin = Vector2.zero;
        leaderboardBgRt.offsetMax = Vector2.zero;

        // Display order (see TopResultsDisplayOrder), not category-index
        // order — the array's own order is the order ShowLeaderboardTables
        // cycles through, so it directly controls what's shown first.
        var leaderboardPages = new GameObject[4];
        for (int i = 0; i < TopResultsDisplayOrder.Length; i++)
        {
            GameObject page = CreateTopResultsPage(leaderboardRootRt, TopResultsDisplayOrder[i]);
            page.SetActive(false);
            leaderboardPages[i] = page;
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
        so.FindProperty("newRecordAnnounceText").objectReferenceValue = newRecordAnnounceGo;
        so.FindProperty("continuePromptRoot").objectReferenceValue = continuePromptGo;
        so.FindProperty("continueCountdownText").objectReferenceValue = continueCountdownText;
        so.FindProperty("winTextRoot").objectReferenceValue = winRt;
        so.FindProperty("winCongratsTextRoot").objectReferenceValue = winCongratsRt;
        so.FindProperty("statsBackdrop").objectReferenceValue = statsBackdropGo;
        so.FindProperty("recapDim").objectReferenceValue = recapDimGo;

        // Аплодисменты под праздничную часть рекапа (см. WinSequence.SetApplause).
        // Свой AudioSource на объекте WinSequence, а не через SfxManager: тот
        // играет одиночные PlayOneShot, а здесь нужен зацикленный фон, который
        // держится через несколько экранов подряд.
        AudioSource applauseSource = win.gameObject.AddComponent<AudioSource>();
        applauseSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/LadyBug/Audio/lady_bug/WinApplause.wav");
        applauseSource.loop = true;
        applauseSource.playOnAwake = false;
        applauseSource.volume = 1f;
        if (applauseSource.clip == null)
            Debug.LogWarning("WinApplause.wav not found — the recap will be silent.");
        so.FindProperty("applauseSource").objectReferenceValue = applauseSource;
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
        SerializedProperty statsIconCountLabelsProp = so.FindProperty("statsIconCountLabels");
        statsIconCountLabelsProp.arraySize = statsIconCountLabels.Length;
        for (int i = 0; i < statsIconCountLabels.Length; i++)
            statsIconCountLabelsProp.GetArrayElementAtIndex(i).objectReferenceValue = statsIconCountLabels[i];
        so.FindProperty("statsTotalText").objectReferenceValue = statsTotalText;
        so.FindProperty("mysteryIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/Mystery.png");
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
    static void CreateTricksUI()
    {
        var canvasGo = new GameObject("TricksCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // scoreCanvas (CreateScoreUI) carries this corner's own wedge
        // backdrop and is also sortingOrder 0 — with ties, separate overlay
        // canvases draw in hierarchy-registration order, not script creation
        // order, so ТРЮКИ's label could end up drawn (and dimmed) UNDER that
        // backdrop instead of on top of it. Explicit order removes the guess.
        canvas.sortingOrder = 1;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        canvasGo.AddComponent<GraphicRaycaster>();

        const float rightContentWidth = 397f; // 345 * 1.15
        const float tricksAngle = RightWedgeAngle * 1.5f;

        // No background image of its own — ScorePanel (CreateScoreUI, a
        // different canvas but the same screen position) already carries
        // the one baked frame covering this whole quarter, same reasoning
        // as TimerPanel's own comment in CreateScoreUI. This is just an
        // invisible anchor point; its own rotation/content angle are
        // unchanged from before and still land ТРЮКИ at the correct 67.5°.
        var panelGo = new GameObject("TricksPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);

        RectTransform panelRt = panelGo.AddComponent<RectTransform>();
        PositionWedgePanel(panelRt, true, tricksAngle, FanRadius);

        RectTransform tricksContentRt = CreateWedgeContent(panelGo.transform, true, 0f, WedgeContentRadius, rightContentWidth, 207f);
        NudgeContentScreenSpace(tricksContentRt, panelGo.transform, new Vector2(0f, -55f)); // lower still again, per repeated feedback

        CreatePanelLabel(tricksContentRt, "ТРЮКИ"); // back to the shared default color, per feedback all 4 panel labels should match

        var textGo = new GameObject("TricksText");
        textGo.transform.SetParent(tricksContentRt, false);
        Text tricksText = textGo.AddComponent<Text>();
        tricksText.font = GameFont;
        tricksText.fontSize = 97;
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

        // Marks the panel's actual visible content center in a (0,0)-
        // anchored frame — popups live in that same frame, so their
        // anchoredPosition is directly comparable/lerp-able against this
        // point (mirrors ScoreManager's counterAnchor setup — same
        // reference-resolution approximation noted there). panelRt's own
        // pivot is the wedge's apex now, not its content, so this
        // reconstructs the content's real on-screen offset the same way
        // PositionWedgePanel/CreateWedgeContent derive it.
        float tricksRad = tricksAngle * Mathf.Deg2Rad;
        Vector2 tricksContentOffset = new Vector2(-Mathf.Cos(tricksRad), -Mathf.Sin(tricksRad)) * WedgeContentRadius;
        Vector2 tricksContentPos = panelRt.anchoredPosition + tricksContentOffset;
        var counterAnchorGo = new GameObject("TricksCounterAnchor");
        counterAnchorGo.transform.SetParent(canvasGo.transform, false);
        RectTransform counterAnchor = counterAnchorGo.AddComponent<RectTransform>();
        counterAnchor.anchorMin = Vector2.zero;
        counterAnchor.anchorMax = Vector2.zero;
        counterAnchor.pivot = Vector2.zero;
        counterAnchor.anchoredPosition = new Vector2(1920f + tricksContentPos.x, 1080f + tricksContentPos.y);

        var managerGo = new GameObject("TricksManager");
        TricksManager manager = managerGo.AddComponent<TricksManager>();
        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("tricksText").objectReferenceValue = tricksText;
        so.FindProperty("popupParent").objectReferenceValue = canvasGo.GetComponent<RectTransform>();
        so.FindProperty("counterAnchor").objectReferenceValue = counterAnchor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Bottom-left/bottom-right HUD per player, for the gesture simulator:
    // sensor height readout and the interpreted-gesture arrows — sits
    // dim/idle when that player is on keyboard controls (GestureInput
    // disabled). Returns both canvases (left, right) so the start screen
    // can hide them while its own menu is up (they're gameplay HUD, not
    // menu chrome) and reveal them once the game actually begins.
    static (GameObject left, GameObject right) CreateGestureIndicators(GameObject playerRight, GameObject playerLeft)
    {
        // Screen-left = height sensors (player 1); screen-right = joystick
        // (player 2), matching the physical rig on the cabinet.
        GameObject leftCanvas = CreateSensorGesturePanel(playerLeft, new Vector2(0f, 0f));
        GameObject rightCanvas = CreateJoystickGesturePanel(playerRight, new Vector2(1f, 0f));
        return (leftCanvas, rightCanvas);
    }

    static GameObject CreateSensorGesturePanel(GameObject player, Vector2 anchor)
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
        actionText.font = GameFont;
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
        glyphText.font = GameFont;
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
        rawValueText.font = GameFont;
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

        // Raw key-state squares (2x2 block, GestureKeyIndicator) and the
        // legend text above them removed per feedback — too much debug
        // clutter during actual play. Sensor height (above) and the
        // interpreted-gesture arrows stay.
        return canvasGo;
    }

    static GameObject CreateJoystickGesturePanel(GameObject player, Vector2 anchor)
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

        // Raised to clear MenuHelpText, which moved into this same bottom-right
        // corner: that box is 450x190 anchored at (-30, 30), so it reaches up to
        // y=220 (and overflows further upward). At the old crossY the cross sat
        // at y 119..269 and the two overlapped — invisible until the arrows
        // started rendering at all again, which is why it was never noticed.
        const float crossY = 20f + 24f + 120f;
        const float crossSize = 150f;
        const float armOffset = 46f;
        var crossGo = new GameObject("JoystickCross");
        crossGo.transform.SetParent(canvasGo.transform, false);
        RectTransform crossRt = crossGo.AddComponent<RectTransform>();
        crossRt.anchorMin = anchor;
        crossRt.anchorMax = anchor;
        crossRt.pivot = anchor;
        crossRt.sizeDelta = new Vector2(crossSize, crossSize);
        crossRt.anchoredPosition = new Vector2(sign * (20f + crossSize * 0.5f), crossY + crossSize * 0.5f);

        // ComicCAT lacks Unicode arrows — ASCII + built-in font so labels render.
        Font arrowFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Text centerDot = CreateJoystickHudArrow(crossGo.transform, "Center", Vector2.zero, "+", arrowFont);
        centerDot.fontSize = 52;
        Text upArrow = CreateJoystickHudArrow(crossGo.transform, "Up", new Vector2(0f, armOffset), "^", arrowFont);
        Text downArrow = CreateJoystickHudArrow(crossGo.transform, "Down", new Vector2(0f, -armOffset), "v", arrowFont);
        Text leftArrow = CreateJoystickHudArrow(crossGo.transform, "Left", new Vector2(-armOffset, 0f), "<", arrowFont);
        Text rightArrow = CreateJoystickHudArrow(crossGo.transform, "Right", new Vector2(armOffset, 0f), ">", arrowFont);

        var indicatorGo = new GameObject(player.name + "JoystickIndicator");
        JoystickIndicator indicator = indicatorGo.AddComponent<JoystickIndicator>();
        SerializedObject indicatorSo = new SerializedObject(indicator);
        indicatorSo.FindProperty("joystickInput").objectReferenceValue = player.GetComponent<JoystickInput>();
        indicatorSo.FindProperty("upArrow").objectReferenceValue = upArrow;
        indicatorSo.FindProperty("downArrow").objectReferenceValue = downArrow;
        indicatorSo.FindProperty("leftArrow").objectReferenceValue = leftArrow;
        indicatorSo.FindProperty("rightArrow").objectReferenceValue = rightArrow;
        indicatorSo.FindProperty("centerDot").objectReferenceValue = centerDot;
        indicatorSo.ApplyModifiedPropertiesWithoutUndo();

        return canvasGo;
    }

    static Text CreateJoystickHudArrow(Transform parent, string name, Vector2 anchoredPos, string glyph, Font font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = font != null ? font : GameFont;
        text.fontSize = 64;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        // 64pt in a 56x56 box. Unity's default Truncate wrap mode does not
        // crop a too-tall line, it discards it — the arrows were built,
        // active, correctly coloured and positioned, and drew absolutely
        // nothing. Exactly the trap already documented on CreateChecklistRow
        // ("once silently blanked a reveal line here"), just never applied
        // here. JoystickIndicator.StyleArrow re-asserts the same font size
        // every session, so it sets these flags too.
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = new Color(0.55f, 0.55f, 0.55f);
        text.text = glyph;
        go.AddComponent<Outline>().effectColor = Color.black;
        RectTransform rt = text.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(56f, 56f);
        rt.anchoredPosition = anchoredPos;
        return text;
    }

    // Used to be 6 color variants (random pick from badDuckPrefabs), tinted
    // at runtime via material.color — now a single real-construction-barrier
    // look (red/white hazard stripes baked into the texture itself), so no
    // tinting and just the one prefab.
    static GameObject CreateArchPrefab()
    {
        const string name = "Arch";
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/SmallArch.png");
        if (tex == null)
        {
            Debug.LogWarning("Texture not found: Assets/LadyBug/Sprites/SmallArch.png");
            return null;
        }

        var root = new GameObject(name);
        root.AddComponent<MovingEntity>();
        root.AddComponent<DuckUnderObstacle>();
        // Duck/jump success is decided purely from DuckUnderObstacle above
        // (see PlayerController.OnTriggerEnter — that check runs first and
        // returns before ever reaching this), so adding ScoreValue here
        // only affects the failure case (walked into it standing up) —
        // without it, that case fell through to OnTriggerEnter's own
        // catch-all path, which registers the hit but never calls
        // ScoreManager.SpawnPopup, so no "-1" ever flew to the score panel
        // for this specific obstacle.
        root.AddComponent<ScoreValue>().value = -1;

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
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        Material material = new Material(shader) { mainTexture = tex };

        System.IO.Directory.CreateDirectory("Assets/LadyBug/Materials/lady_bug");
        string materialPath = "Assets/LadyBug/Materials/lady_bug/" + name + ".mat";
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

        string savePath = "Assets/LadyBug/Prefabs/lady_bug/" + name + ".prefab";
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
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/BigArchSign.png");
        if (tex == null)
        {
            Debug.LogWarning("Texture not found: Assets/LadyBug/Sprites/BigArchSign.png");
            return null;
        }

        var root = new GameObject("BigArch");
        root.AddComponent<MovingEntity>();
        root.AddComponent<TallArchObstacle>();
        BigArchLayout archLayout = root.AddComponent<BigArchLayout>();
        // Same reasoning as CreateArchPrefab's own ScoreValue — the walk/
        // duck-under success path returns out of OnTriggerEnter before
        // ever reaching this, so it only affects the failure case (jumped
        // into it), which otherwise fell through to the no-popup catch-all.
        root.AddComponent<ScoreValue>().value = -1;

        float aspect = (float)tex.width / tex.height;
        // Full road span + shoulder margin — see RoadLayout.BigArchSpanWidth.
        float spanWidth = RoadLayout.BigArchSpanWidth(LaneCount);
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
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        Material material = new Material(shader) { mainTexture = tex };

        System.IO.Directory.CreateDirectory("Assets/LadyBug/Materials/lady_bug");
        string materialPath = "Assets/LadyBug/Materials/lady_bug/BigArchSign.mat";
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

        SerializedObject layoutSo = new SerializedObject(archLayout);
        layoutSo.FindProperty("referenceSpanWidth").floatValue = spanWidth;
        layoutSo.ApplyModifiedPropertiesWithoutUndo();

        string savePath = "Assets/LadyBug/Prefabs/lady_bug/BigArch.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void CreateBigArchSpawner()
    {
        System.IO.Directory.CreateDirectory("Assets/LadyBug/Prefabs/lady_bug");

        GameObject prefab = CreateBigArchPrefab();

        var spawnerGo = new GameObject("BigArchSpawner");
        BigArchSpawner spawner = spawnerGo.AddComponent<BigArchSpawner>();

        SerializedObject so = new SerializedObject(spawner);
        so.FindProperty("prefab").objectReferenceValue = prefab;
        so.FindProperty("spawnZ").floatValue = RoadCenterZ + RoadLength / 2f - 5f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CreateStartScreen(GameObject playerRight, GameObject playerLeft, GameObject gestureCanvasLeft, GameObject gestureCanvasRight)
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

        // Energetic tracks while the menu/instructions carousel is up —
        // MenuMusicRotator shuffles through them at random (no repeat back-
        // to-back). Stops when the game starts (BeginGame). Also why
        // SfxManager mutes pickup/hit one-shots until SpeedController says
        // the game is running — this music, not silence, is the intended
        // backdrop for the start screen.
        var musicGo = new GameObject("StartScreenMusic");
        musicGo.transform.SetParent(canvasGo.transform, false);
        AudioSource musicSource = musicGo.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.volume = 0.5f;
        musicSource.loop = false;

        AudioClip[] menuMusicClips =
        {
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/LadyBug/Audio/lady_bug/StartScreenMusic.mp3"),
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/LadyBug/Audio/lady_bug/MenuMusic_PopTrack03.mp3"),
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/LadyBug/Audio/lady_bug/MenuMusic_BanjoMan.mp3"),
        };
        MenuMusicRotator menuMusic = musicGo.AddComponent<MenuMusicRotator>();
        SerializedObject menuMusicSo = new SerializedObject(menuMusic);
        menuMusicSo.FindProperty("source").objectReferenceValue = musicSource;
        SerializedProperty clipsProp = menuMusicSo.FindProperty("clips");
        clipsProp.arraySize = menuMusicClips.Length;
        for (int i = 0; i < menuMusicClips.Length; i++)
            clipsProp.GetArrayElementAtIndex(i).objectReferenceValue = menuMusicClips[i];
        menuMusicSo.ApplyModifiedPropertiesWithoutUndo();

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
        title.font = GameFont;
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
        // back to a mid-word break. Narrowed back down slightly from a
        // since-tried 1400 — its right edge (+700) reached into the HUD
        // panel stack's own left edge (+680, see CreateScoreUI's panels,
        // all 260 wide with a 20px margin from the screen edge). See
        // CreateTopResultsPage's matching "650f" (this box's new
        // half-width) in its arrow-geometry math.
        carouselRt.sizeDelta = new Vector2(1300f, 720f);
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
        // "пробежать N км" needs to track WinSequence's actual win distance
        // (temporarily lowered for debug/test runs) instead of a hardcoded
        // number that'd lie while testing.
        var goalPage = CreateChecklistPage(carouselRt, "ЦЕЛЬ",
            "пробежать 100 км", // GoalDistanceLabel overwrites this with the real live distance
            "за минимально возможное время",
            "дополнительно набирать очки",
            "дорога разгоняется сама");
        GoalDistanceLabel goalDistanceLabel = goalPage.rowTexts[0].gameObject.AddComponent<GoalDistanceLabel>();
        SerializedObject goalDistanceSo = new SerializedObject(goalDistanceLabel);
        goalDistanceSo.FindProperty("label").objectReferenceValue = goalPage.rowTexts[0];
        goalDistanceSo.ApplyModifiedPropertiesWithoutUndo();

        // Same idea as goalPage above — built with placeholder copy, then
        // wired so the runtime can correct it (keyboard vs real controller).
        var menuSelectionPage = CreateMenuSelectionPage(carouselRt);

        var carouselPages = new System.Collections.Generic.List<GameObject>
        {
            // СУТЬ ИГРЫ first — page[0] is what's actually visible the
            // instant the start screen appears (see the SetActive loop
            // below); per feedback this should lead (what the game even
            // is), with ЦЕЛЬ (the actual win condition) right after it —
            // was the other way around. Leaderboards still come later,
            // after both of these.
            CreateChecklistPage(carouselRt, "СУТЬ ИГРЫ",
                "собирать хорошие объекты",
                "не сталкиваться с препятствиями",
                "выполнять трюки вдвоём").page,

            goalPage.page,

            menuSelectionPage.page,

            CreateObjectGridPage(carouselRt, "ХОРОШИЕ ОБЪЕКТЫ", new Color(0.4f, 1f, 0.5f), GoodObjectNames),
            CreateObjectGridPage(carouselRt, "ПРЕПЯТСТВИЯ", new Color(1f, 0.4f, 0.3f), BadObjectNames),

            // УПРАВЛЕНИЕ (which hardware reads which player) — the actual
            // gesture-move pages that used to lead into it here moved to
            // the ТРЕНИРОВКА carousel instead (see trickCarouselPages
            // below), per feedback that this upfront screen should get
            // straight to ЦЕЛЬ/СУТЬ/objects/controls, not repeat the full
            // move set for players who just want to start playing.
            CreateControlsPage(carouselRt),

            // Order matches TopResultsDisplayOrder (Очки/Время/Скорость/Трюки).
            // Moved to the end of this list — feedback only re-ordered the
            // pages above it, leaderboards are still fine to show, just
            // after the actual instructions rather than before them.
            CreateTopResultsPage(carouselRt, TopResultsDisplayOrder[0]),
            CreateTopResultsPage(carouselRt, TopResultsDisplayOrder[1]),
            CreateTopResultsPage(carouselRt, TopResultsDisplayOrder[2]),
            CreateTopResultsPage(carouselRt, TopResultsDisplayOrder[3]),
        };

        // Trick-instruction pages used to be part of this same upfront
        // carousel (everyone saw them, whether they'd chosen ТРЕНИРОВКА or
        // not) — moved into their own separate carousel/canvas below,
        // shown only after that specific choice, per feedback.
        var trickCarouselGo = new GameObject("TrickCarouselCanvas");
        Canvas trickCarouselCanvas = trickCarouselGo.AddComponent<Canvas>();
        trickCarouselCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        trickCarouselCanvas.sortingOrder = 100; // same layer as StartScreenCanvas/TrainingCanvas — never shown together
        CanvasScaler trickCarouselScaler = trickCarouselGo.AddComponent<CanvasScaler>();
        trickCarouselScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        trickCarouselScaler.referenceResolution = new Vector2(1920f, 1080f);
        trickCarouselScaler.matchWidthOrHeight = 1f;
        trickCarouselGo.AddComponent<GraphicRaycaster>();

        var trickBackdropGo = new GameObject("Backdrop");
        trickBackdropGo.transform.SetParent(trickCarouselGo.transform, false);
        Image trickBackdrop = trickBackdropGo.AddComponent<Image>();
        trickBackdrop.color = new Color(0.05f, 0.05f, 0.08f, 1f);
        RectTransform trickBackdropRt = trickBackdropGo.GetComponent<RectTransform>();
        trickBackdropRt.anchorMin = Vector2.zero;
        trickBackdropRt.anchorMax = Vector2.one;
        trickBackdropRt.offsetMin = Vector2.zero;
        trickBackdropRt.offsetMax = Vector2.zero;

        // Same footprint/style as the main menu's own carousel (carouselRt/
        // carouselBgGo above) — trick pages were built to fit that box, so
        // reusing its exact size keeps them looking identical here.
        var trickCarouselContentGo = new GameObject("TrickCarousel");
        trickCarouselContentGo.transform.SetParent(trickCarouselGo.transform, false);
        RectTransform trickCarouselRt = trickCarouselContentGo.AddComponent<RectTransform>();
        trickCarouselRt.anchorMin = new Vector2(0.5f, 0.5f);
        trickCarouselRt.anchorMax = new Vector2(0.5f, 0.5f);
        trickCarouselRt.pivot = new Vector2(0.5f, 0.5f);
        trickCarouselRt.sizeDelta = new Vector2(1300f, 720f);
        trickCarouselRt.anchoredPosition = new Vector2(0f, 90f);

        var trickCarouselBgGo = new GameObject("TrickCarouselBackground");
        trickCarouselBgGo.transform.SetParent(trickCarouselRt, false);
        Image trickCarouselBg = trickCarouselBgGo.AddComponent<Image>();
        trickCarouselBg.color = new Color(0f, 0f, 0f, 0.22f);
        Outline trickCarouselBgOutline = trickCarouselBgGo.AddComponent<Outline>();
        trickCarouselBgOutline.effectColor = Color.gray;
        trickCarouselBgOutline.effectDistance = new Vector2(4f, -4f);
        RectTransform trickCarouselBgRt = trickCarouselBgGo.GetComponent<RectTransform>();
        trickCarouselBgRt.anchorMin = Vector2.zero;
        trickCarouselBgRt.anchorMax = Vector2.one;
        trickCarouselBgRt.offsetMin = Vector2.zero;
        trickCarouselBgRt.offsetMax = Vector2.zero;

        // Static reminder that holding down (same gesture/key the actual
        // live screen uses to exit — see UpdateTrickCarousel) also backs out
        // of this carousel — the hold itself already worked silently on
        // every page (StartScreenController.UpdateTrickCarousel), but with
        // no on-screen hint anywhere a player had no way to discover it.
        // A sibling of trickCarouselContentGo (not any one page) so it's
        // one shared element visible on every page, not duplicated per page.
        var trickExitHintGo = new GameObject("TrickCarouselExitHint");
        trickExitHintGo.transform.SetParent(trickCarouselGo.transform, false);
        Text trickExitHint = trickExitHintGo.AddComponent<Text>();
        trickExitHint.font = GameFont;
        trickExitHint.fontSize = 26;
        trickExitHint.fontStyle = FontStyle.Bold;
        trickExitHint.alignment = TextAnchor.MiddleCenter;
        trickExitHint.color = new Color(0.85f, 0.85f, 0.85f);
        trickExitHint.text = "ВЫХОД — ДЕРЖАТЬ ВНИЗ 8 СЕК";
        trickExitHintGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform trickExitHintRt = trickExitHint.GetComponent<RectTransform>();
        trickExitHintRt.anchorMin = new Vector2(0.5f, 0f);
        trickExitHintRt.anchorMax = new Vector2(0.5f, 0f);
        trickExitHintRt.pivot = new Vector2(0.5f, 0f);
        trickExitHintRt.sizeDelta = new Vector2(900f, 50f);
        trickExitHintRt.anchoredPosition = new Vector2(0f, 24f);

        // Live countdown while actually holding down, same 2-phase feel as
        // the live practice screen's own trainingCountdownGo (silent first,
        // then this appears) — a sibling of trickCarouselContentGo, not any
        // one page, so it's shared across all of them the same way the
        // static hint above is.
        var trickExitCountdownGo = new GameObject("TrickCarouselExitCountdown");
        trickExitCountdownGo.transform.SetParent(trickCarouselGo.transform, false);
        Text trickExitCountdownText = trickExitCountdownGo.AddComponent<Text>();
        trickExitCountdownText.font = GameFont;
        trickExitCountdownText.fontSize = 90;
        trickExitCountdownText.fontStyle = FontStyle.Bold;
        trickExitCountdownText.alignment = TextAnchor.MiddleCenter;
        trickExitCountdownText.color = new Color(1f, 0.85f, 0.15f);
        trickExitCountdownGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform trickExitCountdownRt = trickExitCountdownText.GetComponent<RectTransform>();
        trickExitCountdownRt.anchorMin = new Vector2(0.5f, 0f);
        trickExitCountdownRt.anchorMax = new Vector2(0.5f, 0f);
        trickExitCountdownRt.pivot = new Vector2(0.5f, 0f);
        trickExitCountdownRt.sizeDelta = new Vector2(240f, 110f);
        trickExitCountdownRt.anchoredPosition = new Vector2(0f, 90f);
        trickExitCountdownGo.SetActive(false);

        // Each gesture page's "ВАШИ ДЕЙСТВИЯ" column gets its own pair of
        // flat live-reaction bugs (see CreateGestureDiagramPage) — the
        // player-2 one collected here so StartScreenController can toggle
        // all of them together based on the 1-player/2-player choice, same
        // as it already does for the real playerLeft.
        var trainingPreviewLeftBugs = new System.Collections.Generic.List<GameObject>();

        var squatPage = CreateGestureDiagramPage(trickCarouselRt, "ПРИСЕСТЬ", false, false, false, playerRight, playerLeft);
        trainingPreviewLeftBugs.Add(squatPage.leftBug);

        // Split from the old single shared "В СТОРОНУ" page into its
        // own left/right page each — showing both directions on one
        // page read as "which one am I even looking at" mid-gesture.
        var leanLeftPage = CreateGestureDiagramPage(trickCarouselRt, "ВЛЕВО", false, true, false, playerRight, playerLeft);
        trainingPreviewLeftBugs.Add(leanLeftPage.leftBug);
        var leanRightPage = CreateGestureDiagramPage(trickCarouselRt, "ВПРАВО", true, false, false, playerRight, playerLeft);
        trainingPreviewLeftBugs.Add(leanRightPage.leftBug);

        var flapPage = CreateGestureDiagramPage(trickCarouselRt, "МАХАТЬ КРЫЛЬЯМИ", true, true, true, playerRight, playerLeft);
        trainingPreviewLeftBugs.Add(flapPage.leftBug);

        // АРКА/КОЛЬЦО get the same ОБРАЗЕЦ/ВАШИ ДЕЙСТВИЯ split as the
        // gesture pages above — the other 5 trick pages (all built via the
        // shared CreateTrickDiagramPage) still need their own routes/arcs
        // rescaled to fit half a page first (some reach ±600+ natively) and
        // are deferred for now.
        var archPage = CreateArchTrickPage(trickCarouselRt, playerRight, playerLeft);
        trainingPreviewLeftBugs.Add(archPage.leftBug);
        var ringPage = CreateRingTrickPage(trickCarouselRt, playerRight, playerLeft);
        trainingPreviewLeftBugs.Add(ringPage.leftBug);

        var leapfrogPage = CreateLeapfrogTrickPage(trickCarouselRt, playerRight, playerLeft);
        trainingPreviewLeftBugs.Add(leapfrogPage.leftBug);
        var syncPage = CreateSyncTrickPage(trickCarouselRt, playerRight, playerLeft);
        trainingPreviewLeftBugs.Add(syncPage.leftBug);
        var hoverPage = CreateHoverTrickPage(trickCarouselRt, playerRight, playerLeft);
        trainingPreviewLeftBugs.Add(hoverPage.leftBug);
        var bigRingPage = CreateBigRingTrickPage(trickCarouselRt, playerRight, playerLeft);
        trainingPreviewLeftBugs.Add(bigRingPage.leftBug);
        var infinityPage = CreateInfinityTrickPage(trickCarouselRt, playerRight, playerLeft);
        trainingPreviewLeftBugs.Add(infinityPage.leftBug);

        var trickCarouselPages = new System.Collections.Generic.List<GameObject>
        {
            // Gesture-move pages first (moved here from the main upfront
            // carousel, per feedback — someone who picked ТРЕНИРОВКА
            // specifically wants this level of detail, a player heading
            // straight to СТАРТ doesn't need it repeated in their way),
            // trick pages after — knowing the moves before the tricks that
            // combine them reads better than the other order.
            squatPage.page,
            leanLeftPage.page,
            leanRightPage.page,
            flapPage.page,

            archPage.page,
            ringPage.page,
            leapfrogPage.page,
            syncPage.page,
            hoverPage.page,
            bigRingPage.page,
            infinityPage.page,
        };
        for (int i = 1; i < trickCarouselPages.Count; i++)
            trickCarouselPages[i].SetActive(false);

        trickCarouselGo.SetActive(false);

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
        rowRt.anchoredPosition = new Vector2(0f, -315f);

        GameObject option1 = CreateMenuOption(rowGo.transform, "Option1", new Vector2(-180f, 0f), "[X] 1 ИГРОК", 280f, 32, 60f);
        GameObject option2 = CreateMenuOption(rowGo.transform, "Option2", new Vector2(180f, 0f), "[ ] 2 ИГРОКА", 280f, 32, 60f);

        // Lane-count row — seven options (1–7); option 1 is disabled when
        // two players are selected (StartScreenController greys it out).
        var lanesRowGo = new GameObject("LanesRow");
        lanesRowGo.transform.SetParent(canvasGo.transform, false);
        Image lanesRowBg = lanesRowGo.AddComponent<Image>();
        lanesRowBg.color = new Color(1f, 1f, 1f, 0.05f);
        lanesRowBg.sprite = CreateSoftRectSprite(128, 0.15f);
        lanesRowBg.type = Image.Type.Sliced;
        Outline lanesRowOutline = lanesRowGo.AddComponent<Outline>();
        lanesRowOutline.effectDistance = new Vector2(4f, -4f);
        RectTransform lanesRowRt = lanesRowGo.GetComponent<RectTransform>();
        lanesRowRt.anchorMin = new Vector2(0.5f, 0.5f);
        lanesRowRt.anchorMax = new Vector2(0.5f, 0.5f);
        lanesRowRt.pivot = new Vector2(0.5f, 0.5f);
        lanesRowRt.sizeDelta = new Vector2(900f, 80f);
        lanesRowRt.anchoredPosition = new Vector2(0f, -390f);

        var laneOptionBgs = new Image[RoadLayout.MaxLaneCount];
        var laneOptionTexts = new Text[RoadLayout.MaxLaneCount];
        const float laneSpacing = 110f;
        float laneStartX = -(RoadLayout.MaxLaneCount - 1) * laneSpacing / 2f;
        for (int i = 0; i < RoadLayout.MaxLaneCount; i++)
        {
            float x = laneStartX + i * laneSpacing;
            GameObject laneOpt = CreateMenuOption(lanesRowGo.transform, "Lane" + (i + 1), new Vector2(x, 0f), "[ ] " + (i + 1), 90f, 26, 60f);
            laneOptionBgs[i] = laneOpt.GetComponent<Image>();
            laneOptionTexts[i] = laneOpt.GetComponentInChildren<Text>();
        }

        // Auto-detected controller status — shown on the right when no board
        // is connected; StartScreenController hides it when hardware is found.
        var controllerStatusGo = new GameObject("ControllerStatusText");
        controllerStatusGo.transform.SetParent(canvasGo.transform, false);
        Text controllerStatus = controllerStatusGo.AddComponent<Text>();
        controllerStatus.font = GameFont;
        controllerStatus.fontSize = 20;
        controllerStatus.fontStyle = FontStyle.Bold;
        // Bottom-LEFT — swapped with MenuHelpText, which now takes the right
        // corner. Runtime layout must agree: StartScreenController's own
        // ApplyControllerStatusLayout rewrites this whole rect every time it
        // refreshes, so changing it only here would snap straight back.
        controllerStatus.alignment = TextAnchor.LowerLeft;
        controllerStatus.horizontalOverflow = HorizontalWrapMode.Overflow;
        controllerStatus.color = new Color(0.9f, 0.9f, 0.9f);
        controllerStatus.text = "КОНТРОЛЛЕР...";
        controllerStatusGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform controllerStatusRt = controllerStatus.GetComponent<RectTransform>();
        controllerStatusRt.anchorMin = new Vector2(0f, 0f);
        controllerStatusRt.anchorMax = new Vector2(0f, 0f);
        controllerStatusRt.pivot = new Vector2(0f, 0f);
        controllerStatusRt.sizeDelta = new Vector2(680f, 80f);
        controllerStatusRt.anchoredPosition = new Vector2(30f, 30f);

        // Large 5-4-3-2-1 while holding down to confirm СТАРТ/ТРЕНИРОВКА.
        var menuConfirmCountdownGo = new GameObject("MenuConfirmCountdown");
        menuConfirmCountdownGo.transform.SetParent(canvasGo.transform, false);
        Text menuConfirmCountdown = menuConfirmCountdownGo.AddComponent<Text>();
        menuConfirmCountdown.font = GameFont;
        menuConfirmCountdown.fontSize = 120;
        menuConfirmCountdown.fontStyle = FontStyle.Bold;
        menuConfirmCountdown.alignment = TextAnchor.MiddleCenter;
        menuConfirmCountdown.color = new Color(1f, 0.85f, 0.15f);
        menuConfirmCountdownGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform menuConfirmCountdownRt = menuConfirmCountdown.GetComponent<RectTransform>();
        menuConfirmCountdownRt.anchorMin = new Vector2(0.5f, 0.5f);
        menuConfirmCountdownRt.anchorMax = new Vector2(0.5f, 0.5f);
        menuConfirmCountdownRt.pivot = new Vector2(0.5f, 0.5f);
        menuConfirmCountdownRt.sizeDelta = new Vector2(150f, 150f);
        menuConfirmCountdownRt.anchoredPosition = Vector2.zero;
        menuConfirmCountdownGo.SetActive(false);

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
        // Widened again (was 420, one centered button) to fit ТРЕНИРОВКА
        // beside СТАРТ — same two-box-side-by-side layout as the player-
        // count row above.
        startRowRt.sizeDelta = new Vector2(700f, 90f);
        startRowRt.anchoredPosition = new Vector2(0f, -465f);

        GameObject startBtn = CreateMenuOption(startRowGo.transform, "StartButton", new Vector2(-180f, 0f), "[X] СТАРТ", 300f, 32, 60f);
        // Leads to an empty placeholder screen for now (TrainingCanvas,
        // below) — real training-mode content comes later.
        GameObject trainingBtn = CreateMenuOption(startRowGo.transform, "TrainingButton", new Vector2(180f, 0f), "[ ] ТРЕНИРОВКА", 300f, 32, 60f);

        // Shown only if Start is pressed while "Датчики расстояния" is selected.
        var notImplementedGo = new GameObject("NotImplemented");
        notImplementedGo.transform.SetParent(canvasGo.transform, false);
        Text notImplemented = notImplementedGo.AddComponent<Text>();
        notImplemented.font = GameFont;
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
        notImplementedRt.anchoredPosition = new Vector2(0f, -430f);
        notImplementedGo.SetActive(false);

        // Placeholder screen for the new ТРЕНИРОВКА button — genuinely
        // empty for now (real training-mode content comes later), just a
        // title and a reminder of how to get back out, since there's
        // nothing else on it to interact with. Its own canvas (not a page
        // inside StartScreenCanvas) so it can fully replace the menu
        // instead of layering over it.
        var trainingCanvasGo = new GameObject("TrainingCanvas");
        Canvas trainingCanvas = trainingCanvasGo.AddComponent<Canvas>();
        trainingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        trainingCanvas.sortingOrder = 100; // same layer as StartScreenCanvas — the two never show at once
        CanvasScaler trainingScaler = trainingCanvasGo.AddComponent<CanvasScaler>();
        trainingScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        trainingScaler.referenceResolution = new Vector2(1920f, 1080f);
        trainingScaler.matchWidthOrHeight = 1f;
        trainingCanvasGo.AddComponent<GraphicRaycaster>();

        var trainingBackdropGo = new GameObject("Backdrop");
        trainingBackdropGo.transform.SetParent(trainingCanvasGo.transform, false);
        Image trainingBackdrop = trainingBackdropGo.AddComponent<Image>();
        trainingBackdrop.color = new Color(0.05f, 0.05f, 0.08f, 1f);
        RectTransform trainingBackdropRt = trainingBackdropGo.GetComponent<RectTransform>();
        trainingBackdropRt.anchorMin = Vector2.zero;
        trainingBackdropRt.anchorMax = Vector2.one;
        trainingBackdropRt.offsetMin = Vector2.zero;
        trainingBackdropRt.offsetMax = Vector2.zero;

        var trainingTitleGo = new GameObject("TrainingTitle");
        trainingTitleGo.transform.SetParent(trainingCanvasGo.transform, false);
        Text trainingTitle = trainingTitleGo.AddComponent<Text>();
        trainingTitle.font = GameFont;
        trainingTitle.fontSize = 64;
        trainingTitle.fontStyle = FontStyle.Bold;
        trainingTitle.alignment = TextAnchor.MiddleCenter;
        trainingTitle.color = new Color(1f, 0.85f, 0.2f);
        trainingTitle.text = "ТРЕНИРОВКА";
        trainingTitleGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform trainingTitleRt = trainingTitle.GetComponent<RectTransform>();
        trainingTitleRt.anchorMin = new Vector2(0.5f, 0.5f);
        trainingTitleRt.anchorMax = new Vector2(0.5f, 0.5f);
        trainingTitleRt.pivot = new Vector2(0.5f, 0.5f);
        trainingTitleRt.sizeDelta = new Vector2(1200f, 100f);
        trainingTitleRt.anchoredPosition = new Vector2(0f, 60f);

        var trainingExitGo = new GameObject("TrainingExitHint");
        trainingExitGo.transform.SetParent(trainingCanvasGo.transform, false);
        Text trainingExit = trainingExitGo.AddComponent<Text>();
        trainingExit.font = GameFont;
        trainingExit.fontSize = 30;
        trainingExit.fontStyle = FontStyle.Bold;
        trainingExit.alignment = TextAnchor.MiddleCenter;
        trainingExit.color = new Color(0.85f, 0.85f, 0.85f);
        trainingExit.text = "ВЫХОД — ДЕРЖАТЬ ВНИЗ 8 СЕК";
        trainingExitGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform trainingExitRt = trainingExit.GetComponent<RectTransform>();
        trainingExitRt.anchorMin = new Vector2(0.5f, 0.5f);
        trainingExitRt.anchorMax = new Vector2(0.5f, 0.5f);
        trainingExitRt.pivot = new Vector2(0.5f, 0.5f);
        trainingExitRt.sizeDelta = new Vector2(900f, 60f);
        trainingExitRt.anchoredPosition = new Vector2(0f, -60f);

        // Live countdown while actually holding down — only visible during
        // the hold itself (see StartScreenController.UpdateTrainingScreen),
        // the static hint above stays up the whole time regardless.
        var trainingCountdownGo = new GameObject("TrainingExitCountdown");
        trainingCountdownGo.transform.SetParent(trainingCanvasGo.transform, false);
        Text trainingCountdownText = trainingCountdownGo.AddComponent<Text>();
        trainingCountdownText.font = GameFont;
        trainingCountdownText.fontSize = 90;
        trainingCountdownText.fontStyle = FontStyle.Bold;
        trainingCountdownText.alignment = TextAnchor.MiddleCenter;
        trainingCountdownText.color = new Color(1f, 0.85f, 0.15f);
        trainingCountdownGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform trainingCountdownRt = trainingCountdownText.GetComponent<RectTransform>();
        trainingCountdownRt.anchorMin = new Vector2(0.5f, 0.5f);
        trainingCountdownRt.anchorMax = new Vector2(0.5f, 0.5f);
        trainingCountdownRt.pivot = new Vector2(0.5f, 0.5f);
        trainingCountdownRt.sizeDelta = new Vector2(240f, 130f);
        trainingCountdownRt.anchoredPosition = new Vector2(0f, -160f);
        trainingCountdownGo.SetActive(false);

        trainingCanvasGo.SetActive(false);

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
        menuHelp.font = GameFont;
        menuHelp.fontSize = 20;
        menuHelp.fontStyle = FontStyle.Bold;
        // Bottom-RIGHT — swapped with ControllerStatusText above.
        menuHelp.alignment = TextAnchor.LowerRight;
        menuHelp.horizontalOverflow = HorizontalWrapMode.Wrap;
        menuHelp.verticalOverflow = VerticalWrapMode.Overflow;
        menuHelp.color = new Color(0.9f, 0.9f, 0.9f);
        // Split across more, shorter explicit lines rather than 2 long ones
        // relying on auto-wrap — the "(клавиши, имитатор жестов или датчики
        // — любое)" line was dropped entirely, redundant with the
        // controller-selection row directly above this text.
        menuHelp.text = "ВЫБОР:\n"
            + "WASD · IJKL\n"
            + "\n"
            + "НАЧАЛО:\n"
            + "ВЫБРАТЬ СТАРТ ИЛИ ТРЕНИРОВКА\n"
            + "И ДЕРЖАТЬ ВНИЗ 5 СЕК";
        menuHelpGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform menuHelpRt = menuHelp.GetComponent<RectTransform>();
        menuHelpRt.anchorMin = new Vector2(1f, 0f);
        menuHelpRt.anchorMax = new Vector2(1f, 0f);
        menuHelpRt.pivot = new Vector2(1f, 0f);
        menuHelpRt.sizeDelta = new Vector2(450f, 190f); // verticalOverflow=Overflow handles the rest if 6 short lines run a touch past this
        menuHelpRt.anchoredPosition = new Vector2(-30f, 30f);

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
        SerializedProperty laneBgsProp = so.FindProperty("laneOptionBgs");
        laneBgsProp.arraySize = laneOptionBgs.Length;
        for (int i = 0; i < laneOptionBgs.Length; i++)
            laneBgsProp.GetArrayElementAtIndex(i).objectReferenceValue = laneOptionBgs[i];
        SerializedProperty laneTextsProp = so.FindProperty("laneOptionTexts");
        laneTextsProp.arraySize = laneOptionTexts.Length;
        for (int i = 0; i < laneOptionTexts.Length; i++)
            laneTextsProp.GetArrayElementAtIndex(i).objectReferenceValue = laneOptionTexts[i];
        so.FindProperty("lanesRowOutline").objectReferenceValue = lanesRowOutline;
        so.FindProperty("lanesRowBg").objectReferenceValue = lanesRowBg;
        so.FindProperty("controllerStatusText").objectReferenceValue = controllerStatus;
        so.FindProperty("menuHelpText").objectReferenceValue = menuHelp;
        so.FindProperty("menuSelectionPlayer1Row").objectReferenceValue = menuSelectionPage.player1Row;
        so.FindProperty("menuSelectionPlayer2Row").objectReferenceValue = menuSelectionPage.player2Row;
        so.FindProperty("menuConfirmCountdownText").objectReferenceValue = menuConfirmCountdown;
        so.FindProperty("notImplementedText").objectReferenceValue = notImplemented;
        so.FindProperty("startBg").objectReferenceValue = startBtn.GetComponent<Image>();
        so.FindProperty("startText").objectReferenceValue = startBtn.GetComponentInChildren<Text>();
        so.FindProperty("trainingBg").objectReferenceValue = trainingBtn.GetComponent<Image>();
        so.FindProperty("trainingText").objectReferenceValue = trainingBtn.GetComponentInChildren<Text>();
        so.FindProperty("trainingCanvasRoot").objectReferenceValue = trainingCanvasGo;
        so.FindProperty("trainingExitCountdownText").objectReferenceValue = trainingCountdownText;
        so.FindProperty("trickCarouselCanvasRoot").objectReferenceValue = trickCarouselGo;
        SetPrefabArray(so, "trickCarouselPages", trickCarouselPages);
        so.FindProperty("trickCarouselBackground").objectReferenceValue = trickCarouselBgGo;
        so.FindProperty("trickCarouselExitCountdownText").objectReferenceValue = trickExitCountdownText;
        so.FindProperty("startOutline").objectReferenceValue = startRowOutline;
        so.FindProperty("startRowBg").objectReferenceValue = startRowBg;
        so.FindProperty("playerRight").objectReferenceValue = playerRight;
        so.FindProperty("playerLeft").objectReferenceValue = playerLeft;
        so.FindProperty("gestureRight").objectReferenceValue = playerRight.GetComponent<GestureInput>();
        so.FindProperty("gestureLeft").objectReferenceValue = playerLeft.GetComponent<GestureInput>();
        so.FindProperty("joystickLeft").objectReferenceValue = playerLeft.GetComponent<JoystickInput>();
        so.FindProperty("joystickRight").objectReferenceValue = playerRight.GetComponent<JoystickInput>();
        so.FindProperty("gestureCanvasRight").objectReferenceValue = gestureCanvasRight;
        so.FindProperty("gestureCanvasLeft").objectReferenceValue = gestureCanvasLeft;
        so.FindProperty("musicSource").objectReferenceValue = musicSource;
        so.FindProperty("menuMusic").objectReferenceValue = menuMusic;
        SetPrefabArray(so, "trainingPreviewLeftBugs", trainingPreviewLeftBugs);
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
        text.font = GameFont;
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
        title.font = GameFont;
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

    // Wraps a gesture/trick page's own diagram content into an "ОБРАЗЕЦ"
    // (left) vs "ВАШИ ДЕЙСТВИЯ" (right) split, with a dashed line marking
    // the middle — contentBuilder receives the left column as its parent,
    // so each page's existing diagram-building code keeps working
    // unmodified (it was already sized to fit well inside half the page's
    // width). The right column gets one or two flat live-reaction bugs (see
    // CreateLiveBugPreview) drawn the same way the ОБРАЗЕЦ side's own bug
    // is, just driven by real input instead of a scripted loop — leftBug is
    // handed back so the caller can collect it for the 1-player/2-player
    // toggle (see trainingPreviewLeftBugs). The page's own main title stays
    // where it already was, untouched, outside this split.
    // sampleContentScale uniformly shrinks contentBuilder's output around
    // the column's own center — gesture pages pass 1 (their content was
    // already sized to fit half a page), but several trick pages' diagrams
    // were built to span almost the FULL page width/height (routes/arcs
    // reaching ±600+), well past half a page, and need shrinking down to
    // actually fit next to ВАШИ ДЕЙСТВИЯ instead of spilling across it.
    // routeDrawer, if given, draws the same dashed route/path guide on
    // BOTH columns (at the same scale) — per feedback it only ever showed
    // up on the ОБРАЗЕЦ side, leaving a player nothing to actually aim for
    // while practicing against their own live bug on the right.
    //
    // A plain, unscaled child unless scale != 1, in which case content is
    // nested one level deeper inside a uniformly-scaled wrapper centered on
    // the parent — shared by both of CreateTrainingSplit's columns so the
    // ОБРАЗЕЦ diagram and its route-guide twin on ВАШИ ДЕЙСТВИЯ always end
    // up at the exact same effective scale.
    static Transform CreateScaledContainer(Transform parent, float scale)
    {
        if (Mathf.Approximately(scale, 1f))
            return parent;

        var scaledGo = new GameObject("ScaledContent");
        scaledGo.transform.SetParent(parent, false);
        RectTransform scaledRt = scaledGo.AddComponent<RectTransform>();
        scaledRt.anchorMin = new Vector2(0.5f, 0.5f);
        scaledRt.anchorMax = new Vector2(0.5f, 0.5f);
        scaledRt.pivot = new Vector2(0.5f, 0.5f);
        scaledRt.sizeDelta = Vector2.zero;
        scaledRt.anchoredPosition = Vector2.zero;
        scaledGo.transform.localScale = Vector3.one * scale;
        return scaledGo.transform;
    }
    static void CreateTrainingSplit(GameObject page, GameObject playerRight, GameObject playerLeft, float sampleContentScale, out GameObject leftBug, System.Action<Transform> contentBuilder, System.Action<Transform> routeDrawer = null)
    {
        var sampleColumnGo = new GameObject("ObrazetsColumn");
        sampleColumnGo.transform.SetParent(page.transform, false);
        RectTransform sampleColumnRt = sampleColumnGo.AddComponent<RectTransform>();
        sampleColumnRt.anchorMin = new Vector2(0f, 0f);
        sampleColumnRt.anchorMax = new Vector2(0.5f, 1f);
        sampleColumnRt.offsetMin = Vector2.zero;
        sampleColumnRt.offsetMax = Vector2.zero;

        CreateSplitColumnLabel(sampleColumnGo.transform, "ОБРАЗЕЦ", new Color(0.7f, 0.85f, 1f));

        Transform sampleContentParent = CreateScaledContainer(sampleColumnGo.transform, sampleContentScale);
        routeDrawer?.Invoke(sampleContentParent);
        contentBuilder(sampleContentParent);

        var actionColumnGo = new GameObject("VashiDeystviyaColumn");
        actionColumnGo.transform.SetParent(page.transform, false);
        RectTransform actionColumnRt = actionColumnGo.AddComponent<RectTransform>();
        actionColumnRt.anchorMin = new Vector2(0.5f, 0f);
        actionColumnRt.anchorMax = new Vector2(1f, 1f);
        actionColumnRt.offsetMin = Vector2.zero;
        actionColumnRt.offsetMax = Vector2.zero;

        // Same route, same scale, drawn first so the live bugs below render
        // in front of it rather than being obscured by it.
        Transform actionRouteParent = CreateScaledContainer(actionColumnGo.transform, sampleContentScale);
        routeDrawer?.Invoke(actionRouteParent);

        CreateSplitColumnLabel(actionColumnGo.transform, "ВАШИ ДЕЙСТВИЯ", new Color(0.6f, 1f, 0.6f));

        const float bugHeight = 200f; // matches the ОБРАЗЕЦ side's own bug size exactly
        // Roughly the vertical center of the page (was 100, up near the
        // ОБРАЗЕЦ bug's own old height) — per feedback, its resting/start
        // pose should sit closer to the middle of this column, not high up.
        // Set to the exact midpoint between top (rest+flyRise=90) and duck
        // (rest-60-duckDrop=-265) — was 0, then -25, neither was the real
        // midpoint of those 2 fixed endpoints. flyRise/duckDrop
        // (LiveBugReactionAnimator) were adjusted by the same amount this
        // moved so top's and duck's own on-screen positions stay put.
        const float liveBugRestY = -87.5f;
        // Rest (center) positions sit close together — was ±90, a bit
        // wider than the bugs' own half-width (~98, at bugHeight 200) so
        // they didn't even touch — per feedback the two should read as
        // "almost in the same spot, just slightly offset" at rest, only
        // pulling apart once one of them actually leans away.
        const float liveBugRestX = 35f;
        // Right side for player-right (IJKL, physically the right side of a
        // keyboard), left side for player-left (WASD, physically the left
        // side) — matches the real game's own control layout, per feedback
        // (was swapped from this before). Each bug's 3 lane X
        // coordinates are explicit, not computed from a shift — both bugs
        // share the same column and are meant to freely cross into each
        // other's territory while leaning (per feedback, deliberately
        // UNLIKE the real game's own separate, non-crossing lanes).
        // Tinted the same colors the real players wear in-game (see
        // CreatePlayer's own tint args).
        CreateLiveBugPreview(actionColumnGo.transform, new Vector2(liveBugRestX, liveBugRestY), bugHeight,
            playerRight.GetComponent<GestureInput>(), playerRight.GetComponent<JoystickInput>(),
            KeyCode.J, KeyCode.L, KeyCode.I, KeyCode.K, PlayerTwoBugSprite, PlayerTwoBugTint, -140f, liveBugRestX, 160f);
        leftBug = CreateLiveBugPreview(actionColumnGo.transform, new Vector2(-liveBugRestX, liveBugRestY), bugHeight,
            playerLeft.GetComponent<GestureInput>(), playerLeft.GetComponent<JoystickInput>(),
            KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.S, PlayerOneBugSprite, PlayerOneBugTint, -160f, -liveBugRestX, 140f);

        CreateDashedVerticalDivider(page.transform);
    }

    // One flat, live-reacting bug for a "ВАШИ ДЕЙСТВИЯ" column — same visual
    // vocabulary as GestureDiagramAnimation's own образец bug (duck squash,
    // lean shift+tilt, flap rise+frame-swap), driven by LiveBugReactionAnimator
    // from the given real player's actual input instead of a scripted loop.
    static GameObject CreateLiveBugPreview(Transform parent, Vector2 anchoredPos, float bugHeight,
        GestureInput gestureInput, JoystickInput joystickInput, KeyCode left, KeyCode right, KeyCode up, KeyCode down, string spriteFile, Color tint,
        float laneXLeft, float laneXCenter, float laneXRight)
    {
        Texture2D bugTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + spriteFile);
        var bugGo = new GameObject("LiveBug");
        bugGo.transform.SetParent(parent, false);
        RawImage bugImage = bugGo.AddComponent<RawImage>();
        bugImage.texture = bugTex;
        bugImage.color = tint;
        RectTransform bugRt = bugImage.GetComponent<RectTransform>();
        bugRt.anchorMin = new Vector2(0.5f, 0.5f);
        bugRt.anchorMax = new Vector2(0.5f, 0.5f);
        bugRt.pivot = new Vector2(0.5f, 0.5f);
        float bugAspect = bugTex != null ? (float)bugTex.width / bugTex.height : 1f;
        bugRt.sizeDelta = new Vector2(bugHeight * bugAspect, bugHeight);
        bugRt.anchoredPosition = anchoredPos;

        LiveBugReactionAnimator anim = bugGo.AddComponent<LiveBugReactionAnimator>();
        SerializedObject so = new SerializedObject(anim);
        so.FindProperty("gestureInput").objectReferenceValue = gestureInput;
        so.FindProperty("joystickInput").objectReferenceValue = joystickInput;
        so.FindProperty("leftKey").intValue = (int)left;
        so.FindProperty("rightKey").intValue = (int)right;
        so.FindProperty("upKey").intValue = (int)up;
        so.FindProperty("downKey").intValue = (int)down;
        // Жужжание крыльев в полёте — тот же клип, что у настоящих жуков
        // (PlayerMovementSfx). Не autoplay: источник заводится в Start()
        // самого аниматора на нулевой громкости.
        AudioSource wings = bugGo.AddComponent<AudioSource>();
        wings.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/LadyBug/Audio/lady_bug/Buzz.wav");
        wings.loop = true;
        wings.playOnAwake = false;
        wings.volume = 0f;
        if (wings.clip == null)
            Debug.LogWarning("LiveBugPreview: Buzz.wav not found — training flight will be silent.");
        so.FindProperty("wingsSource").objectReferenceValue = wings;

        // Бег лапами в приседе — вторая половина той же пары, что и в игре
        // (PlayerMovementSfx: ноги на земле, крылья в воздухе).
        AudioSource feet = bugGo.AddComponent<AudioSource>();
        feet.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/LadyBug/Audio/lady_bug/RunFeet.mp3");
        feet.loop = true;
        feet.playOnAwake = false;
        feet.volume = 0f;
        if (feet.clip == null)
            Debug.LogWarning("LiveBugPreview: RunFeet.mp3 not found — training duck will be silent.");
        so.FindProperty("feetSource").objectReferenceValue = feet;
        so.FindProperty("bugImage").objectReferenceValue = bugImage;
        so.FindProperty("bugNormalTexture").objectReferenceValue = bugTex;
        so.FindProperty("bugAirTexture1").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + spriteFile.Replace(".png", "Air1.png"));
        so.FindProperty("bugAirTexture2").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + spriteFile.Replace(".png", "Air2.png"));
        so.FindProperty("laneXLeft").floatValue = laneXLeft;
        so.FindProperty("laneXCenter").floatValue = laneXCenter;
        so.FindProperty("laneXRight").floatValue = laneXRight;
        so.ApplyModifiedPropertiesWithoutUndo();

        return bugGo;
    }

    static void CreateSplitColumnLabel(Transform parent, string text, Color color)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        Text label = go.AddComponent<Text>();
        label.font = GameFont;
        label.fontSize = 24;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = color;
        label.text = text;
        go.AddComponent<Outline>().effectColor = Color.black;
        RectTransform rt = label.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(600f, 40f);
        rt.anchoredPosition = new Vector2(0f, 240f);
    }

    // Vertical dashed line at the page's own horizontal center, marking the
    // ОБРАЗЕЦ/ВАШИ ДЕЙСТВИЯ boundary.
    static void CreateDashedVerticalDivider(Transform parent)
    {
        const int dashCount = 14;
        const float dashHeight = 22f;
        const float gap = 14f;
        const float totalHeight = dashCount * dashHeight + (dashCount - 1) * gap;
        float startY = totalHeight / 2f - dashHeight / 2f;

        for (int i = 0; i < dashCount; i++)
        {
            var dashGo = new GameObject("DividerDash");
            dashGo.transform.SetParent(parent, false);
            Image dash = dashGo.AddComponent<Image>();
            dash.color = new Color(1f, 1f, 1f, 0.35f);
            RectTransform dashRt = dash.GetComponent<RectTransform>();
            dashRt.anchorMin = new Vector2(0.5f, 0.5f);
            dashRt.anchorMax = new Vector2(0.5f, 0.5f);
            dashRt.pivot = new Vector2(0.5f, 0.5f);
            dashRt.sizeDelta = new Vector2(4f, dashHeight);
            dashRt.anchoredPosition = new Vector2(0f, startY - i * (dashHeight + gap));
        }
    }

    static void CreatePageCaption(Transform parent, string text)
    {
        var go = new GameObject("Caption");
        go.transform.SetParent(parent, false);
        Text caption = go.AddComponent<Text>();
        caption.font = GameFont;
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

        const float topY = 130f;
        const float bottomY = -220f;
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
        mark.font = GameFont;
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
        line.font = GameFont;
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
        lineRt.sizeDelta = new Vector2(1000f, 72f);
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

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + items[i].file);

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
            label.font = GameFont;
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
    static RectTransform CreateSensorGlyph(Transform parent, Vector2 anchoredPos, float palmOffset, float scale = 1f, float tiltAngle = 0f, float widthScale = 1f)
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
        palmRt.sizeDelta = new Vector2(70f * scale * widthScale, 16f * scale);
        palmRt.anchoredPosition = anchoredPos + new Vector2(0f, palmOffset);
        palmRt.localRotation = Quaternion.Euler(0f, 0f, tiltAngle);
        return palmRt;
    }

    // Real generated artwork (asset_gen/gen_asset.sh) — a volumetric
    // red-ball-on-black-base arcade joystick, replacing the earlier flat
    // procedural base/shaft/knob composition (see git history) with
    // something that actually reads as a physical joystick at a glance.
    static void CreateJoystickIcon(Transform parent, Vector2 pos, float scale = 1f, float widthScale = 1f)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/Joystick.png");

        var go = new GameObject("JoystickIcon");
        go.transform.SetParent(parent, false);
        RawImage img = go.AddComponent<RawImage>();
        img.texture = tex;
        RectTransform rt = img.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        float aspect = tex != null ? (float)tex.width / tex.height : 0.875f;
        rt.sizeDelta = new Vector2(160f * aspect * scale * widthScale, 160f * scale);
        rt.anchoredPosition = pos + new Vector2(0f, -5f * scale);
    }

    static void CreateControlsSubLabel(Transform parent, Vector2 pos, string text, int fontSize = 26, float boxWidth = 320f)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        Text label = go.AddComponent<Text>();
        label.font = GameFont;
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
        rt.sizeDelta = new Vector2(boxWidth, Mathf.Max(40f, fontSize + 12f));
        rt.anchoredPosition = pos;
    }

    // Same copy as MenuHelpText on the live menu (StartScreenController) —
    // shown once in the upfront carousel after СУТЬ/ЦЕЛЬ so players read it
    // before the object/controls pages; the bottom-left hint stays too.
    // Checklist rows + ИГРОК 1/2 labels mirror how the live menu maps keys.
    // The two ИГРОК rows are handed back so StartScreenController can rewrite
    // them once controller detection settles — this page is built here with
    // the keyboard mapping, but on the real cabinet it has to name the
    // sensors and the joystick instead (see UpdateMenuHelpText).
    static (GameObject page, Text player1Row, Text player2Row) CreateMenuSelectionPage(Transform parent)
    {
        GameObject page = CreateFillPage(parent, "Page_MenuSelection");

        CreatePageTitle(page.transform, "ВЫБОР В МЕНЮ", new Color(1f, 0.85f, 0.2f));

        const int sectionFontSize = 40;
        CreateControlsSubLabel(page.transform, new Vector2(0f, 150f), "ВЫБОР:", sectionFontSize, 420f);
        Text player1Row = CreateChecklistRow(page.transform, 75f, "ИГРОК 1: WASD");
        Text player2Row = CreateChecklistRow(page.transform, 10f, "ИГРОК 2: IJKL");

        CreateControlsSubLabel(page.transform, new Vector2(0f, -60f), "НАЧАЛО:", sectionFontSize, 420f);
        CreateChecklistRow(page.transform, -125f, "ВЫБРАТЬ СТАРТ ИЛИ ТРЕНИРОВКА");
        CreateChecklistRow(page.transform, -190f, "И ДЕРЖАТЬ ВНИЗ 5 СЕК");

        return (page, player1Row, player2Row);
    }

    // УПРАВЛЕНИЕ: keeps the original 2-line keyboard mapping (top/bottom,
    // same spread CreateChecklistPage used for these exact two lines), and
    // adds the two real hardware alternatives in the gap between them — the
    // co-op rig is one board of hand sensors for player 1 (left) and one
    // joystick board for player 2 (right), see GestureSensorSerial/
    // JoystickSerial — same up/down/left-right mapping either way, just
    // different hardware reading it.
    static GameObject CreateControlsPage(Transform parent)
    {
        GameObject page = CreateFillPage(parent, "Page_Controls");

        CreatePageTitle(page.transform, "УПРАВЛЕНИЕ", new Color(1f, 0.85f, 0.2f));

        // Player columns: no checkboxes (only the exit block keeps one).
        // Labels sit higher now; icons sit lower with a clear gap so the
        // laser/joystick art doesn't creep into the hardware line above.
        const float playerX = 400f;
        const int blockFontSize = 34;
        const float iconScale = 1.3f;
        const float sensorIconY = -38f;
        const float sensorSpread = 58f;
        const float sensorWidthScale = 0.88f;
        const float joystickIconY = -58f;
        const float joystickWidthScale = 1.34f;

        CreateControlsSubLabel(page.transform, new Vector2(-playerX, 175f), "ИГРОК 1", blockFontSize);
        CreateControlsSubLabel(page.transform, new Vector2(-playerX, 105f), "ДАТЧИКИ", blockFontSize);
        CreateSensorGlyph(page.transform, new Vector2(-playerX - sensorSpread, sensorIconY), -52f, iconScale, 0f, sensorWidthScale);
        CreateSensorGlyph(page.transform, new Vector2(-playerX + sensorSpread, sensorIconY), 52f, iconScale, 0f, sensorWidthScale);

        CreateControlsSubLabel(page.transform, new Vector2(playerX, 175f), "ИГРОК 2", blockFontSize);
        CreateControlsSubLabel(page.transform, new Vector2(playerX, 105f), "ДЖОЙСТИК", blockFontSize);
        CreateJoystickIcon(page.transform, new Vector2(playerX, joystickIconY), iconScale, joystickWidthScale);

        CreateVerticalCheck(page.transform, new Vector2(0f, -130f));
        CreateControlsSubLabel(page.transform, new Vector2(0f, -200f), "ВЫХОД", blockFontSize);
        CreateControlsSubLabel(page.transform, new Vector2(0f, -260f), "ПРИСЕСТЬ ОБОИМ НА 5 СЕКУНД", blockFontSize, 620f);

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
        mark.font = GameFont;
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
    static (GameObject page, GameObject leftBug) CreateGestureDiagramPage(Transform parent, string title, bool leftGoesUp, bool rightGoesUp, bool isFlap, GameObject playerRight, GameObject playerLeft)
    {
        GameObject page = CreateFillPage(parent, "Page_Gesture_" + title);

        CreatePageTitle(page.transform, title, new Color(1f, 0.85f, 0.2f));

        CreateTrainingSplit(page, playerRight, playerLeft, 1f, out GameObject leftBug, content =>
        {
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
            RectTransform leftPalm = CreateSensorGlyph(content, new Vector2(-glyphSpacing, bottomHalfCenterY), 0f, glyphScale, glyphTilt);
            RectTransform rightPalm = CreateSensorGlyph(content, new Vector2(glyphSpacing, bottomHalfCenterY), 0f, glyphScale, glyphTilt);

            Color arrowColor = new Color(1f, 0.85f, 0.2f);
            // "↕" (not a plain "↑") for the flap gesture — a fixed "up" arrow
            // read as a static pose, not the repeated up/down motion the actual
            // gesture needs; it also bounces in sync with the palm at runtime
            // (see GestureDiagramAnimation.AnimateFlap) so the *motion* itself
            // carries the rhythm, not just the glyph.
            string leftGlyph = isFlap ? "↕" : (leftGoesUp ? "↑" : "↓");
            string rightGlyph = isFlap ? "↕" : (rightGoesUp ? "↑" : "↓");
            RectTransform leftArrow = CreateSingleArrow(content, leftGlyph, arrowColor, new Vector2(-glyphSpacing - 80f, bottomHalfCenterY));
            RectTransform rightArrow = CreateSingleArrow(content, rightGlyph, arrowColor, new Vector2(glyphSpacing + 80f, bottomHalfCenterY));
            leftArrow.gameObject.SetActive(false);
            rightArrow.gameObject.SetActive(false);

            // Top half: the ladybug that actually performs the resulting move,
            // real in-game poses and all (see GestureDiagramAnimation's class
            // comment) — like the screen above an arcade cabinet's controls,
            // making the abstract sensor reading concrete ("this hand shape
            // means THIS happens to you"). Lowered further (was 100) — per
            // feedback it was still touching the page's own title above it.
            const float topHalfCenterY = 60f;
            const float bugHeight = 200f;
            Texture2D bugTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + PlayerOneBugSprite);
            var bugGo = new GameObject("Bug");
            bugGo.transform.SetParent(content, false);
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
            // Одиночный жук страницы жестов — всегда игрок 1, воздушные кадры от него же.
            so.FindProperty("bugAirTexture1").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + AirSpriteFor(PlayerOneBugSprite));
            so.FindProperty("bugAirTexture2").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + PlayerOneBugSprite.Replace(".png", "Air2.png"));
            so.ApplyModifiedPropertiesWithoutUndo();
        });

        return (page, leftBug);
    }

    // The АРКА trick, animated rather than a static diagram: one ladybug
    // low (ducks), one high (jumps) — direction arrows appear next to each,
    // they react, an arch grows in from the distance and passes between
    // them, then a "trick complete" line flashes. No caption/static
    // per-icon labels — the animation itself carries the explanation.
    // See ArchTrickAnimation.cs for the actual playback.
    static (GameObject page, GameObject leftBug) CreateArchTrickPage(Transform parent, GameObject playerRight, GameObject playerLeft)
    {
        GameObject page = CreateFillPage(parent, "Page_Trick_ArchAnim");
        // Clips the arch to the page bounds once it grows past archMid* —
        // it used to spill past the visible frame at the biggest sizes
        // instead of just filling it.
        page.AddComponent<RectMask2D>();
        CreatePageTitle(page.transform, "ТРЮК: АРКА", new Color(0.7f, 0.4f, 1f));

        GameObject leftBug;
        CreateTrainingSplit(page, playerRight, playerLeft, 1f, out leftBug, content =>
        {
            const float bugHeight = 150f;
            const float bugY = 130f;
            const float arrowXOffset = 210f;

            RectTransform bottomBug = CreateTrickBugIcon(content, PlayerTwoBugSprite, new Vector2(0f, -bugY), bugHeight);
            RectTransform topBug = CreateTrickBugIcon(content, PlayerOneBugSprite, new Vector2(0f, bugY), bugHeight);

            GameObject downArrows = CreateArrowPair(content, "DownArrows", "↓", new Color(1f, 0.85f, 0.2f), -bugY, arrowXOffset);
            GameObject upArrows = CreateArrowPair(content, "UpArrows", "↑", new Color(1f, 0.85f, 0.2f), bugY, arrowXOffset);

            Texture2D archTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/SmallArch.png");
            var archGo = new GameObject("Arch");
            // Parented to the whole page, not content (the ОБРАЗЕЦ half) —
            // content's own center is the LEFT half's center, which had the
            // arch passing through off to one side instead of straight down
            // the page's own true middle, symmetric across both halves.
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
            successGo.transform.SetParent(content, false);
            Text successText = successGo.AddComponent<Text>();
            successText.font = GameFont;
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
            // Выводится из спрайта самого верхнего жука (PlayerOneBugSprite выше),
            // а не задаётся отдельно — иначе он менял цвет при взлёте, см. AirSpriteFor.
            so.FindProperty("topBugAirTexture").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + AirSpriteFor(PlayerOneBugSprite));
            so.ApplyModifiedPropertiesWithoutUndo();
        });

        return (page, leftBug);
    }

    // Only two tricks actually exist in the game (see PlayerController's
    // TryDetectRingTrick/AwardArchTrickOnce, and AchievementStats'
    // RingTricks/ArchTricks — nothing else spawns a trick popup) — this is
    // КОЛЬЦО's animated instruction page, same slide-by-slide treatment as
    // CreateArchTrickPage/ArchTrickAnimation replaces the old static
    // two-icon diagram with.
    static (GameObject page, GameObject leftBug) CreateRingTrickPage(Transform parent, GameObject playerRight, GameObject playerLeft)
    {
        GameObject page = CreateFillPage(parent, "Page_Trick_RingAnim");
        CreatePageTitle(page.transform, "ТРЮК: КОЛЬЦО", new Color(0.7f, 0.4f, 1f));

        GameObject leftBug;
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
        // CreateDashedPathTexture's own showArrowheads comment). Drawn on
        // both ОБРАЗЕЦ and ВАШИ ДЕЙСТВИЯ (routeDrawer, see CreateTrainingSplit)
        // instead of just inline here, so it isn't ОБРАЗЕЦ-only — built once
        // and shared between both (see CreateDashedRouteBackdrop's own
        // comment on why generating it twice is wasteful).
        const float routeWorldSpan = 1400f;
        Texture2D routeTex = CreateDashedPathTexture(900, routeWorldSpan, new System.Func<float, Vector2>[] { oval }, null, showArrowheads: false);
        System.Action<Transform> routeDrawer = routeContent => CreateDashedRouteBackdrop(routeContent, routeTex, routeWorldSpan);
        // Oval route reaches ±352 wide natively (bugX*1.6) — well past half
        // a page — shrunk down (0.72) to actually fit next to ВАШИ ДЕЙСТВИЯ.
        const float sampleContentScale = 0.72f;
        CreateTrainingSplit(page, playerRight, playerLeft, sampleContentScale, out leftBug, content =>
        {
            const float bugHeight = 150f;

            // Both bugs start right at the oval's own bottom point, not y=0 —
            // the whole rise/cross/come-down animation is built relative to
            // wherever these two start (RingTrickAnimation.Awake reads their
            // position directly), so this lines the entire loop up with the
            // route drawn behind it instead of starting mid-air relative to it.
            // LadyBug1 (красный, игрок 1) слева, LadyBug2 (сине-фиолетовый,
            // игрок 2) справа — та же раскладка лево/право, что у живых жуков
            // «ВАШИ ДЕЙСТВИЯ», чтобы ОБРАЗЕЦ её не переворачивал.
            float startY = -ovalYRadius;
            RectTransform airBug = CreateTrickBugIcon(content, PlayerOneBugSprite, new Vector2(-bugX, startY), bugHeight);
            RectTransform groundBug = CreateTrickBugIcon(content, PlayerTwoBugSprite, new Vector2(bugX, startY), bugHeight);

            // Single reusable arrow per bug — glyph and position are swapped
            // per beat by RingTrickAnimation itself (up, then sideways in each
            // bug's own direction, then down), not a fixed pair shown throughout.
            Color arrowColor = new Color(1f, 0.85f, 0.2f);
            RectTransform airArrow = CreateSingleArrow(content, "↑", arrowColor, new Vector2(-bugX, arrowY));
            RectTransform groundArrow = CreateSingleArrow(content, "←", arrowColor, new Vector2(bugX, -arrowY));
            airArrow.gameObject.SetActive(false);
            groundArrow.gameObject.SetActive(false);

            var successGo = new GameObject("SuccessText");
            successGo.transform.SetParent(content, false);
            Text successText = successGo.AddComponent<Text>();
            successText.font = GameFont;
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
            // Counter-scaled against content's own sampleContentScale
            // shrink — see the same fix's comment in CreateTrickDiagramPage.
            successGo.transform.localScale = Vector3.one / sampleContentScale;
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
            // Выводится из спрайта летящего жука (PlayerOneBugSprite выше) — см. AirSpriteFor.
            ringSo.FindProperty("airBugAirTexture").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + AirSpriteFor(PlayerOneBugSprite));
            ringSo.ApplyModifiedPropertiesWithoutUndo();
        }, routeDrawer);

        return (page, leftBug);
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
    static (GameObject page, GameObject leftBug, Transform content) CreateTrickDiagramPage(Transform parent, string title, string spriteA, string spriteB,
        TrickDiagramAnimation.Step[] pathA, TrickDiagramAnimation.Step[] pathB,
        GameObject playerRight, GameObject playerLeft, float sampleContentScale,
        float staggerDelay = 0f, (Vector2 pos, float radius)[] routeDots = null, float laneSpacing = 210f,
        bool showArrowheads = true, params System.Func<float, Vector2>[] routeCurves)
    {
        GameObject page = CreateFillPage(parent, "Page_Trick_" + title);
        CreatePageTitle(page.transform, "ТРЮК: " + title, new Color(0.7f, 0.4f, 1f));

        GameObject leftBug;
        Transform capturedContent = null;
        System.Action<Transform> routeDrawer = null;
        const float routeWorldSpan = 1400f;
        if ((routeCurves != null && routeCurves.Length > 0) || (routeDots != null && routeDots.Length > 0))
        {
            // Built once and shared between ОБРАЗЕЦ and ВАШИ ДЕЙСТВИЯ (see
            // CreateDashedRouteBackdrop's own comment on why generating this
            // twice per page is wasteful).
            Texture2D routeTex = CreateDashedPathTexture(900, routeWorldSpan, routeCurves, routeDots, showArrowheads: showArrowheads);
            routeDrawer = routeContent => CreateDashedRouteBackdrop(routeContent, routeTex, routeWorldSpan);
        }
        CreateTrainingSplit(page, playerRight, playerLeft, sampleContentScale, out leftBug, content =>
        {
            capturedContent = content;

            const float bugHeight = 150f;

            Vector2 startA = new Vector2((pathA[0].lane - 1) * laneSpacing, pathA[0].y);
            Vector2 startB = new Vector2((pathB[0].lane - 1) * laneSpacing, pathB[0].y);
            RectTransform bugA = CreateTrickBugIcon(content, spriteA, startA, bugHeight);
            RectTransform bugB = CreateTrickBugIcon(content, spriteB, startB, bugHeight);

            // Small — half the size CreateSingleArrow's other callers (Arch/
            // Ring's own big directional cues) use, since these ride right next
            // to the bug on every little step rather than standing alone.
            Color arrowColor = new Color(1f, 0.85f, 0.2f);
            RectTransform arrowA = CreateSingleArrow(content, "→", arrowColor, startA);
            arrowA.localScale = Vector3.one * 0.5f;
            arrowA.gameObject.SetActive(false);
            RectTransform arrowB = CreateSingleArrow(content, "→", arrowColor, startB);
            arrowB.localScale = Vector3.one * 0.5f;
            arrowB.gameObject.SetActive(false);

            var successGo = new GameObject("SuccessText");
            successGo.transform.SetParent(content, false);
            Text successText = successGo.AddComponent<Text>();
            successText.font = GameFont;
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
            // Counter-scaled against content's own sampleContentScale shrink
            // (see CreateScaledContainer) — sizeDelta/anchoredPosition are
            // in content's local units so the text still sits in the same
            // relative spot, but its actual rendered font size was
            // shrinking right along with the bug icons/route, unlike ВАШИ
            // ДЕЙСТВИЯ's own (unscaled) text. Only the picture elements
            // were meant to shrink to fit half a page — per feedback the
            // text should read at full native size like the live side does.
            successGo.transform.localScale = Vector3.one / sampleContentScale;
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
            // Выводятся из spriteA/spriteB — см. AirSpriteFor.
            anim.airTextureA = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + AirSpriteFor(spriteA));
            anim.airTextureB = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + AirSpriteFor(spriteB));
        }, routeDrawer);

        return (page, leftBug, capturedContent);
    }

    // ЧЕХАРДА: both start stacked in an edge lane (A riding on B). A
    // dismounts to the middle lane sideways first, THEN comes down — two
    // separate steps, not one diagonal move — freeing B to clear both
    // remaining lanes in one continuous airborne hop, from the edge lane
    // straight to the opposite edge, never landing in between.
    static (GameObject page, GameObject leftBug) CreateLeapfrogTrickPage(Transform parent, GameObject playerRight, GameObject playerLeft)
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
        // Native route reach (±620) is well past half a page — shrunk down
        // (0.47) to actually fit next to ВАШИ ДЕЙСТВИЯ, same reasoning as
        // RingTrickPage's own scale.
        var (page, leftBug, _) = CreateTrickDiagramPage(parent, "ЧЕХАРДА", PlayerOneBugSprite, PlayerTwoBugSprite,
            pathA, pathB, playerRight, playerLeft, 0.47f, 0f, null, wideLaneSpacing, true, arc1, arc2);
        return (page, leftBug);
    }

    // СИНХРОН: same stacked start as ЧЕХАРДА, but this time the pair never
    // separates — both move together, 2 lane-steps, straight over to the
    // opposite edge lane, still stacked at the end. Base sits well below
    // the rider (110 vs -70, more than their combined half-heights apart)
    // so the two icons don't visually overlap.
    static (GameObject page, GameObject leftBug) CreateSyncTrickPage(Transform parent, GameObject playerRight, GameObject playerLeft)
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
        var (page, leftBug, _) = CreateTrickDiagramPage(parent, "СИНХРОН", PlayerOneBugSprite, PlayerTwoBugSprite,
            pathA, pathB, playerRight, playerLeft, 0.47f, 0f, dots, wideLaneSpacing, true, rowA1, rowA2, rowB1, rowB2);
        return (page, leftBug);
    }

    // ЗАВИСАНИЕ: both jump up (each in their own lane) and stay up, bobbing
    // in place without ever touching down, before landing together —
    // holding Up (or tapping it again right as a jump would otherwise end)
    // keeps chaining into another jump as long as a partner is also
    // airborne, see PlayerController.UpdateVerticalState.
    static (GameObject page, GameObject leftBug) CreateHoverTrickPage(Transform parent, GameObject playerRight, GameObject playerLeft)
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
        // No route overlay on this one at all (no routeDots/routeCurves
        // passed below) and the default (narrow) laneSpacing, so its native
        // content already fits comfortably next to ВАШИ ДЕЙСТВИЯ — just a
        // small safety-margin shrink, not the aggressive one the wide-route
        // pages need.
        // spriteA=LadyBug1 (красный, игрок 1) едет по pathA; spriteB=LadyBug2
        // (сине-фиолетовый, игрок 2) по pathB. Воздушные кадры выводятся из них же.
        var (page, leftBug, content) = CreateTrickDiagramPage(parent, "ЗАВИСАНИЕ", PlayerOneBugSprite, PlayerTwoBugSprite,
            pathA, pathB, playerRight, playerLeft, 0.85f);

        var counterGo = new GameObject("HoverCounter");
        counterGo.transform.SetParent(content, false);
        Text counterText = counterGo.AddComponent<Text>();
        counterText.font = GameFont;
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

        return (page, leftBug);
    }

    // БОЛЬШОЕ КОЛЬЦО: one after another, not simultaneous — B starts a
    // beat after A (staggerDelay) instead of moving in lockstep with it.
    // Both sweep 2 lanes one way along the ground, then 2 lanes back the
    // other way in the air — a there-and-back loop across all 3 lanes.
    // Wide laneSpacing (below) matches the oval route's own reach instead
    // of a narrow band in the page's center, and B starts already at the
    // middle lane (rather than stacked on A's own edge start) so the two
    // icons read as distinct from the very first frame, not overlapping.
    static (GameObject page, GameObject leftBug) CreateBigRingTrickPage(Transform parent, GameObject playerRight, GameObject playerLeft)
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
        // Oval's own reach (±650) is the widest of any trick page's route —
        // shrunk down (0.46) to fit next to ВАШИ ДЕЙСТВИЯ.
        // spriteA=LadyBug1 (красный, игрок 1); spriteB=LadyBug2 (сине-фиолетовый, игрок 2).
        var (page, leftBug, _) = CreateTrickDiagramPage(parent, "БОЛЬШОЕ КОЛЬЦО", PlayerOneBugSprite, PlayerTwoBugSprite,
            pathA, pathB, playerRight, playerLeft, 0.46f, 0f, null, wideLaneSpacing, true, oval);
        return (page, leftBug);
    }

    // БЕСКОНЕЧНОСТЬ: both bugs run the same figure-8 shape simultaneously
    // (no staggerDelay) — B's own loop is A's loop shape entered from a
    // different point (edge instead of center), same idea as БОЛЬШОЕ
    // КОЛЬЦО. Each side-trip is now its own explicit arrive-then-land pair
    // (fly back to center, THEN a separate touch-down beat) rather than
    // landing directly off the flight. Wide laneSpacing (below) matches
    // the lemniscate route's own reach.
    static (GameObject page, GameObject leftBug) CreateInfinityTrickPage(Transform parent, GameObject playerRight, GameObject playerLeft)
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
        // Lemniscate's own reach (±600) is well past half a page — shrunk
        // down (0.5) to fit next to ВАШИ ДЕЙСТВИЯ.
        var (page, leftBug, _) = CreateTrickDiagramPage(parent, "БЕСКОНЕЧНОСТЬ", PlayerOneBugSprite, PlayerTwoBugSprite,
            pathA, pathB, playerRight, playerLeft, 0.5f, 0f, null, wideLaneSpacing, false, infinity);
        return (page, leftBug);
    }

    // LadyBug1 = красный P1, LadyBug2 = сине-фиолетовый P2; здесь оба на белом тоне.
    static RectTransform CreateTrickBugIcon(Transform parent, string spriteFile, Vector2 pos, float height)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/" + spriteFile);

        var iconGo = new GameObject("Bug_" + spriteFile);
        iconGo.transform.SetParent(parent, false);
        RawImage icon = iconGo.AddComponent<RawImage>();
        icon.texture = tex;
        icon.color = Color.white;
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
        text.font = GameFont;
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
    // reach into that runtime-only private array at edit time). Category
    // INDICES (used for data — PlayerPrefs, ranksByCategory, etc.) stay in
    // this order everywhere; only the on-screen page ORDER is different,
    // see TopResultsDisplayOrder below.
    static readonly string[] TopCategoryNames = { "ВРЕМЯ", "ОЧКИ", "ТРЮКИ", "СКОРОСТЬ" };

    // Display order for the top-results pages (start-screen carousel and
    // the win-sequence leaderboard both use this) — Очки/Время/Скорость/
    // Трюки, matching the HUD panel stack's own top-to-bottom order
    // (ОЧКИ/ВРЕМЯ/.../СКОРОСТЬ/ТРЮКИ), not the category's internal index
    // order above.
    static readonly int[] TopResultsDisplayOrder = { 1, 0, 3, 2 };

    static GameObject CreateTopResultsPage(Transform parent, int category)
    {
        GameObject page = CreateFillPage(parent, "Page_TopResults_" + category);

        // Placeholder text — TopResultsPage.RefreshTitle/RefreshTable
        // overwrite these once the real leaderboard data is available, on
        // its own staged reveal (empty -> title -> table, see that
        // script's own comment); this is just what shows for the briefest
        // instant before that reveal starts.
        string categoryName = category >= 0 && category < TopCategoryNames.Length ? TopCategoryNames[category] : "?";

        // Every row element (medals/values/photos/arrows below) is parented
        // here instead of directly under `page`, so TopResultsPage can
        // reveal them as one group in its own "then the table appears"
        // stage — the title alone stays a direct child of `page`.
        GameObject tableGroupGo = CreateFillPage(page.transform, "TableGroup");

        // Three columns: title (left, vertically centered), rank+result
        // (middle, split into its own 2 sub-columns), photo (right, sized
        // to touch the carousel background's top and bottom edges) — not
        // all three crowded near the center.
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(page.transform, false);
        Text titleText = titleGo.AddComponent<Text>();
        titleText.font = GameFont;
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
        // -510, not -560 — shifted right to match the box's own left edge
        // moving in from -700 to -650 (see carouselRt/leaderboardRootRt's
        // now-1300-wide box), keeping the same ~10px margin it always had.
        titleRt.anchoredPosition = new Vector2(-510f, 0f);

        // One row per rank — each with its own photo (if that slot ever had
        // one attached), not a single photo for #1 shown off to the side.
        var rowValueTexts = new Text[3];
        var rowMedals = new RawImage[3];
        var rowPhotos = new RawImage[3];
        var rowArrowShafts = new GameObject[3];
        var rowArrowHeads = new GameObject[3];
        float[] rowY = { 245f, 0f, -245f };

        // Real generated medal art (asset_gen/gen_asset.sh, gold/silver/
        // bronze, each with its own embossed star) — replaces an earlier
        // plain flat-tinted circle, which read as just another number next
        // to the result value rather than an actual prize. The medal
        // itself (color + star) already reads as "1st/2nd/3rd" the same
        // way a real medal ceremony does, so there's no separate rank
        // digit drawn on top of it anymore — nowhere clean to put one
        // without covering the star.
        Texture2D[] medalTextures =
        {
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/MedalGold.png"),
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/MedalSilver.png"),
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/lady_bug/MedalBronze.png"),
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
        const float valueRightEdge = -110f + 190f / 2f; // matches rowValueRt's own -110 anchoredPosition.x above
        Texture2D arrowHeadTexture = CreateTriangleTexture(64);

        for (int i = 0; i < 3; i++)
        {
            var medalGo = new GameObject("RowMedal" + i);
            medalGo.transform.SetParent(tableGroupGo.transform, false);
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
            medalRt.anchoredPosition = new Vector2(-285f, rowY[i]); // shifted right 50, same reasoning as titleRt above
            rowMedals[i] = medalImg;

            var rowValueGo = new GameObject("RowValue" + i);
            rowValueGo.transform.SetParent(tableGroupGo.transform, false);
            Text rowValue = rowValueGo.AddComponent<Text>();
            rowValue.font = GameFont;
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
            rowValueRt.anchoredPosition = new Vector2(-110f, rowY[i]); // shifted right 50, same reasoning as titleRt above
            rowValueTexts[i] = rowValue;

            // Connects the result to its photo with a real shaft+arrowhead
            // spanning (almost) the whole gap between them, not a small
            // glyph floating in the middle of it — the 2-column zigzag means
            // a row's photo isn't always directly beside its text anymore
            // (rank 2's photo sits in the nearer column), so without this
            // the link between a result and its picture isn't obvious.
            // Sized to whatever gap that row actually has (rank 2's is much
            // tighter than rank 1/3's, so its arrow comes out shorter too).
            float photoLeftEdge = 650f + photoX[i] - photoSize; // 650 = half of the carousel box's now-1300 width
            float arrowY = (rowY[i] + photoY[i]) / 2f;
            float gap = photoLeftEdge - valueRightEdge;
            float headSize = Mathf.Clamp(gap * 0.3f, 18f, 44f);
            float headX = photoLeftEdge - 8f - headSize / 2f;
            float shaftEndX = headX - headSize / 2f - 6f;
            float shaftStartX = valueRightEdge + 10f;
            float shaftWidth = Mathf.Max(6f, shaftEndX - shaftStartX);
            float shaftCenterX = (shaftStartX + shaftEndX) / 2f;

            var shaftGo = new GameObject("RowArrowShaft" + i);
            shaftGo.transform.SetParent(tableGroupGo.transform, false);
            Image shaftImg = shaftGo.AddComponent<Image>();
            shaftImg.color = new Color(1f, 0.85f, 0.2f, 0.95f);
            RectTransform shaftRt = shaftImg.GetComponent<RectTransform>();
            shaftRt.anchorMin = new Vector2(0.5f, 0.5f);
            shaftRt.anchorMax = new Vector2(0.5f, 0.5f);
            shaftRt.pivot = new Vector2(0.5f, 0.5f);
            shaftRt.sizeDelta = new Vector2(shaftWidth, 8f);
            shaftRt.anchoredPosition = new Vector2(shaftCenterX, arrowY);

            var headGo = new GameObject("RowArrowHead" + i);
            headGo.transform.SetParent(tableGroupGo.transform, false);
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
            rowPhotoGo.transform.SetParent(tableGroupGo.transform, false);
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
        so.FindProperty("tableGroup").objectReferenceValue = tableGroupGo;
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
        question.font = GameFont;
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
        hint.font = GameFont;
        hint.fontSize = 24;
        hint.alignment = TextAnchor.MiddleCenter;
        hint.color = new Color(0.85f, 0.85f, 0.85f);
        hint.text = "←→ выбор, прыжок — подтвердить";
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
        text.font = GameFont;
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

    // Working titles for the 7 cabinet slots, shown on the loader's debug
    // line so it is obvious which key is which. Index-matched to
    // LoaderScreenController.gameStartKeys and to GameIntroThemes below.
    //
    // NOTE: only slot 1 exists. The other six are placeholders and every key
    // still starts БК regardless (see CreateLoaderScreen's own comment).
    static readonly string[] GameSlotTitles =
    {
        "1 - Lady Bug Hit The Road",
        "2 - Викторина про жизнь",
        "3 - Медитация в спешке",
        "4 - Бесконечный Сизиф",
        "5 - Завод",
        "6 - Таблетка в космосе",
        "7 - Игра про кота",
    };

    // One themed falling-object set per game slot on the loader screen
    // (LoaderScreenController's gameStartKeys 1-7, see plan items 9-11) —
    // index 0 is БК's own flowers (lady_bug's real sprites), 1-6 are the
    // other 6 mega-project games' prep artwork, generated via
    // asset_gen/gen_asset.sh straight into Assets/LadyBug/Sprites/loader/. All 7
    // currently still hand off into the SAME real game once their own
    // countdown finishes (see CreateLoaderScreen) — only game 1 actually
    // exists yet, per feedback that's fine/expected for now. isPrimaryGame
    // (true only for index 0) gates the fill-buzz sound and the menu music
    // cue — per feedback those are БК-specific content, not generic loader
    // chrome, so games 2-7 fill silently and don't trigger the menu music.
    // Order matches GameSlotTitles above, one row per key — what falls is what
    // the game is about. The art was originally wired in the order it happened
    // to be generated, which left e.g. cat paws falling for Сизиф and question
    // marks for Завод; reordered per feedback so each set lands on its own game.
    // fillClip is the looping bed that plays while the objects rain down,
    // trimmed and crossfaded to loop without a seam (WAV, not MP3: the codec
    // adds its own padding, which is audible as a hitch every time round).
    // growing louder as the screen fills — every slot now has its own instead
    // of only БК having one and the rest filling in silence. The cat slot
    // reuses the meow the game already ships for a Cat collision rather than
    // adding a second copy of the same mixkit sound.
    static readonly (string canvasName, string spriteFolder, string[] sprites, string fillClip, bool isPrimaryGame)[] GameIntroThemes =
    {
        // 1 - Lady Bug Hit The Road — жужжание
        ("IntroCanvas", "Assets/LadyBug/Sprites/lady_bug/", IntroFlowerSprites,
            "Assets/LadyBug/Audio/lady_bug/Buzz.wav", true),
        // 2 - Викторина про жизнь — тиканье часов
        ("IntroCanvasQuestionMark", "Assets/LadyBug/Sprites/loader/", new[] { "QuestionMark1.png", "QuestionMark2.png", "QuestionMark3.png" },
            "Assets/LadyBug/Audio/loader/IntroClock.wav", false),
        // 3 - Медитация в спешке — индийская флейта
        ("IntroCanvasMeditation", "Assets/LadyBug/Sprites/loader/", new[] { "Meditation1.png", "Meditation2.png", "Meditation3.png" },
            "Assets/LadyBug/Audio/loader/IntroIndianFlute.wav", false),
        // 4 - Бесконечный Сизиф — камни
        ("IntroCanvasStone", "Assets/LadyBug/Sprites/loader/", new[] { "Stone1.png", "Stone2.png", "Stone3.png" },
            "Assets/LadyBug/Audio/loader/IntroStones.wav", false),
        // 5 - Завод — технологический гул
        ("IntroCanvasGear", "Assets/LadyBug/Sprites/loader/", new[] { "Gear1.png", "Gear2.png", "Gear3.png" },
            "Assets/LadyBug/Audio/loader/IntroFactoryHum.wav", false),
        // 6 - Таблетка в космосе — космический эмбиент
        ("IntroCanvasPill", "Assets/LadyBug/Sprites/loader/", new[] { "Pill1.png", "Pill2.png", "Pill3.png" },
            "Assets/LadyBug/Audio/loader/IntroSpaceDrone.wav", false),
        // 7 - Игра про кота — мяуканье
        ("IntroCanvasCatPaw", "Assets/LadyBug/Sprites/loader/", new[] { "CatPaw1.png", "CatPaw2.png", "CatPaw3.png" },
            "Assets/LadyBug/Audio/lady_bug/BadCat.mp3", false),
    };

    static IntroSequence[] CreateAllIntroScreens()
    {
        var result = new IntroSequence[GameIntroThemes.Length];
        for (int i = 0; i < GameIntroThemes.Length; i++)
        {
            var theme = GameIntroThemes[i];
            result[i] = CreateIntroScreen(theme.canvasName, theme.spriteFolder, theme.sprites, theme.fillClip, theme.isPrimaryGame);
        }
        return result;
    }

    // Very first thing the player sees once they hold down that game's key
    // on the loader screen: themed objects (spriteFiles, from spriteFolder)
    // rain from the top and pile up until the whole screen is covered, then
    // the canvas hides itself, revealing the start menu that's been sitting
    // ready underneath. Highest sorting order of any canvas — has to cover
    // absolutely everything (all 7 of these instances share it — never
    // shown at once, see CreateAllIntroScreens/LoaderScreenController).
    static IntroSequence CreateIntroScreen(string canvasName, string spriteFolder, string[] spriteFiles, string fillClipPath, bool isPrimaryGame)
    {
        var canvasGo = new GameObject(canvasName);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 220;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // Lets IntroSequence fade the whole screen out smoothly at the very
        // end instead of canvasRoot just vanishing on the spot.
        CanvasGroup canvasGroup = canvasGo.AddComponent<CanvasGroup>();

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
                string spriteFile = spriteFiles[rng.Next(spriteFiles.Length)];
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(spriteFolder + spriteFile);

                var flowerGo = new GameObject("Obj_" + row + "_" + col);
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

        // Black square covering the whole canvas, LAST sibling so it draws
        // over the finished pile. Starts fully transparent; IntroSequence
        // fades it in to hand off to the game screen behind it. The
        // 5-4-3-2-1-СТАРТ graffiti overlay that used to sit here is gone, per
        // feedback — its six CountdownGraffiti*.png are now unreferenced.
        var darkenGo = new GameObject("DarkenOverlay");
        darkenGo.transform.SetParent(canvasGo.transform, false);
        Image darkenOverlay = darkenGo.AddComponent<Image>();
        darkenOverlay.color = new Color(0f, 0f, 0f, 0f);
        darkenOverlay.raycastTarget = false;
        RectTransform darkenRt = darkenOverlay.GetComponent<RectTransform>();
        darkenRt.anchorMin = Vector2.zero;
        darkenRt.anchorMax = Vector2.one;
        darkenRt.offsetMin = Vector2.zero;
        darkenRt.offsetMax = Vector2.zero;

        // The slot's own looping bed while its objects fill in, growing louder
        // with them (see IntroSequence). Every slot has one now — this used to
        // be БК's buzz only, gated on isPrimaryGame, so the other six filled in
        // silence. isPrimaryGame still gates the MENU music below, which really
        // is БК-specific. Does not autoplay: IntroSequence starts and fades it
        // on its own schedule rather than the instant the scene loads.
        var introGo = new GameObject(canvasName + "_Controller");
        AudioSource introBuzzSource = introGo.AddComponent<AudioSource>();
        introBuzzSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(fillClipPath);
        introBuzzSource.loop = true;
        introBuzzSource.playOnAwake = false;
        introBuzzSource.volume = 0f;
        if (introBuzzSource.clip == null)
            Debug.LogWarning($"IntroScreen {canvasName}: fill clip not found at {fillClipPath} — this slot will fill silently.");

        IntroSequence intro = introGo.AddComponent<IntroSequence>();
        SerializedObject introSo = new SerializedObject(intro);
        introSo.FindProperty("canvasRoot").objectReferenceValue = canvasGo;
        introSo.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        introSo.FindProperty("darkenOverlay").objectReferenceValue = darkenOverlay;
        introSo.FindProperty("buzzSource").objectReferenceValue = introBuzzSource;
        // Wired for every slot (not just isPrimaryGame) — Finish() always
        // calls startScreen.OnRevealed() to reset the menu's carousel back
        // to page 0 right as it becomes visible, regardless of which
        // slot's intro just finished. isPrimaryGame itself is also wired
        // here so Finish() can gate its PlayMusic() call to БК only.
        introSo.FindProperty("startScreen").objectReferenceValue = Object.FindAnyObjectByType<StartScreenController>();
        introSo.FindProperty("isPrimaryGame").boolValue = isPrimaryGame;
        SerializedProperty flowersProp = introSo.FindProperty("flowers");
        flowersProp.arraySize = orderedFlowers.Count;
        for (int i = 0; i < orderedFlowers.Count; i++)
            flowersProp.GetArrayElementAtIndex(i).objectReferenceValue = orderedFlowers[i];
        introSo.ApplyModifiedPropertiesWithoutUndo();

        // Starts hidden — LoaderScreenController.BeginConfirmHold reveals
        // it once a control is held (used to rely on IntroSequence's own
        // now-removed auto-start-or-skip Start() to hide this instantly;
        // nothing does that anymore, so it needs to start off explicitly).
        canvasGo.SetActive(false);

        return intro;
    }

    // The true first thing shown when the game boots (sortingOrder above
    // every other canvas, including IntroCanvas) — a plain arcade-cabinet
    // "attract mode" idle screen: gray backdrop, a schematic of the
    // physical control panel along the bottom (ControlPanelDiagram.png,
    // generated at 1600x420 — the Highlight() pixel coordinates below are
    // expressed in that same source space), and a prompt cycling through
    // what to try, synced to a highlighted control (LoaderScreenController
    // drives both). Holding a key hands off to IntroSequence; releasing
    // early aborts back here — see LoaderScreenController/IntroSequence.
    static void CreateLoaderScreen(IntroSequence[] gameIntros)
    {
        var canvasGo = new GameObject("LoaderCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 230;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var backdropGo = new GameObject("Backdrop");
        backdropGo.transform.SetParent(canvasGo.transform, false);
        Image backdrop = backdropGo.AddComponent<Image>();
        backdrop.color = new Color(0.32f, 0.32f, 0.34f);
        RectTransform backdropRt = backdropGo.GetComponent<RectTransform>();
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;

        Texture2D panelTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LadyBug/Sprites/loader/ControlPanelDiagram.png");
        var panelGo = new GameObject("PanelDiagram");
        panelGo.transform.SetParent(canvasGo.transform, false);
        RawImage panelImage = panelGo.AddComponent<RawImage>();
        panelImage.texture = panelTexture;
        RectTransform panelRt = panelImage.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0f);
        panelRt.anchorMax = new Vector2(0.5f, 0f);
        panelRt.pivot = new Vector2(0.5f, 0f);
        const float panelWidth = 1400f;
        const float panelHeight = panelWidth * 420f / 1600f; // preserve ControlPanelDiagram.png's own 1600x420 aspect ratio
        panelRt.sizeDelta = new Vector2(panelWidth, panelHeight);
        panelRt.anchoredPosition = new Vector2(0f, 60f);

        // Layout matches ControlPanelDiagram.png's own 1600x420 source
        // space: white button top-left above the 2 laser rangefinders
        // (датчики — long red beam + a short pale "palm" crossing it),
        // joystick top-middle with a red/green button pair below it, and a
        // rotating handle (рукоятка, a dial with a pointer) on the right —
        // NOT the wifi-style icon this used to be, per feedback that it
        // reads as a wireless icon rather than a physical twist-knob.
        GameObject buttonWhite = CreatePanelHighlight(panelGo.transform, "HighlightButtonWhite", panelWidth, panelHeight, 290f, 110f, 150f, 150f);
        GameObject rangefinder1 = CreatePanelHighlight(panelGo.transform, "HighlightRangefinder1", panelWidth, panelHeight, 200f, 300f, 130f, 180f);
        GameObject rangefinder2 = CreatePanelHighlight(panelGo.transform, "HighlightRangefinder2", panelWidth, panelHeight, 380f, 300f, 130f, 180f);
        GameObject joystick = CreatePanelHighlight(panelGo.transform, "HighlightJoystick", panelWidth, panelHeight, 760f, 150f, 170f, 220f);
        GameObject buttonRed = CreatePanelHighlight(panelGo.transform, "HighlightButtonRed", panelWidth, panelHeight, 680f, 330f, 150f, 150f);
        GameObject buttonGreen = CreatePanelHighlight(panelGo.transform, "HighlightButtonGreen", panelWidth, panelHeight, 860f, 330f, 150f, 150f);
        GameObject rotaryHandle = CreatePanelHighlight(panelGo.transform, "HighlightRotaryHandle", panelWidth, panelHeight, 1380f, 260f, 170f, 170f);

        var messageGo = new GameObject("MessageText");
        messageGo.transform.SetParent(canvasGo.transform, false);
        Text messageText = messageGo.AddComponent<Text>();
        messageText.font = GameFont;
        messageText.fontSize = 64;
        messageText.fontStyle = FontStyle.Bold;
        messageText.alignment = TextAnchor.MiddleCenter;
        messageText.color = Color.white;
        messageText.text = string.Empty;
        Outline messageOutline = messageGo.AddComponent<Outline>();
        messageOutline.effectColor = Color.black;
        messageOutline.effectDistance = new Vector2(3f, -3f);
        RectTransform messageRt = messageGo.GetComponent<RectTransform>();
        messageRt.anchorMin = new Vector2(0.5f, 1f);
        messageRt.anchorMax = new Vector2(0.5f, 1f);
        messageRt.pivot = new Vector2(0.5f, 0.5f);
        messageRt.sizeDelta = new Vector2(1700f, 100f);
        messageRt.anchoredPosition = new Vector2(0f, -260f);

        // Static, always on screen (unlike the cycling prompt above it) —
        // the hold duration itself doesn't change per-prompt, so it doesn't
        // need to slide in and out with each one.
        var holdHintGo = new GameObject("HoldHintText");
        holdHintGo.transform.SetParent(canvasGo.transform, false);
        Text holdHintText = holdHintGo.AddComponent<Text>();
        holdHintText.font = GameFont;
        holdHintText.fontSize = 34;
        holdHintText.fontStyle = FontStyle.Bold;
        holdHintText.alignment = TextAnchor.MiddleCenter;
        holdHintText.color = new Color(0.85f, 0.85f, 0.85f);
        holdHintText.text = "И НЕ ОТПУСКАЙТЕ 5 СЕК";
        Outline holdHintOutline = holdHintGo.AddComponent<Outline>();
        holdHintOutline.effectColor = Color.black;
        holdHintOutline.effectDistance = new Vector2(2f, -2f);
        RectTransform holdHintRt = holdHintText.GetComponent<RectTransform>();
        holdHintRt.anchorMin = new Vector2(0.5f, 1f);
        holdHintRt.anchorMax = new Vector2(0.5f, 1f);
        holdHintRt.pivot = new Vector2(0.5f, 0.5f);
        holdHintRt.sizeDelta = new Vector2(1000f, 60f);
        holdHintRt.anchoredPosition = new Vector2(0f, -340f);

        // TEMPORARY — real cabinets get a real controller (see class
        // comment on LoaderScreenController); until then this spells out
        // the debug stand-in (number keys 1-7) directly, since nothing
        // else on this screen otherwise explains it.
        var debugHintGo = new GameObject("DebugKeysHintText");
        debugHintGo.transform.SetParent(canvasGo.transform, false);
        Text debugHintText = debugHintGo.AddComponent<Text>();
        debugHintText.font = GameFont;
        debugHintText.fontSize = 26;
        debugHintText.fontStyle = FontStyle.Bold;
        debugHintText.alignment = TextAnchor.MiddleCenter;
        debugHintText.color = new Color(1f, 0.7f, 0.3f);
        debugHintText.text = "ВРЕМЕННО ДЛЯ ОТЛАДКИ - НАЖИМАЙТЕ ЦИФРЫ 1..7 ДЛЯ ЗАПУСКА ИГР";
        // Index-matched to LoaderScreenController.gameStartKeys, i.e. this is
        // the key you press, not just a list. Only slot 1 is a real game; the
        // other six are titles for planned ones and all currently hand off
        // into БК anyway (see GameIntroThemes).
        debugHintText.text += "\n\n" + string.Join("\n", GameSlotTitles);
        Outline debugHintOutline = debugHintGo.AddComponent<Outline>();
        debugHintOutline.effectColor = Color.black;
        debugHintOutline.effectDistance = new Vector2(2f, -2f);
        debugHintText.horizontalOverflow = HorizontalWrapMode.Overflow;
        debugHintText.verticalOverflow = VerticalWrapMode.Overflow; // 9 lines in a box sized for one — Truncate would blank the lot (see CreateJoystickHudArrow)
        RectTransform debugHintRt = debugHintText.GetComponent<RectTransform>();
        debugHintRt.anchorMin = new Vector2(0.5f, 1f);
        debugHintRt.anchorMax = new Vector2(0.5f, 1f);
        debugHintRt.pivot = new Vector2(0.5f, 0.5f);
        debugHintRt.sizeDelta = new Vector2(1400f, 60f);
        debugHintRt.anchoredPosition = new Vector2(0f, -480f);

        var loaderManagerGo = new GameObject("LoaderScreenManager");
        LoaderScreenController loader = loaderManagerGo.AddComponent<LoaderScreenController>();
        SerializedObject loaderSo = new SerializedObject(loader);
        loaderSo.FindProperty("canvasRoot").objectReferenceValue = canvasGo;
        loaderSo.FindProperty("messageText").objectReferenceValue = messageText;

        // Index-matched to LoaderScreenController's own gameStartKeys (keys
        // 1-7) — all 7 now have their own themed falling-object screen (see
        // CreateAllIntroScreens/GameIntroThemes), even though only game 1
        // (БК/lady_bug) is a real playable game yet — pressing 2-7 still
        // shows that game's own art, then hands off into БК regardless
        // (IntroSequence.Finish always reveals the one real menu), per
        // feedback that this is fine/expected for debugging until games
        // 2-7 actually exist.
        SerializedProperty gameIntrosProp = loaderSo.FindProperty("gameIntros");
        gameIntrosProp.arraySize = gameIntros.Length;
        for (int i = 0; i < gameIntros.Length; i++)
            gameIntrosProp.GetArrayElementAtIndex(i).objectReferenceValue = gameIntros[i];

        SerializedProperty buttonProp = loaderSo.FindProperty("buttonHighlights");
        buttonProp.arraySize = 3;
        buttonProp.GetArrayElementAtIndex(0).objectReferenceValue = buttonWhite;
        buttonProp.GetArrayElementAtIndex(1).objectReferenceValue = buttonRed;
        buttonProp.GetArrayElementAtIndex(2).objectReferenceValue = buttonGreen;

        // "Датчик" means the 2 laser rangefinders now, not the rotary
        // handle — see the panel layout comment above.
        SerializedProperty sensorProp = loaderSo.FindProperty("sensorHighlights");
        sensorProp.arraySize = 2;
        sensorProp.GetArrayElementAtIndex(0).objectReferenceValue = rangefinder1;
        sensorProp.GetArrayElementAtIndex(1).objectReferenceValue = rangefinder2;

        SerializedProperty joystickProp = loaderSo.FindProperty("joystickHighlights");
        joystickProp.arraySize = 1;
        joystickProp.GetArrayElementAtIndex(0).objectReferenceValue = joystick;

        // "Рукоятка" is the one rotary dial now, not the rangefinders.
        SerializedProperty knobProp = loaderSo.FindProperty("knobHighlights");
        knobProp.arraySize = 1;
        knobProp.GetArrayElementAtIndex(0).objectReferenceValue = rotaryHandle;

        loaderSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // One static highlight box, hidden by default — px/py/pw/ph are in
    // ControlPanelDiagram.png's own 1600x420 source-pixel space (top-left
    // origin), converted here to an anchoredPosition/sizeDelta against the
    // panel RawImage's actual on-screen size so the box lines up with the
    // real icon regardless of what panelWidth/panelHeight are chosen to be.
    static GameObject CreatePanelHighlight(Transform parent, string name, float panelWidth, float panelHeight, float px, float py, float pw, float ph)
    {
        float anchoredX = (px / 1600f - 0.5f) * panelWidth;
        float anchoredY = (0.5f - py / 420f) * panelHeight;
        float w = pw / 1600f * panelWidth;
        float h = ph / 420f * panelHeight;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(anchoredX, anchoredY);

        Color frameColor = new Color(1f, 0.85f, 0.15f, 0.95f);
        const float thickness = 6f;
        CreateHighlightBar(rt, "Top", frameColor, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero);
        CreateHighlightBar(rt, "Bottom", frameColor, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness));
        CreateHighlightBar(rt, "Left", frameColor, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f));
        CreateHighlightBar(rt, "Right", frameColor, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), Vector2.zero);

        go.SetActive(false);
        return go;
    }

    // One solid-color bar of a highlight frame's rectangular outline — see
    // CreatePanelHighlight. Plain Image rects rather than a 9-sliced sprite
    // so no separate texture-import configuration is needed.
    static void CreateHighlightBar(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var barGo = new GameObject(name);
        barGo.transform.SetParent(parent, false);
        Image img = barGo.AddComponent<Image>();
        img.color = color;
        RectTransform rt = img.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
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
        text.font = GameFont;
        text.fontSize = 18;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.LowerLeft;
        text.color = new Color(1f, 1f, 1f, 0.6f);
        RectTransform rt = text.GetComponent<RectTransform>();
        // Bottom corner now (was top-left) — out of the way of the new
        // corner HUD fans up top.
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(300f, 30f);
        rt.anchoredPosition = new Vector2(6f, 6f);

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
        text.font = GameFont;
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

    // NOT scratch — these PNGs are a committed build input. The scene stores
    // only GUIDs pointing at them, so a clone without this folder renders every
    // road surface, lane divider, HUD wedge, route diagram, arrowhead and
    // placeholder as a blank white box. Commit it together with Main.unity.
    const string GeneratedTextureDir = "Assets/LadyBug/Sprites/lady_bug/generated";

    // Every procedurally drawn texture below MUST come back through here.
    //
    // Handing a bare `new Texture2D(...)` to a material or a RawImage leaves
    // it with no asset backing it, so Unity has nowhere to put the pixels
    // except inside whatever references it — the scene. That is how
    // Main.unity reached 48 MB, of which 45 MB was raw RGBA serialized as
    // hex text: a single 900x900 route diagram cost 6.18 MB despite being
    // 98.6% transparent (11384 opaque pixels out of 810000). Written out as
    // a PNG first, that same diagram is 12.7 KB and the scene stores nothing
    // but a GUID — the whole set of 34 textures goes from 45 MB to ~115 KB.
    // Same class of mistake as ApplyColor vs ApplyPersistentColor further
    // down, just for textures instead of materials.
    //
    // The file is named by content hash, which buys two things: identical
    // textures (8 identical no-photo placeholders, 9 identical arrowheads)
    // collapse onto one asset, and a rebuild that changes nothing produces
    // byte-identical filenames, so Rebuild Scene stays free of git churn.
    static Texture2D SaveGeneratedTexture(Texture2D tex, string prefix, TextureWrapMode wrap)
    {
        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        string hash;
        using (var md5 = System.Security.Cryptography.MD5.Create())
            hash = System.BitConverter.ToString(md5.ComputeHash(png)).Replace("-", "").Substring(0, 8).ToLowerInvariant();

        System.IO.Directory.CreateDirectory(GeneratedTextureDir);
        string path = GeneratedTextureDir + "/" + prefix + "_" + hash + ".png";
        _generatedThisRun.Add(path);

        // Identical content -> identical name -> nothing to write. Skipping the
        // write is what keeps the GUID (and the git diff) stable across rebuilds.
        if (!System.IO.File.Exists(path))
        {
            System.IO.File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            Debug.LogError("SaveGeneratedTexture: не удалось импортировать " + path);
            return null;
        }

        // Checked every run, not just on first write. The import above lands
        // with Unity's DEFAULTS first and is corrected here — so if that
        // correction ever fails to stick (crash, aborted build, a .meta lost
        // or regenerated on another machine), the PNG would otherwise keep
        // the defaults forever, since the file now exists and the write is
        // skipped. The costly default is npotScale: Unity rescales
        // non-power-of-two textures to the nearest power of two, and most of
        // these are 900x900 / 512x727 / 400x569. A squashed 512x727 -> 512x512
        // wedge silently feeds a wrong height/width ratio into
        // PositionWedgePanel and skews the whole HUD panel with no error.
        bool dirty =
            importer.npotScale != TextureImporterNPOTScale.None ||
            importer.mipmapEnabled ||
            importer.textureCompression != TextureImporterCompression.Uncompressed || // was RGBA32; DXT would band the flat fills
            importer.maxTextureSize != 2048 ||
            importer.filterMode != FilterMode.Bilinear ||
            importer.wrapMode != wrap ||
            !importer.alphaIsTransparency;

        if (dirty)
        {
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = wrap;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (loaded == null)
            Debug.LogError("SaveGeneratedTexture: ассет не загрузился после импорта — " + path);
        return loaded;
    }

    // Paths written by the current Rebuild Scene run — see PruneGeneratedTextures.
    static readonly System.Collections.Generic.HashSet<string> _generatedThisRun =
        new System.Collections.Generic.HashSet<string>();

    // Content-hashed names never collide, but a changed diagram abandons its
    // old file, so the folder would silently accumulate orphans.
    //
    // Deliberately a prune at the END rather than a wipe at the start: a file
    // whose content did not change keeps its name, therefore its .meta,
    // therefore its GUID. Wiping the folder up front would hand every texture
    // a brand-new GUID on every rebuild, which both breaks the references
    // inside any material/prefab asset that outlived the rebuild and turns a
    // no-op Rebuild Scene into a churning diff. This way a rebuild that
    // changes nothing writes nothing.
    //
    // Only safe to call from BuildScene, which regenerates EVERY texture.
    // Rebuild Road Geometry deliberately does NOT prune: it rebuilds only the
    // road, so _generatedThisRun would hold just the asphalt/dash textures and
    // the prune would delete the HUD wedges, route diagrams and smileys the
    // still-loaded scene is using. Cost of not pruning there: editing
    // road-texture code and running only Rebuild Road Geometry leaves the
    // superseded PNGs behind until the next full Rebuild Scene sweeps them.
    static void PruneGeneratedTextures()
    {
        if (!AssetDatabase.IsValidFolder(GeneratedTextureDir))
            return;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { GeneratedTextureDir });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!_generatedThisRun.Contains(path))
                AssetDatabase.DeleteAsset(path);
        }
    }

    // Tileable asphalt grain — per-pixel brightness jitter around the base
    // road-gray so the surface reads as slightly uneven instead of a flat
    // block of color. Fixed seed so re-running Rebuild Scene is deterministic.
    static Texture2D CreateRoadTexture()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat; // carried onto the importer by SaveGeneratedTexture
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
        return SaveGeneratedTexture(tex, "RoadGrain", TextureWrapMode.Repeat);
    }

    // A single 90°-wide corner fan — apex at the left edge (opensRight) or
    // right edge (!opensRight) — covering both of a corner's real wedge
    // slices (ДИСТАНЦИЯ+ВРЕМЯ or ОЧКИ+ТРЮКИ) in one shape, split into its
    // own 2 sectors by a thin divider line at the midpoint, with a solid
    // color border band around the whole outer boundary (both straight
    // edges + the outer arc). Computed pixel-by-pixel here at scene
    // rebuild time — per feedback that this should go back to plain
    // primitives, not a generated/baked image (an earlier pass tried
    // AI-generated art for this, both as a sampled texture and as a single
    // fully-generated picture; reverted per feedback). The digits/labels
    // and SpeedIndicator's green-red tick fill are the only things that
    // stay programmatic on top, same as before, sitting over the flat
    // interior fill.
    // withBorder/withDivider default to the plain corner-sector look (no
    // border stripe — removed everywhere per feedback that it made the
    // sectors read as too busy — but with the mid-sector divider line,
    // used to mark the seam between the 2 real content slices sharing one
    // panel). The central gear-hub badge is the one shape that still wants
    // a border (see its own call site) but not a divider (it's a single
    // unified pie, not 2 slices sharing one texture).
    static Texture2D CreateWedgeTexture(int width, float angleWidthDeg, bool opensRight, bool withBorder = false, bool withDivider = true)
    {
        float halfAngle = angleWidthDeg * 0.5f * Mathf.Deg2Rad;
        // Taller than it is wide, not square — a wide sector (e.g. this
        // panel's full 90°) reaches much further vertically from the apex
        // at its outer edge than a square canvas has room for, which used
        // to clip the sector's top/bottom corners off the canvas entirely
        // instead of drawing the full pie slice.
        int height = Mathf.CeilToInt(2f * (width - 1) * Mathf.Sin(halfAngle)) + 4;

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        const float dividerHalfWidth = 0.012f; // radians either side of the sector midpoint that gets the thin divider line
        // A constant ANGULAR margin here used to mean the border band got
        // physically wider (in pixels) further from the apex — and, more to
        // the point, it meant the fill's own inner boundary sat at a
        // narrower half-angle than the sector's true edge (e.g. 37° instead
        // of 45°), so the fill's own "sides" read as tilted relative to the
        // sector's real (screen-aligned) sides instead of running parallel
        // to them. Measuring perpendicular pixel distance to the nearest
        // straight edge instead (distToEdge below) gives a constant-width
        // stroke whose inner edge is a true parallel offset of the outer
        // one, same as any normal border/stroke.
        float borderWidthPx = width * 0.07f;
        float borderRadialWidth = width * 0.09f; // pixels in from the outer arc that get the same border color
        float apexX = opensRight ? 0f : width - 1;
        float apexY = (height - 1) / 2f;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color fill = new Color(0.08f, 0.14f, 0.18f, 0.55f);
        Color divider = new Color(1f, 1f, 1f, 0.4f);
        Color border = new Color(1f, 0.75f, 0.2f, 0.9f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = opensRight ? (x - apexX) : (apexX - x);
                float dy = y - apexY;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dx < 0f || dist > width - 1)
                {
                    tex.SetPixel(x, y, clear);
                    continue;
                }

                float angle = Mathf.Atan2(dy, Mathf.Max(dx, 0.0001f));
                float absAngle = Mathf.Abs(angle);
                if (absAngle > halfAngle)
                {
                    tex.SetPixel(x, y, clear);
                }
                else
                {
                    float distToEdge = dist * Mathf.Sin(halfAngle - absAngle);
                    if (withBorder && (distToEdge < borderWidthPx || dist > width - 1 - borderRadialWidth))
                    {
                        tex.SetPixel(x, y, border);
                    }
                    else if (withDivider && absAngle < dividerHalfWidth && dist > width * 0.12f)
                    {
                        tex.SetPixel(x, y, divider);
                    }
                    else
                    {
                        tex.SetPixel(x, y, fill);
                    }
                }
            }
        }

        tex.Apply();
        return SaveGeneratedTexture(tex, "Wedge", TextureWrapMode.Clamp);
    }

    // Positions/rotates a HUD fan panel's wedge background so its apex
    // sits at the screen corner — angleDeg (the slice's own center, 0° =
    // along the top edge, 90° = straight down the side edge) and radius
    // work the same as the plain-rectangle fan this replaced, but pivot is
    // now the apex itself (0,0.5) or (1,0.5) instead of the shape's center,
    // since sizeDelta now spans the whole wedge (apex to outer edge)
    // rather than a small floating box.
    // heightScale stretches sizeDelta's height relative to its width — 1
    // (square) for the many backgroundless anchor-only panels here (their
    // sizeDelta doesn't actually render anything, content positions itself
    // via CreateWedgeContent's own anchoredPosition regardless), but the two
    // real wedge-texture panels pass their texture's actual height/width
    // aspect so the non-square canvas from CreateWedgeTexture (tall enough
    // for a full 90° sector, see its own comment) isn't squashed back into
    // a square display rect.
    static void PositionWedgePanel(RectTransform rt, bool rightCorner, float angleDeg, float radius, float heightScale = 1f)
    {
        rt.anchorMin = new Vector2(rightCorner ? 1f : 0f, 1f);
        rt.anchorMax = new Vector2(rightCorner ? 1f : 0f, 1f);
        rt.pivot = new Vector2(rightCorner ? 1f : 0f, 0.5f);
        rt.sizeDelta = new Vector2(radius, radius * heightScale);
        rt.anchoredPosition = new Vector2(rightCorner ? -35f : 35f, -35f); // 30 * 1.15 — corner inset scaled up along with the rest of the fan
        // Sign flips with the corner — the wedge texture's own "opens
        // toward screen center" direction mirrors between corners, so the
        // SAME physical sweep (top edge -> down the side edge as angleDeg
        // grows) needs the opposite rotation sign on the right to match.
        rt.localEulerAngles = new Vector3(0f, 0f, rightCorner ? angleDeg : -angleDeg);
    }

    // A wedge panel's content (label/value text, or a single tick dot)
    // can't just fill the whole square background rect anymore — most of
    // that square is transparent (outside the actual slice). This adds a
    // plain child rect, anchored at the same apex point as the wedge
    // itself, offset outward by contentRadius along a direction
    // localAngleDeg off the wedge's own centerline (0 = straight down the
    // centerline, same as every panel's label/value text; nonzero = used
    // for placing several small elements — e.g. SpeedIndicator's ring of
    // tick dots — spread across the slice's width instead of stacked on
    // one point). Positive localAngleDeg is mirrored for the right corner
    // along with everything else here, so a given angle always reads as
    // "toward the same physical side" regardless of which corner it's in.
    // Counter-rotates against the wedge panel's own rotation (PositionWedgePanel
    // tilts the whole wedge to sweep across its corner) so content always
    // ends up axis-aligned on screen — per feedback that label/value text
    // should stay flat/horizontal and readable, not tilted to match its
    // wedge's angle. Harmless for the rotation-symmetric content (the round
    // badge, the square tick dots) that also goes through here.
    static RectTransform CreateWedgeContent(Transform wedgeParent, bool rightCorner, float localAngleDeg, float contentRadius, float width, float height)
    {
        var go = new GameObject("Content");
        go.transform.SetParent(wedgeParent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(rightCorner ? 1f : 0f, 0.5f);
        rt.anchorMax = new Vector2(rightCorner ? 1f : 0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        float rad = localAngleDeg * Mathf.Deg2Rad;
        float sign = rightCorner ? -1f : 1f;
        rt.anchoredPosition = new Vector2(sign * contentRadius * Mathf.Cos(rad), contentRadius * Mathf.Sin(rad));
        RectTransform parentRt = wedgeParent.GetComponent<RectTransform>();
        float parentZ = parentRt != null ? parentRt.localEulerAngles.z : 0f;
        rt.localEulerAngles = new Vector3(0f, 0f, -parentZ);
        return rt;
    }

    // Nudges a CreateWedgeContent result by a small delta in real screen
    // directions (e.g. "a bit lower/righter") regardless of the wedge
    // panel's own tilt — anchoredPosition is measured in the panel's own
    // rotated local space, so a plain += on it would drift off at whatever
    // angle that panel happens to be tilted to instead of moving straight
    // down/right on screen. Rotates screenDelta back into the panel's local
    // space first (the inverse of the rotation CreateWedgeContent's own
    // position math implicitly gets from being that panel's child).
    static void NudgeContentScreenSpace(RectTransform contentRt, Transform wedgeParent, Vector2 screenDelta)
    {
        RectTransform parentRt = wedgeParent.GetComponent<RectTransform>();
        float parentZ = parentRt != null ? parentRt.localEulerAngles.z : 0f;
        Vector3 localDelta = Quaternion.Euler(0f, 0f, -parentZ) * (Vector3)screenDelta;
        contentRt.anchoredPosition += (Vector2)localDelta;
    }

    // A row of small square dots along a shared radius (via CreateWedgeContent),
    // evenly spread across ±halfSpanDeg from the hub's own centerline — the
    // constant radius is what makes them read as a curved arc/dial rather
    // than a scatter. Used both for SpeedIndicator's live gauge and its
    // empty counterpart on the other corner — callers wire up colors
    // themselves (or leave them at this dim default for the empty one).
    static Image[] CreateTickRing(Transform wedgeParent, bool rightCorner, int count, float radius, float halfSpanDeg)
    {
        var ticks = new Image[count];
        for (int i = 0; i < count; i++)
        {
            float angle = count > 1 ? Mathf.Lerp(-halfSpanDeg, halfSpanDeg, (float)i / (count - 1)) : 0f;
            // Thin radial bar, not a square dot — narrow width (was 33x33)
            // with an extra rotation below so its long edge always points
            // toward the wedge's own apex, like a clock's minute marks.
            RectTransform tickContentRt = CreateWedgeContent(wedgeParent, rightCorner, angle, radius, 10f, 34f);
            var tickGo = new GameObject("Tick" + i);
            tickGo.transform.SetParent(tickContentRt, false);
            Image tick = tickGo.AddComponent<Image>();
            tick.color = new Color(1f, 1f, 1f, 0.15f);
            RectTransform tickRt = tick.GetComponent<RectTransform>();
            tickRt.anchorMin = Vector2.zero;
            tickRt.anchorMax = Vector2.one;
            tickRt.offsetMin = Vector2.zero;
            tickRt.offsetMax = Vector2.zero;

            // tickContentRt already counter-rotates against the wedge's own
            // tilt (see CreateWedgeContent) so content sits screen-upright
            // by default — this adds an extra rotation on top of that so
            // each tick instead points radially, parallel to the actual
            // on-screen ray from the wedge's apex to itself. That ray's
            // local direction (sign*cos(rad), sin(rad)) still needs the
            // wedge panel's own on-screen rotation applied back on top of
            // it — tickContentRt's position offset gets that rotation for
            // free as a side effect of being the wedge panel's child (see
            // CreateWedgeContent), but a tick's own ROTATION doesn't
            // inherit it the same way, since its immediate parent
            // (tickContentRt) sits at net-zero screen rotation.
            float rad = angle * Mathf.Deg2Rad;
            float sign = rightCorner ? -1f : 1f;
            Vector3 localDir = new Vector3(sign * Mathf.Cos(rad), Mathf.Sin(rad), 0f);
            RectTransform wedgeParentRt = wedgeParent.GetComponent<RectTransform>();
            float parentZ = wedgeParentRt != null ? wedgeParentRt.localEulerAngles.z : 0f;
            Vector3 screenDir = Quaternion.Euler(0f, 0f, parentZ) * localDir;
            float screenAngleDeg = Mathf.Atan2(screenDir.y, screenDir.x) * Mathf.Rad2Deg;
            tickRt.localEulerAngles = new Vector3(0f, 0f, screenAngleDeg - 90f);

            ticks[i] = tick;
        }
        return ticks;
    }

    // A thin curved guide line bracketing one side (inner or outer radius)
    // of the speed tick row — approximated as many short, closely-packed
    // segments, each individually rotated tangent to the circle at that
    // point (same per-segment rotation trick CreateTickRing uses, just
    // tangent instead of radial), so it reads as a smooth arc rather than
    // straight facets.
    static void CreateArcGuide(Transform wedgeParent, bool rightCorner, float radius, float halfSpanDeg, Color color)
    {
        const int segments = 40;
        const float segLength = 10f;
        const float thickness = 3f;

        RectTransform wedgeParentRt = wedgeParent.GetComponent<RectTransform>();
        float parentZ = wedgeParentRt != null ? wedgeParentRt.localEulerAngles.z : 0f;
        float sign = rightCorner ? -1f : 1f;

        for (int i = 0; i < segments; i++)
        {
            float angle = segments > 1 ? Mathf.Lerp(-halfSpanDeg, halfSpanDeg, (float)i / (segments - 1)) : 0f;
            RectTransform segContentRt = CreateWedgeContent(wedgeParent, rightCorner, angle, radius, thickness, segLength);
            var segGo = new GameObject("ArcSeg" + i);
            segGo.transform.SetParent(segContentRt, false);
            Image seg = segGo.AddComponent<Image>();
            seg.color = color;
            RectTransform segRt = seg.GetComponent<RectTransform>();
            segRt.anchorMin = Vector2.zero;
            segRt.anchorMax = Vector2.one;
            segRt.offsetMin = Vector2.zero;
            segRt.offsetMax = Vector2.zero;

            float rad = angle * Mathf.Deg2Rad;
            Vector3 localDir = new Vector3(sign * Mathf.Cos(rad), Mathf.Sin(rad), 0f);
            Vector3 screenDir = Quaternion.Euler(0f, 0f, parentZ) * localDir;
            float screenAngleDeg = Mathf.Atan2(screenDir.y, screenDir.x) * Mathf.Rad2Deg;
            segRt.localEulerAngles = new Vector3(0f, 0f, screenAngleDeg); // tangent — no -90 offset, unlike the radially-aligned ticks
        }
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
        return SaveGeneratedTexture(tex, "Arrowhead", TextureWrapMode.Clamp);
    }

    static Texture2D CreateDashTexture(int seedOffset = 0)
    {
        // Wider and taller than a flat 1x8 (dash = solid white, gap = solid
        // road-gray, razor-straight cut between them) — the extra
        // resolution leaves room for real paint grain and a ragged, worn
        // edge on the stripe instead of a perfectly clean rectangle, per
        // feedback that the lane markings read as too flat/plastic. Not
        // tiled across its width (mainTextureScale.x stays 1, see
        // CreateDashedDivider — the whole width is just stretched once
        // across the divider), so nothing here needs to line up edge-to-
        // edge horizontally; only top-to-bottom (the repeating dash/gap
        // cycle) has to.
        //
        // DashVariantCount distinctly-styled bands (clean edge, heavily
        // chipped, wavy worn edge) are stacked vertically into one texture
        // — CreateDashedDivider tiles this so a full texture repeat spans
        // DashVariantCount dash+gap cycles, so consecutive dashes along the
        // road cycle through the styles instead of every dash being an
        // identical copy of the same one (per feedback: "add randomness —
        // make several types of distortion and alternate them").
        const int width = 16;
        const int bandHeight = 32;
        var tex = new Texture2D(width, bandHeight * DashVariantCount, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        // seedOffset varies the actual RNG seeds (not just re-seeding with
        // the same numbers) so different callers — e.g. CreateDashedDivider,
        // once per lane divider — get genuinely different chip/jitter
        // patterns instead of every divider line rendering pixel-identical
        // to every other one, per feedback that both lines currently look
        // like the same style.
        DrawDashBand(tex, 0 * bandHeight, width, bandHeight, seed: 777 + seedOffset, edgeJitter: 3, chipChance: 0.05, wavy: false);
        DrawDashBand(tex, 1 * bandHeight, width, bandHeight, seed: 991 + seedOffset, edgeJitter: 6, chipChance: 0.12, wavy: false);
        DrawDashBand(tex, 2 * bandHeight, width, bandHeight, seed: 313 + seedOffset, edgeJitter: 2, chipChance: 0.08, wavy: true);

        tex.Apply();
        return SaveGeneratedTexture(tex, "LaneDash", TextureWrapMode.Repeat);
    }

    // One dash+gap cycle, written into tex at row yOffset..yOffset+height.
    // edgeJitter/chipChance/wavy vary per band so each style genuinely
    // looks different rather than just reseeding the same noise pattern —
    // wavy adds a smooth sine undulation to the dash's leading edge on top
    // of (a smaller amount of) the same per-column random jitter the other
    // bands use, reading as a distinct "worn groove" rather than more chips.
    static void DrawDashBand(Texture2D tex, int yOffset, int width, int height, int seed, int edgeJitter, double chipChance, bool wavy)
    {
        var rng = new System.Random(seed);
        Color gap = new Color(0.25f, 0.25f, 0.25f); // matches CreateRoadTexture's own base gray, so the gap blends into the asphalt instead of reading as a flat block
        Color paint = new Color(0.92f, 0.92f, 0.88f);
        int baseDashEnd = Mathf.RoundToInt(height * 0.625f); // same 5:3 dash:gap ratio the old 8px version used

        // Per-column dash length varies a little — the stripe's leading
        // edge reads as chipped/worn instead of a perfectly straight cut.
        var dashEndPerColumn = new int[width];
        for (int x = 0; x < width; x++)
        {
            int wave = wavy ? Mathf.RoundToInt(Mathf.Sin(x / (float)width * Mathf.PI * 2f) * 4f) : 0;
            dashEndPerColumn[x] = Mathf.Clamp(baseDashEnd + wave + rng.Next(-edgeJitter, edgeJitter + 1), 1, height - 1);
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                bool isDash = y < dashEndPerColumn[x];
                // A handful of small flecks worn through to bare road here
                // and there inside the dash itself, not just at its edge.
                if (isDash && rng.NextDouble() < chipChance)
                    isDash = false;

                Color baseColor = isDash ? paint : gap;
                float jitter = ((float)rng.NextDouble() - 0.5f) * (isDash ? 0.1f : 0.12f);
                Color c = new Color(
                    Mathf.Clamp01(baseColor.r + jitter),
                    Mathf.Clamp01(baseColor.g + jitter),
                    Mathf.Clamp01(baseColor.b + jitter));
                tex.SetPixel(x, yOffset + y, c);
            }
        }
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
        return SaveGeneratedTexture(tex, "RouteDiagram", TextureWrapMode.Clamp);
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
        // The Sprite itself stays an in-scene object — that part is cheap,
        // it is just a rect plus a reference. What must NOT stay in-scene is
        // the texture behind it, hence the round-trip through
        // SaveGeneratedTexture before Sprite.Create sees it.
        Texture2D saved = SaveGeneratedTexture(tex, "SoftRect", TextureWrapMode.Clamp);
        return Sprite.Create(saved, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(borderPx, borderPx, borderPx, borderPx));
    }

    // Round happy/sad face for ObjectFeedbackIndicator — drawn pixels, not
    // a text glyph (ComicCAT lacks reliable emoji coverage in builds).
    static Texture2D CreateSmileyTexture(int size, bool happy)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color[size * size];
        float cx = size * 0.5f;
        float cy = size * 0.5f;
        float radius = size * 0.46f;
        Color faceColor = happy ? new Color(1f, 0.88f, 0.15f) : new Color(0.55f, 0.68f, 0.92f);
        Color featureColor = new Color(0.12f, 0.08f, 0.05f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                pixels[y * size + x] = dx * dx + dy * dy <= radius * radius ? faceColor : Color.clear;
            }
        }

        float eyeR = radius * 0.11f;
        StampFilledCircle(pixels, size, new Vector2(cx - radius * 0.32f, cy + radius * 0.18f), eyeR, featureColor);
        StampFilledCircle(pixels, size, new Vector2(cx + radius * 0.32f, cy + radius * 0.18f), eyeR, featureColor);

        // Sad arc bulges upward (inverted smile), so its center must sit
        // lower than the happy mouth — otherwise the frown peak hits the eyes.
        float mouthY = cy - radius * (happy ? 0.12f : 0.40f);
        float mouthRadius = radius * (happy ? 0.42f : 0.38f);
        float mouthHalfThick = radius * 0.07f;
        int arcSteps = 24;
        for (int i = 0; i < arcSteps; i++)
        {
            float t0 = (float)i / arcSteps;
            float t1 = (float)(i + 1) / arcSteps;
            float a0 = Mathf.Lerp(200f, 340f, t0) * Mathf.Deg2Rad;
            float a1 = Mathf.Lerp(200f, 340f, t1) * Mathf.Deg2Rad;
            Vector2 p0 = new Vector2(cx + Mathf.Cos(a0) * mouthRadius, mouthY + Mathf.Sin(a0) * mouthRadius * (happy ? 1f : -1f));
            Vector2 p1 = new Vector2(cx + Mathf.Cos(a1) * mouthRadius, mouthY + Mathf.Sin(a1) * mouthRadius * (happy ? 1f : -1f));
            StampSolidLine(pixels, size, p0, p1, mouthHalfThick, featureColor);
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return SaveGeneratedTexture(tex, "Smiley", TextureWrapMode.Clamp);
    }

    static void StampFilledCircle(Color[] pixels, int texSize, Vector2 center, float radius, Color color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius));
        int maxX = Mathf.Min(texSize - 1, Mathf.CeilToInt(center.x + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius));
        int maxY = Mathf.Min(texSize - 1, Mathf.CeilToInt(center.y + radius));
        float rSq = radius * radius;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x + 0.5f - center.x;
                float dy = y + 0.5f - center.y;
                if (dx * dx + dy * dy <= rSq)
                    pixels[y * texSize + x] = color;
            }
        }
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
        return SaveGeneratedTexture(tex, "NoPhoto", TextureWrapMode.Clamp);
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
    // traced behind the animation. Takes an already-built texture rather
    // than generating one — CreateTrainingSplit's routeDrawer callback
    // fires once per column (ОБРАЗЕЦ and ВАШИ ДЕЙСТВИЯ both draw the same
    // route), and CreateDashedPathTexture is expensive (900x900, embedded
    // straight into the scene since it's never saved as its own asset) —
    // generating it twice per page doubled the scene file's own size for
    // an identical picture. Callers build the texture once and share it.
    static void CreateDashedRouteBackdrop(Transform parent, Texture2D tex, float worldSpan)
    {
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
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
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
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        Material material = new Material(shader) { color = new Color(0f, 0f, 0f, 0.35f) };

        System.IO.Directory.CreateDirectory("Assets/LadyBug/Materials/lady_bug");
        string materialPath = "Assets/LadyBug/Materials/lady_bug/" + materialName + ".mat";
        AssetDatabase.DeleteAsset(materialPath);
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;
    }
}
}
