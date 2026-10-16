using UnityEngine;

namespace RailSwitchMVP.Visual
{
    /// <summary>
    /// Movimento idle procedural (bob / giro / pulso de escala) pra coletáveis,
    /// power-ups e o crescente do player. Fase derivada da posição, então peças
    /// vizinhas não balançam em uníssono. Só mexe no próprio transform local.
    /// </summary>
    public class AuroraMotion : MonoBehaviour
    {
        [Header("Bob (sobe/desce)")]
        public float bobAmplitude = 0.06f;
        public float bobSpeed = 2.2f;

        [Header("Giro")]
        public Vector3 spinAxis = Vector3.up;
        [Tooltip("Graus por segundo.")]
        public float spinSpeed = 0f;

        [Header("Pulso de escala")]
        public float pulseAmount = 0f;
        public float pulseSpeed = 3f;

        [Header("Billboard")]
        [Tooltip("Sempre de frente pra câmera (igual ao BillboardSprite), aplicado antes do giro/wobble.")]
        public bool faceCamera = false;

        [Tooltip("OFF: só mexe em posição/escala (ex.: bob num Visual que já tem BillboardSprite).")]
        public bool affectRotation = true;

        [Header("Oscilação de rotação (wobble)")]
        public Vector3 wobbleAxis = Vector3.up;
        public float wobbleDegrees = 0f;
        public float wobbleSpeed = 1.5f;

        Vector3 _basePos;
        Vector3 _baseScale;
        Quaternion _baseRot;
        float _phase;
        float _spin;
        Camera _cam;

        void Awake()
        {
            _basePos = transform.localPosition;
            _baseScale = transform.localScale;
            _baseRot = transform.localRotation;
        }

        void OnEnable()
        {
            var p = transform.position;
            _phase = (p.x * 0.73f + p.z * 0.37f) % 6.2832f;
        }

        void LateUpdate()
        {
            float t = Time.time;
            if (bobAmplitude != 0f)
                transform.localPosition = _basePos + Vector3.up * (Mathf.Sin(t * bobSpeed + _phase) * bobAmplitude);

            if (pulseAmount != 0f)
                transform.localScale = _baseScale * (1f + Mathf.Sin(t * pulseSpeed + _phase) * pulseAmount);

            if (!affectRotation) return;
            Quaternion rot = _baseRot;
            if (faceCamera)
            {
                if (_cam == null) _cam = Camera.main;
                if (_cam != null)
                {
                    // Rotação em mundo de frente pra câmera; converte pra local do pai.
                    var world = Quaternion.LookRotation(_cam.transform.forward);
                    rot = transform.parent != null ? Quaternion.Inverse(transform.parent.rotation) * world : world;
                }
            }
            if (spinSpeed != 0f)
            {
                _spin = (_spin + spinSpeed * Time.deltaTime) % 360f;
                rot *= Quaternion.AngleAxis(_spin, spinAxis);
            }
            if (wobbleDegrees != 0f)
                rot *= Quaternion.AngleAxis(Mathf.Sin(t * wobbleSpeed + _phase) * wobbleDegrees, wobbleAxis);
            transform.localRotation = rot;
        }
    }
}
