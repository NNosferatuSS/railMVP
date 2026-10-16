using UnityEngine;

namespace RailSwitchMVP.InputSys
{
    /// <summary>
    /// Tenta múltiplas implementações de IDirectionalInput em ordem.
    /// A primeira que retornar direção != 0 vence o frame. Permite mixar
    /// KeyboardDirectionalInput (Editor) + TouchDirectionalInput (Android)
    /// sem mexer no PlayerRailRider (que aceita 1 só inputSource).
    /// </summary>
    public class CompositeDirectionalInput : MonoBehaviour, IDirectionalInput, ISwitchCommandInput
    {
        [Tooltip("Componentes que implementam IDirectionalInput, em ordem de prioridade. " +
            "Recomendado: [TouchDirectionalInput, KeyboardDirectionalInput] — touch primeiro " +
            "porque é silencioso em desktop (Touchscreen.current == null).")]
        [SerializeField] private MonoBehaviour[] sources;

        public int ConsumeDirection()
        {
            if (sources == null) return 0;
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] is IDirectionalInput di)
                {
                    int dir = di.ConsumeDirection();
                    if (dir != 0) return dir;
                }
            }
            return 0;
        }

        /// <summary>Repassa comandos ricos (toque no trilho, swipe-override) das fontes que os produzem.</summary>
        public bool TryConsumeCommand(out SwitchCommand cmd)
        {
            if (sources != null)
                for (int i = 0; i < sources.Length; i++)
                    if (sources[i] is ISwitchCommandInput ci && ci.TryConsumeCommand(out cmd))
                        return true;
            cmd = default;
            return false;
        }
    }
}
