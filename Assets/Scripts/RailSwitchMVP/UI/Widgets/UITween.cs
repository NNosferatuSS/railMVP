using System.Collections;
using UnityEngine;

namespace RailSwitchMVP.UI.Widgets
{
    /// <summary>
    /// Tweens de UI em coroutine — sem dependência de DOTween.
    ///
    /// Tudo usa <c>Time.unscaledDeltaTime</c> de propósito: o jogo roda em
    /// slow-mo no death cam e em timeScale 0 no pause/game over, e a UI deve
    /// continuar respondendo na velocidade normal nesses momentos.
    ///
    /// A escala base é sempre Vector3.one (não a escala corrente) — se um punch
    /// for reiniciado no meio de outro, capturar a escala atual comporia os
    /// valores e o elemento cresceria sem parar.
    /// </summary>
    public static class UITween
    {
        public static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

        /// <summary>Ease com overshoot leve — dá o "pop" sem exagero.</summary>
        public static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float m = x - 1f;
            return 1f + c3 * m * m * m + c1 * m * m;
        }

        /// <summary>Cresce até (1 + amount) e volta pra 1. Bom pra "valor mudou".</summary>
        public static IEnumerator PunchScale(Transform t, float amount = 0.18f, float duration = 0.22f)
        {
            if (t == null) yield break;

            float up = duration * 0.35f;
            float down = Mathf.Max(0.01f, duration - up);

            float e = 0f;
            while (e < up)
            {
                e += Time.unscaledDeltaTime;
                float k = EaseOutCubic(Mathf.Clamp01(e / up));
                if (t == null) yield break;
                t.localScale = Vector3.one * (1f + amount * k);
                yield return null;
            }

            e = 0f;
            while (e < down)
            {
                e += Time.unscaledDeltaTime;
                float k = EaseOutCubic(Mathf.Clamp01(e / down));
                if (t == null) yield break;
                t.localScale = Vector3.one * (1f + amount * (1f - k));
                yield return null;
            }

            if (t != null) t.localScale = Vector3.one;
        }

        /// <summary>Aparece de `from` até 1 com overshoot. Bom pra "item entrou na tela".</summary>
        public static IEnumerator PopIn(Transform t, float from = 0.55f, float duration = 0.26f)
        {
            if (t == null) yield break;

            t.localScale = Vector3.one * from;
            float e = 0f;
            while (e < duration)
            {
                e += Time.unscaledDeltaTime;
                float k = EaseOutBack(Mathf.Clamp01(e / duration));
                if (t == null) yield break;
                t.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, k);
                yield return null;
            }

            if (t != null) t.localScale = Vector3.one;
        }

        /// <summary>Desliza no eixo Y até a posição de repouso, com fade. Bom pra banner.</summary>
        public static IEnumerator SlideFadeIn(RectTransform rt, CanvasGroup cg,
            float fromOffsetY = -40f, float duration = 0.24f)
        {
            if (rt == null) yield break;

            Vector2 rest = rt.anchoredPosition;
            float e = 0f;
            while (e < duration)
            {
                e += Time.unscaledDeltaTime;
                float k = EaseOutCubic(Mathf.Clamp01(e / duration));
                if (rt == null) yield break;
                rt.anchoredPosition = rest + new Vector2(0f, Mathf.LerpUnclamped(fromOffsetY, 0f, k));
                if (cg != null) cg.alpha = k;
                yield return null;
            }

            if (rt != null) rt.anchoredPosition = rest;
            if (cg != null) cg.alpha = 1f;
        }
    }
}
