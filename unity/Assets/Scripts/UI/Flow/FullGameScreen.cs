using FarmFury.Core;
using FarmFury.Shared.UI;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// Web demo (branch web-demo, after Stampede's): the "full game" panel. Opened by tapping any world other than
    /// Meadow Ruins on World Select ("&lt;World&gt; is in the full game", that world's badge), and once after the
    /// Commander (L18) is beaten ("You cleared Meadow Ruins!", Frozen Tundra's badge). The full game is on the App
    /// Store (a GET IT ON THE APP STORE plaque, behind the parental gate); Google Play is still to come. Back / Esc closes.
    /// </summary>
    public class FullGameScreen : OverlayScreen
    {
        private const string Stores = "Out now on the App Store - coming soon to Google Play.";

        private readonly Image _badge;
        private readonly Text _title;
        private readonly Text _body;
        private System.Action _onClosed;

        public FullGameScreen(Transform canvas, MenuArt art, ShopArt shop, ParentalGate gate) : base(canvas, "FullGame", art, shop)
        {
            _title = UIKit.Label(Rect, "Title", "", 76, TextAnchor.MiddleCenter, UIKit.Accent);
            _title.fontStyle = FontStyle.Bold;
            _title.gameObject.AddComponent<Outline>().effectDistance = new Vector2(4f, -4f);
            UIKit.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1500f, 120f));

            _badge = UIKit.Picture(Rect, "WorldBadge", null);
            _badge.preserveAspect = true;
            _badge.raycastTarget = false;
            UIKit.Place(_badge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(420f, 420f));

            _body = UIKit.Label(Rect, "Body", "", 44, TextAnchor.MiddleCenter, Color.white);
            _body.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3f, -3f);
            UIKit.Place(_body.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(1400f, 150f));

            var store = PlaqueButton(Rect, "AppStore", "GET IT ON THE APP STORE", 34,
                () => gate.Ask(() => Application.OpenURL(WebDemo.AppStoreUrl)));
            UIKit.Place(store.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(560f, 100f));
        }

        /// <summary>A world that isn't in the demo was tapped.</summary>
        public void ShowLockedWorld(WorldType world)
        {
            var data = DataManager.Instance != null ? DataManager.Instance.GetWorldData(world) : null;
            string name = data != null && !string.IsNullOrEmpty(data.displayName) ? data.displayName : world.ToString();
            Open($"{name} is in the full game", $"This demo has Meadow Ruins. {Stores}", data, null);
        }

        /// <summary>The demo's boss is beaten; onClosed runs when the panel is closed.</summary>
        public void ShowDemoComplete(System.Action onClosed)
        {
            var data = DataManager.Instance != null ? DataManager.Instance.GetWorldData(WorldType.FrozenTundra) : null;
            Open("You cleared Meadow Ruins!", $"Frozen Tundra and seven more worlds are waiting in the full game. {Stores}", data, onClosed);
        }

        private void Open(string title, string body, WorldData badgeWorld, System.Action onClosed)
        {
            _onClosed = onClosed;
            _title.text = title;
            _body.text = body;
            var art = badgeWorld != null ? badgeWorld.selectBadge : null;
            _badge.sprite = art;
            _badge.gameObject.SetActive(art != null);
            Show();
        }

        public override void Hide()
        {
            bool wasOpen = IsOpen;
            base.Hide();
            if (wasOpen)
            {
                var then = _onClosed;
                _onClosed = null;
                then?.Invoke();
            }
        }
    }
}
