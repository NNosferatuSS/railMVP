using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RailSwitchMVP.Meta;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Loja de personagens (spec §6). Panel sobreposto na HomeScene. 3 cards
    /// (um por CharacterDef) com lógica de owned/equipped/buy. Compra mostra
    /// popup de confirmação antes de debitar coins.
    ///
    /// Auto-equip ao comprar: a spec §6.3 sugere isso, e UX comum em mobile.
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        [Header("Panel & cards")]
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private CharacterShopCardUI[] cards = new CharacterShopCardUI[3];
        [SerializeField] private Button closeButton;

        [Header("Confirm purchase popup")]
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private TMP_Text confirmText;
        [SerializeField] private Button confirmYesButton;
        [SerializeField] private Button confirmNoButton;

        int _pendingBuyIndex = -1;

        void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (confirmYesButton != null) confirmYesButton.onClick.AddListener(ConfirmBuy);
            if (confirmNoButton != null) confirmNoButton.onClick.AddListener(CancelBuy);

            if (shopPanel != null) shopPanel.SetActive(false);
            if (confirmPanel != null) confirmPanel.SetActive(false);
        }

        void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (confirmYesButton != null) confirmYesButton.onClick.RemoveListener(ConfirmBuy);
            if (confirmNoButton != null) confirmNoButton.onClick.RemoveListener(CancelBuy);
        }

        void OnEnable()
        {
            var pdm = PlayerDataManager.Instance;
            if (pdm != null)
            {
                pdm.OnCoinsChanged += HandleCoinsChanged;
                pdm.OnEquippedCharChanged += HandleEquippedChanged;
            }
        }

        void OnDisable()
        {
            var pdm = PlayerDataManager.Instance;
            if (pdm != null)
            {
                pdm.OnCoinsChanged -= HandleCoinsChanged;
                pdm.OnEquippedCharChanged -= HandleEquippedChanged;
            }
        }

        public void Open()
        {
            if (shopPanel != null) shopPanel.SetActive(true);
            if (confirmPanel != null) confirmPanel.SetActive(false);
            Refresh();
        }

        public void Close()
        {
            if (confirmPanel != null) confirmPanel.SetActive(false);
            if (shopPanel != null) shopPanel.SetActive(false);
            _pendingBuyIndex = -1;
        }

        void HandleCoinsChanged(int total) { if (shopPanel != null && shopPanel.activeSelf) Refresh(); }
        void HandleEquippedChanged(int idx) { if (shopPanel != null && shopPanel.activeSelf) Refresh(); }

        void Refresh()
        {
            var pdm = PlayerDataManager.Instance;
            if (pdm == null) return;

            for (int i = 0; i < cards.Length && i < CharacterCatalog.Count; i++)
            {
                if (cards[i] == null) continue;
                var def = CharacterCatalog.Get(i);
                bool owned = pdm.IsCharacterOwned(i);
                bool equipped = owned && pdm.EquippedChar == i;
                bool canBuy = !owned && pdm.Coins >= def.Cost;
                int index = i;
                cards[i].Bind(def, owned, equipped, canBuy, () => OnCardClick(index));
            }
        }

        void OnCardClick(int index)
        {
            var pdm = PlayerDataManager.Instance;
            if (pdm == null) return;

            if (pdm.IsCharacterOwned(index))
            {
                if (pdm.EquippedChar == index) return; // já equipado, no-op
                pdm.EquipCharacter(index);
                pdm.Save();
                // Refresh chega via OnEquippedCharChanged.
            }
            else
            {
                _pendingBuyIndex = index;
                var def = CharacterCatalog.Get(index);
                if (confirmText != null)
                    confirmText.text = $"Comprar {def.Name} por {def.Cost} coins?";
                if (confirmPanel != null) confirmPanel.SetActive(true);
            }
        }

        void ConfirmBuy()
        {
            if (_pendingBuyIndex < 0) { CancelBuy(); return; }
            var pdm = PlayerDataManager.Instance;
            if (pdm == null) { CancelBuy(); return; }

            var def = CharacterCatalog.Get(_pendingBuyIndex);
            if (pdm.SpendCoins(def.Cost))
            {
                pdm.UnlockCharacter(_pendingBuyIndex);
                pdm.EquipCharacter(_pendingBuyIndex);
                pdm.Save();
                Debug.Log($"[Shop] Bought {def.Name} for {def.Cost} coins.");
            }
            else
            {
                Debug.LogWarning($"[Shop] SpendCoins falhou pra {def.Name} (coins={pdm.Coins} cost={def.Cost})");
            }
            CancelBuy();
            Refresh();
        }

        void CancelBuy()
        {
            _pendingBuyIndex = -1;
            if (confirmPanel != null) confirmPanel.SetActive(false);
        }
    }
}
