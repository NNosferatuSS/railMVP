using UnityEngine;
using RailSwitchMVP.Track;

namespace RailSwitchMVP.Visual
{
    /// <summary>
    /// Flash de emissão no conector quando o jogador troca a direção. Só visual
    /// (MaterialPropertyBlock no renderer da direção escolhida).
    /// </summary>
    public class AuroraSwitchJuice : MonoBehaviour
    {
        [SerializeField] private SwitchController controller;
        [SerializeField] private SwitchVisual visual;
        [SerializeField] private float flashAmount = 2.5f;
        [SerializeField] private float flashDuration = 0.28f;

        static readonly int FlashId = Shader.PropertyToID("_Flash");
        MaterialPropertyBlock _mpb;
        Renderer _r;
        float _t;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (controller == null) controller = GetComponent<SwitchController>();
            if (visual == null) visual = GetComponentInChildren<SwitchVisual>(true);
            if (controller != null) controller.OnStateChanged += OnChanged;
        }

        void OnDestroy()
        {
            if (controller != null) controller.OnStateChanged -= OnChanged;
        }

        void OnDisable()
        {
            // pool: não voltar com flash pendurado
            if (_r != null) Set(_r, 0f);
            _r = null;
            _t = 0f;
        }

        void OnChanged(SwitchState s)
        {
            if (visual == null) return;
            if (_r != null) Set(_r, 0f);
            _r = visual.GetRenderer(s);
            _t = flashDuration;
        }

        void Update()
        {
            if (_r == null || _t <= 0f) return;
            _t -= Time.deltaTime;
            float k = Mathf.Clamp01(_t / flashDuration);
            Set(_r, flashAmount * k * k);
            if (_t <= 0f) _r = null;
        }

        void Set(Renderer r, float v)
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(FlashId, v);
            r.SetPropertyBlock(_mpb);
        }
    }
}
