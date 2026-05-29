using UnityEngine;

namespace RailSwitchMVP.Config
{
    /// <summary>
    /// Config da Camada 3 (Continue após Morte). Custos, limites e timing do
    /// revive. ScriptableObject pra tunar sem recompilar.
    /// </summary>
    [CreateAssetMenu(fileName = "ReviveConfig", menuName = "RailSwitchMVP/Revive Config")]
    public class ReviveConfig : ScriptableObject
    {
        [Header("Continues")]
        [Tooltip("Máximo de continues (revives) por run. 0 = revive desabilitado.")]
        [Min(0)] public int maxContinuesPerRun = 2;

        [Tooltip("Custo em coins do 1º continue.")]
        [Min(0)] public int continueCost1 = 50;

        [Tooltip("Custo em coins do 2º continue (e seguintes).")]
        [Min(0)] public int continueCost2 = 150;

        [Tooltip("Permitir rewarded ad como alternativa ao custo em coins.")]
        public bool allowAdForContinue = true;

        [Header("Revive")]
        [Tooltip("Quantos metros antes do ponto de morte o player reaparece. Clampado " +
            "às rows ainda existentes (não recua além do que já foi despawnado).")]
        [Min(0f)] public float reviveSetbackDistance = 15f;

        [Tooltip("Segundos de invencibilidade após reviver. Cobre obstáculo letal, " +
            "dead-end e out-of-bounds — pra não reviver e morrer no mesmo instante.")]
        [Min(0f)] public float reviveGraceSeconds = 1.5f;

        [Header("Offer")]
        [Tooltip("Segundos do countdown da oferta de continue antes de ir pro game over final.")]
        [Min(1f)] public float offerCountdownSeconds = 5f;

        /// <summary>Custo em coins do próximo continue dado quantos já foram usados nesta run.</summary>
        public int GetContinueCost(int continuesUsed) => continuesUsed <= 0 ? continueCost1 : continueCost2;
    }
}
