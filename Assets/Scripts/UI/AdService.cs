using System;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBreaker
{
    /// <summary>
    /// Full-screen ads: rewarded before a continue, interstitial on entering a boss floor. On Android AdMobProvider serves AdMob.
    /// A continue needs a real rewarded ad: when AdMob has none loaded (offline, ad blocker, no fill) the continue is refused.
    /// An interstitial that failed to load is skipped. The built-in house ad (audio paused, closable after <see cref="adSeconds"/>)
    /// only stands in where no provider exists (editor, tests).
    /// </summary>
    public class AdService : MonoBehaviour
    {
        public interface IProvider
        {
            bool IsReady { get; }
            /// <summary>Shows the ad; <c>closed(true)</c> only when the reward was earned.</summary>
            void Show(Action<bool> closed);
            bool IsInterstitialReady { get; }
            void ShowInterstitial(Action closed);
        }

        public static AdService Instance { get; private set; }
        public static IProvider Provider;

        public CanvasGroup group;
        public Button closeButton;
        public GameObject countdown;
        public Text countdownText;
        public Image countdownRing;
        public RectTransform art;
        public float adSeconds = 5f;

        public bool IsShowing { get; private set; }
        public float Remaining { get; private set; }
        public bool CanClose => IsShowing && Remaining <= 0f;
        public int Shown { get; private set; }

        Action<bool> closed;

        void Awake()
        {
            Instance = this;
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
            SetVisible(false);
        }

        void OnDestroy()
        {
            if (IsShowing)
                AudioListener.pause = false;
            if (Instance == this)
                Instance = null;
        }

        /// <summary>False when an ad network is installed but has no rewarded ad loaded (e.g. blocked by AdGuard); polling it also retries the load.</summary>
        public bool RewardedAvailable => Provider == null || Provider.IsReady;

        /// <summary>
        /// Plays a rewarded ad; <paramref name="onClosed"/> gets true when the player earned the reward.
        /// Returns false, without calling <paramref name="onClosed"/>, when no AdMob ad is available: there is no house-ad fallback for rewards.
        /// </summary>
        public bool ShowRewarded(Action<bool> onClosed)
        {
            if (Provider == null)
            {
                Shown++;
                ShowHouseAd(onClosed);
                return true;
            }
            if (!Provider.IsReady)
                return false;
            Shown++;
            Provider.Show(onClosed);
            return true;
        }

        void ShowHouseAd(Action<bool> onClosed)
        {
            closed = onClosed;
            IsShowing = true;
            Remaining = Mathf.Max(0f, adSeconds);
            AudioListener.pause = true;
            SetVisible(true);
            Refresh();
        }

        /// <summary>Plays an ad with no reward attached; <paramref name="onClosed"/> runs once it has been exited.
        /// When AdMob has no interstitial loaded (offline, ad blocker, no fill) nothing is shown and <paramref name="onClosed"/> runs at once.</summary>
        public void ShowInterstitial(Action onClosed)
        {
            if (Provider == null)
            {
                Shown++;
                ShowHouseAd(_ => onClosed?.Invoke());
                return;
            }
            if (!Provider.IsInterstitialReady)
            {
                onClosed?.Invoke();
                return;
            }
            Shown++;
            Provider.ShowInterstitial(onClosed);
        }

        void Update()
        {
            if (!IsShowing)
                return;
            Remaining = Mathf.Max(0f, Remaining - Time.unscaledDeltaTime);
            Refresh();
            if (art != null)
                art.localScale = Vector3.one * (1f + 0.035f * Mathf.Sin(Time.unscaledTime * 3.2f));
        }

        void Refresh()
        {
            if (countdownText != null)
                countdownText.text = Mathf.CeilToInt(Remaining).ToString();
            if (countdownRing != null)
                countdownRing.fillAmount = adSeconds <= 0f ? 0f : Remaining / adSeconds;
            if (countdown != null)
                countdown.SetActive(Remaining > 0f);
            if (closeButton != null)
                closeButton.gameObject.SetActive(Remaining <= 0f);
        }

        /// <summary>Close (X) button: only works once the countdown has run out.</summary>
        public void Close()
        {
            if (!CanClose)
                return;
            IsShowing = false;
            AudioListener.pause = false;
            SetVisible(false);
            var callback = closed;
            closed = null;
            callback?.Invoke(true);
        }

        void SetVisible(bool visible)
        {
            if (group == null)
                return;
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
            group.gameObject.SetActive(visible);
        }
    }
}
