#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>
    /// Google AdMob: a rewarded ad behind "Lanjutkan" and an interstitial on entering a boss floor. Asks for consent (UMP) on start,
    /// keeps one ad of each kind loaded, and reports whether the reward was earned. Until an ad is loaded <see cref="AdService"/> falls back to its house ad.
    /// Sideload APKs are built with SB_ADMOB_TEST and only request Google's test ads.
    /// </summary>
    public class AdMobProvider : AdService.IProvider
    {
#if SB_ADMOB_TEST
        const string RewardedUnitId = "ca-app-pub-3940256099942544/5224354917";
        const string InterstitialUnitId = "ca-app-pub-3940256099942544/1033173712";
#else
        const string RewardedUnitId = "ca-app-pub-6808667639334235/8407574176";
        const string InterstitialUnitId = "ca-app-pub-6808667639334235/9848849010";
#endif

        RewardedAd rewarded;
        InterstitialAd interstitial;
        bool loading;
        bool loadingInterstitial;
        bool initialized;
        float retryAt;
        float interstitialRetryAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (AdService.Provider != null)
                return;
            var provider = new AdMobProvider();
            AdService.Provider = provider;
            provider.Start();
        }

        void Start()
        {
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            ConsentInformation.Update(new ConsentRequestParameters(), updateError =>
            {
                if (updateError != null)
                    Debug.LogWarning("AdMob consent update: " + updateError.Message);
                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null)
                        Debug.LogWarning("AdMob consent form: " + formError.Message);
                    if (ConsentInformation.CanRequestAds())
                        Initialize();
                });
            });
            // Consent from an earlier session lets ads load while the update is still running.
            if (ConsentInformation.CanRequestAds())
                Initialize();
        }

        void Initialize()
        {
            if (initialized)
                return;
            initialized = true;
            MobileAds.Initialize(_ =>
            {
                Load();
                LoadInterstitial();
            });
        }

        void Load()
        {
            if (loading || (rewarded != null && rewarded.CanShowAd()))
                return;
            loading = true;
            RewardedAd.Load(RewardedUnitId, new AdRequest(), (ad, error) =>
            {
                loading = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning("AdMob rewarded load failed: " + (error != null ? error.GetMessage() : "no ad"));
                    retryAt = Time.realtimeSinceStartup + 30f;
                    return;
                }
                rewarded = ad;
            });
        }

        public bool IsReady
        {
            get
            {
                if (rewarded != null && rewarded.CanShowAd())
                    return true;
                if (initialized && !loading && Time.realtimeSinceStartup >= retryAt)
                    Load();
                return false;
            }
        }

        void LoadInterstitial()
        {
            if (string.IsNullOrEmpty(InterstitialUnitId) || loadingInterstitial || (interstitial != null && interstitial.CanShowAd()))
                return;
            loadingInterstitial = true;
            InterstitialAd.Load(InterstitialUnitId, new AdRequest(), (ad, error) =>
            {
                loadingInterstitial = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning("AdMob interstitial load failed: " + (error != null ? error.GetMessage() : "no ad"));
                    interstitialRetryAt = Time.realtimeSinceStartup + 30f;
                    return;
                }
                interstitial = ad;
            });
        }

        public bool IsInterstitialReady
        {
            get
            {
                if (interstitial != null && interstitial.CanShowAd())
                    return true;
                if (initialized && Time.realtimeSinceStartup >= interstitialRetryAt)
                    LoadInterstitial();
                return false;
            }
        }

        public void ShowInterstitial(Action closed)
        {
            var ad = interstitial;
            interstitial = null;
            if (ad == null || !ad.CanShowAd())
            {
                closed?.Invoke();
                return;
            }
            bool done = false;
            void Finish()
            {
                if (done)
                    return;
                done = true;
                AudioListener.pause = false;
                ad.Destroy();
                LoadInterstitial();
                closed?.Invoke();
            }
            ad.OnAdFullScreenContentClosed += Finish;
            ad.OnAdFullScreenContentFailed += error =>
            {
                Debug.LogWarning("AdMob interstitial show failed: " + error.GetMessage());
                Finish();
            };
            AudioListener.pause = true;
            ad.Show();
        }

        public void Show(Action<bool> closed)
        {
            var ad = rewarded;
            rewarded = null;
            if (ad == null || !ad.CanShowAd())
            {
                closed?.Invoke(false);
                return;
            }
            bool earned = false;
            bool done = false;
            void Finish()
            {
                if (done)
                    return;
                done = true;
                AudioListener.pause = false;
                ad.Destroy();
                Load();
                closed?.Invoke(earned);
            }
            ad.OnAdFullScreenContentClosed += Finish;
            ad.OnAdFullScreenContentFailed += error =>
            {
                Debug.LogWarning("AdMob rewarded show failed: " + error.GetMessage());
                Finish();
            };
            AudioListener.pause = true;
            ad.Show(_ => earned = true);
        }
    }
}
#endif
