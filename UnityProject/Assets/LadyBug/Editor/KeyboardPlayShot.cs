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
    private static bool _restoreEnterPlayOptions;
    private static EnterPlayModeOptions _savedOptions;
    private static bool _savedOptionsEnabled;

    public static void Capture()
    {
        _outPath = ArgValue("-shotPath");
        if (string.IsNullOrEmpty(_outPath))
            _outPath = Path.Combine(Application.temporaryCachePath, "ladybug-keyboard.png");

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

            case 1: // menu settling (intro is skipped now; give Awake/Start a beat)
                if (now - _phaseStart > 1.5)
                {
                    var menu = Object.FindAnyObjectByType<StartScreenController>();
                    if (menu == null)
                        Fail(3, "StartScreenController not found in play mode");
                    // Same call the menu's Space/Enter confirm makes, with the
                    // default selection: КЛАВИАТУРА (controller 0), 2 players.
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

            case 2: // let gameplay actually run before the still
                if (now - _phaseStart > 2.5)
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
        cam.Render();
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
