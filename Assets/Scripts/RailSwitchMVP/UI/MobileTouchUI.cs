using UnityEngine;
using UnityEngine.UI;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Botões on-screen pra usar Teleport (Shift+←/→) em mobile.
    /// Auto-hide baseado em PowerUpManager.HasTeleport — botões só aparecem
    /// quando o input está disponível.
    ///
    /// Keyboard continua funcionando paralelo (ActiveItemInputHandler intacto).
    /// Em desktop você pode escolher escondê-los via hideOnDesktop.
    ///
    /// O botão de active item (Space) foi removido junto com o sistema de slot —
    /// power-ups agora são consumidos na colisão.
    /// </summary>
    public class MobileTouchUI : MonoBehaviour
    {
        [Header("Teleport L / R (equivalente Shift+←/→)")]
        [SerializeField] private Button teleportLeftButton;
        [SerializeField] private Button teleportRightButton;
        [Tooltip("Parent dos 2 botões de teleport — usado pra esconder ambos juntos. " +
            "Se null, esconde cada botão individualmente.")]
        [SerializeField] private GameObject teleportGroup;

        [Tooltip("Quando true, esconde botões em desktop (Standalone/Editor). " +
            "Default false — testar em ambos.")]
        [SerializeField] private bool hideOnDesktop = false;

        bool _wired;

        void Start()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            if (hideOnDesktop)
            {
                HideAll();
                return;
            }
#endif

            if (teleportLeftButton != null)
                teleportLeftButton.onClick.AddListener(() => OnTeleportPressed(-1));
            if (teleportRightButton != null)
                teleportRightButton.onClick.AddListener(() => OnTeleportPressed(+1));

            _wired = true;
            SubscribeEvents();
            RefreshVisibility();
        }

        void OnDestroy()
        {
            UnsubscribeEvents();
        }

        void HideAll()
        {
            if (teleportGroup != null) teleportGroup.SetActive(false);
            else
            {
                if (teleportLeftButton != null) teleportLeftButton.gameObject.SetActive(false);
                if (teleportRightButton != null) teleportRightButton.gameObject.SetActive(false);
            }
        }

        void SubscribeEvents()
        {
            if (PowerUpManager.Instance != null)
            {
                PowerUpManager.Instance.OnPowerUpTick += HandlePowerUpTick;
                PowerUpManager.Instance.OnPowerUpExpired += HandlePowerUpExpired;
            }
        }

        void UnsubscribeEvents()
        {
            if (PowerUpManager.Instance != null)
            {
                PowerUpManager.Instance.OnPowerUpTick -= HandlePowerUpTick;
                PowerUpManager.Instance.OnPowerUpExpired -= HandlePowerUpExpired;
            }
        }

        void HandlePowerUpTick(PowerUpType type, int value)
        {
            if (type == PowerUpType.Teleport) SetTeleportVisible(value > 0);
        }

        void HandlePowerUpExpired(PowerUpType type)
        {
            if (type == PowerUpType.Teleport) SetTeleportVisible(false);
        }

        void RefreshVisibility()
        {
            bool tele = PowerUpManager.Instance != null && PowerUpManager.Instance.HasTeleport;
            SetTeleportVisible(tele);
        }

        void SetTeleportVisible(bool visible)
        {
            if (teleportGroup != null)
            {
                if (teleportGroup.activeSelf != visible) teleportGroup.SetActive(visible);
                return;
            }
            if (teleportLeftButton != null && teleportLeftButton.gameObject.activeSelf != visible)
                teleportLeftButton.gameObject.SetActive(visible);
            if (teleportRightButton != null && teleportRightButton.gameObject.activeSelf != visible)
                teleportRightButton.gameObject.SetActive(visible);
        }

        // ============================================================
        // PUBLIC API — Button.onClick (ou outros disparos manuais)
        // ============================================================

        public void OnTeleportPressed(int dir)
        {
            if (!_wired) return;
            if (GameManager.Instance != null && !GameManager.Instance.IsScoring) return;
            if (PowerUpManager.Instance == null || !PowerUpManager.Instance.HasTeleport) return;
            if (TeleportController.Instance == null) return;
            TeleportController.Instance.TryTrigger(dir);
        }
    }
}
