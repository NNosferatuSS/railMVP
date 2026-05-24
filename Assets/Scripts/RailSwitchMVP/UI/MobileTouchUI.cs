using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Botões on-screen pra usar Active Item (Space) e Teleport (Shift+←/→) em mobile.
    /// Auto-hide baseado em ActiveItemSlot.HasItem e PowerUpManager.HasTeleport —
    /// botões só aparecem quando o input está disponível.
    ///
    /// Keyboard continua funcionando paralelo (ActiveItemInputHandler intacto).
    /// Esses botões são SEMPRE ativos quando há item/teleport — em desktop você
    /// pode escolher escondê-los via hideOnDesktop.
    /// </summary>
    public class MobileTouchUI : MonoBehaviour
    {
        [Header("Use Active Item (equivalente Space)")]
        [SerializeField] private Button useItemButton;
        [Tooltip("Texto opcional dentro do botão — mostra o nome do item (ex: 'TimeFreeze'). " +
            "Se null, ignorado.")]
        [SerializeField] private TMP_Text useItemLabel;

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

            if (useItemButton != null)
                useItemButton.onClick.AddListener(OnUseItemPressed);
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
            if (useItemButton != null) useItemButton.gameObject.SetActive(false);
            if (teleportGroup != null) teleportGroup.SetActive(false);
            else
            {
                if (teleportLeftButton != null) teleportLeftButton.gameObject.SetActive(false);
                if (teleportRightButton != null) teleportRightButton.gameObject.SetActive(false);
            }
        }

        void SubscribeEvents()
        {
            if (ActiveItemSlot.Instance != null)
            {
                ActiveItemSlot.Instance.OnItemAcquired += HandleItemAcquired;
                ActiveItemSlot.Instance.OnItemUsed += HandleItemUsed;
            }
            if (PowerUpManager.Instance != null)
            {
                PowerUpManager.Instance.OnPowerUpTick += HandlePowerUpTick;
                PowerUpManager.Instance.OnPowerUpExpired += HandlePowerUpExpired;
            }
        }

        void UnsubscribeEvents()
        {
            if (ActiveItemSlot.Instance != null)
            {
                ActiveItemSlot.Instance.OnItemAcquired -= HandleItemAcquired;
                ActiveItemSlot.Instance.OnItemUsed -= HandleItemUsed;
            }
            if (PowerUpManager.Instance != null)
            {
                PowerUpManager.Instance.OnPowerUpTick -= HandlePowerUpTick;
                PowerUpManager.Instance.OnPowerUpExpired -= HandlePowerUpExpired;
            }
        }

        void HandleItemAcquired(ActiveItemType type) => SetUseVisible(type != ActiveItemType.None, type);
        void HandleItemUsed(ActiveItemType _) => SetUseVisible(false, ActiveItemType.None);

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
            var item = ActiveItemSlot.Instance != null ? ActiveItemSlot.Instance.HeldItem : ActiveItemType.None;
            SetUseVisible(item != ActiveItemType.None, item);

            bool tele = PowerUpManager.Instance != null && PowerUpManager.Instance.HasTeleport;
            SetTeleportVisible(tele);
        }

        void SetUseVisible(bool visible, ActiveItemType type)
        {
            if (useItemButton != null && useItemButton.gameObject.activeSelf != visible)
                useItemButton.gameObject.SetActive(visible);
            if (useItemLabel != null) useItemLabel.text = visible ? type.ToString() : "";
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

        public void OnUseItemPressed()
        {
            if (!_wired) return;
            if (GameManager.Instance != null && !GameManager.Instance.IsScoring) return;
            if (ActiveItemSlot.Instance != null) ActiveItemSlot.Instance.UseItem();
        }

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
