using UnityEngine;
using UnityEngine.InputSystem;
using RailSwitchMVP.Collectibles;
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

        [Tooltip("Tamanho do painel em pixels.")]
        public Vector2 panelSize = new Vector2(280f, 600f);

        [Tooltip("Posição (top-left corner) do painel na tela.")]
        public Vector2 panelPosition = new Vector2(10f, 200f);

        private bool _show;
        private bool _autoFollowCritical;
        private Vector2 _scroll;
        private GUIStyle _sectionStyle;
        private GUIStyle _hintStyle;

        private PlayerRailRider _player;
        private bool _subscribedToPlayer;
        private bool _initialAutoApplied;

        bool ShouldRespond => !restrictToDebugBuilds || Application.isEditor || Debug.isDebugBuild;

        void Update()
        {
            if (!ShouldRespond) return;

            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame)
                _show = !_show;

            // Lazy-subscribe ao player (caso o componente exista antes do player Start)
            if (!_subscribedToPlayer)
            {
                _player = FindFirstObjectByType<PlayerRailRider>();
                if (_player != null)
                {
                    _player.OnTileEntered += HandleTileEntered;
                    _subscribedToPlayer = true;
                }
            }

            // Aplica auto-follow no tile inicial (OnTileEntered não dispara pra ele).
            if (_autoFollowCritical && !_initialAutoApplied && _player != null && _player.CurrentTile != null)
            {
                AutoFollowCritical(_player.CurrentTile);
                _initialAutoApplied = true;
            }
            if (!_autoFollowCritical) _initialAutoApplied = false;
        }

        void OnDestroy()
        {
            if (_player != null) _player.OnTileEntered -= HandleTileEntered;
        }

        void HandleTileEntered(TrackTile newTile)
        {
            if (_autoFollowCritical) AutoFollowCritical(newTile);
        }

        /// <summary>
        /// Ajusta o switch do tile atual pra apontar à lane do critical path
        /// alcançável na próxima row. Preferência: critical lane via offset
        /// {0, -1, +1}. Fallback: qualquer tile populado em {0, -1, +1}.
        /// </summary>
        void AutoFollowCritical(TrackTile currentTile)
        {
            if (currentTile == null || currentTile.Switch == null) return;
            var rm = RailManager.Instance;
            if (rm == null) return;
            var nextRow = rm.GetRow(currentTile.Row + 1);
            if (nextRow == null) return;

            int chosenOffset = FindBestOffset(nextRow, currentTile.Lane);
            currentTile.Switch.SetState((SwitchState)chosenOffset);
        }

        static int FindBestOffset(RowData nextRow, int currentLane)
        {
            // Preferência: Middle, depois Left, depois Right.
            int[] offsets = { 0, -1, 1 };

            // 1ª passada: critical path (sem hazard por design).
            foreach (var off in offsets)
            {
                int target = currentLane + off;
                if (nextRow.HasTile(target) && nextRow.IsCriticalLane(target))
                    return off;
            }

            // 2ª passada: qualquer tile populado (decoy — pode ter hazard, paciência).
            foreach (var off in offsets)
            {
                int target = currentLane + off;
                if (nextRow.HasTile(target)) return off;
            }

            return 0; // desiste (player provavelmente dead-end)
        }

        void OnGUI()
        {
            if (!_show || !ShouldRespond) return;

            EnsureStyles();

            var rect = new Rect(panelPosition.x, panelPosition.y, panelSize.x, panelSize.y);
            GUI.Box(rect, "DEBUG (F1 toggle)");

            GUILayout.BeginArea(new Rect(rect.x + 6, rect.y + 22, rect.width - 12, rect.height - 28));
            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawAutoTestSection();
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
            _autoFollowCritical = GUILayout.Toggle(_autoFollowCritical, " Auto-follow critical path");
            if (_autoFollowCritical)
                GUILayout.Label("Player segue critical sozinho. Manual input ainda funciona (override por tile).", _hintStyle);
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
            if (GUILayout.Button("Grant Shield (+1)")) pum.GrantShield();
            if (GUILayout.Button("Grant SlowDown")) pum.GrantSlowDown(pum.SlowDownDefaultTiles);
            if (GUILayout.Button("Grant Magnet")) pum.GrantMagnet(pum.MagnetDefaultTiles);
            if (GUILayout.Button("Grant Difficulty Reset")) pum.GrantDifficultyReset();
            if (GUILayout.Button("Grant 2x Coins")) pum.GrantDoubleCoins(pum.DoubleCoinsDefaultTiles);
            if (GUILayout.Button("Grant Ghost")) pum.GrantGhost(pum.GhostDefaultTiles);
            if (GUILayout.Button("Grant Lane Preview")) pum.GrantLanePreview(pum.LanePreviewDefaultTiles);
            if (GUILayout.Button("Grant Coin Radar")) pum.GrantCoinRadar(pum.CoinRadarDefaultTiles);
            if (GUILayout.Button("Grant Teleport")) pum.GrantTeleport(pum.TeleportDefaultTiles);

            GUILayout.Label(
                $"Shield x{pum.ShieldCharges} | Slow {pum.SlowDownTilesRemaining} | Magnet {pum.MagnetTilesRemaining}",
                _hintStyle);
            GUILayout.Label(
                $"2xCoins {pum.DoubleCoinsTilesRemaining} | Ghost {pum.GhostTilesRemaining} | Preview {pum.LanePreviewTilesRemaining}",
                _hintStyle);
            GUILayout.Label(
                $"Radar {pum.CoinRadarTilesRemaining} | Teleport {pum.TeleportTilesRemaining}",
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
            GUILayout.Label("Spawn in player's tile", _sectionStyle);
            var player = FindFirstObjectByType<PlayerRailRider>();
            if (player == null || player.CurrentTile == null)
            {
                GUILayout.Label("(no player or current tile)", _hintStyle);
                return;
            }
            var tile = player.CurrentTile;
            GUILayout.Label($"Current tile: R{tile.Row} L{tile.Lane}", _hintStyle);

            // Spawn manual via Generator's prefab list. Hook detalhado vem depois.
            // Por enquanto, instrução rápida: você pode arrastar prefabs no
            // Inspector deste componente pra spawnar via dropdown (futuro).
        }

        // ============ Styles ============

        void EnsureStyles()
        {
            if (_sectionStyle == null)
            {
                _sectionStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 13,
                };
                _sectionStyle.normal.textColor = new Color(0.9f, 0.9f, 0.5f);
            }
            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    wordWrap = true,
                };
                _hintStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            }
        }
    }
}
