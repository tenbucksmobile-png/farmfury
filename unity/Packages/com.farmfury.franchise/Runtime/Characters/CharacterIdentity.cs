using UnityEngine;

namespace FarmFury.Franchise
{
    /// <summary>
    /// One Farm Fury hero as the franchise knows them (name, story, colours, portrait), shared by every game.
    /// Gameplay art (poses, run loops, abilities) stays in each game, which references this asset from its own data.
    /// The eight assets are created in Phase 5.
    /// </summary>
    [CreateAssetMenu(menuName = "Farm Fury/Character Identity", fileName = "Character")]
    public class CharacterIdentity : ScriptableObject
    {
        [Tooltip("Stable id: cluck, bessie, percy, woolly, ducky, horace, gerald, billy.")]
        public string id;
        public string displayName;
        [Tooltip("e.g. chicken, cow, pig.")]
        public string species;
        [TextArea(3, 8)] public string story;
        public Color primaryColour = Color.white;
        public Sprite portrait;
    }
}
