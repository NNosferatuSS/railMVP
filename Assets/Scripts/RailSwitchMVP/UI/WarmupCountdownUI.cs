using System.Collections;
using UnityEngine;
using TMPro;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Overlay do warmup 3-2-1-GO. Ativa quando GameManager.IsWarmup vira true e
    /// desativa quando termina. Números Chakra 46 accent, "GO" Chakra 34 safe.
    /// Escala 1.4→1.0 em 200ms + fade nos 100ms finais.
    /// </summary>
    public class WarmupCountdownUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text bigNumber;
        [SerializeField] private RectTransform bigRt;
        [SerializeField] private Color numberColor = new Color(0.13f, 0.89f, 1f);
        [SerializeField] private Color goColor     = new Color(0.49f, 1f,   0.31f);
        [SerializeField] private float beatDuration = 1f;

        bool _running;
        int _lastCount = -1;

        void Start()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            gameObject.SetActive(false);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (gm.IsWarmup && !_running) { _running = true; gameObject.SetActive(true); StartCoroutine(RunCo()); }
            else if (!gm.IsWarmup && _running) { _running = false; gameObject.SetActive(false); }
        }

        IEnumerator RunCo()
        {
            var counts = new[] { 3, 2, 1 };
            foreach (var c in counts)
            {
                yield return BeatCo(c.ToString(), numberColor);
                if (!_running) yield break;
            }
            yield return BeatCo("GO", goColor);
            _running = false;
            gameObject.SetActive(false);
        }

        IEnumerator BeatCo(string s, Color c)
        {
            if (bigNumber != null) { bigNumber.text = s; bigNumber.color = c; }
            if (bigRt == null) yield break;

            float scaleFrom = 1.4f, scaleTo = 1f;
            float t = 0f;
            if (group != null) group.alpha = 1f;
            while (t < beatDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / 0.2f);
                float s01 = 1f - Mathf.Pow(1f - p, 3f); // ease-out cubic
                bigRt.localScale = Vector3.one * Mathf.Lerp(scaleFrom, scaleTo, s01);

                if (group != null && t > beatDuration - 0.1f)
                    group.alpha = 1f - Mathf.Clamp01((t - (beatDuration - 0.1f)) / 0.1f);
                yield return null;
            }
        }
    }
}
