using UnityEngine;

namespace LadyBug
{


// Corner wedge HUD (distance/time/score/tricks), tricks panel, and per-player
// gesture readouts — gameplay panels only during a run; gesture debug HUD can
// also show on the pre-game menu when hardware is connected.
public static class GameplayHudVisibility
{
    public static readonly string[] WedgePanelNames =
    {
        "DistancePanel", "TimerPanel", "GearSpeedPanel", "ScorePanel", "RightHubPlaceholder",
    };

    public static bool IsHardwareConnected()
    {
        bool sensorsConnected = GestureSensorSerial.Instance != null && GestureSensorSerial.Instance.IsConnected;
        bool joystickConnected = JoystickSerial.Instance != null && JoystickSerial.Instance.IsConnected;
        return sensorsConnected || joystickConnected;
    }

    public static void SetGameplayHudVisible(bool visible)
    {
        SetWedgePanelsVisible(visible);
        SetTricksHudVisible(visible);
        SetGestureHudVisible(visible);
    }

    public static void SetWedgePanelsVisible(bool visible)
    {
        Canvas scoreCanvas = FindScoreCanvas();
        if (scoreCanvas == null)
            return;

        foreach (string panelName in WedgePanelNames)
        {
            Transform panel = scoreCanvas.transform.Find(panelName);
            if (panel != null)
                panel.gameObject.SetActive(visible);
        }
    }

    public static void SetTricksHudVisible(bool visible)
    {
        GameObject tricksCanvas = FindSceneRoot("TricksCanvas");
        if (tricksCanvas != null)
            tricksCanvas.SetActive(visible);
    }

    public static void SetGestureHudVisible(bool hardwareMode)
    {
        if (!hardwareMode)
        {
            SetPlayerGestureHudVisible("PlayerLeft", false);
            SetPlayerGestureHudVisible("PlayerRight", false);
            return;
        }

        // Left = P1 distance sensors; right = P2 joystick cross — independent
        // panels so the joystick HUD stays visible in 1-player menu/training.
        SetPlayerGestureHudVisible("PlayerLeft", HasSensorHudFeed());
        SetPlayerGestureHudVisible("PlayerRight", HasJoystickHudFeed());
    }

    static bool HasSensorHudFeed()
    {
        if (GestureSensorSerial.Instance != null && GestureSensorSerial.Instance.IsConnected)
            return true;

        JoystickSerial board = JoystickSerial.Instance;
        return board != null && board.IsConnected && board.HasHandSensors;
    }

    static bool HasJoystickHudFeed()
    {
        JoystickSerial board = JoystickSerial.Instance;
        return board != null && board.IsConnected;
    }

    static void SetPlayerGestureHudVisible(string playerName, bool visible)
    {
        GameObject gestureCanvas = FindSceneRoot(playerName + "GestureCanvas");
        if (gestureCanvas == null)
            return;

        gestureCanvas.SetActive(visible);
        if (!visible)
            return;

        // Unity zeroes overlay-canvas scale while inactive — if the scene was
        // saved mid-toggle the canvas can be "active" but still invisible.
        RectTransform rt = gestureCanvas.GetComponent<RectTransform>();
        if (rt != null && rt.localScale.sqrMagnitude < 0.001f)
            rt.localScale = Vector3.one;
    }

    static Canvas FindScoreCanvas()
    {
        GameObject scoreCanvasGo = FindSceneRoot("ScoreCanvas");
        return scoreCanvasGo != null ? scoreCanvasGo.GetComponent<Canvas>() : null;
    }

    // GameObject.Find skips inactive roots. The menu hides whole canvases
    // (TricksCanvas, gesture HUDs) with SetActive(false), so BeginGame's
    // re-show must look through inactive scene objects too.
    static GameObject FindSceneRoot(string name)
    {
        GameObject active = GameObject.Find(name);
        if (active != null)
            return active;

        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            GameObject candidate = all[i];
            if (candidate.name != name || candidate.hideFlags != HideFlags.None)
                continue;
            if (!candidate.scene.IsValid())
                continue;
            return candidate;
        }

        return null;
    }
}
}
