using System.IO;
using UnityEngine;
using UnityEngine.UI;

// One start-screen carousel page: the top 3 for one leaderboard category,
// each row showing its own photo (see PlayerPhotoCapture/HighScoreManager)
// if one was ever attached to that specific slot — not just a single photo
// for #1 shown off to the side. Refreshed every time the page becomes
// visible rather than baked at scene-build time, since the underlying
// leaderboard is only known at runtime (PlayerPrefs) and can change
// between carousel cycles.
public class TopResultsPage : MonoBehaviour
{
    private const int RowCount = 3;

    [SerializeField] private int category;
    [SerializeField] private Text titleText;
    [SerializeField] private Text[] rowValueTexts;
    [SerializeField] private RawImage[] rowMedals;
    [SerializeField] private RawImage[] rowPhotos;
    // Black square, red diagonal cross — shown in a photo slot that has a
    // real ranked entry but no photo was ever attached to it, instead of
    // just leaving the slot blank.
    [SerializeField] private Texture2D noPhotoTexture;
    [SerializeField] private GameObject[] rowArrowShafts;
    [SerializeField] private GameObject[] rowArrowHeads;

    private readonly Texture2D[] _loadedPhotos = new Texture2D[RowCount];

    private void OnEnable()
    {
        Refresh();
    }

    private void OnDisable()
    {
        for (int i = 0; i < RowCount; i++)
        {
            if (_loadedPhotos[i] != null)
            {
                Destroy(_loadedPhotos[i]);
                _loadedPhotos[i] = null;
            }
        }
    }

    private void Refresh()
    {
        if (HighScoreManager.Instance == null)
            return;

        if (titleText != null)
            titleText.text = "ТОП: " + HighScoreManager.Instance.GetCategoryName(category);

        HighScoreManager.Entry[] top = HighScoreManager.Instance.GetTopEntries(category, RowCount);

        for (int i = 0; i < RowCount; i++)
        {
            bool hasEntry = i < top.Length && top[i].Value > 0f;
            string valueText = hasEntry ? HighScoreManager.Instance.FormatEntryValue(category, top[i].Value) : "--";

            if (rowValueTexts != null && i < rowValueTexts.Length && rowValueTexts[i] != null)
                rowValueTexts[i].text = valueText;

            // No medal at all for a rank nobody's actually reached yet — an
            // empty gold/silver/bronze next to a "--" read as a placeholder
            // prize for a result that doesn't exist.
            if (rowMedals != null && i < rowMedals.Length && rowMedals[i] != null)
                rowMedals[i].gameObject.SetActive(hasEntry);

            if (_loadedPhotos[i] != null)
            {
                Destroy(_loadedPhotos[i]);
                _loadedPhotos[i] = null;
            }

            // Arrow visibility follows the photo, not just hasEntry — an
            // entry can exist with no photo ever attached to it, and an
            // arrow pointing at nothing (or a dash) is just clutter.
            bool showPhoto = false;
            string photoPath = hasEntry ? top[i].PhotoPath : null;
            if (!string.IsNullOrEmpty(photoPath) && File.Exists(photoPath) &&
                rowPhotos != null && i < rowPhotos.Length && rowPhotos[i] != null)
            {
                byte[] bytes = File.ReadAllBytes(photoPath);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                _loadedPhotos[i] = tex;
                rowPhotos[i].texture = tex;
                rowPhotos[i].gameObject.SetActive(true);
                showPhoto = true;
            }
            else if (rowPhotos != null && i < rowPhotos.Length && rowPhotos[i] != null)
            {
                // Real entry, just never got a photo attached — show the
                // "no photo" placeholder instead of leaving a blank gap.
                // No entry at all — hide the slot entirely, same as before.
                if (hasEntry && noPhotoTexture != null)
                {
                    rowPhotos[i].texture = noPhotoTexture;
                    rowPhotos[i].gameObject.SetActive(true);
                }
                else
                {
                    rowPhotos[i].texture = null;
                    rowPhotos[i].gameObject.SetActive(false);
                }
            }

            if (rowArrowShafts != null && i < rowArrowShafts.Length && rowArrowShafts[i] != null)
                rowArrowShafts[i].SetActive(showPhoto);
            if (rowArrowHeads != null && i < rowArrowHeads.Length && rowArrowHeads[i] != null)
                rowArrowHeads[i].SetActive(showPhoto);
        }
    }
}
