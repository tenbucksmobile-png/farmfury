using System.Collections.Generic;
using System.Linq;
using FarmFury.Core;
using UnityEngine;
using UnityEngine.UI;
using FarmFury.Shared.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// One world's Leaderboard page (franchise layout, ported from Stampede's WorldDetailScreen): the world's name
    /// lettering top-left, then three rows of label | value | icon | value in mockup pixels. Local records only
    /// (no online board yet), from completed levels:
    ///   Best Farm Fury — the animal holding the most level best scores | coin: coins earned in the world
    ///   High Score     — sum of per-level best scores                   | star: best stars / max stars
    ///   Fastest Time   — sum of per-level fastest clears                | 3-star board: Perfect clears / levels
    /// FarmFury differences: stars and Perfect clears replace Stampede's crops and rare pellets.
    /// </summary>
    public class WorldDetailScreen : LeaderboardPage
    {
        // This game's MenuArt (the shared base holds it as MenuArtBase).
        private new MenuArt Art => (MenuArt)base.Art;

        private static readonly float[] RowY = { 345f, 425f, 505f };
        private const float LabelLeft = 128f, LabelHeight = 56f, LabelMaxWidth = 360f;
        private const float ValueX = 595f, IconX = 868f, SecondValueX = 1068f;
        private const float PlaqueWidth = 136f, PlaqueHeight = 62f, IconBox = 78f, AnimalCardSize = 90f;
        private static readonly Rect TitleBox = new(92f, 58f, 300f, 180f);
        private static readonly Color Dimmed = new(1f, 1f, 1f, 0.35f);

        private readonly Image _title;
        private readonly Text _titleText;
        private readonly Image _bestAnimal;
        private readonly Text _highScore, _fastestTime, _coins, _stars, _perfect;

        public WorldDetailScreen(Transform canvas, MenuArt art, ShopArt shop, LeaderboardArt boards)
            : base(canvas, "WorldDetail", art, shop, boards)
        {
            _title = UIKit.Picture(Board, "WorldName", null);
            PlaceInArt(_title.rectTransform, TitleBox);
            _titleText = OutlinedText(Board, "WorldNameText", "", ValueGold);
            PlaceInArt(_titleText.rectTransform, TitleBox);

            RowLabel("BestFarmFury", Boards.bestLabel, "Best Farm Fury", RowY[0]);
            RowLabel("HighScore", Boards.highScoreLabel, "High Score", RowY[1]);
            RowLabel("FastestTime", Boards.fastestTimeLabel, "Fastest Time", RowY[2]);

            _bestAnimal = UIKit.Picture(Board, "BestAnimal", null);
            PlaceInArt(_bestAnimal.rectTransform, Centred(ValueX, RowY[0], AnimalCardSize, AnimalCardSize));
            _highScore = ValuePlaque("HighScoreValue", ValueX, RowY[1]);
            _fastestTime = ValuePlaque("FastestTimeValue", ValueX, RowY[2]);

            Icon("CoinIcon", Boards.coin, RowY[0]);
            Icon("StarIcon", Art.oneStarBoard, RowY[1]);
            Icon("PerfectIcon", Art.threeStarBoard, RowY[2]);
            _coins = ValuePlaque("CoinsValue", SecondValueX, RowY[0]);
            _stars = ValuePlaque("StarsValue", SecondValueX, RowY[1]);
            _perfect = ValuePlaque("PerfectValue", SecondValueX, RowY[2]);
        }

        private void RowLabel(string name, Sprite sprite, string fallback, float y)
        {
            float width = sprite != null
                ? Mathf.Min(LabelMaxWidth, LabelHeight * sprite.rect.width / sprite.rect.height)
                : LabelMaxWidth;
            var box = new Rect(LabelLeft, y - LabelHeight * 0.5f, width, LabelHeight);
            if (sprite != null)
            {
                PlaceInArt(UIKit.Picture(Board, name, sprite).rectTransform, box);
                return;
            }
            var text = OutlinedText(Board, name, fallback, ValueGold);
            text.alignment = TextAnchor.MiddleLeft;
            PlaceInArt(text.rectTransform, box);
        }

        private void Icon(string name, Sprite sprite, float y)
        {
            if (sprite != null)
            {
                PlaceInArt(UIKit.Picture(Board, name, sprite).rectTransform, Centred(IconX, y, IconBox, IconBox));
            }
        }

        private Text ValuePlaque(string name, float x, float y)
        {
            var box = Centred(x, y, PlaqueWidth, PlaqueHeight);
            if (Shop.plaque != null)
            {
                var plaque = UIKit.Picture(Board, name + "Plaque", Shop.plaque);
                plaque.preserveAspect = false;
                if (Shop.plaque.border != Vector4.zero)
                {
                    plaque.type = Image.Type.Sliced;
                    plaque.pixelsPerUnitMultiplier = Shop.plaque.rect.height / 70f;
                }
                PlaceInArt(plaque.rectTransform, box);
            }
            var text = OutlinedText(Board, name, "-", Color.white);
            PlaceInArt(text.rectTransform, new Rect(box.x + 14f, box.y + 8f, box.width - 28f, box.height - 20f));
            return text;
        }

        public void Show(WorldType world)
        {
            var sprite = Boards.WorldName((int)world);
            _title.sprite = sprite;
            _title.gameObject.SetActive(sprite != null);
            _titleText.text = sprite == null ? LeaderboardsScreen.WorldName(world) : "";

            var dm = DataManager.Instance;
            var save = SaveManager.Instance;
            var levels = dm != null ? dm.GetWorldLevels(world) : new List<LevelData>();
            var played = save != null ? levels.Where(l => save.IsLevelCompleted(l.levelId)).ToList() : new List<LevelData>();

            // Best Farm Fury: the animal holding the most level best scores (ties: the higher summed score).
            var best = save == null ? null : played
                .Select(l => (animal: save.GetBestScoreAnimal(l.levelId), score: save.GetBestScore(l.levelId)))
                .Where(r => r.animal.HasValue)
                .GroupBy(r => r.animal.Value)
                .OrderByDescending(g => g.Count()).ThenByDescending(g => g.Sum(r => r.score))
                .Select(g => (AnimalType?)g.Key)
                .FirstOrDefault();
            _bestAnimal.sprite = Art.AnimalCard(best ?? AnimalType.Cluck);
            _bestAnimal.color = best.HasValue ? Color.white : Dimmed;
            _bestAnimal.gameObject.SetActive(_bestAnimal.sprite != null);

            var timed = save == null ? new List<float>() : played.Select(l => save.GetFastestTime(l.levelId)).Where(t => t > 0f).ToList();
            _highScore.text = played.Count > 0 ? played.Sum(l => save.GetBestScore(l.levelId)).ToString("N0") : "-";
            _fastestTime.text = timed.Count > 0 ? FormatTime(timed.Sum()) : "-";
            _coins.text = save != null ? save.GetWorldCoinsEarned(world).ToString("N0") : "-";
            _stars.text = levels.Count > 0 && save != null ? $"{save.GetWorldStars(world)}/{levels.Count * 3}" : "-";
            _perfect.text = levels.Count > 0 && save != null ? $"{levels.Count(l => save.IsLevelPerfect(l.levelId))}/{levels.Count}" : "-";

            Show();
        }

        public static string FormatTime(float seconds)
        {
            int total = Mathf.RoundToInt(seconds);
            int h = total / 3600, m = total / 60 % 60, s = total % 60;
            return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m}:{s:00}";
        }
    }
}
