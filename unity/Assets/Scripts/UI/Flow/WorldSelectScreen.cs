using System;
using UnityEngine;

namespace FarmFury.UI
{
    /// <summary>
    /// World Select is com.farmfury.core's shared <c>WorldSelectScreen&lt;TWorld&gt;</c> (since core 0.4.0); this keeps
    /// Cannonfire's name and constructor.
    /// </summary>
    public sealed class WorldSelectScreen : FarmFury.Shared.UI.WorldSelectScreen<WorldType>
    {
        public WorldSelectScreen(Transform canvas, Action<WorldType> onEnter, Action onBuyWorld, Action onHome, MenuArt art)
            : base(canvas, CannonfireMenuData.Instance.World, onEnter, onBuyWorld, onHome, art) { }
    }
}
