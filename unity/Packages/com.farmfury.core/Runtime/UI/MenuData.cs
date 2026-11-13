using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmFury.Shared.UI
{
    /// <summary>What the shared menus show for one world: read by the game from its own world data and save.</summary>
    public struct WorldView
    {
        /// <summary>False when the game has no data for the world (the menus then show its enum name).</summary>
        public bool HasData;
        public string DisplayName;
        /// <summary>World Select badge / World Unlocked card (name baked in). Null = the name as text.</summary>
        public Sprite Badge;
        /// <summary>Level Select backdrop (world name baked in). Null = the plain look.</summary>
        public Sprite LevelSelectBackground;
        /// <summary>This world's New Character / World Cleared backdrop; null = MenuArtBase.newCharacterBackground.</summary>
        public Sprite NewCharacterBackground;
        /// <summary>A paid world: sold in the world shop, ordered after the free ones on World Select.</summary>
        public bool PurchaseRequired;
        public bool Unlocked;
    }

    /// <summary>What Level Select shows for one level.</summary>
    public struct LevelView
    {
        public string Id;
        public string Name;
        public bool IsBoss;
        public bool Unlocked;
        public bool Completed;
        public int Stars;
        /// <summary>Plain look only: a beaten level with a secret still to find (Stampede's character-gated secrets).</summary>
        public bool SecretUnfound;
    }

    /// <summary>
    /// The seam between the shared menus (World Select, Level Select, World Cleared) and a game's own world enum,
    /// level asset, data manager and save. Each game implements it once (a stateless adapter); the shared screens
    /// never see the game's types except as the opaque TWorld / TLevel they hand back in callbacks.
    /// </summary>
    public interface IMenuData<TWorld, TLevel> where TWorld : struct, Enum
    {
        WorldView World(TWorld world);
        IReadOnlyList<TLevel> Levels(TWorld world);
        LevelView Level(TLevel level);
    }
}
