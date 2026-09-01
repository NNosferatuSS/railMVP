using System;
using System.Collections.Generic;
using UnityEngine;
using RailSwitchMVP.UI.Widgets;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Gerencia a troca de screens dentro do Canvas_Meta (opção A: paineis toggláveis).
    /// Um GameObject por tela; ScreenNavigator ativa/desativa conforme o BottomNav.
    /// Estende com screens extras via [Screen] tags no inspector.
    /// </summary>
    public class ScreenNavigator : MonoBehaviour
    {
        public enum Screen { Home, Store, Skins, Ranks, More, Settings, Missions, BattlePass, Profile }

        [System.Serializable]
        public struct ScreenSlot
        {
            public Screen id;
            public GameObject panel;
        }

        [SerializeField] private ScreenSlot[] screens;
        [SerializeField] private BottomNavUI bottomNav;
        [SerializeField] private Screen initial = Screen.Home;

        readonly Dictionary<Screen, GameObject> _map = new Dictionary<Screen, GameObject>();

        public event Action<Screen> OnScreenChanged;
        public Screen Current { get; private set; }

        void Awake()
        {
            _map.Clear();
            foreach (var s in screens)
                if (s.panel != null) _map[s.id] = s.panel;
            if (bottomNav != null) bottomNav.OnNavSelected += HandleNav;
        }

        void Start() => ShowInternal(initial);

        void HandleNav(BottomNavUI.Slot slot)
        {
            switch (slot)
            {
                case BottomNavUI.Slot.Home: ShowInternal(Screen.Home); break;
                case BottomNavUI.Slot.Store: ShowInternal(Screen.Store); break;
                case BottomNavUI.Slot.Skins: ShowInternal(Screen.Skins); break;
                case BottomNavUI.Slot.Ranks: ShowInternal(Screen.Ranks); break;
                case BottomNavUI.Slot.More: ShowInternal(Screen.More); break;
            }
        }

        public void Show(Screen s) => ShowInternal(s);

        void ShowInternal(Screen s)
        {
            foreach (var kv in _map) kv.Value.SetActive(false);
            if (_map.TryGetValue(s, out var panel) && panel != null) panel.SetActive(true);
            Current = s;
            OnScreenChanged?.Invoke(s);
        }
    }
}
