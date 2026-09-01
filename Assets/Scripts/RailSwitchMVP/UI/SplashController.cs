using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using RailSwitchMVP.Meta;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Splash / Loading (S1). Mostra logo + barra + tip por N segundos (ou até um
    /// callback do bootstrap) e transiciona pra HomeScene.
    /// </summary>
    public class SplashController : MonoBehaviour
    {
        [SerializeField] private Image progressFill;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private string[] tips = new[]
        {
            "READ TWO SWITCHES AHEAD.",
            "COLLECT COINS ON THE INSIDE LANE.",
            "SHIELD BREAKS THE FIRST HAZARD.",
            "MAGNET PULLS FROM 3 TILES AWAY.",
        };
        [SerializeField] private float minDuration = 1.6f;
        [SerializeField] private string nextScene = SceneNames.Home;

        void Start()
        {
            if (tipText != null && tips != null && tips.Length > 0)
                tipText.text = tips[Random.Range(0, tips.Length)];
            StartCoroutine(LoadCo());
        }

        IEnumerator LoadCo()
        {
            float t = 0f;
            while (t < minDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / minDuration);
                if (progressFill != null) progressFill.fillAmount = p;
                if (progressText != null) progressText.text = Mathf.RoundToInt(p * 100f) + "%";
                yield return null;
            }
            if (!string.IsNullOrEmpty(nextScene))
                SceneManager.LoadScene(nextScene);
        }
    }
}
