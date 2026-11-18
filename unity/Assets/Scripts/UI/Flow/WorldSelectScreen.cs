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
            : base(canvas, WebView, onEnter, onBuyWorld, onHome, art) { }

        // Web demo: every world but Meadow Ruins reads as "unlocked" (bright, tappable - GameFlow.EnterLevelSelect opens
        // the Full Game panel for it) and as paid, so it sorts after Meadow Ruins and Meadow Ruins stays the centred,
        // furthest-unlocked world.
        private static FarmFury.Shared.UI.WorldView WebView(WorldType world)
        {
            var view = CannonfireMenuData.Instance.World(world);
            if (!FarmFury.Core.WebDemo.IsPlayable(world))
            {
                view.Unlocked = true;
                view.PurchaseRequired = true;
            }
            return view;
        }
    }
}
