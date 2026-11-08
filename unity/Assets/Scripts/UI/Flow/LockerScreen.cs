using System;
using FarmFury.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// The in-level Locker (franchise cosmetics, cut down from Stampede's LockerScreen): opened from the HUD's Locker
    /// button, it freezes the level while open. The Locker banner on top, then two rows of tiles — Hats (None + the
    /// five hats) and Trails (None + the six trails), each tile the item's shop icon. An owned tile equips it (a hat
    /// on every animal — they share hats in FarmFury — and the trail behind all of them), the equipped one gets the
    /// Equipped badge; an unowned tile is dimmed and opens that category's purchase page. Changes show on the next
    /// animal loaded into the cannon.
    /// </summary>
    public class LockerScreen : OverlayScreen
    {
        private const float Tile = 150f, Gap = 22f;
        private static readonly Color Unowned = new(0.45f, 0.45f, 0.48f, 1f);

        private readonly Action _onCannons, _onTrails, _onImpacts;
        private readonly Image[] _cannonTiles, _trailTiles, _impactTiles;
        private readonly Image[] _cannonBadges, _trailBadges, _impactBadges;
        private bool _froze;

        public LockerScreen(Transform canvas, MenuArt art, ShopArt shop, Action onCannons, Action onTrails, Action onImpacts)
            : base(canvas, "Locker", art, shop)
        {
            _onCannons = onCannons;
            _onTrails = onTrails;
            _onImpacts = onImpacts;
            HeaderSign(Shop.lockerBanner, "LOCKER");
            int trailsFirst = StoreProducts.Cannons.Length, impactsFirst = trailsFirst + StoreProducts.Trails.Length;
            (_cannonTiles, _cannonBadges) = BuildRow("Cannons", StoreProducts.Cannons, 0, 110f);
            (_trailTiles, _trailBadges) = BuildRow("Trails", StoreProducts.Trails, trailsFirst, -75f);
            (_impactTiles, _impactBadges) = BuildRow("Impacts", StoreProducts.Impacts, impactsFirst, -260f);
        }

        // Tile 0 = "None"; tile i+1 = product i, its art the shop's cosmetic icon.
        private (Image[], Image[]) BuildRow(string name, string[] products, int iconOffset, float y)
        {
            int count = products.Length + 1;
            var row = UIKit.NewRect(name + "Row", Rect);
            UIKit.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(count * (Tile + Gap), Tile));
            var tiles = new Image[count];
            var badges = new Image[count];
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var icon = i == 0 ? null : ShopArt.At(Shop.cosmeticIcons, iconOffset + i - 1);
                var button = UIKit.MakeButton(row, $"{name}_{i}", i == 0 ? "NONE" : icon != null ? "" : StoreProducts.Cosmetics[iconOffset + i - 1].name,
                    UIKit.Card, () => Tapped(products, index), 28);
                if (icon != null) { button.image.sprite = icon; button.image.preserveAspect = true; }
                UIKit.Place(button.image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * (Tile + Gap), 0f), new Vector2(Tile, Tile));
                tiles[i] = button.image;
                var badge = UIKit.Picture(button.transform, "Equipped", Shop.ownedBadge);
                UIKit.Place(badge.rectTransform, Vector2.one, Vector2.one, new Vector2(10f, 10f), new Vector2(64f, 64f));
                badges[i] = badge;
            }
            return (tiles, badges);
        }

        private void Tapped(string[] products, int index)
        {
            var save = SaveManager.Instance;
            if (save == null) { return; }
            string id = index == 0 ? string.Empty : products[index - 1];
            if (index > 0 && !save.IsCosmeticOwned(id))
            {
                (products == StoreProducts.Cannons ? _onCannons : products == StoreProducts.Impacts ? _onImpacts : _onTrails)?.Invoke();
                return;
            }
            save.SetEquipped(products, id);
            save.SaveProgress();
            Refresh();
        }

        public void Open()
        {
            // Freezes a level in progress while the Locker is open (not via Pause: no pause screen behind it).
            var gm = GameManager.Instance;
            _froze = gm != null && gm.State == GameState.Playing && !gm.ReviveDecisionPending && Time.timeScale > 0f;
            if (_froze) { Time.timeScale = 0f; }
            Show();
        }

        public override void Hide()
        {
            base.Hide();
            var gm = GameManager.Instance;
            if (_froze && gm != null && gm.State == GameState.Playing) { Time.timeScale = 1f; }
            _froze = false;
        }

        protected override void OnShow() => Refresh();

        private void Refresh()
        {
            var save = SaveManager.Instance;
            if (save == null) { return; }
            RefreshRow(StoreProducts.Cannons, _cannonTiles, _cannonBadges, save.GetEquippedCannon(), save);
            RefreshRow(StoreProducts.Trails, _trailTiles, _trailBadges, save.GetEquippedTrail(), save);
            RefreshRow(StoreProducts.Impacts, _impactTiles, _impactBadges, save.GetEquippedImpact(), save);
        }

        private static void RefreshRow(string[] products, Image[] tiles, Image[] badges, string equipped, SaveManager save)
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                string id = i == 0 ? string.Empty : products[i - 1];
                bool owned = i == 0 || save.IsCosmeticOwned(id);
                bool on = owned && (i == 0 ? string.IsNullOrEmpty(equipped) || Array.IndexOf(products, equipped) < 0 : id == equipped);
                tiles[i].color = tiles[i].sprite != null ? (owned ? Color.white : Unowned) : (owned ? UIKit.Card : UIKit.CardLocked);
                badges[i].gameObject.SetActive(on && badges[i].sprite != null);
            }
        }
    }
}
