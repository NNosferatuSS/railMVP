using UnityEngine;
using RailSwitchMVP.Config;

namespace RailSwitchMVP.Core
{
    /// <summary>Zonas temporárias da run (ZoneDirector).</summary>
    public enum ZoneKind { None, Rush, GoldRain, Eclipse }

    /// <summary>
    /// Modificadores temporários da run (Fever e Zonas), num lugar só.
    /// Os sistemas de gameplay LEEM daqui (velocidade, coins, ímã, geração);
    /// ComboFeverManager e ZoneDirector ESCREVEM. Tudo volta ao neutro a cada
    /// cena/run (Reset).
    /// </summary>
    public static class RunModifiers
    {
        /// <summary>Multiplica a velocidade do player (Rush).</summary>
        public static float SpeedMultiplier = 1f;

        /// <summary>Assistência de velocidade do tutorial (multiplica junto com SpeedMultiplier).</summary>
        public static float AssistSpeedMultiplier = 1f;

        /// <summary>Multiplicador de coins de zona (Rush). Fever tem o próprio.</summary>
        public static int ZoneCoinMultiplier = 1;

        /// <summary>Multiplicador de coins do Fever.</summary>
        public static int FeverCoinMultiplier = 1;

        /// <summary>Ímã forçado (Fever) — soma ao power-up Magnet.</summary>
        public static bool ForceMagnet;

        /// <summary>Chance de moeda bônus num decoy sem coins (por tile).</summary>
        public static float BonusCoinChance;

        public static ZoneKind ActiveZone = ZoneKind.None;

        /// <summary>Tutorial em andamento: sem queda, sem morte por obstáculo, sem hazards novos.</summary>
        public static bool TutorialProtection;

        /// <summary>Cometa (CometController): velocidade, raio do ímã, ativo e proteção contra queda.</summary>
        public static float CometSpeedMultiplier = 1f;
        public static float CometMagnetRadius;
        public static bool CometActive;
        public static bool CometProtection;

        /// <summary>Multiplicador combinado aplicado na coleta de coins (cap 4).</summary>
        public static int CoinMultiplier => Mathf.Min(4, Mathf.Max(1, ZoneCoinMultiplier) * Mathf.Max(1, FeverCoinMultiplier));

        /// <summary>Ajuste do tier usado pelo gerador nas rows geradas durante uma zona.</summary>
        public static DifficultyTier ModifyTier(DifficultyTier t)
        {
            if (TutorialProtection) t.hazardChanceOnDecoy = 0f;
            switch (ActiveZone)
            {
                case ZoneKind.GoldRain:
                    t.criticalCoinsMin = Mathf.Max(t.criticalCoinsMin, 3);
                    t.criticalCoinsMax = Mathf.Max(t.criticalCoinsMax, 3);
                    t.decoyCoinChance = Mathf.Max(t.decoyCoinChance, 0.85f);
                    t.decoyCoinsMin = Mathf.Max(t.decoyCoinsMin, 1);
                    t.decoyCoinsMax = Mathf.Max(t.decoyCoinsMax, 3);
                    t.hazardChanceOnDecoy = Mathf.Min(1f, t.hazardChanceOnDecoy * 1.15f);
                    break;
                case ZoneKind.Rush:
                    t.criticalCoinsMin = Mathf.Max(t.criticalCoinsMin, 2);
                    break;
                case ZoneKind.Eclipse:
                    // Leitura mais difícil, então menos lixo na pista.
                    t.hazardChanceOnDecoy *= 0.8f;
                    break;
            }
            return t;
        }

        public static void Reset()
        {
            SpeedMultiplier = 1f;
            AssistSpeedMultiplier = 1f;
            ZoneCoinMultiplier = 1;
            FeverCoinMultiplier = 1;
            ForceMagnet = false;
            BonusCoinChance = 0f;
            ActiveZone = ZoneKind.None;
            TutorialProtection = false;
            CometSpeedMultiplier = 1f;
            CometMagnetRadius = 0f;
            CometActive = false;
            CometProtection = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void DomainReset() => Reset();
    }
}
