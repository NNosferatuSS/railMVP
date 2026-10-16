using UnityEngine;
using UnityEngine.Rendering;
using RailSwitchMVP.Collectibles;
using RailSwitchMVP.Gameplay;
using RailSwitchMVP.Config;
using RailSwitchMVP.Core;
using RailSwitchMVP.Player;
using RailSwitchMVP.PowerUps;
using RailSwitchMVP.Track;

namespace RailSwitchMVP.Visual
{
    /// <summary>
    /// Maestro do "juice" da direção Aurora. Só cosmético: escuta eventos de gameplay
    /// e responde com luz, pulsos de post-processing, ondas nos trilhos e partículas.
    /// Não altera tempo, câmera, velocidade nem regras.
    ///
    /// - Publica globais pro shader Surface: poça de luz sob o player e onda de choque.
    /// - Dois Volumes extras (flash / hit) com peso animado = pulsos de tela.
    /// - Véus de aurora intensificam a cada tier (MPB, não toca no material).
    /// - Partículas de velocidade passando pela câmera, densidade pela velocidade do player.
    /// </summary>
    public class AuroraJuice : MonoBehaviour
    {
        public static AuroraJuice Instance { get; private set; }
        /// <summary>Posição do player (mundo). Usada pelos tiles pra montar/desmontar.</summary>
        public static Vector3 PlayerPos { get; private set; }
        /// <summary>Quanto de pista (em Z, a partir do player) cabe na tela até a borda de cima.</summary>
        public static float VisibleAhead { get; private set; } = 40f;

        [Header("Refs")]
        [SerializeField] private PlayerRailRider player;
        [SerializeField] private AuroraFX fx;
        [SerializeField] private AuroraPlayerJuice playerJuice;
        [SerializeField] private Volume flashVolume;
        [SerializeField] private Volume hitVolume;
        [SerializeField] private Renderer[] veils;
        [SerializeField] private Material speedParticleMaterial;

        [Header("Poça de luz sob o player")]
        [ColorUsage(false, true)] [SerializeField] private Color glowColor = new Color(0.25f, 0.95f, 1.1f);
        [SerializeField] private float glowIntensity = 0.5f;
        [SerializeField] private float glowRadius = 2.0f;

        [Header("Onda de choque nos trilhos")]
        [ColorUsage(false, true)] [SerializeField] private Color waveColor = new Color(0.3f, 1.6f, 1.4f);
        [ColorUsage(false, true)] [SerializeField] private Color deathWaveColor = new Color(2.2f, 0.25f, 0.45f);
        [SerializeField] private float waveSpeed = 22f;
        [SerializeField] private float waveWidth = 1.6f;
        [SerializeField] private float waveDuration = 0.9f;
        [SerializeField] private float landWaveStrength = 0.55f;

        [Header("Pulsos de tela (peso dos Volumes)")]
        [SerializeField] private float flashDecay = 3.2f;
        [SerializeField] private float hitDecay = 2.2f;
        [SerializeField] private float coinFlash = 0.12f;
        [SerializeField] private float gemFlash = 0.45f;
        [SerializeField] private float powerUpFlash = 0.75f;
        [SerializeField] private float tierFlash = 1f;

        [Header("Aurora por tier")]
        [Tooltip("Intensidade extra dos véus por tier (acumula).")]
        [SerializeField] private float veilPerTier = 0.12f;
        [SerializeField] private float veilMaxExtra = 0.9f;
        [SerializeField] private float veilBoostOnTier = 1.2f;

        [Header("Partículas de velocidade")]
        [SerializeField] private float speedParticlesPerUnit = 0.9f;

        static readonly int PlayerPosId = Shader.PropertyToID("_AuroraPlayerPos");
        static readonly int GlowColorId = Shader.PropertyToID("_AuroraGlowColor");
        static readonly int WaveId = Shader.PropertyToID("_AuroraWave");
        static readonly int WaveParamsId = Shader.PropertyToID("_AuroraWaveParams");
        static readonly int WaveColorId = Shader.PropertyToID("_AuroraWaveColor");
        static readonly int TimeId = Shader.PropertyToID("_AuroraTime");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int DimId = Shader.PropertyToID("_AuroraDim");
        static readonly int VeilBottomId = Shader.PropertyToID("_ColorBottom");

        [Header("Fever / Zonas")]
        [ColorUsage(false, true)] [SerializeField] private Color feverGlow = new Color(1.3f, 0.85f, 0.25f);
        [ColorUsage(false, true)] [SerializeField] private Color feverWave = new Color(2.2f, 1.4f, 0.4f);
        [ColorUsage(false, true)] [SerializeField] private Color rushVeil = new Color(1.6f, 0.25f, 0.6f);
        [ColorUsage(false, true)] [SerializeField] private Color goldVeil = new Color(1.6f, 1.1f, 0.2f);
        [ColorUsage(false, true)] [SerializeField] private Color eclipseVeil = new Color(0.45f, 0.3f, 1.4f);
        float _dim, _zoneTintK;
        Color _zoneVeil;
        Color[] _veilBottomBase;

        float _flash, _hit, _veilBoost, _veilTierExtra;
        float[] _veilBase;
        MaterialPropertyBlock _mpb;
        ParticleSystem _speed;
        Transform _speedBox;
        Camera _cam;
        bool _subscribed;

        void Awake()
        {
            Instance = this;
            _mpb = new MaterialPropertyBlock();
            if (veils != null)
            {
                _veilBase = new float[veils.Length];
                for (int i = 0; i < veils.Length; i++)
                    _veilBase[i] = veils[i] != null && veils[i].sharedMaterial != null
                        ? veils[i].sharedMaterial.GetFloat(IntensityId) : 1f;
            }
            CreateSpeedParticles();
            Wave(Vector3.zero, 0f, waveColor);
            Shader.SetGlobalFloat(DimId, 0f);
            if (veils != null)
            {
                _veilBottomBase = new Color[veils.Length];
                for (int i = 0; i < veils.Length; i++)
                    _veilBottomBase[i] = veils[i] != null && veils[i].sharedMaterial != null
                        ? veils[i].sharedMaterial.GetColor(VeilBottomId) : Color.white;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unsubscribe();
        }

        void Start() => Subscribe();

        void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            if (player == null) player = FindFirstObjectByType<PlayerRailRider>();
            if (player != null) player.OnTileEntered += OnLand;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameOver += OnGameOver;
                GameManager.Instance.OnRevived += OnRevived;
            }
            if (DifficultyManager.Instance != null) DifficultyManager.Instance.OnTierChanged += OnTier;
            if (PowerUpManager.Instance != null) PowerUpManager.Instance.OnPowerUpActivated += OnPowerUpActivated;
            CollectibleCoin.Collected += OnCoin;
            GemPickup.Collected += OnGem;
            PowerUpBase.Collected += OnPowerUpCollected;
            PlayerCameraRig.ShakeRequested += OnShake;
            ComboFeverManager.FeverStarted += OnFeverStarted;
            ComboFeverManager.FeverReady += OnFeverReady;
            ComboFeverManager.SkillAction += OnSkill;
            ZoneDirector.ZoneStarted += OnZoneStarted;
            ZoneDirector.ZoneEnded += OnZoneEnded;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (player != null) player.OnTileEntered -= OnLand;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameOver -= OnGameOver;
                GameManager.Instance.OnRevived -= OnRevived;
            }
            if (DifficultyManager.Instance != null) DifficultyManager.Instance.OnTierChanged -= OnTier;
            if (PowerUpManager.Instance != null) PowerUpManager.Instance.OnPowerUpActivated -= OnPowerUpActivated;
            CollectibleCoin.Collected -= OnCoin;
            GemPickup.Collected -= OnGem;
            PowerUpBase.Collected -= OnPowerUpCollected;
            PlayerCameraRig.ShakeRequested -= OnShake;
            ComboFeverManager.FeverStarted -= OnFeverStarted;
            ComboFeverManager.FeverReady -= OnFeverReady;
            ComboFeverManager.SkillAction -= OnSkill;
            ZoneDirector.ZoneStarted -= OnZoneStarted;
            ZoneDirector.ZoneEnded -= OnZoneEnded;
            Shader.SetGlobalFloat(DimId, 0f);
        }

        void OnFeverStarted()
        {
            _flash = 1f;
            _veilBoost = Mathf.Max(_veilBoost, 1.6f);
            if (player != null) Wave(player.transform.position, 3f, feverWave, 2.2f);
            if (playerJuice != null) playerJuice.PowerUpPop();
        }

        void OnFeverReady() { _flash = Mathf.Max(_flash, 0.5f); }

        void OnSkill(string kind)
        {
            _flash = Mathf.Max(_flash, kind == "nearmiss" ? 0.22f : 0.3f);
            if (player != null && kind != "nearmiss") Wave(player.transform.position, 0.9f, kind == "risky" ? new Color(2f, 0.6f, 1.2f) : waveColor);
        }

        void OnZoneStarted(Core.ZoneKind k)
        {
            _flash = 1f;
            _veilBoost = Mathf.Max(_veilBoost, 1.2f);
            _zoneVeil = k == Core.ZoneKind.Rush ? rushVeil : k == Core.ZoneKind.GoldRain ? goldVeil : eclipseVeil;
            if (player != null) Wave(player.transform.position, 2.4f, _zoneVeil, 2.2f);
        }

        void OnZoneEnded(Core.ZoneKind k, int bonus)
        {
            if (bonus > 0) { _flash = Mathf.Max(_flash, 0.7f); if (player != null) Wave(player.transform.position, 1.6f, waveColor, 1.6f); }
        }

        // ------------------------------------------------------------ eventos

        void OnLand(TrackTile tile)
        {
            if (player == null) return;
            Wave(player.transform.position, landWaveStrength, waveColor);
            if (playerJuice != null) playerJuice.Land();
        }

        void OnCoin(Vector3 p, int value) { _flash = Mathf.Max(_flash, value > 1 ? gemFlash : coinFlash); }
        void OnGem(Vector3 p) { _flash = Mathf.Max(_flash, gemFlash); Wave(p, 0.6f, new Color(1.4f, 0.6f, 2.2f)); }

        void OnPowerUpCollected(Vector3 p)
        {
            _flash = Mathf.Max(_flash, powerUpFlash);
            Wave(p, 1.3f, waveColor);
        }

        void OnPowerUpActivated(PowerUpType type)
        {
            if (playerJuice != null) playerJuice.PowerUpPop();
        }

        void OnTier(DifficultyTier tier)
        {
            _flash = Mathf.Max(_flash, tierFlash);
            _veilBoost = veilBoostOnTier;
            _veilTierExtra = Mathf.Min(veilMaxExtra, _veilTierExtra + veilPerTier);
            if (player != null) Wave(player.transform.position, 2.2f, new Color(0.9f, 1.4f, 2.4f), 2.4f);
        }

        void OnShake(float intensity)
        {
            // light 0.15 / medium 0.3 / heavy 0.6 (defaults) → hit 0.25 / 0.5 / 1
            _hit = Mathf.Max(_hit, Mathf.Clamp01(intensity / 0.6f));
        }

        void OnGameOver(GameOverReason reason)
        {
            _hit = 1f;
            if (player != null)
            {
                var p = player.transform.position;
                if (playerJuice != null) p = playerJuice.BodyPosition;
                Wave(p, 2.5f, deathWaveColor, 1.6f);
                if (fx != null) fx.PlayDeath(p);
            }
            if (playerJuice != null) playerJuice.SetDead(true);
        }

        void OnRevived()
        {
            if (playerJuice != null) playerJuice.SetDead(false);
            _flash = 1f;
            if (player != null) Wave(player.transform.position, 2f, waveColor, 1.6f);
        }

        // ------------------------------------------------------------ API

        /// <summary>Flash de tela (bloom) — 0..1.</summary>
        public void Flash(float amount) => _flash = Mathf.Max(_flash, amount);

        /// <summary>Onda de choque nos trilhos a partir de <paramref name="pos"/>.</summary>
        public void Wave(Vector3 pos, float strength, Color color, float durationScale = 1f)
        {
            Shader.SetGlobalVector(WaveId, new Vector4(pos.x, Time.unscaledTime, pos.z, strength));
            Shader.SetGlobalVector(WaveParamsId, new Vector4(waveSpeed, waveWidth, waveDuration * durationScale, 0f));
            Shader.SetGlobalColor(WaveColorId, color);
        }

        // ------------------------------------------------------------ frame

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            Shader.SetGlobalFloat(TimeId, Time.unscaledTime);

            if (player != null)
            {
                var pp = playerJuice != null ? playerJuice.BodyPosition : player.transform.position;
                PlayerPos = pp;
                bool dead = playerJuice != null && playerJuice.IsDead;
                Shader.SetGlobalVector(PlayerPosId, new Vector4(pp.x, pp.y, pp.z, glowRadius));
                bool fever = RunModifiers.FeverCoinMultiplier > 1 || RunModifiers.CometActive;
                var baseGlow = fever ? Color.Lerp(feverGlow, glowColor, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f)) : glowColor;
                var g = baseGlow * (dead ? 0f : glowIntensity * (fever ? 1.8f : 1f));
                Shader.SetGlobalColor(GlowColorId, new Color(g.r, g.g, g.b, 1f));
            }

            _flash = Mathf.MoveTowards(_flash, 0f, flashDecay * dt * Mathf.Max(0.35f, _flash));
            _hit = Mathf.MoveTowards(_hit, 0f, hitDecay * dt * Mathf.Max(0.35f, _hit));
            if (flashVolume != null) flashVolume.weight = Ease(_flash);
            if (hitVolume != null) hitVolume.weight = Ease(_hit);

            _veilBoost = Mathf.MoveTowards(_veilBoost, 0f, dt * 0.5f);
            var zone = RunModifiers.ActiveZone;
            _dim = Mathf.MoveTowards(_dim, zone == ZoneKind.Eclipse ? 1f : 0f, dt * 1.2f);
            Shader.SetGlobalFloat(DimId, _dim);
            _zoneTintK = Mathf.MoveTowards(_zoneTintK, zone != ZoneKind.None ? 1f : 0f, dt * 0.8f);
            if (RunModifiers.FeverCoinMultiplier > 1) _veilBoost = Mathf.Max(_veilBoost, 0.8f);
            if (veils != null && _veilBase != null)
            {
                float k = 1f + _veilTierExtra + _veilBoost;
                for (int i = 0; i < veils.Length; i++)
                {
                    if (veils[i] == null) continue;
                    veils[i].GetPropertyBlock(_mpb);
                    _mpb.SetFloat(IntensityId, _veilBase[i] * k);
                    if (_veilBottomBase != null)
                        _mpb.SetColor(VeilBottomId, Color.Lerp(_veilBottomBase[i], _zoneVeil, _zoneTintK * 0.8f));
                    veils[i].SetPropertyBlock(_mpb);
                }
            }

            UpdateSpeedParticles();
            UpdateVisibleAhead();
        }

        void UpdateVisibleAhead()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            var ray = _cam.ViewportPointToRay(new Vector3(0.5f, 1f, 0f));
            if (ray.direction.y >= -1e-3f) { VisibleAhead = 200f; return; }
            float t = -ray.origin.y / ray.direction.y;
            VisibleAhead = Mathf.Max(10f, ray.origin.z + ray.direction.z * t - PlayerPos.z);
        }

        static float Ease(float x) => x * x * (3f - 2f * x);

        // ------------------------------------------------------------ partículas de velocidade

        void CreateSpeedParticles()
        {
            var go = new GameObject("SpeedMotes");
            go.transform.SetParent(transform, false);
            _speedBox = go.transform;
            _speed = go.AddComponent<ParticleSystem>();
            _speed.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = _speed.main;
            m.loop = true;
            m.playOnAwake = true;
            m.duration = 5f;
            m.startLifetime = 4f;
            m.startSpeed = 0f;
            m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.5f, 1.2f, 1.4f, 0.8f), new Color(0.9f, 0.6f, 1.6f, 0.6f));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 220;
            var em = _speed.emission; em.rateOverTime = 0f;
            var sh = _speed.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(46f, 14f, 30f);
            var col = _speed.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = _speed.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = speedParticleMaterial;
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.cameraVelocityScale = 0.06f;
            r.lengthScale = 1.5f;
            r.velocityScale = 0f;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            _speed.Play();
        }

        void UpdateSpeedParticles()
        {
            if (_speed == null) return;
            if (_cam == null) _cam = Camera.main;
            float speed = player != null ? player.CurrentSpeed : 0f;
            var c = _cam != null ? _cam.transform.position : PlayerPos;
            // Caixa à frente e abaixo do plano das pistas: passa pelas laterais e pelas lacunas,
            // longe da área de decisão.
            _speedBox.position = new Vector3(c.x, -6f, PlayerPos.z + 48f);
            var em = _speed.emission;
            bool dead = playerJuice != null && playerJuice.IsDead;
            float zoneK = RunModifiers.ActiveZone == ZoneKind.Rush ? 2.2f : (RunModifiers.FeverCoinMultiplier > 1 ? 1.6f : 1f);
            em.rateOverTime = dead ? 0f : speed * speedParticlesPerUnit * zoneK;
        }
    }
}
