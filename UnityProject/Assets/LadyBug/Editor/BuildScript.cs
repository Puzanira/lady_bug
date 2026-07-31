using UnityEditor;

public static class BuildScript
{
    // Wasn't reachable from the Editor UI at all before (no menu item, and
    // building via the standard Build Profiles window doesn't call this) —
    // which is how cameraUsageDescription stayed empty in ProjectSettings
    // despite this method's own defensive check for exactly that, and the
    // crash it's meant to prevent kept recurring. Use this instead of the
    // Build Profiles window's own Build button from now on, so that check
    // (and anything else added here later) actually runs.
    [MenuItem("Tools/Build (macOS)")]
    public static void PerformBuild()
    {
        // Required as soon as WebCamTexture is used anywhere in the project
        // (PlayerPhotoCapture, for the winner photos) — without it the build
        // fails outright, and if it somehow still produces a .app, macOS
        // kills the process the instant it touches the camera (no
        // NSCameraUsageDescription in Info.plist).
        if (string.IsNullOrEmpty(PlayerSettings.macOS.cameraUsageDescription))
            PlayerSettings.macOS.cameraUsageDescription = "Фото победителя для таблицы рекордов";

        var buildOptions = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/LadyBug/Scenes/Main.unity" },
            // Outside this repo entirely (../../.results/bin, a sibling of
            // lady_bug under Y-GameLab) — same place earlier manual builds
            // already landed, and .gitignore's UnityProject/Builds/ entry
            // is now moot for this path, not just redundant with it.
            locationPathName = "../../.results/bin/LadyBugHitTheRoad.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(buildOptions);
        UnityEngine.Debug.Log("Build result: " + report.summary.result);
    }
}
