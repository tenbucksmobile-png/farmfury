using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Weather and ambient life over the World 1 cornfield (2026-11-09, user: "the scene seems bare... wind, rain...").
/// Owned by <see cref="EnvironmentDepthSystem"/> (created with its cornfield layers, enabled only while they show).
/// Per level (<see cref="LevelData.weather"/>) it picks a <see cref="Profile"/>: the colour grade and haze that
/// EnvironmentDepthSystem applies to the painted layers, plus everything this class animates itself —
///   drifting clouds and a sun glow in the sky, a flock of crows now and then, pollen specks (fireflies at dusk,
///   chaff when overcast), low ground fog rolling across the field, rain streaks slanted by the wind with lightning
///   flashes, and a gusty wind value pushed to the corn's WindSway shader (global _FFWind).
/// All purely visual: no colliders, nothing the physics or the player's aim can touch. Sized in screen fractions so it
/// reads the same on every aspect ratio. Update-driven (no coroutines) so it survives being disabled/re-enabled.
/// </summary>
public class CornfieldAmbience : MonoBehaviour
{
    public struct Profile
    {
        public Color sky;          // multiplies the far painting, sun and clouds
        public Color land;         // multiplies the landform layers and the corn
        public Color haze;         // haze veil + mist colour
        public float hazeAlpha, mistAlpha;
        public Color grade;        // full-screen tint over the play layer (alpha = strength)
        public float wind;         // 0 still .. 1 breeze .. 2 storm
        public float rain;         // 0..1 streak density
        public bool  lightning;
        public float sun;          // sun glow strength
        public Color sunColour;
        public int   clouds;
        public Color cloudTint;
        public float pollen;       // 0..1 speck density
        public int   speckKind;    // 0 pollen, 1 fireflies, 2 chaff
        public bool  crows;
        public float groundFog;    // alpha of the rolling fog banks
        // Frozen Tundra (2026-11-09)
        public float snow;         // 0..1 falling snowflakes (speck layer, kind 3)
        public bool  blizzard;     // rain streaks become driving white snow, nearly horizontal
        public float aurora;       // 0..1 shimmering aurora ribbons in the sky
        public float sparkle;      // 0..1 glints twinkling on the snow
        // Watermill Village (2026-11-10)
        public float rainbow;      // 0..1 a rainbow arc in the sky (spring shower)
    }

    static readonly Profile Golden = new()
    {
        sky = Color.white, land = Color.white, haze = new Color(1f, 0.87f, 0.74f), hazeAlpha = 0.26f, mistAlpha = 0.55f,
        grade = new Color(1f, 0.85f, 0.6f, 0.0f), wind = 1f, sun = 1f, sunColour = new Color(1f, 0.93f, 0.7f),
        clouds = 4, cloudTint = Color.white, pollen = 0.8f, speckKind = 0, crows = true, groundFog = 0.12f,
    };
    static readonly Profile MorningMist = new()
    {
        sky = new Color(0.93f, 0.97f, 1f), land = new Color(0.9f, 0.95f, 0.96f), haze = new Color(0.86f, 0.9f, 0.95f),
        hazeAlpha = 0.42f, mistAlpha = 0.85f, grade = new Color(0.8f, 0.88f, 1f, 0.06f), wind = 0.45f,
        sun = 0.55f, sunColour = new Color(1f, 0.97f, 0.88f), clouds = 2, cloudTint = new Color(0.97f, 0.98f, 1f),
        pollen = 0.45f, speckKind = 0, crows = true, groundFog = 0.55f,
    };
    static readonly Profile Overcast = new()
    {
        sky = new Color(0.72f, 0.76f, 0.82f), land = new Color(0.8f, 0.84f, 0.85f), haze = new Color(0.78f, 0.8f, 0.85f),
        hazeAlpha = 0.38f, mistAlpha = 0.5f, grade = new Color(0.25f, 0.3f, 0.42f, 0.10f), wind = 1.4f,
        sun = 0f, sunColour = Color.white, clouds = 6, cloudTint = new Color(0.74f, 0.76f, 0.82f),
        pollen = 0.5f, speckKind = 2, crows = true, groundFog = 0.22f,
    };
    static readonly Profile Rain = new()
    {
        sky = new Color(0.55f, 0.6f, 0.7f), land = new Color(0.66f, 0.71f, 0.75f), haze = new Color(0.6f, 0.65f, 0.73f),
        hazeAlpha = 0.42f, mistAlpha = 0.62f, grade = new Color(0.12f, 0.16f, 0.3f, 0.22f), wind = 1.9f,
        rain = 1f, lightning = true, sun = 0f, sunColour = Color.white, clouds = 7, cloudTint = new Color(0.55f, 0.58f, 0.66f),
        pollen = 0f, speckKind = 2, crows = false, groundFog = 0.3f,
    };
    static readonly Profile Dusk = new()
    {
        sky = new Color(1f, 0.74f, 0.62f), land = new Color(0.93f, 0.74f, 0.68f), haze = new Color(1f, 0.66f, 0.5f),
        hazeAlpha = 0.3f, mistAlpha = 0.55f, grade = new Color(0.55f, 0.2f, 0.28f, 0.12f), wind = 0.7f,
        sun = 1f, sunColour = new Color(1f, 0.62f, 0.38f), clouds = 3, cloudTint = new Color(1f, 0.72f, 0.62f),
        pollen = 0.7f, speckKind = 1, crows = true, groundFog = 0.2f,
    };

    // ── Frozen Tundra (World 2) ─────────────────────────────────────────────────────────────────────────────────
    // The far layer is World 2's aurora night painting; these grade it and set the cold atmosphere.
    static readonly Profile AuroraNight = new()
    {
        sky = Color.white, land = new Color(0.80f, 0.86f, 1f), haze = new Color(0.52f, 0.6f, 0.86f), hazeAlpha = 0.2f,
        mistAlpha = 0.45f, grade = new Color(0.1f, 0.14f, 0.4f, 0.10f), wind = 0.5f, sun = 0f, sunColour = Color.white,
        clouds = 2, cloudTint = new Color(0.55f, 0.6f, 0.82f), pollen = 0f, crows = false, groundFog = 0.22f,
        snow = 0.35f, aurora = 1f, sparkle = 1f,
    };
    static readonly Profile Snowfall = new()
    {
        sky = new Color(0.86f, 0.9f, 1f), land = new Color(0.9f, 0.94f, 1f), haze = new Color(0.74f, 0.8f, 0.96f),
        hazeAlpha = 0.32f, mistAlpha = 0.6f, grade = new Color(0.55f, 0.65f, 0.9f, 0.06f), wind = 0.8f, sun = 0f,
        sunColour = Color.white, clouds = 5, cloudTint = new Color(0.7f, 0.75f, 0.9f), crows = false, groundFog = 0.3f,
        snow = 1f, aurora = 0.4f, sparkle = 0.5f,
    };
    static readonly Profile Blizzard = new()
    {
        sky = new Color(0.68f, 0.74f, 0.88f), land = new Color(0.78f, 0.82f, 0.92f), haze = new Color(0.82f, 0.86f, 0.96f),
        hazeAlpha = 0.48f, mistAlpha = 0.8f, grade = new Color(0.75f, 0.82f, 0.96f, 0.16f), wind = 2.2f, rain = 0.9f,
        blizzard = true, sun = 0f, sunColour = Color.white, clouds = 7, cloudTint = new Color(0.6f, 0.62f, 0.74f),
        crows = false, groundFog = 0.5f, snow = 1f, aurora = 0f, sparkle = 0f,
    };
    static readonly Profile IceFog = new()
    {
        sky = new Color(0.8f, 0.85f, 0.96f), land = new Color(0.84f, 0.89f, 0.97f), haze = new Color(0.84f, 0.9f, 1f),
        hazeAlpha = 0.55f, mistAlpha = 0.95f, grade = new Color(0.82f, 0.88f, 1f, 0.12f), wind = 0.3f, sun = 0f,
        sunColour = Color.white, clouds = 1, cloudTint = new Color(0.8f, 0.85f, 0.95f), crows = false, groundFog = 0.75f,
        snow = 0.25f, aurora = 0.2f, sparkle = 0.6f,
    };
    static readonly Profile PolarDawn = new()
    {
        sky = new Color(1f, 0.78f, 0.84f), land = new Color(1f, 0.9f, 0.93f), haze = new Color(1f, 0.74f, 0.72f),
        hazeAlpha = 0.28f, mistAlpha = 0.55f, grade = new Color(0.95f, 0.45f, 0.5f, 0.08f), wind = 0.6f, sun = 0.8f,
        sunColour = new Color(1f, 0.72f, 0.62f), clouds = 3, cloudTint = new Color(1f, 0.8f, 0.82f), crows = false,
        groundFog = 0.25f, snow = 0.3f, aurora = 0.35f, sparkle = 1f,
    };

    // ── Watermill Village (World 3, 2026-11-10) ─────────────────────────────────────────────────────────────────
    // A river valley: swallows over the water, blossom petals off the willows, dragonflies over the reeds, sunlight
    // glinting on the river, a rainbow after a spring shower, fireflies at lantern-lit dusk, a summer thunderstorm.
    // speckKind 3 = blossom petals (tumbling pink/white), 4 = dragonflies (darting blue-green, low over the water).
    static readonly Profile SummerMorning = new()
    {
        sky = Color.white, land = Color.white, haze = new Color(0.84f, 0.93f, 0.96f), hazeAlpha = 0.22f, mistAlpha = 0.45f,
        grade = new Color(1f, 0.95f, 0.8f, 0.0f), wind = 0.7f, sun = 1f, sunColour = new Color(1f, 0.96f, 0.8f),
        clouds = 4, cloudTint = Color.white, pollen = 0.55f, speckKind = 4, crows = true, groundFog = 0.08f, sparkle = 0.8f,
    };
    static readonly Profile RiverMist = new()
    {
        sky = new Color(0.92f, 0.96f, 0.98f), land = new Color(0.88f, 0.94f, 0.93f), haze = new Color(0.85f, 0.92f, 0.92f),
        hazeAlpha = 0.45f, mistAlpha = 0.9f, grade = new Color(0.78f, 0.9f, 0.92f, 0.07f), wind = 0.35f, sun = 0.5f,
        sunColour = new Color(1f, 0.97f, 0.88f), clouds = 2, cloudTint = new Color(0.96f, 0.98f, 1f), pollen = 0.5f,
        speckKind = 3, crows = true, groundFog = 0.7f, sparkle = 0.3f,
    };
    static readonly Profile SpringShower = new()
    {
        sky = new Color(0.86f, 0.9f, 0.96f), land = new Color(0.9f, 0.95f, 0.92f), haze = new Color(0.8f, 0.86f, 0.92f),
        hazeAlpha = 0.3f, mistAlpha = 0.6f, grade = new Color(0.5f, 0.6f, 0.8f, 0.06f), wind = 1.0f, rain = 0.5f,
        sun = 0.7f, sunColour = new Color(1f, 0.95f, 0.82f), clouds = 5, cloudTint = new Color(0.86f, 0.88f, 0.94f),
        pollen = 0.45f, speckKind = 3, crows = false, groundFog = 0.2f, sparkle = 0.6f, rainbow = 1f,
    };
    static readonly Profile LanternDusk = new()
    {
        sky = new Color(1f, 0.76f, 0.7f), land = new Color(0.9f, 0.76f, 0.74f), haze = new Color(0.95f, 0.64f, 0.58f),
        hazeAlpha = 0.3f, mistAlpha = 0.6f, grade = new Color(0.45f, 0.2f, 0.4f, 0.14f), wind = 0.4f, sun = 0.9f,
        sunColour = new Color(1f, 0.6f, 0.4f), clouds = 3, cloudTint = new Color(1f, 0.72f, 0.66f), pollen = 1f,
        speckKind = 1, crows = true, groundFog = 0.3f, sparkle = 0.4f,
    };
    static readonly Profile Thunderstorm = new()
    {
        sky = new Color(0.5f, 0.56f, 0.64f), land = new Color(0.62f, 0.7f, 0.7f), haze = new Color(0.56f, 0.63f, 0.68f),
        hazeAlpha = 0.45f, mistAlpha = 0.65f, grade = new Color(0.1f, 0.18f, 0.28f, 0.24f), wind = 2.0f, rain = 1f,
        lightning = true, sun = 0f, sunColour = Color.white, clouds = 7, cloudTint = new Color(0.5f, 0.54f, 0.6f),
        pollen = 0.35f, speckKind = 2, crows = false, groundFog = 0.35f,
    };

    // ── Sky Islands (World 4, 2026-11-11) ──────────────────────────────────────────────────────────────────────
    // Above a sea of clouds: big drifting clouds and bright sun, gusty updraft days (chaff = blown leaves), a pink-gold
    // sunset with pollen, a storm front with rain and lightning, a starlit night with fireflies and a faint aurora.
    static readonly Profile ClearSkies = new()
    {
        sky = Color.white, land = Color.white, haze = new Color(0.86f, 0.93f, 1f), hazeAlpha = 0.2f, mistAlpha = 0.35f,
        grade = new Color(0.9f, 0.96f, 1f, 0.0f), wind = 1.1f, sun = 1f, sunColour = new Color(1f, 0.97f, 0.86f),
        clouds = 7, cloudTint = Color.white, pollen = 0.3f, speckKind = 0, crows = true, groundFog = 0.1f,
    };
    static readonly Profile Windswept = new()
    {
        sky = new Color(0.92f, 0.96f, 1f), land = new Color(0.95f, 0.98f, 1f), haze = new Color(0.84f, 0.9f, 0.98f),
        hazeAlpha = 0.28f, mistAlpha = 0.45f, grade = new Color(0.7f, 0.82f, 1f, 0.05f), wind = 2.0f, sun = 0.8f,
        sunColour = new Color(1f, 0.98f, 0.9f), clouds = 8, cloudTint = new Color(0.96f, 0.98f, 1f), pollen = 0.8f,
        speckKind = 2, crows = false, groundFog = 0.2f,
    };
    static readonly Profile SunsetClouds = new()
    {
        sky = new Color(1f, 0.82f, 0.78f), land = new Color(1f, 0.88f, 0.86f), haze = new Color(1f, 0.74f, 0.66f),
        hazeAlpha = 0.3f, mistAlpha = 0.55f, grade = new Color(0.95f, 0.5f, 0.45f, 0.1f), wind = 0.6f, sun = 1f,
        sunColour = new Color(1f, 0.7f, 0.5f), clouds = 6, cloudTint = new Color(1f, 0.78f, 0.74f), pollen = 0.7f,
        speckKind = 0, crows = true, groundFog = 0.25f,
    };
    static readonly Profile StormFront = new()
    {
        sky = new Color(0.52f, 0.58f, 0.7f), land = new Color(0.66f, 0.72f, 0.8f), haze = new Color(0.58f, 0.64f, 0.74f),
        hazeAlpha = 0.42f, mistAlpha = 0.6f, grade = new Color(0.12f, 0.18f, 0.32f, 0.22f), wind = 1.8f, rain = 0.8f,
        lightning = true, sun = 0f, sunColour = Color.white, clouds = 8, cloudTint = new Color(0.52f, 0.56f, 0.66f),
        pollen = 0.3f, speckKind = 2, crows = false, groundFog = 0.3f,
    };
    static readonly Profile StarlitSky = new()
    {
        sky = new Color(0.42f, 0.48f, 0.72f), land = new Color(0.6f, 0.66f, 0.86f), haze = new Color(0.4f, 0.46f, 0.7f),
        hazeAlpha = 0.32f, mistAlpha = 0.5f, grade = new Color(0.12f, 0.12f, 0.4f, 0.22f), wind = 0.4f, sun = 0.3f,
        sunColour = new Color(0.85f, 0.9f, 1f), clouds = 4, cloudTint = new Color(0.55f, 0.6f, 0.8f), pollen = 1f,
        speckKind = 1, crows = false, groundFog = 0.2f, aurora = 0.25f,
    };

    // ── Sunken City (World 5, 2026-11-12) ──────────────────────────────────────────────────────────────────────
    // Under the sea: no clouds or crows; the "sun" is the light from the surface; marine snow drifts down (the snow
    // speck layer), glowing plankton (fireflies) in the deep, murk rolling along the floor (ground fog), and the kelp
    // sways in the current (wind).
    static readonly Profile SunlitShallows = new()
    {
        sky = Color.white, land = Color.white, haze = new Color(0.55f, 0.85f, 0.88f), hazeAlpha = 0.18f, mistAlpha = 0.3f,
        grade = new Color(0.4f, 0.85f, 0.9f, 0.05f), wind = 0.6f, sun = 0.9f, sunColour = new Color(0.85f, 1f, 0.95f),
        clouds = 0, cloudTint = Color.white, pollen = 0.4f, speckKind = 0, crows = false, groundFog = 0.1f, snow = 0.2f,
    };
    static readonly Profile KelpForest = new()
    {
        sky = new Color(0.86f, 1f, 0.92f), land = new Color(0.88f, 1f, 0.9f), haze = new Color(0.4f, 0.7f, 0.6f),
        hazeAlpha = 0.26f, mistAlpha = 0.4f, grade = new Color(0.3f, 0.7f, 0.5f, 0.08f), wind = 1.2f, sun = 0.6f,
        sunColour = new Color(0.8f, 1f, 0.85f), clouds = 0, cloudTint = Color.white, pollen = 0.3f, speckKind = 0,
        crows = false, groundFog = 0.2f, snow = 0.3f,
    };
    static readonly Profile MurkyDepths = new()
    {
        sky = new Color(0.6f, 0.72f, 0.74f), land = new Color(0.7f, 0.8f, 0.8f), haze = new Color(0.3f, 0.45f, 0.48f),
        hazeAlpha = 0.42f, mistAlpha = 0.6f, grade = new Color(0.1f, 0.28f, 0.32f, 0.2f), wind = 0.4f, sun = 0.2f,
        sunColour = new Color(0.7f, 0.9f, 0.9f), clouds = 0, cloudTint = Color.white, pollen = 0.2f, speckKind = 2,
        crows = false, groundFog = 0.45f, snow = 0.6f,
    };
    static readonly Profile GlowingDeep = new()
    {
        sky = new Color(0.38f, 0.46f, 0.66f), land = new Color(0.55f, 0.62f, 0.82f), haze = new Color(0.2f, 0.28f, 0.5f),
        hazeAlpha = 0.34f, mistAlpha = 0.5f, grade = new Color(0.08f, 0.1f, 0.36f, 0.24f), wind = 0.3f, sun = 0.15f,
        sunColour = new Color(0.6f, 0.8f, 1f), clouds = 0, cloudTint = Color.white, pollen = 1f, speckKind = 1,
        crows = false, groundFog = 0.25f, snow = 0.25f,
    };
    static readonly Profile Undertow = new()
    {
        sky = new Color(0.78f, 0.9f, 0.92f), land = new Color(0.84f, 0.94f, 0.94f), haze = new Color(0.45f, 0.65f, 0.7f),
        hazeAlpha = 0.3f, mistAlpha = 0.45f, grade = new Color(0.2f, 0.5f, 0.6f, 0.1f), wind = 2.0f, sun = 0.5f,
        sunColour = new Color(0.85f, 1f, 1f), clouds = 0, cloudTint = Color.white, pollen = 0.8f, speckKind = 2,
        crows = false, groundFog = 0.3f, snow = 0.2f,
    };

    // ── Robot Mothership (World 6, 2026-11-12) ────────────────────────────────────────────────────────────────
    // On the mothership's deck in space: no clouds or crows; twinkling stars (the firefly specks), drifting sparks
    // (chaff), the glow of the planet or a sun; a soft red alert grade (no flashing), a purple nebula, a power-down gloom.
    static readonly Profile StarField = new()
    {
        sky = Color.white, land = Color.white, haze = new Color(0.2f, 0.26f, 0.45f), hazeAlpha = 0.15f, mistAlpha = 0.25f,
        grade = new Color(0.3f, 0.4f, 0.8f, 0.04f), wind = 0.2f, sun = 0.35f, sunColour = new Color(0.7f, 0.85f, 1f),
        clouds = 0, cloudTint = Color.white, pollen = 0.9f, speckKind = 1, crows = false, groundFog = 0.08f,
    };
    static readonly Profile RedAlert = new()
    {
        sky = new Color(1f, 0.78f, 0.78f), land = new Color(1f, 0.85f, 0.85f), haze = new Color(0.5f, 0.15f, 0.18f),
        hazeAlpha = 0.25f, mistAlpha = 0.35f, grade = new Color(0.7f, 0.08f, 0.08f, 0.1f), wind = 0.4f, sun = 0.2f,
        sunColour = new Color(1f, 0.5f, 0.45f), clouds = 0, cloudTint = Color.white, pollen = 0.6f, speckKind = 2,
        crows = false, groundFog = 0.2f,
    };
    static readonly Profile NebulaGlow = new()
    {
        sky = new Color(0.92f, 0.8f, 1f), land = new Color(0.94f, 0.88f, 1f), haze = new Color(0.45f, 0.25f, 0.6f),
        hazeAlpha = 0.22f, mistAlpha = 0.35f, grade = new Color(0.55f, 0.25f, 0.8f, 0.08f), wind = 0.3f, sun = 0.5f,
        sunColour = new Color(0.9f, 0.7f, 1f), clouds = 0, cloudTint = Color.white, pollen = 1f, speckKind = 1,
        crows = false, groundFog = 0.12f, aurora = 0.2f,
    };
    static readonly Profile SolarFlare = new()
    {
        sky = new Color(1f, 0.95f, 0.86f), land = new Color(1f, 0.96f, 0.9f), haze = new Color(0.8f, 0.6f, 0.35f),
        hazeAlpha = 0.2f, mistAlpha = 0.3f, grade = new Color(1f, 0.6f, 0.2f, 0.07f), wind = 0.5f, sun = 1f,
        sunColour = new Color(1f, 0.85f, 0.6f), clouds = 0, cloudTint = Color.white, pollen = 0.5f, speckKind = 2,
        crows = false, groundFog = 0.1f,
    };
    static readonly Profile PowerDown = new()
    {
        sky = new Color(0.55f, 0.6f, 0.75f), land = new Color(0.65f, 0.7f, 0.85f), haze = new Color(0.12f, 0.15f, 0.3f),
        hazeAlpha = 0.35f, mistAlpha = 0.5f, grade = new Color(0.05f, 0.08f, 0.25f, 0.24f), wind = 0.2f, sun = 0.1f,
        sunColour = new Color(0.6f, 0.75f, 1f), clouds = 0, cloudTint = Color.white, pollen = 0.5f, speckKind = 1,
        crows = false, groundFog = 0.3f,
    };

    // ── Paid worlds (2026-11-12) ────────────────────────────────────────────────────────────────────────────
    // World 7 Dustbowl Canyon: noon sun, sandstorm (dust grade, driving chaff, dust rolling along the floor), dusk,
    // starry canyon night, heat haze; crows stand in for vultures. World 8 Harvest Fairground: day, carnival night
    // (fireworks in PaidWorldDecor), harvest moon, drizzle, golden hour. World 9 Crop Factory is indoors: no clouds or
    // crows, the 'sun' is the lamps, dust motes, steam rolling along the floor, a warm overdrive grade (no flashing).
    static readonly Profile HighNoon = new() { sky = new Color(1.0f, 1.0f, 1.0f), land = new Color(1.0f, 1.0f, 1.0f), haze = new Color(1.0f, 0.86f, 0.66f), hazeAlpha = 0.22f, mistAlpha = 0.3f, grade = new Color(1.0f, 0.8f, 0.5f, 0.04f), wind = 0.6f, sun = 1.0f, sunColour = new Color(1.0f, 0.95f, 0.75f), clouds = 2, cloudTint = new Color(1.0f, 1.0f, 1.0f), pollen = 0.4f, speckKind = 2, crows = true, groundFog = 0.06f };
    static readonly Profile Sandstorm = new() { sky = new Color(1.0f, 0.8f, 0.6f), land = new Color(1.0f, 0.86f, 0.7f), haze = new Color(0.9f, 0.6f, 0.35f), hazeAlpha = 0.5f, mistAlpha = 0.75f, grade = new Color(0.9f, 0.5f, 0.2f, 0.16f), wind = 2.1f, sun = 0.3f, sunColour = new Color(1.0f, 0.8f, 0.55f), clouds = 5, cloudTint = new Color(0.95f, 0.72f, 0.5f), pollen = 1.0f, speckKind = 2, crows = false, groundFog = 0.6f };
    static readonly Profile DesertDusk = new() { sky = new Color(1.0f, 0.74f, 0.7f), land = new Color(0.95f, 0.76f, 0.74f), haze = new Color(0.9f, 0.5f, 0.5f), hazeAlpha = 0.3f, mistAlpha = 0.5f, grade = new Color(0.6f, 0.2f, 0.4f, 0.12f), wind = 0.5f, sun = 1.0f, sunColour = new Color(1.0f, 0.55f, 0.35f), clouds = 3, cloudTint = new Color(1.0f, 0.66f, 0.6f), pollen = 0.6f, speckKind = 1, crows = true, groundFog = 0.15f };
    static readonly Profile CanyonNight = new() { sky = new Color(0.45f, 0.48f, 0.7f), land = new Color(0.6f, 0.58f, 0.78f), haze = new Color(0.35f, 0.32f, 0.55f), hazeAlpha = 0.3f, mistAlpha = 0.45f, grade = new Color(0.12f, 0.1f, 0.35f, 0.22f), wind = 0.3f, sun = 0.25f, sunColour = new Color(0.85f, 0.9f, 1.0f), clouds = 1, cloudTint = new Color(0.5f, 0.5f, 0.7f), pollen = 1.0f, speckKind = 1, crows = false, groundFog = 0.15f };
    static readonly Profile HeatHaze = new() { sky = new Color(1.0f, 0.95f, 0.85f), land = new Color(1.0f, 0.94f, 0.86f), haze = new Color(1.0f, 0.85f, 0.65f), hazeAlpha = 0.4f, mistAlpha = 0.65f, grade = new Color(1.0f, 0.7f, 0.35f, 0.08f), wind = 0.3f, sun = 1.0f, sunColour = new Color(1.0f, 0.92f, 0.7f), clouds = 0, cloudTint = new Color(1.0f, 1.0f, 1.0f), pollen = 0.3f, speckKind = 2, crows = true, groundFog = 0.35f };
    static readonly Profile FairDay = new() { sky = new Color(1.0f, 1.0f, 1.0f), land = new Color(1.0f, 1.0f, 1.0f), haze = new Color(0.9f, 0.92f, 1.0f), hazeAlpha = 0.2f, mistAlpha = 0.3f, grade = new Color(1.0f, 0.9f, 0.7f, 0.0f), wind = 0.7f, sun = 1.0f, sunColour = new Color(1.0f, 0.95f, 0.8f), clouds = 4, cloudTint = new Color(1.0f, 1.0f, 1.0f), pollen = 0.6f, speckKind = 2, crows = true, groundFog = 0.05f };
    static readonly Profile CarnivalNight = new() { sky = new Color(1.0f, 1.0f, 1.0f), land = new Color(0.8f, 0.78f, 0.95f), haze = new Color(0.35f, 0.3f, 0.6f), hazeAlpha = 0.22f, mistAlpha = 0.35f, grade = new Color(0.25f, 0.15f, 0.5f, 0.12f), wind = 0.4f, sun = 0.2f, sunColour = new Color(1.0f, 0.85f, 0.6f), clouds = 1, cloudTint = new Color(0.5f, 0.5f, 0.7f), pollen = 1.0f, speckKind = 1, crows = false, groundFog = 0.15f };
    static readonly Profile HarvestMoon = new() { sky = new Color(1.0f, 0.9f, 0.8f), land = new Color(0.9f, 0.82f, 0.85f), haze = new Color(0.6f, 0.4f, 0.4f), hazeAlpha = 0.25f, mistAlpha = 0.45f, grade = new Color(0.6f, 0.3f, 0.25f, 0.1f), wind = 0.5f, sun = 0.8f, sunColour = new Color(1.0f, 0.75f, 0.45f), clouds = 2, cloudTint = new Color(0.8f, 0.65f, 0.6f), pollen = 0.8f, speckKind = 1, crows = true, groundFog = 0.3f };
    static readonly Profile FairDrizzle = new() { sky = new Color(0.7f, 0.74f, 0.82f), land = new Color(0.8f, 0.82f, 0.86f), haze = new Color(0.65f, 0.68f, 0.76f), hazeAlpha = 0.35f, mistAlpha = 0.55f, grade = new Color(0.2f, 0.25f, 0.4f, 0.14f), wind = 1.1f, rain = 0.6f, sun = 0.0f, sunColour = new Color(1.0f, 1.0f, 1.0f), clouds = 6, cloudTint = new Color(0.65f, 0.68f, 0.75f), pollen = 0.3f, speckKind = 2, crows = false, groundFog = 0.25f };
    static readonly Profile GoldenHour = new() { sky = new Color(1.0f, 0.88f, 0.72f), land = new Color(1.0f, 0.9f, 0.78f), haze = new Color(1.0f, 0.75f, 0.5f), hazeAlpha = 0.26f, mistAlpha = 0.45f, grade = new Color(1.0f, 0.6f, 0.3f, 0.08f), wind = 0.5f, sun = 1.0f, sunColour = new Color(1.0f, 0.75f, 0.45f), clouds = 3, cloudTint = new Color(1.0f, 0.82f, 0.68f), pollen = 0.8f, speckKind = 0, crows = true, groundFog = 0.12f };
    static readonly Profile ShiftStart = new() { sky = new Color(1.0f, 1.0f, 1.0f), land = new Color(1.0f, 1.0f, 1.0f), haze = new Color(0.7f, 0.85f, 0.82f), hazeAlpha = 0.15f, mistAlpha = 0.25f, grade = new Color(0.6f, 0.9f, 0.85f, 0.03f), wind = 0.2f, sun = 0.4f, sunColour = new Color(1.0f, 0.95f, 0.8f), clouds = 0, cloudTint = new Color(1.0f, 1.0f, 1.0f), pollen = 0.5f, speckKind = 2, crows = false, groundFog = 0.1f };
    static readonly Profile SteamWorks = new() { sky = new Color(0.9f, 0.95f, 0.95f), land = new Color(0.92f, 0.96f, 0.96f), haze = new Color(0.85f, 0.9f, 0.9f), hazeAlpha = 0.42f, mistAlpha = 0.8f, grade = new Color(0.8f, 0.9f, 0.9f, 0.08f), wind = 0.4f, sun = 0.3f, sunColour = new Color(1.0f, 0.95f, 0.85f), clouds = 0, cloudTint = new Color(1.0f, 1.0f, 1.0f), pollen = 0.4f, speckKind = 2, crows = false, groundFog = 0.6f };
    static readonly Profile NightShift = new() { sky = new Color(0.55f, 0.65f, 0.75f), land = new Color(0.65f, 0.72f, 0.82f), haze = new Color(0.15f, 0.22f, 0.3f), hazeAlpha = 0.32f, mistAlpha = 0.45f, grade = new Color(0.05f, 0.12f, 0.25f, 0.22f), wind = 0.2f, sun = 0.2f, sunColour = new Color(1.0f, 0.8f, 0.5f), clouds = 0, cloudTint = new Color(1.0f, 1.0f, 1.0f), pollen = 0.8f, speckKind = 1, crows = false, groundFog = 0.25f };
    static readonly Profile Overdrive = new() { sky = new Color(1.0f, 0.85f, 0.75f), land = new Color(1.0f, 0.9f, 0.82f), haze = new Color(0.8f, 0.45f, 0.25f), hazeAlpha = 0.25f, mistAlpha = 0.4f, grade = new Color(0.9f, 0.4f, 0.1f, 0.1f), wind = 0.5f, sun = 0.5f, sunColour = new Color(1.0f, 0.6f, 0.3f), clouds = 0, cloudTint = new Color(1.0f, 1.0f, 1.0f), pollen = 0.8f, speckKind = 2, crows = false, groundFog = 0.3f };
    static readonly Profile Overtime = new() { sky = new Color(0.85f, 0.85f, 0.8f), land = new Color(0.9f, 0.9f, 0.85f), haze = new Color(0.55f, 0.55f, 0.45f), hazeAlpha = 0.3f, mistAlpha = 0.5f, grade = new Color(0.55f, 0.5f, 0.3f, 0.1f), wind = 0.3f, sun = 0.35f, sunColour = new Color(1.0f, 0.85f, 0.6f), clouds = 0, cloudTint = new Color(1.0f, 1.0f, 1.0f), pollen = 0.6f, speckKind = 2, crows = false, groundFog = 0.35f };

    public static Profile For(LevelWeather w) => w switch
    {
        LevelWeather.HighNoon => HighNoon,
        LevelWeather.Sandstorm => Sandstorm,
        LevelWeather.DesertDusk => DesertDusk,
        LevelWeather.CanyonNight => CanyonNight,
        LevelWeather.HeatHaze => HeatHaze,
        LevelWeather.FairDay => FairDay,
        LevelWeather.CarnivalNight => CarnivalNight,
        LevelWeather.HarvestMoon => HarvestMoon,
        LevelWeather.FairDrizzle => FairDrizzle,
        LevelWeather.GoldenHour => GoldenHour,
        LevelWeather.ShiftStart => ShiftStart,
        LevelWeather.SteamWorks => SteamWorks,
        LevelWeather.NightShift => NightShift,
        LevelWeather.Overdrive => Overdrive,
        LevelWeather.Overtime => Overtime,
        LevelWeather.StarField  => StarField,
        LevelWeather.RedAlert   => RedAlert,
        LevelWeather.NebulaGlow => NebulaGlow,
        LevelWeather.SolarFlare => SolarFlare,
        LevelWeather.PowerDown  => PowerDown,
        LevelWeather.SunlitShallows => SunlitShallows,
        LevelWeather.KelpForest     => KelpForest,
        LevelWeather.MurkyDepths    => MurkyDepths,
        LevelWeather.GlowingDeep    => GlowingDeep,
        LevelWeather.Undertow       => Undertow,
        LevelWeather.ClearSkies    => ClearSkies,
        LevelWeather.Windswept     => Windswept,
        LevelWeather.SunsetClouds  => SunsetClouds,
        LevelWeather.StormFront    => StormFront,
        LevelWeather.StarlitSky    => StarlitSky,
        LevelWeather.SummerMorning => SummerMorning,
        LevelWeather.RiverMist     => RiverMist,
        LevelWeather.SpringShower  => SpringShower,
        LevelWeather.LanternDusk   => LanternDusk,
        LevelWeather.Thunderstorm  => Thunderstorm,
        LevelWeather.MorningMist => MorningMist,
        LevelWeather.Overcast    => Overcast,
        LevelWeather.Rain        => Rain,
        LevelWeather.Dusk        => Dusk,
        LevelWeather.AuroraNight => AuroraNight,
        LevelWeather.Snowfall    => Snowfall,
        LevelWeather.Blizzard    => Blizzard,
        LevelWeather.IceFog      => IceFog,
        LevelWeather.PolarDawn   => PolarDawn,
        _                        => Golden,
    };

    // Sorting (see EnvironmentDepthSystem's cornfield orders): far -40 < sun -39 < clouds -38 < ridges -37 <
    // fly-bys/crows -36 < haze veil -35 < hills -33 < mist -32 < mid corn -30 < corn wall -20 < soil -18 <
    // ground fog -17 < play layer (1..10) < specks 30 < rain 38 < grade/flash 39 < corner silhouettes 40.
    const int SunOrder = -39, CloudOrder = -38, CrowOrder = -36, GroundFogOrder = -17, SpeckOrder = 30,
              RainOrder = 38, GradeOrder = 39;
    static readonly int WindID = Shader.PropertyToID("_FFWind");

    Camera _cam;
    Sprite[] _cloudSprites;
    Sprite _disc, _streak, _bird, _solid, _fog;
    Profile _p = Golden;
    float _groundY;
    float _time;

    sealed class Cloud { public SpriteRenderer sr; public float u, ySh, hSh, speed; }
    readonly List<Cloud> _clouds = new();
    SpriteRenderer _sun, _grade, _flash;

    struct Speck { public SpriteRenderer sr; public Vector2 uv; public float size, phase, speed, spin; }
    readonly List<Speck> _specks = new();

    struct Drop { public SpriteRenderer sr; public Vector2 uv; public float speed, len; }
    readonly List<Drop> _drops = new();

    sealed class Fog { public SpriteRenderer sr; public float u, wSh, hSh, speed, phase; }
    readonly List<Fog> _fogs = new();

    // Frozen Tundra extras (2026-11-09): snowflakes, aurora ribbons, glints on the snow.
    struct Flake { public SpriteRenderer sr; public Vector2 uv; public float size, fall, phase; }
    readonly List<Flake> _flakes = new();
    sealed class Ribbon { public SpriteRenderer sr; public float u, ySh, wSh, hSh, phase, hue; }
    readonly List<Ribbon> _ribbons = new();
    struct Glint { public SpriteRenderer sr; public Vector2 uv; public float phase, period, size; }
    readonly List<Glint> _glints = new();
    Sprite _ribbonSprite, _starSprite;
    SpriteRenderer _rainbow;
    const int AuroraOrder = -38;    // with the clouds, behind the glaciers and the haze veil
    const int GlintOrder  = -29;    // on the frozen field, in front of the mid band (-30), behind the corn wall

    readonly List<SpriteRenderer> _crows = new();
    float _crowTimer = 6f, _crowT; bool _crowsFlying; float _crowY, _crowDir;
    float _flashTimer = 5f, _flashT = -1f;

    public void Init(Camera cam, Sprite[] clouds)
    {
        _cam = cam;
        _cloudSprites = clouds;
        _disc = MakeDisc(64, 2.2f);
        _streak = MakeStreak();
        _bird = MakeBird();
        _solid = MakeSolid();
        _fog = MakeDisc(64, 1.4f);
        _sun = NewRenderer("SunGlow", _disc, SunOrder);
        _ribbonSprite = MakeRibbon();
        _starSprite = MakeStar();
        _rainbow = NewRenderer("Rainbow", MakeRainbow(), AuroraOrder);
        _grade = NewRenderer("WeatherGrade", _solid, GradeOrder);
        _flash = NewRenderer("LightningFlash", _solid, GradeOrder);
    }

    /// <summary>Switches to a level's weather (called on every cornfield level start).</summary>
    public void Apply(LevelWeather weather, float groundY)
    {
        _p = For(weather);
        _groundY = groundY;
        var rng = new System.Random((int)weather * 977 + 13);

        // Clouds
        EnsureCount(_clouds, _p.clouds, () => new Cloud { sr = NewRenderer("Cloud", null, CloudOrder) });
        for (int i = 0; i < _clouds.Count; i++)
        {
            var c = _clouds[i];
            bool on = i < _p.clouds && _cloudSprites != null && _cloudSprites.Length > 0;
            c.sr.enabled = on;
            if (!on) continue;
            c.sr.sprite = _cloudSprites[i % _cloudSprites.Length];
            c.sr.flipX = (i & 1) == 1;
            c.u = (i + 0.5f) / Mathf.Max(1, _p.clouds) + (float)(rng.NextDouble() - 0.5) * 0.12f;
            c.ySh = 0.70f + (float)rng.NextDouble() * 0.22f;
            c.hSh = (_p.clouds > 5 ? 0.13f : 0.09f) + (float)rng.NextDouble() * 0.07f;
            c.speed = 0.004f + (float)rng.NextDouble() * 0.006f;
            c.sr.color = new Color(_p.cloudTint.r, _p.cloudTint.g, _p.cloudTint.b, _p.clouds > 5 ? 0.95f : 0.85f);
        }

        // Specks
        int speckCount = Mathf.RoundToInt(36 * _p.pollen);
        EnsureCount(_specks, speckCount, () => new Speck { sr = NewRenderer("Speck", _disc, SpeckOrder) });
        for (int i = 0; i < _specks.Count; i++)
        {
            var s = _specks[i];
            s.sr.enabled = i < speckCount;
            s.sr.sprite = _p.speckKind == 2 || _p.speckKind == 4 ? _streak : _disc;
            s.uv = new Vector2((float)rng.NextDouble(), 0.08f + (float)rng.NextDouble() * 0.8f);
            s.size = _p.speckKind switch { 1 => 0.014f, 2 => 0.02f, 3 => 0.011f, 4 => 0.016f, _ => 0.008f } * (0.6f + (float)rng.NextDouble() * 0.8f);
            s.phase = (float)rng.NextDouble() * 10f;
            s.speed = 0.01f + (float)rng.NextDouble() * 0.02f;
            s.spin = ((float)rng.NextDouble() - 0.5f) * 400f;
            _specks[i] = s;
        }

        // Rain
        int dropCount = Mathf.RoundToInt(150 * _p.rain);
        EnsureCount(_drops, dropCount, () => new Drop { sr = NewRenderer("Rain", _streak, RainOrder) });
        for (int i = 0; i < _drops.Count; i++)
        {
            var d = _drops[i];
            d.sr.enabled = i < dropCount;
            d.uv = new Vector2((float)rng.NextDouble() * 1.2f - 0.1f, (float)rng.NextDouble());
            d.speed = 1.3f + (float)rng.NextDouble() * 0.6f;
            d.len = 0.035f + (float)rng.NextDouble() * 0.035f;
            d.sr.color = new Color(0.82f, 0.87f, 0.96f, 0.22f + (float)rng.NextDouble() * 0.14f);
            _drops[i] = d;
        }

        // Ground fog banks
        int fogCount = _p.groundFog > 0.01f ? 7 : 0;
        EnsureCount(_fogs, fogCount, () => new Fog { sr = NewRenderer("GroundFog", _fog, GroundFogOrder) });
        for (int i = 0; i < _fogs.Count; i++)
        {
            var f = _fogs[i];
            f.sr.enabled = i < fogCount;
            f.u = i / 6f + (float)rng.NextDouble() * 0.08f;
            f.wSh = 0.55f + (float)rng.NextDouble() * 0.4f;
            f.hSh = 0.10f + (float)rng.NextDouble() * 0.06f;
            f.speed = 0.006f + (float)rng.NextDouble() * 0.008f;
            f.phase = (float)rng.NextDouble() * 6f;
        }

        // Snowflakes
        int flakeCount = Mathf.RoundToInt(110 * _p.snow);
        EnsureCount(_flakes, flakeCount, () => new Flake { sr = NewRenderer("Snowflake", _disc, SpeckOrder) });
        for (int i = 0; i < _flakes.Count; i++)
        {
            var f = _flakes[i];
            f.sr.enabled = i < flakeCount;
            f.uv = new Vector2((float)rng.NextDouble() * 1.2f - 0.1f, (float)rng.NextDouble());
            float near = (float)rng.NextDouble();                       // a few big near flakes, many small far ones
            f.size = Mathf.Lerp(0.004f, 0.013f, near * near);
            f.fall = Mathf.Lerp(0.035f, 0.11f, near);
            f.phase = (float)rng.NextDouble() * 10f;
            f.sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.5f, 0.95f, near));
            _flakes[i] = f;
        }

        // Aurora ribbons
        int ribbonCount = _p.aurora > 0.01f ? 3 : 0;
        EnsureCount(_ribbons, ribbonCount, () => new Ribbon { sr = NewRenderer("Aurora", _ribbonSprite, AuroraOrder) });
        for (int i = 0; i < _ribbons.Count; i++)
        {
            var r = _ribbons[i];
            r.sr.enabled = i < ribbonCount;
            r.u = 0.15f + i * 0.33f + (float)rng.NextDouble() * 0.1f;
            r.ySh = 0.66f + (float)rng.NextDouble() * 0.18f;
            r.wSh = 0.9f + (float)rng.NextDouble() * 0.6f;
            r.hSh = 0.22f + (float)rng.NextDouble() * 0.12f;
            r.phase = (float)rng.NextDouble() * 10f;
            r.hue = (float)rng.NextDouble();
        }

        // Glints on the snow
        int glintCount = Mathf.RoundToInt(18 * _p.sparkle);
        EnsureCount(_glints, glintCount, () => new Glint { sr = NewRenderer("Glint", _starSprite, GlintOrder) });
        for (int i = 0; i < _glints.Count; i++)
        {
            var g = _glints[i];
            g.sr.enabled = i < glintCount;
            g.uv = new Vector2((float)rng.NextDouble(), 0.0f);
            g.phase = (float)rng.NextDouble() * 6f;
            g.period = 1.6f + (float)rng.NextDouble() * 2.4f;
            g.size = 0.012f + (float)rng.NextDouble() * 0.014f;
            _glints[i] = g;
        }
        // Blizzard streaks are white and bright.
        if (_p.blizzard)
            for (int i = 0; i < _drops.Count; i++)
                _drops[i].sr.color = new Color(1f, 1f, 1f, 0.35f + (float)rng.NextDouble() * 0.25f);

        _crowsFlying = false;
        foreach (var c in _crows) c.enabled = false;
        _crowTimer = 4f + (float)rng.NextDouble() * 6f;
        _flashTimer = 3f + (float)rng.NextDouble() * 5f;
        _flashT = -1f;
        _flash.enabled = false;
    }

    public Profile Current => _p;

    void OnDisable() => Shader.SetGlobalFloat(WindID, 0f);

    void Update()
    {
        if (_cam == null) return;
        float dt = Time.deltaTime;
        _time += dt;
        float camH = _cam.orthographicSize * 2f, camW = camH * _cam.aspect;
        Vector3 c = _cam.transform.position;
        float left = c.x - camW * 0.5f, bottom = c.y - camH * 0.5f;
        Vector3 Screen(float u, float v) => new(left + u * camW, bottom + v * camH, 0f);

        // Wind: the profile's strength with slow gusts.
        float gust = 1f + 0.35f * Mathf.Sin(_time * 0.37f) * Mathf.Sin(_time * 0.11f + 1.7f) + 0.15f * Mathf.Sin(_time * 1.3f);
        float wind = _p.wind * gust;
        Shader.SetGlobalFloat(WindID, wind);

        // Sun glow, upper left (the far painting's light comes from there).
        _sun.enabled = _p.sun > 0.01f;
        if (_sun.enabled)
        {
            float s = camH * 0.85f;
            _sun.transform.position = Screen(0.22f, 0.86f);
            _sun.transform.localScale = new Vector3(s, s, 1f);
            float pulse = 0.92f + 0.08f * Mathf.Sin(_time * 0.6f);
            _sun.color = new Color(_p.sunColour.r, _p.sunColour.g, _p.sunColour.b, 0.45f * _p.sun * pulse);
        }

        // Rainbow (spring shower): a faint arc over the valley, on the right (away from the sun), breathing slightly.
        _rainbow.enabled = _p.rainbow > 0.01f;
        if (_rainbow.enabled)
        {
            float rw = camH * 1.35f;
            _rainbow.transform.position = Screen(0.68f, 0.42f);
            _rainbow.transform.localScale = new Vector3(rw, rw, 1f);
            _rainbow.color = new Color(1f, 1f, 1f, _p.rainbow * (0.32f + 0.06f * Mathf.Sin(_time * 0.25f)));
        }

        // Clouds drift with the wind, screen-locked (they are far away), wrapping round.
        foreach (var cl in _clouds)
        {
            if (!cl.sr.enabled || cl.sr.sprite == null) continue;
            cl.u += cl.speed * Mathf.Max(0.3f, wind) * dt;
            float h = cl.hSh * camH, scale = h / cl.sr.sprite.bounds.size.y, w = cl.sr.sprite.bounds.size.x * scale;
            float span = 1f + 2f * w / camW;
            float u = Mathf.Repeat(cl.u, 1f) * span - w / camW;
            cl.sr.transform.position = new Vector3(left + u * camW, bottom + cl.ySh * camH, 0f);
            cl.sr.transform.localScale = new Vector3(scale, scale, 1f);
        }

        // Grade + lightning.
        _grade.enabled = _p.grade.a > 0.001f;
        Fill(_grade, c, camW, camH);
        _grade.color = _p.grade;
        if (_p.lightning)
        {
            _flashTimer -= dt;
            if (_flashTimer <= 0f) { _flashT = 0f; _flashTimer = Random.Range(6f, 14f); }
            if (_flashT >= 0f)
            {
                _flashT += dt;
                // Double flicker: bright, dim, bright, decay.
                float a = _flashT < 0.06f ? 0.55f : _flashT < 0.12f ? 0.1f : _flashT < 0.18f ? 0.45f : Mathf.Max(0f, 0.45f - (_flashT - 0.18f) * 1.6f);
                _flash.enabled = a > 0.001f;
                _flash.color = new Color(0.92f, 0.95f, 1f, a);
                Fill(_flash, c, camW, camH);
                if (_flashT > 0.5f) { _flashT = -1f; _flash.enabled = false; }
            }
        }

        // Specks: pollen drifts downwind and bobs; fireflies wander and pulse; chaff tumbles quickly.
        foreach (var sp0 in _specks)
        {
            if (!sp0.sr.enabled) continue;
            var sp = sp0;
            float drift = _p.speckKind == 1 ? 0.004f : sp.speed * Mathf.Max(0.3f, wind);
            float u = Mathf.Repeat(sp.uv.x + _time * drift, 1f);
            float v = sp.uv.y + Mathf.Sin(_time * (_p.speckKind == 1 ? 0.9f : 0.6f) + sp.phase) * 0.025f;
            if (_p.speckKind == 1) u = Mathf.Repeat(u + Mathf.Sin(_time * 0.5f + sp.phase * 2f) * 0.02f, 1f);
            sp.sr.transform.position = Screen(u, Mathf.Clamp(v, 0.04f, 0.95f));
            float size = sp.size * camH;
            if (_p.speckKind == 3)
            {
                // Blossom petals off the willows: tumble (squash as they flip) while they drift and sink slowly.
                float flip = Mathf.Abs(Mathf.Sin(_time * 2.4f + sp.phase * 4f));
                float sink = Mathf.Repeat(sp.uv.y - _time * 0.02f * (0.5f + sp.speed * 30f), 0.9f) + 0.05f;
                sp.sr.transform.position = Screen(u, sink);
                sp.sr.transform.localScale = new Vector3(size, size * (0.25f + 0.75f * flip), 1f);
                sp.sr.transform.rotation = Quaternion.Euler(0, 0, _time * sp.spin * 0.4f + sp.phase * 50f);
                sp.sr.color = (sp.phase % 2f) < 1f ? new Color(1f, 0.82f, 0.88f, 0.85f) : new Color(1f, 0.97f, 0.95f, 0.85f);
            }
            else if (_p.speckKind == 4)
            {
                // Dragonflies over the reeds: hover, then dart to a new spot, low over the water band.
                float hop = Mathf.Floor((_time + sp.phase) / 1.6f);
                float t = Mathf.Repeat(_time + sp.phase, 1.6f) / 1.6f;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.75f) / 0.25f));
                float h0 = Hash(hop, sp.phase), h1 = Hash(hop + 1f, sp.phase);
                float du = Mathf.Lerp(h0, h1, k) * 0.12f - 0.06f;
                float dv = Mathf.Lerp(Hash(h0 * 7.1f, sp.phase), Hash(h1 * 7.1f, sp.phase), k) * 0.08f;
                float baseV = (_groundY + 0.6f - bottom) / camH;
                sp.sr.transform.position = Screen(Mathf.Repeat(sp.uv.x + du, 1f), baseV + 0.02f + dv + Mathf.Sin(_time * 9f + sp.phase) * 0.003f);
                sp.sr.transform.localScale = new Vector3(size * 0.22f, size, 1f);
                sp.sr.transform.rotation = Quaternion.Euler(0, 0, 90f + (h1 > h0 ? -8f : 8f));
                sp.sr.color = new Color(0.25f, 0.75f, 0.85f, 0.9f);
            }
            else if (_p.speckKind == 2)
            {
                sp.sr.transform.localScale = new Vector3(size * 0.35f, size, 1f);
                sp.sr.transform.rotation = Quaternion.Euler(0, 0, _time * sp.spin + sp.phase * 50f);
                sp.sr.color = new Color(0.55f, 0.42f, 0.25f, 0.75f);
            }
            else
            {
                sp.sr.transform.localScale = new Vector3(size, size, 1f);
                sp.sr.transform.rotation = Quaternion.identity;
                sp.sr.color = _p.speckKind == 1
                    ? new Color(1f, 0.93f, 0.45f, 0.35f + 0.55f * Mathf.Max(0f, Mathf.Sin(_time * 2.2f + sp.phase * 3f)))
                    : new Color(1f, 0.97f, 0.85f, 0.55f);
            }
        }

        UpdateFrozen(dt, wind, camW, camH, left, bottom);

        // Rain: streaks slanted by the wind, falling fast, wrapping. A blizzard drives them almost sideways.
        float slant = _p.blizzard ? Mathf.Clamp(wind * 0.7f, 0.8f, 2.2f) : Mathf.Clamp(wind * 0.18f, 0f, 0.45f);
        float angle = Mathf.Atan(slant) * Mathf.Rad2Deg;
        for (int i = 0; i < _drops.Count; i++)
        {
            var d = _drops[i];
            if (!d.sr.enabled) continue;
            d.uv.y -= d.speed * dt;
            d.uv.x += d.speed * slant * dt / _cam.aspect;
            if (d.uv.y < -0.05f) { d.uv.y += 1.1f; d.uv.x = Random.Range(-0.25f, 1.05f); }
            if (d.uv.x > 1.1f) d.uv.x -= 1.3f;
            _drops[i] = d;
            d.sr.transform.position = Screen(d.uv.x, d.uv.y);
            d.sr.transform.localScale = new Vector3((_p.blizzard ? 0.005f : 0.0035f) * camH, d.len * camH * (_p.blizzard ? 0.7f : 1f), 1f);
            d.sr.transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        // Ground fog: wide soft banks rolling along the field's ground line.
        foreach (var f in _fogs)
        {
            if (!f.sr.enabled) continue;
            f.u += f.speed * Mathf.Max(0.3f, wind) * dt;
            float span = 1f + f.wSh * 2f / _cam.aspect;
            float u = Mathf.Repeat(f.u, 1f) * span - f.wSh / _cam.aspect;
            float y = _groundY + 0.25f * f.hSh * camH;
            f.sr.transform.position = new Vector3(left + u * camW, y, 0f);
            f.sr.transform.localScale = new Vector3(f.wSh * camH, f.hSh * camH, 1f);
            float breathe = 0.8f + 0.2f * Mathf.Sin(_time * 0.3f + f.phase);
            f.sr.color = new Color(_p.haze.r, _p.haze.g, _p.haze.b, _p.groundFog * breathe);
        }

        UpdateCrows(dt, camW, camH, left, bottom);
    }

    void UpdateFrozen(float dt, float wind, float camW, float camH, float left, float bottom)
    {
        // Snowflakes: drift down, sway, pushed sideways by the wind; wrap.
        for (int i = 0; i < _flakes.Count; i++)
        {
            var f = _flakes[i];
            if (!f.sr.enabled) continue;
            f.uv.y -= f.fall * dt;
            f.uv.x += (0.012f * wind + Mathf.Sin(_time * 1.3f + f.phase) * 0.01f) * dt * (1f + f.size * 60f);
            if (f.uv.y < -0.03f) { f.uv.y += 1.06f; f.uv.x = Random.Range(-0.15f, 1.05f); }
            if (f.uv.x > 1.08f) f.uv.x -= 1.16f;
            _flakes[i] = f;
            f.sr.transform.position = new Vector3(left + f.uv.x * camW, bottom + f.uv.y * camH, 0f);
            float s = f.size * camH;
            f.sr.transform.localScale = new Vector3(s, s, 1f);
        }

        // Aurora: slow drifting curtains whose brightness breathes and whose colour wanders green <-> violet.
        foreach (var r in _ribbons)
        {
            if (!r.sr.enabled) continue;
            float u = r.u + Mathf.Sin(_time * 0.05f + r.phase) * 0.06f;
            float w = r.wSh * camH, h = r.hSh * camH * (0.9f + 0.1f * Mathf.Sin(_time * 0.4f + r.phase));
            r.sr.transform.position = new Vector3(left + u * camW, bottom + r.ySh * camH, 0f);
            r.sr.transform.localScale = new Vector3(w, h, 1f);
            r.sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_time * 0.11f + r.phase) * 6f);
            float k = 0.5f + 0.5f * Mathf.Sin(_time * 0.17f + r.hue * 6f);
            Color c = Color.Lerp(new Color(0.35f, 1f, 0.75f), new Color(0.72f, 0.45f, 1f), k);
            c.a = _p.aurora * (0.30f + 0.18f * Mathf.Sin(_time * 0.6f + r.phase));
            r.sr.color = c;
        }

        // Glints: tiny stars that flash on the snowfield and fade.
        float fieldTop = (_groundY + 3.4f - bottom) / camH, fieldBottom = (_groundY + 0.4f - bottom) / camH;
        for (int i = 0; i < _glints.Count; i++)
        {
            var g = _glints[i];
            if (!g.sr.enabled) continue;
            float t = Mathf.Repeat(_time + g.phase, g.period) / g.period;
            if (t < dt / g.period * 1.5f) { g.uv = new Vector2(Random.value, Random.value); _glints[i] = g; }
            float v = Mathf.Lerp(fieldBottom, fieldTop, g.uv.y);
            g.sr.transform.position = new Vector3(left + g.uv.x * camW, bottom + v * camH, 0f);
            float flash = t < 0.18f ? Mathf.Sin(t / 0.18f * Mathf.PI) : 0f;
            float s = g.size * camH * (0.4f + 0.8f * flash);
            g.sr.transform.localScale = new Vector3(s, s, 1f);
            g.sr.transform.rotation = Quaternion.Euler(0f, 0f, t * 90f);
            g.sr.color = new Color(1f, 1f, 1f, 0.95f * flash);
        }
    }

    // A soft vertical aurora curtain: bright fringe along its lower edge fading upward, rayed by vertical streaks.
    static Sprite MakeRibbon()
    {
        const int W = 128, H = 64;
        var tex = new Texture2D(W, H, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[W * H];
        var rng = new System.Random(7);
        var rays = new float[W];
        for (int x = 0; x < W; x++) rays[x] = 0.55f + 0.45f * (float)rng.NextDouble();
        for (int x = 1; x < W - 1; x++) rays[x] = (rays[x - 1] + rays[x] + rays[x + 1]) / 3f;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                float edge = Mathf.Sin(u * Mathf.PI);                                  // fade at both ends
                float wave = 0.18f + 0.08f * Mathf.Sin(u * 9f) + 0.05f * Mathf.Sin(u * 23f);
                float body = v < wave ? Mathf.Clamp01(v / wave) : Mathf.Pow(1f - Mathf.Clamp01((v - wave) / (1f - wave)), 1.6f);
                px[y * W + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(body * edge * rays[x]));
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), W);   // 1 x 0.5 units
    }

    static float Hash(float a, float b) => Mathf.Repeat(Mathf.Sin(a * 12.9898f + b * 78.233f) * 43758.5453f, 1f);

    // A rainbow arc: concentric hue bands on the upper half of a ring, soft edges, fading toward both feet.
    static Sprite MakeRainbow()
    {
        const int W = 256, H = 128;
        var tex = new Texture2D(W, H, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[W * H];
        Color[] bands = { new(0.85f, 0.2f, 0.25f), new(1f, 0.55f, 0.15f), new(1f, 0.9f, 0.25f), new(0.35f, 0.85f, 0.35f),
                          new(0.25f, 0.55f, 1f), new(0.45f, 0.3f, 0.85f) };
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float dx = (x + 0.5f) / W * 2f - 1f, dy = (y + 0.5f) / H;   // centre at the bottom middle
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float t = (0.98f - r) / 0.16f;                               // 0 outer edge .. 1 inner edge
                if (t <= 0f || t >= 1f) { px[y * W + x] = Color.clear; continue; }
                float b = t * bands.Length - 0.5f;
                int i0 = Mathf.Clamp(Mathf.FloorToInt(b), 0, bands.Length - 1), i1 = Mathf.Clamp(i0 + 1, 0, bands.Length - 1);
                Color c = Color.Lerp(bands[i0], bands[i1], Mathf.Clamp01(b - i0));
                c.a = Mathf.Clamp01(Mathf.Sin(t * Mathf.PI) * 1.3f) * Mathf.Clamp01(dy / 0.35f);
                px[y * W + x] = c;
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), W);   // 1 x 0.5 units, pivot at the base
    }

    // A four-point twinkle star.
    static Sprite MakeStar()
    {
        const int N = 48;
        var tex = new Texture2D(N, N, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = Mathf.Abs((x + 0.5f) / N - 0.5f) * 2f, dy = Mathf.Abs((y + 0.5f) / N - 0.5f) * 2f;
                float arms = Mathf.Max(Mathf.Clamp01(1f - dx * 6f) * (1f - dy), Mathf.Clamp01(1f - dy * 6f) * (1f - dx));
                float core = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.5f);
                px[y * N + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(arms + core));
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
    }

    void UpdateCrows(float dt, float camW, float camH, float left, float bottom)
    {
        if (!_p.crows) { foreach (var cr in _crows) cr.enabled = false; return; }
        if (!_crowsFlying)
        {
            _crowTimer -= dt;
            if (_crowTimer > 0f) return;
            _crowsFlying = true;
            _crowT = 0f;
            _crowY = Random.Range(0.55f, 0.78f);
            _crowDir = Random.value < 0.6f ? -1f : 1f;
            int n = Random.Range(3, 7);
            EnsureRenderers(_crows, n, () => NewRenderer("Crow", _bird, CrowOrder));
            for (int i = 0; i < _crows.Count; i++) _crows[i].enabled = i < n;
        }
        _crowT += dt / 14f;
        float lead = _crowDir > 0 ? _crowT : 1f - _crowT;
        int k = 0;
        foreach (var cr in _crows)
        {
            if (!cr.enabled) continue;
            // Loose V: each bird a little behind and to one side of the leader.
            float back = (k + 1) / 2 * 0.035f, side = (k % 2 == 0 ? 1 : -1) * ((k + 1) / 2) * 0.022f;
            float u = -0.08f + lead * 1.16f - back * _crowDir;
            float v = _crowY + side + Mathf.Sin(_time * 1.1f + k) * 0.006f;
            cr.transform.position = new Vector3(left + u * camW, bottom + v * camH, 0f);
            float s = 0.022f * camH / _bird.bounds.size.x;
            float flap = Mathf.Sin(_time * 9f + k * 1.7f);
            cr.transform.localScale = new Vector3(s, s * (0.35f + 0.65f * Mathf.Abs(flap)) * Mathf.Sign(flap + 0.3f), 1f);
            cr.color = new Color(0.16f, 0.14f, 0.16f, 0.8f);
            k++;
        }
        if (_crowT >= 1f)
        {
            _crowsFlying = false;
            foreach (var cr in _crows) cr.enabled = false;
            _crowTimer = Random.Range(18f, 34f);
        }
    }

    static void Fill(SpriteRenderer sr, Vector3 c, float w, float h)
    {
        sr.transform.position = new Vector3(c.x, c.y, 0f);
        sr.transform.localScale = new Vector3(w * 1.1f, h * 1.1f, 1f);
    }

    static void EnsureCount<T>(List<T> list, int n, System.Func<T> make) { while (list.Count < n) list.Add(make()); }
    static void EnsureRenderers(List<SpriteRenderer> list, int n, System.Func<SpriteRenderer> make) { while (list.Count < n) list.Add(make()); }

    SpriteRenderer NewRenderer(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    // ── procedural sprites (1 world unit across at scale 1) ─────────────────────────────────────────────────────
    static Sprite MakeDisc(int n, float falloff)
    {
        var tex = new Texture2D(n, n, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Pow(1f - d, falloff));
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }

    static Sprite MakeStreak()
    {
        const int W = 4, H = 32;
        var tex = new Texture2D(W, H, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = (y + 0.5f) / H;
            float a = Mathf.Sin(v * Mathf.PI);
            for (int x = 0; x < W; x++) px[y * W + x] = new Color(1f, 1f, 1f, a * (x == 0 || x == W - 1 ? 0.4f : 1f));
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), H);
    }

    // A distant bird: two shallow wing arcs meeting at the body (the classic far-away gull/crow "M").
    static Sprite MakeBird()
    {
        const int W = 64, H = 24;
        var tex = new Texture2D(W, H, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[W * H];
        for (int x = 0; x < W; x++)
        {
            float t = Mathf.Abs(x - W * 0.5f) / (W * 0.5f);          // 0 body .. 1 wing tip
            float yc = 8f + 10f * Mathf.Sin(t * Mathf.PI) * (1f - 0.35f * t) - 4f * t;
            float thick = Mathf.Lerp(3.2f, 1.2f, t);
            for (int y = 0; y < H; y++)
            {
                float a = Mathf.Clamp01(thick - Mathf.Abs(y - yc));
                px[y * W + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), W);
    }

    static Sprite MakeSolid()
    {
        var tex = new Texture2D(4, 4, TextureFormat.ARGB32, false);
        var px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
    }
}
