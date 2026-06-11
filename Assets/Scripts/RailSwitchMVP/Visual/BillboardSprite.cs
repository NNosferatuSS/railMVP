using UnityEngine;

namespace RailSwitchMVP.Visual
{
    /// <summary>
    /// Faz o SpriteRenderer sempre olhar pra câmera (billboard).
    /// Adicione num filho "Visual" de qualquer prefab de hazard ou power-up,
    /// configure Sprite + Tint no Inspector — pronto.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class BillboardSprite : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("Sprite exibido. Pode ser sobrescrito por Setup() em runtime.")]
        public Sprite sprite;

        [Tooltip("Tint aplicado ao sprite. Branco = sem alteração de cor.")]
        public Color tint = Color.white;

        private SpriteRenderer _sr;
        private Camera _cam;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            Apply();
        }

        void Start()
        {
            _cam = Camera.main;
        }

        void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            // Sempre olha na direção da câmera (billboard esférico).
            transform.rotation = Quaternion.LookRotation(_cam.transform.forward);
        }

        /// <summary>Configura sprite e tint de uma vez (útil em geração procedural).</summary>
        public void Setup(Sprite newSprite, Color newTint)
        {
            sprite = newSprite;
            tint = newTint;
            Apply();
        }

        /// <summary>Troca apenas o tint sem alterar o sprite.</summary>
        public void SetTint(Color newTint)
        {
            tint = newTint;
            if (_sr != null) _sr.color = newTint;
        }

        void Apply()
        {
            if (_sr == null) return;
            if (sprite != null) _sr.sprite = sprite;
            _sr.color = tint;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            Apply();
        }
#endif
    }
}
