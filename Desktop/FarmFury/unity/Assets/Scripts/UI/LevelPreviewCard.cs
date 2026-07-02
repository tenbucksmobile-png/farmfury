using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Popup shown when a WorldMapController marker (unlocked level) is tapped. Built once by
// WorldMapController and toggled via Show(levelIndex)/Hide() — same self-contained,
// runtime-built pattern as HUDController's Level Complete/Failed panels.
// Layout: a full-screen semi-transparent dismiss-catcher behind a centred card. The card has
// its own (transition-less) Button that swallows clicks so tapping inside it doesn't dismiss;
// tapping anywhere else on the dismiss-catcher does.
public class LevelPreviewCard : MonoBehaviour
{
    // Animal card art — same 8-slot array/keyword-matched wiring convention as
    // HUDController._cardSprites (Assets/Sprites/UI/Cards/, indexed by AnimalType).
    [SerializeField] private Sprite[] _animalCardSprites = new Sprite[8];

    private GameObject       _panel;
    private TextMeshProUGUI  _levelNumText;
    private TextMeshProUGUI  _matchupText;
    private Image            _animalImg;
    private TextMeshProUGUI  _starsText;
    private Button           _playBtn;
    private TextMeshProUGUI  _playBtnLabel;
    private int              _levelIndex;

    public void Init(Sprite squareSpr) => BuildUI(squareSpr);

    public void Show(int levelIndex)
    {
        _levelIndex = levelIndex;
        var data  = GameManager.Instance?.GetLevelData(levelIndex);
        int stars = ScoreManager.GetBestStars(levelIndex);

        _levelNumText.text = $"LEVEL {levelIndex + 1}";
        _starsText.text    = StarText(stars);

        if (data != null)
        {
            AnimalType animal = (data.birds != null && data.birds.Length > 0) ? data.birds[0] : AnimalType.Cluck;
            bool isHarvester  = data.robots != null && data.robots.Length > 0 && data.robots[0].robotType == RobotType.Harvester;
            _matchupText.text = $"{animal.ToString().ToUpperInvariant()}  vs  {(isHarvester ? "HARVESTER ROBOT" : "ROBOT")}";

            Sprite spr = _animalCardSprites != null && (int)animal < _animalCardSprites.Length
                ? _animalCardSprites[(int)animal] : null;
            _animalImg.sprite  = spr;
            _animalImg.enabled = spr != null;

            _playBtn.interactable = true;
            _playBtnLabel.text    = "PLAY";
        }
        else
        {
            // Level data doesn't exist yet (only L01-L06 are authored right now, out of 18
            // marker slots — see CLAUDE.md Gap Analysis) — show a placeholder instead of
            // crashing or launching a level with no content.
            _matchupText.text     = "COMING SOON";
            _animalImg.enabled    = false;
            _playBtn.interactable = false;
            _playBtnLabel.text    = "—"; // em dash
        }

        _panel.SetActive(true);
    }

    public void Hide() => _panel.SetActive(false);

    void OnPlayClicked()
    {
        Hide();
        GameManager.Instance?.ForceStartLevel(_levelIndex);
    }

    void OnDismissClicked() => Hide();

    static string StarText(int earned)
    {
        const string gold = "#FFD200";
        const string grey = "#8A8A90";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 3; i++)
        {
            if (i > 0) sb.Append("  ");
            sb.Append(i < earned ? $"<color={gold}>★</color>" : $"<color={grey}>★</color>");
        }
        return sb.ToString();
    }

    // ── UI construction ────────────────────────────────────────────────────────

    void BuildUI(Sprite squareSpr)
    {
        var rootRT = gameObject.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = rootRT.offsetMax = Vector2.zero;
        _panel = gameObject;

        // Dismiss-catcher fills the whole overlay
        var dismissImg = gameObject.AddComponent<Image>();
        dismissImg.sprite = squareSpr;
        dismissImg.color  = new Color(0f, 0f, 0f, 0.55f);
        var dismissBtn = gameObject.AddComponent<Button>();
        dismissBtn.targetGraphic = dismissImg;
        var dCols = dismissBtn.colors;
        dCols.normalColor = dCols.highlightedColor = dCols.pressedColor = Color.white;
        dismissBtn.colors = dCols;
        dismissBtn.onClick.AddListener(OnDismissClicked);

        // Card box
        var box   = new GameObject("Box");
        box.transform.SetParent(transform, false);
        var boxRT = box.AddComponent<RectTransform>();
        boxRT.anchorMin = new Vector2(0.5f, 0.5f);
        boxRT.anchorMax = new Vector2(0.5f, 0.5f);
        boxRT.pivot     = new Vector2(0.5f, 0.5f);
        boxRT.sizeDelta = new Vector2(520f, 440f);
        var boxImg = box.AddComponent<Image>();
        boxImg.sprite = squareSpr;
        boxImg.color  = new Color(0.97f, 0.94f, 0.88f);
        var boxBtn = box.AddComponent<Button>(); // swallows clicks so the card itself never dismisses
        boxBtn.transition = Selectable.Transition.None;

        _levelNumText = MakeLabel(box.transform, "LevelNum",
            new Vector2(0f, 165f), new Vector2(460f, 60f), 40f, new Color(0.12f, 0.10f, 0.06f));
        _matchupText = MakeLabel(box.transform, "Matchup",
            new Vector2(0f, -20f), new Vector2(460f, 46f), 24f, new Color(0.30f, 0.28f, 0.24f));

        var animGO = new GameObject("AnimalCard");
        animGO.transform.SetParent(box.transform, false);
        var animRT = animGO.AddComponent<RectTransform>();
        animRT.anchorMin        = new Vector2(0.5f, 0.5f);
        animRT.anchorMax        = new Vector2(0.5f, 0.5f);
        animRT.pivot            = new Vector2(0.5f, 0.5f);
        animRT.anchoredPosition = new Vector2(0f, 90f);
        animRT.sizeDelta        = new Vector2(160f, 200f);
        _animalImg = animGO.AddComponent<Image>();
        _animalImg.preserveAspect = true;

        _starsText = MakeLabel(box.transform, "Stars",
            new Vector2(0f, -80f), new Vector2(400f, 50f), 32f, Color.white);

        // PLAY button
        var playGO = new GameObject("PlayBtn");
        playGO.transform.SetParent(box.transform, false);
        var playRT = playGO.AddComponent<RectTransform>();
        playRT.anchorMin        = new Vector2(0.5f, 0f);
        playRT.anchorMax        = new Vector2(0.5f, 0f);
        playRT.pivot            = new Vector2(0.5f, 0f);
        playRT.anchoredPosition = new Vector2(0f, 30f);
        playRT.sizeDelta        = new Vector2(220f, 64f);
        var playImg = playGO.AddComponent<Image>();
        playImg.sprite = squareSpr;
        playImg.color  = new Color(1.00f, 0.55f, 0.05f);
        _playBtnLabel = MakeLabel(playGO.transform, "Label",
            Vector2.zero, new Vector2(220f, 64f), 28f, Color.white);
        _playBtn = playGO.AddComponent<Button>();
        _playBtn.targetGraphic = playImg;
        _playBtn.onClick.AddListener(OnPlayClicked);

        _panel.SetActive(false);
    }

    static TextMeshProUGUI MakeLabel(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text               = "";
        tmp.fontSize           = fontSize;
        tmp.color              = color;
        tmp.alignment          = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.richText           = true;
        tmp.raycastTarget      = false;
        return tmp;
    }
}
