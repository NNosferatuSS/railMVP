using UnityEngine;
using RailSwitchMVP.Core;
using RailSwitchMVP.Player;

namespace RailSwitchMVP.Visual
{
    /// <summary>
    /// Juice do visual do player (Aurora): bob, squash no pouso, stretch na lacuna,
    /// inclinação nas trocas de lane, faíscas no rastro e auras de power-up
    /// (escudo / ghost / ímã / 2x). Só mexe em transforms e renderers filhos —
    /// o root do Player e a lógica não são tocados.
    /// </summary>
    public class AuroraPlayerJuice : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private PlayerRailRider rider;
        [SerializeField] private Transform body;
        [SerializeField] private Renderer pearl;
        [SerializeField] private Renderer crescent;
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private Renderer bubble;
        [SerializeField] private Transform magnetRing;
        [SerializeField] private Material sparkMaterial;

        [Header("Bob")]
        [SerializeField] private float bobAmplitude = 0.035f;
        [SerializeField] private float bobSpeed = 5f;

        [Header("Squash & stretch (mola)")]
        [SerializeField] private float stiffness = 260f;
        [SerializeField] private float damping = 14f;
        [SerializeField] private float landImpulse = 7f;
        [Tooltip("Alongamento em Z enquanto atravessa a lacuna.")]
        [SerializeField] private float gapStretch = 0.28f;

        [Header("Inclinação")]
        [SerializeField] private float leanPerSpeed = 4.5f;
        [SerializeField] private float maxLean = 28f;

        [Header("Auras (HDR)")]
        [ColorUsage(false, true)] [SerializeField] private Color shieldColor = new Color(0.35f, 1.5f, 2.2f);
        [ColorUsage(false, true)] [SerializeField] private Color ghostColor = new Color(1.3f, 0.6f, 2.2f);
        [ColorUsage(false, true)] [SerializeField] private Color doubleCoinsEmission = new Color(2.4f, 1.4f, 0.2f);

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        Vector3 _squash, _squashVel;       // offset de escala (x,y,z) relativo a 1
        float _lastX, _lean, _bubbleScale, _ringScale, _pop;
        bool _dead;
        MaterialPropertyBlock _mpb;
        Color _crescentEmissionBase;
        ParticleSystem _sparks;
        Vector3 _bubbleBase, _ringBase;

        public bool IsDead => _dead;
        public Vector3 BodyPosition => body != null ? body.position : transform.position;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (crescent != null && crescent.sharedMaterial != null)
                _crescentEmissionBase = crescent.sharedMaterial.GetColor(EmissionId);
            if (bubble != null) { _bubbleBase = bubble.transform.localScale; bubble.enabled = false; }
            if (magnetRing != null) { _ringBase = magnetRing.localScale; magnetRing.gameObject.SetActive(false); }
            CreateSparks();
            if (rider == null) rider = GetComponentInParent<PlayerRailRider>();
            _lastX = transform.position.x;
        }

        /// <summary>Pouso num tile novo: achata e volta com mola.</summary>
        public void Land()
        {
            _squashVel += new Vector3(landImpulse * 0.6f, -landImpulse, landImpulse * 0.6f);
        }

        /// <summary>Power-up ativado: "pop" de escala.</summary>
        public void PowerUpPop()
        {
            _pop = 1f;
            _squashVel += Vector3.one * landImpulse * 0.8f;
        }

        public void SetDead(bool dead)
        {
            _dead = dead;
            if (body != null) body.gameObject.SetActive(!dead);
            if (trail != null) { trail.emitting = !dead; if (dead) trail.Clear(); }
            if (_sparks != null)
            {
                var em = _sparks.emission;
                em.enabled = !dead;
            }
            if (dead && bubble != null) bubble.enabled = false;
            if (dead && magnetRing != null) magnetRing.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || body == null) return;
            float t = Time.time;

            // ---- stretch na lacuna (alvo) + mola
            bool inGap = false;
            if (rider != null && rider.CurrentTile != null && rider.CurrentTile.EndPoint != null)
                inGap = rider.transform.position.z > rider.CurrentTile.EndPoint.position.z + 0.05f;
            Vector3 target = inGap ? new Vector3(-gapStretch * 0.45f, -gapStretch * 0.45f, gapStretch) : Vector3.zero;

            Vector3 acc = -stiffness * (_squash - target) - damping * _squashVel;
            _squashVel += acc * dt;
            _squash += _squashVel * dt;
            _squash = Vector3.Max(_squash, new Vector3(-0.6f, -0.6f, -0.6f));

            // ---- inclinação pela velocidade lateral
            float x = transform.position.x;
            float vx = (x - _lastX) / dt;
            _lastX = x;
            float targetLean = Mathf.Clamp(-vx * leanPerSpeed, -maxLean, maxLean);
            _lean = Mathf.Lerp(_lean, targetLean, 1f - Mathf.Exp(-14f * dt));

            body.localPosition = new Vector3(0f, Mathf.Sin(t * bobSpeed) * bobAmplitude, 0f);
            body.localScale = Vector3.one + _squash;
            body.localRotation = Quaternion.Euler(0f, -_lean * 0.6f, _lean);

            UpdateAuras(dt, t);
        }

        void UpdateAuras(float dt, float t)
        {
            var pm = PowerUpManager.Instance;
            bool shield = !_dead && pm != null && pm.HasShield;
            bool ghost = !_dead && pm != null && pm.IsGhost;
            bool magnet = !_dead && pm != null && pm.HasMagnet;
            bool doubleCoins = !_dead && pm != null && pm.HasDoubleCoins;

            _pop = Mathf.MoveTowards(_pop, 0f, dt * 3f);

            if (bubble != null)
            {
                bool on = shield || ghost;
                _bubbleScale = Mathf.MoveTowards(_bubbleScale, on ? 1f : 0f, dt * (on ? 5f : 8f));
                bubble.enabled = _bubbleScale > 0.01f;
                if (bubble.enabled)
                {
                    float s = EaseOutBack(_bubbleScale) * (1f + _pop * 0.25f + Mathf.Sin(t * 6f) * 0.03f);
                    bubble.transform.localScale = _bubbleBase * s;
                    bubble.GetPropertyBlock(_mpb);
                    _mpb.SetColor(ColorId, ghost && !shield ? ghostColor : shieldColor);
                    bubble.SetPropertyBlock(_mpb);
                }
            }

            if (magnetRing != null)
            {
                _ringScale = Mathf.MoveTowards(_ringScale, magnet ? 1f : 0f, dt * 5f);
                bool on = _ringScale > 0.01f;
                if (magnetRing.gameObject.activeSelf != on) magnetRing.gameObject.SetActive(on);
                if (on)
                {
                    magnetRing.localScale = _ringBase * EaseOutBack(_ringScale) * (1f + Mathf.Sin(t * 8f) * 0.06f);
                    magnetRing.localRotation = Quaternion.Euler(90f, t * 160f, 0f);
                }
            }

            if (crescent != null)
            {
                crescent.GetPropertyBlock(_mpb);
                var e = doubleCoins
                    ? doubleCoinsEmission * (0.85f + 0.3f * Mathf.Sin(t * 10f))
                    : _crescentEmissionBase * (1f + _pop * 1.5f);
                _mpb.SetColor(EmissionId, e);
                crescent.SetPropertyBlock(_mpb);
            }
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.7f, c3 = c1 + 1f;
            float p = x - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        void CreateSparks()
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(transform, false);
            _sparks = go.AddComponent<ParticleSystem>();
            _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = _sparks.main;
            m.loop = true;
            m.duration = 1f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.9f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 1.6f, 2.0f), new Color(1.6f, 1.8f, 2.0f));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 80;
            var em = _sparks.emission;
            em.rateOverTime = 0f;
            em.rateOverDistance = 2.2f;
            var sh = _sparks.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.18f;
            var sol = _sparks.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
            var col = _sparks.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = _sparks.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = sparkMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _sparks.Play();
        }
    }
}
