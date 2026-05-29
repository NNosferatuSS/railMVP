using UnityEngine;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.PowerUps
{
    /// <summary>
    /// Caixa surpresa: ao coletar, sorteia um power-up aleatório do pool do tier
    /// atual (ponderado pelos pesos) e o concede via PowerUpManager.GrantByType.
    /// Estilo "? box" de Mario Kart. O feedback de qual saiu vem dos indicadores
    /// normais do HUD (o power-up concedido aparece lá).
    ///
    /// Exclui a própria MysteryBox do sorteio (não dá mystery → mystery) e os
    /// debuffs (que vêm de hazards, não do powerUpPool).
    /// </summary>
    public class MysteryBoxPickup : PowerUpBase
    {
        // Usado se o tier não tiver powerUpPool válido (não deveria, mas é seguro).
        static readonly PowerUpType[] Fallback =
        {
            PowerUpType.Shield, PowerUpType.Magnet, PowerUpType.SlowDown, PowerUpType.DoubleCoins
        };

        protected override void Activate()
        {
            var pm = PowerUpManager.Instance;
            if (pm == null) return;
            pm.GrantByType(PickType());
        }

        PowerUpType PickType()
        {
            var dm = DifficultyManager.Instance;
            var pool = dm != null ? dm.CurrentTier.powerUpPool : null;

            if (pool == null || pool.entries == null || pool.entries.Count == 0)
                return Fallback[Random.Range(0, Fallback.Length)];

            // Soma de pesos (ignora MysteryBox e pesos <= 0).
            float total = 0f;
            foreach (var e in pool.entries)
                if (e.weight > 0f && e.type != PowerUpType.MysteryBox) total += e.weight;

            if (total <= 0f)
                return Fallback[Random.Range(0, Fallback.Length)];

            float r = Random.value * total;
            float acc = 0f;
            foreach (var e in pool.entries)
            {
                if (e.weight <= 0f || e.type == PowerUpType.MysteryBox) continue;
                acc += e.weight;
                if (r < acc) return e.type;
            }

            // Fallback numérico (arredondamento) — último válido.
            for (int i = pool.entries.Count - 1; i >= 0; i--)
                if (pool.entries[i].weight > 0f && pool.entries[i].type != PowerUpType.MysteryBox)
                    return pool.entries[i].type;

            return Fallback[Random.Range(0, Fallback.Length)];
        }
    }
}
