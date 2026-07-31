using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LadyBug
{

public class WinSequence : MonoBehaviour
{
    public static WinSequence Instance { get; private set; }

    [SerializeField] private float winDistanceKm = 10f;

    // Exposed so the start-screen "ЦЕЛЬ" instructions can show the real
    // distance instead of a hardcoded number that'd lie while this is
    // temporarily lowered for testing. Reflects winDistanceKm live, so it
    // also picks up any extension from a "flap to continue" choice below.
    public float WinDistanceKm => winDistanceKm;

    // "Flap to keep going" prompt at the finish line — reaching the goal
    // clears the road and offers a few seconds to flap before committing to
    // the real ending. See OfferContinue.
    [SerializeField] private GameObject continuePromptRoot;
    [SerializeField] private Text continueCountdownText;
    [SerializeField] private float continueDistanceKm = 10f;
    private const int ContinueCountdownStart = 10;
    private bool _awaitingContinueDecision;

    // Shown first, before controls are disabled and anything starts fading
    // out — a plain "nothing's responding anymore" moment otherwise, with
    // no cue why.
    [SerializeField] private GameObject finishText;
    [SerializeField] private float finishTextDuration = 1.6f;

    // Plain, unanimated hold right before the webcam screen (see
    // CaptureRecordPhoto) — a beat of "here's why we're about to point a
    // camera at you" before it actually starts, instead of the capture
    // screen just appearing out of nowhere.
    [SerializeField] private GameObject newRecordAnnounceText;
    [SerializeField] private float newRecordAnnounceDuration = 5f;

    [SerializeField] private RectTransform winTextRoot;
    // How long the title sits alone on screen at the very end, once
    // everything else (stats, records, leaderboard tables) has already
    // finished and hidden — see the end of RunSequence.
    [SerializeField] private float finalTitleHoldDuration = 4f;
    // Shared dark-tint backdrop behind the stats pages — same treatment
    // every other table in the game uses.
    [SerializeField] private GameObject statsBackdrop;
    // Stats pages — a title plus a pool of checkbox rows (CreateWinCheckRow),
    // matching the checklist style already used elsewhere (СУТЬ ИГРЫ, ЦЕЛЬ)
    // instead of one big multi-line text block. Each page uses however many
    // rows it needs (statsRows.Length is the max across any page); unused
    // ones for that page are hidden via their own root.
    [SerializeField] private Text statsTitle;
    [SerializeField] private Text[] statsRows;
    [SerializeField] private GameObject[] statsRowRoots;
    [SerializeField] private float achievementsPageDuration = 5f;
    // СОБРАНО/СБИТО use this icon grid instead of the checkbox rows above —
    // one small icon per unit collected/hit (repeated per count) plus a
    // single "ИТОГО ±N" line, no per-type text breakdown. See
    // ShowIconStatsPage. Once a page's total gets too big to draw one icon
    // per unit, it collapses to one icon per TYPE with a "×N" badge
    // (statsIconCountLabels, matched 1:1 against the first few
    // statsIconSlots) instead.
    [SerializeField] private RawImage[] statsIconSlots;
    [SerializeField] private Text[] statsIconCountLabels;
    [SerializeField] private Text statsTotalText;
    // Stand-in for the rare case AchievementStats recorded a null texture
    // (its Renderer/Material couldn't be read at collision time) — keeps
    // that entry visible on the page instead of it silently vanishing
    // while still counting toward ИТОГО (see the old "empty page, -1
    // total" bug this replaced).
    [SerializeField] private Texture2D mysteryIcon;
    // Container for the tables below (background + all 4 pages) — kept
    // inactive except while ShowLeaderboardTables is actually running, so
    // its background panel doesn't sit visible as an empty tinted box from
    // the moment the game starts.
    [SerializeField] private GameObject leaderboardRoot;
    // Real top-3 tables (TopResultsPage, photo slots included), one per
    // leaderboard category — same component the start-screen carousel uses.
    [SerializeField] private GameObject[] leaderboardPages;

    [SerializeField] private float entityFadeDuration = 1.5f;
    [SerializeField] private float flyDuration = 3f;
    [SerializeField] private float flyHeightGain = 20f;
    [SerializeField] private float flyDistanceGain = 220f;

    private bool _triggered;
    // Set by WaitPage the instant any button/gesture is pressed during the
    // post-win recap — every remaining stage (including the current one)
    // bails out immediately so the whole recap collapses straight to the
    // reload, instead of only skipping the one page being shown.
    private bool _skipToEnd;

    public bool Triggered => _triggered;

    private void Awake()
    {
        Instance = this;
        if (finishText != null)
            finishText.SetActive(false);
        if (newRecordAnnounceText != null)
            newRecordAnnounceText.SetActive(false);
        if (continuePromptRoot != null)
            continuePromptRoot.SetActive(false);
        if (winTextRoot != null)
            winTextRoot.gameObject.SetActive(false);
        if (statsBackdrop != null)
            statsBackdrop.SetActive(false);
        if (statsTitle != null)
            statsTitle.gameObject.SetActive(false);
        if (statsRowRoots != null)
            foreach (var root in statsRowRoots)
                if (root != null)
                    root.SetActive(false);
        if (statsIconSlots != null)
            foreach (var icon in statsIconSlots)
                if (icon != null)
                    icon.gameObject.SetActive(false);
        if (statsIconCountLabels != null)
            foreach (var label in statsIconCountLabels)
                if (label != null)
                    label.gameObject.SetActive(false);
        if (statsTotalText != null)
            statsTotalText.gameObject.SetActive(false);
    }

    public void TryTrigger(float distanceKm)
    {
        if (_triggered || _awaitingContinueDecision || distanceKm < winDistanceKm)
            return;

        StartCoroutine(OfferContinue());
    }

    // Reaching the goal doesn't commit straight to the real ending anymore —
    // the road clears out and this offers a few seconds to flap and keep
    // going instead, per feedback that an abrupt "you're done" the instant
    // the distance ticks over felt too sudden. No flap in time -> the usual
    // RunSequence plays exactly as before. A flap -> the goal just moves
    // further out and normal play resumes, no different from never having
    // reached it yet.
    private IEnumerator OfferContinue()
    {
        _awaitingContinueDecision = true;

        // Same idea as RunSequence's own entity cleanup, just without the
        // fade — this is meant to read as "the road pausing to ask a
        // question", not the start of the real ending. BigArchSpawner runs
        // on its own timer independent of EntitySpawner (see RunSequence's
        // own identical comment below) — without disabling it here too, it
        // could drop a fresh arch right after this same clearing pass,
        // which is exactly what was happening (a big arch still showing up
        // during this "everything cleared" moment).
        foreach (var spawner in FindObjectsByType<EntitySpawner>())
            spawner.enabled = false;
        foreach (var spawner in FindObjectsByType<BigArchSpawner>())
            spawner.enabled = false;
        foreach (var entity in FindObjectsByType<MovingEntity>())
            if (entity != null)
                Destroy(entity.gameObject);

        // Same gradual ease-to-a-stop RunSequence itself uses once the
        // fly-away finishes (EndWinBoost) — the road visibly slowing down
        // sells "the run is pausing to ask something" much better than
        // just clearing obstacles while still racing along at full speed.
        if (SpeedController.Instance != null)
            SpeedController.Instance.EndWinBoost();

        if (continuePromptRoot != null)
            continuePromptRoot.SetActive(true);

        bool flapped = false;
        for (int n = ContinueCountdownStart; n >= 1 && !flapped; n--)
        {
            if (continueCountdownText != null)
                continueCountdownText.text = n.ToString();

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime;
                if (AnyPlayerFlapping())
                {
                    flapped = true;
                    break;
                }
                yield return null;
            }
        }

        if (continuePromptRoot != null)
            continuePromptRoot.SetActive(false);

        _awaitingContinueDecision = false;

        if (flapped)
        {
            winDistanceKm += continueDistanceKm;
            if (SpeedController.Instance != null)
                SpeedController.Instance.CancelDecelerate();
            foreach (var spawner in FindObjectsByType<EntitySpawner>())
                spawner.enabled = true;
            foreach (var spawner in FindObjectsByType<BigArchSpawner>())
                spawner.enabled = true;
            yield break;
        }

        _triggered = true;
        float winTimestamp = GameTimer.Instance != null ? GameTimer.Instance.Elapsed : Time.time;
        StartCoroutine(RunSequence(winTimestamp));
    }

    private static bool AnyPlayerFlapping()
    {
        foreach (var pc in FindObjectsByType<PlayerController>())
            if (pc != null && pc.enabled && pc.IsJumpInputHeld)
                return true;
        return false;
    }

    private IEnumerator RunSequence(float winTimestamp)
    {
        // First thing shown, before anything else changes — controls go
        // dead the moment this sequence takes over (right below), so the
        // player needs to see why before the world starts fading/flying.
        if (finishText != null)
        {
            finishText.SetActive(true);
            yield return new WaitForSeconds(finishTextDuration);
            finishText.SetActive(false);
        }

        foreach (var spawner in FindObjectsByType<EntitySpawner>())
            spawner.enabled = false;
        foreach (var spawner in FindObjectsByType<SideScenerySpawner>())
            spawner.enabled = false;
        // BigArchSpawner runs on its own timer independent of EntitySpawner
        // and only checks SpeedController.IsRunning (which stays true
        // through the whole win sequence) — without disabling it too, it
        // could drop a fresh arch on the road well after the one-time
        // fade-out below already ran, leaving it stranded there for good.
        foreach (var spawner in FindObjectsByType<BigArchSpawner>())
            spawner.enabled = false;
        // Clouds and the sun drift on their own timers, independent of
        // SpeedController (so they keep moving even on the start screen) —
        // nothing above stops them, so without this they'd keep sailing
        // across the sky through the whole win sequence.
        foreach (var spawner in FindObjectsByType<CloudSpawner>())
            spawner.enabled = false;
        foreach (var cloud in FindObjectsByType<CloudDrift>())
            cloud.enabled = false;
        foreach (var sun in FindObjectsByType<SunArc>())
            sun.enabled = false;

        if (GameTimer.Instance != null)
            GameTimer.Instance.Pause(); // the run is over — the clock shouldn't keep ticking through the cutscene

        // Every player currently in the scene — works for 1 or 2 players
        // without the sequence needing to know which/how many up front.
        PlayerController[] players = FindObjectsByType<PlayerController>();
        foreach (var pc in players)
        {
            pc.ForceAirborneVisual(); // wings out for the flight, not whatever ground/air pose they were mid-stride in
            // If the win condition landed mid-crash-tumble, PlayerController's
            // own spin (transform.rotation, see StartCrash/UpdateCrash) is
            // still mid-way through its 720° roll — disabling the component
            // right below freezes it there for good, since nothing else
            // ever resets it, and FlyPlayersAway only touches position/
            // scale. Force upright here so the fly-away never carries a
            // stuck sideways/upside-down spin with it.
            pc.transform.rotation = Quaternion.identity;
            // Same freeze risk if the win condition landed mid-crash-blink
            // (PlayerController's own invincibility flicker) — the sprite
            // could be off at that exact instant and never get flipped back
            // on, since disabling the component right below stops it from
            // ever finishing that cycle on its own (see PlayerController.
            // ForceVisible's own comment) — the ladybug itself vanished,
            // leaving only its shadow flying off, without this.
            pc.ForceVisible();
            pc.enabled = false; // stops input AND OnTriggerEnter — no more collisions
        }

        // Per-player "КЛАВИШИ/ЖЕСТЫ" key-instruction HUD — named after the
        // player GameObject by CreateGesturePanel (SceneSetup.cs). Hidden by
        // fixed name for BOTH players, not just the ones with an active
        // PlayerController above: in 1-player mode the unused side's whole
        // player GameObject is deactivated (StartScreenController), so it
        // never shows up in FindObjectsOfType, but its gesture canvas is a
        // separate, always-active root object that keeps rendering (dim/
        // idle) regardless — left stranded on screen through the fly-away
        // if it isn't hidden here explicitly.
        foreach (string playerName in new[] { "PlayerLeft", "PlayerRight" })
        {
            GameObject gestureCanvas = GameObject.Find(playerName + "GestureCanvas");
            if (gestureCanvas != null)
                gestureCanvas.SetActive(false);
        }

        if (SpeedController.Instance != null)
            SpeedController.Instance.BeginWinBoost();

        MovingEntity[] entities = FindObjectsByType<MovingEntity>();
        foreach (var entity in entities)
        {
            Collider col = entity.GetComponent<Collider>();
            if (col != null)
                col.enabled = false;
            entity.enabled = false; // hold position — don't let the speed boost drag them around mid-fade
        }
        yield return StartCoroutine(FadeOutEntities(entities));

        if (players.Length > 0)
            yield return StartCoroutine(FlyPlayersAway(players));

        // Boost was only ever meant to last while they're still visible
        // shrinking into the distance — once they're gone, ease the road
        // back down to a stop instead of racing on forever.
        if (SpeedController.Instance != null)
            SpeedController.Instance.EndWinBoost();

        if (winTextRoot != null)
            winTextRoot.gameObject.SetActive(true);

        List<HighScoreManager.NewRecord> newRecords = null;
        int[] ranksByCategory = null;
        if (HighScoreManager.Instance != null)
        {
            int score = ScoreManager.Instance != null ? ScoreManager.Instance.Score : 0;
            int tricks = TricksManager.Instance != null ? TricksManager.Instance.Count : 0;
            float maxSpeed = SpeedController.Instance != null ? SpeedController.Instance.MaxSpeedReached : 0f;

            // ranksByCategory feeds ИТОГИ ЗАБЕГА's own inline "НОВЫЙ РЕКОРД
            // ТОП-N" tags below (every category's placement for this run,
            // 0 = not top 10) — the only place a "new record" gets shown
            // now, see CaptureRecordPhoto's own comment on why the old
            // separate reveal line was dropped.
            newRecords = HighScoreManager.Instance.ReportRun(winTimestamp, score, tricks, maxSpeed, out ranksByCategory);
            // The photo should only snap for an actual "record" the way the
            // player sees it on ИТОГИ ЗАБЕГА — top-3, not any top-10
            // placement. HighScoreManager still tracks/returns the full
            // top-10 board either way (the leaderboard tables shown later
            // need that full depth), this just narrows what counts as
            // photo-worthy here.
            newRecords = newRecords.FindAll(r => r.Rank <= 3);
        }
        // Fixed order for the whole post-win recap: run stats (which
        // already show any new-record tags inline), then the photo, then
        // the real leaderboard tables once — finally back to the start
        // screen (which has its own copy of the top results plus
        // instructions). Any button/gesture press at any point during this
        // collapses straight to that reload instead of waiting out the rest.
        _skipToEnd = false;

        if (statsBackdrop != null)
            statsBackdrop.SetActive(true);
        try
        {
            if (statsTitle != null)
                yield return StartCoroutine(ShowStatsPages(winTimestamp, ranksByCategory));

            // Hidden from here through the leaderboard tables below — its
            // box overlapped the top of that (much bigger) table, and it
            // has no business floating behind the photo-capture screen
            // either (it used to sit there undimmed, right between the
            // record message and the smile caption). Brought back right at
            // the very end so it's still the last thing on screen.
            if (winTextRoot != null)
                winTextRoot.gameObject.SetActive(false);

            if (!_skipToEnd && newRecords != null && newRecords.Count > 0)
                yield return StartCoroutine(CaptureRecordPhoto(newRecords));
        }
        finally
        {
            if (statsBackdrop != null)
                statsBackdrop.SetActive(false);
        }

        if (!_skipToEnd && leaderboardPages != null)
            yield return StartCoroutine(ShowLeaderboardTables());

        if (winTextRoot != null)
            winTextRoot.gameObject.SetActive(true);

        // Once the leaderboard tables hide, all that's left on screen is
        // the plain "ВЫ ПРОШЛИ ДО КОНЦА" title — a good, symbolic ending
        // beat on its own, held deliberately instead of flashing by for a
        // single frame before the reload. Always plays in full, even if
        // the rest of the recap was skipped — it's the actual ending, not
        // another page to sit through.
        yield return new WaitForSeconds(finalTitleHoldDuration);

        // Reload by buildIndex, not name: in the arcade-hub build lady_bug's entry
        // scene and Sisyphus's are BOTH named "Main", so LoadScene(name) resolves to
        // the first "Main" in Build Settings (Sisyphus) and launches the wrong game.
        // buildIndex is collision-proof both standalone and in-hub.
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Post-win summary, split across a few pages (same "read at your own
    // pace" idea as the in-game top-3 panel) since it doesn't fit on screen
    // all at once: run totals (each with an inline "НОВЫЙ РЕКОРД ТОП-N" tag
    // if this run placed top-3 in that category — folded in here instead of
    // a separate always-on rating column), what was collected, what was
    // hit, tricks — shown once, not cycled (RunSequence already runs this
    // whole recap exactly once: stats, then rank placements, then the
    // photo, then the leaderboard tables).
    private IEnumerator ShowStatsPages(float winTimestamp, int[] ranksByCategory)
    {
        AchievementStats stats = AchievementStats.Instance;
        float maxSpeed = SpeedController.Instance != null ? SpeedController.Instance.MaxSpeedReached : 0f;
        int minutes = Mathf.FloorToInt(winTimestamp / 60f);
        int secs = Mathf.FloorToInt(winTimestamp % 60f);
        int score = ScoreManager.Instance != null ? ScoreManager.Instance.Score : 0;
        int tricksCount = TricksManager.Instance != null ? TricksManager.Instance.Count : 0;

        // HighScoreManager's own category order (Time/Score/Tricks/Speed) —
        // rank 1-3 only (top-10 placements outside that don't get called
        // out here, the leaderboard tables shown later cover those).
        string RecordSuffix(int categoryIndex)
        {
            int rank = ranksByCategory != null && categoryIndex < ranksByCategory.Length ? ranksByCategory[categoryIndex] : 0;
            return rank >= 1 && rank <= 3 ? " — НОВЫЙ РЕКОРД ТОП-" + rank : "";
        }

        if (statsTitle != null)
            statsTitle.gameObject.SetActive(true);

        var totalsLines = new List<string>
        {
            "Время: " + string.Format("{0:00}:{1:00}", minutes, secs) + RecordSuffix(0),
            "Очки: " + score + RecordSuffix(1),
            "Трюки: " + tricksCount + RecordSuffix(2),
            "Скорость: " + maxSpeed.ToString("0.0") + " км/ч" + RecordSuffix(3),
        };
        yield return StartCoroutine(ShowTextStatsPage("ИТОГИ ЗАБЕГА", totalsLines));

        // СОБРАНО/СБИТО: a small icon per unit collected/hit (repeated per
        // count) instead of a per-type text breakdown — see
        // ShowIconStatsPage, which itself collapses to one icon per TYPE
        // plus a "×N" badge once a page's total is too big to draw one
        // icon per unit. Types come straight from AchievementStats' own
        // dictionaries (keyed by the exact texture each object was shown
        // with, see PlayerController.EntityIcon) — every distinct object
        // ever collided with gets its own real picture here, not a fixed
        // shortlist of named categories.
        if (!_skipToEnd && stats != null)
        {
            var collectedTypes = new List<(Texture2D icon, int count)>();
            foreach (var kv in stats.CollectedByIcon)
                collectedTypes.Add((kv.Key, kv.Value));
            if (stats.UnknownCollected > 0)
                collectedTypes.Add((mysteryIcon, stats.UnknownCollected));
            if (stats.TotalCollected > 0)
                yield return StartCoroutine(ShowIconStatsPage("СОБРАНО", collectedTypes, "ИТОГО +" + stats.TotalCollected));

            if (!_skipToEnd)
            {
                var hitTypes = new List<(Texture2D icon, int count)>();
                foreach (var kv in stats.HitByIcon)
                    hitTypes.Add((kv.Key, kv.Value));
                if (stats.UnknownHit > 0)
                    hitTypes.Add((mysteryIcon, stats.UnknownHit));
                if (stats.TotalHit > 0)
                    yield return StartCoroutine(ShowIconStatsPage("СБИТО", hitTypes, "ИТОГО -" + stats.TotalHit));
            }

            if (!_skipToEnd)
            {
                var tricks = new List<string>();
                if (stats.RingTricks > 0) tricks.Add("Кольцо: +" + stats.RingTricks);
                if (stats.ArchTricks > 0) tricks.Add("Арка: +" + stats.ArchTricks);
                if (stats.LeapfrogTricks > 0) tricks.Add("Чехарда: +" + stats.LeapfrogTricks);
                if (stats.SyncTricks > 0) tricks.Add("Синхрон: +" + stats.SyncTricks);
                if (stats.HoverTricks > 0) tricks.Add("Зависание: +" + stats.HoverTricks);
                if (stats.BigRingTricks > 0) tricks.Add("Большое кольцо: +" + stats.BigRingTricks);
                if (stats.InfinityTricks > 0) tricks.Add("Бесконечность: +" + stats.InfinityTricks);
                if (tricks.Count > 0)
                    yield return StartCoroutine(ShowTextStatsPage("ТРЮКИ", tricks));
            }
        }

        if (statsTitle != null)
            statsTitle.gameObject.SetActive(false);
    }

    private IEnumerator ShowTextStatsPage(string title, List<string> lines)
    {
        if (statsTitle != null)
            statsTitle.text = title;

        for (int i = 0; i < statsRows.Length; i++)
        {
            bool used = i < lines.Count;
            if (statsRowRoots != null && i < statsRowRoots.Length && statsRowRoots[i] != null)
                statsRowRoots[i].SetActive(used);
            if (used && statsRows[i] != null)
                statsRows[i].text = lines[i];
        }

        yield return StartCoroutine(WaitPage(achievementsPageDuration));

        if (statsRowRoots != null)
            foreach (var root in statsRowRoots)
                if (root != null)
                    root.SetActive(false);
    }

    // Above this many total units, ShowIconStatsPage collapses to one icon
    // per TYPE with a "×N" badge instead of one icon per unit — a wall of
    // dozens of tiny repeated icons doesn't read any better than the count
    // would, and would blow well past the icon pool besides.
    private const int IconGroupThreshold = 20;

    // СОБРАНО/СБИТО's own page kind — normally a grid of small icons (one
    // per unit, repeated per count) plus one "ИТОГО ±N" line, no per-type
    // text. Once a page's total exceeds IconGroupThreshold, groups instead:
    // one icon per type plus a "×N" badge (statsIconCountLabels).
    private IEnumerator ShowIconStatsPage(string title, List<(Texture2D icon, int count)> types, string totalLine)
    {
        if (statsTitle != null)
            statsTitle.text = title;

        int totalUnits = 0;
        foreach (var t in types)
            if (t.icon != null)
                totalUnits += t.count;
        bool grouped = totalUnits > IconGroupThreshold;

        int slot = 0;
        if (statsIconSlots != null)
        {
            if (grouped)
            {
                foreach (var t in types)
                {
                    if (t.icon == null || t.count <= 0 || slot >= statsIconSlots.Length)
                        continue;
                    statsIconSlots[slot].gameObject.SetActive(true);
                    statsIconSlots[slot].texture = t.icon;
                    if (statsIconCountLabels != null && slot < statsIconCountLabels.Length && statsIconCountLabels[slot] != null)
                    {
                        statsIconCountLabels[slot].text = "×" + t.count;
                        statsIconCountLabels[slot].gameObject.SetActive(true);
                    }
                    slot++;
                }
            }
            else
            {
                foreach (var t in types)
                {
                    if (t.icon == null)
                        continue;
                    for (int i = 0; i < t.count && slot < statsIconSlots.Length; i++)
                    {
                        statsIconSlots[slot].gameObject.SetActive(true);
                        statsIconSlots[slot].texture = t.icon;
                        slot++;
                    }
                }
            }
            for (int i = slot; i < statsIconSlots.Length; i++)
                statsIconSlots[i].gameObject.SetActive(false);
        }
        // Ungrouped: no badges at all. Grouped: only whichever badges were
        // actually assigned above (slot of them) stay on.
        if (statsIconCountLabels != null)
            for (int i = grouped ? slot : 0; i < statsIconCountLabels.Length; i++)
                if (statsIconCountLabels[i] != null)
                    statsIconCountLabels[i].gameObject.SetActive(false);

        if (statsTotalText != null)
        {
            statsTotalText.text = totalLine;
            statsTotalText.gameObject.SetActive(true);
        }

        yield return StartCoroutine(WaitPage(achievementsPageDuration));

        if (statsIconSlots != null)
            foreach (var icon in statsIconSlots)
                if (icon != null)
                    icon.gameObject.SetActive(false);
        if (statsIconCountLabels != null)
            foreach (var label in statsIconCountLabels)
                if (label != null)
                    label.gameObject.SetActive(false);
        if (statsTotalText != null)
            statsTotalText.gameObject.SetActive(false);
    }

    // The real per-category top-3 tables (photo slots included), one at a
    // time — shown once, right after the photo, before returning to the
    // start screen (which has its own copy of these same tables).
    private IEnumerator ShowLeaderboardTables()
    {
        if (leaderboardRoot != null)
            leaderboardRoot.SetActive(true);

        try
        {
            foreach (var page in leaderboardPages)
            {
                if (page == null)
                    continue;

                page.SetActive(true);
                yield return StartCoroutine(WaitPage(achievementsPageDuration));
                page.SetActive(false);

                if (_skipToEnd)
                    yield break;
            }
        }
        finally
        {
            if (leaderboardRoot != null)
                leaderboardRoot.SetActive(false);
        }
    }

    // Any button/gesture press during this wait sets _skipToEnd — every
    // remaining stage of the post-win recap (RunSequence) checks that and
    // bails straight to the reload instead of waiting out the rest, so an
    // arcade cabinet returns to attract-mode promptly for the next player.
    private IEnumerator WaitPage(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            if (AnyInputPressed())
            {
                _skipToEnd = true;
                yield break;
            }
            t += Time.deltaTime;
            yield return null;
        }
    }

    // The actual player-facing control keys (player-right's own IJKL,
    // player-left's own WASD, Space/Return for start/confirm) — NOT
    // Input.anyKeyDown, which used to fire on literally any key including
    // OS-level shortcuts (e.g. a screenshot key combo) that have nothing to
    // do with the game, skipping the whole recap by accident. F1/Q (help
    // panel toggle) are deliberately excluded too, same reasoning.
    private static readonly KeyCode[] SkipKeys =
    {
        KeyCode.I, KeyCode.J, KeyCode.K, KeyCode.L,
        KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D,
        KeyCode.Space, KeyCode.Return,
    };

    private static bool AnyInputPressed()
    {
        foreach (var key in SkipKeys)
            if (Input.GetKeyDown(key))
                return true;

        foreach (var gesture in FindObjectsByType<GestureInput>())
        {
            if (gesture.JumpDown || gesture.DuckHeld || gesture.LeanLeftDown || gesture.LeanRightDown)
                return true;
        }
        return false;
    }

    // A run can land in several of the 4 leaderboards at once (e.g. both
    // fastest time and highest score) — one photo covers the whole run
    // regardless, not one per category (sitting through a full ~10s
    // capture per qualifying category, up to 4 in a row for a run that
    // sweeps every leaderboard, read as the capture being stuck repeating
    // instead of finishing). No separate on-screen "NEW RECORD: X — N
    // МЕСТО" reveal anymore — ИТОГИ ЗАБЕГА's own inline tags already show
    // that, this just handles the webcam capture (smile + 5s countdown, if
    // a camera is available — see PlayerPhotoCapture) that used to follow it.
    private IEnumerator CaptureRecordPhoto(List<HighScoreManager.NewRecord> records)
    {
        if (PlayerPhotoCapture.Instance == null || records.Count == 0)
            yield break;

        HighScoreManager.NewRecord best = records[0];
        foreach (var r in records)
            if (r.Rank < best.Rank)
                best = r; // headline the strongest placement (rank 1 if any)

        string message = best.Rank == 1
            ? "НОВЫЙ РЕКОРД!\n" + best.CategoryName
            : best.CategoryName + " — " + best.Rank + " МЕСТО!";

        if (newRecordAnnounceText != null)
        {
            newRecordAnnounceText.SetActive(true);
            yield return new WaitForSeconds(newRecordAnnounceDuration);
            newRecordAnnounceText.SetActive(false);
        }

        // statsBackdrop is the announce text's own background (see its
        // sortingOrder comment) so it has to stay up through that — but it
        // has no business floating behind the webcam countdown either, same
        // reasoning as winTextRoot just above in RunSequence. Caller's own
        // finally block still hides it again afterward, harmlessly.
        if (statsBackdrop != null)
            statsBackdrop.SetActive(false);

        string savedPath = null;
        yield return StartCoroutine(PlayerPhotoCapture.Instance.CaptureForRecord(message, p => savedPath = p));
        if (savedPath != null && HighScoreManager.Instance != null)
        {
            // Same photo represents the whole run — attach it to every
            // category it qualified for, not just the headline one.
            foreach (var r in records)
                HighScoreManager.Instance.SetPhotoPath(r.CategoryIndex, r.Rank, savedPath);
        }
    }

    private IEnumerator FadeOutEntities(MovingEntity[] entities)
    {
        var startScales = new Vector3[entities.Length];
        for (int i = 0; i < entities.Length; i++)
            if (entities[i] != null)
                startScales[i] = entities[i].transform.localScale;

        float t = 0f;
        while (t < entityFadeDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / entityFadeDuration);
            for (int i = 0; i < entities.Length; i++)
            {
                if (entities[i] == null)
                    continue;
                entities[i].transform.localScale = Vector3.Lerp(startScales[i], Vector3.zero, p);
            }
            yield return null;
        }

        foreach (var entity in entities)
            if (entity != null)
                Destroy(entity.gameObject);
    }

    private IEnumerator FlyPlayersAway(PlayerController[] players)
    {
        var starts = new Vector3[players.Length];
        var startScales = new Vector3[players.Length];
        var targets = new Vector3[players.Length];
        var targetScales = new Vector3[players.Length];

        for (int i = 0; i < players.Length; i++)
        {
            Transform t = players[i].transform;
            starts[i] = t.position;
            startScales[i] = t.localScale;
            targets[i] = starts[i] + new Vector3(0f, flyHeightGain, flyDistanceGain);
            targetScales[i] = startScales[i] * 0.05f;
        }

        float elapsed = 0f;
        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / flyDuration);
            float eased = p * p; // ease-in — starts slow, rockets off toward the end

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null)
                    continue;
                Transform t = players[i].transform;
                t.position = Vector3.Lerp(starts[i], targets[i], eased);
                t.localScale = Vector3.Lerp(startScales[i], targetScales[i], eased);
            }
            yield return null;
        }
    }
}
}
