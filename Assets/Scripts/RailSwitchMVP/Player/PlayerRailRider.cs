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
        // Distância Z do gap atual — varia entre normal (rowGap) e ghost (até trackLength+rowGap).
        // gapProgress incrementa em (speed * dt / _gapDistance), garantindo velocidade
        // forward consistente independente de quanto Z o gap cobre.
        private float _gapDistance;
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

            // Usa _gapDistance (Z forward) — varia entre rowGap (gap normal)
            // e trackLength+rowGap (ghost flight continuando). Fallback safety.
            float dist = _gapDistance > 0.01f ? _gapDistance : config.rowGap;
            gapProgress += (currentSpeed * Time.deltaTime) / dist;
            Vector3 p = Vector3.Lerp(_gapStartPos, _gapEndPos, gapProgress);
            p.y = _playerY;
            transform.position = p;

            if (gapProgress >= 1f)
                ExitGap();
        }

        // === Ghost flight state (PostMVP2.2) ===
        // Quando Ghost está ativo e player encontra DeadEnd, ele "voa" sobre
        // rows vazias até pousar num tile real. Estado tracking abaixo.
        private int _ghostFlightLane;
        private int _ghostFlightRow;
        private int _ghostFlightSkips;
        private const int MaxGhostFlightSkips = 12;

        void TryEnterGap()
        {
            if (currentTile == null) return;

            int targetLane = currentTile.Switch != null
                ? currentTile.Switch.TargetLane
                : currentTile.Lane;

            bool isGhost = PowerUpManager.Instance != null && PowerUpManager.Instance.IsGhost;

            RowData nextRow = (RailManager.Instance != null)
                ? RailManager.Instance.GetRow(currentTile.Row + 1)
                : null;

            // Sem próxima linha registrada: trata como OutOfBounds.
            if (nextRow == null)
            {
                TriggerGameOver(GameOverReason.OutOfBounds);
                return;
            }

            // OutOfBounds: com Ghost, clamp pra current lane (segue reto).
            // Sem Ghost, Game Over.
            if (targetLane < 0 || targetLane >= nextRow.MaxLanesAtSpawn)
            {
                if (isGhost)
                {
                    targetLane = currentTile.Lane;
                }
                else
                {
                    TriggerGameOver(GameOverReason.OutOfBounds);
                    return;
                }
            }

            // DeadEnd: com Ghost, voa sobre. Sem Ghost, Game Over.
            if (!nextRow.HasTile(targetLane))
            {
                if (isGhost)
                {
                    BeginGhostFlight(targetLane);
                    return;
                }
                TriggerGameOver(GameOverReason.DeadEnd);
                return;
            }

            // OK — preparar gap até o tile destino.
            targetTile = nextRow.Tiles[targetLane];
            _gapStartPos = currentTile.EndPoint.position;
            _gapEndPos = targetTile.StartPoint.position;
            _gapStartPos.y = _playerY;
            _gapEndPos.y = _playerY;
            _gapDistance = Mathf.Max(0.01f, _gapEndPos.z - _gapStartPos.z);
            gapProgress = 0f;
            inGap = true;
        }

        // Inicia voo Ghost: configura phantom destination = onde o tile ESTARIA
        // se a row N+1 tivesse um no targetLane.
        void BeginGhostFlight(int targetLane)
        {
            _ghostFlightLane = targetLane;
            _ghostFlightRow = currentTile.Row + 1;
            _ghostFlightSkips = 0;
            SetupGhostGap();
        }

        void SetupGhostGap()
        {
            int globalMax = config.globalMaxLanes;
            Vector3 phantomCenter = TrackTile.ComputeWorldPosition(
                _ghostFlightRow, _ghostFlightLane, globalMax, config);
            // StartPoint do phantom = center - trackLength/2 no eixo Z.
            Vector3 phantomStart = phantomCenter;
            phantomStart.z -= config.trackLength * 0.5f;

            // BUG FIX: usa transform.position (onde player REALMENTE está) em vez
            // de currentTile.EndPoint (que pode estar várias rows atrás durante
            // voos consecutivos). Garante lerp suave sem teleport pra trás.
            _gapStartPos = transform.position;
            _gapEndPos = phantomStart;
            _gapStartPos.y = _playerY;
            _gapEndPos.y = _playerY;

            // BUG FIX: distância forward varia. 1º voo = rowGap. Voos seguintes
            // = trackLength + rowGap (atravessa uma row inteira vazia).
            // gapProgress agora normaliza pela distância real → speed forward
            // consistente independente do tipo de gap.
            _gapDistance = Mathf.Max(0.01f, _gapEndPos.z - _gapStartPos.z);

            targetTile = null; // marca "ghost gap" pra ExitGap saber
            gapProgress = 0f;
            inGap = true;
        }

        void ExitGap()
        {
            // targetTile == null → estávamos em ghost flight, tratar separado.
            if (targetTile == null)
            {
                TryLandFromGhostFlight();
                return;
            }

            // Normal: aterriza no targetTile.
            currentTile = targetTile;
            targetTile = null;
            inGap = false;
            gapProgress = 0f;

            if (currentTile != null)
                OnTileEntered?.Invoke(currentTile);

            if (currentTile != null && currentTile.StartPoint != null)
            {
                Vector3 p = currentTile.StartPoint.position;
                p.y = _playerY;
                transform.position = p;
            }
        }

        // Tenta pousar no fim de um ghost gap. Se a row destino tem tile,
        // aterriza. Senão, voa mais uma row (com safety limit).
        void TryLandFromGhostFlight()
        {
            var landingRow = (RailManager.Instance != null)
                ? RailManager.Instance.GetRow(_ghostFlightRow)
                : null;

            if (landingRow == null)
            {
                // Row não existe (não gerada ainda ou fora do mundo) — game over.
                TriggerGameOver(GameOverReason.OutOfBounds);
                return;
            }

            if (landingRow.HasTile(_ghostFlightLane))
            {
                LandOnTile(landingRow.Tiles[_ghostFlightLane]);
                return;
            }

            // Sem tile na row — voa outra. Conta esta row como traversada
            // (decrementa Ghost e outros power-ups via OnTileEntered).
            OnTileEntered?.Invoke(null);
            _ghostFlightSkips++;

            // Pós-tick: Ghost ainda ativo? Safety limit OK?
            bool stillGhost = PowerUpManager.Instance != null && PowerUpManager.Instance.IsGhost;
            if (!stillGhost || _ghostFlightSkips >= MaxGhostFlightSkips)
            {
                // Ghost expirou mid-flight: lerp suave (0.2s) até a lane mais
                // próxima com tile na row atual. Sem isso o player ficaria
                // sem controle indefinidamente.
                int rescueLane = FindClosestPopulatedLane(landingRow, _ghostFlightLane);
                if (rescueLane >= 0)
                {
                    Debug.Log($"[PlayerRailRider] Ghost expirou em voo — rescue land row={landingRow.RowIndex} lane={rescueLane} (target era {_ghostFlightLane})");
                    StartRescueGap(landingRow.Tiles[rescueLane]);
                    return;
                }
                // Row inteira vazia — fallback game over (extremamente raro com minLanesPerRow≥2).
                TriggerGameOver(GameOverReason.DeadEnd);
                return;
            }

            _ghostFlightRow++;
            SetupGhostGap();
        }

        /// <summary>
        /// Aterriza imediatamente num tile (snap). Usado pra ghost flight
        /// landing direta (mesma lane do target original).
        /// </summary>
        void LandOnTile(TrackTile tile)
        {
            currentTile = tile;
            targetTile = null;
            inGap = false;
            gapProgress = 0f;
            OnTileEntered?.Invoke(currentTile);
            if (currentTile != null && currentTile.StartPoint != null)
            {
                Vector3 p = currentTile.StartPoint.position;
                p.y = _playerY;
                transform.position = p;
            }
        }

        /// <summary>
        /// Configura um gap rápido (~0.2s) da posição atual até o StartPoint
        /// de um tile rescue. Usado quando Ghost expira mid-flight pra evitar
        /// o snap lateral visualmente abrupto. Reusa o flow normal de ExitGap.
        /// </summary>
        void StartRescueGap(TrackTile rescueTile)
        {
            if (rescueTile == null || rescueTile.StartPoint == null) return;

            targetTile = rescueTile;
            _gapStartPos = transform.position;
            _gapEndPos = rescueTile.StartPoint.position;
            _gapStartPos.y = _playerY;
            _gapEndPos.y = _playerY;

            // _gapDistance dimensionado pra dar ~0.2s de lerp na speed atual.
            // gapProgress += speed*dt/dist → reaches 1 em dist/speed = 0.2s. ✓
            const float rescueDuration = 0.2f;
            _gapDistance = Mathf.Max(0.01f, currentSpeed * rescueDuration);

            gapProgress = 0f;
            inGap = true;
        }

        // Encontra a lane com tile mais próxima de targetLane na row dada.
        // Retorna -1 se a row está completamente vazia (impossível em geração normal).
        static int FindClosestPopulatedLane(RowData row, int targetLane)
        {
            if (row == null) return -1;
            int best = -1;
            int bestDist = int.MaxValue;
            for (int L = 0; L < row.Tiles.Length; L++)
            {
                if (row.Tiles[L] == null) continue;
                int dist = Mathf.Abs(L - targetLane);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = L;
                }
            }
            return best;
        }

        void TriggerGameOver(GameOverReason reason)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.TriggerGameOver(reason);
            else
                Debug.LogWarning($"[PlayerRailRider] GAME OVER ({reason}) — no GameManager in scene.");
        }

        /// <summary>
        /// Teleporta INSTANTANEAMENTE pro tile adjacente. Chamado pelo
        /// TeleportController (active item). Mantém Z atual do player,
        /// muda X pra alinhar com o tile destino. Não funciona durante gap.
        /// Retorna false se a teleportação não pode acontecer.
        /// </summary>
        public bool TeleportToAdjacent(TrackTile destinationTile)
        {
            if (destinationTile == null || destinationTile.StartPoint == null) return false;
            if (inGap) return false; // mid-gap = não pode teleportar
            if (currentTile == null) return false;

            currentTile = destinationTile;

            // Só muda X — mantém o Z onde o player estava no tile anterior.
            Vector3 p = transform.position;
            p.x = destinationTile.StartPoint.position.x;
            p.y = _playerY;
            transform.position = p;

            // OnTileEntered notifica HUD/PowerUpManager (tile mudou).
            OnTileEntered?.Invoke(currentTile);
            return true;
        }
    }
}
