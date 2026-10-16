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
        TrailRenderer _ghostBridge;
        Gradient _trailBase, _trailGhost;
        Color _pearlEmissionBase;
        float _ghostK;
        Vector3 _bubbleBase, _ringBase;
        Gradient _trailComet;
        float _trailWidthBase = 1f, _cometK;
        ParticleSystem.MinMaxGradient _sparkColorBase;

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
            if (pearl != null && pearl.sharedMaterial != null) _pearlEmissionBase = pearl.sharedMaterial.GetColor(EmissionId);
            if (trail != null)
            {
                _trailBase = trail.colorGradient;
                _trailGhost = new Gradient();
                _trailGhost.SetKeys(new[] { new GradientColorKey(new Color(0.85f, 0.6f, 1f), 0), new GradientColorKey(new Color(0.55f, 0.3f, 1f), 1) },
                                    new[] { new GradientAlphaKey(0.95f, 0), new GradientAlphaKey(0f, 1) });
                CreateGhostBridge();
                _trailWidthBase = trail.widthMultiplier;
                _trailComet = new Gradient();
                _trailComet.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.97f, 0.85f), 0), new GradientColorKey(new Color(1f, 0.62f, 0.15f), 0.45f), new GradientColorKey(new Color(1f, 0.25f, 0.2f), 1) },
                                    new[] { new GradientAlphaKey(1f, 0), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1) });
            }
            if (_sparks != null) _sparkColorBase = _sparks.main.startColor;
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

            // Mola com sub-passos fixos: estável mesmo com engasgo de frame (Euler simples
            // com stiffness alto explode quando dt passa de ~0.06 s).
            float remaining = Mathf.Min(dt, 0.1f);
            const float step = 1f / 120f;
            while (remaining > 0f)
            {
                float h = Mathf.Min(step, remaining);
                remaining -= h;
                Vector3 acc = -stiffness * (_squash - target) - damping * _squashVel;
                _squashVel += acc * h;
                _squash += _squashVel * h;
            }
            _squash = Vector3.Max(Vector3.Min(_squash, new Vector3(0.6f, 0.6f, 0.6f)), new Vector3(-0.5f, -0.5f, -0.5f));

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
            bool doubleCoins = !_dead && ((pm != null && pm.HasDoubleCoins) || RailSwitchMVP.Core.RunModifiers.FeverCoinMultiplier > 1);

            _pop = Mathf.MoveTowards(_pop, 0f, dt * 3f);
            UpdateGhost(dt, t, ghost);
            UpdateComet(dt, t, !_dead && pm != null && pm.HasComet);

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

        // Ghost: rastro largo violeta embaixo da pérola enquanto ela atravessa o vazio —
        // a "ponte fantasma" deixa claro que você NÃO caiu.
        void CreateGhostBridge()
        {
            var go = new GameObject("GhostBridge");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, -0.3f / Mathf.Max(0.01f, transform.lossyScale.y), 0f);
            _ghostBridge = go.AddComponent<TrailRenderer>();
            _ghostBridge.time = 0.7f;
            _ghostBridge.minVertexDistance = 0.1f;
            _ghostBridge.widthMultiplier = 1.1f;
            _ghostBridge.widthCurve = new AnimationCurve(new Keyframe(0, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1, 0f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.7f, 1f), 0), new GradientColorKey(new Color(0.5f, 0.3f, 1f), 1) },
                      new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0f, 1) });
            _ghostBridge.colorGradient = g;
            _ghostBridge.alignment = LineAlignment.TransformZ;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // deitado na horizontal
            _ghostBridge.sharedMaterial = trail.sharedMaterial;
            _ghostBridge.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ghostBridge.emitting = false;
        }

        void UpdateGhost(float dt, float t, bool ghost)
        {
            bool flying = rider != null && rider.IsGhostFlying && !_dead;
            if (_ghostBridge != null) _ghostBridge.emitting = flying;
            _ghostK = Mathf.MoveTowards(_ghostK, ghost ? 1f : 0f, dt * 4f);

            if (trail != null && _trailBase != null) trail.colorGradient = _ghostK > 0.5f ? _trailGhost : _trailBase;

            if (pearl != null)
            {
                pearl.GetPropertyBlock(_mpb);
                var ghostE = new Color(1.1f, 0.55f, 2.2f) * (flying ? 1.6f : 0.6f + 0.4f * Mathf.Sin(t * 12f));
                _mpb.SetColor(EmissionId, Color.Lerp(_pearlEmissionBase, ghostE, _ghostK));
                pearl.SetPropertyBlock(_mpb);
                // tremulação "fantasma" na escala da pérola
                float flick = 1f - _ghostK * (0.12f + 0.08f * Mathf.Sin(t * 31f));
                pearl.transform.localScale = Vector3.one * flick;
            }
        }

        // Cometa: rastro dourado 2,6x mais largo, chuva de faíscas e pérola em brasa.
        void UpdateComet(float dt, float t, bool comet)
        {
            _cometK = Mathf.MoveTowards(_cometK, comet ? 1f : 0f, dt * (comet ? 4f : 1.5f));
            if (trail != null)
            {
                trail.widthMultiplier = _trailWidthBase * (1f + 1.6f * _cometK);
                if (_cometK > 0.3f && _trailComet != null) trail.colorGradient = _trailComet;
            }
            if (_sparks != null)
            {
                var em = _sparks.emission;
                em.rateOverDistance = 2.2f + 7f * _cometK;
                var m = _sparks.main;
                m.startColor = _cometK > 0.3f
                    ? new ParticleSystem.MinMaxGradient(new Color(2.4f, 1.6f, 0.4f), new Color(2.4f, 0.7f, 0.3f))
                    : _sparkColorBase;
            }
            if (pearl != null && _cometK > 0.01f)
            {
                pearl.GetPropertyBlock(_mpb);
                var hot = new Color(2.6f, 1.7f, 0.5f) * (1.2f + 0.3f * Mathf.Sin(t * 18f));
                _mpb.SetColor(EmissionId, Color.Lerp(_pearlEmissionBase, hot, _cometK));
                pearl.SetPropertyBlock(_mpb);
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
