using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

// 4 independent leaderboards (time to finish, final score, final tricks,
// peak speed) — top 10 each, saved in PlayerPrefs. The in-game panel cycles
// through showing the top 3 of one category at a time; ReportRun (called
// once by WinSequence when a run finishes) returns which categories that
// run newly qualifies for, so the end screen can reveal them one by one.
public class HighScoreManager : MonoBehaviour
{
    private const int RowCount = 3;

    public static HighScoreManager Instance { get; private set; }

    [SerializeField] private Text titleText;
    [SerializeField] private Text[] rowTexts;
    [SerializeField] private RawImage[] rowPhotos;
    [SerializeField] private float cycleInterval = 5f;

    // Which category/photo is currently loaded into rowPhotos, so
    // UpdateDisplay (called every frame) only touches disk when the
    // category cycles or a photo actually changes, not every frame.
    private int _displayedCategory = -1;
    private readonly string[] _loadedPhotoPaths = new string[RowCount];
    private readonly Texture2D[] _loadedPhotoTextures = new Texture2D[RowCount];

    // Matches RowCount (3) — nothing in this file or TopResultsPage ever
    // reads past index 2, so storing 10 was just unused depth. To make
    // testing easier (more runs qualify as new records), clear the saved
    // PlayerPrefs data down to each category's #1 entry directly instead of
    // shrinking this — this constant stays at the real intended size.
    private const int Count = 3;

    private enum Category { Time, Score, Tricks, Speed }
    private static readonly string[] CategoryNames = { "ВРЕМЯ", "ОЧКИ", "ТРЮКИ", "СКОРОСТЬ" };
    private static readonly string[] CategoryKeys = { "Time", "Score", "Tricks", "Speed" };

    // Time: lower is better. Everything else: higher is better.
    private static readonly bool[] HigherIsBetter = { false, true, true, true };

    private readonly float[][] _values = new float[4][];
    // Parallel to _values — empty string until PlayerPhotoCapture attaches a
    // saved snapshot to that slot (see SetPhotoPath). Shifted in lockstep
    // with _values whenever a new run displaces existing entries.
    private readonly string[][] _photoPaths = new string[4][];

    public struct NewRecord
    {
        public int CategoryIndex; // 0-3, same order as CategoryKeys — for SetPhotoPath
        public string CategoryName;
        public int Rank; // 1-based
        public float Value;
    }

    public struct Entry
    {
        public float Value;
        public string PhotoPath; // "" if none was ever attached
    }

    public static int CategoryCount => 4;

    private void Awake()
    {
        Instance = this;
        for (int c = 0; c < 4; c++)
        {
            _values[c] = new float[Count];
            _photoPaths[c] = new string[Count];
            for (int i = 0; i < Count; i++)
            {
                _values[c][i] = PlayerPrefs.GetFloat(Key(c, i), 0f);
                _photoPaths[c][i] = PlayerPrefs.GetString(PhotoKey(c, i), "");
            }
        }
        UpdateDisplay();
    }

    private void Update()
    {
        UpdateDisplay();
    }

    // Called once, at the moment a run finishes. Returns one entry per
    // category the run newly qualifies for (empty list if none).
    // ranksByCategory (Time/Score/Tricks/Speed, same order as CategoryKeys)
    // gets every category's rank regardless of whether it made the top 10
    // (0 = didn't) — for the end-of-run achievements screen, which shows a
    // placement for all 4 categories, not just newly-qualifying ones.
    public List<NewRecord> ReportRun(float timeSeconds, int score, int tricks, float maxSpeed, out int[] ranksByCategory)
    {
        float[] runValues = { timeSeconds, score, tricks, maxSpeed };
        var results = new List<NewRecord>();
        ranksByCategory = new int[4];

        for (int c = 0; c < 4; c++)
        {
            int rank = TryInsert(c, runValues[c]);
            ranksByCategory[c] = rank;
            if (rank > 0)
                results.Add(new NewRecord { CategoryIndex = c, CategoryName = CategoryNames[c], Rank = rank, Value = runValues[c] });
        }

        Save();
        return results;
    }

    // Attaches a saved snapshot (see PlayerPhotoCapture) to a specific
    // leaderboard slot — called once the photo finishes saving, since that
    // happens well after ReportRun already determined the rank.
    public void SetPhotoPath(int category, int rank, string path)
    {
        if (category < 0 || category >= 4 || rank < 1 || rank > Count)
            return;
        _photoPaths[category][rank - 1] = path;
        PlayerPrefs.SetString(PhotoKey(category, rank - 1), path ?? "");
        PlayerPrefs.Save();
    }

    // Top `count` entries for a category — used by the start-screen carousel
    // to show placements alongside whatever photo (if any) was attached.
    public Entry[] GetTopEntries(int category, int count)
    {
        count = Mathf.Min(count, Count);
        var result = new Entry[count];
        for (int i = 0; i < count; i++)
            result[i] = new Entry { Value = _values[category][i], PhotoPath = _photoPaths[category][i] };
        return result;
    }

    public string GetCategoryName(int category) => CategoryNames[category];
    public string FormatEntryValue(int category, float value) => FormatValue(category, value);

    // True once any category has at least one real entry — rank 1 (index 0)
    // is always the best entry in a category (TryInsert keeps each array
    // sorted), so checking just that slot is enough, no need to scan all
    // 10. Used to keep the live in-game "ТОП" panel hidden entirely on a
    // fresh save with zero records anywhere instead of showing an empty
    // gray box with nothing but "--" in it. Only the live panel does this —
    // the start-screen carousel shows every category's table regardless,
    // "--" placeholders included, same as its other static pages.
    public bool HasAnyRecordAnywhere()
    {
        for (int c = 0; c < 4; c++)
            if (_values[c][0] > 0f)
                return true;
        return false;
    }

    // Returns the 1-based rank the value landed at, or 0 if it didn't make the top 10.
    private int TryInsert(int category, float value)
    {
        // A run that scored/moved/tricked nothing at all isn't a record of
        // anything, even into an otherwise-empty board — without this, a
        // 0-trick run could rank #1 in ТРЮКИ on a fresh save and trigger a
        // "NEW RECORD" reveal for it, matching FormatValue's own <=0 ==
        // "no real entry" ("--") treatment.
        if (value <= 0f)
            return 0;

        float[] arr = _values[category];
        string[] photos = _photoPaths[category];
        bool higherBetter = HigherIsBetter[category];

        for (int i = 0; i < Count; i++)
        {
            bool slotEmpty = arr[i] <= 0f;
            bool beats = !slotEmpty && (higherBetter ? value > arr[i] : value < arr[i]);
            if (!slotEmpty && !beats)
                continue;

            for (int j = Count - 1; j > i; j--)
            {
                arr[j] = arr[j - 1];
                photos[j] = photos[j - 1];
            }
            arr[i] = value;
            photos[i] = ""; // the run that just landed here hasn't had its photo taken yet
            return i + 1;
        }
        return 0;
    }

    private void Save()
    {
        for (int c = 0; c < 4; c++)
            for (int i = 0; i < Count; i++)
            {
                PlayerPrefs.SetFloat(Key(c, i), _values[c][i]);
                PlayerPrefs.SetString(PhotoKey(c, i), _photoPaths[c][i] ?? "");
            }
        PlayerPrefs.Save();
    }

    private static string Key(int category, int index) => "Board_" + CategoryKeys[category] + "_" + index;
    private static string PhotoKey(int category, int index) => "BoardPhoto_" + CategoryKeys[category] + "_" + index;

    // Rotates the on-screen panel between the 4 categories' top-3, looping —
    // driven off Time.time so no timer field is needed (same trick as
    // StartScreenController's carousel). Each row shows its own photo (if
    // that slot ever had one attached), same idea as TopResultsPage on the
    // start screen — but this runs every frame, so photo textures are only
    // (re)loaded from disk when the shown path actually changes, not on
    // every call.
    private void UpdateDisplay()
    {
        int cat = Mathf.FloorToInt(Time.time / cycleInterval) % 4;

        if (titleText != null && cat != _displayedCategory)
        {
            titleText.text = "ТОП: " + CategoryNames[cat];
            _displayedCategory = cat;
        }

        for (int i = 0; i < RowCount; i++)
        {
            if (rowTexts != null && i < rowTexts.Length && rowTexts[i] != null)
                rowTexts[i].text = (i + 1) + ". " + FormatValue(cat, _values[cat][i]);

            if (rowPhotos == null || i >= rowPhotos.Length || rowPhotos[i] == null)
                continue;

            string photoPath = _values[cat][i] > 0f ? _photoPaths[cat][i] : "";
            if (photoPath == _loadedPhotoPaths[i])
                continue;
            _loadedPhotoPaths[i] = photoPath;

            if (_loadedPhotoTextures[i] != null)
            {
                Destroy(_loadedPhotoTextures[i]);
                _loadedPhotoTextures[i] = null;
            }

            if (!string.IsNullOrEmpty(photoPath) && File.Exists(photoPath))
            {
                byte[] bytes = File.ReadAllBytes(photoPath);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                _loadedPhotoTextures[i] = tex;
                rowPhotos[i].texture = tex;
                rowPhotos[i].gameObject.SetActive(true);
            }
            else
            {
                rowPhotos[i].texture = null;
                rowPhotos[i].gameObject.SetActive(false);
            }
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < RowCount; i++)
        {
            if (_loadedPhotoTextures[i] != null)
                Destroy(_loadedPhotoTextures[i]);
        }
    }

    private static string FormatValue(int category, float value)
    {
        if (value <= 0f)
            return "--";

        switch ((Category)category)
        {
            case Category.Time:
                int minutes = Mathf.FloorToInt(value / 60f);
                int secs = Mathf.FloorToInt(value % 60f);
                return string.Format("{0:00}:{1:00}", minutes, secs);
            case Category.Speed:
                return string.Format("{0:0.0} км/ч", value);
            case Category.Score:
                return Mathf.RoundToInt(value) + " очков";
            case Category.Tricks:
                return Mathf.RoundToInt(value) + " шт";
            default:
                return Mathf.RoundToInt(value).ToString();
        }
    }
}
