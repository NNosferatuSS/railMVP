using UnityEngine;
using RailSwitchMVP.Collectibles;
using RailSwitchMVP.PowerUps;

namespace RailSwitchMVP.Visual
{
    /// <summary>
    /// VFX cosméticos da direção Aurora, disparados pelos eventos estáticos de
    /// coleta (coin, gem, power-up). Pool pequeno de bursts criados em código —
    /// não precisa de prefab. Não altera tempo, câmera nem regras.
    /// </summary>
    public class AuroraFX : MonoBehaviour
    {
        [SerializeField] private Material burstMaterial;
        [SerializeField] private Material flashMaterial;
        [SerializeField] private Material ringMaterial;
        [SerializeField] private int poolSize = 10;

        [Header("Cores (HDR)")]
        [ColorUsage(true, true)] [SerializeField] private Color coinColor = new Color(2.2f, 1.3f, 0.25f, 1f);
        [ColorUsage(true, true)] [SerializeField] private Color gemColor = new Color(1.5f, 0.7f, 2.4f, 1f);
        [ColorUsage(true, true)] [SerializeField] private Color powerUpColor = new Color(0.6f, 2.0f, 2.4f, 1f);
        [ColorUsage(true, true)] [SerializeField] private Color deathColor = new Color(2.4f, 0.35f, 0.5f, 1f);
        [ColorUsage(true, true)] [SerializeField] private Color deathCoreColor = new Color(2.2f, 2.4f, 2.6f, 1f);

        struct Burst { public ParticleSystem sparks; public ParticleSystem flash; public ParticleSystem ring; }
        Burst[] _pool;
        int _next;

        void Awake()
        {
            _pool = new Burst[Mathf.Max(1, poolSize)];
            for (int i = 0; i < _pool.Length; i++) _pool[i] = CreateBurst(i);
        }

        void OnEnable()
        {
            CollectibleCoin.Collected += OnCoin;
            GemPickup.Collected += OnGem;
            PowerUpBase.Collected += OnPowerUp;
        }

        void OnDisable()
        {
            CollectibleCoin.Collected -= OnCoin;
            GemPickup.Collected -= OnGem;
            PowerUpBase.Collected -= OnPowerUp;
        }

        void OnCoin(Vector3 p) => Play(p, coinColor, 10, 1.0f, 0.9f);
        void OnGem(Vector3 p) => Play(p, gemColor, 18, 1.4f, 1.6f);
        void OnPowerUp(Vector3 p) => Play(p, powerUpColor, 26, 2.0f, 2.6f);

        /// <summary>Estilhaço do player: dois bursts sobrepostos (coral + núcleo branco) e anel grande.</summary>
        public void PlayDeath(Vector3 pos)
        {
            Play(pos, deathColor, 24, 2.6f, 3.2f, gravity: 1.5f);
            Play(pos, deathCoreColor, 14, 1.6f, 0f, gravity: 0.8f);
        }

        public void Play(Vector3 pos, Color color, int count, float scale, float ringSize = 0f, float gravity = 0f)
        {
            if (_pool == null) return;
            var b = _pool[_next];
            _next = (_next + 1) % _pool.Length;

            b.sparks.transform.position = pos;
            var main = b.sparks.main;
            main.startColor = color;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f * scale, 0.26f * scale);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f * scale, 7f * scale);
            main.gravityModifier = gravity;
            b.sparks.Clear(true);
            b.sparks.Emit(count);

            var fm = b.flash.main;
            fm.startColor = color;
            fm.startSize = 1.6f * scale;
            b.flash.Emit(1);

            if (ringSize > 0f && b.ring != null)
            {
                b.ring.transform.position = pos;
                var rm = b.ring.main;
                rm.startColor = color;
                rm.startSize = ringSize;
                b.ring.Emit(1);
            }
        }

        Burst CreateBurst(int i)
        {
            var root = new GameObject($"AuroraBurst_{i}");
            root.transform.SetParent(transform, false);

            var sparks = root.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = sparks.main;
            m.playOnAwake = false;
            m.loop = false;
            m.duration = 0.5f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.gravityModifier = 0f;
            m.maxParticles = 40;
            var em = sparks.emission; em.enabled = false;
            var sh = sparks.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f;
            var sol = sparks.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
            var vol = sparks.limitVelocityOverLifetime; vol.enabled = true; vol.dampen = 0.12f; vol.limit = 0.6f;
            var r = sparks.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = burstMaterial;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            var flashGo = new GameObject("Flash");
            flashGo.transform.SetParent(root.transform, false);
            var flash = flashGo.AddComponent<ParticleSystem>();
            flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var fm = flash.main;
            fm.playOnAwake = false;
            fm.loop = false;
            fm.duration = 0.3f;
            fm.startLifetime = 0.16f;
            fm.startSpeed = 0f;
            fm.simulationSpace = ParticleSystemSimulationSpace.Local;
            fm.maxParticles = 2;
            var fem = flash.emission; fem.enabled = false;
            var fsh = flash.shape; fsh.enabled = false;
            var fsol = flash.sizeOverLifetime; fsol.enabled = true;
            fsol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.3f, 1, 1.2f));
            var fcol = flash.colorOverLifetime; fcol.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            fcol.color = g;
            var fr = flash.GetComponent<ParticleSystemRenderer>();
            fr.sharedMaterial = flashMaterial != null ? flashMaterial : burstMaterial;
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fr.receiveShadows = false;

            // alpha das faíscas também some
            var scol = sparks.colorOverLifetime; scol.enabled = true; scol.color = g;

            // Anel de choque: billboard que expande e some
            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(root.transform, false);
            var ring = ringGo.AddComponent<ParticleSystem>();
            ring.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var rmn = ring.main;
            rmn.playOnAwake = false;
            rmn.loop = false;
            rmn.duration = 0.4f;
            rmn.startLifetime = 0.32f;
            rmn.startSpeed = 0f;
            rmn.simulationSpace = ParticleSystemSimulationSpace.World;
            rmn.maxParticles = 2;
            var rem = ring.emission; rem.enabled = false;
            var rsh = ring.shape; rsh.enabled = false;
            var rsol = ring.sizeOverLifetime; rsol.enabled = true;
            rsol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.15f, 0, 4f), new Keyframe(1, 1f, 0.2f, 0)));
            var rcol = ring.colorOverLifetime; rcol.enabled = true; rcol.color = g;
            var rr = ring.GetComponent<ParticleSystemRenderer>();
            rr.sharedMaterial = ringMaterial != null ? ringMaterial : flashMaterial;
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rr.receiveShadows = false;

            return new Burst { sparks = sparks, flash = flash, ring = ring };
        }
    }
}
