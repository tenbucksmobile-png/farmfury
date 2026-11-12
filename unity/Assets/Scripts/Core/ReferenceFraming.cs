using UnityEngine;

/// <summary>
/// Screen framing measured from the Angry Birds 2 reference recording (docs/REFERENCE_FRAMEWORK.md, 2026-11-08).
/// A level with <see cref="LevelData.referenceFraming"/> set is framed by these rules instead of the old
/// content-fitting auto-zoom, so every such level shows its animals, robots and blocks at the same on-screen size.
///
/// The reference showed FarmFury's sprites are already in proportion with each other (robot ≈ 1.25x the animal,
/// haybale ≈ 1.1x, cannon ≈ 2.5x — AB2: pig 1-1.5x bird, block ≈ 1x, sling ≈ 2x); what was wrong was the zoom
/// (everything about 1.6x too close). So the rule fixes the zoom from the animal's size rather than rescaling art.
///
/// World 1 also gets one shared ground line: every hand-placed World 1 prop (barn, fences, rocks, barrels) and the
/// FarmCannon stand at y ≈ -5.6, while the physics ground was at -6.6, so anything knocked loose fell a full unit
/// below the scenery. Framed levels move the physics ground up to the scenery's line and pin the painted meadow
/// to it (EnvironmentDepthSystem).
/// </summary>
public static class ReferenceFraming
{
    /// <summary>AB2: the bird is 0.040-0.045 of the screen height.</summary>
    public const float AnimalScreenHeight = 0.045f;

    /// <summary>FarmFury's animal on screen: collider diameter 0.72 (CatapultLauncher.BirdScale is solved to it).</summary>
    public const float AnimalWorldHeight = 0.72f;

    /// <summary>AB2: the slingshot sits 0.26-0.27 of the screen width from the left.</summary>
    public const float LauncherScreenX = 0.26f;

    /// <summary>Where the ground line sits, as a fraction of the screen height from the bottom (classic flat-ground
    /// framing; AB2's floating islands sit higher, but World 1 is a meadow).</summary>
    public const float GroundScreenY = 0.12f;

    /// <summary>World 1's shared ground line: where the hand-placed props and the FarmCannon stand.</summary>
    public const float World1GroundY = -5.6f;

    /// <summary>Height of the painted meadow edge in ParallaxForeground.png, as a UV fraction from the bottom.</summary>
    public const float World1PaintedGroundUV = 0.19f;

    /// <summary>Orthographic size that draws the animal at <see cref="AnimalScreenHeight"/> (= 8.0).</summary>
    public static float OrthoSize => AnimalWorldHeight / (2f * AnimalScreenHeight);

    // ── Flight and aim, in screen heights (sh), measured from the AB2 recording ─────────────────────────────
    // Gravity from the preview-dot arc (y = 0.52-0.70·x² in sh) and 1.2-1.5 s release-to-impact; launch speed
    // ≈0.85 sh/s. Aim distances keep the finger travel the same fraction of the screen as before the reframe.

    /// <summary>Projectile gravity, sh/s² (AB2 ≈ 0.5; FarmFury was 0.23 at this zoom).</summary>
    public const float ProjectileGravitySh = 0.5f;
    /// <summary>Full-pull launch speed, sh/s.</summary>
    public const float MaxLaunchSpeedSh = 0.85f * FlightPace;
    /// <summary>Softest-pull launch speed, sh/s (same min/max ratio as the original 3.8/9.3).</summary>
    public const float MinLaunchSpeedSh = 0.35f * FlightPace;
    /// <summary>Pull distance for full power, sh (was 2.4 u at ortho 4.79 = 0.25 sh).</summary>
    public const float MaxDragSh = 0.25f;
    /// <summary>Angle deadzone around the pivot, sh (was 0.6 u = 0.063 sh).</summary>
    public const float AimDeadzoneSh = 0.063f;
    /// <summary>How close to the loaded animal a press must land to start aiming, sh (was 1.2 u = 0.125 sh).</summary>
    public const float GrabRadiusSh = 0.125f;
    /// <summary>Trajectory preview dot diameter, sh (was 0.16 u = 0.017 sh).</summary>
    public const float TrajectoryDotSh = 0.017f;

    /// <summary>Flight pace (2026-11-12, user: "the character still flys too fast, slow them down - almost slow mo ...
    /// it should not feel rushed - but the user should feel the anxiety, anticipation of their shot"). Time-scales the
    /// shot: launch speeds x pace, projectile gravity x pace squared - every arc keeps exactly the same shape and reach
    /// (the preview, the designers' reach checks), it just takes 1/pace as long. The animal's impact mass is divided by
    /// the pace (AnimalBase.Launch) so a hit carries the same momentum as before; abilities follow the same pace
    /// (AnimalBase.AbilitySpeed, AbilityProjectile.Spawn, Cluck's eggs, Bessie's slam).</summary>
    public const float FlightPace = 0.6f;

    /// <summary>Screen heights to world units at the framed zoom.</summary>
    public static float ToWorld(float sh) => sh * 2f * OrthoSize;

    /// <summary>Rigidbody2D.gravityScale that gives <see cref="ProjectileGravitySh"/> (0.40 with gravity -20).</summary>
    public static float ProjectileGravityScale =>
        ToWorld(ProjectileGravitySh) * FlightPace * FlightPace / Mathf.Max(0.01f, Mathf.Abs(Physics2D.gravity.y));

    public static bool Active(LevelData level) => level != null && level.referenceFraming;

    /// <summary>Ground line for a framed level (only World 1 has a measured one so far).</summary>
    public static float GroundY(LevelData level) => World1GroundY;

    /// <summary>Camera centre that puts the launcher at <see cref="LauncherScreenX"/> and the ground at
    /// <see cref="GroundScreenY"/>.</summary>
    public static Vector2 CameraCentre(float launcherX, float groundY, float aspect)
    {
        float ortho = OrthoSize;
        return new Vector2(
            launcherX + (0.5f - LauncherScreenX) * 2f * ortho * aspect,
            groundY + (0.5f - GroundScreenY) * 2f * ortho);
    }
}
