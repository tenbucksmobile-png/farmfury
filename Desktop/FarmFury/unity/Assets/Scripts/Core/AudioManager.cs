using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public enum Sound { Launch, WoodHit, StoneHit, RobotDeath, Win, Fail }

    public static AudioManager Instance { get; private set; }

    private AudioSource _src;
    private AudioClip[] _clips;
    private float[]     _lastPlayTime;

    private const int SR = 44100;

    public static bool SfxEnabled   { get; private set; } = true;
    public static bool MusicEnabled { get; private set; } = true;

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
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        SfxEnabled   = PlayerPrefs.GetInt("ff_sfx_enabled",   1) == 1;
        MusicEnabled = PlayerPrefs.GetInt("ff_music_enabled", 1) == 1;

        _src             = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;

        _clips = new AudioClip[]
        {
            BuildLaunch(),
            BuildWoodHit(),
            BuildStoneHit(),
            BuildRobotDeath(),
            BuildWinFanfare(),
            BuildFailBuzzer(),
        };
        _lastPlayTime = new float[_clips.Length];
    }

    void Start()
    {
        // Subscribe in Start() so GameManager.Instance is guaranteed to be set
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged += OnStateChanged;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= OnStateChanged;
    }

    void OnStateChanged(GameState state)
    {
        if (state == GameState.LevelComplete) Play(Sound.Win);
        if (state == GameState.LevelFailed)   Play(Sound.Fail);
    }

    // cooldown: ignore the call if this sound played within the last N seconds
    public static void Play(Sound sound, float cooldown = 0f)
    {
        if (Instance == null || !SfxEnabled) return;
        int idx = (int)sound;
        if (cooldown > 0f && Time.time - Instance._lastPlayTime[idx] < cooldown) return;
        Instance._lastPlayTime[idx] = Time.time;
        Instance._src.PlayOneShot(Instance._clips[idx], 0.8f);
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

    // Ascending major arpeggio: C4 – E4 – G4 – C5, each note 0.18 s
    static AudioClip BuildWinFanfare()
    {
        float[] freqs = { 261.6f, 329.6f, 392.0f, 523.3f };
        var parts = new float[freqs.Length][];
        for (int i = 0; i < freqs.Length; i++)
        {
            var note = Sine(freqs[i], 0.18f, 0.70f);
            note = Adsr(note, a: 0.01f, d: 0.02f, s: 0.70f, r: 0.10f);
            parts[i] = note;
        }
        return Clip("Win", Concat(parts));
    }

    // Descending sawtooth with vibrato (wah-wah fail tone)
    static AudioClip BuildFailBuzzer()
    {
        int n = N(0.55f);
        var s = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t    = i / (float)SR;
            float freq = Mathf.Lerp(320f, 85f, (float)i / n);
            freq += Mathf.Sin(2f * Mathf.PI * 9f * t) * 14f; // vibrato
            phase += freq / SR;
            s[i] = 0.65f * (2f * (phase % 1f) - 1f);         // sawtooth
        }
        s = Adsr(s, a: 0.01f, d: 0.04f, s: 0.75f, r: 0.22f);
        return Clip("Fail", s);
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

    static float[] Concat(float[][] parts)
    {
        int total = 0;
        foreach (var p in parts) total += p.Length;
        var buf = new float[total];
        int pos = 0;
        foreach (var p in parts) { p.CopyTo(buf, pos); pos += p.Length; }
        return buf;
    }

    static AudioClip Clip(string name, float[] samples)
    {
        var c = AudioClip.Create(name, samples.Length, 1, SR, false);
        c.SetData(samples, 0);
        return c;
    }
}
