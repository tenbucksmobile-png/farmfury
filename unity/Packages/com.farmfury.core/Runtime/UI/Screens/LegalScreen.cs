using UnityEngine;

namespace FarmFury.Shared.UI
{
    /// <summary>
    /// Arcade's Legal screen (Settings -> Policies): the Legal sign and two plaque buttons that open the franchise
    /// Privacy Policy and Terms of Use pages in the system browser, each behind the parental gate.
    /// </summary>
    public class LegalScreen : OverlayScreen
    {
        private const string PrivacyPolicyUrl = "https://www.farmfurygames.com/privacy/";
        private const string TermsOfUseUrl = "https://www.farmfurygames.com/terms/";
        private static readonly Vector2 ButtonSize = new(460f, 110f);
        private const float ButtonGap = 24f;

        public LegalScreen(Transform canvas, MenuArtBase art, ShopArtBase shop, ParentalGate gate) : base(canvas, "Legal", art, shop)
        {
            HeaderSign(Shop.legalSign, "LEGAL");

            var privacy = PlaqueButton(Rect, "PrivacyPolicyButton", "Privacy Policy", 40, () => gate.Ask(() => Application.OpenURL(PrivacyPolicyUrl)));
            UIKit.Place(privacy.image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -60f + (ButtonSize.y + ButtonGap) * 0.5f), ButtonSize);

            var terms = PlaqueButton(Rect, "TermsOfUseButton", "Terms of Use", 40, () => gate.Ask(() => Application.OpenURL(TermsOfUseUrl)));
            UIKit.Place(terms.image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -60f - (ButtonSize.y + ButtonGap) * 0.5f), ButtonSize);
        }
    }
}
