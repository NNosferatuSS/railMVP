using UnityEngine;

namespace RailSwitchMVP.Collectibles
{
    /// <summary>
    /// Singleton acumulando moedas coletadas. Sem UI no MVP, mas o dado existe
    /// e é logado a cada coleta.
    /// </summary>
    public class CoinManager : MonoBehaviour
    {
        public static CoinManager Instance { get; private set; }

        [SerializeField] private int total;
        public int Total => total;

        public event System.Action<int> OnCoinsChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void AddCoins(int amount)
        {
            total += amount;
            OnCoinsChanged?.Invoke(total);
            // Log removido — spammava com 10+ moedas por tile crítico.
            // HUD coin counter já dá feedback visual contínuo.
        }

        public void ResetTotal()
        {
            total = 0;
            OnCoinsChanged?.Invoke(0);
        }
    }
}
