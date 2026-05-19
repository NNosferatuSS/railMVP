using UnityEngine;
using UnityEngine.InputSystem;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.InputSys
{
    /// <summary>
    /// Listener da tecla "use active item" (default: Space).
    /// Chama ActiveItemSlot.UseItem quando pressionada.
    ///
    /// Mobile future: tap em zona específica do HUD vira esse mesmo Use.
    /// </summary>
    public class ActiveItemInputHandler : MonoBehaviour
    {
        void Update()
        {
            if (ActiveItemSlot.Instance == null) return;
            // Só age durante o jogo (não no Game Over screen).
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.spaceKey.wasPressedThisFrame)
                ActiveItemSlot.Instance.UseItem();
        }
    }
}
