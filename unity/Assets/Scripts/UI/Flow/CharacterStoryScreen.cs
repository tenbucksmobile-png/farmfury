using System.Collections.Generic;
using FarmFury.Core;
using UnityEngine;
using UnityEngine.UI;
using FarmFury.Shared.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// Settings -> Character Story (franchise layout, ported from Stampede's CharacterStoryScreen): five wooden tabs
    /// over a scrolling list of cream rows. FarmFury's own text: Story (StoryContent.Story + Credits), How to Play
    /// (the cannon game), Characters (each animal's framed card, StoryContent entry and which world it joins in),
    /// Worlds (each world's badge, launcher and StoryContent entry), Robots (since 2026-11-13 Stampede's design: one
    /// world at a time, its boss inside a turning ring of its robots, a card for the tapped one - see RobotsPage).
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
                if (i == RobotsTab) { _pages[i] = RobotsPage(); }   // a ring per world instead of a list
                else { (_pages[i], _lists[i]) = ScrollList(TabNames[i]); }
            }
        }

        // ------------------------------------------------------------ robots page (2026-11-13, Stampede's design)
        // User: "because there are different robots for each world, we placed them in a circulating rotation which
        // works better, also create one and a small description for each world boss". One world at a time (< and >
        // step through, wrapping): the world's boss in the middle of a slowly turning ring of its three robots
        // (RobotRing), and a card with the tapped robot's name and story (StoryContent.Bosses / RobotTypes). Art:
        // MenuArt.worldRobots, the robot prefabs' own sprites.
        private const int RobotsTab = 4;
        private const int Boss = 3;   // index of the boss within a world's four
        private readonly List<(string title, int world)> _robotWorlds = new();
        private int _robotWorld;
        private RectTransform _robotStage;
        private RobotRing _ring;
        private Text _robotWorldTitle, _robotName, _robotStory;
        private readonly List<(Image image, int kind)> _robotButtons = new();

        private GameObject RobotsPage()
        {
            var page = UIKit.NewRect("RobotsPage", Rect);
            page.anchorMin = new Vector2(0.5f, 0f);
            page.anchorMax = new Vector2(0.5f, 1f);
            page.offsetMin = new Vector2(-(RowWidth + 40f) * 0.5f, 180f);
            page.offsetMax = new Vector2((RowWidth + 40f) * 0.5f, -170f);

            var prev = UIKit.MakeButton(page, "PrevWorld", "<", TabInactive, () => ShowRobotWorld(_robotWorld - 1), 60);
            UIKit.Place(prev.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-560f, -10f), new Vector2(110f, 90f));
            var next = UIKit.MakeButton(page, "NextWorld", ">", TabInactive, () => ShowRobotWorld(_robotWorld + 1), 60);
            UIKit.Place(next.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(560f, -10f), new Vector2(110f, 90f));
            foreach (var b in new[] { prev, next })
            {
                b.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
                b.gameObject.AddComponent<Outline>().effectColor = RowBorder;
            }
            _robotWorldTitle = UIKit.Label(page, "WorldTitle", "", 54, TextAnchor.MiddleCenter, UIKit.Accent);
            _robotWorldTitle.fontStyle = FontStyle.Bold;
            _robotWorldTitle.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3f, -3f);
            UIKit.Place(_robotWorldTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(980f, 90f));

            _robotStage = UIKit.NewRect("RobotRing", page);
            UIKit.Place(_robotStage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-400f, -55f), new Vector2(860f, 600f));
            _ring = _robotStage.gameObject.AddComponent<RobotRing>();
            _ring.radius = new Vector2(300f, 170f);

            var card = Row(page, "RobotCard");
            UIKit.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(430f, -55f), new Vector2(760f, 520f));
            _robotName = UIKit.Label(card, "Name", "", 50, TextAnchor.UpperLeft, TitleColor);
            _robotName.fontStyle = FontStyle.Bold;
            UIKit.Place(_robotName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(680f, 70f));
            _robotStory = UIKit.Label(card, "Story", "", 34, TextAnchor.UpperLeft, Color.black);
            UIKit.Place(_robotStory.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Vector2(680f, 370f));
            return page.gameObject;
        }

        private void ShowRobotWorld(int index)
        {
            if (_robotWorlds.Count == 0 || _robotStage == null) { return; }
            _robotWorld = (index % _robotWorlds.Count + _robotWorlds.Count) % _robotWorlds.Count;   // wraps both ways
            var (title, world) = _robotWorlds[_robotWorld];
            _robotWorldTitle.text = title;

            for (int i = _robotStage.childCount - 1; i >= 0; i--) { Object.Destroy(_robotStage.GetChild(i).gameObject); }
            _robotButtons.Clear();

            var ringItems = new List<RectTransform>();
            foreach (int kind in new[] { Boss, 0, 1, 2 })   // the boss first, in the middle
            {
                int slot = world * 4 + kind;
                var art = Art.worldRobots != null && slot < Art.worldRobots.Length ? Art.worldRobots[slot] : null;
                bool boss = kind == Boss;
                var image = UIKit.Picture(_robotStage, boss ? "Boss" : $"Robot_{kind}", art);
                image.gameObject.SetActive(true);
                image.raycastTarget = true;
                image.color = art != null ? Color.white : new Color(1f, 1f, 1f, 0f);
                var rt = image.rectTransform;
                UIKit.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, boss ? new Vector2(320f, 320f) : new Vector2(190f, 190f));
                var outline = image.gameObject.AddComponent<Outline>();
                outline.effectColor = UIKit.Accent;
                outline.effectDistance = new Vector2(5f, -5f);
                outline.enabled = false;
                int picked = kind;
                image.gameObject.AddComponent<Button>().onClick.AddListener(() => SelectRobot(picked));
                _robotButtons.Add((image, kind));
                if (!boss) { ringItems.Add(rt); }
            }
            _ring.SetItems(ringItems);
            SelectRobot(Boss);
        }

        private void SelectRobot(int kind)
        {
            if (_robotWorlds.Count == 0) { return; }
            int world = _robotWorlds[_robotWorld].world;
            foreach (var (image, k) in _robotButtons)
            {
                var outline = image.GetComponent<Outline>();
                if (outline != null) { outline.enabled = k == kind; }
            }
            if (kind == Boss)
            {
                var boss = world < StoryContent.Bosses.Length ? StoryContent.Bosses[world] : new StoryContent.Entry("The Boss", "");
                _robotName.text = boss.Title + " (Boss)";
                _robotStory.text = boss.Body;
                return;
            }
            var names = world < StoryContent.RobotTypeNames.Length ? StoryContent.RobotTypeNames[world] : null;
            _robotName.text = names != null ? names[kind] : StoryContent.RobotTypes[kind].Title;
            _robotStory.text = StoryContent.RobotTypes[kind].Body;
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
            if (_lists[index] != null) { _lists[index].anchoredPosition = Vector2.zero; }   // the Robots page has no list
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

            _robotWorlds.Clear();
            if (dm != null)
            {
                foreach (var world in dm.GetAllWorlds())
                {
                    int w = (int)world.worldType;
                    if (w >= 0 && w < 9) { _robotWorlds.Add((world.displayName, w)); }
                }
            }
            if (_robotWorlds.Count == 0)
            {
                for (int w = 0; w < 9; w++) { _robotWorlds.Add(($"World {w + 1}", w)); }
            }
            ShowRobotWorld(0);
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
