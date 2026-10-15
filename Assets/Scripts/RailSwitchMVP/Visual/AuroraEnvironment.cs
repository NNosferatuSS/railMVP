using UnityEngine;

namespace RailSwitchMVP.Visual
{
    /// <summary>
    /// Ambiente da direção Aurora. Só cosmético — não lê nem altera gameplay.
    ///
    /// - Publica os globais de fog usados pelo shader Rail/Aurora/Surface.
    /// - <c>followRoot</c>: acompanha o Z da câmera (véus de aurora ficam "parados"
    ///   no fundo; o movimento vem do próprio shader).
    /// - <c>passingRoot</c>: filhos espaçados em Z que ficam parados no mundo e são
    ///   reciclados pra frente quando saem por trás da câmera — é o que dá a
    ///   sensação de velocidade sem mexer na câmera.
    /// </summary>
    [ExecuteAlways]
    public class AuroraEnvironment : MonoBehaviour
    {
        [Header("Fog (shader global)")]
        [SerializeField] private Color fogColor = new Color(0.02f, 0.06f, 0.16f, 1f);
        [Tooltip("Distância da câmera onde o fog começa / satura.")]
        [SerializeField] private float fogStart = 30f;
        [SerializeField] private float fogEnd = 120f;
        [Tooltip("Altura (Y mundo) onde o fog de profundidade começa / satura. Pistas ficam em y≈0.")]
        [SerializeField] private float heightFogTop = -2f;
        [SerializeField] private float heightFogBottom = -45f;

        [Header("Luz (direção dos dois tons do shader Surface)")]
        [Tooltip("Rotação da 'luz' de arte (como uma Directional Light). Independe das luzes da cena.")]
        [SerializeField] private Vector3 lightEuler = new Vector3(52f, -28f, 0f);

        [Header("Camadas")]
        [Tooltip("Câmera seguida. Vazio = Camera.main.")]
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private Transform followRoot;
        [SerializeField] private Transform passingRoot;
        [Tooltip("Espaçamento em Z entre os filhos do passingRoot (reciclagem).")]
        [SerializeField] private float passingSpacing = 45f;
        [Tooltip("Quanto atrás da câmera um elemento pode ficar antes de ir pra frente.")]
        [SerializeField] private float recycleBehind = 30f;

        static readonly int FogColorId = Shader.PropertyToID("_AuroraFogColor");
        static readonly int FogParamsId = Shader.PropertyToID("_AuroraFogParams");
        static readonly int LightDirId = Shader.PropertyToID("_AuroraLightDir");

        void OnEnable() => PushGlobals();
        void OnValidate() => PushGlobals();

        void LateUpdate()
        {
            PushGlobals();
            if (cameraTarget == null && Camera.main != null) cameraTarget = Camera.main.transform;
            if (cameraTarget != null) Tick(cameraTarget.position);
        }

        public void PushGlobals()
        {
            Shader.SetGlobalColor(FogColorId, fogColor);
            Shader.SetGlobalVector(FogParamsId, new Vector4(fogStart, fogEnd, heightFogTop, heightFogBottom));
            Vector3 toLight = -(Quaternion.Euler(lightEuler) * Vector3.forward);
            Shader.SetGlobalVector(LightDirId, new Vector4(toLight.x, toLight.y, toLight.z, 1f));
        }

        /// <summary>Atualiza camadas pra uma posição de câmera (público pro preview do editor).</summary>
        public void Tick(Vector3 camPos)
        {
            if (followRoot != null)
            {
                var p = followRoot.position;
                followRoot.position = new Vector3(p.x, p.y, camPos.z);
            }

            if (passingRoot == null || passingRoot.childCount == 0) return;
            float loop = passingSpacing * passingRoot.childCount;
            for (int i = 0; i < passingRoot.childCount; i++)
            {
                var c = passingRoot.GetChild(i);
                var p = c.position;
                // Pra trás da câmera demais → vai pro fim da fila; muito à frente
                // (câmera pulou/reset) → volta.
                while (p.z < camPos.z - recycleBehind) p.z += loop;
                while (p.z > camPos.z - recycleBehind + loop) p.z -= loop;
                c.position = p;
            }
        }
    }
}
