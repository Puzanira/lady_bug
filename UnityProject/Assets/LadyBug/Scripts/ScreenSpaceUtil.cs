using UnityEngine;

// Converts a world position to the (0,0)-anchored canvas coordinates shared
// by all UI popups (matches the 1920x1080 reference resolution every canvas
// uses), so a popup can spawn at a specific player's on-screen position
// instead of a fixed point.
public static class ScreenSpaceUtil
{
    public static Vector2 WorldToCanvasPoint(Vector3 worldPos)
    {
        if (Camera.main == null)
            return Vector2.zero;

        Vector3 screenPoint = Camera.main.WorldToScreenPoint(worldPos);
        return new Vector2(
            screenPoint.x * (1920f / Screen.width),
            screenPoint.y * (1080f / Screen.height));
    }
}
