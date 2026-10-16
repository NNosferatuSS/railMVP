using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using RailSwitchMVP.Collectibles;
using RailSwitchMVP.Config;
using RailSwitchMVP.Core;
using RailSwitchMVP.Obstacles;
using RailSwitchMVP.Player;
using RailSwitchMVP.PowerUps;
using RailSwitchMVP.Track;

namespace RailSwitchMVP.Gameplay
{
    /// <summary>
    /// Combo + Fever. Ações de habilidade enchem a barra de Fever:
    /// - CLUTCH: trocar o conector no último terço do trilho e pousar vivo.
    /// - RISCO: pousar num decoy (fora do caminho crítico) e seguir.
    /// - RASPÃO: passar colado num hazard da lane vizinha.
    /// - Coins / moeda bônus / gem / power-up.
    /// Ações seguidas (janela chainWindow) formam uma CORRENTE que multiplica o ganho.
    /// Parado demais, a barra escoa; impacto (shake) quebra a corrente.
    ///
    /// Barra cheia = Fever PRONTO. O JOGADOR escolhe quando soltar (botão do HUD,
    /// Espaço / ↑ / W). Fever: coins ×2 + ímã automático por feverDuration segundos
    /// (via RunModifiers) + aurora/música no máximo (AuroraJuice/AuroraAudio).
    /// Não guarda item — é um medidor, respeitando a diretriz "sem inventário".
    /// </summary>
    public class ComboFeverManager : MonoBehaviour
    {
        public static ComboFeverManager Instance { get; private set; }

        [Tooltip("Ganhos, corrente, escoamento e duração do Fever. Tunável no Control Panel (Gameplay Layer).")]
        [SerializeField] private GameplayLayerConfig config;

        GameplayLayerConfig _fallbackConfig;
        GameplayLayerConfig Cfg => config != null ? config : FallbackConfig();

        GameplayLayerConfig FallbackConfig()
        {
            if (_fallbackConfig == null)
            {
                Debug.LogWarning("[ComboFever] GameplayLayerConfig não atribuído — usando os defaults do código.", this);
                _fallbackConfig = ScriptableObject.CreateInstance<GameplayLayerConfig>();
            }
            return _fallbackConfig;
        }

        // ---------------------------------------------------------- estado público
        public float Meter01 => Mathf.Clamp01(_meter / Cfg.meterMax);
        public bool IsReady => _ready && !_fever;
        public bool IsFever => _fever;
        public float FeverRemaining01 => _fever ? Mathf.Clamp01(_feverLeft / Cfg.feverDuration) : 0f;
        public int Chain => _chain;
        public int BestChain { get; private set; }

        /// <summary>Popup de ação: rótulo, pontos, posição no mundo, cor.</summary>
        public static event Action<string, int, Vector3, Color> Popup;

        /// <summary>Popup de ação disparado por outros sistemas (ex.: cometa).</summary>
        public static void RaisePopup(string label, int points, Vector3 world, Color color) => Popup?.Invoke(label, points, world, color);
        public static event Action FeverReady;
        public static event Action FeverStarted;
        public static event Action FeverEnded;
        /// <summary>Elo novo na corrente (tamanho atual).</summary>
        public static event Action<int> ChainChanged;
        /// <summary>Tipo de ação (pra som): "clutch", "risky", "nearmiss".</summary>
        public static event Action<string> SkillAction;

        static readonly Color ClutchColor = new Color(0.45f, 1f, 0.85f);
        static readonly Color RiskyColor = new Color(1f, 0.55f, 0.75f);
        static readonly Color NearColor = new Color(1f, 0.85f, 0.35f);
        static readonly Color GoldColor = new Color(1f, 0.8f, 0.3f);

        float _meter, _lastGainTime = -99f, _lastChainTime = -99f, _feverLeft;
        int _chain;
        bool _ready, _fever, _dead;

        PlayerRailRider _rider;
        TrackTile _tile;
        bool _nudgedThisTile;
        float _lastNudgeProgress;
        readonly List<Transform> _pendingHazards = new List<Transform>();

        void Awake()
        {
            Instance = this;
            RunModifiers.Reset();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_fallbackConfig != null) Destroy(_fallbackConfig);
            RunModifiers.FeverCoinMultiplier = 1;
            RunModifiers.ForceMagnet = false;
            CollectibleCoin.Collected -= OnCoin;
            GemPickup.Collected -= OnGem;
            PowerUpBase.Collected -= OnPowerUp;
            SwitchController.AnyNudged -= OnNudge;
            PlayerCameraRig.ShakeRequested -= OnShake;
            if (_rider != null) _rider.OnTileEntered -= OnTileEntered;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameOver -= OnGameOver;
                GameManager.Instance.OnRevived -= OnRevived;
            }
        }

        void Start()
        {
            _rider = FindFirstObjectByType<PlayerRailRider>();
            if (_rider != null) _rider.OnTileEntered += OnTileEntered;
            CollectibleCoin.Collected += OnCoin;
            GemPickup.Collected += OnGem;
            PowerUpBase.Collected += OnPowerUp;
            SwitchController.AnyNudged += OnNudge;
            PlayerCameraRig.ShakeRequested += OnShake;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameOver += OnGameOver;
                GameManager.Instance.OnRevived += OnRevived;
            }
        }

        bool Scoring => !_dead && (GameManager.Instance == null || GameManager.Instance.IsPlaying);

        // ---------------------------------------------------------- ações

        void OnNudge(SwitchState s)
        {
            if (_rider == null || _tile == null) return;
            _nudgedThisTile = true;
            _lastNudgeProgress = TileProgress();
        }

        void OnTileEntered(TrackTile tile)
        {
            // CLUTCH: a troca que te trouxe até aqui foi feita no fim do trilho anterior.
            if (_tile != null && _nudgedThisTile && _lastNudgeProgress >= Cfg.clutchThreshold)
                Award(Cfg.clutchGain, "CLUTCH!", ClutchColor, "clutch");

            // RISCO: pousou num decoy (fora do caminho crítico garantido).
            if (tile != null && !tile.IsOnCriticalPath && tile.Row > 0)
                Award(Cfg.riskyGain, "RISKY!", RiskyColor, "risky");

            _tile = tile;
            _nudgedThisTile = false;
            _lastNudgeProgress = 0f;
            CollectNeighborHazards(tile);
        }

        void OnCoin(Vector3 p, int value)
        {
            if (value > 1) Award(Cfg.bonusCoinGain, $"+{value * RunModifiers.CoinMultiplier}", GoldColor, null, p);
            else Award(Cfg.coinGain, null, GoldColor, null, p, chain: false);
        }

        void OnGem(Vector3 p) => Award(Cfg.gemGain, "GEM!", new Color(0.85f, 0.6f, 1f), null, p);
        void OnPowerUp(Vector3 p) => Award(Cfg.powerUpGain, null, Color.white, null, p, chain: false);

        void OnShake(float intensity)
        {
            if (intensity < 0.25f || !Scoring) return;
            _meter = Mathf.Max(0f, _meter - Cfg.impactPenalty);
            BreakChain();
        }

        void OnGameOver(GameOverReason r)
        {
            _dead = true;
            BreakChain();
            if (_fever) EndFever();
        }

        void OnRevived() => _dead = false;

        // ---------------------------------------------------------- núcleo

        void Award(float gain, string label, Color color, string skill, Vector3? at = null, bool chain = true)
        {
            if (!Scoring) return;
            float now = Time.time;
            if (chain)
            {
                _chain = now - _lastChainTime <= Cfg.chainWindow ? _chain + 1 : 1;
                _lastChainTime = now;
                BestChain = Mathf.Max(BestChain, _chain);
                ChainChanged?.Invoke(_chain);
            }
            float mult = 1f + Mathf.Min(Cfg.chainBonusMax, Mathf.Max(0, _chain - 1) * Cfg.chainBonusPerLink);
            float g = gain * mult;
            _lastGainTime = now;

            if (!_fever)
            {
                bool wasReady = _ready;
                _meter = Mathf.Min(Cfg.meterMax, _meter + g);
                if (!wasReady && _meter >= Cfg.meterMax)
                {
                    _ready = true;
                    FeverReady?.Invoke();
                }
            }

            if (label != null)
            {
                var pos = at ?? (_rider != null ? _rider.transform.position : Vector3.zero);
                Popup?.Invoke(_chain > 1 && chain ? $"{label}  x{_chain}" : label, Mathf.RoundToInt(g), pos, color);
            }
            if (skill != null) SkillAction?.Invoke(skill);
        }

        void BreakChain()
        {
            if (_chain == 0) return;
            _chain = 0;
            ChainChanged?.Invoke(0);
        }

        /// <summary>Soma direto na barra (recompensa de zona etc.). Ignorado durante o Fever.</summary>
        public void AddMeter(float amount)
        {
            if (_fever || !Scoring) return;
            bool wasReady = _ready;
            _meter = Mathf.Min(Cfg.meterMax, _meter + amount);
            _lastGainTime = Time.time;
            if (!wasReady && _meter >= Cfg.meterMax)
            {
                _ready = true;
                FeverReady?.Invoke();
            }
        }

        /// <summary>Solta o Fever (se a barra estiver cheia). Botão do HUD / teclado.</summary>
        public bool TryActivateFever()
        {
            if (!IsReady || !Scoring) return false;
            _fever = true;
            _ready = false;
            _feverLeft = Cfg.feverDuration;
            RunModifiers.FeverCoinMultiplier = Cfg.feverCoinMultiplier;
            RunModifiers.ForceMagnet = true;
            FeverStarted?.Invoke();
            Popup?.Invoke("FEVER!", 0, _rider != null ? _rider.transform.position : Vector3.zero, new Color(1f, 0.7f, 1f));
            return true;
        }

        void EndFever()
        {
            _fever = false;
            _meter = 0f;
            RunModifiers.FeverCoinMultiplier = 1;
            RunModifiers.ForceMagnet = false;
            FeverEnded?.Invoke();
        }

        void Update()
        {
            float now = Time.time;

            if (_fever)
            {
                _feverLeft -= Time.deltaTime;
                _meter = Cfg.meterMax * Mathf.Clamp01(_feverLeft / Cfg.feverDuration);
                if (_feverLeft <= 0f) EndFever();
            }
            else if (!_ready && now - _lastGainTime > Cfg.decayDelay)
            {
                _meter = Mathf.Max(0f, _meter - Cfg.decayPerSecond * Time.deltaTime);
            }

            if (_chain > 0 && now - _lastChainTime > Cfg.chainWindow) BreakChain();

            // Teclado (editor/desktop).
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame))
                TryActivateFever();

            TrackNearMiss();
        }

        // ---------------------------------------------------------- raspão

        float TileProgress()
        {
            if (_tile == null || _tile.StartPoint == null || _tile.EndPoint == null || _rider == null) return 0f;
            return Mathf.InverseLerp(_tile.StartPoint.position.z, _tile.EndPoint.position.z, _rider.transform.position.z);
        }

        void CollectNeighborHazards(TrackTile tile)
        {
            _pendingHazards.Clear();
            if (tile == null || RailManager.Instance == null) return;
            var row = RailManager.Instance.GetRow(tile.Row);
            if (row == null || row.Tiles == null) return;
            for (int d = -1; d <= 1; d += 2)
            {
                int lane = tile.Lane + d;
                if (lane < 0 || lane >= row.Tiles.Length) continue;
                var n = row.Tiles[lane];
                if (n == null) continue;
                foreach (var o in n.GetComponentsInChildren<ObstacleBase>())
                    _pendingHazards.Add(o.transform);
            }
        }

        void TrackNearMiss()
        {
            if (_rider == null || _pendingHazards.Count == 0) return;
            var p = _rider.transform.position;
            float laneSpacing = 3f;
            for (int i = _pendingHazards.Count - 1; i >= 0; i--)
            {
                var h = _pendingHazards[i];
                if (h == null) { _pendingHazards.RemoveAt(i); continue; }
                if (p.z < h.position.z) continue;
                _pendingHazards.RemoveAt(i);
                if (Mathf.Abs(h.position.x - p.x) <= laneSpacing * Cfg.nearMissLanes + 0.1f)
                    Award(Cfg.nearMissGain, "NEAR MISS!", NearColor, "nearmiss", h.position);
            }
        }
    }
}
