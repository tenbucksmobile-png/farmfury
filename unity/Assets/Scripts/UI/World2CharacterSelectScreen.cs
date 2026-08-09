using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// World 2 "choose your farmhands" screen — added 2026-08-09, user request: a tappable roster grid
// (Percy/Woolly/Cluck/Bessie selectable, Ducky/Horace/Gerald/Billy greyed out/locked) shown before
// the existing MatchUpScreen VS-slam/countdown sequence, letting the player build their own bird
// queue for the level instead of always launching whatever LevelData.birds[] hard-codes.
//
// "Locked" is derived directly from World2MapController's own _animalCardSprites array (passed in
// via Init(), same array MatchUpScreen already reads) rather than a hardcoded animal list — a
// slot with no card art wired is locked, a slot with art is tappable. This means a future
// World 3+ animal unlocks automatically here the moment its card art is wired, no code change
// needed.
//
// Selection flow: the player taps unlocked cards to fill a lineup queue sized to the level's own
// birds[].Length (so this generalises to any future World 2 level's bird count, not hardcoded to
// 3) — tapping a filled queue slot removes that pick. Once the queue is full, Launch is
// interactable; confirming invokes the onConfirmed callback (level index + chosen AnimalType[])
// and hides. World2MapController owns what happens next (see its own wiring) — this class only
// owns the picking UI, not what launching actually does with the picks.
public class World2CharacterSelectScreen : MonoBehaviour
{
    Sprite _squareSpr;
    Sprite[] _animalCardSprites; // indexed by AnimalType, shared with MatchUpScreen — see class comment
    Sprite[] _robotCardSprites;  // indexed by RobotType, shared with MatchUpScreen

    Action<int, AnimalType[]> _onConfirmed;

    GameObject _panel;
    TextMeshProUGUI _headerText;
    TextMeshProUGUI _launchLabel;
    Button _launchButton;
    Image _robotPreview;
    readonly Image[] _cardImages = new Image[8];
    readonly Button[] _cardButtons = new Button[8];
    readonly Image[] _queueSlotImages = new Image[3];
    readonly Button[] _queueSlotButtons = new Button[3];

    int _levelIndex;
    int _requiredCount;
    readonly List<AnimalType> _queue = new();

    static readonly Color LockedTint = new(0.45f, 0.45f, 0.45f, 1f);
    static readonly Color UnlockedRing = new(0.42f, 0.78f, 0.38f, 1f);   // farm-green — matches the approved mockup's "unlocked" ring
    static readonly Color SelectedRing = new(0.95f, 0.55f, 0.05f, 1f);   // amber — matches StyleAsGameNumber's gold-orange family

    public void Init(Sprite squareSpr, Sprite[] animalCardSprites, Sprite[] robotCardSprites,
        Action<int, AnimalType[]> onConfirmed)
    {
        _squareSpr         = squareSpr;
        _animalCardSprites = animalCardSprites;
        _robotCardSprites  = robotCardSprites;
        _onConfirmed       = onConfirmed;
        BuildUI();
    }

    public void Show(int levelIndex)
    {
        var data = GameManager.Instance?.GetLevelData(levelIndex);
        // Caller (World2MapController) is expected to only call Show() for a level that actually
        // has LevelData — the no-data "COMING SOON" case stays MatchUpScreen's existing job, not
        // duplicated here. Defensive fallback just in case: bail straight to a 1-bird requirement
        // rather than throwing on a null array below.
        _levelIndex     = levelIndex;
        _requiredCount  = data != null && data.birds != null && data.birds.Length > 0 ? data.birds.Length : 1;
        _queue.Clear();

        RobotType robotType = data != null && data.robots != null && data.robots.Length > 0
            ? data.robots[0].robotType : RobotType.Basic;
        Sprite robotSpr = _robotCardSprites != null && (int)robotType < _robotCardSprites.Length
            ? _robotCardSprites[(int)robotType] : null;
        _robotPreview.sprite = robotSpr != null ? robotSpr : _squareSpr;
        _robotPreview.color  = robotSpr != null ? Color.white : new Color(0.3f, 0.3f, 0.35f);

        _headerText.text = $"Choose {_requiredCount} Farmhand{(_requiredCount == 1 ? "" : "s")}";

        RefreshQueueSlots();
        RefreshLaunchState();
        _panel.SetActive(true);
    }

    public void Hide() => _panel?.SetActive(false);

    void OnCardTapped(AnimalType type)
    {
        if (_queue.Count >= _requiredCount) return;
        _queue.Add(type);
        RefreshQueueSlots();
        RefreshLaunchState();
    }

    void OnQueueSlotTapped(int slotIndex)
    {
        if (slotIndex >= _queue.Count) return; // empty slot, nothing to remove
        _queue.RemoveAt(slotIndex);
        RefreshQueueSlots();
        RefreshLaunchState();
    }

    void RefreshQueueSlots()
    {
        for (int i = 0; i < _queueSlotImages.Length; i++)
        {
            bool filled = i < _queue.Count;
            if (filled)
            {
                AnimalType type = _queue[i];
                Sprite spr = _animalCardSprites != null && (int)type < _animalCardSprites.Length
                    ? _animalCardSprites[(int)type] : null;
                _queueSlotImages[i].sprite = spr != null ? spr : _squareSpr;
                _queueSlotImages[i].color  = spr != null ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            }
            else
            {
                _queueSlotImages[i].sprite = _squareSpr;
                _queueSlotImages[i].color  = new Color(1f, 1f, 1f, 0.15f); // faint empty-slot outline
            }
        }
    }

    void RefreshLaunchState()
    {
        bool ready = _queue.Count >= _requiredCount;
        _launchButton.interactable = ready;
        _launchLabel.text = ready ? "Launch!" : $"Launch ({_queue.Count}/{_requiredCount})";
    }

    void OnLaunchClicked()
    {
        if (_queue.Count < _requiredCount) return;
        _onConfirmed?.Invoke(_levelIndex, _queue.ToArray());
        Hide();
    }

    // ── UI construction ──────────────────────────────────────────────────────

    void BuildUI()
    {
        var root = MakeFullScreenRect(transform, "World2CharacterSelectScreen");
        _panel = root.gameObject;

        var bg = root.gameObject.AddComponent<Image>();
        bg.sprite = _squareSpr;
        bg.color  = new Color(0.08f, 0.10f, 0.14f, 0.92f); // dims whatever's behind, same "modal overlay" convention as every other full-screen panel in this project

        _headerText = MakeCentredText(root, "HeaderText", new Vector2(0f, 380f), new Vector2(700f, 50f),
            30f, "Choose Farmhands", new Color(1f, 0.87f, 0.4f));
        _headerText.fontStyle = FontStyles.Bold;

        // 4x2 roster grid — card index order matches AnimalType enum (Cluck, Bessie, Percy,
        // Woolly, Ducky, Horace, Gerald, Billy), same indexing MatchUpScreen's own
        // _animalCardSprites array already uses.
        const float cardW = 150f, cardH = 190f, gapX = 18f;
        float gridWidth  = cardW * 4 + gapX * 3;
        float startX     = -gridWidth / 2f + cardW / 2f;
        float[] rowY     = { 190f, -10f };

        for (int i = 0; i < 8; i++)
        {
            int col = i % 4, row = i / 4;
            Vector2 pos = new Vector2(startX + col * (cardW + gapX), rowY[row]);
            BuildCard(root, i, pos, new Vector2(cardW, cardH));
        }

        // Robot preview — bottom-left, static, non-interactive (never a Button, matches "robots
        // are displayed, not selectable" requirement).
        var robotWrap = MakeRect(root, "RobotPreviewWrap",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(40f, 40f), new Vector2(150f, 190f));
        _robotPreview = MakeImage(robotWrap, "RobotArt", _squareSpr, Color.white,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, 0f), new Vector2(140f, 150f));
        _robotPreview.preserveAspect = true;
        MakeCentredText(robotWrap, "RobotLabel", new Vector2(0f, 6f), new Vector2(150f, 24f),
            12f, "Guarding this level", new Color(1f, 0.6f, 0.55f));

        // Lineup queue — bottom-right, next to the robot preview, per explicit request ("allow for
        // the choices to flow to the bottom of the frame next to the robot to show the match up").
        var queueWrap = MakeRect(root, "QueueWrap",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-40f, 40f), new Vector2(280f, 190f));
        MakeCentredText(queueWrap, "QueueLabel", new Vector2(0f, 160f), new Vector2(280f, 24f),
            12f, "Your Lineup", new Color(1f, 0.87f, 0.4f));
        for (int i = 0; i < 3; i++)
        {
            int slot = i;
            var slotImg = MakeImage(queueWrap, $"QueueSlot{i}", _squareSpr, new Color(1f, 1f, 1f, 0.15f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-92f + i * 92f, 130f), new Vector2(80f, 80f));
            slotImg.preserveAspect = true;
            var slotBtn = slotImg.gameObject.AddComponent<Button>();
            slotBtn.targetGraphic = slotImg;
            slotBtn.onClick.AddListener(() => OnQueueSlotTapped(slot));
            _queueSlotImages[i]  = slotImg;
            _queueSlotButtons[i] = slotBtn;
        }

        // Launch button — bottom-center, only interactable once the lineup is full.
        var launchGO = new GameObject("LaunchButton");
        launchGO.transform.SetParent(root, false);
        var launchRT = launchGO.AddComponent<RectTransform>();
        launchRT.anchorMin        = new Vector2(0.5f, 0f);
        launchRT.anchorMax        = new Vector2(0.5f, 0f);
        launchRT.pivot            = new Vector2(0.5f, 0f);
        launchRT.anchoredPosition = new Vector2(0f, 230f);
        launchRT.sizeDelta        = new Vector2(280f, 70f);
        var launchImg = launchGO.AddComponent<Image>();
        launchImg.sprite = _squareSpr;
        launchImg.color  = new Color(0.95f, 0.55f, 0.05f);
        _launchButton = launchGO.AddComponent<Button>();
        _launchButton.targetGraphic = launchImg;
        _launchButton.onClick.AddListener(OnLaunchClicked);
        _launchLabel = MakeCentredText(launchRT, "LaunchLabel", Vector2.zero, new Vector2(260f, 60f),
            22f, "Launch (0/3)", Color.white);
        _launchLabel.fontStyle = FontStyles.Bold;

        _panel.SetActive(false);
    }

    void BuildCard(Transform parent, int animalIndex, Vector2 pos, Vector2 size)
    {
        AnimalType type = (AnimalType)animalIndex;
        Sprite art = _animalCardSprites != null && animalIndex < _animalCardSprites.Length
            ? _animalCardSprites[animalIndex] : null;
        bool unlocked = art != null;

        var img = MakeImage(parent, $"Card_{type}", art != null ? art : _squareSpr,
            unlocked ? Color.white : LockedTint,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            pos, size);
        img.preserveAspect = true;
        _cardImages[animalIndex] = img;

        MakeCentredText(img.rectTransform, "Name", new Vector2(0f, -size.y / 2f + 14f),
            new Vector2(size.x, 22f), 13f, type.ToString(),
            unlocked ? Color.white : new Color(0.75f, 0.75f, 0.75f));

        if (unlocked)
        {
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var cols = btn.colors;
            cols.normalColor      = Color.white;
            cols.highlightedColor = new Color(0.92f, 1f, 0.9f);
            cols.pressedColor     = new Color(0.8f, 0.8f, 0.8f);
            btn.colors = cols;
            btn.onClick.AddListener(() => OnCardTapped(type));
            _cardButtons[animalIndex] = btn;

            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor    = UnlockedRing;
            outline.effectDistance = new Vector2(3f, 3f);
        }
        else
        {
            MakeCentredText(img.rectTransform, "Locked", new Vector2(0f, size.y / 2f - 16f),
                new Vector2(size.x, 22f), 11f, "LOCKED", new Color(1f, 0.75f, 0.3f));
        }
    }

    // ── Small shared helpers (same conventions as HUDController/MatchUpScreen) ──────────────────

    static RectTransform MakeFullScreenRect(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static RectTransform MakeRect(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        return rt;
    }

    static Image MakeImage(Transform parent, string name, Sprite sprite, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt  = MakeRect(parent, name, anchorMin, anchorMax, pivot, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color  = color;
        img.type   = Image.Type.Simple;
        return img;
    }

    static TextMeshProUGUI MakeCentredText(Transform parent, string name,
        Vector2 pos, Vector2 size, float fontSize, string text, Color color)
    {
        var rt  = MakeRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                      new Vector2(0.5f, 0.5f), pos, size);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        return tmp;
    }
}
