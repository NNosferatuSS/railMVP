using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RailSwitchMVP.UI.Widgets
{
    /// <summary>
    /// Barra inferior persistente (5 slots: HOME, STORE, SKINS, RANKS, MORE).
    /// Emite OnNavSelected(index). Slot ativo destaca com cor accent.
    /// </summary>
    public class BottomNavUI : MonoBehaviour
    {
        public enum Slot { Home = 0, Store = 1, Skins = 2, Ranks = 3, More = 4 }

        [SerializeField] private Button homeBtn, storeBtn, skinsBtn, ranksBtn, moreBtn;
        [SerializeField] private Image homeIcon, storeIcon, skinsIcon, ranksIcon, moreIcon;
        [SerializeField] private TMP_Text homeLabel, storeLabel, skinsLabel, ranksLabel, moreLabel;
        [SerializeField] private Color activeColor = new Color(0.13f, 0.89f, 1f);
        [SerializeField] private Color inactiveColor = new Color(1f, 1f, 1f, 0.55f);

        public event Action<Slot> OnNavSelected;

        void Awake()
        {
            Wire(homeBtn, Slot.Home);
            Wire(storeBtn, Slot.Store);
            Wire(skinsBtn, Slot.Skins);
            Wire(ranksBtn, Slot.Ranks);
            Wire(moreBtn, Slot.More);
        }

        void Wire(Button b, Slot s)
        {
            if (b == null) return;
            b.onClick.AddListener(() => Select(s));
        }

        public void Select(Slot slot)
        {
            SetActive(homeIcon,  homeLabel,  slot == Slot.Home);
            SetActive(storeIcon, storeLabel, slot == Slot.Store);
            SetActive(skinsIcon, skinsLabel, slot == Slot.Skins);
            SetActive(ranksIcon, ranksLabel, slot == Slot.Ranks);
            SetActive(moreIcon,  moreLabel,  slot == Slot.More);
            OnNavSelected?.Invoke(slot);
        }

        void SetActive(Image icon, TMP_Text label, bool active)
        {
            var c = active ? activeColor : inactiveColor;
            if (icon != null) icon.color = c;
            if (label != null) label.color = c;
        }
    }
}
