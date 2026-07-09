using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Full-screen "animal VS robot" transition screen shown between the Sunrise Meadows world map
// and gameplay. Built once by WorldMapController and toggled via Show(levelIndex)/Hide(), same
// self-contained runtime-built pattern as everything else in this project's UI layer.
//
// ANIMATION REDESIGN (2026-07-21) — replaced the old static "tap anywhere to continue" screen
// with a scripted, non-interactive motion sequence per the user's explicit shot list: backdrop
// -> level header pops in -> animal card slides in from the left / robot card slides in from the
// right, meeting as the VS graphic pops in between them -> hold -> countdown 3, 2, 1 (each pops
// in, holds, pops out) -> READY! -> auto-launch into gameplay. No player input during the
// sequence — the old ✕ close button and tap-to-continue catcher are both gone (removed per
// explicit instruction; there is currently no way to back out once a marker is tapped).
//
// LEVEL HEADER ART — indexed by level (0-based), same fallback-to-index-0 convention as
// LevelCompleteManager's/LevelFailedManager's clip arrays (see Core/ scripts): Show(levelIndex)
// picks _levelHeaderSprites[levelIndex] if that slot is wired, else falls back to index 0
// (LevelHeader1.png). Originally hardcoded to LevelHeader1.png only ("concentrating on level 1
// only" per the original explicit instruction) — extended once LevelHeader-equivalent art for
// levels 2+ existed (level2.png/level3-removebg-preview.png/level4.png/level5.png, wired by
// SceneSetup.WireMatchUpCards).
//
// MATCHUP NOTE — both cards are read fresh from GameManager.GetLevelData(levelIndex) on every
// Show(): the animal card is birds[0] and the robot card is robots[0].robotType for THAT level.
// Player-chosen-animal (a possible future feature once animals are unlockable) is intentionally
// NOT built here — Show() always uses the level's birds[0].
//
// AUDIO NOTE — the countdown beat (3/2/1/READY) plays Countdown.mp3 once, and the four numeral
// pops are timed to its four beeps rather than an arbitrary fixed pace (added 2026-07-07, see
// CountdownBeat's timing comment). Every other beat (whoosh/impact stings) is still a hook point
// for a future AudioManager.Play(...) call.
public class MatchUpScreen : MonoBehaviour
{
    // Card art — 8-slot array indexed by AnimalType, 3-slot array indexed by RobotType. Sourced
    // from Assets/Sprites/UI/MatchUp/ (see SceneSetup.WireMatchUpCards) — a dedicated art set for
    // this screen, distinct from the HUD's Assets/Sprites/UI/Cards/. Only Cluck/Bessie (animal)
    // and Basic/Harvester/SemiHarvester (robot) have art there today, matching what L01-L02
    // actually use (L03-L06 removed 2026-07-09 — see LevelDataGenerator.cs).
    //
    // NOT [SerializeField] on this component — MatchUpScreen is a child GameObject created at
    // runtime inside WorldMapController.BuildUI(), which only executes when Awake() actually
    // fires (Play mode, or immediately after a fresh AddComponent). SceneSetup's batch
    // "Wire Scene References" pass opens the scene without entering Play mode, so it never
    // reaches a live instance of this component to wire a SerializeField on — confirmed
    // empirically 2026-07-16 (the old LevelPreviewCard had the exact same silent gap). Instead,
    // WorldMapController holds these references (which DO persist, since that component lives
    // directly in the saved scene) and threads them through Init() below.
    private Sprite[] _animalCardSprites;
    private Sprite[] _robotCardSprites;

    private Sprite _backgroundSprite;   // MatchUpBackground.png — full-bleed backdrop
    private Sprite _vsSprite;           // VS.png
    private Sprite[] _levelHeaderSprites; // level-indexed, fallback to index 0 — see class comment above
    private Sprite _countdown3Sprite;
    private Sprite _countdown2Sprite;
    private Sprite _countdown1Sprite;
    private Sprite _countdownReadySprite;
    private AudioClip _countdownClip;   // Countdown.mp3 — see CountdownBeat's timing comment
    private AudioSource _countdownAudioSrc;
    private Sprite _cluckFlySprite;     // Cluck_InFlight.png — see the CluckFlyBy() timing comment

    private GameObject      _panel;
    private Image            _fadeOverlayImg; // fades to opaque black just before auto-launch, see PlaySequence step 6
    private Image            _bgImg;
    private RectTransform    _headerRT;
    private Image             _headerImg;
    private RectTransform    _animalRT;
    private Image             _animalImg;
    private RectTransform    _robotRT;
    private Image             _robotImg;
    private TextMeshProUGUI  _robotFallbackLabel;
    // Second robot card — only shown when a level's robots[] contains a SECOND distinct
    // RobotType beyond robots[0] (e.g. L02 "Harvest Yard": Harvester + SemiHarvester). Sits IN
    // FRONT of the primary card in render order (see BuildUI — created after "RobotCard") and
    // offset, fanned out "like a deck of cards" (added 2026-07-09 per explicit request; changed
    // from behind to in-front the same day per follow-up "make the new card slide over, not
    // under") rather than a second full slide-in beat.
    private RectTransform    _robot2RT;
    private Image             _robot2Img;
    private RectTransform    _vsRT;
    private Image             _vsImg;
    private RectTransform    _countdownRT;
    private Image             _countdownImg;
    private RectTransform    _cluckFlyRT;  // flies across the bottom behind the cards, see CluckFlyBy()
    private Image             _cluckFlyImg;

    private int       _levelIndex;
    private bool      _hasData;
    private Coroutine _sequenceRoutine;

    // Rest positions for the two cards / VS graphic (2026-07-23 pass — "fuller" layout per user
    // feedback: bigger cards, closer together, touching VS). Cards are 560x560 (half-width 280),
    // VS is 220x220 (half-width 110); animal/robot X = ∓(110+280) = ∓390 so each card's inner
    // edge exactly touches VS's outer edge. All three sit at the safe area's vertical centre
    // (y=0) — see BuildUI's SafeArea section for why centring here is the robust choice rather
    // than a fixed offset guessed against the full 1920x1080 reference size.
    private static readonly Vector2 AnimalRestPos = new(-390f, 0f);
    private static readonly Vector2 RobotRestPos  = new(390f, 0f);
    private static readonly Vector2 VsRestPos     = new(0f, 0f);
    private const float OffscreenOffset = 900f; // how far off-screen each card starts before sliding in
    private const float CardRotationDeg = 10f;  // outward tilt — animal +10 (leans left), robot -10 (leans right)

    // Second robot card's rest position, relative to RobotRestPos — offset down-right and behind
    // (lower sibling index, see BuildUI) the primary robot card so it peeks out from underneath,
    // reading as a second card fanned in a deck rather than a separate full card. Raised from an
    // original (55,-45) (2026-07-09, user-reported "the second card does not come out over the
    // first") — against 560px/500px cards that only left a ~25px sliver visible past the primary
    // card's edge, easy to miss entirely. This offset intentionally pushes a large enough portion
    // of the card past the primary's bounds to read clearly as a second card, not just a shadow.
    private static readonly Vector2 Robot2RestOffset = new(130f, -100f);
    private const float Robot2RotationDeg = -22f; // steeper tilt than the primary robot card's -10

    // Countdown.mp3 timing (measured via waveform analysis, 2026-07-07): four beeps — three short
    // taps at 0.02s/1.02s/2.02s (1.0s apart) for 3/2/1, then one longer confirmation tone starting
    // at 3.02s and running to the clip's end (~4.05s total). Each numeral's pop-in/hold/pop-out is
    // paced to total exactly one beep interval, so consecutive numerals land back-to-back on each
    // beep instead of the old arbitrary fixed 2s-per-numeral pacing.
    private const float CountdownBeepInterval = 1.0f;
    private const float CountdownDigitPopIn   = 0.15f;
    private const float CountdownDigitPopOut  = 0.30f;
    private const float CountdownReadyPopIn   = 0.25f;
    private const float CountdownClipLength   = 4.05f; // Countdown.mp3's measured duration

    // Cluck flies across the bottom of the screen, behind the cards, timed to the countdown —
    // starts the instant "3" appears (Countdown.mp3 starts playing) and finishes ("lands")
    // exactly as the READY hold ends, i.e. over the same CountdownClipLength duration. User-
    // requested 2026-07-09. Y is a fixed height above the safe area's bottom edge (same
    // anchor style as the Countdown numeral below); X range covers off-screen-left to
    // off-screen-right so the whole flight is a clean pass through frame.
    private const float CluckFlyY      = 110f;
    private const float CluckFlyStartX = -1400f;
    private const float CluckFlyEndX   = 1400f;

    public void Init(Sprite squareSpr, Sprite backgroundSprite, Sprite vsSprite,
        Sprite[] levelHeaderSprites,
        Sprite countdown3Sprite, Sprite countdown2Sprite, Sprite countdown1Sprite, Sprite countdownReadySprite,
        AudioClip countdownClip, Sprite cluckFlySprite,
        Sprite[] animalCardSprites, Sprite[] robotCardSprites)
    {
        _backgroundSprite      = backgroundSprite;
        _vsSprite               = vsSprite;
        _levelHeaderSprites     = levelHeaderSprites;
        _countdown3Sprite       = countdown3Sprite;
        _countdown2Sprite       = countdown2Sprite;
        _countdown1Sprite       = countdown1Sprite;
        _countdownReadySprite   = countdownReadySprite;
        _countdownClip          = countdownClip;
        _cluckFlySprite         = cluckFlySprite;
        _animalCardSprites      = animalCardSprites;
        _robotCardSprites       = robotCardSprites;
        BuildUI(squareSpr);
    }

    public void Show(int levelIndex)
    {
        _levelIndex = levelIndex;

        Sprite header = (_levelHeaderSprites != null && levelIndex < _levelHeaderSprites.Length && _levelHeaderSprites[levelIndex] != null)
            ? _levelHeaderSprites[levelIndex]
            : (_levelHeaderSprites != null && _levelHeaderSprites.Length > 0 ? _levelHeaderSprites[0] : null);
        _headerImg.sprite  = header;
        _headerImg.enabled = header != null;

        var data = GameManager.Instance?.GetLevelData(levelIndex);
        _hasData = data != null;

        if (data != null)
        {
            AnimalType animal = (data.birds != null && data.birds.Length > 0) ? data.birds[0] : AnimalType.Cluck;
            RobotType  robot  = (data.robots != null && data.robots.Length > 0) ? data.robots[0].robotType : RobotType.Basic;

            Sprite animalSpr = _animalCardSprites != null && (int)animal < _animalCardSprites.Length
                ? _animalCardSprites[(int)animal] : null;
            _animalImg.sprite  = animalSpr;
            _animalImg.enabled = animalSpr != null;

            Sprite robotSpr = _robotCardSprites != null && (int)robot < _robotCardSprites.Length
                ? _robotCardSprites[(int)robot] : null;
            _robotImg.sprite            = robotSpr;
            _robotImg.enabled           = robotSpr != null;
            _robotFallbackLabel.enabled = robotSpr == null;
            _robotFallbackLabel.text    = robot == RobotType.Harvester ? "HARVESTER\nROBOT" : "ROBOT";

            // Second card — only shown if this level's robots[] contains a distinct RobotType
            // beyond robots[0] (e.g. L02 "Harvest Yard": Harvester then SemiHarvester).
            RobotType? robot2 = null;
            if (data.robots != null)
            {
                foreach (var r in data.robots)
                {
                    if (r.robotType != robot) { robot2 = r.robotType; break; }
                }
            }
            Sprite robot2Spr = robot2.HasValue && _robotCardSprites != null && (int)robot2.Value < _robotCardSprites.Length
                ? _robotCardSprites[(int)robot2.Value] : null;
            _robot2Img.sprite  = robot2Spr;
            _robot2Img.enabled = robot2Spr != null;
        }
        else
        {
            // Level data doesn't exist yet (only L01-L02 are authored right now, out of 18
            // marker slots — see CLAUDE.md Gap Analysis). The sequence still plays (see SCOPE
            // NOTE at the top of this file) but skips the auto-launch at the end and just
            // returns to the map instead of crashing on a null level.
            _animalImg.enabled          = false;
            _robotImg.enabled           = false;
            _robot2Img.enabled          = false;
            _robotFallbackLabel.enabled = true;
            _robotFallbackLabel.text    = "COMING\nSOON";
        }

        // Menu music (looping under the world map/landing page) is paused for the whole
        // match-up/countdown sequence so only the countdown SFX plays underneath it — see
        // AudioManager.PauseMenuMusic. Resumed in PlaySequence's "no level data" branch below if
        // the player ends up back on the map without launching; if the level does launch, the
        // Idle->Playing state transition already swaps to gameplay music, so no resume is needed
        // on that path.
        AudioManager.PauseMenuMusic();

        _panel.SetActive(true);
        if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);
        _sequenceRoutine = StartCoroutine(PlaySequence());
    }

    public void Hide()
    {
        if (_sequenceRoutine != null) { StopCoroutine(_sequenceRoutine); _sequenceRoutine = null; }
        if (_countdownAudioSrc != null && _countdownAudioSrc.isPlaying) _countdownAudioSrc.Stop();
        _panel.SetActive(false);
    }

    // ── Motion sequence ───────────────────────────────────────────────────────
    // Backdrop -> header pop -> cards slide in from both sides, VS slamming in between them right
    // as the cards land -> 2s hold -> countdown 3/2/1 (2s each) -> READY! -> whole panel fades ->
    // auto-launch. Entirely non-interactive; nothing here waits on player input.
    //
    // TIMING (2026-07-22, slowed down + retimed per user feedback — "too fast", "VS does not
    // seem to appear"). The whole sequence previously ran in well under 4 seconds total, with the
    // VS pop a lone 0.20s beat sandwiched between two other fast beats — easy to miss even though
    // nothing was actually broken in the visibility logic (sprite/enabled/scale all checked out
    // statically). Fixed by both slowing every beat down and restructuring cards+VS into one
    // continuous "clash" (see CardsAndVsClash below) so the VS pop is no longer a separate,
    // easy-to-miss afterthought — it's timed to complete exactly as the cards land, and it's
    // bigger (peak overshoot 1.4 vs the header's 1.15) so it reads as a "slam," not a fade-in.
    IEnumerator PlaySequence()
    {
        // Reset every animated element to its pre-entrance state.
        _fadeOverlayImg.color    = new Color(0f, 0f, 0f, 0f);
        _headerRT.localScale     = Vector3.zero;
        _animalRT.anchoredPosition = AnimalRestPos + new Vector2(-OffscreenOffset, 0f);
        _robotRT.anchoredPosition  = RobotRestPos + new Vector2(OffscreenOffset, 0f);
        _robot2RT.anchoredPosition = RobotRestPos + Robot2RestOffset + new Vector2(OffscreenOffset, 0f);
        _vsRT.localScale         = Vector3.zero;
        _vsImg.enabled           = _vsSprite != null; // defensive re-assert, see class comment
        _countdownImg.enabled    = false;
        _cluckFlyRT.anchoredPosition = new Vector2(CluckFlyStartX, CluckFlyY); // in case Show() re-fires mid-flight

        // 1) Level header pops in. (Future hook: AudioManager whoosh/pop SFX.)
        yield return PopIn(_headerRT, 0.6f);

        // 2) Cards slide in from both sides and meet in the middle, VS slamming in between them
        // right as they land — one continuous beat, not three separate ones.
        yield return CardsAndVsClash(slideDuration: 0.9f, vsPopDuration: 0.4f, vsPeak: 1.4f);

        // 2b) Second robot card ("deck of cards" overlay) slides in separately, after a short
        // delay following the primary clash above — user-requested 2026-07-09: "first the one -
        // delay - then the second" (previously slid in at the same time as the primary card,
        // which read as one single motion rather than two distinct arrivals). Skipped entirely
        // for levels with only one robot type — Show() already left _robot2Img disabled there.
        if (_robot2Img.enabled)
        {
            yield return new WaitForSecondsRealtime(0.3f);
            yield return SlideRobot2CardIn(0.5f);
        }

        // 3) Hold so the full matchup registers before the countdown starts — 2s per explicit
        // instruction.
        yield return new WaitForSecondsRealtime(2.0f);

        // 4) Countdown: 3, 2, 1 — synced to Countdown.mp3's three short beeps (see the timing
        // constants above). The clip plays once, right as "3" appears; each numeral's
        // pop-in/hold/pop-out totals exactly one beep interval so the next numeral lands on the
        // next beep. Only one numeral is ever visible at a time.
        if (_countdownClip != null && _countdownAudioSrc != null)
        {
            _countdownAudioSrc.mute = !AudioManager.SfxEnabled;
            _countdownAudioSrc.clip = _countdownClip;
            _countdownAudioSrc.Play();
        }
        // Cluck flies across the bottom of the screen, behind the cards, over the exact same
        // duration as the countdown — started here (not yielded) so it runs concurrently with
        // the countdown beats below rather than blocking them.
        if (_cluckFlyImg.enabled) StartCoroutine(CluckFlyBy(CountdownClipLength));
        yield return CountdownBeat(_countdown3Sprite, CountdownBeepInterval);
        yield return CountdownBeat(_countdown2Sprite, CountdownBeepInterval);
        yield return CountdownBeat(_countdown1Sprite, CountdownBeepInterval);

        // 5) READY! — lands on the clip's longer fourth tone at 3.02s. Same pop-in language, held
        // until the clip finishes, does not pop back out (the whole panel fades instead,
        // immediately below).
        _countdownImg.sprite  = _countdownReadySprite;
        _countdownImg.enabled = _countdownReadySprite != null;
        Color rc = _countdownImg.color; rc.a = 1f; _countdownImg.color = rc;
        yield return AnimateScale(_countdownRT, 0f, 1f, CountdownReadyPopIn, bounce: true);
        yield return new WaitForSecondsRealtime(
            CountdownClipLength - 3f * CountdownBeepInterval - CountdownReadyPopIn);

        // 6) Fade to solid black, then auto-launch into gameplay — no tap required.
        //
        // BUG FIX (2026-07-24) — this used to fade the whole panel's CanvasGroup to *transparent*
        // before launching. MatchUpScreen is a sibling of the World Map's own background/pins
        // inside the same shared canvas (see WorldMapController.BuildUI, "added last so it
        // renders on top"), so making it transparent revealed the map underneath for the entire
        // 0.4s fade, before gameplay had even started — reported as "the game navigates back to
        // the meadows sunrise scene ... before moving to gameplay." Fixed by fading an opaque
        // black overlay IN instead (0 -> 1 alpha, not the reverse) so the screen goes to solid
        // black rather than see-through. GameManager.ForceStartLevel() then triggers
        // WorldMapController.HidePanel(), which deactivates the whole map canvas — including this
        // screen, since it's a child of that canvas — atomically in the same frame; the last
        // thing rendered before that cut is solid black, never a peek at stale map/card content.
        yield return FadeImageAlpha(_fadeOverlayImg, 0f, 1f, 0.4f);
        if (_hasData)
            GameManager.Instance?.ForceStartLevel(_levelIndex);
        else
        {
            _panel.SetActive(false); // no level to launch — just return to the map
            AudioManager.ResumeMenuMusic(); // no Playing transition will happen, so resume it ourselves
        }
        _sequenceRoutine = null;
    }

    // Cards slide in from off-screen; the VS pop starts partway through that slide (timed so it
    // finishes exactly as the cards land) rather than after the slide fully completes — this is
    // what makes it read as "cards meet, VS slams in between them" as a single beat instead of
    // three sequential ones.
    IEnumerator CardsAndVsClash(float slideDuration, float vsPopDuration, float vsPeak)
    {
        var slideRoutine = StartCoroutine(SlideCardsIn(slideDuration));
        float vsDelay = Mathf.Max(0f, slideDuration - vsPopDuration);
        yield return new WaitForSecondsRealtime(vsDelay);
        var vsRoutine = StartCoroutine(PopIn(_vsRT, vsPopDuration, vsPeak));
        yield return slideRoutine;
        yield return vsRoutine;
    }

    // One numeral: pop in, hold, pop back out (scale + fade), then hide. totalDuration is paced
    // to exactly one Countdown.mp3 beep interval (see the class-level timing constants above) so
    // consecutive numerals land back-to-back on each beep — pop-in and pop-out are fixed lengths,
    // the remainder becomes the hold.
    IEnumerator CountdownBeat(Sprite spr, float totalDuration)
    {
        _countdownImg.sprite  = spr;
        _countdownImg.enabled = spr != null;
        Color c = _countdownImg.color; c.a = 1f; _countdownImg.color = c;

        float hold = Mathf.Max(0f, totalDuration - CountdownDigitPopIn - CountdownDigitPopOut);
        yield return AnimateScale(_countdownRT, 0f, 1f, CountdownDigitPopIn, bounce: true);
        yield return new WaitForSecondsRealtime(hold);
        yield return AnimateScaleAndFade(_countdownRT, _countdownImg, 1f, 0f, CountdownDigitPopOut);

        _countdownImg.enabled = false;
    }

    // Scale-pop entrance for a persistent element (header / VS) that stays visible afterward:
    // 0 -> overshoot -> settles at 1, using the same ease-out-back language as the Level
    // Complete panel's star pop-in (HUDController.PopStar), just starting from 0 instead of 1.
    IEnumerator PopIn(RectTransform rt, float duration, float peak = 1.15f) =>
        AnimateScale(rt, 0f, 1f, duration, bounce: true, peak: peak);

    IEnumerator SlideCardsIn(float duration)
    {
        Vector2 animalStart = _animalRT.anchoredPosition;
        Vector2 robotStart  = _robotRT.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            _animalRT.anchoredPosition = Vector2.Lerp(animalStart, AnimalRestPos, t);
            _robotRT.anchoredPosition  = Vector2.Lerp(robotStart, RobotRestPos, t);
            yield return null;
        }
        _animalRT.anchoredPosition = AnimalRestPos;
        _robotRT.anchoredPosition  = RobotRestPos;
    }

    // Second robot card's own slide-in, run separately (after a delay) from the primary clash
    // above — see the "2b" step comment in PlaySequence for why this was split out.
    IEnumerator SlideRobot2CardIn(float duration)
    {
        Vector2 start = _robot2RT.anchoredPosition;
        Vector2 rest  = RobotRestPos + Robot2RestOffset;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            _robot2RT.anchoredPosition = Vector2.Lerp(start, rest, t);
            yield return null;
        }
        _robot2RT.anchoredPosition = rest;
    }

    // Cluck flies across the bottom of the screen, behind the cards, over `duration` (matched to
    // CountdownClipLength by the caller so it "lands" — finishes its pass — exactly as the
    // countdown ends). Plays the cannon-shot/launch sound at the moment it starts, reusing the
    // existing gameplay launch SFX rather than a new dedicated clip.
    IEnumerator CluckFlyBy(float duration)
    {
        AudioManager.Play(AudioManager.Sound.Launch);
        _cluckFlyRT.anchoredPosition = new Vector2(CluckFlyStartX, CluckFlyY);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _cluckFlyRT.anchoredPosition = new Vector2(Mathf.Lerp(CluckFlyStartX, CluckFlyEndX, t), CluckFlyY);
            yield return null;
        }
        _cluckFlyRT.anchoredPosition = new Vector2(CluckFlyEndX, CluckFlyY);
    }

    // from -> to scale over duration; bounce=true uses an ease-out-back overshoot (only sensible
    // for a 0->1 entrance, peak configurable — see PopScale), bounce=false is a plain SmoothStep
    // (used for the 1->0 exit).
    IEnumerator AnimateScale(RectTransform rt, float from, float to, float duration, bool bounce, float peak = 1.15f)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t     = Mathf.Clamp01(elapsed / duration);
            float value = bounce ? Mathf.LerpUnclamped(from, to, PopScale(t, peak)) : Mathf.SmoothStep(from, to, t);
            rt.localScale = Vector3.one * value;
            yield return null;
        }
        rt.localScale = Vector3.one * to;
    }

    // Scale + fade together (used for the countdown numerals' pop-out).
    IEnumerator AnimateScaleAndFade(RectTransform rt, Image img, float fromScale, float toScale, float duration)
    {
        Color baseColor = img.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            rt.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, t);
            Color c = baseColor; c.a = Mathf.Lerp(1f, 0f, t);
            img.color = c;
            yield return null;
        }
        rt.localScale = Vector3.one * toScale;
    }

    IEnumerator FadeImageAlpha(Image img, float from, float to, float duration)
    {
        Color c = img.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            img.color = c;
            yield return null;
        }
        c.a = to;
        img.color = c;
    }

    // Ease-out-back overshoot: 0 at t=0, peak at t=0.7, settles to 1 at t=1. Same curve shape as
    // HUDController.StarBounce, just starting from 0 instead of 1 since these elements pop in
    // from nothing rather than re-emphasising something already on screen. peak is configurable
    // per caller — a soft 1.15 for the header (should read as "arriving"), a bigger 1.4 for VS
    // (should read as "slamming in").
    static float PopScale(float t, float peak = 1.15f)
    {
        const float peakT = 0.70f;
        return t < peakT
            ? Mathf.SmoothStep(0f, peak, t / peakT)
            : Mathf.SmoothStep(peak, 1f, (t - peakT) / (1f - peakT));
    }

    // ── UI construction ────────────────────────────────────────────────────────

    void BuildUI(Sprite squareSpr)
    {
        var rootRT = gameObject.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = rootRT.offsetMax = Vector2.zero;
        _panel = gameObject;

        _countdownAudioSrc             = gameObject.AddComponent<AudioSource>();
        _countdownAudioSrc.playOnAwake = false;

        // Background — MatchUpBackground.png, full-bleed, deliberately OUTSIDE the SafeArea
        // below (should paint edge-to-edge including under a notch/rounded corner). Also blocks
        // raycasts to whatever is behind this screen (the world map markers) since
        // Image.raycastTarget defaults to true and this renders first/behind everything else.
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        _bgImg = bgGO.AddComponent<Image>();
        _bgImg.sprite         = _backgroundSprite != null ? _backgroundSprite : squareSpr;
        _bgImg.color          = _backgroundSprite != null ? Color.white : new Color(0.35f, 0.55f, 0.85f);
        _bgImg.preserveAspect = false;

        // SafeArea — every other element lives inside this, same pattern as
        // HUDController.BuildSafeArea/ApplySafeArea. Fixes a real clipping bug reported
        // 2026-07-23: the countdown numeral was rendering outside the device's actual safe
        // (non-notch/non-home-indicator) region on a real phone aspect ratio, even though it sat
        // safely within the flat 1920x1080 reference canvas math. A fixed pixel offset guessed
        // against the full reference resolution can never account for a real device's actual
        // inset, which varies per device — anchoring to the true Screen.safeArea does.
        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(transform, false);
        var safeRT = safeGO.AddComponent<RectTransform>();
        ApplySafeArea(safeRT);
        Transform safe = safeRT;

        // Cluck flying across the bottom of the screen, behind the cards — created FIRST inside
        // "safe" (before header/cards below) so it's the earliest sibling and therefore renders
        // behind all of them. See CluckFlyBy() for the countdown-synced timing. Bottom-anchored
        // like the countdown numeral below, animated purely via anchoredPosition.x in CluckFlyBy.
        var cluckFlyGO = new GameObject("CluckFlyBy");
        cluckFlyGO.transform.SetParent(safe, false);
        _cluckFlyRT = cluckFlyGO.AddComponent<RectTransform>();
        _cluckFlyRT.anchorMin        = new Vector2(0.5f, 0f);
        _cluckFlyRT.anchorMax        = new Vector2(0.5f, 0f);
        _cluckFlyRT.pivot            = new Vector2(0.5f, 0.5f);
        _cluckFlyRT.anchoredPosition = new Vector2(CluckFlyStartX, CluckFlyY);
        _cluckFlyRT.sizeDelta        = new Vector2(180f, 180f);
        _cluckFlyImg = cluckFlyGO.AddComponent<Image>();
        _cluckFlyImg.sprite         = _cluckFlySprite;
        _cluckFlyImg.enabled        = _cluckFlySprite != null;
        _cluckFlyImg.preserveAspect = true;
        _cluckFlyImg.raycastTarget  = false;

        // Level header — LevelHeader1.png (see SCOPE NOTE at the top of this file). Top-anchored
        // within the safe area with a small fixed inset, NOT centre-anchored with a guessed
        // offset — this guarantees it never clips the top edge regardless of how much the actual
        // safe rect shrinks on a given device (same reasoning as the countdown below, and the
        // same technique WorldMapController already uses for its corner-anchored buttons).
        // Enlarged considerably (560x280, still LevelHeader1.png's ~2:1 aspect) per user feedback
        // ("increase the header text sizing").
        var headerGO = new GameObject("LevelHeader");
        headerGO.transform.SetParent(safe, false);
        _headerRT = headerGO.AddComponent<RectTransform>();
        _headerRT.anchorMin        = new Vector2(0.5f, 1f);
        _headerRT.anchorMax        = new Vector2(0.5f, 1f);
        _headerRT.pivot            = new Vector2(0.5f, 1f);
        _headerRT.anchoredPosition = new Vector2(0f, -30f);
        _headerRT.sizeDelta        = new Vector2(560f, 280f);
        _headerImg = headerGO.AddComponent<Image>();
        _headerImg.enabled        = false; // set per-level in Show() — see _levelHeaderSprites
        _headerImg.preserveAspect = true;
        _headerImg.raycastTarget  = false;

        // Left card — player's animal. Enlarged (560x560, was 480) and moved closer to centre so
        // its inner edge touches VS (see AnimalRestPos/RobotRestPos/VsRestPos comment above),
        // tilted outward (see CardRotationDeg) per user feedback ("bring the cards closer
        // together and enlarge, rotate the cards slightly outwards, they should touch the VS").
        // Centred on the safe area (0.5,0.5) rather than the raw canvas, same reasoning as the
        // header/countdown but less at-risk since it's near the vertical middle, not an edge.
        var animGO = new GameObject("AnimalCard");
        animGO.transform.SetParent(safe, false);
        _animalRT = animGO.AddComponent<RectTransform>();
        _animalRT.anchorMin        = new Vector2(0.5f, 0.5f);
        _animalRT.anchorMax        = new Vector2(0.5f, 0.5f);
        _animalRT.pivot            = new Vector2(0.5f, 0.5f);
        _animalRT.anchoredPosition = AnimalRestPos;
        _animalRT.sizeDelta        = new Vector2(560f, 560f); // square framed portraits (500x500 source art)
        _animalRT.localEulerAngles = new Vector3(0f, 0f, CardRotationDeg);
        _animalImg = animGO.AddComponent<Image>();
        _animalImg.preserveAspect = true;
        _animalImg.raycastTarget  = false;

        // Right card — the robot(s) this level's player will face. Mirrors the animal card.
        var robotGO = new GameObject("RobotCard");
        robotGO.transform.SetParent(safe, false);
        _robotRT = robotGO.AddComponent<RectTransform>();
        _robotRT.anchorMin        = new Vector2(0.5f, 0.5f);
        _robotRT.anchorMax        = new Vector2(0.5f, 0.5f);
        _robotRT.pivot            = new Vector2(0.5f, 0.5f);
        _robotRT.anchoredPosition = RobotRestPos;
        _robotRT.sizeDelta        = new Vector2(560f, 560f);
        _robotRT.localEulerAngles = new Vector3(0f, 0f, -CardRotationDeg);
        _robotImg = robotGO.AddComponent<Image>();
        _robotImg.preserveAspect = true;
        _robotImg.raycastTarget  = false;

        // Fallback label shown inside the robot card slot when no dedicated art exists yet
        // or when the level has no data ("COMING SOON"). Follows the robot card's position, not
        // its rotation (a rotated text block reads worse than a rotated card) — a same-position
        // substitute for _robotImg, not a separate beat.
        _robotFallbackLabel = MakeLabel(safe, "RobotFallbackLabel",
            RobotRestPos, new Vector2(400f, 200f), 36f, new Color(0.25f, 0.20f, 0.12f));
        _robotFallbackLabel.fontStyle = FontStyles.Bold;

        // Second robot card — "deck of cards" overlay IN FRONT of the primary robot card (see
        // class comment + Robot2RestOffset/Robot2RotationDeg above). Created AFTER "RobotCard"
        // above so it sits later in sibling order and therefore renders on top of it (changed
        // 2026-07-09 — user-reported "make the new card slide over, not under"; originally
        // behind). Only shown by Show() when a level actually has a second distinct RobotType.
        // Slightly smaller than the primary card (500x500 vs 560x560) so it still reads as a
        // second card overlaying the first rather than a same-size card just offset to the side.
        var robot2GO = new GameObject("RobotCard2");
        robot2GO.transform.SetParent(safe, false);
        _robot2RT = robot2GO.AddComponent<RectTransform>();
        _robot2RT.anchorMin        = new Vector2(0.5f, 0.5f);
        _robot2RT.anchorMax        = new Vector2(0.5f, 0.5f);
        _robot2RT.pivot            = new Vector2(0.5f, 0.5f);
        _robot2RT.anchoredPosition = RobotRestPos + Robot2RestOffset;
        _robot2RT.sizeDelta        = new Vector2(500f, 500f);
        _robot2RT.localEulerAngles = new Vector3(0f, 0f, Robot2RotationDeg);
        _robot2Img = robot2GO.AddComponent<Image>();
        _robot2Img.preserveAspect = true;
        _robot2Img.raycastTarget  = false;
        _robot2Img.enabled        = false; // only enabled by Show() when a second robot type exists

        // VS graphic, centred between the two cards, touching both — enlarged (220x220, was 160)
        // to match the bigger cards.
        var vsGO = new GameObject("VS");
        vsGO.transform.SetParent(safe, false);
        _vsRT = vsGO.AddComponent<RectTransform>();
        _vsRT.anchorMin        = new Vector2(0.5f, 0.5f);
        _vsRT.anchorMax        = new Vector2(0.5f, 0.5f);
        _vsRT.pivot            = new Vector2(0.5f, 0.5f);
        _vsRT.anchoredPosition = VsRestPos;
        _vsRT.sizeDelta        = new Vector2(220f, 220f);
        _vsImg = vsGO.AddComponent<Image>();
        _vsImg.sprite         = _vsSprite;
        _vsImg.enabled        = _vsSprite != null;
        _vsImg.preserveAspect = true;
        _vsImg.raycastTarget  = false;

        // Countdown display — bottom-anchored within the safe area with a small fixed inset (the
        // exact clipping fix described in the SafeArea comment above), not centre-anchored with a
        // guessed negative offset like the first pass. Enlarged (260x260, was 220) and shared by
        // all four sprites (3/2/1/READY) via this one RectTransform, so they're all identically
        // sized ("resize the countdown sprites to the same sizing") by construction.
        var cdGO = new GameObject("Countdown");
        cdGO.transform.SetParent(safe, false);
        _countdownRT = cdGO.AddComponent<RectTransform>();
        _countdownRT.anchorMin        = new Vector2(0.5f, 0f);
        _countdownRT.anchorMax        = new Vector2(0.5f, 0f);
        _countdownRT.pivot            = new Vector2(0.5f, 0f);
        _countdownRT.anchoredPosition = new Vector2(0f, 40f);
        _countdownRT.sizeDelta        = new Vector2(260f, 260f);
        _countdownImg = cdGO.AddComponent<Image>();
        _countdownImg.preserveAspect = true;
        _countdownImg.raycastTarget  = false;
        _countdownImg.enabled        = false;

        // Fade-to-black overlay — full-bleed, added LAST (outside the safe area, like the
        // background) so it renders on top of literally everything else in this screen. See
        // PlaySequence step 6 for why this fades TO OPAQUE rather than the screen fading to
        // transparent.
        var fadeGO = new GameObject("FadeOverlay");
        fadeGO.transform.SetParent(transform, false);
        var fadeRT = fadeGO.AddComponent<RectTransform>();
        fadeRT.anchorMin = Vector2.zero;
        fadeRT.anchorMax = Vector2.one;
        fadeRT.offsetMin = fadeRT.offsetMax = Vector2.zero;
        _fadeOverlayImg = fadeGO.AddComponent<Image>();
        _fadeOverlayImg.sprite        = squareSpr;
        _fadeOverlayImg.color         = new Color(0f, 0f, 0f, 0f);
        _fadeOverlayImg.raycastTarget = false;

        _panel.SetActive(false);
    }

    // Same technique as HUDController.ApplySafeArea — maps Screen.safeArea (actual device pixels)
    // to normalized anchors so children of this RectTransform can never render into a notch,
    // rounded corner, or home-indicator zone regardless of device.
    static void ApplySafeArea(RectTransform rt)
    {
        if (Screen.width <= 0 || Screen.height <= 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
        }
        else
        {
            Rect safe = Screen.safeArea;
            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= Screen.width;  min.y /= Screen.height;
            max.x /= Screen.width;  max.y /= Screen.height;
            rt.anchorMin = min;
            rt.anchorMax = max;
        }
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI MakeLabel(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text               = "";
        tmp.fontSize           = fontSize;
        tmp.color              = color;
        tmp.alignment          = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.richText           = true;
        tmp.raycastTarget      = false;
        return tmp;
    }
}
