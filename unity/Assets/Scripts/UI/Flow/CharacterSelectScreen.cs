using System;
using System.Collections.Generic;
using System.Linq;
using FarmFury.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// Pre-level character select (GameState.CharacterSelect). Since 2026-11-02 (user request: "make the character
    /// select the carousel as per stampede, when a character is selected, animate so it drops into the three open
    /// slots below") the eight framed animal cards sit in Stampede's carousel (CardCarouselController, as in its
    /// SwapCharacterScreen): drag, or tap a side card, to bring a card to the centre; tap the centred card to pick it.
    /// A picked card's art flies out of the carousel on a short arc and drops into the next open lineup slot with a
    /// bounce. Locked animals (SaveManager.IsAnimalUnlocked: intro world not open) are dark silhouettes — browsable,
    /// not pickable. The lineup row has one open slot per shot the level allows (LevelData.birds.Length, 3; 4 on a
    /// boss level), empty on open; tap a filled slot to empty it. Play is enabled once every slot is filled and starts
    /// the level with that lineup (GameManager.StartLevel). Back returns to Level Select.
    /// Not yet seen in Play mode — positions are from the 1920x1080 reference-canvas maths only.
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
        private readonly RectTransform _rootRect;
        private readonly RectTransform _lineupRow;
        private readonly Button _playButton;
        private readonly CardCarouselController _carousel;
        private readonly List<RectTransform> _items = new();
        private readonly List<Button> _cardButtons = new();
        private readonly List<Image> _slotImages = new();
        private readonly List<AnimalType?> _lineup = new();
        private readonly HashSet<int> _landing = new();   // slots whose card is still in flight
        private readonly Driver _driver;

        // The carousel band sits below the backdrop's "Character" lettering and above the lineup row.
        private const float CarouselY = 40f, CarouselHeight = 400f;
        private const float SlotSize = 160f, SlotGap = 22f, LineupBottom = 36f;
        private const float EdgeMargin = 16f;
        private const float FlightSeconds = 0.5f, ArcHeight = 140f, LandBounceSeconds = 0.22f, LandBounce = 0.18f;
        private static readonly Color EmptySlot = new(0f, 0f, 0f, 0.45f);
        private static readonly Color Silhouette = new(0.08f, 0.07f, 0.12f, 1f);

        public CharacterSelectScreen(Transform canvas, Action<LevelData, AnimalType[]> onLaunch, Action onBack, MenuArt art)
        {
            _art = art ?? new MenuArt();
            _onLaunch = onLaunch;
            _onBack = onBack;

            var root = UIKit.NewRect("CharacterSelect", canvas);
            UIKit.Stretch(root);
            _rootRect = root;
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

            // Carousel: the container's invisible raycast Image catches drags that start between cards.
            var cards = UIKit.NewRect("Cards", safe);
            UIKit.Place(cards, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, CarouselY), new Vector2(1920f, CarouselHeight));
            cards.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            _carousel = cards.gameObject.AddComponent<CardCarouselController>();
            foreach (var animal in AnimalRoster.All)
            {
                var item = UIKit.NewRect($"Item_{animal}", cards);
                var sprite = _art.AnimalCard(animal);
                // No onClick here: the carousel adds the listener and reports only a tap on the centred card.
                var button = UIKit.MakeButton(item, "Card", sprite != null ? "" : animal.ToString(), sprite != null ? Color.white : UIKit.Card, null, 36);
                if (sprite != null) { button.image.sprite = sprite; button.image.preserveAspect = true; }
                UIKit.Stretch(button.image.rectTransform);
                _items.Add(item);
                _cardButtons.Add(button);
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

            _driver = Root.AddComponent<Driver>();
            Root.SetActive(false);
        }

        public void Open(LevelData level)
        {
            PendingLevel = level;
            var save = SaveManager.Instance;
            var world = DataManager.Instance != null ? DataManager.Instance.GetWorldData(level.WorldType) : null;
            UIKit.SetBackdrop(_backdrop, MenuArt.Either(world != null ? world.characterSelectBackground : null, _art.characterSelectBackground));
            _levelTitle.text = level.levelName;

            // Centre on the level's lead animal (the world's new animal), else the first unlocked one.
            int start = 0;
            AnimalType? lead = level.birds != null && level.birds.Length > 0 ? level.birds[0] : null;
            for (int i = 0; i < AnimalRoster.All.Length; i++)
            {
                var animal = AnimalRoster.All[i];
                bool unlocked = IsUnlocked(animal);
                var image = _cardButtons[i].image;
                image.color = image.sprite != null ? (unlocked ? Color.white : Silhouette) : (unlocked ? UIKit.Card : UIKit.CardLocked);
                if (unlocked && lead.HasValue && animal == lead.Value) { start = i; }
            }

            _driver.StopAllCoroutines();
            _landing.Clear();
            int shots = Mathf.Max(1, level.birds != null ? level.birds.Length : 3);
            _lineup.Clear();
            for (int i = 0; i < shots; i++) { _lineup.Add(null); }
            BuildSlots();
            RefreshLineup();

            Root.SetActive(true);
            _carousel.SetItems(_items, _cardButtons, start, i => Pick(AnimalRoster.All[i]));
        }

        public void Hide()
        {
            _driver.StopAllCoroutines();
            Root.SetActive(false);
        }

        private static bool IsUnlocked(AnimalType animal) =>
            SaveManager.Instance != null && SaveManager.Instance.IsAnimalUnlocked(animal);

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
                // Centre pivot so the landing bounce squashes about the slot's middle.
                UIKit.Place(slot.image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(i * (SlotSize + SlotGap) + SlotSize * 0.5f, 0f), new Vector2(SlotSize, SlotSize));
                _slotImages.Add(slot.image);
            }
        }

        // The centred card was tapped: claim the first open slot now (so quick taps fill the next ones) and fly the
        // card's art down into it.
        private void Pick(AnimalType animal)
        {
            if (!IsUnlocked(animal)) { return; }
            int slot = _lineup.FindIndex(a => !a.HasValue);
            if (slot < 0) { return; }
            _lineup[slot] = animal;
            _landing.Add(slot);
            RefreshLineup();

            int index = Array.IndexOf(AnimalRoster.All, animal);
            _driver.StartCoroutine(FlyToSlot(_cardButtons[index].image, slot));
        }

        private System.Collections.IEnumerator FlyToSlot(Image source, int slot)
        {
            var target = _slotImages[slot].rectTransform;
            var fly = UIKit.Picture(_rootRect, "FlyingCard", source.sprite);
            fly.preserveAspect = true;
            fly.raycastTarget = false;
            var rect = fly.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.SetAsLastSibling();

            float unit = Mathf.Max(0.0001f, _rootRect.lossyScale.x);   // world units per canvas unit
            Vector3 from = source.rectTransform.TransformPoint(source.rectTransform.rect.center);
            Vector2 fromSize = source.rectTransform.rect.size * (source.rectTransform.lossyScale.x / unit);
            Vector2 toSize = target.rect.size;

            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / FlightSeconds)
            {
                if (target == null) { break; }
                Vector3 to = target.TransformPoint(target.rect.center);
                float e = t * t * (3f - 2f * t);                                   // smoothstep along the path
                Vector3 pos = Vector3.Lerp(from, to, e);
                pos.y += Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.15f)) * ArcHeight * unit;   // up, then drop
                rect.position = pos;
                rect.sizeDelta = Vector2.Lerp(fromSize, toSize, e);
                yield return null;
            }
            UnityEngine.Object.Destroy(fly.gameObject);

            _landing.Remove(slot);
            RefreshLineup();

            // Land with a squash-and-settle bounce.
            if (slot < _slotImages.Count)
            {
                var slotRect = _slotImages[slot].rectTransform;
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / LandBounceSeconds)
                {
                    if (slotRect == null) { yield break; }
                    float s = 1f + LandBounce * Mathf.Sin(Mathf.PI * t);
                    slotRect.localScale = new Vector3(s, 2f - s, 1f);
                    yield return null;
                }
                if (slotRect != null) { slotRect.localScale = Vector3.one; }
            }
        }

        private void RemoveFromLineup(int index)
        {
            if (index < 0 || index >= _lineup.Count || !_lineup[index].HasValue || _landing.Contains(index)) { return; }
            _lineup[index] = null;
            RefreshLineup();
        }

        private void RefreshLineup()
        {
            for (int i = 0; i < _slotImages.Count; i++)
            {
                var image = _slotImages[i];
                var pick = _landing.Contains(i) ? null : _lineup[i];   // a slot shows its card only once it lands
                var card = pick.HasValue ? _art.AnimalCard(pick.Value) : null;
                image.sprite = card;
                image.color = pick.HasValue ? (card != null ? Color.white : UIKit.Card) : EmptySlot;
                var label = image.GetComponentInChildren<Text>();
                label.text = pick.HasValue && card == null ? pick.Value.ToString() : "";
            }
            _playButton.interactable = _landing.Count == 0 && _lineup.All(a => a.HasValue);
        }

        private void Launch()
        {
            if (PendingLevel == null || _landing.Count > 0 || !_lineup.All(a => a.HasValue)) { return; }
            var lineup = _lineup.Select(a => a.Value).ToArray();
            if (SaveManager.Instance != null) { SaveManager.Instance.LastLineup = lineup.ToList(); }
            Hide();
            _onLaunch?.Invoke(PendingLevel, lineup);
        }

        // Hosts the flight coroutines (this screen is a plain class); unscaled time throughout.
        private sealed class Driver : MonoBehaviour { }
    }
}
