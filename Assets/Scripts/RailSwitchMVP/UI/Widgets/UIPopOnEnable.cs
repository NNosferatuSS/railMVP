using UnityEngine;

namespace RailSwitchMVP.UI.Widgets
{
    /// <summary>
    /// Dá um pop-in sutil sempre que o objeto é ativado. Usado nos painéis de
    /// overlay (Game Over, Pause) pra que não apareçam "estalados" na tela.
    ///
    /// OnEnable dispara também quando um PAI é ativado, então funciona colocando
    /// isto no card interno enquanto o wrapper Content é quem faz o SetActive.
    /// </summary>
    public class UIPopOnEnable : MonoBehaviour
    {
        [Tooltip("Escala inicial. 1 = sem efeito. 0.9 é sutil; 0.6 é chamativo.")]
        [SerializeField, Range(0.1f, 1f)] private float from = 0.92f;

        [Tooltip("Duração em segundos (unscaled — funciona com o jogo pausado).")]
        [SerializeField, Range(0.05f, 1f)] private float duration = 0.20f;

        [Tooltip("Alvo do tween. Vazio = este transform.")]
        [SerializeField] private Transform target;

        void OnEnable()
        {
            var t = target != null ? target : transform;
            t.localScale = Vector3.one * from;
            StartCoroutine(UITween.PopIn(t, from, duration));
        }

        void OnDisable()
        {
            // Sai limpo — o próximo OnEnable recomeça do zero.
            var t = target != null ? target : transform;
            if (t != null) t.localScale = Vector3.one;
        }
    }
}
