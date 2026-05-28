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

        [Header("Lateral Follow (experimento)")]
        [Tooltip("Quando false, X da câmera fica travado em anchoredX e ignora switch de lanes. Toggle ao vivo no Inspector.")]
        [SerializeField] private bool followLateral = false;

        [Tooltip("X mundial usado quando followLateral=false. 0 = centro do grid (lane central com globalMaxLanes ímpar).")]
        [SerializeField] private float anchoredX = 0f;

        [Header("Runtime (read-only)")]
        [SerializeField] private float currentZoom;
        [SerializeField] private float currentOrthoSize;

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

            if (difficulty != null)
            {
                currentZoom = difficulty.CurrentTier.cameraZoomMin;
                currentOrthoSize = difficulty.CurrentTier.cameraOrthoSizeMin;
            }
            else
            {
                currentZoom = 15f;
                currentOrthoSize = 6f;
            }

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
            bool isOrtho = _cam != null && _cam.orthographic;

            // LookAhead escala com playerSpeed pra manter horizonte temporal constante
            // (mesmos ~N segundos à frente em todos os tiers, em vez de N unidades fixas).
            // Referência: speedAtMinZoom (o "baseline" do mapping de altura).
            // Funciona idêntico em perspective e ortho.
            float speedRatio = speedAtMinZoom > 0f ? (tier.playerSpeed / speedAtMinZoom) : 1f;
            float effectiveLookAhead = config.cameraLookAhead * speedRatio;
            float targetX = followLateral ? player.position.x : anchoredX;
            Vector3 anchorBase = new Vector3(targetX, player.position.y, player.position.z);
            Vector3 target = anchorBase + Vector3.forward * effectiveLookAhead;

            // Per-tier interp por speed — usado nos dois modos (cada um lê seus campos).
            float speedFactor = Mathf.InverseLerp(speedAtMinZoom, speedAtMaxZoom, tier.playerSpeed);
            float globalZoom = Mathf.Max(0.01f, config.cameraZoomGlobalMultiplier);

            if (isOrtho)
            {
                // === ORTHOGRAPHIC: zoom real é via orthographicSize ===
                // Per-tier ortho size × globalZoom (multiplier vira zoom verdadeiro aqui).
                float targetOrtho = Mathf.Lerp(tier.cameraOrthoSizeMin, tier.cameraOrthoSizeMax, speedFactor) * globalZoom;
                currentOrthoSize = Mathf.Lerp(currentOrthoSize, targetOrtho, Time.deltaTime * config.cameraZoomSpeed);
                _cam.orthographicSize = Mathf.Max(0.1f, currentOrthoSize - config.deathCamOrthoSizeDelta * _deathCamFactor);
                // currentZoom (altura Y) ainda usado pra posicionar — não afeta zoom visual em ortho.
                float targetZoomY = Mathf.Lerp(tier.cameraZoomMin, tier.cameraZoomMax, speedFactor);
                currentZoom = Mathf.Lerp(currentZoom, targetZoomY, Time.deltaTime * config.cameraZoomSpeed);
            }
            else
            {
                // === PERSPECTIVE: FOV + altura/distance ===
                if (_cam != null && !Mathf.Approximately(_cam.fieldOfView, config.cameraFieldOfView))
                    _cam.fieldOfView = config.cameraFieldOfView;
                float targetZoom = Mathf.Lerp(tier.cameraZoomMin, tier.cameraZoomMax, speedFactor);
                currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * config.cameraZoomSpeed);
            }

            // Death cam aplica delta no distance/tilt (afeta posição nos dois modos).
            float effectiveDistance = config.cameraDistance - config.deathCamZoomDelta * _deathCamFactor;
            float effectiveTilt = config.cameraTilt + config.deathCamTiltDelta * _deathCamFactor;

            // Distance escala com speedRatio junto com lookAhead — caso contrário, ao
            // crescer lookAhead sem crescer distance, a câmera ultrapassa o player
            // (camZ = player + lookAhead − distance fica positivo). Vale nos dois modos:
            // em ortho não afeta zoom visual, mas mantém invariante "câmera atrás do player".
            float effectiveDistanceScaled = effectiveDistance * speedRatio;

            // Em ortho, globalZoom já foi aplicado no orthoSize — não escalar posição também.
            // Em perspective, globalZoom escala distance+altura (preserva ângulo).
            float positionScale = isOrtho ? 1f : globalZoom;
            Vector3 desiredPos = target
                + Vector3.back * (effectiveDistanceScaled * positionScale)
                + Vector3.up * (currentZoom * positionScale);

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
