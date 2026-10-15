using UnityEngine;

namespace RailSwitchMVP.Config
{
    /// <summary>
    /// Preset de animação do switch "ponte elevadiça". Cada direção do switch
    /// (Left/Middle/Right) tem uma mesh própria que gira em X: a direção
    /// SELECIONADA sobe para raisedAngleX (0 = deitada/atravessável), as outras
    /// duas descem para loweredAngleX (-90 = pendurada pra baixo).
    ///
    /// Só visual — a direção real do player vem de SwitchController.State.
    /// </summary>
    [CreateAssetMenu(fileName = "SwitchVisualConfig", menuName = "RailSwitchMVP/Switch Visual Config")]
    public class SwitchVisualConfig : ScriptableObject, IValidatedConfig
    {
        /// <summary>Curvas prontas. Custom = usa a AnimationCurve exposta.</summary>
        public enum EasePreset
        {
            Linear,
            EaseOut,
            PopBack,
            Elastic,
            Custom
        }

        /// <summary>Como a mesh entra/sai de cena.</summary>
        public enum AnimMode
        {
            /// <summary>Gira em X entre loweredAngleX e raisedAngleX (ponte elevadiça).</summary>
            HingeX,
            /// <summary>Estende em Z local de 0 a 1 a partir do pivot (fita de luz atravessando o gap).</summary>
            ExtendZ
        }

        [Header("Tweak / decisão de design")]
        [Tooltip("HingeX: ponte elevadiça (gira em X). ExtendZ: a direção escolhida se " +
            "estende do fim do trilho até a próxima linha; as outras recolhem pra 0. " +
            "Em ExtendZ os ângulos são ignorados.")]
        public AnimMode animMode = AnimMode.HingeX;

        [Tooltip("ON: a mesh levantada só fica VERDE quando o switch aponta pra uma lane " +
            "válida; quando aponta pro vazio ela volta pro material original (sem vermelho). " +
            "OFF: comportamento clássico verde/vermelho.")]
        public bool colorOnlyConnected = false;

        [Tooltip("ON: a direção antiga DESCE por completo antes da nova começar a subir " +
            "(espera lowerDuration + raiseDelay). Leitura de mecanismo, mais lento. " +
            "OFF: as duas se movem juntas, só defasadas por raiseDelay. Mais rápido e responsivo.")]
        public bool sequenceLowerThenRaise = false;

        [Header("Ângulos (locais, eixo X)")]
        [Tooltip("Ângulo X da mesh SELECIONADA. 0 = deitada na horizontal (posição de passagem).")]
        [Range(-180f, 180f)]
        public float raisedAngleX = 0f;

        [Tooltip("Ângulo X das meshes NÃO selecionadas. -90 = pendurada pra baixo, " +
            "fora do caminho (ponte elevadiça vindo de baixo).")]
        [Range(-180f, 180f)]
        public float loweredAngleX = -90f;

        [Header("Velocidade")]
        [Tooltip("Duração (s) da mesh SUBINDO até raisedAngleX. Curto = pop seco.")]
        [Range(0.01f, 2f)]
        public float raiseDuration = 0.18f;

        [Tooltip("Duração (s) da mesh DESCENDO até loweredAngleX.")]
        [Range(0.01f, 2f)]
        public float lowerDuration = 0.12f;

        [Tooltip("Atraso (s) antes da nova direção começar a subir. Dá o beat de " +
            "\"a antiga baixa, depois a nova sobe\" — sem isso as duas se movem no " +
            "mesmo frame e o olho lê como troca de sprite, não como mecanismo.")]
        [Range(0f, 0.5f)]
        public float raiseDelay = 0.04f;

        [Header("Easing")]
        [Tooltip("Curva do movimento de SUBIR. PopBack passa um pouco de 0 e volta.")]
        public EasePreset raiseEase = EasePreset.PopBack;

        [Tooltip("Curva do movimento de DESCER. EaseOut costuma ficar melhor " +
            "(overshoot pra baixo não é visível — a mesh já está fora de vista).")]
        public EasePreset lowerEase = EasePreset.EaseOut;

        [Tooltip("Intensidade do overshoot do preset PopBack. 0 = sem pop. " +
            "Elastic tem amplitude própria e ignora este valor.")]
        [Range(0f, 4f)]
        public float overshoot = 1.7f;

        [Tooltip("Usada quando o preset é Custom. Espera-se f(0)=0 e f(1)=1 " +
            "(pode passar de 1 no meio pra criar overshoot).")]
        public AnimationCurve customRaiseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Usada quando lowerEase é Custom.")]
        public AnimationCurve customLowerCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        /// <summary>Progresso eased (0..1, pode passar de 1) do movimento de subir.</summary>
        public float EvaluateRaise(float t) => Evaluate(raiseEase, customRaiseCurve, t);

        /// <summary>Progresso eased (0..1, pode passar de 1) do movimento de descer.</summary>
        public float EvaluateLower(float t) => Evaluate(lowerEase, customLowerCurve, t);

        float Evaluate(EasePreset preset, AnimationCurve custom, float t)
        {
            t = Mathf.Clamp01(t);
            switch (preset)
            {
                case EasePreset.Linear:
                    return t;

                case EasePreset.EaseOut:
                {
                    float inv = 1f - t;
                    return 1f - inv * inv * inv;
                }

                case EasePreset.PopBack:
                {
                    // Back-ease-out: passa do alvo e volta. c1 = overshoot.
                    float c1 = overshoot;
                    float c3 = c1 + 1f;
                    float p = t - 1f;
                    return 1f + c3 * p * p * p + c1 * p * p;
                }

                case EasePreset.Elastic:
                {
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    // Ease-out elastic clássico: passa do alvo e oscila até assentar.
                    // A amplitude é fixa (a fórmula precisa dela pra começar exatamente
                    // em 0) — quem controla intensidade de pop é o PopBack.
                    const float c4 = 2f * Mathf.PI / 3f;
                    return 1f + Mathf.Pow(2f, -10f * t) * Mathf.Sin((10f * t - 0.75f) * c4);
                }

                case EasePreset.Custom:
                    return custom != null ? custom.Evaluate(t) : t;

                default:
                    return t;
            }
        }

        /// <summary>
        /// Espera (s) antes da nova direção começar a subir. Sequenciado = espera a
        /// antiga descer por completo; senão só a defasagem de raiseDelay.
        /// </summary>
        public float RaiseWaitSeconds => sequenceLowerThenRaise
            ? lowerDuration + raiseDelay
            : raiseDelay;

        /// <summary>Tempo total de uma troca de direção, ponta a ponta.</summary>
        public float TotalSwitchDuration => RaiseWaitSeconds + raiseDuration;

        public string GetValidationWarnings()
        {
            var sb = new System.Text.StringBuilder();

            if (animMode == AnimMode.HingeX && Mathf.Approximately(raisedAngleX, loweredAngleX))
                sb.AppendLine("• raisedAngleX == loweredAngleX: as meshes não vão se mover.");

            if (raiseEase == EasePreset.Custom && customRaiseCurve == null)
                sb.AppendLine("• raiseEase = Custom mas customRaiseCurve está vazia (vai cair em linear).");

            if (lowerEase == EasePreset.Custom && customLowerCurve == null)
                sb.AppendLine("• lowerEase = Custom mas customLowerCurve está vazia (vai cair em linear).");

            if (!sequenceLowerThenRaise && raiseDelay >= raiseDuration)
                sb.AppendLine($"• raiseDelay ({raiseDelay:0.##}s) >= raiseDuration ({raiseDuration:0.##}s): " +
                    "a mesh nova fica parada mais tempo do que leva pra subir — pode parecer travado.");

            if (TotalSwitchDuration > 0.6f)
                sb.AppendLine($"• Troca completa leva ~{TotalSwitchDuration:0.##}s" +
                    (sequenceLowerThenRaise ? " (sequenciado)" : "") +
                    " — confira se cabe no tempo que o player tem dentro do tile, " +
                    "senão a ponte ainda está subindo quando ele chega no fim.");

            if ((raiseEase == EasePreset.PopBack || lowerEase == EasePreset.PopBack)
                && Mathf.Approximately(overshoot, 0f))
                sb.AppendLine("• PopBack selecionado mas overshoot = 0: o efeito vira um ease comum.");

            return sb.Length > 0 ? sb.ToString().TrimEnd() : null;
        }
    }
}
