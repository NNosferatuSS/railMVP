using UnityEngine;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Setup global de performance — roda ANTES de qualquer cena via RuntimeInitializeOnLoadMethod.
    /// Não precisa estar em nenhuma cena.
    ///
    /// - Trava targetFrameRate (Android sem isso pode ficar oscilando entre 30-60).
    /// - Desliga vSync (vSync + targetFrameRate brigam).
    /// - Desabilita sleep timeout em mobile (a tela não apaga durante jogo).
    /// </summary>
    public static class PerformanceBootstrapper
    {
        /// <summary>Target FPS pra desktop e mobile high-end.</summary>
        public const int TargetFpsHigh = 60;

        /// <summary>Target FPS de fallback pra mobile low-end. Trocar em runtime via SetTarget.</summary>
        public const int TargetFpsLow = 30;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFpsHigh;

#if UNITY_ANDROID || UNITY_IOS
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
#endif
        }

        /// <summary>
        /// Trocar dinamicamente (ex: settings menu user-facing).
        /// </summary>
        public static void SetTarget(int fps)
        {
            Application.targetFrameRate = Mathf.Clamp(fps, 24, 120);
        }
    }
}
