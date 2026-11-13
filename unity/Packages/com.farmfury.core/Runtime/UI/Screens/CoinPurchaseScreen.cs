using FarmFury.Shared.Monetisation;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFury.Shared.UI
{
    /// <summary>
    /// Arcade's coin-pack screen (Shop -> Cash): the Shop sign, the four price plaques (100 / 500 / 5000 / 15000,
    /// coin count and price baked into the art) centred between the sign and the bottom content, a status line,
    /// and Restore Purchases on a plaque bottom-left (the store's required restore entry point lives here, on the
    /// purchase surface). Every purchase and restore passes the parental gate first.
    /// </summary>
    public class CoinPurchaseScreen : OverlayScreen
    {
        private static readonly string[] FallbackLabels = { "100", "500", "5000", "15000" };
        private static readonly Vector2 RestoreSize = new(340f, 100f);
        private const float RestoreLeft = 70f;
        private const float RestoreBottom = 40f;

        private readonly ParentalGate _gate;
        private readonly Text _status;
        private readonly Text _restoreStatus;

        public CoinPurchaseScreen(Transform canvas, MenuArtBase art, ShopArtBase shop, ParentalGate gate) : base(canvas, "CoinPurchase", art, shop)
        {
            _gate = gate;
            HeaderSign(Shop.shopSign, "SHOP");

            int count = Store.CoinPackIds.Count;
            var row = Row("CoinRow", count, new Vector2(ItemWidth, ItemHeight), IconSpacing, -92.5f);
            for (int i = 0; i < count; i++)
            {
                string productId = Store.CoinPackIds[i];
                IconButton(row, productId + "Button", ShopArtBase.At(Shop.coinPacks, i), (i < FallbackLabels.Length ? FallbackLabels[i] + " coins" : productId), () => _gate.Ask(() => Buy(productId)));
            }

            _status = StatusText(150f);

            var restore = PlaqueButton(Safe, "RestorePurchasesButton", "Restore Purchases", 30, () => _gate.Ask(Restore));
            UIKit.Place(restore.image.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(RestoreLeft, RestoreBottom), RestoreSize);

            _restoreStatus = UIKit.Label(Safe, "RestoreStatusText", "", 24, TextAnchor.MiddleCenter);
            UIKit.Place(_restoreStatus.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(RestoreLeft, RestoreBottom + RestoreSize.y + 8f), new Vector2(RestoreSize.x, 32f));
        }

        protected override void OnShow()
        {
            _status.text = "";
            _restoreStatus.text = "";
        }

        private void Buy(string productId)
        {
            _status.text = "Processing...";
            Store.Purchase(productId, (ok, message) => _status.text = ok ? "Purchase complete!" : message);
        }

        private void Restore()
        {
            _restoreStatus.text = "Restoring...";
            Store.RestorePurchases((ok, message) => _restoreStatus.text = ok ? "Purchases restored!" : message);
        }
    }
}
