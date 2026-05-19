using UnityEngine;
using UnityEngine.InputSystem;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.InputSys
{
    /// <summary>
    /// Listener de inputs pra items ativos/ações especiais.
    /// - **Space** = use item do ActiveItemSlot (TimeFreeze).
    /// - **Shift + ←/→** (ou A/D) = Teleport laterais SE
    ///   PowerUpManager.HasTeleport (window tile-based aberto).
    ///   Cada uso NÃO consome o window — só transições de tile.
    ///
    /// KeyboardDirectionalInput ignora setas com Shift held, então os
    /// inputs (switch vs teleport) não brigam pela mesma tecla.
    /// </summary>
    public class ActiveItemInputHandler : MonoBehaviour
    {
        void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            // Non-directional active item use (TimeFreeze via slot)
            if (kb.spaceKey.wasPressedThisFrame && ActiveItemSlot.Instance != null)
                ActiveItemSlot.Instance.UseItem();

            // Teleport (passive power-up, tile-based window)
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (!shift) return;

            bool pressedLeft = kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame;
            bool pressedRight = kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
            if (!pressedLeft && !pressedRight) return;

            // Só dispara se o power-up Teleport está ativo (window aberto).
            if (PowerUpManager.Instance == null || !PowerUpManager.Instance.HasTeleport) return;
            if (TeleportController.Instance == null) return;

            int dir = pressedLeft ? -1 : +1;
            TeleportController.Instance.TryTrigger(dir);
        }
    }
}
