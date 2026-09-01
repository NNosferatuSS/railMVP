using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RailSwitchMVP.Economy;
using RailSwitchMVP.Meta;
using RailSwitchMVP.UI.Widgets;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Fica no Canvas_Meta root. No Awake acha os paineis Screen_* que o
    /// BuildMetaCanvas cria, anexa os controllers legados e preenche seus
    /// [SerializeField] privados por reflection. Campos sem correspondência
    /// ficam null — os controllers legados toleram isso.
    ///
    /// Também escuta ScreenNavigator.OnScreenChanged pra dar Refresh/Open nos
    /// sub-controllers quando o usuário navega pelo BottomNav, e liga os
    /// botões "Btn_Back" de cada tela de volta pra Home.
    /// </summary>
    public class MetaCanvasBinder : MonoBehaviour
    {
        readonly Dictionary<string, Transform> _screens = new Dictionary<string, Transform>();
        readonly Dictionary<string, GameObject> _subPanels = new Dictionary<string, GameObject>();
        Transform _topBar;
        ScreenNavigator _nav;
        ChipUI _coinsChip, _gemsChip;
        bool _currenciesSeeded;

        void Awake()
        {
            IndexScreens();
            _nav = GetComponent<ScreenNavigator>();

            if (_topBar != null)
            {
                _coinsChip = FindIn<ChipUI>(_topBar, "Chip_Coins");
                _gemsChip  = FindIn<ChipUI>(_topBar, "Chip_Gems");
            }

            WireHome();
            WireStore();
            WireLeaderboard();
            WireProfile();
            WireBackButtons();

            if (_nav != null) _nav.OnScreenChanged += OnScreenChanged;
        }

        void Start()
        {
            // Stats do Profile não têm controller próprio — preenche direto.
            RefreshProfileStats();

            // Assinar só aqui: em Awake os managers singleton podem não ter
            // rodado o próprio Awake ainda (ordem não é garantida).
            var pdm = PlayerDataManager.Instance;
            if (pdm != null) pdm.OnCoinsChanged += HandleCoinsChanged;

            var cm = CurrencyManager.Instance;
            if (cm != null) cm.OnCurrencyChanged += HandleCurrencyChanged;

            RefreshCurrencies();
            _currenciesSeeded = true;
        }

        void OnDestroy()
        {
            if (_nav != null) _nav.OnScreenChanged -= OnScreenChanged;

            var pdm = PlayerDataManager.Instance;
            if (pdm != null) pdm.OnCoinsChanged -= HandleCoinsChanged;

            var cm = CurrencyManager.Instance;
            if (cm != null) cm.OnCurrencyChanged -= HandleCurrencyChanged;
        }

        // ---------- moedas (chips do TopBar) ----------

        void HandleCoinsChanged(int total)
        {
            if (_coinsChip == null) return;
            _coinsChip.SetValue(total);
            PopChip(_coinsChip);
        }

        void HandleCurrencyChanged(CurrencyType type, int total)
        {
            if (type != CurrencyType.Gems || _gemsChip == null) return;
            _gemsChip.SetValue(total);
            PopChip(_gemsChip);
        }

        void RefreshCurrencies()
        {
            var pdm = PlayerDataManager.Instance;
            if (_coinsChip != null && pdm != null) _coinsChip.SetValue(pdm.Coins);

            var cm = CurrencyManager.Instance;
            if (_gemsChip != null && cm != null)
                _gemsChip.SetValue(cm.GetBalance(CurrencyType.Gems));
        }

        void PopChip(ChipUI chip)
        {
            // O seed inicial não é "o saldo mudou" — não anima.
            if (!_currenciesSeeded || chip == null || !isActiveAndEnabled) return;
            chip.transform.localScale = Vector3.one;
            StartCoroutine(UITween.PunchScale(chip.transform, 0.14f, 0.20f));
        }

        void IndexScreens()
        {
            foreach (var rt in GetComponentsInChildren<RectTransform>(true))
            {
                if (rt.name.StartsWith("Screen_") && !_screens.ContainsKey(rt.name))
                    _screens[rt.name] = rt;
                else if (rt.name == "TopBar") _topBar = rt;
            }
        }

        void OnScreenChanged(ScreenNavigator.Screen s)
        {
            if (s == ScreenNavigator.Screen.Store && Screen("Store", out var store))
            {
                var shop = store.GetComponent<ShopController>();
                if (shop != null) Invoke(shop, "Refresh");
            }
            if (s == ScreenNavigator.Screen.Ranks && Screen("Leaderboard", out var lb))
            {
                var c = lb.GetComponent<LeaderboardPanelController>();
                if (c != null) c.Open();
            }
            if (s == ScreenNavigator.Screen.Profile && Screen("Profile", out var pr))
            {
                var c = pr.GetComponent<ProfilePanelController>();
                if (c != null) c.Open();
                RefreshProfileStats();
            }
        }

        // ---------- WIRE ----------

        void WireHome()
        {
            if (!Screen("Home", out var home)) return;
            var c = Ensure<HomeScreenController>(home);

            SetField(c, "playButton",            FindIn<Button>(home, "Btn_StartRun"));
            SetField(c, "dailyChallengeButton",  FindIn<Button>(home, "Btn_PlayDaily"));
            SetField(c, "bestDistanceText",      FindIn<TMP_Text>(home, "BestLine"));
            SetField(c, "dailyChallengeText",    FindIn<TMP_Text>(home, "Counter"));

            // coinsText/gemsText NÃO são wirados de propósito: o controller escreve
            // "Coins: 9638" neles, e o chip é desenhado pra número puro — a frase
            // estourava a largura e vazava por cima do ícone. Quem alimenta os
            // chips é RefreshCurrencies() abaixo, via ChipUI.SetValue.

            // Missões vivem na tela de Missions, mas quem faz o Bind é o
            // HomeScreenController — GetComponentsInChildren(true) acha mesmo
            // com o painel desativado.
            if (Screen("Missions", out var missions))
            {
                var entries = new List<MissionEntryUI>();
                for (int i = 0; i < MissionTracker.DailySlots; i++)
                {
                    var row = FindIn<MissionEntryUI>(missions, "Mission_" + i);
                    if (row != null) entries.Add(row);
                }
                if (entries.Count > 0) SetField(c, "dailyEntries", entries.ToArray());

                SetField(c, "chestButton",     FindIn<Button>(missions, "ChestButton"));
                SetField(c, "chestButtonText", FindLabelOf(missions, "ChestButton"));
            }

            // Cross-refs pros outros controllers.
            if (Screen("Store", out var store))
                SetField(c, "shopController", Ensure<ShopController>(store));
            if (Screen("Leaderboard", out var lb))
                SetField(c, "leaderboardController", Ensure<LeaderboardPanelController>(lb));
            if (Screen("Profile", out var pr))
                SetField(c, "profileController", Ensure<ProfilePanelController>(pr));
        }

        void WireStore()
        {
            if (!Screen("Store", out var store)) return;
            var c = Ensure<ShopController>(store);
            // shopPanel tem que ser um filho interno, não o Screen_ inteiro —
            // senão Open/Close do controller briga com o ScreenNavigator.
            var inner = InnerPanel(store, "ShopPanelInner");
            SetField(c, "shopPanel", inner);
        }

        void WireLeaderboard()
        {
            if (!Screen("Leaderboard", out var lb)) return;
            var c = Ensure<LeaderboardPanelController>(lb);
            SetField(c, "leaderboardPanel",  InnerPanel(lb, "LeaderboardPanelInner"));
            SetField(c, "entriesContainer",  FindIn<Transform>(lb, "EntriesContainer"));
            SetField(c, "myRankBannerText",  FindIn<TMP_Text>(lb, "Value"));
            SetField(c, "headerText",        FindIn<TMP_Text>(lb, "Title"));

            // Tabs DAILY | GLOBAL → SwitchTab do controller.
            var tabs = lb.GetComponentInChildren<TabsUI>(true);
            if (tabs != null)
            {
                tabs.OnTabSelected += idx =>
                {
                    Invoke(c, idx == 0 ? "OnDailyTabClicked" : "OnGlobalTabClicked");
                };
            }
        }

        void WireProfile()
        {
            if (!Screen("Profile", out var pr)) return;
            var c = Ensure<ProfilePanelController>(pr);
            SetField(c, "profilePanel", InnerPanel(pr, "ProfilePanelInner"));
        }

        void WireBackButtons()
        {
            if (_nav == null) return;
            foreach (var kv in _screens)
            {
                var back = FindIn<Button>(kv.Value, "Btn_Back");
                if (back == null) continue;
                back.onClick.RemoveAllListeners();
                back.onClick.AddListener(() => _nav.Show(ScreenNavigator.Screen.Home));
            }
        }

        // ---------- stats do Profile (sem controller próprio) ----------

        void RefreshProfileStats()
        {
            if (!Screen("Profile", out var pr)) return;
            var pdm = PlayerDataManager.Instance;
            if (pdm == null) return;

            SetStat(pr, "Stat_BESTDISTANCE", pdm.BestDistance.ToString());
            SetStat(pr, "Stat_TOTALRUNS",    pdm.TotalRuns.ToString());
            SetStat(pr, "Stat_COINSEARNED",  pdm.Coins.ToString());
            SetStat(pr, "Stat_BESTTIER",     pdm.BestTier.ToString());

            var name = FindIn<TMP_Text>(pr, "Name");
            if (name != null) name.text = pdm.PlayerName;
            var level = FindIn<TMP_Text>(pr, "Level");
            if (level != null) level.text = $"Lv. {pdm.AccountLevel}";
        }

        void SetStat(Transform root, string cardName, string value)
        {
            var card = FindIn<Transform>(root, cardName);
            if (card == null) return;
            var v = card.Find("Value");
            if (v == null) return;
            var tmp = v.GetComponent<TMP_Text>();
            if (tmp != null) tmp.text = value;
        }

        // ---------- helpers ----------

        bool Screen(string shortName, out Transform t)
            => _screens.TryGetValue("Screen_" + shortName, out t) && t != null;

        static T Ensure<T>(Transform host) where T : Component
        {
            var c = host.GetComponent<T>();
            return c != null ? c : host.gameObject.AddComponent<T>();
        }

        GameObject InnerPanel(Transform parent, string name)
        {
            if (_subPanels.TryGetValue(name, out var cached) && cached != null) return cached;
            var existing = parent.Find(name);
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            }
            go.SetActive(true);
            _subPanels[name] = go;
            return go;
        }

        static T FindIn<T>(Transform root, string name) where T : class
        {
            if (root == null) return null;
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name != name) continue;
                if (typeof(T) == typeof(Transform)) return tr as T;
                var c = tr.GetComponent(typeof(T));
                if (c != null) return c as T;
            }
            return null;
        }

        static TMP_Text FindLabelOf(Transform root, string buttonName)
        {
            var b = FindIn<Transform>(root, buttonName);
            if (b == null) return null;
            var lbl = b.Find("Label");
            return lbl != null ? lbl.GetComponent<TMP_Text>() : null;
        }

        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static void SetField(object target, string name, object value)
        {
            if (target == null || value == null) return;
            var t = target.GetType();
            while (t != null)
            {
                var f = t.GetField(name, Flags);
                if (f != null)
                {
                    if (f.FieldType.IsInstanceOfType(value)) f.SetValue(target, value);
                    return;
                }
                t = t.BaseType;
            }
        }

        static void Invoke(object target, string method)
        {
            if (target == null) return;
            var t = target.GetType();
            while (t != null)
            {
                var m = t.GetMethod(method, Flags);
                if (m != null) { m.Invoke(target, null); return; }
                t = t.BaseType;
            }
        }
    }
}
