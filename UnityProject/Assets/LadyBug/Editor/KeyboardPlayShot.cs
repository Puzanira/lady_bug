using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LadyBug
{

// Batch verification still for the arcade integration: proves the game is
// playable from the keyboard on a machine with NO Arduino board attached.
// Opens the Main scene, enters play mode, waits for the menu, confirms the
// default КЛАВИАТУРА selection by invoking the same BeginGame the menu's
// Space/Enter press calls, lets gameplay run a couple of seconds, then
// renders the main camera to an offscreen texture and writes the PNG given
// by -shotPath. Exits 0 on success, non-zero on any failure. Invoked via
// -executeMethod LadyBug.KeyboardPlayShot.Capture (batchmode, no -quit —
// this tool exits the editor itself).
public static class KeyboardPlayShot
{
    private static int _phase;
    private static double _phaseStart;
    private static string _outPath;
    // "gameplay" (default, historical behaviour) | "menu" | "final".
    // menu  — shoot the pre-game screen without ever confirming.
    // final — confirm, run, then force the win trigger and shoot the finale
    //         after -shotDelay seconds, so the celebration FX (the author's
    //         new Resources/Celebration PNG sequences) are on screen.
    private static string _mode = "gameplay";
    private static double _shotDelay = 2.5;
    private static double _menuSettle = 1.5;
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
                if (now - _phaseStart > (_mode == "menu" ? _menuSettle : 1.5))
                {
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
                        Debug.Log("[KeyboardPlayShot] final mode: win triggered, holding "
                                  + _shotDelay + "s before the still.");
                        _phase = 4;
                        _phaseStart = now;
                        break;
                    }

                    WriteShot();
                    _phase = 3;
                }
                break;

            case 4: // finale playing out
                if (now - _phaseStart > _shotDelay)
                {
                    WriteShot();
                    _phase = 3;
                }
                break;
        }
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
