using UnityEngine;
using UnityEngine.InputSystem;

namespace RailSwitchMVP.InputSys
{
    /// <summary>
    /// Implementação de IDirectionalInput usando o New Input System.
    /// O projeto está em activeInputHandler=1 (New only), então UnityEngine.Input
    /// (legacy) não funciona — usamos Keyboard.current diretamente.
    ///
    /// Aceita setas ←/→ e A/D.
    /// </summary>
    public class KeyboardDirectionalInput : MonoBehaviour, IDirectionalInput
    {
        public int ConsumeDirection()
        {
            var kb = Keyboard.current;
            if (kb == null) return 0;

            // Quando Shift é segurado, as arrows são RESERVADAS pra active items
            // (Teleport direcional via ActiveItemInputHandler). Não nudga switch.
            if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) return 0;

            int dir = 0;
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
                dir = -1;
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                dir = 1;

            // LaneSwap debuff: inverte ← / → por N tiles (PostMVP2.5).
            if (dir != 0 && RailSwitchMVP.Core.PowerUpManager.Instance != null
                && RailSwitchMVP.Core.PowerUpManager.Instance.HasLaneSwapDebuff)
            {
                dir = -dir;
            }
            return dir;
        }
    }
}
