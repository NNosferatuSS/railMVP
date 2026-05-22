using UnityEngine;
using RailSwitchMVP.Meta;

namespace RailSwitchMVP.Player
{
    /// <summary>
    /// Aplica a cor primária do personagem equipado no Renderer do Player
    /// usando MaterialPropertyBlock (sem instanciar material novo — preserva
    /// batching). Lê do PlayerDataManager no Start e subscribe a
    /// OnEquippedCharChanged pra reagir live.
    ///
    /// Component separado do PlayerRailRider pra manter scope — vai no
    /// mesmo GameObject ou num filho que tenha o Renderer.
    /// </summary>
    public class PlayerCharacterApplier : MonoBehaviour
    {
        [Tooltip("Se vazio, busca o primeiro Renderer no GameObject ou em filhos.")]
        [SerializeField] private Renderer targetRenderer;

        // Suporta URP (_BaseColor) e Built-in Standard (_Color). Setar ambos
        // é seguro — propriedades inexistentes no shader são ignoradas.
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        MaterialPropertyBlock _mpb;

        void Start()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponentInChildren<Renderer>();
            if (targetRenderer == null)
            {
                Debug.LogWarning("[PlayerCharacterApplier] Sem Renderer no Player — cor de personagem não vai aplicar.");
                return;
            }
            _mpb = new MaterialPropertyBlock();
            Apply();

            var pdm = PlayerDataManager.Instance;
            if (pdm != null) pdm.OnEquippedCharChanged += HandleEquippedChanged;
        }

        void OnDestroy()
        {
            var pdm = PlayerDataManager.Instance;
            if (pdm != null) pdm.OnEquippedCharChanged -= HandleEquippedChanged;
        }

        void HandleEquippedChanged(int _) => Apply();

        void Apply()
        {
            if (_mpb == null || targetRenderer == null) return;
            var pdm = PlayerDataManager.Instance;
            int idx = pdm != null ? pdm.EquippedChar : 0;
            var def = CharacterCatalog.Get(idx);

            targetRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, def.Primary);
            _mpb.SetColor(ColorId, def.Primary);
            targetRenderer.SetPropertyBlock(_mpb);
        }
    }
}
