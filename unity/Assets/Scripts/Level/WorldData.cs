using UnityEngine;

/// <summary>
/// One world's menu identity and unlock rule (franchise convention, ported from Stampede's WorldData). Created by
/// FarmFury > Generate World Data (WorldDataGenerator) into Assets/ScriptableObjects/Worlds and wired onto
/// DataManager by Wire Scene References. Art left null falls back to the shared MenuArt stand-ins (see
/// docs/ART_REQUIREMENTS.md for what each world still needs).
/// </summary>
[CreateAssetMenu(fileName = "WorldData", menuName = "FarmFury/World Data", order = 1)]
public class WorldData : ScriptableObject
{
    public WorldType worldType;
    public string displayName;

    [Tooltip("Planned level count (GDD). Level Select shows this many slots; levels without LevelData show as coming soon.")]
    public int levelCount;

    [Tooltip("levelId of this world's boss level. Clearing it unlocks the next story world.")]
    public string bossLevelId;

    [Tooltip("The world's launcher, for Character Story / Level Select copy (GDD world table).")]
    public string launcherName;

    [TextArea] public string blurb;

    [Tooltip("Animals first introduced in this world: they unlock when the world does (GDD animal table).")]
    public AnimalType[] introducedAnimals;

    [Header("Menu art (null = shared stand-in)")]
    [Tooltip("World Select carousel badge (wooden shield, world name baked in).")]
    public Sprite selectBadge;
    [Tooltip("Full-screen Level Select backdrop (world name baked in along the top).")]
    public Sprite levelSelectBackground;
    [Tooltip("Level Complete backdrop (title, logo and three gold stars painted in).")]
    public Sprite levelCompleteBackground;
    [Tooltip("Level Failed backdrop (title and logo painted in).")]
    public Sprite levelFailedBackground;
    [Tooltip("Pause backdrop.")]
    public Sprite pauseBackground;
    [Tooltip("New Character page backdrop (logo painted in, no lettering).")]
    public Sprite newCharacterBackground;
    [Tooltip("Pre-level character select backdrop ('Character' lettering painted in).")]
    public Sprite characterSelectBackground;

    [Header("Unlock")]
    [Tooltip("Paid post-finale world: unlocked by purchase alone, no boss needed.")]
    public bool purchaseRequired;
}
