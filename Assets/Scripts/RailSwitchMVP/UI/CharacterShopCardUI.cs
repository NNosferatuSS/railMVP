using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RailSwitchMVP.Meta;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Card visual de um personagem na loja. Agrupa refs (nome, status,
    /// preview, botão de ação) num GameObject; ShopController decide a
    /// lógica de cada estado.
    /// </summary>
    public class CharacterShopCardUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image previewImage;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionText;

        Action _onClick;

        void Awake()
        {
            if (actionButton != null)
                actionButton.onClick.AddListener(HandleClick);
        }

        void OnDestroy()
        {
            if (actionButton != null)
                actionButton.onClick.RemoveListener(HandleClick);
        }

        void HandleClick() => _onClick?.Invoke();

        /// <summary>
        /// Estados:
        /// - equipped → "Equipado" (disabled)
        /// - owned, not equipped → "Equipar" (enabled)
        /// - not owned, canBuy → "Comprar" (enabled)
        /// - not owned, !canBuy → "Insuficiente" (disabled)
        /// </summary>
        public void Bind(CharacterDef def, bool owned, bool equipped, bool canBuy, Action onClick)
        {
            _onClick = onClick;

            if (nameText != null) nameText.text = def.Name;
            if (previewImage != null) previewImage.color = def.Primary;

            string status;
            string action;
            bool enabled;

            if (equipped) { status = "Equipado"; action = "Equipado"; enabled = false; }
            else if (owned) { status = "Desbloqueado"; action = "Equipar"; enabled = true; }
            else if (canBuy) { status = $"{def.Cost} coins"; action = "Comprar"; enabled = true; }
            else { status = $"{def.Cost} coins"; action = "Insuficiente"; enabled = false; }

            if (statusText != null) statusText.text = status;
            if (actionText != null) actionText.text = action;
            if (actionButton != null) actionButton.interactable = enabled;
        }
    }
}
