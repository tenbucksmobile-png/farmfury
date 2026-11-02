using System;
using System.Collections.Generic;
using System.Linq;
using FarmFury.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// Pre-level character select (GameState.CharacterSelect) — FarmFury's version of the franchise picker
    /// (user decision 2026-11-02: "use select as characters become available"). Replaces the old World 2-only
    /// World2CharacterSelectScreen and the MatchUp VS screen.
    ///
    /// Layout (1920x1080 reference, inside the safe area): the world's "Character" backdrop; the eight framed animal
    /// cards in two rows of four — an unlocked animal (SaveManager.IsAnimalUnlocked: its intro world is open) is
    /// tappable, a locked one is dimmed; below them the lineup row, one slot per shot the level allows
    /// (LevelData.birds.Length), pre-filled with the level's authored animals (any still locked swapped for Cluck).
    /// Tap a card to put that animal in the first empty slot, tap a filled slot to empty it; Play is enabled once
    /// every slot is filled and starts the level with that lineup (GameManager.StartLevel). Back returns to Level
    /// Select. Not yet seen in Play mode — positions are from the reference-canvas maths only.
    /// </summary>
    public class CharacterSelectScreen
    {
        public GameObject Root { get; private set; }
        public LevelData PendingLevel { get; private set; }
        public bool IsOpen => Root.activeSelf;

        private readonly MenuArt _art;
        private readonly Action<LevelData, AnimalType[]> _onLaunch;
        private readonly Action _onBack;
        private readonly Image _backdrop;
        private readonly Text _levelTitle;
        private readonly RectTransform _lineupRow;
        private readonly Button _playButton;
        private readonly Dictionary<AnimalType, Button> _cards = new();
        private readonly List<Image> _slotImages = new();
        private readonly List<AnimalType?> _lineup = new();

        // Cards span y -215..+275 from the centre: below the backdrop's "Character" lettering (top ~20% of a ~980
        // tall wide-phone canvas) and above the lineup row (bottom 190).
        private const float CardSize = 230f, CardColumnStep = 290f, CardRowStep = 260f;
        private const float CardsCentreY = 30f;           // from the screen centre, up
        private const float SlotSize = 150f, SlotGap = 18f, LineupBottom = 40f;
        private const float EdgeMargin = 16f;
        private static readonly Color EmptySlot = new(0f, 0f, 0f, 0.45f);
        private static readonly Color LockedCard = new(0.3f, 0.3f, 0.32f, 1f);

        public CharacterSelectScreen(Transform canvas, Action<LevelData, AnimalType[]> onLaunch, Action onBack, MenuArt art)
        {
            _art = art ?? new MenuArt();
            _onLaunch = onLaunch;
            _onBack = onBack;

            var root = UIKit.NewRect("CharacterSelect", canvas);
            UIKit.Stretch(root);
            Root = root.gameObject;
            Root.AddComponent<Image>().color = UIKit.Dark;
            _backdrop = UIKit.Backdrop(root);

            var safe = UIKit.NewRect("SafeArea", root);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // Level name, small, top-right (the backdrop's own "Character" lettering is the header).
            _levelTitle = UIKit.Label(safe, "LevelName", "", 40, TextAnchor.UpperRight, Color.white);
            _levelTitle.fontStyle = FontStyle.Bold;
            _levelTitle.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            UIKit.Place(_levelTitle.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(700f, 60f));

            for (int i = 0; i < AnimalRoster.All.Length; i++)
            {
                var animal = AnimalRoster.All[i];
                int col = i % 4, row = i / 4;
                var card = _art.AnimalCard(animal);
                var button = UIKit.MakeButton(safe, $"Card_{animal}", card != null ? "" : animal.ToString(), card != null ? Color.white : UIKit.Card,
                    () => AddToLineup(animal), 36);
                if (card != null)
                {
                    button.image.sprite = card;
                    button.image.preserveAspect = true;
                }
                UIKit.Place(button.image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2((col - 1.5f) * CardColumnStep, CardsCentreY + (0.5f - row) * CardRowStep), new Vector2(CardSize, CardSize));
                _cards[animal] = button;
            }

            _lineupRow = UIKit.NewRect("Lineup", safe);
            UIKit.Place(_lineupRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, LineupBottom), new Vector2(10f, SlotSize));

            _playButton = UIKit.MakeButton(safe, "PlayButton", _art.playButton != null ? "" : "PLAY", _art.playButton != null ? Color.white : UIKit.Good, Launch, 40);
            if (_art.playButton != null) { _playButton.image.sprite = _art.playButton; _playButton.image.preserveAspect = true; }
            UIKit.Place(_playButton.image.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-EdgeMargin, EdgeMargin),
                new Vector2(UIKit.RoundButtonSize, UIKit.RoundButtonSize));

            var back = UIKit.MakeButton(safe, "BackButton", _art.backButton != null ? "" : "<", _art.backButton != null ? Color.white : new Color(0.62f, 0.38f, 0.17f, 1f),
                () => _onBack?.Invoke(), 72);
            if (_art.backButton != null) { back.image.sprite = _art.backButton; back.image.preserveAspect = true; }
            UIKit.Place(back.image.rectTransform, Vector2.zero, Vector2.zero, new Vector2(EdgeMargin, EdgeMargin),
                new Vector2(UIKit.RoundButtonSize, UIKit.RoundButtonSize));

            Root.SetActive(false);
        }

        public void Open(LevelData level)
        {
            PendingLevel = level;
            var save = SaveManager.Instance;
            var world = DataManager.Instance != null ? DataManager.Instance.GetWorldData(level.WorldType) : null;
            UIKit.SetBackdrop(_backdrop, MenuArt.Either(world != null ? world.characterSelectBackground : null, _art.characterSelectBackground));
            _levelTitle.text = level.levelName;

            foreach (var (animal, button) in _cards)
            {
                bool unlocked = save != null && save.IsAnimalUnlocked(animal);
                button.interactable = unlocked;
                if (button.image.sprite != null) { button.image.color = unlocked ? Color.white : LockedCard; }
                else { button.image.color = unlocked ? UIKit.Card : UIKit.CardLocked; }
            }

            // One slot per shot the level allows, pre-filled with its authored animals (locked ones -> Cluck).
            int shots = Mathf.Max(1, level.birds != null ? level.birds.Length : 3);
            _lineup.Clear();
            for (int i = 0; i < shots; i++)
            {
                AnimalType? pick = level.birds != null && i < level.birds.Length ? level.birds[i] : null;
                if (pick.HasValue && (save == null || !save.IsAnimalUnlocked(pick.Value))) { pick = AnimalType.Cluck; }
                _lineup.Add(pick);
            }
            BuildSlots();
            RefreshLineup();
            Root.SetActive(true);
        }

        public void Hide() => Root.SetActive(false);

        private void BuildSlots()
        {
            foreach (var image in _slotImages) { UnityEngine.Object.Destroy(image.gameObject); }
            _slotImages.Clear();

            float width = _lineup.Count * SlotSize + (_lineup.Count - 1) * SlotGap;
            _lineupRow.sizeDelta = new Vector2(width, SlotSize);
            for (int i = 0; i < _lineup.Count; i++)
            {
                int index = i;
                var slot = UIKit.MakeButton(_lineupRow, $"Slot_{i}", "", EmptySlot, () => RemoveFromLineup(index), 30);
                slot.image.preserveAspect = true;
                UIKit.Place(slot.image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(i * (SlotSize + SlotGap), 0f), new Vector2(SlotSize, SlotSize));
                _slotImages.Add(slot.image);
            }
        }

        private void AddToLineup(AnimalType animal)
        {
            int empty = _lineup.FindIndex(a => !a.HasValue);
            if (empty < 0) { return; }
            _lineup[empty] = animal;
            RefreshLineup();
        }

        private void RemoveFromLineup(int index)
        {
            if (index < 0 || index >= _lineup.Count || !_lineup[index].HasValue) { return; }
            _lineup[index] = null;
            RefreshLineup();
        }

        private void RefreshLineup()
        {
            for (int i = 0; i < _slotImages.Count; i++)
            {
                var image = _slotImages[i];
                var pick = _lineup[i];
                var card = pick.HasValue ? _art.AnimalCard(pick.Value) : null;
                image.sprite = card;
                image.color = pick.HasValue ? (card != null ? Color.white : UIKit.Card) : EmptySlot;
                var label = image.GetComponentInChildren<Text>();
                label.text = pick.HasValue && card == null ? pick.Value.ToString() : "";
            }
            _playButton.interactable = _lineup.All(a => a.HasValue);
        }

        private void Launch()
        {
            if (PendingLevel == null || !_lineup.All(a => a.HasValue)) { return; }
            var lineup = _lineup.Select(a => a.Value).ToArray();
            if (SaveManager.Instance != null) { SaveManager.Instance.LastLineup = lineup.ToList(); }
            Root.SetActive(false);
            _onLaunch?.Invoke(PendingLevel, lineup);
        }
    }
}
