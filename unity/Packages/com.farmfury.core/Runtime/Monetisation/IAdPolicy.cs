namespace FarmFury.Shared.Monetisation
{
    /// <summary>
    /// The save-backed ad state <see cref="AdManager"/> needs from a game. Each game implements this (normally on its
    /// SaveManager, keeping its own save keys and prefix) and assigns it to <see cref="AdManager.Policy"/> at startup.
    /// </summary>
    public interface IAdPolicy
    {
        /// <summary>True once the player owns Remove Ads: no interstitials or banners.</summary>
        bool AdsRemoved { get; }

        /// <summary>Level loads since the last interstitial check fired. Persisted by the game.</summary>
        int LevelsSinceLastInterstitial { get; set; }

        /// <summary>Called just before an interstitial is shown (e.g. to pace a Remove Ads promotion).</summary>
        void OnInterstitialShown();
    }
}
