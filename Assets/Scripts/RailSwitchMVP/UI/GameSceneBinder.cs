using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using RailSwitchMVP.Meta;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Fica no Canvas_HUD. Faz os hookups cross-canvas que o build dos prefabs
    /// não consegue fazer sozinho (cada prefab é gerado isolado):
    ///
    ///   HUD.pauseButton  → PauseController.Open (Canvas_Pause vive em outro prefab)
    ///   GameOver footer HOME       → SceneManager.LoadScene(HomeScene)
    ///   GameOver footer LEADERBOARD → SceneManager.LoadScene(HomeScene)
    ///                                 (deep-link p/ tab Ranks fica p/ depois)
    ///   GameOver footer SHARE       → no-op (stub silencioso)
    ///
    /// Usa uma coroutine que espera 1 frame antes de resolver os controllers —
    /// os canvases dos overlays podem ser instanciados no mesmo Awake do HUD e
    /// FindFirstObjectByType pode não pegar no primeiro tick.
    /// </summary>
    public class GameSceneBinder : MonoBehaviour
    {
        [SerializeField] private HUDViewController hud;

        void Start()
        {
            if (hud == null) hud = GetComponent<HUDViewController>();
            StartCoroutine(BindNextFrame());
        }

        IEnumerator BindNextFrame()
        {
            // Um frame de folga pra qualquer prefab overlay já ter dado Awake/Start.
            yield return null;

            WirePauseButton();
            WireGameOverFooter();
        }

        void WirePauseButton()
        {
            if (hud == null) { Debug.LogWarning("[GameSceneBinder] HUD ref missing."); return; }
            var pause = FindFirstObjectByType<PauseController>(FindObjectsInactive.Include);
            if (pause == null)
            {
                Debug.LogWarning("[GameSceneBinder] PauseController not in scene — pause button sem ação. Arraste Canvas_Pause.prefab.");
                return;
            }
            var pauseBtn = ReflectField<Button>(hud, "pauseButton");
            if (pauseBtn == null)
            {
                Debug.LogWarning("[GameSceneBinder] HUD.pauseButton null — Canvas_HUD.prefab desatualizado?");
                return;
            }
            pauseBtn.onClick.RemoveListener(pause.Open);
            pauseBtn.onClick.AddListener(pause.Open);
        }

        void WireGameOverFooter()
        {
            var go = FindFirstObjectByType<GameOverController>(FindObjectsInactive.Include);
            if (go == null) return;
            var canvas = go.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var footer = FindByName(canvas.transform, "Footer");
            if (footer == null) return;

            // HOME NÃO é hookado aqui: o próprio GameOverController já recebe esse
            // botão em `homeButton` e chama GoHome(), que commita coins/XP/runs no
            // PlayerDataManager antes de trocar de cena. Hookar aqui (com
            // RemoveAllListeners) apagaria esse listener e a run seria perdida.
            HookFooter(footer, "LEADERBOARD", () => go.GoHome());   // sem deep-link ainda
            HookFooter(footer, "SHARE",       () => Debug.Log("[GameSceneBinder] Share stub — implementar Native Share depois."));
        }

        static void HookFooter(Transform footer, string childName, System.Action onClick)
        {
            var t = FindByName(footer, childName);
            if (t == null) return;
            var btn = t.GetComponent<Button>();
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onClick());
        }

        static Transform FindByName(Transform root, string name)
        {
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                if (tr.name == name) return tr;
            return null;
        }

        static T ReflectField<T>(object target, string field) where T : class
        {
            if (target == null) return null;
            var t = target.GetType();
            while (t != null)
            {
                var f = t.GetField(field, System.Reflection.BindingFlags.Instance |
                                          System.Reflection.BindingFlags.NonPublic |
                                          System.Reflection.BindingFlags.Public);
                if (f != null) return f.GetValue(target) as T;
                t = t.BaseType;
            }
            return null;
        }
    }
}
