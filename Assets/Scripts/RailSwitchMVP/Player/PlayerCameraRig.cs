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

        [Header("Zoom Mapping (Speed → Altura)")]
        [Tooltip("Velocidade do player que mapeia para o zoom mínimo (mais perto)")]
        [SerializeField] private float speedAtMinZoom = 8f;

        [Tooltip("Velocidade do player que mapeia para o zoom máximo (mais longe)")]
        [SerializeField] private float speedAtMaxZoom = 20f;

        [Header("Runtime (read-only)")]
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

            if (difficulty != null)
                currentZoom = difficulty.CurrentTier.cameraZoomMin;
            else
                currentZoom = 15f;

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

            // Target da câmera: player + offset pra frente (look-ahead)
            Vector3 target = player.position + Vector3.forward * config.cameraLookAhead;

            // Zoom alvo baseado na speed do tier atual
            var tier = difficulty.CurrentTier;
            float speedFactor = Mathf.InverseLerp(speedAtMinZoom, speedAtMaxZoom, tier.playerSpeed);
            float targetZoom = Mathf.Lerp(tier.cameraZoomMin, tier.cameraZoomMax, speedFactor);
            currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * config.cameraZoomSpeed);

            // Death cam aplica delta no distance/tilt
            float effectiveDistance = config.cameraDistance - config.deathCamZoomDelta * _deathCamFactor;
            float effectiveTilt = config.cameraTilt + config.deathCamTiltDelta * _deathCamFactor;

            Vector3 desiredPos = target
                + Vector3.back * effectiveDistance
                + Vector3.up * currentZoom;

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
            transform.rotation = Quaternion.Euler(effectiveTilt, 0f, 0f);
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
