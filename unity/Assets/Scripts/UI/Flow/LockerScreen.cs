using System;
using FarmFury.Core;
using UnityEngine;
using UnityEngine.UI;
using FarmFury.Shared.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// The in-level Locker (franchise cosmetics, cut down from Stampede's LockerScreen): opened from the HUD's Locker
    /// button, it freezes the level while open. The Locker banner on top, then three rows of tiles - cannon skins,
    /// trails, impact effects - each tile the item's shop icon. Tapping an owned tile equips it, or takes it off if it
    /// is already on (no "None" tile since 2026-11-13); the equipped one carries the Equipped tick; an unowned tile is
    /// dimmed and opens that category's purchase page. A cannon skin shows at once (SaveManager.EquippedChanged); a
    /// trail on the next animal loaded.
    /// </summary>
    public class LockerScreen : OverlayScreen
    {
        // Layout (2026-11-13, user: "remove the none (only ticked and unticked) ... space the items equally so they don't
        // overlap each other or the banner"): three centred rows of equal tiles and equal gaps, laid out from the top,
        // starting a clear margin under the Locker banner (HeaderTop + HeaderSize.y); the Equipped tick sits INSIDE its
        // tile's corner. No "None" tile: tapping the equipped item takes it off again.
        private const float Tile = 140f, Gap = 50f, RowStep = 185f;
        private const float FirstRowTop = -HeaderTop + 310f + 45f;   // banner top offset + its height + a margin
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
            (_cannonTiles, _cannonBadges) = BuildRow("Cannons", StoreProducts.Cannons, 0, 0);
            (_trailTiles, _trailBadges) = BuildRow("Trails", StoreProducts.Trails, trailsFirst, 1);
            (_impactTiles, _impactBadges) = BuildRow("Impacts", StoreProducts.Impacts, impactsFirst, 2);
        }

        // Tile i = product i, its art the shop's cosmetic icon; the row centred, its top RowStep * row under the first.
        private (Image[], Image[]) BuildRow(string name, string[] products, int iconOffset, int rowIndex)
        {
            int count = products.Length;
            float width = count * Tile + (count - 1) * Gap;
            var row = UIKit.NewRect(name + "Row", Rect);
            UIKit.Place(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -(FirstRowTop + rowIndex * RowStep)),
                new Vector2(width, Tile));
            var tiles = new Image[count];
            var badges = new Image[count];
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var icon = ShopArt.At(Shop.cosmeticIcons, iconOffset + i);
                var button = UIKit.MakeButton(row, $"{name}_{i}", icon != null ? "" : StoreProducts.Cosmetics[iconOffset + i].name,
                    UIKit.Card, () => Tapped(products, index), 24);
                if (icon != null) { button.image.sprite = icon; button.image.preserveAspect = true; }
                UIKit.Place(button.image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * (Tile + Gap), 0f), new Vector2(Tile, Tile));
                tiles[i] = button.image;
                var badge = UIKit.Picture(button.transform, "Equipped", Shop.ownedBadge);
                UIKit.Place(badge.rectTransform, Vector2.one, Vector2.one, new Vector2(-4f, -4f), new Vector2(52f, 52f));   // inside the corner
                badges[i] = badge;
            }
            return (tiles, badges);
        }

        // Owned: equip it, or take it off if it's already on. Not owned: that category's purchase page.
        private void Tapped(string[] products, int index)
        {
            var save = SaveManager.Instance;
            if (save == null) { return; }
            string id = products[index];
            if (!save.IsCosmeticOwned(id))
            {
                (products == StoreProducts.Cannons ? _onCannons : products == StoreProducts.Impacts ? _onImpacts : _onTrails)?.Invoke();
                return;
            }
            save.SetEquipped(products, save.GetEquipped(products) == id ? string.Empty : id);
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
                string id = products[i];
                bool owned = save.IsCosmeticOwned(id);
                bool on = owned && id == equipped;
                tiles[i].color = tiles[i].sprite != null ? (owned ? Color.white : Unowned) : (owned ? UIKit.Card : UIKit.CardLocked);
                badges[i].gameObject.SetActive(on && badges[i].sprite != null);
            }
        }
    }
}
