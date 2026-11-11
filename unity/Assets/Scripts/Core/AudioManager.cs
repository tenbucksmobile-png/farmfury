using System.Collections;
using UnityEngine;

// [DefaultExecutionOrder(-90)] guarantees this Awake() runs (and claims Instance) before
// CatapultLauncher's fallback `if (AudioManager.Instance == null) AddComponent<AudioManager>()`
// — without it, the runtime-added fallback (with null external clips) could win the singleton
// race depending on scene object order, silently dropping music/cannon-shot/falling audio.
[DefaultExecutionOrder(-90)]
public class AudioManager : MonoBehaviour
{
    public enum Sound { Launch, WoodHit, StoneHit, RobotDeath, BlockDestroy, RobotHit }

    public static AudioManager Instance { get; private set; }

    [Header("External Clips (wired via FarmFury -> Wire Scene References)")]
    [SerializeField] private AudioClip _musicClip;       // SunriseMeadows_Background.mp3, loops during gameplay
    [SerializeField] private AudioClip _menuMusicClip;   // SunriseMeadows_TransitionMusic.mp3, loops on the landing
                                                          // page and the Sunrise Meadows world map (any menu GameState)
    [SerializeField] private AudioClip _cannonShotClip;  // CannonShot.mp3, replaces the procedural Launch sound
    [SerializeField] private AudioClip _fallingClip;     // Cluck_falling.mp3, loops while Cluck is airborne
    [SerializeField] private AudioClip _bessieFallingClip; // Bessie_falling.mp3, loops while Bessie is airborne —
                                                          // see PlayFalling(AnimalType). Impact/landing still goes
                                                          // through the same StopFallingFade() fade-out either way.
    // Per-world gameplay music (2026-11-10, user: "review the music from stampede and arcade for the different worlds -
    // apply, leave the theme as is"), indexed by LevelData.world (1-9). The franchise tracks from FarmFury_Stampede's
    // AudioManager.worldMusic (Arcade's are the same files under other names). World 1 keeps its own Sunrise Meadows
    // track (_musicClip) and the menus keep the Sunrise Meadows theme (_menuMusicClip); an empty slot falls back to
    // _musicClip. Files in Assets/Audio/Worlds/, wired by SceneSetup.EnsureAudioManager.
    [SerializeField] private AudioClip[] _worldMusic = new AudioClip[10];
    // Ability sounds (2026-11-10): Stampede's per-character ability SFX, indexed by AnimalType, played on the tap that
    // triggers the ability (AnimalBase.Update). Woolly has none in the franchise (empty slot = silent). Files in
    // Assets/Audio/Abilities/.
    [SerializeField] private AudioClip[] _abilityClips = new AudioClip[8];
    // Per-animal launch scream (2026-11-11, user-supplied clips in Assets/Audio/Falling/<Animal>_Falling.mp3), index =
    // AnimalType. Plays once from the cannon shot and fades on impact; an empty slot falls back to the older
    // _fallingClip / _bessieFallingClip (Cluck / Bessie), otherwise the flight is silent (Percy has none yet).
    [SerializeField] private AudioClip[] _fallingClips = new AudioClip[8];
    private const float AbilityVolume = 0.9f;

    private AudioSource _src;
    private AudioSource _musicSrc;
    private AudioSource _menuMusicSrc;
    private AudioSource _fallingSrc;
    private AudioClip[] _clips;
    private float[]     _lastPlayTime;
    private Coroutine   _fallingFadeRoutine;

    private const float DefaultMusicVolume  = 0.5f; // prior fixed mix level, now the Settings slider's default
    private const float DefaultSfxVolume    = 1f;   // 1.0x multiplier on top of each sound's own tuned VolumeScale below — unchanged mix by default
    // Raised 0.6 -> 0.9 (2026-07-26): the falling/scream loop starts at the exact same instant
    // as the cannon-shot one-shot (see CatapultLauncher.Fire()) and was getting buried under it —
    // user-reported "cannot hear the chicken scream, it is deafened by the cannon fire". Paired
    // with Launch's own volume being turned down below, in VolumeScale.
    private const float FallingVolume        = 0.9f;
    private const float FallingFadeDuration  = 0.35f;

    // Per-sound PlayOneShot volume scale, indexed by (int)Sound — was a single hardcoded 0.8f
    // for every sound. Launch (the cannon shot) is turned down specifically so it doesn't drown
    // out the falling/scream loop that starts in the same frame; every other SFX keeps the
    // original 0.8 level.
    private static readonly float[] VolumeScale =
    {
        0.5f, // Launch
        0.8f, // WoodHit
        0.8f, // StoneHit
        0.8f, // RobotDeath
        0.8f, // BlockDestroy
        0.8f, // RobotHit
    };

    private const int SR = 44100;

    public static bool SfxEnabled   { get; private set; } = true;
    public static bool MusicEnabled { get; private set; } = true;

    // Music/SFX Volume sliders — added 2026-07-13 for the redesigned Settings > Audio tab (see
    // MainMenuController.BuildSettingsPopup). No Voice/Dialogue toggle — dropped per user
    // decision, since there's no voice-line audio content (character reactions, robot lines)
    // anywhere in this codebase to back it yet.
    public static float MusicVolume  { get; private set; } = DefaultMusicVolume;
    public static float SfxVolume    { get; private set; } = DefaultSfxVolume;

    public static void SetSfxEnabled(bool value)
    {
        SfxEnabled = value;
        PlayerPrefs.SetInt("ff_sfx_enabled", value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void SetMusicEnabled(bool value)
    {
        MusicEnabled = value;
        PlayerPrefs.SetInt("ff_music_enabled", value ? 1 : 0);
        PlayerPrefs.Save();
        if (Instance != null)
        {
            if (Instance._musicSrc != null) Instance._musicSrc.mute = !value;
            if (Instance._menuMusicSrc != null) Instance._menuMusicSrc.mute = !value;
        }
    }

    public static void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("ff_music_volume", MusicVolume);
        PlayerPrefs.Save();
        if (Instance != null)
        {
            if (Instance._musicSrc     != null) Instance._musicSrc.volume     = MusicVolume;
            if (Instance._menuMusicSrc != null) Instance._menuMusicSrc.volume = MusicVolume;
        }
    }

    public static void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("ff_sfx_volume", SfxVolume);
        PlayerPrefs.Save();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        SfxEnabled   = PlayerPrefs.GetInt("ff_sfx_enabled",   1) == 1;
        MusicEnabled = PlayerPrefs.GetInt("ff_music_enabled", 1) == 1;
        MusicVolume  = PlayerPrefs.GetFloat("ff_music_volume", DefaultMusicVolume);
        SfxVolume    = PlayerPrefs.GetFloat("ff_sfx_volume",   DefaultSfxVolume);

        _src             = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;

        _musicSrc             = gameObject.AddComponent<AudioSource>();
        _musicSrc.playOnAwake = false;
        _musicSrc.loop        = true;
        _musicSrc.volume      = MusicVolume;
        _musicSrc.clip        = _musicClip;
        _musicSrc.mute        = !MusicEnabled;

        _menuMusicSrc             = gameObject.AddComponent<AudioSource>();
        _menuMusicSrc.playOnAwake = false;
        _menuMusicSrc.loop        = true;
        _menuMusicSrc.volume      = MusicVolume;
        _menuMusicSrc.clip        = _menuMusicClip;
        _menuMusicSrc.mute        = !MusicEnabled;

        _fallingSrc             = gameObject.AddComponent<AudioSource>();
        _fallingSrc.playOnAwake = false;
        _fallingSrc.loop        = false;   // a shout, not a loop (2026-11-11): short clips repeated on long flights
        _fallingSrc.volume      = FallingVolume;
        _fallingSrc.clip        = _fallingClip;

        _clips = new AudioClip[]
        {
            _cannonShotClip != null ? _cannonShotClip : BuildLaunch(),
            BuildWoodHit(),
            BuildStoneHit(),
            BuildRobotDeath(),
            BuildBlockDestroy(),
            BuildRobotHit(),
        };
        _lastPlayTime = new float[_clips.Length];
    }

    void Start()
    {
        // Subscribe in Start() so GameManager.Instance is guaranteed to be set
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged += OnStateChanged;
            // GameManager.State starts as MainMenu by default, without ever firing a transition
            // event for it (TransitionTo only fires on an actual change) — so on a fresh launch
            // landing on the menu, nothing would otherwise tell us to start the menu music. Sync
            // once against whatever the state already is (mirrors HUDController.Start()'s own
            // "CatapultLauncher may have already forced Playing before us" catch-up).
            OnStateChanged(GameManager.Instance.State);
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= OnStateChanged;
    }

    void OnStateChanged(GameState state) => SyncMusic();

    void Update() => SyncMusic();

    private enum MusicMode { None, Menu, Game }
    private MusicMode _musicMode;
    private AudioClip _gameTrack;          // the world track currently loaded on _musicSrc
    private bool _gameMusicPaused;         // _musicSrc paused behind an in-level page (resume, don't restart)
    private bool _menuMusicStarted;        // the theme has been started once; afterwards it is paused / unpaused

    /// <summary>
    /// User (2026-11-11): "theme music must play on every page outside of gameplay". The world's gameplay track plays
    /// only while a level is actually being played (Playing, unfrozen, no revive prompt). Every other page (Landing,
    /// World / Level / Character Select, Pause, the Locker and in-level character switch, the revive prompt, Level
    /// Complete / Failed) plays the menu theme. The gameplay track is paused (not restarted) behind an in-level page
    /// and resumes where it left off; the theme likewise resumes rather than restarting.
    /// Acts only when the wanted mode / track CHANGES — never on AudioSource.isPlaying, which reads false for a while
    /// after Play() on clips imported without "Preload Audio Data" (all of ours): polling it restarted the track every
    /// frame and nothing was heard (2026-11-11, user: "no sound is coming through").
    /// </summary>
    private void SyncMusic()
    {
        var gm = GameManager.Instance;
        if (gm == null || _musicSrc == null || _menuMusicSrc == null) return;
        var s = gm.State;
        bool gameplay = s == GameState.Playing && Time.timeScale > 0f && !gm.ReviveDecisionPending;
        bool inLevel = s == GameState.Playing || s == GameState.Paused;
        var want = gameplay ? MusicMode.Game : MusicMode.Menu;
        var track = gameplay ? MusicForWorld(gm.CurrentLevel != null ? gm.CurrentLevel.world : 1) : null;

        // Left the level from an in-level page (Pause -> Home / Retry ...): the paused world track is finished with.
        if (!inLevel && _gameMusicPaused)
        {
            _musicSrc.Stop();
            _gameMusicPaused = false;
            _gameTrack = null;
        }
        if (want == _musicMode && (want != MusicMode.Game || track == _gameTrack)) return;

        if (want == MusicMode.Game)
        {
            _menuMusicSrc.Pause();
            if (_gameMusicPaused && track == _gameTrack) _musicSrc.UnPause();
            else if (track != null)
            {
                _musicSrc.Stop();
                _musicSrc.clip = track;
                _musicSrc.Play();
            }
            _gameTrack = track;
            _gameMusicPaused = false;
        }
        else
        {
            if (inLevel && _musicMode == MusicMode.Game) { _musicSrc.Pause(); _gameMusicPaused = true; }
            else if (!inLevel) { _musicSrc.Stop(); _gameTrack = null; }
            if (_menuMusicSrc.clip != null)
            {
                if (_menuMusicStarted) _menuMusicSrc.UnPause();
                else { _menuMusicSrc.Play(); _menuMusicStarted = true; }
            }
        }
        _musicMode = want;
    }

    // ── Gameplay music pause/resume (top-right Pause button) ────────────────
    // Time.timeScale=0 freezes physics/animation but NOT audio playback — AudioSources run on
    // their own wall-clock timeline regardless of timeScale (same reason VideoChromaKey/
    // LevelCompleteManager's celebration clips can play during that freeze). Without this, the
    // gameplay music kept looping right through a paused game. HUDController.SetPaused() calls
    // these in lockstep with the Time.timeScale flip.
    // Kept for callers; SyncMusic() (every frame) owns the switching now.
    public static void PauseGameplayMusic() => Instance?.SyncMusic();

    public static void ResumeGameplayMusic() => Instance?.SyncMusic();

    AudioClip MusicForWorld(int world) =>
        _worldMusic != null && world > 1 && world < _worldMusic.Length && _worldMusic[world] != null
            ? _worldMusic[world] : _musicClip;

    /// <summary>The animal's ability sound (tap in flight); silent when it has none.</summary>
    public static void PlayAbility(AnimalType animal)
    {
        if (Instance == null || Instance._abilityClips == null) return;
        int i = (int)animal;
        if (i < 0 || i >= Instance._abilityClips.Length) return;
        PlayClip(Instance._abilityClips[i], AbilityVolume);
    }

    // cooldown: ignore the call if this sound played within the last N seconds
    public static void Play(Sound sound, float cooldown = 0f)
    {
        if (Instance == null || !SfxEnabled) return;
        int idx = (int)sound;
        if (cooldown > 0f && Time.time - Instance._lastPlayTime[idx] < cooldown) return;
        Instance._lastPlayTime[idx] = Time.time;
        Instance._src.PlayOneShot(Instance._clips[idx], VolumeScale[idx] * SfxVolume);
    }

    // Plays an arbitrary external AudioClip (e.g. a block's own dedicated destroy sound) through
    // the shared SFX AudioSource, uncached and un-throttled — unlike Play(Sound, cooldown) above,
    // there's no cooldown gate here, since callers that reach for this want a guarantee the clip
    // always plays (e.g. HaybaleBlock's explosion sound).
    public static void PlayClip(AudioClip clip, float volume = 0.8f)
    {
        if (Instance == null || clip == null || !SfxEnabled) return;
        Instance._src.PlayOneShot(clip, volume * SfxVolume);
    }

    // ── Falling sound (animal airborne) ──────────────────────────────────────
    // Loops from the moment the animal is fired; CatapultLauncher stops it (with a fade) the
    // instant AnimalBase.OnAnimalImpact fires — i.e. on the real hit, not on pass-through
    // punches (CluckAnimal's pass-through branch never calls base.OnCollisionEnter2D). The
    // fade-out on impact (StopFallingFade/FadeOutFalling below) is shared by every animal —
    // only which clip loops during the flight itself differs per animal.

    public void PlayFalling(AnimalType animal = AnimalType.Cluck)
    {
        int i = (int)animal;
        AudioClip clip = _fallingClips != null && i >= 0 && i < _fallingClips.Length ? _fallingClips[i] : null;
        if (clip == null && animal == AnimalType.Bessie) clip = _bessieFallingClip;
        if (clip == null && animal == AnimalType.Cluck)  clip = _fallingClip;
        if (_fallingSrc == null || clip == null || !SfxEnabled) return;
        if (_fallingFadeRoutine != null) { StopCoroutine(_fallingFadeRoutine); _fallingFadeRoutine = null; }
        _fallingSrc.clip   = clip;
        _fallingSrc.volume = FallingVolume * SfxVolume;
        _fallingSrc.Play();
    }

    public void StopFallingFade()
    {
        if (_fallingSrc == null || !_fallingSrc.isPlaying) return;
        if (_fallingFadeRoutine != null) StopCoroutine(_fallingFadeRoutine);
        _fallingFadeRoutine = StartCoroutine(FadeOutFalling());
    }

    IEnumerator FadeOutFalling()
    {
        float startVol = _fallingSrc.volume;
        float t = 0f;
        while (t < FallingFadeDuration)
        {
            t += Time.deltaTime;
            _fallingSrc.volume = Mathf.Lerp(startVol, 0f, t / FallingFadeDuration);
            yield return null;
        }
        _fallingSrc.Stop();
        _fallingSrc.volume = FallingVolume; // reset for next shot
        _fallingFadeRoutine = null;
    }

    // ── Clip builders ─────────────────────────────────────────────────────────

    // Elastic twang: high→low sweep + brief noise burst (rubber-band snap)
    static AudioClip BuildLaunch()
    {
        var sweep = Sweep(900f, 110f, 0.32f);
        sweep = Adsr(sweep, a: 0.003f, d: 0.06f, s: 0.35f, r: 0.20f);

        var burst = Noise(0.32f, 0.35f);
        burst = Adsr(burst, a: 0.001f, d: 0.03f, s: 0f, r: 0.29f);

        return Clip("Launch", Mix(Scale(sweep, 0.8f), burst));
    }

    // Heavy wooden crash on block death: deep thud + crack burst + falling tone
    static AudioClip BuildBlockDestroy()
    {
        var thud  = Sweep(160f, 50f, 0.40f, 0.9f);
        thud  = Adsr(thud,  a: 0.001f, d: 0.05f, s: 0.25f, r: 0.30f);
        var crack = Noise(0.40f, 0.80f);
        crack = Adsr(crack, a: 0.001f, d: 0.03f, s: 0f,    r: 0.37f);
        var tone  = Sine(95f, 0.40f, 0.55f);
        tone  = Adsr(tone,  a: 0.001f, d: 0.07f, s: 0.15f, r: 0.28f);
        return Clip("BlockDestroy", Mix(Mix(Scale(thud, 0.70f), Scale(crack, 0.65f)), Scale(tone, 0.40f)));
    }

    // Metallic clank when a robot takes a hit
    static AudioClip BuildRobotHit()
    {
        var ping  = Sine(380f, 0.20f, 0.80f);
        ping  = Adsr(ping,  a: 0.001f, d: 0.02f, s: 0.20f, r: 0.17f);
        var crack = Noise(0.20f, 0.40f);
        crack = Adsr(crack, a: 0.001f, d: 0.04f, s: 0f,    r: 0.16f);
        return Clip("RobotHit", Mix(Scale(ping, 0.65f), Scale(crack, 0.45f)));
    }

    // Low-frequency thud + short noise crack
    static AudioClip BuildWoodHit()
    {
        var body  = Sine(80f,  0.22f);
        body  = Adsr(body,  a: 0.001f, d: 0.06f, s: 0.05f, r: 0.14f);
        var crack = Noise(0.22f);
        crack = Adsr(crack, a: 0.001f, d: 0.04f, s: 0f,    r: 0.18f);
        return Clip("WoodHit", Mix(Scale(body, 0.65f), Scale(crack, 0.50f)));
    }

    // Metallic ping: fundamental + octave harmonic + noise crack
    static AudioClip BuildStoneHit()
    {
        var fund  = Sine(280f, 0.30f);
        var harm  = Sine(560f, 0.30f);
        fund  = Adsr(fund,  a: 0.001f, d: 0.04f, s: 0.20f, r: 0.24f);
        harm  = Adsr(harm,  a: 0.001f, d: 0.03f, s: 0.10f, r: 0.24f);
        var crack = Noise(0.30f);
        crack = Adsr(crack, a: 0.001f, d: 0.02f, s: 0f,    r: 0.28f);
        return Clip("StoneHit", Mix(Mix(Scale(fund, 0.55f), Scale(harm, 0.25f)),
                                    Scale(crack, 0.45f)));
    }

    // Explosion: white-noise burst + low-pitch sweep-down rumble
    static AudioClip BuildRobotDeath()
    {
        var bang   = Noise(0.50f, 0.9f);
        bang   = Adsr(bang,   a: 0.001f, d: 0.06f, s: 0.30f, r: 0.38f);
        var rumble = Sweep(130f, 35f, 0.50f);
        rumble = Adsr(rumble, a: 0.001f, d: 0.08f, s: 0.20f, r: 0.38f);
        return Clip("RobotDeath", Mix(Scale(bang, 0.80f), Scale(rumble, 0.55f)));
    }

    // ── DSP primitives ────────────────────────────────────────────────────────

    static int N(float dur) => Mathf.Max(1, Mathf.RoundToInt(SR * dur));

    static float[] Sine(float freq, float dur, float amp = 1f)
    {
        int n = N(dur);
        var s = new float[n];
        for (int i = 0; i < n; i++)
            s[i] = amp * Mathf.Sin(2f * Mathf.PI * freq * i / SR);
        return s;
    }

    static float[] Noise(float dur, float amp = 1f)
    {
        int n = N(dur);
        var s = new float[n];
        for (int i = 0; i < n; i++)
            s[i] = amp * (Random.value * 2f - 1f);
        return s;
    }

    // Linear frequency sweep via instantaneous-phase integration (no phase discontinuities)
    static float[] Sweep(float f0, float f1, float dur, float amp = 1f)
    {
        int n = N(dur);
        var s = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            s[i]   = amp * Mathf.Sin(2f * Mathf.PI * phase);
            phase += Mathf.Lerp(f0, f1, (float)i / n) / SR;
        }
        return s;
    }

    // ADSR envelope: attack, decay, sustain-level, release
    static float[] Adsr(float[] src, float a, float d, float s, float r)
    {
        int n   = src.Length;
        int ia  = Mathf.Max(1, Mathf.Min(Mathf.RoundToInt(a * SR), n));
        int id  = Mathf.Max(1, Mathf.Min(Mathf.RoundToInt(d * SR), n - ia));
        int ir  = Mathf.Max(1, Mathf.Min(Mathf.RoundToInt(r * SR), n));
        int isu = Mathf.Max(0, n - ia - id - ir);
        var dst = new float[n];
        for (int i = 0; i < n; i++)
        {
            float env;
            if      (i < ia)                 env = (float)i / ia;
            else if (i < ia + id)            env = Mathf.Lerp(1f, s, (float)(i - ia) / id);
            else if (i < ia + id + isu)      env = s;
            else                             env = Mathf.Lerp(s, 0f, (float)(i - ia - id - isu) / ir);
            dst[i] = src[i] * env;
        }
        return dst;
    }

    static float[] Mix(float[] a, float[] b)
    {
        int n = Mathf.Max(a.Length, b.Length);
        var o = new float[n];
        for (int i = 0; i < n; i++)
            o[i] = Mathf.Clamp((i < a.Length ? a[i] : 0f) + (i < b.Length ? b[i] : 0f), -1f, 1f);
        return o;
    }

    static float[] Scale(float[] src, float amp)
    {
        var dst = new float[src.Length];
        for (int i = 0; i < src.Length; i++) dst[i] = src[i] * amp;
        return dst;
    }

    static AudioClip Clip(string name, float[] samples)
    {
        var c = AudioClip.Create(name, samples.Length, 1, SR, false);
        c.SetData(samples, 0);
        return c;
    }
}
