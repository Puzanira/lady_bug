using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LadyBug
{

// Batch verification still for the arcade integration: opens the Main scene,
// enters play mode, drives the game to the screen a given -shotMode names, then
// renders the main camera to an offscreen texture and writes the PNG given by
// -shotPath. Exits 0 on success, non-zero on any failure. Invoked via
// -executeMethod LadyBug.KeyboardPlayShot.Capture (batchmode, no -quit —
// this tool exits the editor itself).
//
// It photographs the game AS THE CABINET RUNS IT — i.e. inside the launcher.
// That is not a detail: run standalone, the loader's attract screen (canvas
// sortingOrder 230, full screen) sits on top of everything and only stands aside
// once somebody holds one of its keys for five seconds. The game starts UNDER it
// regardless, so every mode used to come back with the same photo of the attract
// screen — menu, gameplay and finale alike — a still that looks fine and proves
// nothing about what it claims to show.
//
// The game already has one honest way out of that, and it is the way the cabinet
// itself uses: inside the arcade launcher LoaderScreenController hides its canvas
// in Awake and disables itself, and StartScreenController brings the menu up
// without waiting for an IntroSequence that will never finish
// (ArcadeControlsReader.InsideArcadeLauncher). So instead of reaching into the
// scene and switching objects off — a test-only path through the game that no
// player would ever take — this tool makes the game's own probe find a facade:
// ArcadeLauncherStub.Install() supplies an editor-only, neutral-valued
// AiGameStudio.ArcadeControls.ArcadeInput. Not a line of game code knows this
// tool exists; the game takes the same branch it takes on the real cabinet.
//
// -shotMode attract skips the stub on purpose, so the standalone behaviour the
// author wrote — the attract screen really being there, first frame to last — can
// be photographed too, and stays provable after every upstream merge.
public static class KeyboardPlayShot
{
    private static int _phase;
    private static double _phaseStart;
    private static string _outPath;
    // "gameplay" (default) | "menu" | "final" | "attract".
    // menu    — shoot the pre-game menu without ever confirming.
    // final   — confirm, run for -shotDelay seconds, force the win trigger, then
    //           WAIT for the run-results screen (ИТОГИ ЗАБЕГА) to actually come up
    //           and shoot that. -shotDelay does not time the finale: see phase 4.
    // attract — shoot the standalone loader/attract screen (no launcher stub).
    private static string _mode = "gameplay";
    private static double _shotDelay = 2.5;
    private static double _menuSettle = 1.5;
    private static WinSequence _winSequence;
    private static bool _restoreEnterPlayOptions;
    private static EnterPlayModeOptions _savedOptions;
    private static bool _savedOptionsEnabled;

    public static void Capture()
    {
        _outPath = ArgValue("-shotPath");
        if (string.IsNullOrEmpty(_outPath))
            _outPath = Path.Combine(Application.temporaryCachePath, "ladybug-keyboard.png");

        string mode = ArgValue("-shotMode");
        if (!string.IsNullOrEmpty(mode))
            _mode = mode;
        string delay = ArgValue("-shotDelay");
        if (!string.IsNullOrEmpty(delay) && double.TryParse(delay, out double parsed))
        {
            _shotDelay = parsed;
            _menuSettle = parsed;
        }

        // Every mode but "attract" photographs the game as the cabinet runs it —
        // inside the launcher, where the attract screen stands aside by itself.
        // See the class comment for why this is the only honest way to get past it.
        if (_mode != "attract")
        {
            ArcadeLauncherStub.Install();
            Debug.Log("[KeyboardPlayShot] arcade launcher facade installed for this session: "
                      + "the game will take its in-cabinet branch (attract screen stands aside).");
        }
        else
        {
            Debug.Log("[KeyboardPlayShot] attract mode: no launcher facade — "
                      + "photographing the standalone boot screen the author wrote.");
        }

        // Keep statics (this state machine, its delegates) alive across the
        // edit->play transition for this batch session only; restored before exit.
        _savedOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        _savedOptions = EditorSettings.enterPlayModeOptions;
        _restoreEnterPlayOptions = true;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

        EditorSceneManager.OpenScene("Assets/LadyBug/Scenes/Main.unity", OpenSceneMode.Single);
        _phase = 0;
        _phaseStart = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;

        // Global watchdog so a wedged run can't hang the batch forever.
        if (now - _phaseStart > 120.0)
            Fail(4, "watchdog timeout in phase " + _phase);

        switch (_phase)
        {
            case 0: // waiting for play mode
                if (EditorApplication.isPlaying)
                {
                    if (!VerifyAttractScreenState())
                        break;
                    _phase = 1;
                    _phaseStart = now;
                }
                break;

            // Menu settling (intro is skipped now; give Awake/Start a beat). In
            // menu mode -shotDelay doubles as the settle time: the instruction
            // carousel is deliberately blank for the first
            // PreGameScreenTiming.PageDwellSeconds (7s), so a shot taken earlier
            // shows a legitimately empty middle of the screen.
            case 1:
                if (now - _phaseStart > (_mode == "menu" || _mode == "attract" ? _menuSettle : 1.5))
                {
                    if (_mode == "attract")
                    {
                        // The boot screen IS the subject here; the menu is still behind it.
                        Debug.Log("[KeyboardPlayShot] attract mode: capturing the loader screen.");
                        WriteShot();
                        _phase = 3;
                        break;
                    }

                    var menu = Object.FindAnyObjectByType<StartScreenController>();
                    if (menu == null)
                        Fail(3, "StartScreenController not found in play mode");

                    if (_mode == "menu")
                    {
                        // Nothing to confirm — the pre-game screen IS the subject.
                        Debug.Log("[KeyboardPlayShot] menu mode: capturing the start screen.");
                        WriteShot();
                        _phase = 3;
                        break;
                    }

                    // Same call the menu's confirm makes, with whatever selection
                    // the menu defaults to on this machine.
                    MethodInfo begin = typeof(StartScreenController)
                        .GetMethod("BeginGame", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (begin == null)
                        Fail(3, "BeginGame not found via reflection");
                    begin.Invoke(menu, null);
                    Debug.Log("[KeyboardPlayShot] BeginGame invoked (keyboard default).");
                    _phase = 2;
                    _phaseStart = now;
                }
                break;

            // Let gameplay actually run before the still. -shotDelay steers this
            // too, not just menu/final: the road is empty for the first seconds
            // and the frame-animated creatures (cat, crow, dog, rabbit, snake)
            // only get near the camera later, so a fixed short wait photographs
            // bare asphalt and proves nothing about them.
            case 2:
                if (now - _phaseStart > _shotDelay)
                {
                    if (_mode == "final")
                    {
                        // Force the goal so the whole finale plays out for real
                        // (finish text -> continue offer -> flight -> celebration)
                        // instead of shooting a synthetic pose.
                        var win = Object.FindAnyObjectByType<WinSequence>();
                        if (win == null)
                            Fail(3, "WinSequence not found in play mode");
                        win.TryTrigger(9999f);
                        _winSequence = win;
                        Debug.Log("[KeyboardPlayShot] final mode: win triggered; "
                                  + "waiting for ИТОГИ ЗАБЕГА to actually come up.");
                        _phase = 4;
                        _phaseStart = now;
                        break;
                    }

                    WriteShot();
                    _phase = 3;
                }
                break;

            // Finale playing out. The recap is NOT at a fixed offset from the trigger:
            // the win first offers a few seconds to carry on (ContinueCountdownStart),
            // then plays ФИНИШ, fades the road's creatures out, flies the bugs away and
            // only then raises ИТОГИ ЗАБЕГА. A blind "hold N seconds" here is what made
            // this mode photograph the flight and call it the finale. Wait for the recap
            // itself to be on screen; the global watchdog turns a finale that never
            // arrives into a failed run instead of a wrong picture.
            case 4:
                if (RecapIsOnScreen())
                {
                    Debug.Log("[KeyboardPlayShot] final mode: ИТОГИ ЗАБЕГА is up, settling "
                              + RecapSettleSeconds + "s before the still.");
                    _phase = 5;
                    _phaseStart = now;
                }
                break;

            case 5: // recap up — let its first page lay itself out, then shoot
                if (now - _phaseStart > RecapSettleSeconds)
                {
                    WriteShot();
                    _phase = 3;
                }
                break;
        }
    }

    // How long to let the results page settle once it appears: its rows/icons are
    // filled in over the frames right after the backdrop goes up.
    private const double RecapSettleSeconds = 1.0;

    // True once the run-results screen (ИТОГИ ЗАБЕГА — WinSequence's statsBackdrop,
    // the page that carries the run's stats and any new-record tags) is actually
    // visible. Reached through the serialized field rather than an object name so a
    // renamed GameObject in the generated scene cannot quietly turn this into "never".
    private static bool RecapIsOnScreen()
    {
        if (_winSequence == null)
            Fail(3, "WinSequence disappeared while the finale was playing");

        FieldInfo backdropField = typeof(WinSequence)
            .GetField("statsBackdrop", BindingFlags.NonPublic | BindingFlags.Instance);
        if (backdropField == null)
            Fail(3, "WinSequence.statsBackdrop is gone — the results screen this mode "
                    + "promises to photograph is found through it. Re-point KeyboardPlayShot "
                    + "at whatever now carries ИТОГИ ЗАБЕГА.");

        var backdrop = backdropField.GetValue(_winSequence) as GameObject;
        if (backdrop == null)
            Fail(3, "WinSequence has no statsBackdrop assigned in the scene — the results "
                    + "screen cannot come up at all (Tools → Rebuild Scene?).");

        return backdrop.activeInHierarchy;
    }

    // The failure this tool shipped with was silent: the attract screen covered the
    // subject and the PNG still looked like a plausible picture of the game. So the
    // first thing every run does, once play mode is live, is check that the screen it
    // is about to photograph is the screen the mode promised — and die loudly if not.
    // Returns false when the run has already been failed.
    private static bool VerifyAttractScreenState()
    {
        bool insideLauncher = _mode != "attract";

        if (insideLauncher && !ArcadeLauncherStub.GameSeesTheLauncher())
        {
            Fail(3, "the game does not see the arcade launcher facade in play mode — "
                    + "the attract screen would cover every shot. See ArcadeLauncherStub.");
            return false;
        }

        var loaders = Object.FindObjectsByType<LoaderScreenController>(FindObjectsInactive.Include);
        foreach (LoaderScreenController loader in loaders)
        {
            bool standingAside = !loader.enabled;
            if (insideLauncher && !standingAside)
            {
                Fail(3, "LoaderScreenController is still running inside the launcher: the attract "
                        + "screen (sortingOrder 230) will sit on top of the shot. Its Awake is "
                        + "supposed to stand it down when ArcadeControlsReader.InsideArcadeLauncher.");
                return false;
            }
            if (!insideLauncher && standingAside)
            {
                Fail(3, "attract mode found no running LoaderScreenController — standalone, the "
                        + "author's boot screen must still be there. Something disabled it outside "
                        + "the launcher.");
                return false;
            }
        }

        Debug.Log("[KeyboardPlayShot] attract screen check passed (insideLauncher=" + insideLauncher
                  + ", loaders=" + loaders.Length + ").");
        return true;
    }

    private static void WriteShot()
    {
        const int w = 1920, h = 1080;
        Camera cam = Camera.main;
        if (cam == null)
            cam = Object.FindAnyObjectByType<Camera>();
        if (cam == null)
            Fail(3, "no camera found for capture");

        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        RenderTexture prevTarget = cam.targetTexture;
        cam.targetTexture = rt;

        // Camera.Render() does NOT draw Screen Space - Overlay canvases: they are
        // composited straight to the display, never into a camera's target. Every
        // menu, HUD and win-recap page in this game is an overlay canvas, so a raw
        // cam.Render() yields a picture of the road with the entire UI missing —
        // which is exactly the kind of shot that looks fine and proves nothing.
        // Borrow each overlay canvas into this camera for the duration of the
        // render, then hand it straight back.
        var borrowed = new System.Collections.Generic.List<Canvas>();
        var savedPlaneDistance = new System.Collections.Generic.List<float>();
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
        {
            if (c == null || !c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;
            borrowed.Add(c);
            savedPlaneDistance.Add(c.planeDistance);
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = cam;
            c.planeDistance = Mathf.Max(cam.nearClipPlane + 0.1f, 1f);
        }
        Canvas.ForceUpdateCanvases();

        cam.Render();

        for (int i = 0; i < borrowed.Count; i++)
        {
            borrowed[i].renderMode = RenderMode.ScreenSpaceOverlay;
            borrowed[i].worldCamera = null;
            borrowed[i].planeDistance = savedPlaneDistance[i];
        }
        Canvas.ForceUpdateCanvases();
        Debug.Log("[KeyboardPlayShot] overlay canvases folded into the capture: " + borrowed.Count);

        cam.targetTexture = prevTarget;

        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;
        rt.Release();

        int magenta = 0;
        Color32[] px = tex.GetPixels32();
        for (int i = 0; i < px.Length; i++)
            if (px[i].r > 220 && px[i].g < 40 && px[i].b > 220) magenta++;

        Directory.CreateDirectory(Path.GetDirectoryName(_outPath));
        File.WriteAllBytes(_outPath, tex.EncodeToPNG());
        Object.Destroy(tex);
        Debug.Log($"[KeyboardPlayShot] Wrote '{_outPath}' ({w}x{h}); magentaPixels={magenta}");
        Finish(0);
    }

    private static void Fail(int code, string reason)
    {
        Debug.LogError("[KeyboardPlayShot] FAIL: " + reason);
        Finish(code);
    }

    private static void Finish(int code)
    {
        EditorApplication.update -= Tick;
        if (_restoreEnterPlayOptions)
        {
            EditorSettings.enterPlayModeOptionsEnabled = _savedOptionsEnabled;
            EditorSettings.enterPlayModeOptions = _savedOptions;
        }
        EditorApplication.Exit(code);
    }

    private static string ArgValue(string flag)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == flag) return args[i + 1];
        return null;
    }
}
}
