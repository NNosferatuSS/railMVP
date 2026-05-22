using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using RailSwitchMVP.Config;

namespace RailSwitchMVP.Core
{
    public enum HazardKind { None, Lethal, Barrier, SpeedUp, LaneSwap, Vortex }
    public enum SpawnLocation { CriticalOnly, DecoyOnly, Both }

    public struct HazardResolution
    {
        public GameObject prefab;
        public HazardKind kind;
        public static HazardResolution None => new HazardResolution { prefab = null, kind = HazardKind.None };
    }

    /// <summary>
    /// Override in-runtime do spawn de hazards/power-ups do ProceduralRailGenerator.
    /// Separado do DebugPanelController (F1) — toggle com F2, ancorado à direita.
    /// Quando MasterEnabled = OFF, gerador usa DifficultyTier normalmente (zero impacto).
    /// Quando ON, este controller dita chances e localização (critical/decoy/both).
    /// Editor / Debug builds apenas.
    /// </summary>
    public class SpawnOverrideController : MonoBehaviour
    {
        public static SpawnOverrideController Instance { get; private set; }

        [System.Serializable]
        public class Entry
        {
            public bool enabled = true;
            [Range(0f, 1f)] public float chance = 0.1f;
            public SpawnLocation location = SpawnLocation.DecoyOnly;
        }

        [Tooltip("Quando true, só funciona em Editor ou Debug Build.")]
        public bool restrictToDebugBuilds = true;

        [Tooltip("Tamanho do painel em pixels.")]
        public Vector2 panelSize = new Vector2(320f, 640f);

        [Tooltip("Offset do painel a partir do anchor direito.")]
        public Vector2 panelPosition = new Vector2(10f, 50f);

        [Header("Master")]
        [Tooltip("Quando false, o gerador usa tier config normal (sem override).")]
        public bool masterEnabled = false;

        [Range(0f, 5f)]
        [Tooltip("Multiplicador global aplicado em cima de todas as chances.")]
        public float globalMultiplier = 1f;

        [Header("Tier lock")]
        [Tooltip("Quando true, o DifficultyManager fica permanentemente no tier abaixo " +
            "(não avança com distância). Útil pra estudar 1 tier por vez.")]
        public bool tierLockEnabled = false;

        [Tooltip("Índice do tier a travar (0-based). Clamped ao range do DifficultyConfig.")]
        public int lockedTierIndex = 0;

        [Header("Streaming (rowsAhead override)")]
        [Tooltip("Quando true, sobrescreve config.rowsAhead. Útil pra ver mudanças " +
            "dos sliders aparecerem em poucos tiles. RailManager despawna o excedente " +
            "automaticamente e re-seeds o gerador.")]
        public bool rowsAheadOverrideEnabled = false;

        [Range(1, 20)]
        [Tooltip("Quantas rows à frente do player o RailManager mantém quando o override está ON.")]
        public int rowsAheadOverride = 3;

        [Header("Hazards (defaults — clique 'Snapshot from tier' pra copiar do ativo)")]
        public Entry lethal   = new Entry { enabled = true, chance = 0.10f, location = SpawnLocation.DecoyOnly };
        public Entry barrier  = new Entry { enabled = true, chance = 0.10f, location = SpawnLocation.DecoyOnly };
        public Entry speedUp  = new Entry { enabled = true, chance = 0.05f, location = SpawnLocation.DecoyOnly };
        public Entry laneSwap = new Entry { enabled = true, chance = 0.05f, location = SpawnLocation.DecoyOnly };
        public Entry vortex   = new Entry { enabled = true, chance = 0.05f, location = SpawnLocation.DecoyOnly };

        private readonly Dictionary<GameObject, Entry> _powerUpEntries = new Dictionary<GameObject, Entry>();
        private readonly List<GameObject> _powerUpOrder = new List<GameObject>();

        private string _soloKey;

        private bool _show;
        private Vector2 _scroll;
        private GUIStyle _sectionStyle, _hintStyle, _masterOnStyle;

        bool ShouldRespond => !restrictToDebugBuilds || Application.isEditor || Debug.isDebugBuild;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            var gen = Object.FindFirstObjectByType<ProceduralRailGenerator>();
            if (gen != null && gen.PowerUpPrefabs != null)
                EnsurePowerUpEntries(gen.PowerUpPrefabs);
        }

        void Update()
        {
            if (!ShouldRespond) return;
            var kb = Keyboard.current;
            if (kb != null && kb.f2Key.wasPressedThisFrame) _show = !_show;
        }

        // ========================================================
        // PUBLIC API — chamado pelo RailManager / ProceduralRailGenerator
        // ========================================================

        public int GetEffectiveRowsAhead(int defaultValue)
        {
            return rowsAheadOverrideEnabled ? Mathf.Max(1, rowsAheadOverride) : defaultValue;
        }

        public bool TryGetLockedTier(int tierCount, out int clampedIndex)
        {
            if (!tierLockEnabled || tierCount <= 0)
            {
                clampedIndex = -1;
                return false;
            }
            clampedIndex = Mathf.Clamp(lockedTierIndex, 0, tierCount - 1);
            return true;
        }

        public HazardResolution ResolveHazard(
            bool isOnCriticalPath,
            DifficultyTier tier,
            GameObject lethalPrefab,
            GameObject barrierPrefab,
            GameObject speedUpPrefab,
            GameObject laneSwapPrefab,
            GameObject vortexPrefab)
        {
            if (!masterEnabled)
            {
                if (isOnCriticalPath) return HazardResolution.None;
                if (lethalPrefab   != null && tier.obstacleChanceOnDecoy    > 0f && Random.value < tier.obstacleChanceOnDecoy)
                    return new HazardResolution { prefab = lethalPrefab,   kind = HazardKind.Lethal };
                if (barrierPrefab  != null && tier.barrierChanceOnDecoy     > 0f && Random.value < tier.barrierChanceOnDecoy)
                    return new HazardResolution { prefab = barrierPrefab,  kind = HazardKind.Barrier };
                if (speedUpPrefab  != null && tier.speedUpZoneChanceOnDecoy > 0f && Random.value < tier.speedUpZoneChanceOnDecoy)
                    return new HazardResolution { prefab = speedUpPrefab,  kind = HazardKind.SpeedUp };
                if (laneSwapPrefab != null && tier.laneSwapChanceOnDecoy    > 0f && Random.value < tier.laneSwapChanceOnDecoy)
                    return new HazardResolution { prefab = laneSwapPrefab, kind = HazardKind.LaneSwap };
                if (vortexPrefab   != null && tier.vortexChanceOnDecoy      > 0f && Random.value < tier.vortexChanceOnDecoy)
                    return new HazardResolution { prefab = vortexPrefab,   kind = HazardKind.Vortex };
                return HazardResolution.None;
            }

            if (TryRollHazard("hz_lethal",   lethal,   isOnCriticalPath, lethalPrefab,   HazardKind.Lethal,   out var r1)) return r1;
            if (TryRollHazard("hz_barrier",  barrier,  isOnCriticalPath, barrierPrefab,  HazardKind.Barrier,  out var r2)) return r2;
            if (TryRollHazard("hz_speedup",  speedUp,  isOnCriticalPath, speedUpPrefab,  HazardKind.SpeedUp,  out var r3)) return r3;
            if (TryRollHazard("hz_laneswap", laneSwap, isOnCriticalPath, laneSwapPrefab, HazardKind.LaneSwap, out var r4)) return r4;
            if (TryRollHazard("hz_vortex",   vortex,   isOnCriticalPath, vortexPrefab,   HazardKind.Vortex,   out var r5)) return r5;
            return HazardResolution.None;
        }

        bool TryRollHazard(string key, Entry e, bool isCritical, GameObject prefab, HazardKind kind, out HazardResolution result)
        {
            result = HazardResolution.None;
            if (prefab == null || !e.enabled) return false;
            if (_soloKey != null && _soloKey != key) return false;
            if (!LocationAllows(e.location, isCritical)) return false;
            float chance = Mathf.Clamp01(e.chance * globalMultiplier);
            if (chance <= 0f || Random.value >= chance) return false;
            result = new HazardResolution { prefab = prefab, kind = kind };
            return true;
        }

        public GameObject ResolvePowerUpPrefab(bool isOnCriticalPath, DifficultyTier tier, GameObject[] prefabs)
        {
            if (prefabs == null || prefabs.Length == 0) return null;

            if (!masterEnabled)
            {
                float chance = isOnCriticalPath ? tier.powerUpChanceOnCritical : tier.powerUpChanceOnDecoy;
                if (chance <= 0f || Random.value >= chance) return null;
                int idx = Random.Range(0, prefabs.Length);
                return prefabs[idx];
            }

            EnsurePowerUpEntries(prefabs);

            foreach (var go in _powerUpOrder)
            {
                if (go == null) continue;
                var e = _powerUpEntries[go];
                if (!e.enabled) continue;
                string key = PowerUpKey(go);
                if (_soloKey != null && _soloKey != key) continue;
                if (!LocationAllows(e.location, isOnCriticalPath)) continue;
                float chance = Mathf.Clamp01(e.chance * globalMultiplier);
                if (chance <= 0f) continue;
                if (Random.value < chance) return go;
            }
            return null;
        }

        void EnsurePowerUpEntries(GameObject[] prefabs)
        {
            foreach (var go in prefabs)
            {
                if (go == null || _powerUpEntries.ContainsKey(go)) continue;
                _powerUpEntries[go] = new Entry { enabled = true, chance = 0.05f, location = SpawnLocation.Both };
                _powerUpOrder.Add(go);
            }
        }

        static bool LocationAllows(SpawnLocation loc, bool isCritical)
        {
            return loc == SpawnLocation.Both
                || (loc == SpawnLocation.CriticalOnly && isCritical)
                || (loc == SpawnLocation.DecoyOnly && !isCritical);
        }

        static string PowerUpKey(GameObject go) => "pu_" + (go != null ? go.name : "null");

        public void SnapshotFromTier(DifficultyTier tier)
        {
            lethal.chance   = tier.obstacleChanceOnDecoy;
            barrier.chance  = tier.barrierChanceOnDecoy;
            speedUp.chance  = tier.speedUpZoneChanceOnDecoy;
            laneSwap.chance = tier.laneSwapChanceOnDecoy;
            vortex.chance   = tier.vortexChanceOnDecoy;
            lethal.location = barrier.location = speedUp.location = laneSwap.location = vortex.location = SpawnLocation.DecoyOnly;

            float avgPu = (tier.powerUpChanceOnCritical + tier.powerUpChanceOnDecoy) * 0.5f;
            foreach (var go in _powerUpOrder)
            {
                var e = _powerUpEntries[go];
                e.chance = avgPu;
                e.location = SpawnLocation.Both;
            }
        }

        // ========================================================
        // OnGUI
        // ========================================================

        void OnGUI()
        {
            if (!_show || !ShouldRespond) return;
            EnsureStyles();

            float x = Screen.width - panelSize.x - panelPosition.x;
            var rect = new Rect(x, panelPosition.y, panelSize.x, panelSize.y);
            GUI.Box(rect, "SPAWN OVERRIDE (F2)");

            GUILayout.BeginArea(new Rect(rect.x + 6, rect.y + 22, rect.width - 12, rect.height - 28));
            _scroll = GUILayout.BeginScrollView(_scroll);

            // Auto-follow (mesmo toggle do F1 — handy ter aqui pra playtest one-handed).
            var auto = AutoCriticalFollower.Instance;
            if (auto != null)
                auto.DebugForceActive = GUILayout.Toggle(auto.DebugForceActive, " Auto-follow critical path");
            else
                GUILayout.Label("(AutoCriticalFollower not in scene)", _hintStyle);

            GUILayout.Space(4);

            var masterStyle = masterEnabled ? _masterOnStyle : GUI.skin.toggle;
            masterEnabled = GUILayout.Toggle(masterEnabled,
                masterEnabled ? "Master: ON (override active)" : "Master: OFF (using tier config)",
                masterStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Global mult:", GUILayout.Width(80));
            globalMultiplier = GUILayout.HorizontalSlider(globalMultiplier, 0f, 5f);
            GUILayout.Label(globalMultiplier.ToString("0.00") + "x", GUILayout.Width(45));
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Snapshot from current tier"))
            {
                var mgr = DifficultyManager.Instance;
                if (mgr != null) SnapshotFromTier(mgr.CurrentTier);
            }

            // Tier lock
            GUILayout.Space(4);
            var dm = DifficultyManager.Instance;
            int tierCount = (dm != null && dm.Config != null && dm.Config.tiers != null) ? dm.Config.tiers.Count : 0;
            int currentIdx = dm != null ? dm.CurrentTierIndex : -1;
            tierLockEnabled = GUILayout.Toggle(tierLockEnabled,
                tierLockEnabled
                    ? $"Tier lock: ON → tier {lockedTierIndex} (current: {currentIdx})"
                    : $"Tier lock: OFF (auto-advance, current: {currentIdx})");
            if (tierLockEnabled && tierCount > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("  tier:", GUILayout.Width(60));
                float tv = GUILayout.HorizontalSlider(lockedTierIndex, 0f, tierCount - 1);
                lockedTierIndex = Mathf.Clamp(Mathf.RoundToInt(tv), 0, tierCount - 1);
                GUILayout.Label($"{lockedTierIndex}/{tierCount - 1}", GUILayout.Width(45));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4);
            rowsAheadOverrideEnabled = GUILayout.Toggle(rowsAheadOverrideEnabled,
                rowsAheadOverrideEnabled
                    ? $"rowsAhead override: ON ({rowsAheadOverride})"
                    : "rowsAhead override: OFF (using config)");
            if (rowsAheadOverrideEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("  ahead:", GUILayout.Width(60));
                float v = GUILayout.HorizontalSlider(rowsAheadOverride, 1f, 20f);
                rowsAheadOverride = Mathf.RoundToInt(v);
                GUILayout.Label(rowsAheadOverride.ToString(), GUILayout.Width(30));
                GUILayout.EndHorizontal();
            }

            if (_soloKey != null && GUILayout.Button($"Clear solo ({_soloKey})"))
                _soloKey = null;

            GUILayout.Space(6);
            GUILayout.Label("HAZARDS", _sectionStyle);
            DrawEntry("Lethal",   "hz_lethal",   lethal);
            DrawEntry("Barrier",  "hz_barrier",  barrier);
            DrawEntry("SpeedUp",  "hz_speedup",  speedUp);
            DrawEntry("LaneSwap", "hz_laneswap", laneSwap);
            DrawEntry("Vortex",   "hz_vortex",   vortex);

            GUILayout.Space(6);
            GUILayout.Label("POWER-UPS", _sectionStyle);
            if (_powerUpOrder.Count == 0)
                GUILayout.Label("(aguardando geração popular a lista...)", _hintStyle);
            foreach (var go in _powerUpOrder)
            {
                if (go == null) continue;
                DrawEntry(go.name, PowerUpKey(go), _powerUpEntries[go]);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawEntry(string label, string key, Entry e)
        {
            GUILayout.BeginHorizontal();
            e.enabled = GUILayout.Toggle(e.enabled, label, GUILayout.Width(170));
            bool isSolo = _soloKey == key;
            if (GUILayout.Button(isSolo ? "★solo" : "solo", GUILayout.Width(50)))
                _soloKey = isSolo ? null : key;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("  chance:", GUILayout.Width(60));
            e.chance = GUILayout.HorizontalSlider(e.chance, 0f, 1f);
            GUILayout.Label((e.chance * 100f).ToString("0") + "%", GUILayout.Width(40));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("  loc:", GUILayout.Width(40));
            if (GUILayout.Toggle(e.location == SpawnLocation.CriticalOnly, "Crit",  GUILayout.Width(55)) && e.location != SpawnLocation.CriticalOnly) e.location = SpawnLocation.CriticalOnly;
            if (GUILayout.Toggle(e.location == SpawnLocation.DecoyOnly,    "Decoy", GUILayout.Width(60)) && e.location != SpawnLocation.DecoyOnly)    e.location = SpawnLocation.DecoyOnly;
            if (GUILayout.Toggle(e.location == SpawnLocation.Both,         "Both",  GUILayout.Width(55)) && e.location != SpawnLocation.Both)         e.location = SpawnLocation.Both;
            GUILayout.EndHorizontal();
        }

        void EnsureStyles()
        {
            if (_sectionStyle == null)
                _sectionStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Italic };
                _hintStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            }
            if (_masterOnStyle == null)
            {
                _masterOnStyle = new GUIStyle(GUI.skin.toggle) { fontStyle = FontStyle.Bold };
                _masterOnStyle.normal.textColor = new Color(0.4f, 1f, 0.4f);
                _masterOnStyle.onNormal.textColor = new Color(0.4f, 1f, 0.4f);
            }
        }
    }
}
