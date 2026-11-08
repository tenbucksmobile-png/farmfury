using UnityEngine;

public class StoneBlock : BlockBase
{
    protected override void Awake()
    {
        // 220 -> 70 (2026-11-08, user: "the game is too hard requiring multiple hits"): stone is the tough material,
        // as in Angry Birds, but a Cluck now cracks a stone box in ~2 hits and Bessie smashes it in one.
        baseMaxHealth = 70f;
        baseMass      = 8f;
        bounciness    = 0.1f;
        // Self-healing — 2026-08-09, same pattern as the fields above. An audit found this
        // prefab still serialized at 2.2 (over 4x wood's own 0.5 death-burst size) — the pre-
        // 2026-07-10 value, from before every block type was settled on a much smaller,
        // consistent burst. Reduced to 1.0 (2x wood's 0.5, not 4.4x) per explicit user request
        // ("reduce the 4x radius to only two, make it seem realistic") — asserted here so it
        // can't silently drift back to whatever's serialized on the prefab.
        _explodeSizeMultiplier = 1.0f;
        base.Awake();
        if (_sr) _sr.color = new Color(0.55f, 0.55f, 0.58f); // stone grey
    }

    protected override void PlayHitSound() =>
        AudioManager.Play(AudioManager.Sound.StoneHit, cooldown: 0.08f);
}