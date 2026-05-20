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
        private Vector2 _scroll;
        private GUIStyle _sectionStyle;
        private GUIStyle _hintStyle;

        bool ShouldRespond => !restrictToDebugBuilds || Application.isEditor || Debug.isDebugBuild;

        void Update()
        {
            if (!ShouldRespond) return;

            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame)
                _show = !_show;
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
            if (GUILayout.Button("Grant AutoFollow")) pum.GrantAutoCriticalFollow(pum.AutoCriticalFollowDefaultTiles);
            GUILayout.Space(4);
            GUILayout.Label("Debuffs (PostMVP2.5)", _hintStyle);
            if (GUILayout.Button("Grant SpeedUp Debuff")) pum.GrantSpeedUpDebuff(pum.SpeedUpDebuffDefaultTiles);
            if (GUILayout.Button("Grant LaneSwap Debuff")) pum.GrantLaneSwapDebuff(pum.LaneSwapDebuffDefaultTiles);

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
