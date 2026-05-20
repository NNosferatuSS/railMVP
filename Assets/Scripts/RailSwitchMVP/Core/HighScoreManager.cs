using UnityEngine;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Persiste high scores entre sessões via PlayerPrefs.
    /// 4 records trackeados: distance (m), coins (count), tier (index), time (segundos).
    ///
    /// Carrega no Awake. GameOverController chama TryUpdate ao morrer.
    /// Tem ResetAll pra debug.
    /// </summary>
    public class HighScoreManager : MonoBehaviour
    {
        public static HighScoreManager Instance { get; private set; }

        const string KeyDistance = "RailMVP.BestDistance";
        const string KeyCoins    = "RailMVP.BestCoins";
        const string KeyTier     = "RailMVP.BestTier";
        const string KeyTime     = "RailMVP.BestTime";

        [Header("Runtime (read-only)")]
        [SerializeField] private int bestDistance;
        [SerializeField] private int bestCoins;
        [SerializeField] private int bestTier;
        [SerializeField] private float bestTime;

        public int BestDistance => bestDistance;
        public int BestCoins    => bestCoins;
        public int BestTier     => bestTier;
        public float BestTime   => bestTime;

        /// <summary>
        /// Resultado de tentativa de update — diz quais records foram batidos.
        /// </summary>
        public struct RecordResult
        {
            public bool distance, coins, tier, time;
            public bool Any => distance || coins || tier || time;
        }

        public event System.Action<RecordResult> OnRecordsUpdated;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            LoadFromPrefs();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void LoadFromPrefs()
        {
            bestDistance = PlayerPrefs.GetInt(KeyDistance, 0);
            bestCoins    = PlayerPrefs.GetInt(KeyCoins, 0);
            bestTier     = PlayerPrefs.GetInt(KeyTier, 0);
            bestTime     = PlayerPrefs.GetFloat(KeyTime, 0f);
            Debug.Log($"[HighScore] Loaded best: dist={bestDistance}m coins={bestCoins} tier={bestTier} time={bestTime:F1}s");
        }

        /// <summary>
        /// Atualiza bests com stats de uma run. Salva em PlayerPrefs (e dispara
        /// OnRecordsUpdated) se qualquer record foi batido.
        /// </summary>
        public RecordResult TryUpdate(int distance, int coins, int tier, float time)
        {
            var result = new RecordResult();

            if (distance > bestDistance)
            {
                bestDistance = distance;
                PlayerPrefs.SetInt(KeyDistance, bestDistance);
                result.distance = true;
            }
            if (coins > bestCoins)
            {
                bestCoins = coins;
                PlayerPrefs.SetInt(KeyCoins, bestCoins);
                result.coins = true;
            }
            if (tier > bestTier)
            {
                bestTier = tier;
                PlayerPrefs.SetInt(KeyTier, bestTier);
                result.tier = true;
            }
            if (time > bestTime)
            {
                bestTime = time;
                PlayerPrefs.SetFloat(KeyTime, bestTime);
                result.time = true;
            }

            if (result.Any)
            {
                PlayerPrefs.Save();
                Debug.Log($"[HighScore] Record(s) broken: dist={result.distance} coins={result.coins} tier={result.tier} time={result.time}");
                OnRecordsUpdated?.Invoke(result);
            }

            return result;
        }

        /// <summary>
        /// Apaga todos os records (debug). Sem confirmação — quem chama tem responsabilidade.
        /// </summary>
        public void ResetAll()
        {
            bestDistance = 0;
            bestCoins = 0;
            bestTier = 0;
            bestTime = 0f;
            PlayerPrefs.DeleteKey(KeyDistance);
            PlayerPrefs.DeleteKey(KeyCoins);
            PlayerPrefs.DeleteKey(KeyTier);
            PlayerPrefs.DeleteKey(KeyTime);
            PlayerPrefs.Save();
            Debug.Log("[HighScore] All records reset.");
        }
    }
}
