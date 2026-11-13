using System;
using UnityEngine;

namespace FarmFury.UI
{
    /// <summary>
    /// Level Select is com.farmfury.core's shared <c>LevelSelectScreen&lt;TWorld, TLevel&gt;</c> (since core 0.4.0);
    /// this keeps Cannonfire's name and constructor. Worlds hold 16-24 levels, so the art grid uses 8 columns above
    /// 12 tiles (the package default, LargeWorldColumns).
    /// </summary>
    public sealed class LevelSelectScreen : FarmFury.Shared.UI.LevelSelectScreen<WorldType, LevelData>
    {
        public LevelSelectScreen(Transform canvas, Action<LevelData> onPick, Action onBack, MenuArt art)
            : base(canvas, CannonfireMenuData.Instance, onPick, onBack, art) { }
    }
}
