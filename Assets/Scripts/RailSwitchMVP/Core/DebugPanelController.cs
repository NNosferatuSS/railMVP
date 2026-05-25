using UnityEngine;
using UnityEngine.InputSystem;
using RailSwitchMVP.Collectibles;
using RailSwitchMVP.Meta;
using RailSwitchMVP.Player;
using RailSwitchMVP.Track;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Painel de debug OnGUI pra testar power-ups, debuffs e cenários
    /// rapidamente sem precisar coletar pickups na cena. Toggle com F1.
    /// Auto-restringe a Editor / Debug builds (esconde no release).
    ///
    /// Cresce conforme novos power-ups/obstáculos entram — adicione um botão
    /// pra cada Grant... no PowerUpManager ou cada "trigger" útil.
    /// </summary>
    public class DebugPanelController : MonoBehaviour
    {
        [Tooltip("Quando true, só funciona em Editor ou Debug Build (recomendado).")]
        public bool restrictToDebugBuilds = true;

        public enum PanelAnchor { Left, Right }

        [Tooltip("Lado da tela onde o painel ancora. Default Right pra não tampar HUD top-left.")]
        public PanelAnchor anchor = PanelAnchor.Right;

        [Tooltip("Tamanho do painel em pixels.")]
        public Vector2 panelSize = new Vector2(280f, 600f);

        [Tooltip("Offset do painel a partir do anchor. " +
            "Left anchor: x = panelPosition.x. " +
            "Right anchor: x = Screen.width - panelSize.x - panelPosition.x.")]
        public Vector2 panelPosition = new Vector2(10f, 50f);

        [Header("Debug spawn — pickup prefabs (arraste pro botão Spawn funcionar)")]
        [SerializeField] private GameObject shieldPickupPrefab;
        [SerializeField] private GameObject slowDownPickupPrefab;
        [SerializeField] private GameObject magnetPickupPrefab;
        [SerializeField] private GameObject difficultyResetPickupPrefab;
        [SerializeField] private GameObject doubleCoinsPickupPrefab;
        [SerializeField] private GameObject ghostPickupPrefab;
        [SerializeField] private GameObject lanePreviewPickupPrefab;
        [SerializeField] private GameObject coinRadarPickupPrefab;
        [SerializeField] private GameObject teleportPickupPrefab;
        [SerializeField] private GameObject autoFollowPickupPrefab;
        [SerializeField] private GameObject timeFreezePickupPrefab;

        [Header("Debug spawn — hazard prefabs")]
        [SerializeField] private GameObject lethalObstaclePrefab;
        [SerializeField] private GameObject barrierObstaclePrefab;
        [SerializeField] private GameObject speedUpZonePrefab;
        [SerializeField] private GameObject laneSwapObstaclePrefab;
        [SerializeField] private GameObject vortexObstaclePrefab;

        [Tooltip("Quantas rows à frente do player o prefab é spawnado nos botões Spawn.")]
        [SerializeField] private int debugSpawnRowsAhead = 2;

        [Tooltip("Multiplicador aplicado em TODOS os elementos dentro do painel (fontes + altura " +
            "implícita de botões). 1.0 = tamanho original. 1.25 = 25% maior (default mobile).")]
        [Range(0.8f, 2.5f)]
        [SerializeField] private float uiScale = 1.25f;

        private bool _show;
        private Vector2 _scroll;
        private GUIStyle _sectionStyle;
        private GUIStyle _hintStyle;

        // Originais do GUI.skin antes do scale — restaurados ao fim de OnGUI
        // pra não poluir outros OnGUI da app (ex: SpawnOverrideController).
        private bool _skinOriginalsCaptured;
        private int _origButtonFont, _origLabelFont, _origToggleFont, _origTextFieldFont, _origBoxFont;

        bool ShouldRespond => !restrictToDebugBuilds || Application.isEditor || Debug.isDebugBuild;

        void Update()
        {
            if (!ShouldRespond) return;

            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame)
                _show = !_show;
        }

        /// <summary>
        /// Toggle público pra ser ligado em UI Button (Android, onde F1 não existe).
        /// Respeita restrictToDebugBuilds — no-op em release.
        /// </summary>
        public void Toggle()
        {
            if (!ShouldRespond) return;
            _show = !_show;
        }

        void OnGUI()
        {
            if (!_show || !ShouldRespond) return;

            PushScaledSkin();
            try
            {
                DrawPanel();
            }
            finally
            {
                PopScaledSkin();
            }
        }

        void DrawPanel()
        {
            EnsureStyles();

            float x = anchor == PanelAnchor.Left
                ? panelPosition.x
                : Screen.width - panelSize.x - panelPosition.x;
            var rect = new Rect(x, panelPosition.y, panelSize.x, panelSize.y);
            GUI.Box(rect, "DEBUG (F1 toggle)");

            GUILayout.BeginArea(new Rect(rect.x + 6, rect.y + 22, rect.width - 12, rect.height - 28));
            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawAutoTestSection();
            GUILayout.Space(6);
            DrawPlayerDataSection();
            GUILayout.Space(6);
            DrawCharactersSection();
            GUILayout.Space(6);
            DrawDailyLoginSection();
            GUILayout.Space(6);
            DrawDailyChallengeSection();
            GUILayout.Space(6);
            DrawAuthSection();
            GUILayout.Space(6);
            DrawAdsSection();
            GUILayout.Space(6);
            DrawMissionsSection();
            GUILayout.Space(6);
            DrawActiveItemSection();
            GUILayout.Space(6);
            DrawPowerUpSection();
            GUILayout.Space(6);
            DrawDifficultySection();
            GUILayout.Space(6);
            DrawCoinsSection();
            GUILayout.Space(6);
            DrawGameStateSection();
            GUILayout.Space(6);
            DrawSpawnSection();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ============ Sections ============

        void DrawAutoTestSection()
        {
            GUILayout.Label("Auto-test", _sectionStyle);
            var auto = AutoCriticalFollower.Instance;
            if (auto == null)
            {
                GUILayout.Label("(AutoCriticalFollower not in scene)", _hintStyle);
                return;
            }
            auto.DebugForceActive = GUILayout.Toggle(auto.DebugForceActive, " Auto-follow critical path (debug)");
            if (auto.IsActive)
                GUILayout.Label("Player segue critical sozinho. Manual input ainda funciona (override por tile).", _hintStyle);
        }

        void DrawCharactersSection()
        {
            GUILayout.Label("Characters", _sectionStyle);
            var pdm = PlayerDataManager.Instance;
            if (pdm == null)
            {
                GUILayout.Label("(PlayerDataManager not in scene)", _hintStyle);
                return;
            }
            var owned = new System.Text.StringBuilder();
            for (int i = 0; i < CharacterCatalog.Count; i++)
            {
                if (pdm.IsCharacterOwned(i))
                {
                    if (owned.Length > 0) owned.Append(",");
                    owned.Append(CharacterCatalog.Get(i).Name);
                }
            }
            GUILayout.Label($"Equipped: {pdm.EquippedChar} ({CharacterCatalog.Get(pdm.EquippedChar).Name})", _hintStyle);
            GUILayout.Label($"Owned: {owned}", _hintStyle);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < CharacterCatalog.Count; i++)
            {
                int idx = i;
                if (GUILayout.Button($"Equip {CharacterCatalog.Get(i).Name}", GUILayout.MaxWidth(110)))
                {
                    if (!pdm.IsCharacterOwned(idx)) pdm.UnlockCharacter(idx);
                    pdm.EquipCharacter(idx);
                    pdm.Save();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Unlock All", GUILayout.MaxWidth(110)))
            {
                for (int i = 0; i < CharacterCatalog.Count; i++) pdm.UnlockCharacter(i);
                pdm.Save();
            }
            if (GUILayout.Button("Reset Chars", GUILayout.MaxWidth(110))) pdm.DebugResetCharacters();
            GUILayout.EndHorizontal();
        }

        void DrawDailyLoginSection()
        {
            GUILayout.Label("Daily Login + Chest", _sectionStyle);
            var dl = DailyLoginManager.Instance;
            if (dl == null)
            {
                GUILayout.Label("(DailyLoginManager not in scene)", _hintStyle);
                return;
            }
            string lc = string.IsNullOrEmpty(dl.LastClaimDate) ? "—" : dl.LastClaimDate;
            string cd = string.IsNullOrEmpty(dl.ChestLastDate) ? "—" : dl.ChestLastDate;
            GUILayout.Label($"Login: lastDay={dl.LastClaimedDay} lastClaim={lc} | Next: Day{dl.NextDay}(+{dl.NextDayReward})", _hintStyle);
            GUILayout.Label($"Chest: lastDate={cd} | available={dl.IsChestAvailable()}", _hintStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Show Login", GUILayout.MaxWidth(110))) dl.DebugForceLoginAvailable();
            if (GUILayout.Button("Claim Login", GUILayout.MaxWidth(110))) dl.ClaimLogin();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Avail Chest", GUILayout.MaxWidth(110))) dl.DebugForceChestAvailable();
            if (GUILayout.Button("Claim Chest", GUILayout.MaxWidth(110))) dl.ClaimChest();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Reset Login+Chest")) dl.DebugResetAll();
        }

        void DrawDailyChallengeSection()
        {
            GUILayout.Label("Daily Challenge (Fatia 6)", _sectionStyle);
            var dc = DailyChallengeManager.Instance;
            if (dc == null)
            {
                GUILayout.Label("(DailyChallengeManager not in scene)", _hintStyle);
                return;
            }
            string bestEverDate = string.IsNullOrEmpty(dc.BestEverDate) ? "—" : dc.BestEverDate;
            GUILayout.Label($"Today seed: {dc.GetTodaySeed()} | active mode: {(dc.IsDailyChallenge ? "DAILY" : "normal")}", _hintStyle);
            GUILayout.Label($"Today best: {dc.TodayBestM} m | Ever: {dc.BestEverM} m ({bestEverDate})", _hintStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Toggle Daily", GUILayout.MaxWidth(110))) dc.DebugToggleDailyMode();
            if (GUILayout.Button("Reset Today", GUILayout.MaxWidth(110))) dc.DebugResetToday();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Wipe All Daily")) dc.DebugWipe();
        }

        void DrawAuthSection()
        {
            GUILayout.Label("Auth (Fatia 7A)", _sectionStyle);
            var auth = AuthManager.Instance;
            if (auth == null)
            {
                GUILayout.Label("(AuthManager not in scene)", _hintStyle);
                return;
            }
            string uid = string.IsNullOrEmpty(auth.UserId) ? "—" : (auth.UserId.Length > 12 ? auth.UserId.Substring(0, 12) + "…" : auth.UserId);
            GUILayout.Label($"auth'd={auth.IsAuthenticated} | uid={uid}", _hintStyle);
            if (!string.IsNullOrEmpty(auth.LastError))
                GUILayout.Label($"err: {auth.LastError}", _hintStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Log status", GUILayout.MaxWidth(110))) auth.DebugLogStatus();
            if (GUILayout.Button("Re-auth", GUILayout.MaxWidth(110))) auth.DebugForceReauth();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Sign out (no re-auth)")) auth.DebugSignOut();
        }

        void DrawAdsSection()
        {
            GUILayout.Label("Ads (Fatia 5)", _sectionStyle);
            var ads = AdsManager.Instance;
            if (ads == null)
            {
                GUILayout.Label("(AdsManager not in scene — modo stub)", _hintStyle);
                return;
            }
            GUILayout.Label($"init={ads.IsInitialized} ready={ads.IsRewardedReady}", _hintStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Log state", GUILayout.MaxWidth(110))) ads.DebugLogState();
            if (GUILayout.Button("Force show", GUILayout.MaxWidth(110)))
            {
                ads.TryShowRewarded(
                    onSuccess: () => Debug.Log("[Ads-Debug] success"),
                    onFailed:  () => Debug.Log("[Ads-Debug] failed/skipped"));
            }
            GUILayout.EndHorizontal();
        }

        void DrawMissionsSection()
        {
            GUILayout.Label("Missions", _sectionStyle);
            var mt = MissionTracker.Instance;
            if (mt == null)
            {
                GUILayout.Label("(MissionTracker not in scene)", _hintStyle);
                return;
            }

            GUILayout.Label("Daily:", _hintStyle);
            for (int i = 0; i < MissionTracker.DailySlots; i++)
            {
                var entry = mt.GetDailyMission(i);
                string status = entry.IsClaimed ? "✓" : (entry.IsComplete ? "★" : " ");
                GUILayout.Label($"  [{status}] {Trunc(entry.Description, 30)} {entry.Progress:0}/{entry.Target:0}", _hintStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button($"Force D{i}", GUILayout.MaxWidth(100))) mt.DebugForceCompleteDaily(i);
                if (GUILayout.Button($"Claim D{i}", GUILayout.MaxWidth(100))) mt.ClaimDaily(i);
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("Weekly:", _hintStyle);
            for (int i = 0; i < MissionTracker.WeeklySlots; i++)
            {
                var entry = mt.GetWeeklyMission(i);
                string status = entry.IsClaimed ? "✓" : (entry.IsComplete ? "★" : " ");
                GUILayout.Label($"  [{status}] {Trunc(entry.Description, 30)} {entry.Progress:0}/{entry.Target:0}", _hintStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button($"Force W{i}", GUILayout.MaxWidth(100))) mt.DebugForceCompleteWeekly(i);
                if (GUILayout.Button($"Claim W{i}", GUILayout.MaxWidth(100))) mt.ClaimWeekly(i);
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cycle Daily", GUILayout.MaxWidth(120))) mt.DebugCycleDaily();
            if (GUILayout.Button("Cycle Weekly", GUILayout.MaxWidth(120))) mt.DebugCycleWeekly();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Reset All Missions")) mt.DebugResetAll();
        }

        static string Trunc(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length > max ? s.Substring(0, max - 1) + "…" : s;
        }

        void DrawPlayerDataSection()
        {
            GUILayout.Label("Player data", _sectionStyle);
            var pdm = PlayerDataManager.Instance;
            if (pdm == null)
            {
                GUILayout.Label("(PlayerDataManager not in scene)", _hintStyle);
                return;
            }
            GUILayout.Label($"Coins: {pdm.Coins} | Runs: {pdm.TotalRuns} | Char: {pdm.EquippedChar}", _hintStyle);
            int min = Mathf.FloorToInt(pdm.BestTime) / 60;
            int sec = Mathf.FloorToInt(pdm.BestTime) % 60;
            GUILayout.Label($"Best — Dist {pdm.BestDistance}m | Coins {pdm.BestCoins} | Tier {pdm.BestTier} | Time {min:D2}:{sec:D2}", _hintStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+100 coins", GUILayout.MaxWidth(110))) { pdm.AddCoins(100); pdm.Save(); }
            if (GUILayout.Button("+1000 coins", GUILayout.MaxWidth(110))) { pdm.AddCoins(1000); pdm.Save(); }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Reset All Player Data")) pdm.WipeAll();
        }

        void DrawActiveItemSection()
        {
            GUILayout.Label("Active item slot", _sectionStyle);
            var slot = ActiveItemSlot.Instance;
            if (slot == null)
            {
                GUILayout.Label("(ActiveItemSlot not in scene)", _hintStyle);
                return;
            }
            GUILayout.Label($"Slot: {slot.HeldItem}", _hintStyle);
            if (GUILayout.Button("Grant TimeFreeze")) slot.SetItem(ActiveItemType.TimeFreeze);
            if (GUILayout.Button("Use (Space)")) slot.UseItem();
        }

        void DrawPowerUpSection()
        {
            GUILayout.Label("Power-ups", _sectionStyle);
            var pum = PowerUpManager.Instance;
            if (pum == null)
            {
                GUILayout.Label("(PowerUpManager not in scene)", _hintStyle);
                return;
            }
            GrantSpawnRow("Shield (+1)",       () => pum.GrantShield(), shieldPickupPrefab, null);
            GrantSpawnRow("SlowDown",          () => pum.GrantSlowDown(pum.SlowDownDefaultTiles), slowDownPickupPrefab, null);
            GrantSpawnRow("Magnet",            () => pum.GrantMagnet(pum.MagnetDefaultTiles), magnetPickupPrefab, null);
            GrantSpawnRow("Difficulty Reset",  () => pum.GrantDifficultyReset(), difficultyResetPickupPrefab, null);
            GrantSpawnRow("2x Coins",          () => pum.GrantDoubleCoins(pum.DoubleCoinsDefaultTiles), doubleCoinsPickupPrefab, null);
            GrantSpawnRow("Ghost",             () => pum.GrantGhost(pum.GhostDefaultTiles), ghostPickupPrefab, null);
            GrantSpawnRow("Lane Preview",      () => pum.GrantLanePreview(pum.LanePreviewDefaultTiles), lanePreviewPickupPrefab, null);
            GrantSpawnRow("Coin Radar",        () => pum.GrantCoinRadar(pum.CoinRadarDefaultTiles), coinRadarPickupPrefab, null);
            GrantSpawnRow("Teleport",          () => pum.GrantTeleport(pum.TeleportDefaultTiles), teleportPickupPrefab, null);
            GrantSpawnRow("AutoFollow",        () => pum.GrantAutoCriticalFollow(pum.AutoCriticalFollowDefaultTiles), autoFollowPickupPrefab, null);
            GrantSpawnRow("TimeFreeze",        () => {
                if (ActiveItemSlot.Instance != null)
                    ActiveItemSlot.Instance.SetItem(ActiveItemType.TimeFreeze);
            }, timeFreezePickupPrefab, null);
            GUILayout.Space(4);
            GUILayout.Label("Debuffs (PostMVP2.5)", _hintStyle);
            GrantSpawnRow("SpeedUp Debuff",    () => pum.GrantSpeedUpDebuff(pum.SpeedUpDebuffDefaultTiles), speedUpZonePrefab, null);
            GrantSpawnRow("LaneSwap Debuff",   () => pum.GrantLaneSwapDebuff(pum.LaneSwapDebuffDefaultTiles), laneSwapObstaclePrefab, null);

            GUILayout.Label(
                $"Shield x{pum.ShieldCharges} | Slow {pum.SlowDownTilesRemaining} | Magnet {pum.MagnetTilesRemaining}",
                _hintStyle);
            GUILayout.Label(
                $"2xCoins {pum.DoubleCoinsTilesRemaining} | Ghost {pum.GhostTilesRemaining} | Preview {pum.LanePreviewTilesRemaining}",
                _hintStyle);
            GUILayout.Label(
                $"Radar {pum.CoinRadarTilesRemaining} | Teleport {pum.TeleportTilesRemaining} | AutoFollow {pum.AutoCriticalFollowTilesRemaining}",
                _hintStyle);
            GUILayout.Label(
                $"⚡SpeedUp {pum.SpeedUpDebuffTilesRemaining} | ↔LaneSwap {pum.LaneSwapDebuffTilesRemaining}",
                _hintStyle);
        }

        void DrawDifficultySection()
        {
            GUILayout.Label("Difficulty", _sectionStyle);
            var dm = DifficultyManager.Instance;
            if (dm == null)
            {
                GUILayout.Label("(DifficultyManager not in scene)", _hintStyle);
                return;
            }
            if (GUILayout.Button("Reset Difficulty (R)")) dm.ResetDifficulty();
            if (GUILayout.Button("Force Next Tier (T)")) dm.ForceNextTier();
            GUILayout.Label($"Tier {dm.CurrentTierIndex} · dist {dm.DistanceTraveled:F0}m", _hintStyle);
        }

        void DrawCoinsSection()
        {
            GUILayout.Label("Coins", _sectionStyle);
            var cm = CoinManager.Instance;
            if (cm == null)
            {
                GUILayout.Label("(CoinManager not in scene)", _hintStyle);
                return;
            }
            if (GUILayout.Button("+10 coins")) cm.AddCoins(10);
            if (GUILayout.Button("+100 coins")) cm.AddCoins(100);
            if (GUILayout.Button("Reset (0)")) cm.ResetTotal();
            GUILayout.Label($"Total: {cm.Total}", _hintStyle);
        }

        void DrawGameStateSection()
        {
            GUILayout.Label("Game state", _sectionStyle);
            var gm = GameManager.Instance;
            if (gm == null)
            {
                GUILayout.Label("(GameManager not in scene)", _hintStyle);
                return;
            }
            GUILayout.Label($"State: {gm.State}", _hintStyle);
            if (GUILayout.Button("Trigger DeadEnd")) gm.TriggerGameOver(GameOverReason.DeadEnd);
            if (GUILayout.Button("Trigger OutOfBounds")) gm.TriggerGameOver(GameOverReason.OutOfBounds);
            if (GUILayout.Button("Trigger HitObstacle")) gm.TriggerGameOver(GameOverReason.HitObstacle);
        }

        void DrawSpawnSection()
        {
            GUILayout.Label("Spawn hazards ahead", _sectionStyle);
            GrantSpawnRow("Lethal", null, lethalObstaclePrefab, null);
            GrantSpawnRow("Barrier", null, barrierObstaclePrefab, null);
            GrantSpawnRow("SpeedUp", null, speedUpZonePrefab, null);
            GrantSpawnRow("LaneSwap", null, laneSwapObstaclePrefab, null);
            GrantSpawnRow("Vortex", null, vortexObstaclePrefab, null);
        }

        // Renderiza linha [Grant?] [Spawn]. Se grantAction == null, mostra
        // só Spawn (caso de hazards sem Grant direto). Se prefab == null,
        // botão Spawn fica desabilitado.
        void GrantSpawnRow(string label, System.Action grantAction, GameObject prefab, string spawnLabel)
        {
            GUILayout.BeginHorizontal();
            if (grantAction != null)
            {
                if (GUILayout.Button($"Grant {label}", GUILayout.MaxWidth(140)))
                    grantAction();
            }
            else
            {
                GUILayout.Label(label, GUILayout.MaxWidth(140));
            }
            GUI.enabled = prefab != null;
            if (GUILayout.Button(spawnLabel ?? "Spawn", GUILayout.MaxWidth(80)))
                SpawnPrefabAhead(prefab);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Instancia o prefab em N rows à frente do player (default 2),
        /// na lane do player se houver tile, senão na lane vizinha mais próxima.
        /// Posicionado em altura padrão (0.8 — mesmo do PowerUpSpawner).
        /// </summary>
        void SpawnPrefabAhead(GameObject prefab)
        {
            if (prefab == null) return;
            var player = FindFirstObjectByType<PlayerRailRider>();
            if (player == null || player.CurrentTile == null)
            {
                Debug.LogWarning("[DebugPanel] Player ou CurrentTile null — spawn cancelado.");
                return;
            }

            int targetRow = player.CurrentTile.Row + debugSpawnRowsAhead;
            int targetLane = player.CurrentTile.Lane;

            var rm = RailManager.Instance;
            if (rm == null) { Debug.LogWarning("[DebugPanel] RailManager null"); return; }
            var row = rm.GetRow(targetRow);
            if (row == null)
            {
                Debug.LogWarning($"[DebugPanel] Row {targetRow} ainda não spawnada — tente menos rows ahead.");
                return;
            }

            // Tenta lane do player primeiro, depois ±1, ±2...
            TrackTile target = null;
            for (int dist = 0; dist <= row.Tiles.Length; dist++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int lane = targetLane + dist * sign;
                    if (lane < 0 || lane >= row.Tiles.Length) continue;
                    if (row.Tiles[lane] != null)
                    {
                        target = row.Tiles[lane];
                        targetLane = lane;
                        break;
                    }
                    if (dist == 0) break; // não duplicar lane do meio
                }
                if (target != null) break;
            }

            if (target == null || target.StartPoint == null || target.EndPoint == null)
            {
                Debug.LogWarning($"[DebugPanel] Sem tile disponível em R{targetRow} pra spawnar {prefab.name}.");
                return;
            }

            Vector3 pos = (target.StartPoint.position + target.EndPoint.position) * 0.5f;
            pos.y += 0.8f;
            var spawned = Instantiate(prefab, pos, Quaternion.identity, target.transform);
            Debug.Log($"[DebugPanel] Spawned {prefab.name} → '{spawned.name}' at R{targetRow} L{targetLane}");
        }

        // ============ Styles ============

        void EnsureStyles()
        {
            int sectionSize = Mathf.RoundToInt(13 * uiScale);
            int hintSize = Mathf.RoundToInt(10 * uiScale);

            if (_sectionStyle == null)
            {
                _sectionStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = sectionSize,
                };
                _sectionStyle.normal.textColor = new Color(0.9f, 0.9f, 0.5f);
            }
            else _sectionStyle.fontSize = sectionSize;

            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = hintSize,
                    wordWrap = true,
                };
                _hintStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            }
            else _hintStyle.fontSize = hintSize;
        }

        // ============ UI scale helpers ============

        // Aplica uiScale aos font sizes do GUI.skin (button, label, etc).
        // Salva originais na primeira chamada e restaura via PopScaledSkin
        // pra não vazar pro próximo OnGUI (ex: SpawnOverrideController).
        void PushScaledSkin()
        {
            var skin = GUI.skin;
            if (!_skinOriginalsCaptured)
            {
                _origButtonFont = skin.button.fontSize;
                _origLabelFont = skin.label.fontSize;
                _origToggleFont = skin.toggle.fontSize;
                _origTextFieldFont = skin.textField.fontSize;
                _origBoxFont = skin.box.fontSize;
                _skinOriginalsCaptured = true;
            }
            // Se o original era 0 (default da fonte built-in ~12), usa baseline 12.
            int baseFont = _origButtonFont > 0 ? _origButtonFont : 12;
            int scaled = Mathf.RoundToInt(baseFont * uiScale);
            skin.button.fontSize = scaled;
            skin.label.fontSize = scaled;
            skin.toggle.fontSize = scaled;
            skin.textField.fontSize = scaled;
            skin.box.fontSize = scaled;
        }

        void PopScaledSkin()
        {
            if (!_skinOriginalsCaptured) return;
            var skin = GUI.skin;
            skin.button.fontSize = _origButtonFont;
            skin.label.fontSize = _origLabelFont;
            skin.toggle.fontSize = _origToggleFont;
            skin.textField.fontSize = _origTextFieldFont;
            skin.box.fontSize = _origBoxFont;
        }
    }
}
