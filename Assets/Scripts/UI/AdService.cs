using System;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBreaker
{
    /// <summary>
    /// Full-screen interstitial played before a continue. The project has no ad network SDK yet, so a built-in
    /// house ad covers the whole screen (audio paused) and can be closed after <see cref="adSeconds"/>.
    /// An SDK can take over later by assigning <see cref="Provider"/>.
    /// </summary>
    public class AdService : MonoBehaviour
    {
        public interface IProvider
        {
            bool IsReady { get; }
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

        Action closed;

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

        public void ShowInterstitial(Action onClosed)
        {
            Shown++;
            if (Provider != null && Provider.IsReady)
            {
                Provider.ShowInterstitial(onClosed);
                return;
            }
            closed = onClosed;
            IsShowing = true;
            Remaining = Mathf.Max(0f, adSeconds);
            AudioListener.pause = true;
            SetVisible(true);
            Refresh();
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
            callback?.Invoke();
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
