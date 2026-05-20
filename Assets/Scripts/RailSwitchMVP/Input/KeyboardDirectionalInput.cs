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

            // Nota: durante warmup, input é PERMITIDO. Player pode errar no
            // warmup e morrer — vira micro-tutorial pela tentativa-e-erro.

            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
                return -1;
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                return 1;
            return 0;
        }
    }
}
