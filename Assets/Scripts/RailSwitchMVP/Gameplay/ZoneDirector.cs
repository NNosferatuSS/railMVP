using System;
using UnityEngine;
using RailSwitchMVP.Collectibles;
using RailSwitchMVP.Config;
using RailSwitchMVP.Core;
using RailSwitchMVP.Player;

namespace RailSwitchMVP.Gameplay
{
    /// <summary>
    /// Zonas: trechos curtos com regra própria pra quebrar a monotonia.
    /// - RUSH: mais rápido, coins ×2.
    /// - CHUVA DE OURO: pista lotada de coins + moedas bônus nos decoys.
    /// - ECLIPSE: a pista escurece; só conectores, contornos e coletáveis brilham.
    /// Primeira zona após firstZoneAt metros de corrida (pós-GO); depois a cada
    /// zoneInterval metros, sem repetir a anterior. Completar a zona viva paga
    /// bônus de coins e barra de Fever.
    ///
    /// Também define a chance-base de moeda bônus por tier (isca de risco em decoy).
    /// Só escreve em RunModifiers; generator/player/power-ups leem de lá.
    /// </summary>
    public class ZoneDirector : MonoBehaviour
    {
        public static ZoneDirector Instance { get; private set; }

        [Tooltip("Agenda, duração, Rush, bônus e chance de moeda bônus. Tunável no Control Panel (Gameplay Layer).")]
        [SerializeField] private GameplayLayerConfig config;

        GameplayLayerConfig _fallbackConfig;
        GameplayLayerConfig Cfg => config != null ? config : FallbackConfig();

        GameplayLayerConfig FallbackConfig()
        {
            if (_fallbackConfig == null)
            {
                Debug.LogWarning("[ZoneDirector] GameplayLayerConfig não atribuído — usando os defaults do código.", this);
                _fallbackConfig = ScriptableObject.CreateInstance<GameplayLayerConfig>();
            }
            return _fallbackConfig;
        }

        public ZoneKind Active => RunModifiers.ActiveZone;
        public float Remaining01 => Active == ZoneKind.None ? 0f : Mathf.Clamp01(_left / Cfg.zoneDuration);

        public static event Action<ZoneKind> ZoneStarted;
        /// <summary>Zona terminou: tipo, coins de bônus pagos (0 se morreu).</summary>
        public static event Action<ZoneKind, int> ZoneEnded;

        PlayerRailRider _rider;
        float _runStartZ = float.NaN, _nextZoneAt, _left;
        ZoneKind _last = ZoneKind.None;
        bool _dead;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_fallbackConfig != null) Destroy(_fallbackConfig);
            RunModifiers.ActiveZone = ZoneKind.None;
            RunModifiers.SpeedMultiplier = 1f;
            RunModifiers.ZoneCoinMultiplier = 1;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameOver -= OnGameOver;
                GameManager.Instance.OnRevived -= OnRevived;
                GameManager.Instance.OnWarmupEnded -= OnRunStart;
            }
        }

        void Start()
        {
            _rider = FindFirstObjectByType<PlayerRailRider>();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameOver += OnGameOver;
                GameManager.Instance.OnRevived += OnRevived;
                GameManager.Instance.OnWarmupEnded += OnRunStart;
                if (GameManager.Instance.IsPlaying) OnRunStart();
            }
        }

        void OnRunStart()
        {
            if (_rider == null) return;
            _runStartZ = _rider.transform.position.z;
            _nextZoneAt = Cfg.firstZoneAt;
        }

        void OnGameOver(GameOverReason r)
        {
            _dead = true;
            if (Active != ZoneKind.None) Stop(paid: false);
        }

        void OnRevived() => _dead = false;

        void Update()
        {
            var dm = DifficultyManager.Instance;
            int tierIdx = dm != null ? dm.CurrentTierIndex : 0;
            RunModifiers.BonusCoinChance = Active == ZoneKind.GoldRain
                ? Cfg.goldRainBonusCoinChance
                : (tierIdx >= 2 ? Cfg.baseBonusCoinChance : 0f);

            if (_dead || _rider == null || float.IsNaN(_runStartZ)) return;
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

            if (Active != ZoneKind.None)
            {
                _left -= Time.deltaTime;
                if (_left <= 0f) Stop(paid: true);
                return;
            }

            if (RunDistance() >= _nextZoneAt && tierIdx >= Cfg.minTierIndex)
                Begin(Pick());
        }

        ZoneKind Pick()
        {
            var options = new[] { ZoneKind.Rush, ZoneKind.GoldRain, ZoneKind.Eclipse };
            ZoneKind k;
            int guard = 8;
            do { k = options[UnityEngine.Random.Range(0, options.Length)]; } while (k == _last && guard-- > 0);
            return k;
        }

        /// <summary>Força uma zona (debug / Control Panel).</summary>
        public void Begin(ZoneKind kind)
        {
            if (kind == ZoneKind.None) return;
            if (Active != ZoneKind.None) Stop(paid: false);
            RunModifiers.ActiveZone = kind;
            RunModifiers.SpeedMultiplier = kind == ZoneKind.Rush ? Cfg.rushSpeedMultiplier : 1f;
            RunModifiers.ZoneCoinMultiplier = kind == ZoneKind.Rush ? 2 : 1;
            _left = Cfg.zoneDuration;
            _last = kind;
            ZoneStarted?.Invoke(kind);
        }

        float RunDistance() => _rider != null && !float.IsNaN(_runStartZ) ? _rider.transform.position.z - _runStartZ : 0f;

        void Stop(bool paid)
        {
            var kind = Active;
            // Próxima zona conta a partir do FIM desta (senão, em alta velocidade, 20 s de zona
            // já cobrem o intervalo inteiro e as zonas ficam coladas).
            _nextZoneAt = RunDistance() + Cfg.zoneInterval;
            RunModifiers.ActiveZone = ZoneKind.None;
            RunModifiers.SpeedMultiplier = 1f;
            RunModifiers.ZoneCoinMultiplier = 1;
            int bonus = 0;
            if (paid)
            {
                bonus = Cfg.clearBonusCoins;
                if (CoinManager.Instance != null) CoinManager.Instance.AddCoins(bonus);
                var cf = ComboFeverManager.Instance;
                if (cf != null) cf.AddMeter(Cfg.clearFeverGain);
            }
            ZoneEnded?.Invoke(kind, bonus);
        }

        public static string Title(ZoneKind k) => k switch
        {
            ZoneKind.Rush => "RUSH",
            ZoneKind.GoldRain => "GOLD RAIN",
            ZoneKind.Eclipse => "ECLIPSE",
            _ => "",
        };

        public static string Subtitle(ZoneKind k) => k switch
        {
            ZoneKind.Rush => "faster · coins ×2",
            ZoneKind.GoldRain => "coins everywhere · bonus on detours",
            ZoneKind.Eclipse => "follow the connectors' light",
            _ => "",
        };

        public static Color Tint(ZoneKind k) => k switch
        {
            ZoneKind.Rush => new Color(1f, 0.35f, 0.55f),
            ZoneKind.GoldRain => new Color(1f, 0.78f, 0.25f),
            ZoneKind.Eclipse => new Color(0.6f, 0.5f, 1f),
            _ => Color.white,
        };
    }
}
