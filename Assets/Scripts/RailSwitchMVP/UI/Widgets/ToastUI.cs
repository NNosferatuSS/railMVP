using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RailSwitchMVP.UI.Widgets
{
    /// <summary>
    /// Toast (slide-in de baixo 200ms, hold 2.5s, fade-out 200ms). Cores verde/vermelho.
    /// Uso: ToastUI.Instance.Show("NOT ENOUGH GEMS", ToastUI.Kind.Red);
    /// </summary>
    public class ToastUI : MonoBehaviour
    {
        public enum Kind { Green, Red }

        public static ToastUI Instance { get; private set; }

        [SerializeField] private Image bg;
        [SerializeField] private Image dot;
        [SerializeField] private TMP_Text text;
        [SerializeField] private Sprite toastGreen, toastRed, dotGreen, dotRed;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private float hold = 2.5f;

        void Awake()
        {
            Instance = this;
            if (group != null) group.alpha = 0f;
            gameObject.SetActive(false);
        }

        public void Show(string message, Kind kind)
        {
            if (bg != null) bg.sprite = kind == Kind.Green ? toastGreen : toastRed;
            if (dot != null) dot.sprite = kind == Kind.Green ? dotGreen : dotRed;
            if (text != null) text.text = message;
            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(PlayCo());
        }

        IEnumerator PlayCo()
        {
            if (group == null) yield break;
            float t = 0f;
            while (t < 0.2f) { t += Time.unscaledDeltaTime; group.alpha = Mathf.Clamp01(t / 0.2f); yield return null; }
            group.alpha = 1f;
            yield return new WaitForSecondsRealtime(hold);
            t = 0f;
            while (t < 0.2f) { t += Time.unscaledDeltaTime; group.alpha = 1f - Mathf.Clamp01(t / 0.2f); yield return null; }
            group.alpha = 0f;
            gameObject.SetActive(false);
        }
    }
}
