using UnityEngine;
using UnityEngine.InputSystem;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.InputSys
{
    /// <summary>
    /// Listener de inputs pra items ativos.
    /// - **Space** = use item não-direcional (TimeFreeze).
    /// - **Shift + ←** (ou A) = teleport esquerda (direcional).
    /// - **Shift + →** (ou D) = teleport direita.
    ///
    /// Quando Shift é pressionado, o KeyboardDirectionalInput ignora as
    /// setas (não nudga o switch) — assim os 2 inputs (switch vs teleport)
    /// não brigam pela mesma tecla.
    ///
    /// Mobile future: tap em zona = Space; swipe lateral = Teleport directional.
    /// </summary>
    public class ActiveItemInputHandler : MonoBehaviour
    {
        void Update()
        {
            if (ActiveItemSlot.Instance == null) return;
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            // Non-directional use (Space)
            if (kb.spaceKey.wasPressedThisFrame)
                ActiveItemSlot.Instance.UseItem();

            // Directional use (Shift + arrow)
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (!shift) return;

            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
                ActiveItemSlot.Instance.UseItemWithDirection(-1);
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                ActiveItemSlot.Instance.UseItemWithDirection(+1);
        }
    }
}
