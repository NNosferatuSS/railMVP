using System;
using UnityEngine;
using RailSwitchMVP.Collectibles;
using RailSwitchMVP.Config;
using RailSwitchMVP.Core;
using RailSwitchMVP.Obstacles;
using RailSwitchMVP.Player;
using RailSwitchMVP.Track;
using RailSwitchMVP.Visual;

namespace RailSwitchMVP.Gameplay
{
    /// <summary>
    /// COMETA — power-up "overpower": por N trilhos o player vira um cometa.
    ///  • Acelera (rampa) e o piloto automático escolhe a rota — mirando hazards e moedas.
    ///  • Não cai: conector pro vazio é corrigido (RunModifiers.CometProtection).
    ///  • Hazard/debuff tocado é ESMAGADO e vira moedas (SMASH!, em corrente).
    ///  • Ímã gigante puxa as moedas de todas as lanes por perto.
    ///  • No fim: ONDA DE CHOQUE esmaga todos os hazards à frente + alguns trilhos de carência.
    /// Estado de duração fica no PowerUpManager (tiles); aqui ficam os efeitos.
    /// Parâmetros no GameplayLayerConfig (Control Panel).
    /// </summary>
    public class CometController : MonoBehaviour
    {
        public static CometController Instance { get; private set; }

        [SerializeField] private GameplayLayerConfig config;

        /// <summary>Cosmético (áudio/HUD).</summary>
        public static event Action Started;
        public static event Action<int> Ended;            // nº de hazards da onda de choque
        public static event Action<Vector3, int> SmashedAt; // posição, moedas ganhas

        static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
        static readonly Color Ember = new Color(1f, 0.45f, 0.25f);

        GameplayLayerConfig _fallback;
        GameplayLayerConfig Cfg
        {
            get
            {
                if (config != null) return config;
                if (_fallback == null) _fallback = ScriptableObject.CreateInstance<GameplayLayerConfig>();
                return _fallback;
            }
        }

        public int DefaultTiles => Cfg.cometTiles;

        PlayerRailRider _rider;
        AuroraFX _fx;
        bool _active, _inShockwave;
        int _graceTiles, _chain, _shockCount;
        float _lastSmash = -10f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Start()
        {
            _rider = FindAnyObjectByType<PlayerRailRider>();
            _fx = FindAnyObjectByType<AuroraFX>();
            ObstacleBase.Smashed += OnSmashed;
            if (_rider != null) _rider.OnTileEntered += OnTile;
            if (GameManager.Instance != null) GameManager.Instance.OnGameOver += OnGameOver;
        }

        void OnDestroy()
        {
            ObstacleBase.Smashed -= OnSmashed;
            if (_rider != null) _rider.OnTileEntered -= OnTile;
            if (GameManager.Instance != null) GameManager.Instance.OnGameOver -= OnGameOver;
            ClearModifiers();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            var pm = PowerUpManager.Instance;
            bool on = pm != null && pm.HasComet && (GameManager.Instance == null || GameManager.Instance.IsActive);
            if (on && !_active) Begin();
            else if (!on && _active) End();

            var cfg = Cfg;
            float target = _active ? cfg.cometSpeedMultiplier : 1f;
            float span = Mathf.Max(0.05f, _active ? cfg.cometRampIn : cfg.cometRampOut);
            float rate = Mathf.Abs(cfg.cometSpeedMultiplier - 1f) / span;
            RunModifiers.CometSpeedMultiplier = Mathf.MoveTowards(RunModifiers.CometSpeedMultiplier, target, rate * Time.deltaTime);
            RunModifiers.CometMagnetRadius = _active ? cfg.cometMagnetRadius : 0f;
            RunModifiers.CometActive = _active;
            RunModifiers.CometProtection = _active || _graceTiles > 0;
        }

        void Begin()
        {
            _active = true;
            _chain = 0;
            var pos = PlayerPos();
            ComboFeverManager.RaisePopup("COMET!", 0, pos, Gold);
            if (PlayerCameraRig.Instance != null) PlayerCameraRig.Instance.ShakeMedium();
            if (AuroraJuice.Instance != null)
            {
                AuroraJuice.Instance.Flash(1f);
                AuroraJuice.Instance.Wave(pos, 2.6f, new Color(2.2f, 1.5f, 0.4f), 1.6f);
            }
            Started?.Invoke();
        }

        void End()
        {
            _active = false;
            _graceTiles = Cfg.cometGraceTiles;
            Shockwave();
            Ended?.Invoke(_shockCount);
        }

        // Fim do cometa: tudo que é hazard à frente (até cometShockwaveRange) vira moeda.
        void Shockwave()
        {
            _shockCount = 0;
            if (_rider == null) return;
            float z0 = _rider.transform.position.z - 1f;
            float z1 = z0 + Cfg.cometShockwaveRange;
            _inShockwave = true;
            foreach (var o in FindObjectsByType<ObstacleBase>(FindObjectsSortMode.None))
            {
                if (o == null) continue;
                float z = o.transform.position.z;
                if (z < z0 || z > z1) continue;
                o.Smash();
                _shockCount++;
            }
            _inShockwave = false;

            var pos = PlayerPos();
            if (AuroraJuice.Instance != null)
            {
                AuroraJuice.Instance.Flash(1f);
                AuroraJuice.Instance.Wave(pos, 3f, new Color(2.4f, 1.2f, 0.3f), 2.2f);
            }
            if (PlayerCameraRig.Instance != null) PlayerCameraRig.Instance.ShakeHeavy();
            ComboFeverManager.RaisePopup(_shockCount > 0 ? $"SHOCKWAVE  x{_shockCount}" : "SHOCKWAVE", 0, pos, Gold);
        }

        void OnSmashed(ObstacleBase o)
        {
            var pos = o.transform.position;
            var pm = PowerUpManager.Instance;
            int coins = Cfg.cometSmashCoins * (pm != null ? pm.CoinMultiplier : 1);
            if (CoinManager.Instance != null) CoinManager.Instance.AddCoins(coins);
            if (ComboFeverManager.Instance != null) ComboFeverManager.Instance.AddMeter(Cfg.cometFeverPerSmash);

            if (_fx != null)
            {
                _fx.Play(pos, Gold * 2f, 18, 1.8f, 2.4f, gravity: 1.2f);
                _fx.Play(pos, Ember * 2f, 10, 1.2f);
            }
            SmashedAt?.Invoke(pos, coins);
            if (_inShockwave) return;   // a onda de choque mostra um popup só

            _chain = Time.unscaledTime - _lastSmash < 2.5f ? _chain + 1 : 1;
            _lastSmash = Time.unscaledTime;
            ComboFeverManager.RaisePopup(_chain > 1 ? $"SMASH!  x{_chain}" : "SMASH!", coins, pos, Ember);
            if (PlayerCameraRig.Instance != null) PlayerCameraRig.Instance.ShakeLight();
            if (AuroraJuice.Instance != null) AuroraJuice.Instance.Flash(0.45f);
        }

        void OnTile(TrackTile t)
        {
            if (!_active && _graceTiles > 0) _graceTiles--;
        }

        void OnGameOver(GameOverReason r)
        {
            _active = false;
            _graceTiles = 0;
            ClearModifiers();
        }

        static void ClearModifiers()
        {
            RunModifiers.CometSpeedMultiplier = 1f;
            RunModifiers.CometMagnetRadius = 0f;
            RunModifiers.CometActive = false;
            RunModifiers.CometProtection = false;
        }

        Vector3 PlayerPos() => _rider != null ? _rider.transform.position : Vector3.zero;
    }
}
