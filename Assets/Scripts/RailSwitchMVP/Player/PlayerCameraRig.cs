using System.Collections;
using UnityEngine;
using RailSwitchMVP.Config;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.Player
{
    /// <summary>
    /// Rig de câmera independente do player.
    /// - Tilt configurável (0-90°)
    /// - Look-ahead (target deslocado pra frente)
    /// - Zoom adaptativo: altura sobe conforme playerSpeed sobe
    /// - Smoothing de posição (Lerp configurável)
    /// - Shake API com 3 presets (Light/Medium/Heavy) + custom
    /// - Death cam: slow-mo + zoom-in + shake em OnGameOver
    /// - Auto-shake leve em tier change
    ///
    /// Substituível por Cinemachine no futuro — esse rig cobre os
    /// fundamentos sem dependência externa.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PlayerCameraRig : MonoBehaviour
    {
        public static PlayerCameraRig Instance { get; private set; }

        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private RailGenConfig config;
        [SerializeField] private DifficultyManager difficulty;

        [Header("Lateral Follow (experimento)")]
        [Tooltip("Quando false, X da câmera fica travado em anchoredX e ignora switch de lanes. Toggle ao vivo no Inspector.")]
        [SerializeField] private bool followLateral = false;

        [Tooltip("X mundial usado quando followLateral=false. 0 = centro do grid (lane central com globalMaxLanes ímpar).")]
        [SerializeField] private float anchoredX = 0f;

        [Header("Runtime (read-only)")]
        [Tooltip("Distância atual da câmera ao foco (lerp suave do cameraZoom do tier ativo).")]
        [SerializeField] private float currentZoom;

        // Base position usada pra smoothing — separada do transform.position
        // pra que o shake (somado depois) não interfira no Lerp.
        Vector3 _basePos;
        bool _hasBasePos;

        // Shake state
        float _shakeTimer;
        float _shakeDuration;
        float _shakeIntensity;
        float _shakeSeed;

        // Death cam factor 0..1, lerped over deathCamDuration via coroutine
        float _deathCamFactor;
        bool _deathSequenceRunning;

        // Skip o primeiro OnTierChanged (disparado no ResetDifficulty/init).
        bool _seenInitialTier;

        Camera _cam;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Cena pode ter dois rigs (debug) — primeiro vence.
                Debug.LogWarning("[PlayerCameraRig] Multiple instances; destroying this one.");
                Destroy(this);
                return;
            }
            Instance = this;
            _cam = GetComponent<Camera>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnsubscribeEvents();
        }

        void Start()
        {
            if (player == null)
                Debug.LogError("[PlayerCameraRig] Player transform not assigned.");
            if (config == null)
                Debug.LogError("[PlayerCameraRig] RailGenConfig not assigned.");
            if (difficulty == null)
                Debug.LogError("[PlayerCameraRig] DifficultyManager not assigned.");

            // Só perspective (ortho removida).
            if (_cam != null) _cam.orthographic = false;

            currentZoom = difficulty != null ? difficulty.CurrentTier.cameraZoom : 12f;

            SubscribeEvents();
        }

        void SubscribeEvents()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver += HandleGameOver;
            if (difficulty != null)
                difficulty.OnTierChanged += HandleTierChanged;
        }

        void UnsubscribeEvents()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver -= HandleGameOver;
            if (difficulty != null)
                difficulty.OnTierChanged -= HandleTierChanged;
        }

        // ============ Event handlers ============

        void HandleGameOver(GameOverReason reason)
        {
            // Shake forte só pra impacto físico; outros death cam sem shake.
            if (reason == GameOverReason.HitObstacle)
                ShakeHeavy();
            StartCoroutine(DeathCamSequence());
        }

        void HandleTierChanged(Config.DifficultyTier tier)
        {
            if (!_seenInitialTier) { _seenInitialTier = true; return; }
            ShakeLight();
        }

        // ============ Camera update ============

        void LateUpdate()
        {
            if (player == null || config == null || difficulty == null) return;

            var tier = difficulty.CurrentTier;

            float targetX = followLateral ? player.position.x : anchoredX;
            Vector3 playerPos = new Vector3(targetX, player.position.y, player.position.z);

            // Zoom = distância da câmera ao player. Um valor único por tier (lerp suave nas
            // trocas), escalado pelo multiplier global. Maior = mais longe.
            float globalZoom = Mathf.Max(0.01f, config.cameraZoomGlobalMultiplier);
            float targetZoom = tier.cameraZoom * globalZoom;
            currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * config.cameraZoomSpeed);

            // Tilt e FOV: globais do RailGenConfig por padrão; o tier pode sobrescrever
            // (overrideCameraAngle) pra tunar ângulo/lente por dificuldade.
            float baseTilt = tier.overrideCameraAngle ? tier.cameraTilt : config.cameraTilt;
            float fov      = tier.overrideCameraAngle ? tier.cameraFieldOfView : config.cameraFieldOfView;

            // Death cam: aproxima (reduz zoom) e aumenta o tilt.
            float effectiveZoom = Mathf.Max(0.1f, currentZoom - config.deathCamZoomDelta * _deathCamFactor);
            float effectiveTilt = baseTilt + config.deathCamTiltDelta * _deathCamFactor;

            // FOV (perspective) — por tier se override, senão global.
            if (_cam != null && !Mathf.Approximately(_cam.fieldOfView, fov))
                _cam.fieldOfView = fov;

            Quaternion rot = Quaternion.Euler(effectiveTilt, 0f, 0f);
            Vector3 viewDir = rot * Vector3.forward;
            Vector3 camUp = rot * Vector3.up;

            // LookAhead COMPENSADO: a câmera mira num ponto deslocado do player na direção
            // camUp (pra baixo na tela → vê-se mais à frente), MAS o deslocamento ESCALA com
            // a distância (effectiveZoom). Como o ângulo do player = atan(offset/distância) e
            // offset = distância·k, o ângulo fica CONSTANTE → o player aparece sempre no mesmo
            // ponto da tela em qualquer zoom/tier. k = config.cameraLookAhead (0 = centro).
            Vector3 focus = playerPos + camUp * (effectiveZoom * config.cameraLookAhead);
            Vector3 desiredPos = focus - viewDir * effectiveZoom;

            // Smoothing: lerp _basePos pra desiredPos. Primeira vez teleporta.
            if (!_hasBasePos)
            {
                _basePos = desiredPos;
                _hasBasePos = true;
            }
            else if (config.cameraPositionSmoothing <= 0f)
            {
                _basePos = desiredPos;
            }
            else
            {
                _basePos = Vector3.Lerp(_basePos, desiredPos, Time.deltaTime * config.cameraPositionSmoothing);
            }

            // Aplica shake como offset depois do smoothing — não polui o base.
            Vector3 shakeOffset = ComputeShakeOffset();
            transform.position = _basePos + shakeOffset;
            transform.rotation = rot;
        }

        // ============ Shake ============

        Vector3 ComputeShakeOffset()
        {
            if (_shakeTimer <= 0f) return Vector3.zero;
            _shakeTimer -= Time.unscaledDeltaTime;

            // Falloff linear pra suavizar o fim.
            float t = Mathf.Clamp01(_shakeTimer / Mathf.Max(0.0001f, _shakeDuration));
            float magnitude = _shakeIntensity * t;

            float nx = Mathf.PerlinNoise(_shakeSeed + Time.unscaledTime * 25f, 0f) * 2f - 1f;
            float ny = Mathf.PerlinNoise(0f, _shakeSeed + Time.unscaledTime * 25f) * 2f - 1f;
            return new Vector3(nx, ny, 0f) * magnitude;
        }

        public void Shake(float intensity, float duration)
        {
            // Se já tem shake rolando, escolhe o maior (não acumula intensity).
            if (intensity * duration <= _shakeIntensity * _shakeTimer) return;
            _shakeIntensity = intensity;
            _shakeDuration = duration;
            _shakeTimer = duration;
            _shakeSeed = Random.value * 100f;
        }

        public void ShakeLight()
        {
            if (config == null) { Shake(0.15f, 0.15f); return; }
            Shake(config.shakeLightIntensity, config.shakeLightDuration);
        }
        public void ShakeMedium()
        {
            if (config == null) { Shake(0.3f, 0.25f); return; }
            Shake(config.shakeMediumIntensity, config.shakeMediumDuration);
        }
        public void ShakeHeavy()
        {
            if (config == null) { Shake(0.6f, 0.5f); return; }
            Shake(config.shakeHeavyIntensity, config.shakeHeavyDuration);
        }

        // ============ Death cam ============

        // ============ Shield impact slow-mo ============

        Coroutine _impactCo;

        /// <summary>
        /// Slow-mo breve e reversível de impacto (GLOBAL — timeScale). Dá peso ao
        /// "ufa, escapei". timeScale mergulha pra shieldImpactSlowMo e faz lerp de
        /// volta a 1. Acompanha um shake médio. Não atropela o game over.
        ///
        /// ⚠️ DISPONÍVEL PRA REUSO, atualmente não plugado em nada. Antes era
        /// chamado no Barrier+Shield; isso virou desaceleração de velocidade COM
        /// decay no PlayerRailRider.ApplyImpactSlowdown (perda de momentum, sem
        /// mexer no timeScale). Este efeito global continua aqui pra eventual uso
        /// (revive, quase-acidente, momentos dramáticos).
        /// </summary>
        public void ImpactSlowmo()
        {
            if (config == null) return;
            if (GameManager.Instance != null && !GameManager.Instance.IsActive) return;
            if (_impactCo != null) StopCoroutine(_impactCo);
            _impactCo = StartCoroutine(ImpactSlowmoRoutine());
            ShakeMedium();
        }

        IEnumerator ImpactSlowmoRoutine()
        {
            float scale = Mathf.Clamp(config.shieldImpactSlowMo, 0.05f, 1f);
            float dur = Mathf.Max(0.05f, config.shieldImpactDuration);
            float hold = dur * 0.35f;
            float recover = dur - hold;

            Time.timeScale = scale;
            yield return new WaitForSecondsRealtime(hold);

            float t = 0f;
            while (t < recover)
            {
                // Se um game over começou no meio, deixa a death cam controlar o timeScale.
                if (GameManager.Instance != null && !GameManager.Instance.IsActive) { _impactCo = null; yield break; }
                t += Time.unscaledDeltaTime;
                Time.timeScale = Mathf.Lerp(scale, 1f, recover > 0f ? t / recover : 1f);
                yield return null;
            }
            Time.timeScale = 1f;
            _impactCo = null;
        }

        IEnumerator DeathCamSequence()
        {
            if (_deathSequenceRunning) yield break;
            _deathSequenceRunning = true;

            float duration = config != null ? config.deathCamDuration : 1f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _deathCamFactor = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                yield return null;
            }
            _deathCamFactor = 1f;
            _deathSequenceRunning = false;
        }
    }
}
