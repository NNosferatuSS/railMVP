using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RailSwitchMVP.UI.Widgets
{
    /// <summary>
    /// Banner de hazard (topo do HUD). Slide-in de cima, triângulo pulsa,
    /// contador é a distância em s pro impacto. Fica hidden por default.
    /// </summary>
    public class HazardBannerUI : MonoBehaviour
    {
        [SerializeField] private Image bg;                // banner_hazard_[cor]
        [SerializeField] private Image warningTriangle;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text counterText;

        [SerializeField] private Sprite bgOrange, bgPink, bgViolet;
        [SerializeField] private float pulsePeriod = 1.1f;

        public enum Kind { Orange, Pink, Violet }

        RectTransform _rt;
        CanvasGroup _cg;
        float _pulseT;
        bool _showing;
        Coroutine _enterCo;

        Vector2 _restPos;

        void Awake()
        {
            _rt = (RectTransform)transform;
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
            // Guarda a posição de repouso ANTES de qualquer tween. Sem isso, um
            // Show() disparado no meio do slide anterior capturaria a posição
            // intermediária como repouso e o banner iria subindo a cada hazard.
            _restPos = _rt.anchoredPosition;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!_showing) return;
            _pulseT += Time.unscaledDeltaTime;
            float ph = Mathf.Repeat(_pulseT, pulsePeriod) / pulsePeriod;
            float a = Mathf.Lerp(0.35f, 1f, 0.5f + 0.5f * Mathf.Sin(ph * Mathf.PI * 2f));
            if (warningTriangle != null)
            {
                var c = warningTriangle.color; c.a = a; warningTriangle.color = c;
            }
        }

        public void Show(Kind kind, string title, string subtitle)
        {
            if (bg != null)
                bg.sprite = kind switch { Kind.Orange => bgOrange, Kind.Pink => bgPink, _ => bgViolet };
            if (titleText != null) titleText.text = title;
            if (subtitleText != null) subtitleText.text = subtitle;
            _showing = true;
            _pulseT = 0f;
            gameObject.SetActive(true);

            // Slide de cima + fade. StartCoroutine só funciona com o GO ativo,
            // então tem que vir depois do SetActive(true).
            if (_enterCo != null) StopCoroutine(_enterCo);
            _rt.anchoredPosition = _restPos;
            _enterCo = StartCoroutine(UITween.SlideFadeIn(_rt, _cg, -40f, 0.22f));
        }

        public void UpdateCounter(float secondsToImpact)
        {
            if (counterText != null) counterText.text = Mathf.Max(0, Mathf.CeilToInt(secondsToImpact)) + "s";
        }

        /// <summary>
        /// Contador em TILES (não segundos). Os debuffs do PowerUpManager expiram
        /// por tiles percorridos, então "4s" seria mentira — mostra "4" + unidade.
        /// </summary>
        public void UpdateCounterTiles(int tilesRemaining)
        {
            if (counterText != null) counterText.text = Mathf.Max(0, tilesRemaining).ToString();
        }

        public void Hide()
        {
            _showing = false;
            if (_enterCo != null) { StopCoroutine(_enterCo); _enterCo = null; }
            if (_rt != null) _rt.anchoredPosition = _restPos;
            if (_cg != null) _cg.alpha = 1f;
            gameObject.SetActive(false);
        }
    }
}
