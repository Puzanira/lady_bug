using UnityEditor;
using UnityEngine;

// One-off maintenance utility — not part of scene construction (see
// SceneSetup for that). Keeps each leaderboard's #1 entry but clears ranks
// 2-3, which mostly accumulated from repeated testing runs rather than
// genuine attempts at beating the top spot. Matches HighScoreManager's own
// PlayerPrefs key scheme exactly (Key/PhotoKey there).
public static class HighScoreMaintenance
{
    private static readonly string[] CategoryKeys = { "Time", "Score", "Tricks", "Speed" };

    [MenuItem("Tools/Trim Saved High Scores To Top 1")]
    static void TrimToTopOne()
    {
        foreach (string category in CategoryKeys)
        {
            for (int rankIndex = 1; rankIndex <= 2; rankIndex++)
            {
                PlayerPrefs.DeleteKey("Board_" + category + "_" + rankIndex);
                PlayerPrefs.DeleteKey("BoardPhoto_" + category + "_" + rankIndex);
            }
        }
        PlayerPrefs.Save();
        Debug.Log("Trimmed saved high scores down to each category's #1 entry.");
    }
}
