# Changelog – com.farmfury.franchise

## 0.2.0 – 2026-11-13
- Shared ability SFX: `Runtime/Audio/Abilities/<Hero>_Ability.mp3` for Bessie, Billy, Cluck, Ducky, Gerald, Horace,
  Percy (byte-identical in Arcade, Stampede and Cannonfire; Stampede's files and GUIDs, so Stampede's and Arcade's
  scene references need a GUID remap only for Arcade). Woolly's differs per game and stays in each game.
- Not included: world music. The tracks are identical but Stampede imports them streamed at quality 0.7 and
  Cannonfire decompressed at quality 1; they move once one import setting is chosen.

## 0.1.0 – 2026-11-13
- Skeleton: `CharacterIdentity` ScriptableObject. Content arrives in Phase 5.
