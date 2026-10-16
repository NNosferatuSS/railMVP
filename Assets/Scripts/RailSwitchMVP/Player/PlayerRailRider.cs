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

        // Gap FATAL: o player segue a direção do switch rumo a onde o tile deveria
        // estar (dead-end) ou pra fora da pista (out-of-bounds) e só morre no FIM da
        // transição — não no fim do trilho. Dá leitura visual: você vê o switch
        // levar pro vazio antes do game over.
        private bool _gapIsFatal;
        private GameOverReason _gapFatalReason;

        // Morte já disparada (fim do gap fatal). Congela o player exatamente onde
        // morreu — evita que ele re-processe movimento e volte pro fim do trilho
        // enquanto o game over ainda não foi confirmado (ex.: oferta de revive,
        // que mantém o state em Playing). Limpo no RespawnAt (revive).
        private bool _deathPending;

        public float CurrentSpeed => currentSpeed;

        // Velocidade-base do tier com rampa de aceleração (RailGenConfig.speedRampPerSecond).
        float _rampedBaseSpeed;

        // ===== Input com buffer / tolerância =====
        [Header("Input assist")]
        [Tooltip("Por quanto tempo um input feito na lacuna fica guardado pro próximo trilho.")]
        [SerializeField] private float inputBufferSeconds = 0.35f;
        [Tooltip("Tolerância: nos primeiros X s da lacuna, um input ainda corrige o destino " +
            "(pode salvar de um dead end por um toque atrasado).")]
        [SerializeField] private float lateCorrectionSeconds = 0.12f;

        readonly System.Collections.Generic.List<SwitchCommand> _pending = new System.Collections.Generic.List<SwitchCommand>();
        float _gapStartTime = -10f;

        /// <summary>Voo fantasma começou (Ghost atravessando o vazio) — feedback visual/sonoro.</summary>
        public event System.Action GhostFlightStarted;

        /// <summary>Atravessando o vazio com Ghost agora.</summary>
        public bool IsGhostFlying => inGap && targetTile == null && !_gapIsFatal;

        /// <summary>Tutorial corrigiu um conector que levaria a uma queda.</summary>
        public event System.Action TutorialRescued;

        /// <summary>Disparado quando a tolerância corrigiu o destino já dentro da lacuna.</summary>
        public event System.Action LateCorrectionApplied;

        void ProcessPendingCommands()
        {
            if (_pending.Count == 0) return;
            float now = Time.unscaledTime;

            // Toque em trilho distante: o trilho da rota logo antes dele conecta nele — agora,
            // independente de o player estar no trilho ou na lacuna.
            for (int i = 0; i < _pending.Count; i++)
                if (_pending[i].kind == SwitchCommandKind.RouteTile) ConnectAhead(_pending[i]);
            _pending.RemoveAll(c => c.kind == SwitchCommandKind.RouteTile);
            if (_pending.Count == 0) return;

            if (inGap)
            {
                // Tolerância: comando novo logo no começo da lacuna corrige o destino
                // a partir do trilho que acabou de terminar (não vale pra ghost flight).
                bool ghostFlight = targetTile == null && !_gapIsFatal;
                if (!ghostFlight && currentTile != null && currentTile.Switch != null
                    && now - _gapStartTime <= lateCorrectionSeconds)
                {
                    var before = currentTile.Switch.State;
                    foreach (var c in _pending) ApplyCommand(c, currentTile);
                    _pending.Clear();
                    // A tolerância só AJUDA: se o novo destino não tem trilho, desfaz e
                    // mantém o caminho que já estava (nunca troca um destino por uma queda).
                    if (currentTile.Switch.State != before)
                    {
                        var nextRow = RailManager.Instance != null ? RailManager.Instance.GetRow(currentTile.Row + 1) : null;
                        if (nextRow == null || !nextRow.HasTile(currentTile.Switch.TargetLane))
                        {
                            currentTile.Switch.SetState(before, immediate: true);
                            return;
                        }
                    }
                    if (currentTile.Switch.State != before)
                    {
                        float p = gapProgress;
                        TryEnterGap();
                        gapProgress = Mathf.Min(p, 0.95f);
                        LateCorrectionApplied?.Invoke();
                    }
                    return;
                }
                // Fora da tolerância: segura pro próximo tile (descarta os velhos).
                _pending.RemoveAll(c => now - c.time > inputBufferSeconds);
                return;
            }

            if (currentTile == null || currentTile.Switch == null) { _pending.Clear(); return; }
            foreach (var c in _pending)
                if (now - c.time <= inputBufferSeconds + 0.05f) ApplyCommand(c, currentTile);
            _pending.Clear();
        }

        /// <summary>
        /// Toque num trilho resolvido (feedback visual/sonoro): trilho tocado, trilho cujo
        /// conector trocou (ou que tentou), e se deu certo (false = não alcança).
        /// </summary>
        public static event System.Action<TrackTile, TrackTile, bool> TapResolved;

        // Segue a rota atual (conectores como estão) até a linha antes do alvo; aquele
        // trilho aponta pro alvo se ele estiver a até 1 lane. Muda UM conector só.
        void ConnectAhead(SwitchCommand c)
        {
            var rm = RailManager.Instance;
            var row = rm != null ? rm.GetRow(c.targetRow) : null;
            if (row == null || !row.HasTile(c.targetLane) || currentTile == null) return;
            var target = row.Tiles[c.targetLane];

            var t = currentTile;
            while (t != null && t.Row < c.targetRow - 1)
            {
                var nr = rm.GetRow(t.Row + 1);
                int lane = t.Switch != null ? t.Switch.TargetLane : t.Lane;
                t = nr != null && nr.HasTile(lane) ? nr.Tiles[lane] : null;
            }
            if (t == null || t.Switch == null || t.Row != c.targetRow - 1) { TapResolved?.Invoke(target, null, false); return; }

            int d = c.targetLane - t.Lane;
            if (Mathf.Abs(d) > 1) { TapResolved?.Invoke(target, t, false); return; }
            t.Switch.SelectRemote((SwitchState)d);
            TapResolved?.Invoke(target, t, true);
        }

        void ApplyCommand(SwitchCommand c, TrackTile tile)
        {
            var sw = tile.Switch;
            switch (c.kind)
            {
                case SwitchCommandKind.Relative:
                    sw.Nudge(c.dir);
                    break;
                case SwitchCommandKind.TargetTile:
                    // Trilho tocado à frente (ou ao lado): aponta pra ele / dá um passo na direção dele.
                    if (c.targetRow < tile.Row) break;
                    if (c.targetRow == tile.Row && c.targetLane == tile.Lane) break;
                    sw.Select((SwitchState)Mathf.Clamp(c.targetLane - tile.Lane, -1, 1));
                    var tr = RailManager.Instance != null ? RailManager.Instance.GetRow(c.targetRow) : null;
                    if (tr != null && tr.HasTile(c.targetLane)) TapResolved?.Invoke(tr.Tiles[c.targetLane], tile, true);
                    break;
                case SwitchCommandKind.OverrideFromPress:
                    if (c.pressTile == tile) sw.Select((SwitchState)Mathf.Clamp(c.pressState + c.dir, -1, 1));
                    else sw.Nudge(c.dir);
                    break;
            }
        }
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

        // Impacto do Shield: velocidade cai pra _impactFrom e recupera com decay.
        // 1 = sem impacto ativo. Não mexe no Time.timeScale (mundo segue normal).
        float _impactFactor = 1f;
        float _impactFrom = 1f;
        float _impactTimer = 0f;
        float _impactDuration = 0f;

        /// <summary>
        /// Dispara a desaceleração de impacto (Barrier absorvida pelo Shield):
        /// a velocidade do player mergulha pra config.shieldImpactSpeedFactor e
        /// recupera ao longo de config.shieldImpactRecoverSeconds (decay). Simula
        /// a batida / perda de momentum sem afetar o resto do jogo.
        /// </summary>
        public void ApplyImpactSlowdown()
        {
            if (config == null) return;
            _impactFrom = Mathf.Clamp01(config.shieldImpactSpeedFactor);
            _impactDuration = Mathf.Max(0.01f, config.shieldImpactRecoverSeconds);
            _impactTimer = 0f;
            _impactFactor = _impactFrom;
        }

        void Update()
        {
            // Game Over → trava o player. Warmup permite movimento (player anda
            // através dos warmup tiles), só GameOver trava.
            if (GameManager.Instance != null && !GameManager.Instance.IsActive)
                return;

            // Morte já iniciada (fim do gap fatal): congela o player onde morreu.
            // Sem isso, com o state ainda em Playing (oferta de revive), ele voltaria
            // pro fim do trilho ao re-entrar na lógica de gap.
            if (_deathPending)
                return;

            // Decay do impacto: recupera _impactFactor de _impactFrom até 1.
            if (_impactFactor < 1f)
            {
                _impactTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_impactTimer / _impactDuration);
                _impactFactor = Mathf.SmoothStep(_impactFrom, 1f, t);
                if (t >= 1f) _impactFactor = 1f;
            }

            if (difficulty != null)
            {
                float baseSpeed = difficulty.CurrentTier.playerSpeed
                    * (config != null ? config.globalSpeedMultiplier : 1f);
                float multiplier = 1f;
                if (PowerUpManager.Instance != null)
                {
                    multiplier *= PowerUpManager.Instance.SpeedMultiplier; // SlowDown (≤1)
                    multiplier *= PowerUpManager.Instance.SpeedUpMultiplier; // SpeedUp debuff (≥1)
                }
                multiplier *= RunModifiers.SpeedMultiplier * RunModifiers.AssistSpeedMultiplier; // zona Rush / tutorial
                multiplier *= RunModifiers.CometSpeedMultiplier; // cometa
                // Warmup speed reduction (Idea 1).
                if (GameManager.Instance != null && GameManager.Instance.IsWarmup && config != null)
                    multiplier *= config.warmupSpeedMultiplier;
                multiplier *= _impactFactor; // impacto do Shield (decay)

                // Rampa: a velocidade do tier é alcançada com aceleração (sem tranco na troca).
                float ramp = config != null ? config.speedRampPerSecond : 0f;
                if (_rampedBaseSpeed <= 0f || ramp <= 0f) _rampedBaseSpeed = baseSpeed;
                else _rampedBaseSpeed = Mathf.MoveTowards(_rampedBaseSpeed, baseSpeed, ramp * Time.deltaTime);
                currentSpeed = _rampedBaseSpeed * multiplier;
            }

            // Input → comandos (relativo, toque no trilho, swipe-override) com buffer:
            // o que chega na lacuna vale pro próximo tile; e nos primeiros instantes da
            // lacuna ainda dá pra corrigir o destino (tolerância).
            if (_input != null)
            {
                int dir = _input.ConsumeDirection();
                if (dir != 0) _pending.Add(new SwitchCommand { kind = SwitchCommandKind.Relative, dir = dir, time = Time.unscaledTime });
                if (_input is ISwitchCommandInput ci)
                    while (ci.TryConsumeCommand(out var c)) _pending.Add(c);
            }
            ProcessPendingCommands();

            if (inGap)
                TickGap();
            else
                TickOnTile();

            // Durante o gap fatal, congela a distância — o trechinho extra até o
            // vazio não conta no score (você já estava morto no fim do trilho).
            if (!_gapIsFatal)
            {
                distanceTraveled = transform.position.z;
                if (difficulty != null)
                    difficulty.UpdateDistance(distanceTraveled);
            }
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
            {
                if (_gapIsFatal)
                {
                    // Fim da transição do switch sem tile pra pousar → morte agora
                    // (visualmente: o player seguiu o switch e chegou ao vazio).
                    // _deathPending congela o player AQUI (não volta pro fim do trilho).
                    inGap = false;
                    _gapIsFatal = false;
                    _deathPending = true;
                    TriggerGameOver(_gapFatalReason);
                    return;
                }
                ExitGap();
            }
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
            // Grace period pós-revive (Camada 3): trata dead-end/out-of-bounds como
            // sobrevivência (redireciona) em vez de game over.
            bool invincible = ReviveController.Instance != null && ReviveController.Instance.IsGracePeriodActive;

            // Tutorial: o jogador não pode perder. Conector pro vazio/fora da pista é
            // corrigido pra um trilho vizinho que existe (visível: o conector vira).
            if ((RunModifiers.TutorialProtection || RunModifiers.CometProtection) && currentTile.Switch != null)
            {
                var nr = RailManager.Instance != null ? RailManager.Instance.GetRow(currentTile.Row + 1) : null;
                if (nr != null && !nr.HasTile(targetLane))
                {
                    foreach (int s in new[] { 0, -1, 1 })
                    {
                        if (!nr.HasTile(currentTile.Lane + s)) continue;
                        currentTile.Switch.SetState((SwitchState)s);
                        targetLane = currentTile.Lane + s;
                        if (RunModifiers.TutorialProtection) TutorialRescued?.Invoke();
                        break;
                    }
                }
                invincible = true;
            }

            RowData nextRow = (RailManager.Instance != null)
                ? RailManager.Instance.GetRow(currentTile.Row + 1)
                : null;

            // Sem próxima linha registrada: anda o gap seguindo o switch e morre no fim.
            if (nextRow == null)
            {
                BeginFatalGap(targetLane, GameOverReason.OutOfBounds);
                return;
            }

            // OutOfBounds: com Ghost ou grace, clamp pra current lane (segue reto).
            // Senão, anda pra fora da pista e morre no fim da transição.
            if (targetLane < 0 || targetLane >= nextRow.MaxLanesAtSpawn)
            {
                if (isGhost || invincible)
                {
                    targetLane = currentTile.Lane;
                }
                else
                {
                    BeginFatalGap(targetLane, GameOverReason.OutOfBounds);
                    return;
                }
            }

            // DeadEnd: com Ghost, voa sobre. Com grace, redireciona pra lane populada
            // mais próxima. Senão, anda rumo ao tile que não existe e morre no fim.
            if (!nextRow.HasTile(targetLane))
            {
                if (isGhost)
                {
                    BeginGhostFlight(targetLane);
                    return;
                }
                if (invincible)
                {
                    int rescue = FindClosestPopulatedLane(nextRow, targetLane);
                    if (rescue >= 0) targetLane = rescue;
                    else { BeginFatalGap(targetLane, GameOverReason.DeadEnd); return; }
                }
                else
                {
                    BeginFatalGap(targetLane, GameOverReason.DeadEnd);
                    return;
                }
            }

            // OK — preparar gap até o tile destino.
            targetTile = nextRow.Tiles[targetLane];
            _gapStartPos = currentTile.EndPoint.position;
            _gapEndPos = targetTile.StartPoint.position;
            _gapStartPos.y = _playerY;
            _gapEndPos.y = _playerY;
            _gapDistance = Mathf.Max(0.01f, _gapEndPos.z - _gapStartPos.z);
            gapProgress = 0f;
            if (!inGap) _gapStartTime = Time.unscaledTime;
            inGap = true;
            _gapIsFatal = false;
        }

        // Gap FATAL: prepara a transição rumo a onde o tile-alvo ESTARIA (row+1,
        // targetLane) — usando a mesma matemática do ghost flight. Vale tanto pra
        // dead-end (tile não existe) quanto out-of-bounds (lane fora da pista, X
        // cai além da borda). O player percorre a diagonal do switch e o game over
        // dispara no fim do gap (TickGap), não no fim do trilho.
        void BeginFatalGap(int targetLane, GameOverReason reason)
        {
            if (config == null) { TriggerGameOver(reason); return; }

            Vector3 phantomCenter = TrackTile.ComputeWorldPosition(
                currentTile.Row + 1, targetLane, config.globalMaxLanes, config);
            Vector3 phantomStart = phantomCenter;
            phantomStart.z -= config.trackLength * 0.5f;

            _gapStartPos = currentTile.EndPoint.position;
            _gapEndPos = phantomStart;
            _gapStartPos.y = _playerY;
            _gapEndPos.y = _playerY;
            _gapDistance = Mathf.Max(0.01f, _gapEndPos.z - _gapStartPos.z);
            gapProgress = 0f;
            targetTile = null;       // não aterriza — morre no fim
            if (!inGap) _gapStartTime = Time.unscaledTime;
            inGap = true;
            _gapIsFatal = true;
            _gapFatalReason = reason;
        }

        // Inicia voo Ghost: configura phantom destination = onde o tile ESTARIA
        // se a row N+1 tivesse um no targetLane.
        void BeginGhostFlight(int targetLane)
        {
            GhostFlightStarted?.Invoke();
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
            _gapIsFatal = false;
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
        /// <summary>
        /// Reposiciona o player num tile (revive — Camada 3). Reseta o estado de gap/
        /// ghost flight e teleporta pro StartPoint do tile. Dispara OnTileEntered.
        /// </summary>
        public void RespawnAt(TrackTile tile)
        {
            if (tile == null || tile.StartPoint == null) return;
            inGap = false;
            gapProgress = 0f;
            targetTile = null;
            _gapIsFatal = false;
            _deathPending = false;
            _ghostFlightSkips = 0;
            currentTile = tile;
            Vector3 p = tile.StartPoint.position;
            p.y = _playerY;
            transform.position = p;
            OnTileEntered?.Invoke(currentTile);
        }

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
