using System;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Analytics;
using FarmFury.Utilities;

namespace FarmFury.Core
{
    /// <summary>
    /// Wraps Unity Gaming Services Analytics (com.unity.services.analytics), ported from Farm Fury: Arcade: one
    /// singleton on GameManagers owns all SDK calls. Events: level_start, level_complete, level_failed, purchase,
    /// ad_shown. Needs the project linked to Stampede's Unity Cloud project (Project Settings > Services, with
    /// "targeted to children" set to Yes, as for Arcade) and the custom events registered in the Analytics Event
    /// Manager; unregistered events are silently dropped by the dashboard. Without a linked project, init fails
    /// with a warning and events are dropped.
    /// StartDataCollection() is kept (as in Arcade) over the newer consent API: the app is child-directed by
    /// default and has no consent UI.
    /// </summary>
    public class AnalyticsManager : MonoSingleton<AnalyticsManager>
    {
        public bool IsInitialized { get; private set; }

        private async void Start()
        {
            try
            {
                await UnityServices.InitializeAsync();
                AnalyticsService.Instance.StartDataCollection();
                IsInitialized = true;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Debug.Log($"[AnalyticsManager] Initialized. userId={AnalyticsService.Instance.GetAnalyticsUserID()} " +
                    $"sessionId={AnalyticsService.Instance.SessionID}");
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AnalyticsManager] Unity Gaming Services init failed: {e.Message}");
            }
        }

        /// <summary>Every LogXxx method below funnels through here — one place to keep the
        /// try/catch discipline every other SDK-boundary call in this project already uses
        /// (AdManager/IAPManager's own "a malformed native response shouldn't crash the app"
        /// convention), and one place a no-op-while-uninitialized guard lives.</summary>
        private void LogEvent(string eventName, params (string key, object value)[] parameters)
        {
            if (!IsInitialized)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                // An event recorded before Start()'s async init finishes (or after init failed) is
                // dropped. Logged in dev builds so a missing event can be told apart from one that
                // was sent but rejected by the dashboard.
                Debug.LogWarning($"[AnalyticsManager] DROPPED {eventName} (not initialized): {FormatParameters(parameters)}");
#endif
                return;
            }

            try
            {
                var customEvent = new CustomEvent(eventName);
                foreach (var (key, value) in parameters)
                {
                    switch (value)
                    {
                        case string s: customEvent.Add(key, s); break;
                        case int i: customEvent.Add(key, i); break;
                        case long l: customEvent.Add(key, l); break;
                        case float f: customEvent.Add(key, f); break;
                        case double d: customEvent.Add(key, d); break;
                        case bool b: customEvent.Add(key, b); break;
                        default: customEvent.Add(key, value?.ToString() ?? string.Empty); break;
                    }
                }
                AnalyticsService.Instance.RecordEvent(customEvent);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Debug.Log($"[AnalyticsManager] RECORDED {eventName}: {FormatParameters(parameters)}");
                // The SDK uploads on a 60s timer (AnalyticsContainer.k_AutoFlushPeriod in 6.3.0).
                // Flushing after every event in dev builds only, so a test session doesn't have to
                // wait on that timer before events can reach the Event Browser.
                AnalyticsService.Instance.Flush();
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AnalyticsManager] RecordEvent({eventName}) failed: {e.Message}");
            }
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static string FormatParameters((string key, object value)[] parameters)
        {
            var parts = new string[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var value = parameters[i].value;
                string typeName = value == null ? "null" : value.GetType().Name;
                parts[i] = $"{parameters[i].key}={value} ({typeName})";
            }
            return "{ " + string.Join(", ", parts) + " }";
        }
#endif

        /// <summary>GameManager.StartLevel: every attempt, including restarts.</summary>
        public void LogLevelStart(int levelIndex, string levelName) =>
            LogEvent("level_start",
                ("level_index", levelIndex),
                ("level_name", levelName ?? string.Empty));

        /// <summary>GameManager.EndLevel(true) — fires once per successful completion, after
        /// ComputeLevelResult has run.</summary>
        public void LogLevelComplete(int levelIndex, int stars, int score, float elapsedSeconds) =>
            LogEvent("level_complete",
                ("level_index", levelIndex),
                ("stars", stars),
                ("score", score),
                ("elapsed_seconds", elapsedSeconds));

        /// <summary>GameManager.EndLevel(false) — fires on both a timeout end and a declined
        /// revive; doesn't currently distinguish which, since both are "the run ended unsuccessfully"
        /// from an analytics standpoint.</summary>
        public void LogLevelFailed(int levelIndex, float elapsedSeconds) =>
            LogEvent("level_failed", ("level_index", levelIndex), ("elapsed_seconds", elapsedSeconds));

        /// <summary>IAPManager.HandlePurchasePendingInner — fires once per confirmed real-money
        /// purchase, after the effect has already been granted. priceString is whatever
        /// IAPManager.GetPriceString resolved (a real localized price once the store connection is
        /// live, otherwise the static fallback) — kept as a string rather than parsed into a
        /// currency/amount pair, since that parsing isn't reliable across locales/currencies.</summary>
        public void LogPurchase(string productId, string priceString) =>
            LogEvent("purchase", ("product_id", productId ?? string.Empty), ("price", priceString ?? string.Empty));

        /// <summary>A shop card or the New Worlds page came up (UI.ShopPromos, GameFlow).</summary>
        public void LogPromoShown(string promoId) => LogEvent("promo_shown", ("promo_id", promoId ?? string.Empty));

        /// <summary>A shop card, or Level Failed's Shop sign, was tapped.</summary>
        public void LogPromoTapped(string promoId) => LogEvent("promo_tapped", ("promo_id", promoId ?? string.Empty));

        /// <summary>AdManager — fires only on a confirmed-successful ad (a rewarded ad that
        /// actually granted its reward, or an interstitial that actually closed after showing) —
        /// never for a skipped/unready/failed attempt, so this tracks real ad exposure, not just
        /// button taps.</summary>
        public void LogAdShown(string placementType, string placementName) =>
            LogEvent("ad_shown",
                ("placement_type", placementType ?? string.Empty),
                ("placement_name", placementName ?? string.Empty));
    }
}
