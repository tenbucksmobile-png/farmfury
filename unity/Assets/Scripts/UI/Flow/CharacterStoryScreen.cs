using System.Collections.Generic;
using FarmFury.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// Settings -> Character Story (franchise layout, ported from Stampede's CharacterStoryScreen): five wooden tabs
    /// over a scrolling list of cream rows. FarmFury's own text: Story (StoryContent.Story + Credits), How to Play
    /// (the cannon game), Characters (each animal's framed card, StoryContent entry and which world it joins in),
    /// Worlds (each world's badge, launcher and StoryContent entry), Robots (FarmFury's four robot types with their
    /// sprites — MenuArt.robotPortraits). Stampede's Cosmetics tab is added with the shop (franchise Phase 5).
    /// Replaces the old Settings popup's Story / About tabs.
    /// </summary>
    public class CharacterStoryScreen : OverlayScreen
    {
        private static readonly Color TabActive = new(0.85f, 0.65f, 0.2f);
        private static readonly Color TabInactive = new(0.35f, 0.28f, 0.18f);
        private static readonly Color RowBorder = new(0.70f, 0.55f, 0.20f);
        private static readonly Color RowBackground = new(0.97f, 0.94f, 0.86f, 0.94f);
        private static readonly Color TitleColor = new(0.35f, 0.18f, 0.05f);
        private const float RowWidth = 1650f;
        private static readonly string[] TabNames = { "Story", "How to Play", "Characters", "Worlds", "Robots" };

        private static readonly (string title, string body)[] HowToPlay =
        {
            ("Pick Your Farmhands", "Before each level, choose who goes in the cannon. The level shows how many shots " +
                "you get - fill every slot with any animal you've unlocked, then press Play."),
            ("Aim & Fire", "Touch the animal in the cannon and drag back. The direction you pull sets the angle, how " +
                "far you pull sets the power - the dotted arc shows where it will fly. Let go to fire."),
            ("Abilities", "From level 4 on, tap the screen while an animal is flying to use its special ability - " +
                "Cluck's eggs, Bessie's ground slam and more."),
            ("Knock Them Down", "Destroy every robot to win. Wood, stone, hay bales and barrels all break - and " +
                "a robot whose platform falls away takes a tumble too. Barrels and hay bales explode!"),
            ("Stars", "Beat every robot for 1 star. Win with one animal left over for 2 stars, and with two or more " +
                "left for 3. Clear every block too and it's a Perfect level."),
            ("Coins", "Clearing a level earns coins - the most the first time, plus a bonus for every new star " +
                "after. Spend them in the Shop."),
            ("New Friends", "Beat a world's boss to open the next world - and the animals who live there join " +
                "your team."),
        };

        private readonly Button[] _tabs = new Button[TabNames.Length];
        private readonly RectTransform[] _lists = new RectTransform[TabNames.Length];
        private readonly GameObject[] _pages = new GameObject[TabNames.Length];
        private bool _populated;

        public CharacterStoryScreen(Transform canvas, MenuArt art, ShopArt shop) : base(canvas, "CharacterStory", art, shop)
        {
            const float tabWidth = 310f, tabGap = 20f;
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                _tabs[i] = UIKit.MakeButton(Rect, $"{TabNames[i]}Tab", TabNames[i], TabInactive, () => SelectTab(index), 38);
                var label = _tabs[i].GetComponentInChildren<Text>();
                label.fontStyle = FontStyle.Bold;
                label.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2f, -2f);
                _tabs[i].gameObject.AddComponent<Outline>().effectColor = RowBorder;
                UIKit.Place(_tabs[i].image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2((i - (TabNames.Length - 1) * 0.5f) * (tabWidth + tabGap), -50f), new Vector2(tabWidth, 90f));
                (_pages[i], _lists[i]) = ScrollList(TabNames[i]);
            }
        }

        protected override void OnShow()
        {
            Populate();
            SelectTab(0);
        }

        private void SelectTab(int index)
        {
            for (int i = 0; i < _tabs.Length; i++)
            {
                _pages[i].SetActive(i == index);
                _tabs[i].image.color = i == index ? TabActive : TabInactive;
            }
            _lists[index].anchoredPosition = Vector2.zero;
        }

        private (GameObject, RectTransform) ScrollList(string name)
        {
            var viewport = UIKit.NewRect(name + "Page", Rect);
            viewport.anchorMin = new Vector2(0.5f, 0f);
            viewport.anchorMax = new Vector2(0.5f, 1f);
            viewport.offsetMin = new Vector2(-(RowWidth + 40f) * 0.5f, 180f);
            viewport.offsetMax = new Vector2((RowWidth + 40f) * 0.5f, -170f);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            var content = UIKit.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 10, 10);
            layout.spacing = 30f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            return (viewport.gameObject, content);
        }

        private void Populate()
        {
            if (_populated) { return; }
            _populated = true;

            foreach (var entry in StoryContent.Story) { InfoRow(_lists[0], null, 0f, entry.Title, entry.Body); }
            foreach (var entry in StoryContent.Credits) { InfoRow(_lists[0], null, 0f, entry.Title, entry.Body); }

            foreach (var (title, body) in HowToPlay) { InfoRow(_lists[1], null, 0f, title, body); }

            for (int i = 0; i < AnimalRoster.All.Length && i < StoryContent.Characters.Length; i++)
            {
                var animal = AnimalRoster.All[i];
                var entry = StoryContent.Characters[i];
                var world = AnimalRoster.IntroWorld(animal);
                int after = AnimalRoster.UnlockAfter(animal);   // levels of its world (2026-11-12)
                string joins = after <= 0 ? "\nReady from the start."
                    : $"\nJoins the team after {after} level{(after == 1 ? "" : "s")} in {LeaderboardsScreen.WorldName(world)}.";
                InfoRow(_lists[2], Art.AnimalCard(animal), 300f, entry.Title, entry.Body + joins);
            }

            var dm = DataManager.Instance;
            if (dm != null)
            {
                foreach (var world in dm.GetAllWorlds())
                {
                    int index = (int)world.worldType;
                    string body = index < StoryContent.Worlds.Length ? StoryContent.Worlds[index].Body : world.blurb;
                    string paid = world.purchaseRequired ? "\nBonus world - unlock it in the Shop." : "";
                    InfoRow(_lists[3], world.selectBadge, 240f, world.displayName, $"Launcher: {world.launcherName}\n{body}{paid}");
                }
            }

            for (int i = 0; i < StoryContent.Robots.Length; i++)
            {
                var portrait = Art.robotPortraits != null && i < Art.robotPortraits.Length ? Art.robotPortraits[i] : null;
                InfoRow(_lists[4], portrait, 220f, StoryContent.Robots[i].Title, StoryContent.Robots[i].Body);
            }
        }

        private void InfoRow(Transform list, Sprite icon, float iconSize, string title, string body)
        {
            var row = Row(list, title);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 24, 24);
            layout.spacing = 36f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            if (icon != null)
            {
                var image = UIKit.Picture(row, "Icon", icon);
                var element = image.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = element.minWidth = iconSize;
                element.preferredHeight = element.minHeight = iconSize;
            }

            var column = UIKit.NewRect("TextColumn", row);
            column.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var columnLayout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            columnLayout.spacing = 8f;
            columnLayout.childControlWidth = columnLayout.childControlHeight = true;
            columnLayout.childForceExpandWidth = true;
            columnLayout.childForceExpandHeight = false;

            var titleText = UIKit.Label(column, "Title", title, 40, TextAnchor.MiddleLeft, TitleColor);
            titleText.fontStyle = FontStyle.Bold;
            UIKit.Label(column, "Body", body, 32, TextAnchor.UpperLeft, Color.black);
        }

        private static RectTransform Row(Transform list, string name)
        {
            var row = UIKit.Panel(list, $"Row_{name}", RowBackground);
            var outline = row.gameObject.AddComponent<Outline>();
            outline.effectColor = RowBorder;
            outline.effectDistance = new Vector2(6f, -6f);
            return row.rectTransform;
        }
    }
}
