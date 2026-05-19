using UnityEngine;
using RailSwitchMVP.Config;
using RailSwitchMVP.Core;
using RailSwitchMVP.Track;
using RailSwitchMVP.InputSys;

namespace RailSwitchMVP.Player
{
    /// <summary>
    /// Iter 2: movimento forward + transição entre tiles via switch + game over.
    /// - Lê speed do tier atual (DifficultyManager).
    /// - Lê input direcional (IDirectionalInput) e chama Switch.Nudge.
    /// - Quando atinge EndPoint, lê o switch atual e tenta ir para o tile destino:
    ///     * Lane fora do grid da próxima linha → GameOver(OutOfBounds).
    ///     * Lane sem tile (decoy dead-end)     → GameOver(DeadEnd).
    ///     * Lane válida com tile               → lerp X no gap até o StartPoint do alvo.
    /// </summary>
    public class PlayerRailRider : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RailGenConfig config;
        [SerializeField] private DifficultyManager difficulty;

        [Tooltip("Implementação de input direcional. Se vazio, busca um IDirectionalInput na cena no Start.")]
        [SerializeField] private MonoBehaviour inputSource; // deve implementar IDirectionalInput
        private IDirectionalInput _input;

        [Header("Initial Tile")]
        [Tooltip("Tile onde o player começa. Iter 3: definido pelo RailManager.")]
        [SerializeField] private TrackTile startTile;

        [Header("Runtime (read-only)")]
        [SerializeField] private float currentSpeed;
        [SerializeField] private float distanceTraveled;
        [SerializeField] private TrackTile currentTile;
        [SerializeField] private TrackTile targetTile;
        [SerializeField] private bool inGap;
        [SerializeField] private float gapProgress;

        private Vector3 _gapStartPos;
        private Vector3 _gapEndPos;
        private float _playerY;

        public float CurrentSpeed => currentSpeed;
        public float DistanceTraveled => distanceTraveled;
        public TrackTile CurrentTile => currentTile;

        /// <summary>
        /// Disparado quando o player entra num NOVO tile (após o gap ou ao
        /// iniciar). Usado pelo PowerUpManager pra decrementar duração em tiles.
        /// </summary>
        public event System.Action<TrackTile> OnTileEntered;

        void Start()
        {
            _playerY = transform.position.y;

            // Resolve input
            if (inputSource is IDirectionalInput src) _input = src;
            if (_input == null)
            {
                foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (mb is IDirectionalInput di) { _input = di; break; }
                }
            }
            if (_input == null)
                Debug.LogWarning("[PlayerRailRider] No IDirectionalInput found in scene. Switches won't respond to input.");

            // Iter 2: startTile vem do Inspector. Iter 3: RailManager chama SetStartTile no Start dele.
            if (startTile != null)
                ApplyStartTile(startTile);

            transform.rotation = Quaternion.identity;
        }

        /// <summary>
        /// Chamado pelo RailManager em runtime quando o tile inicial é gerado proceduralmente.
        /// </summary>
        public void SetStartTile(TrackTile tile)
        {
            if (tile == null) return;
            startTile = tile;
            ApplyStartTile(tile);
        }

        void ApplyStartTile(TrackTile tile)
        {
            if (tile == null || tile.StartPoint == null) return;
            currentTile = tile;
            Vector3 pos = tile.StartPoint.position;
            pos.y = _playerY;
            transform.position = pos;
        }

        void Update()
        {
            // Game Over → trava o player.
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
                return;

            if (difficulty != null)
            {
                float baseSpeed = difficulty.CurrentTier.playerSpeed;
                float multiplier = PowerUpManager.Instance != null
                    ? PowerUpManager.Instance.SpeedMultiplier
                    : 1f;
                currentSpeed = baseSpeed * multiplier;
            }

            // Input → switch do tile atual.
            if (_input != null && currentTile != null && currentTile.Switch != null && !inGap)
            {
                int dir = _input.ConsumeDirection();
                if (dir != 0)
                    currentTile.Switch.Nudge(dir);
            }

            if (inGap)
                TickGap();
            else
                TickOnTile();

            distanceTraveled = transform.position.z;
            if (difficulty != null)
                difficulty.UpdateDistance(distanceTraveled);
        }

        void TickOnTile()
        {
            transform.position += Vector3.forward * (currentSpeed * Time.deltaTime);

            if (currentTile == null || currentTile.EndPoint == null) return;

            if (transform.position.z >= currentTile.EndPoint.position.z)
                TryEnterGap();
        }

        void TickGap()
        {
            if (config == null) { inGap = false; return; }

            gapProgress += (currentSpeed * Time.deltaTime) / config.rowGap;
            Vector3 p = Vector3.Lerp(_gapStartPos, _gapEndPos, gapProgress);
            p.y = _playerY;
            transform.position = p;

            if (gapProgress >= 1f)
                ExitGap();
        }

        void TryEnterGap()
        {
            if (currentTile == null) return;

            int targetLane = currentTile.Switch != null
                ? currentTile.Switch.TargetLane
                : currentTile.Lane;

            RowData nextRow = (RailManager.Instance != null)
                ? RailManager.Instance.GetRow(currentTile.Row + 1)
                : null;

            // Sem próxima linha registrada: trata como OutOfBounds.
            if (nextRow == null)
            {
                TriggerGameOver(GameOverReason.OutOfBounds);
                return;
            }

            if (targetLane < 0 || targetLane >= nextRow.MaxLanesAtSpawn)
            {
                TriggerGameOver(GameOverReason.OutOfBounds);
                return;
            }

            if (!nextRow.HasTile(targetLane))
            {
                TriggerGameOver(GameOverReason.DeadEnd);
                return;
            }

            // OK — preparar gap até o tile destino.
            targetTile = nextRow.Tiles[targetLane];
            _gapStartPos = currentTile.EndPoint.position;
            _gapEndPos = targetTile.StartPoint.position;
            _gapStartPos.y = _playerY;
            _gapEndPos.y = _playerY;
            gapProgress = 0f;
            inGap = true;
        }

        void ExitGap()
        {
            currentTile = targetTile;
            targetTile = null;
            inGap = false;
            gapProgress = 0f;

            // Notifica listeners (PowerUpManager decrementa duração em tiles aqui)
            if (currentTile != null)
                OnTileEntered?.Invoke(currentTile);

            if (currentTile != null && currentTile.StartPoint != null)
            {
                Vector3 p = currentTile.StartPoint.position;
                p.y = _playerY;
                transform.position = p;
            }
        }

        void TriggerGameOver(GameOverReason reason)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.TriggerGameOver(reason);
            else
                Debug.LogWarning($"[PlayerRailRider] GAME OVER ({reason}) — no GameManager in scene.");
        }
    }
}
