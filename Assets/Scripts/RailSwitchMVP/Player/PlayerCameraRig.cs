using UnityEngine;
using RailSwitchMVP.Config;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.Player
{
    /// <summary>
    /// Rig de câmera independente do player.
    /// - Tilt configurável (inclinação)
    /// - Look-ahead (target deslocado pra frente)
    /// - Zoom adaptativo: altura sobe conforme playerSpeed sobe (mais visibilidade quando o jogo está rápido)
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PlayerCameraRig : MonoBehaviour
    {
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

        void Start()
        {
            if (player == null)
                Debug.LogError("[PlayerCameraRig] Player transform not assigned.");
            if (config == null)
                Debug.LogError("[PlayerCameraRig] RailGenConfig not assigned.");
            if (difficulty == null)
                Debug.LogError("[PlayerCameraRig] DifficultyManager not assigned.");

            // Inicializa zoom com o min do tier 0 pra evitar snap no primeiro frame
            if (difficulty != null)
                currentZoom = difficulty.CurrentTier.cameraZoomMin;
            else
                currentZoom = 15f;
        }

        void LateUpdate()
        {
            if (player == null || config == null || difficulty == null) return;

            // Target da câmera: player + offset pra frente (look-ahead)
            Vector3 target = player.position + Vector3.forward * config.cameraLookAhead;

            // Calcula zoom alvo baseado na speed do tier atual
            var tier = difficulty.CurrentTier;
            float speedFactor = Mathf.InverseLerp(speedAtMinZoom, speedAtMaxZoom, tier.playerSpeed);
            float targetZoom = Mathf.Lerp(tier.cameraZoomMin, tier.cameraZoomMax, speedFactor);

            // Smoothing do zoom
            currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * config.cameraZoomSpeed);

            // Posição final: atrás e acima do target
            Vector3 desiredPos = target
                + Vector3.back * config.cameraDistance
                + Vector3.up * currentZoom;

            transform.position = desiredPos;
            transform.rotation = Quaternion.Euler(config.cameraTilt, 0f, 0f);
        }
    }
}
