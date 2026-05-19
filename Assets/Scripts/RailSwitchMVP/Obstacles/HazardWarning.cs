using UnityEngine;
using TMPro;

namespace RailSwitchMVP.Obstacles
{
    /// <summary>
    /// Componente visual que adiciona um ícone "!" 3D flutuante acima de um
    /// hazard, sempre orientado pra câmera (billboard). Player vê de longe e
    /// pode decidir o switch com antecedência.
    ///
    /// Cor + símbolo customizáveis via Setup() pra distinguir tipos
    /// (vermelho = Lethal, amarelo = Barrier). Auto-cria o filho TMP no Awake.
    /// </summary>
    public class HazardWarning : MonoBehaviour
    {
        [Header("Display")]
        [Tooltip("Símbolo exibido no ícone. \"!\" funciona com fonte default.")]
        public string symbol = "!";

        public Color color = new Color(1f, 0.85f, 0f); // amarelo brilhante default

        [Tooltip("Quanto acima do hazard o ícone fica.")]
        public float heightOffset = 2.5f;

        [Tooltip("Offset Z em relação ao hazard (em unidades mundiais). " +
            "Negativo = ícone aparece ANTES do hazard (próximo ao player), " +
            "dando antecedência pra o player desviar. Default 0 = mesma row.")]
        public float leadOffsetZ = 0f;

        [Tooltip("Tamanho da fonte do ícone (TMP world-space).")]
        public float fontSize = 8f;

        private TextMeshPro _tmp;
        private Transform _iconTransform;
        private Camera _camera;

        void Awake()
        {
            CreateIcon();
            ApplyToIcon();
        }

        void Start()
        {
            // Cache da camera (chamado em LateUpdate)
            _camera = Camera.main;
        }

        /// <summary>
        /// Configura cor + símbolo + offset do warning. Chamável após
        /// AddComponent pra customização pelo generator.
        /// </summary>
        public void Setup(Color newColor, string newSymbol = null, float? leadZ = null)
        {
            color = newColor;
            if (!string.IsNullOrEmpty(newSymbol)) symbol = newSymbol;
            if (leadZ.HasValue)
            {
                leadOffsetZ = leadZ.Value;
                if (_iconTransform != null)
                    _iconTransform.localPosition = new Vector3(0f, heightOffset, leadOffsetZ);
            }
            ApplyToIcon();
        }

        void CreateIcon()
        {
            if (_iconTransform != null) return;

            var iconGo = new GameObject("WarningIcon");
            iconGo.transform.SetParent(transform, false);
            iconGo.transform.localPosition = new Vector3(0f, heightOffset, leadOffsetZ);

            _tmp = iconGo.AddComponent<TextMeshPro>();
            _tmp.fontSize = fontSize;
            _tmp.alignment = TextAlignmentOptions.Center;
            _tmp.fontStyle = FontStyles.Bold;
            _tmp.enableWordWrapping = false;

            _iconTransform = iconGo.transform;
        }

        void ApplyToIcon()
        {
            if (_tmp == null) return;
            _tmp.text = symbol;
            _tmp.color = color;
        }

        void LateUpdate()
        {
            if (_iconTransform == null) return;
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null) return;
            }

            // Billboard: alinha o ícone à orientação da câmera pra ler de frente.
            _iconTransform.rotation = Quaternion.LookRotation(_camera.transform.forward);
        }
    }
}
