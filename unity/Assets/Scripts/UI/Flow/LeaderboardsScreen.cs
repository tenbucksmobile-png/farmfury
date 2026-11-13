using System;
using FarmFury.Core;
using UnityEngine;
using UnityEngine.UI;
using FarmFury.Shared.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// Settings -> Leaderboards, per the Leaderboard mockup (Leaderboard_Mock.png): the Farm Fury Stampede logo in
    /// the top-left corner, the LeaderBoard sign top-right and the nine world names as lettering scattered
    /// over the whole board (since 2026-09-30; was an even 3x3 grid that left the right side empty): the story
    /// worlds in the mockup's order (Meadow Ruins, Watermill Village, Frozen Tundra, Sky Island, Sunken City,
    /// Mothership), then the paid worlds (Dustbowl Canyon, Harvest Fairground, Crop Factory). Locked worlds - including a paid world not yet bought - are greyed. Tapping an unlocked world opens its
    /// <see cref="WorldDetailScreen"/>; a locked purchase-gated world opens the world shop; any other locked world
    /// says how to unlock it. The records are this device's own (no online board until the shared backend exists).
    /// </summary>
    public class LeaderboardsScreen : LeaderboardPage
    {
        // Mockup order, left to right, top row first.
        private static readonly WorldType[] GridOrder =
        {
            WorldType.MeadowRuins, WorldType.WatermillVillage, WorldType.FrozenTundra,
            WorldType.SkyIslands, WorldType.SunkenCity, WorldType.RobotMothership,
            WorldType.DustbowlCanyon, WorldType.HarvestFairground, WorldType.CropFactory,
        };
        // Name slots (centres, mockup pixels), scattered over the whole board rather than on a grid: one between the
        // logo and the LeaderBoard sign, then two staggered bands of four. No two slots overlap, and all stay inside
        // the board (so inside the safe area), clear of the logo, the sign (x 862-1188, down to y 226), the back
        // button (from x 1120, y 576) and the hint line (y 652). Re-check those when moving one.
        private static readonly Vector2[] NameCentres =
        {
            new(545f, 100f),
            new(175f, 265f), new(470f, 300f), new(775f, 310f), new(1090f, 330f),
            new(200f, 470f), new(500f, 530f), new(795f, 500f), new(1085f, 495f),
        };
        private const float NameWidth = 280f, NameHeight = 145f;
        private static readonly Rect LogoBox = new(36f, 22f, 162f, 114f);   // 2026-11-10: the Cannonfire logo a touch smaller
        private static readonly Rect HintBox = new(100f, 652f, 700f, 40f);
        private static readonly Color LockedTint = new(0.55f, 0.55f, 0.55f, 0.8f);

        private readonly Button[] _names = new Button[GridOrder.Length];
        private readonly Text _hint;
        private readonly Action<WorldType> _onWorld;
        private readonly Action _onBuyWorld;

        public LeaderboardsScreen(Transform canvas, MenuArt art, ShopArt shop, LeaderboardArt boards,
            Action<WorldType> onWorld, Action onBuyWorld)
            : base(canvas, "Leaderboards", art, shop, boards)
        {
            _onWorld = onWorld;
            _onBuyWorld = onBuyWorld;

            if (Boards.logo != null)
            {
                PlaceInArt(UIKit.Picture(Board, "Logo", Boards.logo).rectTransform, LogoBox);
            }

            for (int i = 0; i < GridOrder.Length; i++)
            {
                var world = GridOrder[i];
                var sprite = Boards.WorldName((int)world);
                var button = UIKit.MakeButton(Board, $"World_{world}", "", Color.white, () => Tapped(world));
                if (sprite != null)
                {
                    button.image.sprite = sprite;
                    button.image.preserveAspect = true;
                }
                else
                {
                    button.image.color = Color.clear;   // text only, the whole slot still tappable
                    var label = button.GetComponentInChildren<Text>();
                    label.gameObject.SetActive(false);
                    var text = OutlinedText(button.transform, "Name", WorldName(world), ValueGold);
                    UIKit.Stretch(text.rectTransform);
                }
                PlaceInArt(button.image.rectTransform, Centred(NameCentres[i].x, NameCentres[i].y, NameWidth, NameHeight));
                _names[i] = button;
            }

            _hint = OutlinedText(Board, "Hint", "", Color.white);
            PlaceInArt(_hint.rectTransform, HintBox);
        }

        protected override void OnShow()
        {
            _hint.text = "";
            for (int i = 0; i < GridOrder.Length; i++)
            {
                bool unlocked = SaveManager.Instance != null && SaveManager.Instance.IsWorldUnlocked(GridOrder[i]);
                var tint = unlocked ? Color.white : LockedTint;
                var image = _names[i].image;
                if (image.sprite != null)
                {
                    image.color = tint;
                }
                else
                {
                    _names[i].transform.Find("Name").GetComponent<Text>().color = unlocked ? ValueGold : ValueGold * LockedTint;
                }
            }
        }

        private void Tapped(WorldType world)
        {
            var save = SaveManager.Instance;
            if (save != null && save.IsWorldUnlocked(world))
            {
                _onWorld?.Invoke(world);
                return;
            }

            var data = DataManager.Instance != null ? DataManager.Instance.GetWorldData(world) : null;
            if (data != null && data.purchaseRequired)
            {
                _onBuyWorld?.Invoke();
                return;
            }

            _hint.text = world == WorldType.MeadowRuins ? "" : $"Beat the {WorldName(world - 1)} boss to unlock {WorldName(world)}.";
        }

        public static string WorldName(WorldType world)
        {
            var data = DataManager.Instance != null ? DataManager.Instance.GetWorldData(world) : null;
            return data != null && !string.IsNullOrEmpty(data.displayName) ? data.displayName : world.ToString();
        }
    }
}
